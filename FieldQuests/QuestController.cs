using System;
using System.Collections.Generic;
using System.Text.Json;
using Game.PlayerOperations;
using Game.UI;
using Il2CppInterop.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InfernoProtocol.FieldQuests;

public sealed class QuestController : MonoBehaviour
{
    private const string RequestChannel = "FrankMods.FieldQuests.Request.v1";
    private const string ResponseChannel = "FrankMods.FieldQuests.Response.v1";
    private NetworkManager _network;
    private CustomMessagingManager _messages;
    private CustomMessagingManager.HandleNamedMessageDelegate _requestHandler, _responseHandler;
    private Il2CppSystem.Action _saveHandler;
    private readonly Dictionary<ulong, float> _lastRequest = new();
    private readonly QuestBoard _board = new();
    private QuestPanel _ui;
    private readonly SkinVisuals _skins = new();
    private bool _skinFailed;
    internal QuestService Service;
    internal bool BlockingInput => _ui != null && _ui.BlockingInput;
    private float _nextCheck, _nextPoll, _nextBoardAttempt;
    private float _nextHudPoll;
    private bool _failed, _wasListening;
    private readonly QuestInteraction _talk = new();
    private float _nextSpawnWarning;
    private QuestBoardLocation _hostLocation;
    private string _placementStatus = "Preparing the quest board";
    private System.Reflection.PropertyInfo _carpetMounted;
    private bool _checkedCarpet;
    public QuestController(IntPtr ptr) : base(ptr) { }
    private void Update()
    {
        try { Tick(); }
        catch (Exception e)
        {
            if (!_failed) QuestPlugin.LogSource.LogError("FieldQuests runtime: " + e);
            _failed = true;
            _ui?.Dispose(); _ui = null;
            _talk.Dispose();
        }
    }
    private void Tick()
    {
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + 0.5f;
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                if (_wasListening) Cleanup();
                return;
            }
            if (!Player.isLocalPlayerLoaded)
            {
                _ui?.Close(); _ui?.Tick();
                if (!BlockingInput) _talk.Release();
                return;
            }
            if (_network != network || !_wasListening)
            {
                Cleanup();
                _network = network;
                _messages = network.CustomMessagingManager;
                _requestHandler = DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(new Action<ulong, FastBufferReader>(OnRequest));
                _responseHandler = DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(new Action<ulong, FastBufferReader>(OnResponse));
                _messages.RegisterNamedMessageHandler(RequestChannel, _requestHandler);
                _messages.RegisterNamedMessageHandler(ResponseChannel, _responseHandler);
                _wasListening = true;
                QuestService.ValidateCatalog();
                if (network.IsServer)
                {
                    Service = new QuestService();
                    Service.Load();
                    _saveHandler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
                    {
                        try { Service?.SaveWithWorld(); }
                        catch (Exception e) { QuestPlugin.LogSource.LogError("Quest save failed: " + e); }
                    }));
                    SaveSystem.SaveManager.onSaveComplete += _saveHandler;
                }
                _ui = new QuestPanel(Send);
            }
        }
        if (!_wasListening || _failed) return;
        if (!Player.isLocalPlayerLoaded || Player.localPlayer == null)
        {
            _ui?.Close(); _ui?.Tick();
            if (!BlockingInput) _talk.Release();
            return;
        }
        if (_board.Root == null && Time.unscaledTime >= _nextBoardAttempt)
        {
            _nextBoardAttempt = Time.unscaledTime + 3f;
            try
            {
                if (_network.IsServer)
                {
                    _board.Create();
                    _placementStatus = _board.WaitingReason;
                }
                else if (_hostLocation?.IsValid == true)
                {
                    _board.Create(new Vector3(_hostLocation.X, _hostLocation.Y, _hostLocation.Z), _hostLocation.Yaw);
                    _placementStatus = _board.WaitingReason;
                }
                else
                {
                    _placementStatus = "Waiting for the host's quest board (host needs ContentPlus)";
                    SendText(RequestChannel, NetworkManager.ServerClientId, "locate:");
                }
            }
            catch (Exception e)
            {
                _placementStatus = "Quest board could not be created; retrying (details in game log)";
                if (Time.unscaledTime >= _nextSpawnWarning) QuestPlugin.LogSource.LogWarning("Quest board placement: " + e);
            }
            if (_board.Root == null && Time.unscaledTime >= _nextSpawnWarning)
            {
                _nextSpawnWarning = Time.unscaledTime + 30f;
                QuestPlugin.LogSource.LogWarning("Quest board: " + _placementStatus);
            }
        }
        bool gameplay = Application.isFocused && (!Cursor.visible || Cursor.lockState == CursorLockMode.Locked) &&
            !PlayerInventoryUI.isOpen && !SkillWheelManager.isOpen && !Player.localPlayer.isDead;
        bool near = gameplay && _board.CanRead();
        bool cosmetics = gameplay && _board.CanRead(true) &&
            Vector3.Distance(Player.localTransform.position, _board.CosmeticPosition) < Vector3.Distance(Player.localTransform.position, _board.Position);
        near |= cosmetics;
        if (!_skinFailed)
            try { _skins.Tick(); }
            catch (Exception e)
            {
                _skinFailed = true;
                QuestPlugin.LogSource.LogWarning("Cosmetic rendering paused for this session: " + e);
                try { _skins.Dispose(); } catch { }
            }
        if (gameplay && !_ui.IsOpen && Time.unscaledTime >= _nextHudPoll)
        {
            _nextHudPoll = Time.unscaledTime + 5f;
            Send("progress", "");
        }
        bool riding = IsRidingCarpet();
        bool canTalk = near && !riding;
        if (canTalk && !BlockingInput)
        {
            _talk.Reserve();
        }
        else if (!BlockingInput)
        {
            _talk.Release();
        }
        _ui.SetPrompt(near, _board.Root != null ? _board.Position : Vector3.zero, riding, _talk.Label,
            _board.Root != null, _placementStatus, gameplay, cosmetics);
        bool open = _talk.Pressed;
        if (canTalk && open && !BlockingInput)
        {
            _ui.Open(cosmetics);
            Send(_ui.IsCosmetics ? "wardrobe" : "view", "");
            _nextPoll = Time.unscaledTime + 1;
        }
        if (_ui.IsOpen && (Player.localPlayer.isDead || _board.Root == null || Vector3.Distance(Player.localTransform.position, _board.Position) > 5.5f)) _ui.Close();
        _ui.Tick();
        if (_ui.IsOpen && Time.unscaledTime >= _nextPoll)
        {
            Send(_ui.IsCosmetics ? "wardrobe" : "view", "");
            _nextPoll = Time.unscaledTime + 1;
        }
    }
    private bool IsRidingCarpet()
    {
        if (!_checkedCarpet)
        {
            _checkedCarpet = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("InfernoProtocol.MagicCarpet.MagicCarpetController", false);
                if (type != null) _carpetMounted = type.GetProperty("IsMounted", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            }
        }
        return _carpetMounted?.GetValue(null) is true;
    }
    private void Send(string verb, string id)
    {
        if (_network == null || (_board.Root == null && verb != "progress")) return;
        if (_network.IsServer)
        {
            QuestView result = Service.Execute(Player.localPlayer, verb, id, _board.Root != null ? _board.Position : Vector3.zero);
            _skins.Receive(result); _ui.Receive(result);
            return;
        }
        SendText(RequestChannel, NetworkManager.ServerClientId, verb + ":" + id);
    }
    private void OnRequest(ulong sender, FastBufferReader reader)
    {
        try
        {
            if (_network == null || !_network.IsServer || Service == null || reader.Length > 512) return;
            float now = Time.unscaledTime;
            if (_lastRequest.TryGetValue(sender, out float last) && now - last < 0.15f) return;
            _lastRequest[sender] = now;
            reader.ReadValueSafe(out string request, true);
            if (request == null || request.Length > 80) return;
            string[] parts = request.Split(':');
            if (parts.Length != 2) return;
            if (!_network.ConnectedClients.TryGetValue(sender, out NetworkClient client)) return;
            if (parts[0] == "locate")
            {
                QuestBoardLocation location = _board.Root == null ? null : new QuestBoardLocation
                {
                    X = _board.Position.x, Y = _board.Position.y, Z = _board.Position.z,
                    Yaw = _board.Root.transform.eulerAngles.y
                };
                SendText(ResponseChannel, sender, JsonSerializer.Serialize(new QuestView
                { LocationOnly = true, Location = location, Message = _placementStatus }));
                return;
            }
            if (_board.Root == null && parts[0] != "progress") return;
            Player player = client.PlayerObject != null ? client.PlayerObject.GetComponent<Player>() : null;
            if (player == null)
                foreach (Player candidate in UnityEngine.Object.FindObjectsOfType<Player>())
                    if (candidate.IsSpawned && candidate.OwnerClientId == sender) { player = candidate; break; }
            if (player == null) return;
            QuestView result = Service.Execute(player, parts[0], parts[1], _board.Root != null ? _board.Position : Vector3.zero);
            SendText(ResponseChannel, sender, JsonSerializer.Serialize(result));
        }
        catch (Exception e) { QuestPlugin.LogSource.LogWarning("Quest request rejected: " + e.Message); }
    }
    private void OnResponse(ulong sender, FastBufferReader reader)
    {
        try
        {
            if (_network == null || _network.IsServer || sender != NetworkManager.ServerClientId || reader.Length > 20000) return;
            reader.ReadValueSafe(out string json, true);
            QuestView view = JsonSerializer.Deserialize<QuestView>(json);
            if (view?.Protocol != 1 || view.Rows == null || view.Rows.Count > QuestCatalog.All.Length) return;
            if (view.LocationOnly)
            {
                if (view.Location?.IsValid == true) _hostLocation = view.Location;
                if (!string.IsNullOrEmpty(view.Message)) _placementStatus = view.Message;
                return;
            }
            var ids = new HashSet<string>();
            foreach (QuestRow row in view.Rows)
                if (row == null || QuestCatalog.Find(row.Id) == null || !ids.Add(row.Id)) return;
            _skins.Receive(view); _ui?.Receive(view);
        }
        catch (Exception e) { QuestPlugin.LogSource.LogWarning("Quest response rejected: " + e.Message); }
    }
    private void SendText(string channel, ulong target, string value)
    {
        int capacity = value.Length * 2 + 32;
        FastBufferWriter writer = new(capacity, Allocator.Temp, capacity);
        try { writer.WriteValueSafe(value, true); _messages.SendNamedMessage(channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced); }
        finally { writer.Dispose(); }
    }
    internal void Cleanup()
    {
        if (_saveHandler != null) SaveSystem.SaveManager.onSaveComplete -= _saveHandler;
        _saveHandler = null;
        _ui?.Dispose(); _ui = null;
        _skins.Dispose();
        _skinFailed = false;
        _talk.Dispose();
        _board.Dispose();
        if (_messages != null)
        {
            try { _messages.UnregisterNamedMessageHandler(RequestChannel); _messages.UnregisterNamedMessageHandler(ResponseChannel); }
            catch { }
        }
        Service = null; _messages = null; _network = null;
        _hostLocation = null; _placementStatus = "Preparing the quest board";
        _nextHudPoll = 0;
        _nextBoardAttempt = 0; _nextSpawnWarning = 0;
        _lastRequest.Clear(); _wasListening = false; _failed = false;
    }
    private void OnDestroy() => Cleanup();
}
