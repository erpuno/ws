F# WebSocket Server
===================

MailboxProcessor/Async-based WebSocket server with supervision and server-initiated pings in 200 LOC.

```
$ dotnet build ws.fsproj
$ bin/Debug/net5.0/ws
$ wscat -c ws://localhost:1900/n2o
```

Credits
-------

* Igor Gorodetsky
* Maxim Sokhatsky
