using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace InfernoProtocol.Renovator;

/// <summary>Host-authoritative transport for committed finishes and moves.</summary>
internal sealed class RenovatorNetworkSync : IDisposable
{
    private const string HelloChannel = "FrankMods.Renovator.Hello.v1";
    private const string StateChannel = "FrankMods.Renovator.State.v1";
    private const byte SnapshotBegin = 0, FinishChanged = 1, ObjectMoved = 2, SnapshotEnd = 3;
    private const int MaxKeyLength = 160;

    private readonly Func<bool> _snapshotReady;
    private readonly Func<IReadOnlyDictionary<string, RenovatorChoice>> _snapshot;
    private readonly Action _beginSnapshot, _endSnapshot, _remoteSessionEnded;
    private readonly Action<string, int, int> _receiveFinish;
    private readonly Action<string, Vector3, Quaternion> _receiveMove;
    private readonly HashSet<ulong> _compatibleClients = new(), _pendingSnapshots = new();
    private readonly List<ulong> _disconnected = new();
    private NetworkManager _manager;
    private CustomMessagingManager _messages;
    private CustomMessagingManager.HandleNamedMessageDelegate _helloHandler, _stateHandler;
    private float _nextCheck;
    private bool _helloSent, _failureReported;

    internal RenovatorNetworkSync(
        Func<bool> snapshotReady,
        Func<IReadOnlyDictionary<string, RenovatorChoice>> snapshot,
        Action beginSnapshot,
        Action<string, int, int> receiveFinish,
        Action<string, Vector3, Quaternion> receiveMove,
        Action endSnapshot,
        Action remoteSessionEnded)
    {
        _snapshotReady=snapshotReady; _snapshot=snapshot; _beginSnapshot=beginSnapshot;
        _receiveFinish=receiveFinish; _receiveMove=receiveMove; _endSnapshot=endSnapshot;
        _remoteSessionEnded=remoteSessionEnded;
    }

    internal bool IsRemoteClient => _manager!=null&&_manager.IsListening&&_manager.IsClient&&!_manager.IsServer;

    internal void Tick(bool enabled)
    {
        float now=Time.unscaledTime;
        if(now<_nextCheck) return;
        _nextCheck=now+.75f;
        try
        {
            NetworkManager manager=enabled?NetworkManager.Singleton:null;
            if(manager==null||!manager.IsListening||manager.CustomMessagingManager==null) { Reset(); return; }
            if(_manager==null||_manager.GetInstanceID()!=manager.GetInstanceID())
            {
                Reset(); _manager=manager; _messages=manager.CustomMessagingManager;
                _helloHandler=DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(new Action<ulong,FastBufferReader>(OnHello));
                _stateHandler=DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(new Action<ulong,FastBufferReader>(OnState));
                _messages.RegisterNamedMessageHandler(HelloChannel,_helloHandler);
                _messages.RegisterNamedMessageHandler(StateChannel,_stateHandler);
                _failureReported=false;
                RenovatorPlugin.Logger.LogInfo("Renovator multiplayer finish layer ready.");
            }
            if(manager.IsClient&&!manager.IsServer&&!_helloSent) SendHello();
            if(manager.IsServer&&_snapshotReady()&&_pendingSnapshots.Count>0)
            {
                foreach(ulong clientId in _pendingSnapshots) SendSnapshot(clientId);
                _pendingSnapshots.Clear();
            }
        }
        catch(Exception exception)
        {
            if(_failureReported) return;
            _failureReported=true;
            RenovatorPlugin.Logger.LogWarning("Renovator multiplayer sync is unavailable: "+exception.Message);
        }
    }

    private void SendHello()
    {
        FastBufferWriter writer=new(1,Allocator.Temp,1);
        try { _messages.SendNamedMessage(HelloChannel,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableSequenced); _helloSent=true; }
        finally { writer.Dispose(); }
    }

    private void OnHello(ulong sender,FastBufferReader reader)
    {
        if(_manager==null||!_manager.IsServer||sender==_manager.LocalClientId) return;
        if(!_compatibleClients.Add(sender)) return;
        RenovatorPlugin.Logger.LogInfo("Renovator client "+sender+" joined finish synchronization.");
        if(_snapshotReady()) SendSnapshot(sender); else _pendingSnapshots.Add(sender);
    }

    private void SendSnapshot(ulong clientId)
    {
        if(!SendControl(clientId,SnapshotBegin)) return;
        IReadOnlyDictionary<string,RenovatorChoice> snapshot=_snapshot();
        if(snapshot!=null) foreach(KeyValuePair<string,RenovatorChoice> pair in snapshot)
        {
            RenovatorChoice choice=pair.Value;
            if(choice!=null&&RenovatorPatterns.StoredChoiceValid(choice.Pattern,choice.Scale)&&RenovatorPatterns.Available(choice.Pattern))
                SendFinish(clientId,pair.Key,choice.Pattern,choice.Scale);
        }
        SendControl(clientId,SnapshotEnd);
    }

