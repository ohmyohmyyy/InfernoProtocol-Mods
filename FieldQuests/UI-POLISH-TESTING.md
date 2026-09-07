# Turn-in presentation checks (0.3.1)

- Complete a supply quest using mouse, then another using controller: confirm processing is shown, and receipt appears only after server completion.
- Hold/double-click confirm: only one reward and XP award. Close during processing and reopen; progress is authoritative, no extra award.
- Insufficient resources or full inventory: error remains readable; no success receipt.
- Gain a level: XP meter crosses the threshold and level-up text names the new level. Idle polls must not repeat the receipt.
- Switch filters and browse during a response: retained contracts stay selected by ID; if completed contract leaves the filter, selection clamps safely.
- Disconnect host during claim: waiting/timeout feedback, back/close still usable; retry cannot reward a completed quest twice.
- Test short and ultrawide resolutions for header receipt fitting. No gameplay floating marker should appear.

Automated build and gameplay-model tests do not replace these in-game presentation checks.
