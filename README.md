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
-------

```
$ wscat -c ws://localhost:1900/n2o
```

Benchmark
---------

```
$ tcpkali -c4 --ws --message "PING" \
  --latency-first-byte -r50m 127.0.0.1:1900/n2o \
  --latency-percentiles 50,100
```

MacBook Air 2018

```
Total data sent:     19.7 MiB (20683996 bytes)
Total data received: 8.9 MiB (9367440 bytes)
Bandwidth per channel: 6.009⇅ Mbps (751.2 kBps)
Aggregate bandwidth: 7.493↓, 16.544↑ Mbps
Packet rate estimate: 46817.0↓, 1479.5↑ (1↓, 14↑ TCP MSS/op)
First byte latency at percentiles: 9.5/32.2 ms (50/100%)
Test duration: 10.0018 s.
```

Credits
-------

* Igor Gorodetsky
* Maxim Sokhatsky