    private void OnState(ulong sender,FastBufferReader reader)
    {
        try
        {
            if(_manager==null||!_manager.IsClient||_manager.IsServer||sender!=NetworkManager.ServerClientId||reader.Length>512) return;
            reader.ReadByte(out byte operation);
            if(operation==SnapshotBegin) { _beginSnapshot(); return; }
            if(operation==SnapshotEnd) { _endSnapshot(); return; }
            reader.ReadValue(out string key,true);
            if(!ValidKey(key)) return;
            if(operation==FinishChanged)
            {
                reader.ReadValue(out int pattern,default(FastBufferWriter.ForPrimitives));
                reader.ReadValue(out int scale,default(FastBufferWriter.ForPrimitives));
                if(pattern!=0&&(!RenovatorPatterns.StoredChoiceValid(pattern,scale)||!RenovatorPatterns.Available(pattern))) return;
                _receiveFinish(key,pattern,scale);
            }
            else if(operation==ObjectMoved)
            {
                reader.ReadValue(out Vector3 position); reader.ReadValue(out Quaternion rotation);
                if(!Finite(position)||!Finite(rotation)||position.sqrMagnitude>100000000f) return;
                _receiveMove(key,position,rotation);
            }
        }
        catch(Exception exception) { RenovatorPlugin.Logger.LogDebug("Renovator network message rejected: "+exception.Message); }
    }

    internal void BroadcastFinish(string key,int pattern,int scale)
    {
        Tick(true);
        if(_manager==null||!_manager.IsServer||_messages==null||!ValidKey(key)) return;
        _disconnected.Clear();
        foreach(ulong clientId in _compatibleClients) if(!SendFinish(clientId,key,pattern,scale)) _disconnected.Add(clientId);
        RemoveDisconnected();
    }

    internal void BroadcastSnapshot()
    {
        Tick(true);
        if(_manager==null||!_manager.IsServer||_messages==null||!_snapshotReady()) return;
        _disconnected.Clear();
        foreach(ulong clientId in _compatibleClients)
            if(!SendControl(clientId,SnapshotBegin)) _disconnected.Add(clientId);
            else
            {
                IReadOnlyDictionary<string,RenovatorChoice> snapshot=_snapshot();
                if(snapshot!=null) foreach(KeyValuePair<string,RenovatorChoice> pair in snapshot)
                {
                    RenovatorChoice choice=pair.Value;
                    if(choice!=null&&RenovatorPatterns.StoredChoiceValid(choice.Pattern,choice.Scale)&&RenovatorPatterns.Available(choice.Pattern))
                        SendFinish(clientId,pair.Key,choice.Pattern,choice.Scale);
                }
                SendControl(clientId,SnapshotEnd);
            }
        RemoveDisconnected();
    }

    internal void BroadcastMove(string key,Vector3 position,Quaternion rotation)
    {
        Tick(true);
        if(_manager==null||!_manager.IsServer||_messages==null||!ValidKey(key)) return;
        _disconnected.Clear();
        foreach(ulong clientId in _compatibleClients) if(!SendMove(clientId,key,position,rotation)) _disconnected.Add(clientId);
        RemoveDisconnected();
    }

    private bool SendControl(ulong clientId,byte operation)
    {
        FastBufferWriter writer=new(1,Allocator.Temp,1);
        try { writer.WriteByte(operation); _messages.SendNamedMessage(StateChannel,clientId,writer,NetworkDelivery.ReliableSequenced); return true; }
        catch { return false; }
        finally { writer.Dispose(); }
    }

    private bool SendFinish(ulong clientId,string key,int pattern,int scale)
    {
        if(!ValidKey(key)) return false;
        FastBufferWriter writer=new(Math.Max(256,key.Length*2+64),Allocator.Temp,512);
        try
        {
            byte operation=FinishChanged; writer.WriteByte(operation); writer.WriteValue(key,true);
            writer.WriteValue(ref pattern,default(FastBufferWriter.ForPrimitives));
            writer.WriteValue(ref scale,default(FastBufferWriter.ForPrimitives));
            _messages.SendNamedMessage(StateChannel,clientId,writer,NetworkDelivery.ReliableSequenced); return true;
        }
        catch { return false; }
        finally { writer.Dispose(); }
    }

    private bool SendMove(ulong clientId,string key,Vector3 position,Quaternion rotation)
    {
        if(!ValidKey(key)) return false;
        FastBufferWriter writer=new(Math.Max(256,key.Length*2+96),Allocator.Temp,512);
        try
        {
            byte operation=ObjectMoved; writer.WriteByte(operation); writer.WriteValue(key,true);
            writer.WriteValue(ref position); writer.WriteValue(ref rotation);
            _messages.SendNamedMessage(StateChannel,clientId,writer,NetworkDelivery.ReliableSequenced); return true;
        }
        catch { return false; }
        finally { writer.Dispose(); }
    }

    private void RemoveDisconnected()
    {
        for(int i=0;i<_disconnected.Count;i++) { _compatibleClients.Remove(_disconnected[i]); _pendingSnapshots.Remove(_disconnected[i]); }
    }

    private static bool ValidKey(string key)=>!string.IsNullOrWhiteSpace(key)&&key.Length<=MaxKeyLength;
    private static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
    private static bool Finite(Quaternion value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z)&&float.IsFinite(value.w);

    private void Reset()
    {
        bool remote=IsRemoteClient;
        if(_messages!=null) try { _messages.UnregisterNamedMessageHandler(HelloChannel); _messages.UnregisterNamedMessageHandler(StateChannel); } catch { }
        _manager=null; _messages=null; _helloHandler=null; _stateHandler=null; _helloSent=false;
        _compatibleClients.Clear(); _pendingSnapshots.Clear(); _disconnected.Clear();
        if(remote) _remoteSessionEnded();
    }

    public void Dispose()=>Reset();
}
