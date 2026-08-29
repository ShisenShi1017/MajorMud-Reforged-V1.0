# Communication Persistence and Limits — Update 0328

PASS57 promotes `native.communications.persistence-logging` from **UNKNOWN** to **STRONG** as an evidence-bounded profile/page-limit contract.

The exact new closure is `SET MAXPAGES <n>`: native help states that it limits pages or telegrams received in each **3 second period**. `PAGE` separately toggles whether other system users can page the player while in-game. These facts are not evidence for a general chat-flood limiter.

Existing native communication controls are retained: per-sender telepath FORGET, GOSSIP/AUCTION receipt toggles, SET MESSAGES thresholds, and SET ENTRANCES selection. TELEPATH explicit ON/OFF named polarity remains unresolved because the retained compact wording does not map the names safely to receipt/block storage semantics.

The current Reforged `RuntimeCommunicationService` 512-character maximum and 10-second/6-message flood window remain extension-only and receive zero Classic parity credit. Reforged character `Conditions` storage also remains a compatibility persistence representation, not a claim about native MajorMUD record layout.

Exact defaults, serialization, overflow/backpressure, page/telegram host handoff, reconnect save/load ordering, message-priority membership and exact rendering prevent PASS. Worldgroup mail and native logging parity are excluded.
