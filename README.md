F# WebSocket Server
===================

MailboxProcessor/Async-based WebSocket server with supervision and server-initiated pings in 200 LOC.

Build
-----

```
$ dotnet build ws.fsproj
```

Run
---

```
$ bin/Debug/net5.0/ws
N2O/F# WebSocket Server 1.0
[smp] [cpu:12] [io:12]
```

Connect

```
$ wscat -c ws://localhost:1900/n2o
```

Credits
-------

* Igor Gorodetsky
* Maxim Sokhatsky
