---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-04 17:32 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 887,387 | 826,522–952,735 | 1.47 | 0.93x |
| Confluent | 2 | 949,450 | 826,511–1,090,675 | 1.94 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.87 | 777.55 | 1,332,227 | 1,319,665 | -5.9% | -0.47% | 1270.51 | 1,332,227 | 0 | 1.16 |
| Confluent (dekaf-first) | 1.63 | - | 1,061,543 | 1,090,675 | -15.0% | -1.28% | 1012.37 | 1,061,543 | 0 | 1.73 |
| Dekaf (dekaf-first) | 1.45 | 1396.09 | 936,149 | 952,735 | -12.4% | -1.30% | 892.78 | 936,149 | 0 | 1.36 |
| Dekaf (confluent-first) | 1.49 | 1247.64 | 825,974 | 826,522 | +19.3% | +1.75% | 787.71 | 825,974 | 0 | 1.23 |
| Confluent (confluent-first) | 2.25 | - | 798,682 | 826,511 | -7.5% | -0.91% | 761.68 | 798,682 | 0 | 1.79 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 874,648 | 971.82 | 957.57 KB |
| Dekaf | 1 | 887,570 | 986.18 | 832.58 KB |
| Dekaf (3conn) | 1 | 1,338,859 | 1487.61 | 890.23 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:27.7071791+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 384,079 msg/s |
| Dekaf | 2026-08-04T16:31:54.7142202+00:00 | 1 | 16.0 MiB / 9.7 MiB | 1158.0 MB/s | 0/0 | 6,293 | 27.0s / 717,521 msg/s |
| Dekaf | 2026-08-04T16:32:22.7221996+00:00 | 1 | 14.0 MiB / 3.4 MiB | 1158.0 MB/s | 1/0 | 14,960 | 55.0s / 700,139 msg/s |
| Dekaf | 2026-08-04T16:32:49.7320707+00:00 | 1 | 14.0 MiB / 12.1 MiB | 1158.0 MB/s | 1/0 | 22,992 | 82.0s / 750,615 msg/s |
| Dekaf | 2026-08-04T16:33:16.7453356+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1158.0 MB/s | 2/0 | 29,904 | 109.0s / 755,985 msg/s |
| Dekaf | 2026-08-04T16:33:43.7534462+00:00 | 1 | 12.0 MiB / 1.5 MiB | 1158.0 MB/s | 2/1 | 35,995 | 136.1s / 592,587 msg/s |
| Dekaf | 2026-08-04T16:34:11.7632116+00:00 | 1 | 12.0 MiB / 9.3 MiB | 1158.0 MB/s | 2/1 | 42,198 | 164.1s / 699,492 msg/s |
| Dekaf | 2026-08-04T16:34:38.7753176+00:00 | 1 | 12.0 MiB / 4.1 MiB | 1158.0 MB/s | 2/1 | 45,621 | 191.1s / 763,553 msg/s |
| Dekaf | 2026-08-04T16:35:05.7814637+00:00 | 1 | 12.0 MiB / 3.3 MiB | 1158.0 MB/s | 2/2 | 53,705 | 218.1s / 632,661 msg/s |
| Dekaf | 2026-08-04T16:35:32.7972829+00:00 | 1 | 12.0 MiB / 1.4 MiB | 1158.0 MB/s | 2/2 | 58,161 | 245.1s / 584,259 msg/s |
| Dekaf | 2026-08-04T16:36:00.8126457+00:00 | 1 | 12.0 MiB / 4.3 MiB | 1158.0 MB/s | 2/2 | 59,523 | 273.1s / 584,635 msg/s |
| Dekaf | 2026-08-04T16:36:27.8251633+00:00 | 1 | 12.0 MiB / 2.6 MiB | 1158.0 MB/s | 2/2 | 66,402 | 300.1s / 859,361 msg/s |
| Dekaf | 2026-08-04T16:36:54.8372142+00:00 | 1 | 12.0 MiB / 3.6 MiB | 1158.0 MB/s | 2/2 | 71,024 | 327.1s / 662,550 msg/s |
| Dekaf | 2026-08-04T16:37:21.8416428+00:00 | 1 | 12.0 MiB / 2.3 MiB | 1158.0 MB/s | 2/3 | 80,428 | 354.1s / 726,874 msg/s |
| Dekaf | 2026-08-04T16:37:49.8528082+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1158.0 MB/s | 2/3 | 92,245 | 382.2s / 861,696 msg/s |
| Dekaf | 2026-08-04T16:38:16.8629705+00:00 | 1 | 12.0 MiB / 4.2 MiB | 1158.0 MB/s | 2/3 | 101,129 | 409.2s / 867,791 msg/s |
| Dekaf | 2026-08-04T16:38:43.8751829+00:00 | 1 | 12.0 MiB / 7.0 MiB | 1158.0 MB/s | 2/3 | 109,855 | 436.2s / 828,841 msg/s |
| Dekaf | 2026-08-04T16:39:10.8811591+00:00 | 1 | 12.0 MiB / 3.7 MiB | 1158.0 MB/s | 2/3 | 120,430 | 463.2s / 817,034 msg/s |
| Dekaf | 2026-08-04T16:39:38.8893077+00:00 | 1 | 12.0 MiB / 2.8 MiB | 1158.0 MB/s | 2/3 | 132,285 | 491.2s / 836,788 msg/s |
| Dekaf | 2026-08-04T16:40:05.8998407+00:00 | 1 | 12.0 MiB / 6.2 MiB | 1190.8 MB/s | 2/3 | 144,570 | 518.2s / 913,412 msg/s |
| Dekaf | 2026-08-04T16:40:32.9121733+00:00 | 1 | 12.0 MiB / 3.7 MiB | 1207.2 MB/s | 2/3 | 159,943 | 545.2s / 900,442 msg/s |
| Dekaf | 2026-08-04T16:41:00.9242985+00:00 | 1 | 12.0 MiB / 0.5 MiB | 1207.2 MB/s | 2/3 | 169,815 | 573.2s / 919,668 msg/s |
| Dekaf | 2026-08-04T16:41:27.9364902+00:00 | 1 | 13.0 MiB / 3.6 MiB | 1207.2 MB/s | 2/3 | 180,276 | 600.2s / 800,454 msg/s |
| Dekaf | 2026-08-04T16:41:54.9442967+00:00 | 1 | 12.0 MiB / 6.4 MiB | 1207.2 MB/s | 2/4 | 192,918 | 627.2s / 795,984 msg/s |
| Dekaf | 2026-08-04T16:42:21.9542829+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1207.2 MB/s | 2/4 | 202,782 | 654.3s / 969,102 msg/s |
| Dekaf | 2026-08-04T16:42:49.9660449+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1207.2 MB/s | 2/4 | 218,103 | 682.3s / 1,018,025 msg/s |
| Dekaf | 2026-08-04T16:43:16.9733354+00:00 | 1 | 12.0 MiB / 1.9 MiB | 1207.2 MB/s | 2/4 | 232,838 | 709.3s / 962,360 msg/s |
| Dekaf | 2026-08-04T16:43:43.9937351+00:00 | 1 | 12.0 MiB / 4.5 MiB | 1207.2 MB/s | 2/4 | 241,244 | 736.3s / 875,265 msg/s |
| Dekaf | 2026-08-04T16:44:11.0024729+00:00 | 1 | 12.0 MiB / 3.6 MiB | 1278.6 MB/s | 2/4 | 253,935 | 763.3s / 846,553 msg/s |
| Dekaf | 2026-08-04T16:44:39.0158402+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1278.6 MB/s | 2/4 | 265,179 | 791.3s / 948,656 msg/s |
| Dekaf | 2026-08-04T16:45:06.0334069+00:00 | 1 | 12.0 MiB / 9.0 MiB | 1278.6 MB/s | 2/4 | 273,347 | 818.3s / 887,007 msg/s |
| Dekaf | 2026-08-04T16:45:33.0383507+00:00 | 1 | 10.0 MiB / 4.7 MiB | 1278.6 MB/s | 2/4 | 282,915 | 845.3s / 754,253 msg/s |
| Dekaf | 2026-08-04T16:46:00.0442708+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1278.6 MB/s | 2/5 | 290,720 | 872.3s / 992,491 msg/s |
| Dekaf | 2026-08-04T16:46:28.9232797+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 339,958 msg/s |
| Dekaf | 2026-08-04T16:46:55.9304231+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1640.5 MB/s | 0/0 | 18,124 | 27.0s / 1,009,267 msg/s |
| Dekaf | 2026-08-04T16:47:22.9373124+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1640.5 MB/s | 0/1 | 29,935 | 54.0s / 1,037,667 msg/s |
| Dekaf | 2026-08-04T16:47:49.95006+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1640.5 MB/s | 0/1 | 44,492 | 81.0s / 1,021,584 msg/s |
| Dekaf | 2026-08-04T16:48:17.9611567+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1640.5 MB/s | 0/1 | 61,299 | 109.1s / 1,000,989 msg/s |
| Dekaf | 2026-08-04T16:48:44.9682741+00:00 | 1 | 16.0 MiB / 12.0 MiB | 1640.5 MB/s | 0/2 | 79,840 | 136.1s / 1,003,581 msg/s |
| Dekaf | 2026-08-04T16:49:11.9748453+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1640.5 MB/s | 0/2 | 95,126 | 163.1s / 1,021,612 msg/s |
| Dekaf | 2026-08-04T16:49:39.9841579+00:00 | 1 | 16.0 MiB / 14.2 MiB | 1640.5 MB/s | 0/3 | 108,187 | 191.1s / 1,050,043 msg/s |
| Dekaf | 2026-08-04T16:50:06.9969394+00:00 | 1 | 16.0 MiB / 14.5 MiB | 1640.5 MB/s | 0/3 | 121,367 | 218.1s / 939,230 msg/s |
| Dekaf | 2026-08-04T16:50:34.0092662+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1640.5 MB/s | 0/3 | 136,058 | 245.1s / 1,069,810 msg/s |
| Dekaf | 2026-08-04T16:51:01.0166279+00:00 | 1 | 16.0 MiB / 5.0 MiB | 1640.5 MB/s | 0/3 | 148,548 | 272.1s / 922,129 msg/s |
| Dekaf | 2026-08-04T16:51:29.0218704+00:00 | 1 | 16.0 MiB / 6.9 MiB | 1640.5 MB/s | 0/3 | 162,269 | 300.1s / 949,742 msg/s |
| Dekaf | 2026-08-04T16:51:56.0313907+00:00 | 1 | 16.0 MiB / 13.3 MiB | 1640.5 MB/s | 0/3 | 176,540 | 327.1s / 1,014,802 msg/s |
| Dekaf | 2026-08-04T16:52:23.0448067+00:00 | 1 | 16.0 MiB / 14.5 MiB | 1640.5 MB/s | 0/3 | 188,845 | 354.1s / 997,740 msg/s |
| Dekaf | 2026-08-04T16:52:50.0536392+00:00 | 1 | 16.0 MiB / 4.3 MiB | 1640.5 MB/s | 0/3 | 203,471 | 381.1s / 977,135 msg/s |
| Dekaf | 2026-08-04T16:53:18.0623326+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1640.5 MB/s | 0/3 | 222,540 | 409.1s / 1,038,406 msg/s |
| Dekaf | 2026-08-04T16:53:45.0663492+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1640.5 MB/s | 1/3 | 242,312 | 436.2s / 1,047,066 msg/s |
| Dekaf | 2026-08-04T16:54:12.0749868+00:00 | 1 | 14.0 MiB / 4.4 MiB | 1640.5 MB/s | 1/4 | 260,523 | 463.2s / 913,761 msg/s |
| Dekaf | 2026-08-04T16:54:39.0830531+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1640.5 MB/s | 1/4 | 279,031 | 490.2s / 873,841 msg/s |
| Dekaf | 2026-08-04T16:55:07.0908964+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1640.5 MB/s | 1/5 | 292,496 | 518.2s / 732,553 msg/s |
| Dekaf | 2026-08-04T16:55:34.0935304+00:00 | 1 | 14.0 MiB / 2.4 MiB | 1640.5 MB/s | 1/5 | 305,968 | 545.2s / 905,871 msg/s |
| Dekaf | 2026-08-04T16:56:01.1031009+00:00 | 1 | 14.0 MiB / 11.2 MiB | 1640.5 MB/s | 1/5 | 323,331 | 572.2s / 787,191 msg/s |
| Dekaf | 2026-08-04T16:56:28.1155375+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1640.5 MB/s | 1/5 | 334,332 | 599.2s / 863,074 msg/s |
| Dekaf | 2026-08-04T16:56:56.1236263+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1640.5 MB/s | 1/5 | 351,870 | 627.2s / 752,119 msg/s |
| Dekaf | 2026-08-04T16:57:23.129459+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1640.5 MB/s | 2/5 | 365,636 | 654.2s / 873,023 msg/s |
| Dekaf | 2026-08-04T16:57:50.13946+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1640.5 MB/s | 2/5 | 383,414 | 681.2s / 960,613 msg/s |
| Dekaf | 2026-08-04T16:58:17.1445349+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1640.5 MB/s | 3/5 | 402,615 | 708.2s / 853,334 msg/s |
| Dekaf | 2026-08-04T16:58:45.1514924+00:00 | 1 | 13.0 MiB / 8.4 MiB | 1640.5 MB/s | 3/6 | 419,210 | 736.2s / 829,346 msg/s |
| Dekaf | 2026-08-04T16:59:12.1565087+00:00 | 1 | 13.0 MiB / 12.2 MiB | 1640.5 MB/s | 3/6 | 430,655 | 763.2s / 876,163 msg/s |
| Dekaf | 2026-08-04T16:59:39.1612924+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1640.5 MB/s | 3/7 | 443,199 | 790.2s / 870,840 msg/s |
| Dekaf | 2026-08-04T17:00:07.172326+00:00 | 1 | 13.0 MiB / 11.2 MiB | 1640.5 MB/s | 3/7 | 465,949 | 818.3s / 819,400 msg/s |
| Dekaf | 2026-08-04T17:00:34.1764714+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1640.5 MB/s | 3/7 | 483,395 | 845.3s / 869,484 msg/s |
| Dekaf | 2026-08-04T17:01:01.1861752+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1640.5 MB/s | 3/7 | 506,049 | 872.3s / 966,289 msg/s |
| Dekaf | 2026-08-04T17:01:28.1925223+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1640.5 MB/s | 3/7 | 520,130 | 899.3s / 898,450 msg/s |
| Dekaf (3conn) | 2026-08-04T17:16:57.2231799+00:00 | 1 | 16.0 MiB / 8.3 MiB | 2007.4 MB/s | 0/0 | 1,510 | 27.0s / 1,710,297 msg/s |
| Dekaf (3conn) | 2026-08-04T17:17:24.232162+00:00 | 1 | 14.0 MiB / 5.3 MiB | 2051.5 MB/s | 1/0 | 4,028 | 54.0s / 1,421,950 msg/s |
| Dekaf (3conn) | 2026-08-04T17:17:51.2401715+00:00 | 1 | 14.0 MiB / 4.0 MiB | 2051.5 MB/s | 1/0 | 6,193 | 81.1s / 1,178,258 msg/s |
| Dekaf (3conn) | 2026-08-04T17:18:18.2485173+00:00 | 1 | 14.0 MiB / 5.4 MiB | 2051.5 MB/s | 1/1 | 8,500 | 108.1s / 1,442,286 msg/s |
| Dekaf (3conn) | 2026-08-04T17:18:46.2621717+00:00 | 1 | 14.0 MiB / 5.6 MiB | 2051.5 MB/s | 1/1 | 11,006 | 136.1s / 1,405,748 msg/s |
| Dekaf (3conn) | 2026-08-04T17:19:13.2723194+00:00 | 1 | 15.0 MiB / 1.5 MiB | 2051.5 MB/s | 1/1 | 13,489 | 163.1s / 1,127,314 msg/s |
| Dekaf (3conn) | 2026-08-04T17:19:40.2860115+00:00 | 1 | 15.0 MiB / 1.6 MiB | 2051.5 MB/s | 2/1 | 15,372 | 190.1s / 1,511,938 msg/s |
| Dekaf (3conn) | 2026-08-04T17:20:07.3001885+00:00 | 1 | 15.0 MiB / 10.7 MiB | 2051.5 MB/s | 2/2 | 17,831 | 217.1s / 1,530,399 msg/s |
| Dekaf (3conn) | 2026-08-04T17:20:35.3192868+00:00 | 1 | 15.0 MiB / 7.8 MiB | 2051.5 MB/s | 2/2 | 20,152 | 245.1s / 1,261,438 msg/s |
| Dekaf (3conn) | 2026-08-04T17:21:02.333165+00:00 | 1 | 13.0 MiB / 2.7 MiB | 2051.5 MB/s | 2/2 | 22,706 | 272.1s / 1,442,080 msg/s |
| Dekaf (3conn) | 2026-08-04T17:21:29.3512068+00:00 | 1 | 13.0 MiB / 4.9 MiB | 2051.5 MB/s | 3/2 | 26,639 | 299.2s / 1,471,521 msg/s |
| Dekaf (3conn) | 2026-08-04T17:21:56.3660689+00:00 | 1 | 13.0 MiB / 1.9 MiB | 2051.5 MB/s | 3/2 | 30,324 | 326.2s / 1,320,109 msg/s |
| Dekaf (3conn) | 2026-08-04T17:22:24.371208+00:00 | 1 | 13.0 MiB / 13.0 MiB | 2051.5 MB/s | 3/3 | 34,053 | 354.2s / 1,539,560 msg/s |
| Dekaf (3conn) | 2026-08-04T17:22:51.3873172+00:00 | 1 | 13.0 MiB / 7.0 MiB | 2051.5 MB/s | 3/3 | 38,145 | 381.2s / 1,441,552 msg/s |
| Dekaf (3conn) | 2026-08-04T17:23:18.3952439+00:00 | 1 | 14.0 MiB / 4.4 MiB | 2051.5 MB/s | 4/3 | 41,203 | 408.2s / 1,644,008 msg/s |
| Dekaf (3conn) | 2026-08-04T17:23:46.4124358+00:00 | 1 | 15.0 MiB / 5.8 MiB | 2051.5 MB/s | 4/3 | 42,988 | 436.2s / 1,224,672 msg/s |
| Dekaf (3conn) | 2026-08-04T17:24:13.4276583+00:00 | 1 | 14.0 MiB / 1.7 MiB | 2051.5 MB/s | 4/4 | 44,978 | 463.3s / 1,297,646 msg/s |
| Dekaf (3conn) | 2026-08-04T17:24:40.4369655+00:00 | 1 | 14.0 MiB / 4.6 MiB | 2051.5 MB/s | 4/4 | 46,180 | 490.3s / 1,129,259 msg/s |
| Dekaf (3conn) | 2026-08-04T17:25:07.4428773+00:00 | 1 | 14.0 MiB / 5.7 MiB | 2051.5 MB/s | 4/4 | 47,690 | 517.3s / 1,526,168 msg/s |
| Dekaf (3conn) | 2026-08-04T17:25:35.4527424+00:00 | 1 | 14.0 MiB / 7.5 MiB | 2142.5 MB/s | 4/5 | 50,148 | 545.3s / 1,616,117 msg/s |
| Dekaf (3conn) | 2026-08-04T17:26:02.4712375+00:00 | 1 | 14.0 MiB / 6.6 MiB | 2142.5 MB/s | 4/5 | 54,008 | 572.3s / 1,490,497 msg/s |
| Dekaf (3conn) | 2026-08-04T17:26:29.48148+00:00 | 1 | 14.0 MiB / 6.2 MiB | 2142.5 MB/s | 4/5 | 56,885 | 599.3s / 1,387,514 msg/s |
| Dekaf (3conn) | 2026-08-04T17:26:56.4935728+00:00 | 1 | 14.0 MiB / 2.5 MiB | 2142.5 MB/s | 4/5 | 59,914 | 626.3s / 1,334,750 msg/s |
| Dekaf (3conn) | 2026-08-04T17:27:24.5095575+00:00 | 1 | 14.0 MiB / 3.4 MiB | 2142.5 MB/s | 4/5 | 62,221 | 654.3s / 1,283,318 msg/s |
| Dekaf (3conn) | 2026-08-04T17:27:51.5235778+00:00 | 1 | 15.0 MiB / 1.3 MiB | 2142.5 MB/s | 5/5 | 64,201 | 681.3s / 1,195,412 msg/s |
| Dekaf (3conn) | 2026-08-04T17:28:18.5338953+00:00 | 1 | 16.0 MiB / 3.7 MiB | 2142.5 MB/s | 6/5 | 65,632 | 708.4s / 1,294,947 msg/s |
| Dekaf (3conn) | 2026-08-04T17:28:45.5453747+00:00 | 1 | 16.0 MiB / 3.2 MiB | 2142.5 MB/s | 6/5 | 66,258 | 735.4s / 1,121,999 msg/s |
| Dekaf (3conn) | 2026-08-04T17:29:13.5542244+00:00 | 1 | 18.0 MiB / 4.7 MiB | 2142.5 MB/s | 7/5 | 66,975 | 763.4s / 1,160,643 msg/s |
| Dekaf (3conn) | 2026-08-04T17:29:40.5621595+00:00 | 1 | 18.0 MiB / 4.1 MiB | 2142.5 MB/s | 7/5 | 67,797 | 790.4s / 1,287,519 msg/s |
| Dekaf (3conn) | 2026-08-04T17:30:07.5815456+00:00 | 1 | 20.0 MiB / 2.9 MiB | 2142.5 MB/s | 8/5 | 68,843 | 817.4s / 1,090,660 msg/s |
| Dekaf (3conn) | 2026-08-04T17:30:34.5912231+00:00 | 1 | 20.0 MiB / 3.7 MiB | 2142.5 MB/s | 8/6 | 69,489 | 844.4s / 1,267,941 msg/s |
| Dekaf (3conn) | 2026-08-04T17:31:02.610943+00:00 | 1 | 20.0 MiB / 3.1 MiB | 2142.5 MB/s | 8/6 | 70,405 | 872.4s / 1,247,976 msg/s |
| Dekaf (3conn) | 2026-08-04T17:31:29.6211613+00:00 | 1 | 20.0 MiB / 11.3 MiB | 2142.5 MB/s | 8/6 | 71,623 | 899.4s / 1,297,167 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-04T16:31:57.8345772+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 13.5 MiB |
| Dekaf | 2026-08-04T16:32:12.858695+00:00 | 1 | capacity | succeeded | 15,023ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-08-04T16:32:42.913264+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-04T16:32:57.945922+00:00 | 1 | capacity | succeeded | 15,032ms | 12.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-04T16:33:27.9914317+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-04T16:33:43.0155338+00:00 | 1 | capacity | failed | 15,023ms | 12.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-04T16:34:43.105714+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 0.6 MiB |
| Dekaf | 2026-08-04T16:34:58.1333393+00:00 | 1 | capacity | failed | 15,027ms | 12.0 MiB / 10.9 MiB |
| Dekaf | 2026-08-04T16:36:58.3153885+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.7 MiB |
| Dekaf | 2026-08-04T16:37:13.3350889+00:00 | 1 | capacity | failed | 15,019ms | 12.0 MiB / 4.8 MiB |
| Dekaf | 2026-08-04T16:41:13.688823+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 2.9 MiB |
| Dekaf | 2026-08-04T16:41:28.7050446+00:00 | 1 | capacity | failed | 15,016ms | 12.0 MiB / 11.2 MiB |
| Dekaf | 2026-08-04T16:45:29.0546512+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 1.9 MiB |
| Dekaf | 2026-08-04T16:45:44.0782259+00:00 | 1 | capacity | failed | 15,023ms | 12.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-04T16:46:59.0794486+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:47:14.0988122+00:00 | 1 | capacity | failed | 15,019ms | 16.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-04T16:48:14.2261049+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-04T16:48:29.2420117+00:00 | 1 | capacity | failed | 15,015ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:48:59.2892966+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:49:14.3112846+00:00 | 1 | capacity | failed | 15,021ms | 16.0 MiB / 12.0 MiB |
| Dekaf | 2026-08-04T16:53:14.6847492+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.2 MiB |
| Dekaf | 2026-08-04T16:53:29.701996+00:00 | 1 | capacity | succeeded | 15,016ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:53:32.7050183+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:53:47.7232968+00:00 | 1 | capacity | failed | 15,018ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:54:47.7952923+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:55:02.8145275+00:00 | 1 | capacity | failed | 15,019ms | 14.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-04T16:57:02.9752841+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-04T16:57:17.9964909+00:00 | 1 | capacity | succeeded | 15,021ms | 15.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-04T16:57:48.0373391+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 13.6 MiB |
| Dekaf | 2026-08-04T16:58:03.0582622+00:00 | 1 | capacity | succeeded | 15,020ms | 13.0 MiB / 10.2 MiB |
| Dekaf | 2026-08-04T16:58:06.0650587+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-04T16:58:21.0909176+00:00 | 1 | capacity | failed | 15,025ms | 13.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-04T16:59:21.2014198+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.2 MiB |
| Dekaf | 2026-08-04T16:59:36.2211284+00:00 | 1 | capacity | failed | 15,019ms | 13.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-08-04T17:17:00.3327599+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.6 MiB |
| Dekaf (3conn) | 2026-08-04T17:17:15.3535565+00:00 | 1 | capacity | succeeded | 15,020ms | 14.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:17:45.3955452+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:18:00.4202217+00:00 | 1 | capacity | failed | 15,024ms | 14.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-08-04T17:19:00.5208865+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:19:15.5583159+00:00 | 1 | capacity | succeeded | 15,037ms | 15.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:19:45.6064301+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-08-04T17:20:00.63707+00:00 | 1 | capacity | failed | 15,030ms | 15.0 MiB / 7.4 MiB |
| Dekaf (3conn) | 2026-08-04T17:21:00.7572192+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 13.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:21:15.7835756+00:00 | 1 | capacity | succeeded | 15,026ms | 13.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-08-04T17:21:45.8665097+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:22:00.8939149+00:00 | 1 | capacity | failed | 15,027ms | 13.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-08-04T17:23:01.0082271+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:23:16.0299641+00:00 | 1 | capacity | succeeded | 15,021ms | 14.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:23:46.0777056+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:24:01.1038338+00:00 | 1 | capacity | failed | 15,026ms | 14.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-08-04T17:25:01.1983316+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:25:16.2239645+00:00 | 1 | capacity | failed | 15,025ms | 14.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-08-04T17:27:16.4119899+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-08-04T17:27:31.4479552+00:00 | 1 | capacity | succeeded | 15,035ms | 15.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-08-04T17:28:01.5031791+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-08-04T17:28:16.5349363+00:00 | 1 | capacity | succeeded | 15,031ms | 16.0 MiB / 6.3 MiB |
| Dekaf (3conn) | 2026-08-04T17:28:46.5751738+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:29:01.594578+00:00 | 1 | capacity | succeeded | 15,019ms | 18.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-08-04T17:29:31.6475657+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:29:46.6809226+00:00 | 1 | capacity | succeeded | 15,033ms | 20.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:30:16.733017+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-08-04T17:30:31.7579937+00:00 | 1 | capacity | failed | 15,024ms | 20.0 MiB / 9.8 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 441 |
| Dekaf | 1 | 0.002–0.004ms | 482 |
| Dekaf | 1 | 0.004–0.008ms | 1,108 |
| Dekaf | 1 | 0.008–0.016ms | 3,889 |
| Dekaf | 1 | 0.016–0.032ms | 9,661 |
| Dekaf | 1 | 0.032–0.064ms | 12,415 |
| Dekaf | 1 | 0.064–0.128ms | 17,135 |
| Dekaf | 1 | 0.128–0.256ms | 36,918 |
| Dekaf | 1 | 0.256–0.512ms | 58,959 |
| Dekaf | 1 | 0.512–1.024ms | 46,390 |
| Dekaf | 1 | 1.024–2.048ms | 19,295 |
| Dekaf | 1 | 2.048–4.096ms | 3,671 |
| Dekaf | 1 | 4.096–8.192ms | 1,009 |
| Dekaf | 1 | 8.192–16.384ms | 131 |
| Dekaf | 1 | 16.384–32.768ms | 5 |
| Dekaf | 1 | 0.001–0.002ms | 326 |
| Dekaf | 1 | 0.002–0.004ms | 390 |
| Dekaf | 1 | 0.004–0.008ms | 779 |
| Dekaf | 1 | 0.008–0.016ms | 2,190 |
| Dekaf | 1 | 0.016–0.032ms | 5,142 |
| Dekaf | 1 | 0.032–0.064ms | 8,926 |
| Dekaf | 1 | 0.064–0.128ms | 13,085 |
| Dekaf | 1 | 0.128–0.256ms | 28,993 |
| Dekaf | 1 | 0.256–0.512ms | 42,296 |
| Dekaf | 1 | 0.512–1.024ms | 17,371 |
| Dekaf | 1 | 1.024–2.048ms | 4,277 |
| Dekaf | 1 | 2.048–4.096ms | 1,528 |
| Dekaf | 1 | 4.096–8.192ms | 474 |
| Dekaf | 1 | 8.192–16.384ms | 30 |
| Dekaf | 1 | 16.384–32.768ms | 4 |
| Dekaf | 1 | 131.072–262.144ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 19 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 39 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 83 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 225 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 716 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 1,891 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 2,101 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 3,444 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 4,596 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 4,135 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 2,801 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,115 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 247 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 27 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 2 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 146,337,000 | 2026-08-04T16:19:23.9056551+00:00 | 102.5ms | GC pause | - | - | 177.1s / 818,085 msg/s | Gen2 +0 / pause +66.4ms |
| Confluent | 151,641,000 | 2026-08-04T16:19:29.7838385+00:00 | 174.8ms | GC pause | - | - | 183.1s / 777,322 msg/s | Gen2 +0 / pause +114.6ms |
| Confluent | 151,853,000 | 2026-08-04T16:19:30.069536+00:00 | 102.5ms | GC pause | - | - | 183.1s / 777,322 msg/s | Gen2 +0 / pause +114.6ms |
| Confluent | 192,237,000 | 2026-08-04T16:20:18.9586906+00:00 | 111.5ms | GC pause | - | - | 232.2s / 792,379 msg/s | Gen2 +0 / pause +126.4ms |
| Confluent | 192,239,000 | 2026-08-04T16:20:18.9635905+00:00 | 100.7ms | GC pause | - | - | 232.2s / 792,379 msg/s | Gen2 +0 / pause +126.4ms |
| Confluent | 192,303,000 | 2026-08-04T16:20:19.0230134+00:00 | 120.0ms | GC pause | - | - | 232.2s / 792,379 msg/s | Gen2 +0 / pause +126.4ms |
| Confluent | 192,326,000 | 2026-08-04T16:20:19.0579281+00:00 | 118.9ms | GC pause | - | - | 232.2s / 792,379 msg/s | Gen2 +0 / pause +126.4ms |
| Confluent | 193,268,000 | 2026-08-04T16:20:20.2425265+00:00 | 109.2ms | GC pause | - | - | 233.2s / 807,221 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 319,817,000 | 2026-08-04T16:22:50.0219247+00:00 | 114.1ms | GC pause | - | - | 383.3s / 581,003 msg/s | Gen2 +0 / pause +126.7ms |
| Confluent | 356,937,000 | 2026-08-04T16:23:33.087517+00:00 | 101.7ms | GC pause | - | - | 426.4s / 888,012 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 365,961,000 | 2026-08-04T16:23:43.1550369+00:00 | 110.1ms | GC pause | - | - | 436.4s / 819,720 msg/s | Gen2 +0 / pause +88.7ms |
| Confluent | 366,100,000 | 2026-08-04T16:23:43.2634623+00:00 | 110.7ms | GC pause | - | - | 436.4s / 819,720 msg/s | Gen2 +0 / pause +88.7ms |
| Confluent | 423,521,000 | 2026-08-04T16:24:51.5126809+00:00 | 110.8ms | GC pause | - | - | 504.4s / 787,710 msg/s | Gen2 +0 / pause +115.3ms |
| Confluent | 438,668,000 | 2026-08-04T16:25:11.0965618+00:00 | 103.5ms | GC pause | - | - | 524.4s / 879,892 msg/s | Gen2 +0 / pause +93.7ms |
| Confluent | 483,839,000 | 2026-08-04T16:26:16.7374923+00:00 | 143.0ms | GC pause | - | - | 589.5s / 818,551 msg/s | Gen2 +0 / pause +81.8ms |
| Confluent | 483,941,000 | 2026-08-04T16:26:16.9748062+00:00 | 105.5ms | GC pause | - | - | 590.5s / 442,828 msg/s | Gen2 +0 / pause +139.9ms |
| Confluent | 533,189,000 | 2026-08-04T16:27:28.2360112+00:00 | 136.4ms | GC pause | - | - | 661.5s / 831,748 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 533,608,000 | 2026-08-04T16:27:28.8180681+00:00 | 116.9ms | GC pause | - | - | 661.5s / 831,748 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 533,627,000 | 2026-08-04T16:27:28.8362817+00:00 | 121.7ms | GC pause | - | - | 661.5s / 831,748 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 533,691,000 | 2026-08-04T16:27:28.9160945+00:00 | 117.6ms | GC pause | - | - | 662.5s / 906,866 msg/s | Gen2 +0 / pause +213.2ms |
| Confluent | 533,861,000 | 2026-08-04T16:27:29.1099291+00:00 | 114.9ms | GC pause | - | - | 662.5s / 906,866 msg/s | Gen2 +0 / pause +109.9ms |
| Confluent | 533,903,000 | 2026-08-04T16:27:29.1519421+00:00 | 114.4ms | GC pause | - | - | 662.5s / 906,866 msg/s | Gen2 +0 / pause +109.9ms |
| Confluent | 534,797,000 | 2026-08-04T16:27:30.1026697+00:00 | 168.0ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 534,820,000 | 2026-08-04T16:27:30.1233251+00:00 | 145.4ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 534,837,000 | 2026-08-04T16:27:30.1359153+00:00 | 177.4ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 535,002,000 | 2026-08-04T16:27:30.2826878+00:00 | 142.5ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 535,026,000 | 2026-08-04T16:27:30.3071889+00:00 | 147.1ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 535,104,000 | 2026-08-04T16:27:30.3986328+00:00 | 141.7ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 535,273,000 | 2026-08-04T16:27:30.5614771+00:00 | 157.2ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 535,361,000 | 2026-08-04T16:27:30.6596862+00:00 | 172.0ms | GC pause | - | - | 663.5s / 993,343 msg/s | Gen2 +0 / pause +59.8ms |
| Confluent | 535,831,000 | 2026-08-04T16:27:31.1638559+00:00 | 135.3ms | GC pause | - | - | 664.5s / 888,450 msg/s | Gen2 +0 / pause +92.2ms |
| Confluent | 535,941,000 | 2026-08-04T16:27:31.2951874+00:00 | 132.2ms | GC pause | - | - | 664.5s / 888,450 msg/s | Gen2 +0 / pause +92.2ms |
| Confluent | 539,937,000 | 2026-08-04T16:27:35.5648919+00:00 | 112.0ms | GC pause | - | - | 668.5s / 920,687 msg/s | Gen2 +0 / pause +104.8ms |
| Confluent | 540,077,000 | 2026-08-04T16:27:35.6778902+00:00 | 192.9ms | GC pause | - | - | 668.5s / 920,687 msg/s | Gen2 +0 / pause +104.8ms |
| Confluent | 540,275,000 | 2026-08-04T16:27:35.9936549+00:00 | 111.8ms | GC pause | - | - | 669.5s / 896,973 msg/s | Gen2 +0 / pause +204.5ms |
| Confluent | 540,345,000 | 2026-08-04T16:27:36.0925388+00:00 | 101.8ms | GC pause | - | - | 669.5s / 896,973 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 541,010,000 | 2026-08-04T16:27:36.8336704+00:00 | 131.1ms | GC pause | - | - | 669.5s / 896,973 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 541,161,000 | 2026-08-04T16:27:36.9890289+00:00 | 224.1ms | GC pause | - | - | 670.5s / 717,950 msg/s | Gen2 +0 / pause +220.2ms |
| Confluent | 544,983,000 | 2026-08-04T16:27:41.4864018+00:00 | 137.8ms | GC pause | - | - | 674.5s / 893,179 msg/s | Gen2 +0 / pause +103.1ms |
| Confluent | 545,018,000 | 2026-08-04T16:27:41.5215034+00:00 | 154.4ms | GC pause | - | - | 674.5s / 893,179 msg/s | Gen2 +0 / pause +103.1ms |
| Confluent | 545,066,000 | 2026-08-04T16:27:41.5734664+00:00 | 125.7ms | GC pause | - | - | 674.5s / 893,179 msg/s | Gen2 +0 / pause +103.1ms |
| Confluent | 545,466,000 | 2026-08-04T16:27:42.043694+00:00 | 117.8ms | GC pause | - | - | 675.5s / 823,156 msg/s | Gen2 +0 / pause +87.0ms |
| Confluent | 546,137,000 | 2026-08-04T16:27:42.8140257+00:00 | 157.4ms | GC pause | - | - | 675.5s / 823,156 msg/s | Gen2 +0 / pause +87.0ms |
| Confluent | 547,326,000 | 2026-08-04T16:27:44.2051666+00:00 | 194.7ms | GC pause | - | - | 677.5s / 758,689 msg/s | Gen2 +0 / pause +115.4ms |
| Confluent | 547,547,000 | 2026-08-04T16:27:44.4320295+00:00 | 256.0ms | GC pause | - | - | 677.5s / 758,689 msg/s | Gen2 +0 / pause +115.4ms |
| Confluent | 547,772,000 | 2026-08-04T16:27:44.7873443+00:00 | 121.7ms | GC pause | - | - | 677.5s / 758,689 msg/s | Gen2 +0 / pause +115.4ms |
| Confluent | 548,657,000 | 2026-08-04T16:27:45.893668+00:00 | 111.6ms | GC pause | - | - | 679.5s / 887,414 msg/s | Gen2 +0 / pause +279.5ms |
| Confluent | 549,987,000 | 2026-08-04T16:27:47.3263741+00:00 | 123.0ms | GC pause | - | - | 680.5s / 1,032,955 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 550,155,000 | 2026-08-04T16:27:47.4592257+00:00 | 164.2ms | GC pause | - | - | 680.5s / 1,032,955 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 550,471,000 | 2026-08-04T16:27:47.8030957+00:00 | 216.4ms | GC pause | - | - | 681.5s / 652,452 msg/s | Gen2 +0 / pause +215.1ms |
| Confluent | 550,495,000 | 2026-08-04T16:27:47.8291229+00:00 | 190.1ms | GC pause | - | - | 681.5s / 652,452 msg/s | Gen2 +0 / pause +215.1ms |
| Confluent | 551,037,000 | 2026-08-04T16:27:48.6297471+00:00 | 122.9ms | GC pause | - | - | 681.5s / 652,452 msg/s | Gen2 +0 / pause +120.3ms |
| Confluent | 559,783,000 | 2026-08-04T16:27:59.0759358+00:00 | 130.7ms | GC pause | - | - | 692.5s / 775,762 msg/s | Gen2 +0 / pause +129.4ms |
| Confluent | 559,795,000 | 2026-08-04T16:27:59.1032628+00:00 | 105.2ms | GC pause | - | - | 692.5s / 775,762 msg/s | Gen2 +0 / pause +129.4ms |
| Confluent | 560,860,000 | 2026-08-04T16:28:00.3390368+00:00 | 209.2ms | GC pause | - | - | 693.5s / 821,431 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 560,957,000 | 2026-08-04T16:28:00.4465887+00:00 | 219.9ms | GC pause | - | - | 693.5s / 821,431 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 561,022,000 | 2026-08-04T16:28:00.5442719+00:00 | 163.6ms | GC pause | - | - | 693.5s / 821,431 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 561,276,000 | 2026-08-04T16:28:00.9193915+00:00 | 150.0ms | GC pause | - | - | 694.5s / 739,726 msg/s | Gen2 +0 / pause +174.9ms |
| Confluent | 561,521,000 | 2026-08-04T16:28:01.3069376+00:00 | 145.9ms | GC pause | - | - | 694.5s / 739,726 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 570,162,000 | 2026-08-04T16:28:14.2978116+00:00 | 109.3ms | GC pause | - | - | 707.6s / 901,724 msg/s | Gen2 +0 / pause +57.7ms |
| Confluent | 570,231,000 | 2026-08-04T16:28:14.3746091+00:00 | 132.8ms | GC pause | - | - | 707.6s / 901,724 msg/s | Gen2 +0 / pause +57.7ms |
| Confluent | 570,392,000 | 2026-08-04T16:28:14.570719+00:00 | 100.2ms | GC pause | - | - | 707.6s / 901,724 msg/s | Gen2 +0 / pause +57.7ms |
| Confluent | 581,449,000 | 2026-08-04T16:28:27.4118813+00:00 | 101.6ms | GC pause | - | - | 720.6s / 817,268 msg/s | Gen2 +0 / pause +84.5ms |
| Confluent | 581,621,000 | 2026-08-04T16:28:27.6651739+00:00 | 101.3ms | GC pause | - | - | 720.6s / 817,268 msg/s | Gen2 +0 / pause +84.5ms |
| Confluent | 600,317,000 | 2026-08-04T16:28:49.6371726+00:00 | 139.8ms | GC pause | - | - | 742.6s / 660,887 msg/s | Gen2 +0 / pause +149.9ms |
| Confluent | 600,418,000 | 2026-08-04T16:28:49.801207+00:00 | 170.5ms | GC pause | - | - | 742.6s / 660,887 msg/s | Gen2 +0 / pause +149.9ms |
| Confluent | 612,349,000 | 2026-08-04T16:29:05.3934692+00:00 | 172.8ms | GC pause | - | - | 758.6s / 687,233 msg/s | Gen2 +0 / pause +144.1ms |
| Confluent | 612,394,000 | 2026-08-04T16:29:05.4519834+00:00 | 166.3ms | GC pause | - | - | 758.6s / 687,233 msg/s | Gen2 +0 / pause +144.1ms |
| Confluent | 613,187,000 | 2026-08-04T16:29:06.4979535+00:00 | 160.1ms | GC pause | - | - | 759.6s / 720,980 msg/s | Gen2 +0 / pause +76.4ms |
| Confluent | 620,368,000 | 2026-08-04T16:29:16.5160287+00:00 | 160.7ms | GC pause | - | - | 769.6s / 636,313 msg/s | Gen2 +0 / pause +90.5ms |
| Confluent | 620,370,000 | 2026-08-04T16:29:16.5188326+00:00 | 138.0ms | GC pause | - | - | 769.6s / 636,313 msg/s | Gen2 +0 / pause +90.5ms |
| Confluent | 620,498,000 | 2026-08-04T16:29:16.7392161+00:00 | 124.9ms | GC pause | - | - | 769.6s / 636,313 msg/s | Gen2 +0 / pause +90.5ms |
| Confluent | 621,701,000 | 2026-08-04T16:29:18.2693396+00:00 | 113.5ms | GC pause | - | - | 771.6s / 848,690 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 621,771,000 | 2026-08-04T16:29:18.3352843+00:00 | 155.5ms | GC pause | - | - | 771.6s / 848,690 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 621,983,000 | 2026-08-04T16:29:18.658221+00:00 | 113.1ms | GC pause | - | - | 771.6s / 848,690 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 622,165,000 | 2026-08-04T16:29:18.8290788+00:00 | 185.7ms | GC pause | - | - | 771.6s / 848,690 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 622,311,000 | 2026-08-04T16:29:18.9859645+00:00 | 264.4ms | GC pause | - | - | 772.6s / 804,465 msg/s | Gen2 +0 / pause +239.8ms |
| Confluent | 622,545,000 | 2026-08-04T16:29:19.3405532+00:00 | 174.0ms | GC pause | - | - | 772.6s / 804,465 msg/s | Gen2 +0 / pause +125.2ms |
| Confluent | 622,807,000 | 2026-08-04T16:29:19.6905691+00:00 | 215.7ms | GC pause | - | - | 772.6s / 804,465 msg/s | Gen2 +0 / pause +125.2ms |
| Confluent | 622,838,000 | 2026-08-04T16:29:19.7268996+00:00 | 220.3ms | GC pause | - | - | 772.6s / 804,465 msg/s | Gen2 +0 / pause +125.2ms |
| Confluent | 623,140,000 | 2026-08-04T16:29:20.0691003+00:00 | 274.5ms | GC pause | - | - | 773.6s / 628,559 msg/s | Gen2 +0 / pause +233.4ms |
| Confluent | 623,297,000 | 2026-08-04T16:29:20.3094878+00:00 | 301.7ms | GC pause | - | - | 773.6s / 628,559 msg/s | Gen2 +0 / pause +108.2ms |
| Confluent | 623,379,000 | 2026-08-04T16:29:20.4047908+00:00 | 230.9ms | GC pause | - | - | 773.6s / 628,559 msg/s | Gen2 +0 / pause +108.2ms |
| Confluent | 624,247,000 | 2026-08-04T16:29:21.5983331+00:00 | 100.5ms | GC pause | - | - | 774.6s / 949,673 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 624,454,000 | 2026-08-04T16:29:21.7861603+00:00 | 123.3ms | GC pause | - | - | 774.6s / 949,673 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 624,479,000 | 2026-08-04T16:29:21.8083357+00:00 | 139.1ms | GC pause | - | - | 774.6s / 949,673 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 624,567,000 | 2026-08-04T16:29:21.8895867+00:00 | 202.2ms | GC pause | - | - | 774.6s / 949,673 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 624,640,000 | 2026-08-04T16:29:21.965834+00:00 | 204.6ms | GC pause | - | - | 775.6s / 687,538 msg/s | Gen2 +0 / pause +193.0ms |
| Confluent | 624,649,000 | 2026-08-04T16:29:21.9752631+00:00 | 195.5ms | GC pause | - | - | 775.6s / 687,538 msg/s | Gen2 +0 / pause +193.0ms |
| Confluent | 625,334,000 | 2026-08-04T16:29:22.9214356+00:00 | 152.9ms | GC pause | - | - | 775.6s / 687,538 msg/s | Gen2 +0 / pause +108.3ms |
| Confluent | 631,796,000 | 2026-08-04T16:29:32.6785136+00:00 | 125.2ms | GC pause | - | - | 785.6s / 749,150 msg/s | Gen2 +0 / pause +100.5ms |
| Confluent | 631,845,000 | 2026-08-04T16:29:32.7401898+00:00 | 129.6ms | GC pause | - | - | 785.6s / 749,150 msg/s | Gen2 +0 / pause +100.5ms |
| Confluent | 632,215,000 | 2026-08-04T16:29:33.2901254+00:00 | 100.9ms | GC pause | - | - | 786.6s / 727,726 msg/s | Gen2 +0 / pause +98.5ms |
| Confluent | 632,418,000 | 2026-08-04T16:29:33.5636772+00:00 | 151.6ms | GC pause | - | - | 786.6s / 727,726 msg/s | Gen2 +0 / pause +98.5ms |
| Confluent | 632,510,000 | 2026-08-04T16:29:33.6781624+00:00 | 144.0ms | GC pause | - | - | 786.6s / 727,726 msg/s | Gen2 +0 / pause +98.5ms |
| Confluent | 632,521,000 | 2026-08-04T16:29:33.6925342+00:00 | 189.5ms | GC pause | - | - | 786.6s / 727,726 msg/s | Gen2 +0 / pause +98.5ms |
| Confluent | 632,717,000 | 2026-08-04T16:29:33.9641725+00:00 | 240.8ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +185.8ms |
| Confluent | 632,811,000 | 2026-08-04T16:29:34.1006383+00:00 | 204.8ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +185.8ms |
| Confluent | 632,814,000 | 2026-08-04T16:29:34.1058774+00:00 | 154.4ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +185.8ms |
| Confluent | 633,014,000 | 2026-08-04T16:29:34.404272+00:00 | 120.0ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +87.3ms |
| Confluent | 633,044,000 | 2026-08-04T16:29:34.4449219+00:00 | 113.1ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +87.3ms |
| Confluent | 633,308,000 | 2026-08-04T16:29:34.7749216+00:00 | 187.2ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +87.3ms |
| Confluent | 633,315,000 | 2026-08-04T16:29:34.7822519+00:00 | 116.3ms | GC pause | - | - | 787.6s / 685,421 msg/s | Gen2 +0 / pause +87.3ms |
| Confluent | 637,746,000 | 2026-08-04T16:29:40.0222864+00:00 | 158.8ms | GC pause | - | - | 793.6s / 793,858 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 637,812,000 | 2026-08-04T16:29:40.0983873+00:00 | 134.3ms | GC pause | - | - | 793.6s / 793,858 msg/s | Gen2 +0 / pause +168.6ms |
| Confluent | 638,645,000 | 2026-08-04T16:29:41.1636484+00:00 | 183.3ms | GC pause | - | - | 794.6s / 708,188 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 639,078,000 | 2026-08-04T16:29:41.8415897+00:00 | 120.4ms | GC pause | - | - | 794.6s / 708,188 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 639,150,000 | 2026-08-04T16:29:41.9179124+00:00 | 133.3ms | GC pause | - | - | 794.6s / 708,188 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 639,441,000 | 2026-08-04T16:29:42.2653982+00:00 | 186.4ms | GC pause | - | - | 795.6s / 655,495 msg/s | Gen2 +0 / pause +95.7ms |
| Confluent | 639,491,000 | 2026-08-04T16:29:42.3337539+00:00 | 177.8ms | GC pause | - | - | 795.6s / 655,495 msg/s | Gen2 +0 / pause +95.7ms |
| Confluent | 639,559,000 | 2026-08-04T16:29:42.449245+00:00 | 113.8ms | GC pause | - | - | 795.6s / 655,495 msg/s | Gen2 +0 / pause +95.7ms |
| Confluent | 640,287,000 | 2026-08-04T16:29:43.4660499+00:00 | 111.2ms | GC pause | - | - | 796.6s / 767,885 msg/s | Gen2 +0 / pause +88.5ms |
| Confluent | 641,291,000 | 2026-08-04T16:29:44.8375356+00:00 | 104.4ms | GC pause | - | - | 797.6s / 794,301 msg/s | Gen2 +0 / pause +109.1ms |
| Confluent | 641,398,000 | 2026-08-04T16:29:44.9634777+00:00 | 144.3ms | GC pause | - | - | 797.6s / 794,301 msg/s | Gen2 +0 / pause +109.1ms |
| Confluent | 641,415,000 | 2026-08-04T16:29:44.9807265+00:00 | 110.7ms | GC pause | - | - | 797.6s / 794,301 msg/s | Gen2 +0 / pause +109.1ms |
| Confluent | 641,444,000 | 2026-08-04T16:29:45.0040545+00:00 | 136.2ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +224.1ms |
| Confluent | 641,606,000 | 2026-08-04T16:29:45.2267312+00:00 | 149.3ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 641,631,000 | 2026-08-04T16:29:45.2625344+00:00 | 201.9ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 641,754,000 | 2026-08-04T16:29:45.4269261+00:00 | 167.5ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 641,851,000 | 2026-08-04T16:29:45.5450591+00:00 | 243.2ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 641,915,000 | 2026-08-04T16:29:45.6354261+00:00 | 171.9ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 642,022,000 | 2026-08-04T16:29:45.818003+00:00 | 120.6ms | GC pause | - | - | 798.7s / 646,425 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 646,588,000 | 2026-08-04T16:29:52.0633884+00:00 | 101.6ms | GC pause | - | - | 805.7s / 738,617 msg/s | Gen2 +0 / pause +173.9ms |
| Confluent | 646,737,000 | 2026-08-04T16:29:52.253874+00:00 | 107.5ms | GC pause | - | - | 805.7s / 738,617 msg/s | Gen2 +0 / pause +76.5ms |
| Confluent | 647,272,000 | 2026-08-04T16:29:52.9621069+00:00 | 114.3ms | GC pause | - | - | 805.7s / 738,617 msg/s | Gen2 +0 / pause +76.5ms |
| Confluent | 648,811,000 | 2026-08-04T16:29:54.8911781+00:00 | 103.0ms | GC pause | - | - | 807.7s / 872,319 msg/s | Gen2 +0 / pause +102.5ms |
| Confluent | 648,840,000 | 2026-08-04T16:29:54.9218539+00:00 | 103.8ms | GC pause | - | - | 807.7s / 872,319 msg/s | Gen2 +0 / pause +102.5ms |
| Confluent | 649,102,000 | 2026-08-04T16:29:55.2559403+00:00 | 109.5ms | GC pause | - | - | 808.7s / 793,411 msg/s | Gen2 +0 / pause +76.4ms |
| Confluent | 649,817,000 | 2026-08-04T16:29:56.1487599+00:00 | 179.8ms | GC pause | - | - | 809.7s / 692,439 msg/s | Gen2 +0 / pause +117.9ms |
| Confluent | 650,010,000 | 2026-08-04T16:29:56.4336287+00:00 | 129.5ms | GC pause | - | - | 809.7s / 692,439 msg/s | Gen2 +0 / pause +117.9ms |
| Confluent | 650,331,000 | 2026-08-04T16:29:56.8795732+00:00 | 155.2ms | GC pause | - | - | 809.7s / 692,439 msg/s | Gen2 +0 / pause +117.9ms |
| Confluent | 651,001,000 | 2026-08-04T16:29:57.7283633+00:00 | 122.3ms | GC pause | - | - | 810.7s / 853,573 msg/s | Gen2 +0 / pause +133.4ms |
| Confluent | 651,259,000 | 2026-08-04T16:29:58.0008586+00:00 | 207.6ms | GC pause | - | - | 811.7s / 477,286 msg/s | Gen2 +0 / pause +261.6ms |
| Confluent | 651,333,000 | 2026-08-04T16:29:58.1071339+00:00 | 202.9ms | GC pause | - | - | 811.7s / 477,286 msg/s | Gen2 +0 / pause +261.6ms |
| Confluent | 659,111,000 | 2026-08-04T16:30:10.4311388+00:00 | 125.1ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,112,000 | 2026-08-04T16:30:10.4319442+00:00 | 116.6ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,172,000 | 2026-08-04T16:30:10.5129425+00:00 | 112.7ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,231,000 | 2026-08-04T16:30:10.6107842+00:00 | 111.4ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,261,000 | 2026-08-04T16:30:10.6381017+00:00 | 119.1ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,281,000 | 2026-08-04T16:30:10.652157+00:00 | 129.6ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,428,000 | 2026-08-04T16:30:10.8559554+00:00 | 137.8ms | GC pause | - | - | 823.7s / 754,010 msg/s | Gen2 +0 / pause +165.8ms |
| Confluent | 659,570,000 | 2026-08-04T16:30:11.076938+00:00 | 101.4ms | GC pause | - | - | 824.7s / 762,598 msg/s | Gen2 +0 / pause +245.4ms |
| Confluent | 659,598,000 | 2026-08-04T16:30:11.1064662+00:00 | 143.7ms | GC pause | - | - | 824.7s / 762,598 msg/s | Gen2 +0 / pause +245.4ms |
| Confluent | 659,893,000 | 2026-08-04T16:30:11.484837+00:00 | 147.3ms | GC pause | - | - | 824.7s / 762,598 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 659,908,000 | 2026-08-04T16:30:11.4991521+00:00 | 179.8ms | GC pause | - | - | 824.7s / 762,598 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 660,377,000 | 2026-08-04T16:30:12.125879+00:00 | 142.0ms | GC pause | - | - | 825.7s / 768,814 msg/s | Gen2 +0 / pause +185.6ms |
| Confluent | 660,608,000 | 2026-08-04T16:30:12.3839444+00:00 | 168.2ms | GC pause | - | - | 825.7s / 768,814 msg/s | Gen2 +0 / pause +106.1ms |
| Confluent | 660,977,000 | 2026-08-04T16:30:12.8484034+00:00 | 165.2ms | GC pause | - | - | 825.7s / 768,814 msg/s | Gen2 +0 / pause +106.1ms |
| Confluent | 661,068,000 | 2026-08-04T16:30:12.9700596+00:00 | 145.8ms | GC pause | - | - | 825.7s / 768,814 msg/s | Gen2 +0 / pause +106.1ms |
| Confluent | 661,714,000 | 2026-08-04T16:30:13.7430416+00:00 | 121.5ms | GC pause | - | - | 826.7s / 888,149 msg/s | Gen2 +0 / pause +97.5ms |
| Confluent | 661,891,000 | 2026-08-04T16:30:13.9422296+00:00 | 162.2ms | GC pause | - | - | 826.7s / 888,149 msg/s | Gen2 +0 / pause +97.5ms |
| Confluent | 662,155,000 | 2026-08-04T16:30:14.274588+00:00 | 163.2ms | GC pause | - | - | 827.7s / 760,800 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 662,480,000 | 2026-08-04T16:30:14.7452833+00:00 | 150.5ms | GC pause | - | - | 827.7s / 760,800 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 662,670,000 | 2026-08-04T16:30:14.9759166+00:00 | 167.3ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +224.1ms |
| Confluent | 662,687,000 | 2026-08-04T16:30:14.9975042+00:00 | 238.7ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +224.1ms |
| Confluent | 662,710,000 | 2026-08-04T16:30:15.0196115+00:00 | 173.8ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +224.1ms |
| Confluent | 662,824,000 | 2026-08-04T16:30:15.153872+00:00 | 189.6ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 662,838,000 | 2026-08-04T16:30:15.1712959+00:00 | 295.9ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 662,999,000 | 2026-08-04T16:30:15.4568092+00:00 | 142.4ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 663,133,000 | 2026-08-04T16:30:15.6404645+00:00 | 132.8ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 663,160,000 | 2026-08-04T16:30:15.6873278+00:00 | 128.5ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 663,188,000 | 2026-08-04T16:30:15.7487534+00:00 | 174.4ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 663,261,000 | 2026-08-04T16:30:15.8325336+00:00 | 187.1ms | GC pause | - | - | 828.7s / 643,399 msg/s | Gen2 +0 / pause +127.6ms |
| Confluent | 666,448,000 | 2026-08-04T16:30:19.8250626+00:00 | 122.6ms | GC pause | - | - | 832.7s / 805,239 msg/s | Gen2 +0 / pause +122.8ms |
| Confluent | 667,088,000 | 2026-08-04T16:30:20.607054+00:00 | 145.8ms | GC pause | - | - | 833.7s / 844,775 msg/s | Gen2 +0 / pause +77.3ms |
| Confluent | 667,110,000 | 2026-08-04T16:30:20.631637+00:00 | 117.7ms | GC pause | - | - | 833.7s / 844,775 msg/s | Gen2 +0 / pause +77.3ms |
| Confluent | 667,203,000 | 2026-08-04T16:30:20.7264899+00:00 | 137.5ms | GC pause | - | - | 833.7s / 844,775 msg/s | Gen2 +0 / pause +77.3ms |
| Confluent | 667,218,000 | 2026-08-04T16:30:20.7428101+00:00 | 181.7ms | GC pause | - | - | 833.7s / 844,775 msg/s | Gen2 +0 / pause +77.3ms |
| Confluent | 667,276,000 | 2026-08-04T16:30:20.8077586+00:00 | 147.5ms | GC pause | - | - | 833.7s / 844,775 msg/s | Gen2 +0 / pause +77.3ms |
| Confluent | 669,991,000 | 2026-08-04T16:30:24.4895266+00:00 | 135.7ms | GC pause | - | - | 837.7s / 728,067 msg/s | Gen2 +0 / pause +125.9ms |
| Confluent | 670,224,000 | 2026-08-04T16:30:24.7694052+00:00 | 146.4ms | GC pause | - | - | 837.7s / 728,067 msg/s | Gen2 +0 / pause +125.9ms |
| Confluent | 673,514,000 | 2026-08-04T16:30:29.1719295+00:00 | 175.7ms | GC pause | - | - | 842.7s / 666,719 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 673,756,000 | 2026-08-04T16:30:29.5329789+00:00 | 148.3ms | GC pause | - | - | 842.7s / 666,719 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 673,802,000 | 2026-08-04T16:30:29.587227+00:00 | 144.3ms | GC pause | - | - | 842.7s / 666,719 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 673,965,000 | 2026-08-04T16:30:29.8243291+00:00 | 132.3ms | GC pause | - | - | 842.7s / 666,719 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 673,991,000 | 2026-08-04T16:30:29.8834292+00:00 | 143.3ms | GC pause | - | - | 842.7s / 666,719 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 674,018,000 | 2026-08-04T16:30:29.9388169+00:00 | 118.4ms | GC pause | - | - | 842.7s / 666,719 msg/s | Gen2 +0 / pause +115.0ms |
| Confluent | 674,187,000 | 2026-08-04T16:30:30.1850027+00:00 | 134.0ms | GC pause | - | - | 843.7s / 665,258 msg/s | Gen2 +0 / pause +125.6ms |
| Confluent | 677,325,000 | 2026-08-04T16:30:34.1523327+00:00 | 104.0ms | GC pause | - | - | 847.7s / 870,037 msg/s | Gen2 +0 / pause +168.2ms |
| Confluent | 677,606,000 | 2026-08-04T16:30:34.4676598+00:00 | 130.9ms | GC pause | - | - | 847.7s / 870,037 msg/s | Gen2 +0 / pause +71.2ms |
| Confluent | 677,751,000 | 2026-08-04T16:30:34.6403224+00:00 | 119.6ms | GC pause | - | - | 847.7s / 870,037 msg/s | Gen2 +0 / pause +71.2ms |
| Confluent | 677,818,000 | 2026-08-04T16:30:34.7085131+00:00 | 129.7ms | GC pause | - | - | 847.7s / 870,037 msg/s | Gen2 +0 / pause +71.2ms |
| Confluent | 677,877,000 | 2026-08-04T16:30:34.7696968+00:00 | 135.0ms | GC pause | - | - | 847.7s / 870,037 msg/s | Gen2 +0 / pause +71.2ms |
| Confluent | 678,133,000 | 2026-08-04T16:30:35.0497593+00:00 | 198.1ms | GC pause | - | - | 848.7s / 714,755 msg/s | Gen2 +0 / pause +150.0ms |
| Confluent | 678,221,000 | 2026-08-04T16:30:35.1866555+00:00 | 160.9ms | GC pause | - | - | 848.7s / 714,755 msg/s | Gen2 +0 / pause +78.8ms |
| Confluent | 685,158,000 | 2026-08-04T16:30:44.7128775+00:00 | 111.3ms | GC pause | - | - | 857.7s / 822,466 msg/s | Gen2 +0 / pause +123.8ms |
| Confluent | 685,308,000 | 2026-08-04T16:30:44.9006694+00:00 | 123.6ms | GC pause | - | - | 857.7s / 822,466 msg/s | Gen2 +0 / pause +123.8ms |
| Confluent | 685,381,000 | 2026-08-04T16:30:45.0120181+00:00 | 130.1ms | GC pause | - | - | 857.7s / 822,466 msg/s | Gen2 +0 / pause +123.8ms |
| Confluent | 685,807,000 | 2026-08-04T16:30:45.5570604+00:00 | 209.9ms | GC pause | - | - | 858.7s / 711,184 msg/s | Gen2 +0 / pause +117.2ms |
| Confluent | 685,808,000 | 2026-08-04T16:30:45.5580645+00:00 | 208.9ms | GC pause | - | - | 858.7s / 711,184 msg/s | Gen2 +0 / pause +117.2ms |
| Confluent | 685,925,000 | 2026-08-04T16:30:45.7053318+00:00 | 178.3ms | GC pause | - | - | 858.7s / 711,184 msg/s | Gen2 +0 / pause +117.2ms |
| Confluent | 686,004,000 | 2026-08-04T16:30:45.7787282+00:00 | 218.1ms | GC pause | - | - | 858.7s / 711,184 msg/s | Gen2 +0 / pause +117.2ms |
| Confluent | 687,879,000 | 2026-08-04T16:30:48.2357814+00:00 | 109.8ms | GC pause | - | - | 861.7s / 724,248 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 688,098,000 | 2026-08-04T16:30:48.5702788+00:00 | 121.3ms | GC pause | - | - | 861.7s / 724,248 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 688,330,000 | 2026-08-04T16:30:48.851723+00:00 | 118.6ms | GC pause | - | - | 861.7s / 724,248 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 688,381,000 | 2026-08-04T16:30:48.9259023+00:00 | 128.3ms | GC pause | - | - | 861.7s / 724,248 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 688,397,000 | 2026-08-04T16:30:48.9546474+00:00 | 117.8ms | GC pause | - | - | 861.7s / 724,248 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 688,407,000 | 2026-08-04T16:30:48.9730358+00:00 | 108.9ms | GC pause | - | - | 861.7s / 724,248 msg/s | Gen2 +0 / pause +134.5ms |
| Confluent | 688,741,000 | 2026-08-04T16:30:49.4230025+00:00 | 138.4ms | GC pause | - | - | 862.7s / 624,399 msg/s | Gen2 +0 / pause +99.2ms |
| Confluent | 690,992,000 | 2026-08-04T16:30:52.3456068+00:00 | 188.7ms | GC pause | - | - | 865.7s / 773,903 msg/s | Gen2 +0 / pause +138.0ms |
| Confluent | 691,149,000 | 2026-08-04T16:30:52.5726563+00:00 | 142.5ms | GC pause | - | - | 865.7s / 773,903 msg/s | Gen2 +0 / pause +138.0ms |
| Confluent | 691,566,000 | 2026-08-04T16:30:53.1217945+00:00 | 148.7ms | GC pause | - | - | 866.7s / 746,266 msg/s | Gen2 +0 / pause +282.5ms |
| Confluent | 691,662,000 | 2026-08-04T16:30:53.2604403+00:00 | 128.5ms | GC pause | - | - | 866.7s / 746,266 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 691,671,000 | 2026-08-04T16:30:53.2712687+00:00 | 179.0ms | GC pause | - | - | 866.7s / 746,266 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 691,674,000 | 2026-08-04T16:30:53.2735367+00:00 | 129.8ms | GC pause | - | - | 866.7s / 746,266 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 691,766,000 | 2026-08-04T16:30:53.4093027+00:00 | 113.0ms | GC pause | - | - | 866.7s / 746,266 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 692,228,000 | 2026-08-04T16:30:54.0411759+00:00 | 122.6ms | GC pause | - | - | 866.7s / 746,266 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 692,347,000 | 2026-08-04T16:30:54.1699026+00:00 | 166.8ms | GC pause | - | - | 867.7s / 726,626 msg/s | Gen2 +0 / pause +286.1ms |
| Confluent | 697,440,000 | 2026-08-04T16:31:01.1340498+00:00 | 113.3ms | GC pause | - | - | 874.7s / 746,457 msg/s | Gen2 +0 / pause +239.4ms |
| Confluent | 697,486,000 | 2026-08-04T16:31:01.1750233+00:00 | 113.7ms | GC pause | - | - | 874.7s / 746,457 msg/s | Gen2 +0 / pause +239.4ms |
| Confluent | 697,651,000 | 2026-08-04T16:31:01.3487656+00:00 | 140.3ms | GC pause | - | - | 874.7s / 746,457 msg/s | Gen2 +0 / pause +139.2ms |
| Confluent | 697,686,000 | 2026-08-04T16:31:01.3943519+00:00 | 117.9ms | GC pause | - | - | 874.7s / 746,457 msg/s | Gen2 +0 / pause +139.2ms |
| Confluent | 697,693,000 | 2026-08-04T16:31:01.4039765+00:00 | 118.9ms | GC pause | - | - | 874.7s / 746,457 msg/s | Gen2 +0 / pause +139.2ms |
| Confluent | 697,835,000 | 2026-08-04T16:31:01.5871963+00:00 | 116.0ms | GC pause | - | - | 874.7s / 746,457 msg/s | Gen2 +0 / pause +139.2ms |
| Confluent | 704,093,000 | 2026-08-04T16:31:09.4215533+00:00 | 119.8ms | GC pause | - | - | 882.7s / 928,446 msg/s | Gen2 +0 / pause +62.9ms |
| Confluent | 704,183,000 | 2026-08-04T16:31:09.5123985+00:00 | 118.9ms | GC pause | - | - | 882.7s / 928,446 msg/s | Gen2 +0 / pause +62.9ms |
| Confluent | 704,197,000 | 2026-08-04T16:31:09.5254502+00:00 | 140.3ms | GC pause | - | - | 882.7s / 928,446 msg/s | Gen2 +0 / pause +62.9ms |
| Confluent | 705,713,000 | 2026-08-04T16:31:11.2814094+00:00 | 106.2ms | GC pause | - | - | 884.7s / 867,397 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 705,770,000 | 2026-08-04T16:31:11.3409379+00:00 | 109.9ms | GC pause | - | - | 884.7s / 867,397 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 705,965,000 | 2026-08-04T16:31:11.5652105+00:00 | 108.9ms | GC pause | - | - | 884.7s / 867,397 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 705,999,000 | 2026-08-04T16:31:11.6113189+00:00 | 101.3ms | GC pause | - | - | 884.7s / 867,397 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 707,670,000 | 2026-08-04T16:31:13.5570874+00:00 | 113.9ms | GC pause | - | - | 886.7s / 877,611 msg/s | Gen2 +0 / pause +93.5ms |
| Confluent | 707,731,000 | 2026-08-04T16:31:13.6373818+00:00 | 122.7ms | GC pause | - | - | 886.7s / 877,611 msg/s | Gen2 +0 / pause +93.5ms |
| Confluent | 707,805,000 | 2026-08-04T16:31:13.7098337+00:00 | 101.0ms | GC pause | - | - | 886.7s / 877,611 msg/s | Gen2 +0 / pause +93.5ms |
| Confluent | 708,217,000 | 2026-08-04T16:31:14.1885156+00:00 | 202.4ms | GC pause | - | - | 887.7s / 860,765 msg/s | Gen2 +0 / pause +172.5ms |
| Confluent | 708,675,000 | 2026-08-04T16:31:14.743907+00:00 | 136.3ms | GC pause | - | - | 887.7s / 860,765 msg/s | Gen2 +0 / pause +79.0ms |
| Confluent | 708,745,000 | 2026-08-04T16:31:14.8302359+00:00 | 130.3ms | GC pause | - | - | 887.7s / 860,765 msg/s | Gen2 +0 / pause +79.0ms |
| Confluent | 708,752,000 | 2026-08-04T16:31:14.8367545+00:00 | 126.1ms | GC pause | - | - | 887.7s / 860,765 msg/s | Gen2 +0 / pause +79.0ms |
| Confluent | 708,859,000 | 2026-08-04T16:31:14.9473031+00:00 | 144.4ms | GC pause | - | - | 887.7s / 860,765 msg/s | Gen2 +0 / pause +79.0ms |
| Confluent | 708,874,000 | 2026-08-04T16:31:14.9652946+00:00 | 103.7ms | GC pause | - | - | 887.7s / 860,765 msg/s | Gen2 +0 / pause +79.0ms |
| Confluent | 709,014,000 | 2026-08-04T16:31:15.1190637+00:00 | 131.9ms | GC pause | - | - | 888.7s / 719,600 msg/s | Gen2 +0 / pause +199.2ms |
| Confluent | 709,130,000 | 2026-08-04T16:31:15.2581272+00:00 | 216.7ms | GC pause | - | - | 888.7s / 719,600 msg/s | Gen2 +0 / pause +120.2ms |
| Confluent | 709,385,000 | 2026-08-04T16:31:15.5937046+00:00 | 163.8ms | GC pause | - | - | 888.7s / 719,600 msg/s | Gen2 +0 / pause +120.2ms |
| Confluent | 709,403,000 | 2026-08-04T16:31:15.6220158+00:00 | 185.7ms | GC pause | - | - | 888.7s / 719,600 msg/s | Gen2 +0 / pause +120.2ms |
| Confluent | 709,457,000 | 2026-08-04T16:31:15.6932118+00:00 | 261.5ms | GC pause | - | - | 888.7s / 719,600 msg/s | Gen2 +0 / pause +120.2ms |
| Confluent | 709,699,000 | 2026-08-04T16:31:16.048602+00:00 | 108.3ms | GC pause | - | - | 888.7s / 719,600 msg/s | Gen2 +0 / pause +120.2ms |
| Confluent | 709,783,000 | 2026-08-04T16:31:16.1728214+00:00 | 124.8ms | GC pause | - | - | 889.7s / 910,111 msg/s | Gen2 +0 / pause +226.9ms |
| Confluent | 709,867,000 | 2026-08-04T16:31:16.2844099+00:00 | 176.4ms | GC pause | - | - | 889.7s / 910,111 msg/s | Gen2 +0 / pause +106.7ms |
| Confluent | 710,456,000 | 2026-08-04T16:31:16.952948+00:00 | 115.4ms | GC pause | - | - | 889.7s / 910,111 msg/s | Gen2 +0 / pause +106.7ms |
| Confluent | 710,561,000 | 2026-08-04T16:31:17.0580647+00:00 | 162.8ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +187.0ms |
| Confluent | 710,577,000 | 2026-08-04T16:31:17.0750751+00:00 | 172.1ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +187.0ms |
| Confluent | 710,581,000 | 2026-08-04T16:31:17.0777584+00:00 | 177.0ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +187.0ms |
| Confluent | 710,593,000 | 2026-08-04T16:31:17.0939696+00:00 | 181.3ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +187.0ms |
| Confluent | 710,761,000 | 2026-08-04T16:31:17.2510987+00:00 | 219.4ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 710,833,000 | 2026-08-04T16:31:17.3192753+00:00 | 230.2ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 711,014,000 | 2026-08-04T16:31:17.5697296+00:00 | 177.0ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 711,163,000 | 2026-08-04T16:31:17.8129002+00:00 | 103.1ms | GC pause | - | - | 890.7s / 715,963 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 714,984,000 | 2026-08-04T16:31:22.7062455+00:00 | 134.9ms | GC pause | - | - | 895.8s / 898,956 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 714,990,000 | 2026-08-04T16:31:22.7130232+00:00 | 147.3ms | GC pause | - | - | 895.8s / 898,956 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 715,011,000 | 2026-08-04T16:31:22.7340456+00:00 | 150.5ms | GC pause | - | - | 895.8s / 898,956 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 715,057,000 | 2026-08-04T16:31:22.7813568+00:00 | 174.1ms | GC pause | - | - | 895.8s / 898,956 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 715,229,000 | 2026-08-04T16:31:22.9532143+00:00 | 185.5ms | GC pause | - | - | 895.8s / 898,956 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 715,234,000 | 2026-08-04T16:31:22.9600493+00:00 | 169.6ms | GC pause | - | - | 895.8s / 898,956 msg/s | Gen2 +0 / pause +136.3ms |
| Confluent | 716,298,000 | 2026-08-04T16:31:24.2414897+00:00 | 123.1ms | GC pause | - | - | 897.8s / 724,347 msg/s | Gen2 +0 / pause +141.9ms |
| Confluent | 717,850,000 | 2026-08-04T16:31:26.1607831+00:00 | 165.7ms | GC pause | - | - | 899.8s / 761,516 msg/s | Gen2 +0 / pause +259.7ms |
| Confluent | 717,864,000 | 2026-08-04T16:31:26.1778836+00:00 | 183.4ms | GC pause | - | - | 899.8s / 761,516 msg/s | Gen2 +0 / pause +259.7ms |
| Dekaf | 642,244,000 | 2026-08-04T16:44:27.5789437+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,245,000 | 2026-08-04T16:44:27.5807295+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,246,000 | 2026-08-04T16:44:27.5821563+00:00 | 223.1ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,247,000 | 2026-08-04T16:44:27.5824717+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,248,000 | 2026-08-04T16:44:27.5831088+00:00 | 222.9ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,249,000 | 2026-08-04T16:44:27.5849027+00:00 | 222.3ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,250,000 | 2026-08-04T16:44:27.5867044+00:00 | 223.0ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,251,000 | 2026-08-04T16:44:27.5877017+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,252,000 | 2026-08-04T16:44:27.5887324+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,253,000 | 2026-08-04T16:44:27.5894276+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,254,000 | 2026-08-04T16:44:27.5901926+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 780.3s / 626,351 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 9,357,000 | 2026-08-04T17:01:41.1015667+00:00 | 101.1ms | GC pause | - | - | 12.0s / 844,739 msg/s | Gen2 +0 / pause +124.2ms |
| Confluent | 50,883,000 | 2026-08-04T17:02:17.4621795+00:00 | 102.4ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 50,913,000 | 2026-08-04T17:02:17.496694+00:00 | 101.1ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 50,921,000 | 2026-08-04T17:02:17.5026353+00:00 | 102.6ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 50,923,000 | 2026-08-04T17:02:17.5047169+00:00 | 101.5ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 50,928,000 | 2026-08-04T17:02:17.5084555+00:00 | 100.3ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 50,953,000 | 2026-08-04T17:02:17.5332221+00:00 | 104.2ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 50,960,000 | 2026-08-04T17:02:17.5399893+00:00 | 113.1ms | GC pause | - | - | 49.1s / 1,013,667 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 93,581,000 | 2026-08-04T17:02:54.0530559+00:00 | 100.6ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 93,587,000 | 2026-08-04T17:02:54.0570677+00:00 | 106.4ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 93,621,000 | 2026-08-04T17:02:54.0857314+00:00 | 103.1ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 93,638,000 | 2026-08-04T17:02:54.09744+00:00 | 108.1ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 93,641,000 | 2026-08-04T17:02:54.0993804+00:00 | 106.3ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 93,658,000 | 2026-08-04T17:02:54.1117292+00:00 | 102.0ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 93,668,000 | 2026-08-04T17:02:54.1191782+00:00 | 102.8ms | GC pause | - | - | 85.1s / 1,276,824 msg/s | Gen2 +0 / pause +42.6ms |
| Confluent | 108,548,000 | 2026-08-04T17:03:07.073838+00:00 | 100.5ms | GC pause | - | - | 98.1s / 1,328,953 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 108,637,000 | 2026-08-04T17:03:07.1431209+00:00 | 101.1ms | GC pause | - | - | 98.1s / 1,328,953 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 188,530,000 | 2026-08-04T17:04:13.1073932+00:00 | 102.9ms | GC pause | - | - | 164.1s / 1,247,022 msg/s | Gen2 +0 / pause +97.9ms |
| Confluent | 406,027,000 | 2026-08-04T17:07:36.0917611+00:00 | 101.0ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,041,000 | 2026-08-04T17:07:36.0994714+00:00 | 105.7ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,048,000 | 2026-08-04T17:07:36.1061923+00:00 | 110.2ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,051,000 | 2026-08-04T17:07:36.1084628+00:00 | 108.9ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,057,000 | 2026-08-04T17:07:36.1136428+00:00 | 105.5ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,067,000 | 2026-08-04T17:07:36.1203493+00:00 | 109.5ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,077,000 | 2026-08-04T17:07:36.1264514+00:00 | 107.1ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,097,000 | 2026-08-04T17:07:36.1428413+00:00 | 108.6ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,101,000 | 2026-08-04T17:07:36.1456312+00:00 | 106.0ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,108,000 | 2026-08-04T17:07:36.1571238+00:00 | 104.1ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,117,000 | 2026-08-04T17:07:36.1652496+00:00 | 103.5ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,121,000 | 2026-08-04T17:07:36.1682499+00:00 | 102.0ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 406,138,000 | 2026-08-04T17:07:36.1935358+00:00 | 102.3ms | GC pause | - | - | 367.3s / 1,099,063 msg/s | Gen2 +0 / pause +117.3ms |
| Confluent | 414,427,000 | 2026-08-04T17:07:43.0770745+00:00 | 105.8ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,431,000 | 2026-08-04T17:07:43.0798732+00:00 | 111.4ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,435,000 | 2026-08-04T17:07:43.081994+00:00 | 116.0ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,438,000 | 2026-08-04T17:07:43.0846006+00:00 | 114.6ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,439,000 | 2026-08-04T17:07:43.0852949+00:00 | 113.1ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,440,000 | 2026-08-04T17:07:43.0860051+00:00 | 104.9ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,443,000 | 2026-08-04T17:07:43.0879206+00:00 | 111.0ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,445,000 | 2026-08-04T17:07:43.0894858+00:00 | 110.1ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,447,000 | 2026-08-04T17:07:43.0909487+00:00 | 109.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,449,000 | 2026-08-04T17:07:43.0927195+00:00 | 107.9ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,450,000 | 2026-08-04T17:07:43.0938277+00:00 | 106.5ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,458,000 | 2026-08-04T17:07:43.1015983+00:00 | 108.0ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,464,000 | 2026-08-04T17:07:43.1096387+00:00 | 110.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,466,000 | 2026-08-04T17:07:43.110963+00:00 | 109.9ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,468,000 | 2026-08-04T17:07:43.1127598+00:00 | 111.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,469,000 | 2026-08-04T17:07:43.1139151+00:00 | 109.8ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,474,000 | 2026-08-04T17:07:43.1190077+00:00 | 122.0ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,476,000 | 2026-08-04T17:07:43.1203112+00:00 | 112.1ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,477,000 | 2026-08-04T17:07:43.1208783+00:00 | 121.4ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,478,000 | 2026-08-04T17:07:43.1215522+00:00 | 120.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,479,000 | 2026-08-04T17:07:43.124303+00:00 | 117.5ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,481,000 | 2026-08-04T17:07:43.1280868+00:00 | 114.3ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,483,000 | 2026-08-04T17:07:43.1308842+00:00 | 112.0ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,488,000 | 2026-08-04T17:07:43.1375934+00:00 | 108.0ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,489,000 | 2026-08-04T17:07:43.1381844+00:00 | 107.2ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,491,000 | 2026-08-04T17:07:43.1399393+00:00 | 105.8ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,493,000 | 2026-08-04T17:07:43.1423603+00:00 | 102.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,496,000 | 2026-08-04T17:07:43.1463644+00:00 | 107.3ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,499,000 | 2026-08-04T17:07:43.1503634+00:00 | 103.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,504,000 | 2026-08-04T17:07:43.1574782+00:00 | 102.8ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,517,000 | 2026-08-04T17:07:43.1731083+00:00 | 105.7ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 414,521,000 | 2026-08-04T17:07:43.178044+00:00 | 100.9ms | GC pause | - | - | 374.3s / 1,172,246 msg/s | Gen2 +0 / pause +84.8ms |
| Confluent | 456,554,000 | 2026-08-04T17:08:19.1675986+00:00 | 109.5ms | GC pause | - | - | 410.3s / 985,391 msg/s | Gen2 +0 / pause +142.3ms |
| Confluent | 456,577,000 | 2026-08-04T17:08:19.1885976+00:00 | 109.1ms | GC pause | - | - | 410.3s / 985,391 msg/s | Gen2 +0 / pause +142.3ms |
| Confluent | 456,580,000 | 2026-08-04T17:08:19.1902451+00:00 | 108.2ms | GC pause | - | - | 410.3s / 985,391 msg/s | Gen2 +0 / pause +142.3ms |
| Confluent | 456,581,000 | 2026-08-04T17:08:19.1968715+00:00 | 102.4ms | GC pause | - | - | 410.3s / 985,391 msg/s | Gen2 +0 / pause +142.3ms |
| Confluent | 456,584,000 | 2026-08-04T17:08:19.199346+00:00 | 105.7ms | GC pause | - | - | 410.3s / 985,391 msg/s | Gen2 +0 / pause +142.3ms |
| Confluent | 457,440,000 | 2026-08-04T17:08:19.9962213+00:00 | 100.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,447,000 | 2026-08-04T17:08:20.0002252+00:00 | 109.9ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,448,000 | 2026-08-04T17:08:20.0008385+00:00 | 109.3ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,450,000 | 2026-08-04T17:08:20.0021919+00:00 | 118.7ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,451,000 | 2026-08-04T17:08:20.0028082+00:00 | 107.5ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,457,000 | 2026-08-04T17:08:20.0071018+00:00 | 114.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,459,000 | 2026-08-04T17:08:20.0093642+00:00 | 112.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,466,000 | 2026-08-04T17:08:20.0148741+00:00 | 116.2ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,468,000 | 2026-08-04T17:08:20.0160054+00:00 | 161.3ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,469,000 | 2026-08-04T17:08:20.017081+00:00 | 126.9ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,470,000 | 2026-08-04T17:08:20.019259+00:00 | 143.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,473,000 | 2026-08-04T17:08:20.0225508+00:00 | 154.2ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,474,000 | 2026-08-04T17:08:20.0240031+00:00 | 140.2ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,479,000 | 2026-08-04T17:08:20.0297116+00:00 | 134.8ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,482,000 | 2026-08-04T17:08:20.0346596+00:00 | 142.0ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,490,000 | 2026-08-04T17:08:20.0463285+00:00 | 132.3ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,491,000 | 2026-08-04T17:08:20.0471894+00:00 | 149.3ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,496,000 | 2026-08-04T17:08:20.0539049+00:00 | 142.4ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,498,000 | 2026-08-04T17:08:20.0564115+00:00 | 141.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,500,000 | 2026-08-04T17:08:20.0586051+00:00 | 128.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,503,000 | 2026-08-04T17:08:20.0615536+00:00 | 127.4ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,507,000 | 2026-08-04T17:08:20.0656131+00:00 | 142.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,508,000 | 2026-08-04T17:08:20.0666094+00:00 | 141.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,509,000 | 2026-08-04T17:08:20.0673696+00:00 | 138.5ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,511,000 | 2026-08-04T17:08:20.0692666+00:00 | 140.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,515,000 | 2026-08-04T17:08:20.0731914+00:00 | 134.9ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,516,000 | 2026-08-04T17:08:20.0743172+00:00 | 133.8ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,517,000 | 2026-08-04T17:08:20.0754803+00:00 | 154.4ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,519,000 | 2026-08-04T17:08:20.0771631+00:00 | 132.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,520,000 | 2026-08-04T17:08:20.0779875+00:00 | 129.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,521,000 | 2026-08-04T17:08:20.0790179+00:00 | 151.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,522,000 | 2026-08-04T17:08:20.0801695+00:00 | 117.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,523,000 | 2026-08-04T17:08:20.0810781+00:00 | 126.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,524,000 | 2026-08-04T17:08:20.0819755+00:00 | 127.0ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,532,000 | 2026-08-04T17:08:20.0894486+00:00 | 119.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,539,000 | 2026-08-04T17:08:20.1001821+00:00 | 135.2ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,544,000 | 2026-08-04T17:08:20.1063671+00:00 | 128.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,545,000 | 2026-08-04T17:08:20.1076352+00:00 | 129.1ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,547,000 | 2026-08-04T17:08:20.1091959+00:00 | 137.6ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,548,000 | 2026-08-04T17:08:20.1134364+00:00 | 133.4ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,568,000 | 2026-08-04T17:08:20.1490606+00:00 | 113.7ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 457,577,000 | 2026-08-04T17:08:20.1619071+00:00 | 104.4ms | GC pause | - | - | 411.3s / 1,010,972 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 497,687,000 | 2026-08-04T17:08:55.0492524+00:00 | 115.6ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,698,000 | 2026-08-04T17:08:55.0601381+00:00 | 120.2ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,700,000 | 2026-08-04T17:08:55.0624511+00:00 | 105.7ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,705,000 | 2026-08-04T17:08:55.067398+00:00 | 107.8ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,707,000 | 2026-08-04T17:08:55.0686923+00:00 | 118.9ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,708,000 | 2026-08-04T17:08:55.0725423+00:00 | 115.2ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,712,000 | 2026-08-04T17:08:55.0746655+00:00 | 103.4ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,717,000 | 2026-08-04T17:08:55.0859596+00:00 | 106.5ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,728,000 | 2026-08-04T17:08:55.0988188+00:00 | 106.5ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,737,000 | 2026-08-04T17:08:55.1063592+00:00 | 109.4ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,754,000 | 2026-08-04T17:08:55.1198806+00:00 | 103.4ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,758,000 | 2026-08-04T17:08:55.1220845+00:00 | 111.6ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,759,000 | 2026-08-04T17:08:55.1230441+00:00 | 100.4ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,761,000 | 2026-08-04T17:08:55.1242394+00:00 | 109.6ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 497,767,000 | 2026-08-04T17:08:55.1278635+00:00 | 106.9ms | GC pause | - | - | 446.3s / 1,132,946 msg/s | Gen2 +0 / pause +87.6ms |
| Confluent | 501,244,000 | 2026-08-04T17:08:58.0653139+00:00 | 105.0ms | GC pause | - | - | 449.3s / 1,148,266 msg/s | Gen2 +0 / pause +107.5ms |
| Confluent | 501,272,000 | 2026-08-04T17:08:58.0979951+00:00 | 100.4ms | GC pause | - | - | 449.3s / 1,148,266 msg/s | Gen2 +0 / pause +107.5ms |
| Confluent | 501,274,000 | 2026-08-04T17:08:58.099865+00:00 | 103.6ms | GC pause | - | - | 449.3s / 1,148,266 msg/s | Gen2 +0 / pause +107.5ms |
| Confluent | 534,451,000 | 2026-08-04T17:09:30.1222866+00:00 | 108.5ms | GC pause | - | - | 481.4s / 1,031,252 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 534,458,000 | 2026-08-04T17:09:30.1306652+00:00 | 101.6ms | GC pause | - | - | 481.4s / 1,031,252 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 534,518,000 | 2026-08-04T17:09:30.1921293+00:00 | 100.3ms | GC pause | - | - | 481.4s / 1,031,252 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 670,460,000 | 2026-08-04T17:11:31.0715781+00:00 | 101.2ms | GC pause | - | - | 602.4s / 1,024,888 msg/s | Gen2 +0 / pause +73.7ms |
| Confluent | 708,377,000 | 2026-08-04T17:12:06.2048711+00:00 | 112.4ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,405,000 | 2026-08-04T17:12:06.2520759+00:00 | 101.3ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,407,000 | 2026-08-04T17:12:06.2545905+00:00 | 131.4ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,417,000 | 2026-08-04T17:12:06.2734704+00:00 | 121.8ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,419,000 | 2026-08-04T17:12:06.2770174+00:00 | 102.0ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,427,000 | 2026-08-04T17:12:06.2901358+00:00 | 113.9ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,429,000 | 2026-08-04T17:12:06.2923701+00:00 | 100.2ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 708,438,000 | 2026-08-04T17:12:06.3067616+00:00 | 108.1ms | GC pause | - | - | 637.5s / 913,648 msg/s | Gen2 +0 / pause +118.0ms |
| Confluent | 737,248,000 | 2026-08-04T17:12:35.7711366+00:00 | 104.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,251,000 | 2026-08-04T17:12:35.7741414+00:00 | 110.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,258,000 | 2026-08-04T17:12:35.7830797+00:00 | 110.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,266,000 | 2026-08-04T17:12:35.7907408+00:00 | 101.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,269,000 | 2026-08-04T17:12:35.7938256+00:00 | 108.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,272,000 | 2026-08-04T17:12:35.7966482+00:00 | 112.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,277,000 | 2026-08-04T17:12:35.8007865+00:00 | 117.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,282,000 | 2026-08-04T17:12:35.8066261+00:00 | 106.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,284,000 | 2026-08-04T17:12:35.8086001+00:00 | 114.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,285,000 | 2026-08-04T17:12:35.8093484+00:00 | 113.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,290,000 | 2026-08-04T17:12:35.8147877+00:00 | 114.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,294,000 | 2026-08-04T17:12:35.8187728+00:00 | 120.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,297,000 | 2026-08-04T17:12:35.8220606+00:00 | 123.8ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,298,000 | 2026-08-04T17:12:35.8230627+00:00 | 122.8ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,302,000 | 2026-08-04T17:12:35.8262056+00:00 | 114.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,315,000 | 2026-08-04T17:12:35.8397871+00:00 | 112.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,320,000 | 2026-08-04T17:12:35.8455979+00:00 | 110.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,322,000 | 2026-08-04T17:12:35.8480379+00:00 | 107.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,325,000 | 2026-08-04T17:12:35.8502396+00:00 | 110.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,326,000 | 2026-08-04T17:12:35.8514391+00:00 | 110.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,328,000 | 2026-08-04T17:12:35.8534243+00:00 | 116.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,329,000 | 2026-08-04T17:12:35.8545085+00:00 | 107.8ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,332,000 | 2026-08-04T17:12:35.8565149+00:00 | 108.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,340,000 | 2026-08-04T17:12:35.8640828+00:00 | 116.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,341,000 | 2026-08-04T17:12:35.86474+00:00 | 119.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,343,000 | 2026-08-04T17:12:35.8665788+00:00 | 113.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,344,000 | 2026-08-04T17:12:35.8672982+00:00 | 113.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,345,000 | 2026-08-04T17:12:35.8684419+00:00 | 113.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,347,000 | 2026-08-04T17:12:35.8699642+00:00 | 122.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,349,000 | 2026-08-04T17:12:35.8714688+00:00 | 110.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,355,000 | 2026-08-04T17:12:35.8762976+00:00 | 112.8ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,359,000 | 2026-08-04T17:12:35.8801997+00:00 | 117.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,360,000 | 2026-08-04T17:12:35.8808872+00:00 | 122.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,361,000 | 2026-08-04T17:12:35.8814368+00:00 | 128.8ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,362,000 | 2026-08-04T17:12:35.8822763+00:00 | 120.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,369,000 | 2026-08-04T17:12:35.8873333+00:00 | 122.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,370,000 | 2026-08-04T17:12:35.888017+00:00 | 125.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,372,000 | 2026-08-04T17:12:35.8892307+00:00 | 119.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,375,000 | 2026-08-04T17:12:35.8914508+00:00 | 123.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,378,000 | 2026-08-04T17:12:35.8937602+00:00 | 139.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,379,000 | 2026-08-04T17:12:35.8944967+00:00 | 120.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,382,000 | 2026-08-04T17:12:35.896708+00:00 | 121.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,383,000 | 2026-08-04T17:12:35.8974078+00:00 | 131.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,384,000 | 2026-08-04T17:12:35.8980468+00:00 | 130.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,385,000 | 2026-08-04T17:12:35.8990559+00:00 | 125.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,388,000 | 2026-08-04T17:12:35.901379+00:00 | 140.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,389,000 | 2026-08-04T17:12:35.9024698+00:00 | 130.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,394,000 | 2026-08-04T17:12:35.9065625+00:00 | 131.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,395,000 | 2026-08-04T17:12:35.907274+00:00 | 134.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,405,000 | 2026-08-04T17:12:35.9171804+00:00 | 140.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,406,000 | 2026-08-04T17:12:35.9184526+00:00 | 138.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,408,000 | 2026-08-04T17:12:35.920504+00:00 | 149.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,411,000 | 2026-08-04T17:12:35.9243026+00:00 | 145.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,414,000 | 2026-08-04T17:12:35.9272953+00:00 | 138.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,415,000 | 2026-08-04T17:12:35.9280452+00:00 | 137.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,416,000 | 2026-08-04T17:12:35.9299642+00:00 | 139.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,422,000 | 2026-08-04T17:12:35.9368255+00:00 | 134.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,425,000 | 2026-08-04T17:12:35.9404927+00:00 | 133.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,428,000 | 2026-08-04T17:12:35.9434666+00:00 | 148.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,430,000 | 2026-08-04T17:12:35.9449269+00:00 | 140.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,436,000 | 2026-08-04T17:12:35.9523863+00:00 | 139.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,439,000 | 2026-08-04T17:12:35.954689+00:00 | 147.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,441,000 | 2026-08-04T17:12:35.9568157+00:00 | 151.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,445,000 | 2026-08-04T17:12:35.9593946+00:00 | 144.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,451,000 | 2026-08-04T17:12:35.9658872+00:00 | 154.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,452,000 | 2026-08-04T17:12:35.9672462+00:00 | 140.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,454,000 | 2026-08-04T17:12:35.9687225+00:00 | 139.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,462,000 | 2026-08-04T17:12:35.9779338+00:00 | 140.9ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,464,000 | 2026-08-04T17:12:35.9816396+00:00 | 139.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,466,000 | 2026-08-04T17:12:35.9833911+00:00 | 137.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,467,000 | 2026-08-04T17:12:35.9841177+00:00 | 164.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,472,000 | 2026-08-04T17:12:35.9899776+00:00 | 141.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,480,000 | 2026-08-04T17:12:36.0018035+00:00 | 148.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,484,000 | 2026-08-04T17:12:36.0099887+00:00 | 148.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,488,000 | 2026-08-04T17:12:36.0184622+00:00 | 153.6ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,491,000 | 2026-08-04T17:12:36.0217987+00:00 | 150.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,493,000 | 2026-08-04T17:12:36.0247939+00:00 | 146.2ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,494,000 | 2026-08-04T17:12:36.0261549+00:00 | 138.5ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,495,000 | 2026-08-04T17:12:36.0274896+00:00 | 137.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,497,000 | 2026-08-04T17:12:36.0332623+00:00 | 147.7ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,504,000 | 2026-08-04T17:12:36.047997+00:00 | 127.4ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,511,000 | 2026-08-04T17:12:36.0722467+00:00 | 123.3ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,513,000 | 2026-08-04T17:12:36.0745022+00:00 | 112.1ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 737,521,000 | 2026-08-04T17:12:36.0951796+00:00 | 116.0ms | GC pause | - | - | 667.5s / 779,700 msg/s | Gen2 +0 / pause +119.8ms |
| Confluent | 824,677,000 | 2026-08-04T17:14:13.3856824+00:00 | 106.0ms | GC pause | - | - | 764.6s / 1,029,231 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 824,711,000 | 2026-08-04T17:14:13.4271304+00:00 | 112.6ms | GC pause | - | - | 764.6s / 1,029,231 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 824,727,000 | 2026-08-04T17:14:13.4434965+00:00 | 105.1ms | GC pause | - | - | 764.6s / 1,029,231 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 824,737,000 | 2026-08-04T17:14:13.4516526+00:00 | 107.6ms | GC pause | - | - | 764.6s / 1,029,231 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 824,741,000 | 2026-08-04T17:14:13.4558044+00:00 | 108.9ms | GC pause | - | - | 764.6s / 1,029,231 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 824,757,000 | 2026-08-04T17:14:13.4700723+00:00 | 110.1ms | GC pause | - | - | 764.6s / 1,029,231 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 825,431,000 | 2026-08-04T17:14:14.0959928+00:00 | 108.7ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,451,000 | 2026-08-04T17:14:14.1145895+00:00 | 106.6ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,467,000 | 2026-08-04T17:14:14.1262517+00:00 | 105.4ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,468,000 | 2026-08-04T17:14:14.1270404+00:00 | 104.6ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,477,000 | 2026-08-04T17:14:14.1327919+00:00 | 109.0ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,497,000 | 2026-08-04T17:14:14.1532963+00:00 | 104.9ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,508,000 | 2026-08-04T17:14:14.1687634+00:00 | 101.2ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,511,000 | 2026-08-04T17:14:14.1717184+00:00 | 100.7ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 825,561,000 | 2026-08-04T17:14:14.2289171+00:00 | 100.6ms | GC pause | - | - | 765.6s / 1,132,129 msg/s | Gen2 +0 / pause +49.5ms |
| Confluent | 828,291,000 | 2026-08-04T17:14:16.58205+00:00 | 100.5ms | GC pause | - | - | 767.6s / 1,106,347 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 845,311,000 | 2026-08-04T17:14:31.053432+00:00 | 119.9ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 845,327,000 | 2026-08-04T17:14:31.0663858+00:00 | 113.9ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 845,331,000 | 2026-08-04T17:14:31.0691357+00:00 | 111.3ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 845,338,000 | 2026-08-04T17:14:31.0737058+00:00 | 113.4ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 845,351,000 | 2026-08-04T17:14:31.083521+00:00 | 114.6ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 845,352,000 | 2026-08-04T17:14:31.0839468+00:00 | 102.6ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 845,358,000 | 2026-08-04T17:14:31.09862+00:00 | 102.4ms | GC pause | - | - | 782.6s / 1,177,289 msg/s | Gen2 +0 / pause +100.1ms |
| Confluent | 894,504,000 | 2026-08-04T17:15:24.1951619+00:00 | 106.8ms | GC pause | - | - | 835.6s / 1,099,759 msg/s | Gen2 +0 / pause +123.9ms |
| Confluent | 926,577,000 | 2026-08-04T17:15:58.5267662+00:00 | 104.0ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,601,000 | 2026-08-04T17:15:58.5502496+00:00 | 102.6ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,617,000 | 2026-08-04T17:15:58.5606999+00:00 | 102.8ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,621,000 | 2026-08-04T17:15:58.5628238+00:00 | 100.8ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,662,000 | 2026-08-04T17:15:58.6006314+00:00 | 113.0ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,668,000 | 2026-08-04T17:15:58.6097437+00:00 | 103.6ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,672,000 | 2026-08-04T17:15:58.6144314+00:00 | 102.6ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 926,702,000 | 2026-08-04T17:15:58.6465095+00:00 | 104.7ms | GC pause | - | - | 869.7s / 1,007,486 msg/s | Gen2 +0 / pause +101.7ms |
| Dekaf (3conn) | 415,733,000 | 2026-08-04T17:21:34.1263619+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,739,000 | 2026-08-04T17:21:34.1289745+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,740,000 | 2026-08-04T17:21:34.1295011+00:00 | 222.7ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,743,000 | 2026-08-04T17:21:34.1378625+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,747,000 | 2026-08-04T17:21:34.1394671+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,749,000 | 2026-08-04T17:21:34.1417915+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,750,000 | 2026-08-04T17:21:34.1421852+00:00 | 212.5ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,751,000 | 2026-08-04T17:21:34.1424949+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,752,000 | 2026-08-04T17:21:34.1429195+00:00 | 211.7ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,753,000 | 2026-08-04T17:21:34.1444585+00:00 | 214.7ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,754,000 | 2026-08-04T17:21:34.1447478+00:00 | 208.2ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 415,755,000 | 2026-08-04T17:21:34.145354+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 304.2s / 1,094,816 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*35,421 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.32x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 0.93x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.28 | 1163.85 | 994,847 | 1,008,520 | -13.1% | -1.57% | 948.76 | 994,847 | 0 | 1.28 |
| Dekaf (3conn) | 1.32 | 1200.16 | 977,519 | 970,976 | -19.9% | -1.85% | 932.23 | 977,519 | 0 | 1.29 |
| Confluent | 2.02 | - | 745,950 | 738,121 | +2.2% | -0.05% | 711.39 | 745,950 | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 330,021 | 366.69 | 894.04 KB |
| Dekaf | 2 | 325,864 | 362.07 | 896.71 KB |
| Dekaf | 3 | 330,877 | 367.64 | 915.13 KB |
| Dekaf (3conn) | 1 | 318,479 | 353.86 | 900.42 KB |
| Dekaf (3conn) | 2 | 332,831 | 369.81 | 903.38 KB |
| Dekaf (3conn) | 3 | 318,456 | 353.83 | 901.59 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:26.6869352+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 783,967 msg/s |
| Dekaf | 2026-08-04T16:31:44.7101073+00:00 | 3 | 16.0 MiB / 0.6 MiB | 450.5 MB/s | 0/0 | 957 | 18.0s / 1,039,110 msg/s |
| Dekaf | 2026-08-04T16:32:03.7261329+00:00 | 1 | 16.0 MiB / 3.9 MiB | 439.1 MB/s | 0/0 | 1,501 | 37.0s / 999,372 msg/s |
| Dekaf | 2026-08-04T16:32:21.7318395+00:00 | 1 | 14.0 MiB / 5.7 MiB | 464.8 MB/s | 1/0 | 3,041 | 55.1s / 1,052,077 msg/s |
| Dekaf | 2026-08-04T16:32:39.7454074+00:00 | 2 | 12.0 MiB / 3.4 MiB | 495.5 MB/s | 2/0 | 2,400 | 73.1s / 1,044,160 msg/s |
| Dekaf | 2026-08-04T16:32:57.7596024+00:00 | 2 | 12.0 MiB / 1.4 MiB | 495.5 MB/s | 2/0 | 3,367 | 91.1s / 1,132,962 msg/s |
| Dekaf | 2026-08-04T16:33:15.7755888+00:00 | 3 | 14.0 MiB / 9.1 MiB | 491.1 MB/s | 1/1 | 5,309 | 109.1s / 904,537 msg/s |
| Dekaf | 2026-08-04T16:33:33.8152327+00:00 | 3 | 14.0 MiB / 13.2 MiB | 491.1 MB/s | 1/1 | 6,469 | 127.1s / 1,142,763 msg/s |
| Dekaf | 2026-08-04T16:33:52.8205898+00:00 | 1 | 14.0 MiB / 14.0 MiB | 464.8 MB/s | 1/1 | 9,044 | 146.2s / 1,027,814 msg/s |
| Dekaf | 2026-08-04T16:34:10.8302695+00:00 | 1 | 14.0 MiB / 10.0 MiB | 464.8 MB/s | 1/2 | 9,837 | 164.2s / 1,058,558 msg/s |
| Dekaf | 2026-08-04T16:34:28.8475861+00:00 | 2 | 10.0 MiB / 10.0 MiB | 495.5 MB/s | 2/2 | 7,835 | 182.2s / 1,082,287 msg/s |
| Dekaf | 2026-08-04T16:34:46.86288+00:00 | 2 | 10.0 MiB / 1.0 MiB | 495.5 MB/s | 3/2 | 8,669 | 200.2s / 1,022,118 msg/s |
| Dekaf | 2026-08-04T16:35:04.8692542+00:00 | 3 | 10.0 MiB / 10.0 MiB | 502.6 MB/s | 3/2 | 15,594 | 218.2s / 1,322,946 msg/s |
| Dekaf | 2026-08-04T16:35:22.8803331+00:00 | 3 | 10.0 MiB / 0.8 MiB | 502.6 MB/s | 3/2 | 16,874 | 236.2s / 1,089,735 msg/s |
| Dekaf | 2026-08-04T16:35:41.900565+00:00 | 1 | 14.0 MiB / 3.8 MiB | 475.9 MB/s | 1/3 | 13,626 | 255.2s / 1,141,088 msg/s |
| Dekaf | 2026-08-04T16:35:59.9109816+00:00 | 1 | 14.0 MiB / 1.1 MiB | 485.2 MB/s | 1/3 | 14,493 | 273.2s / 1,112,470 msg/s |
| Dekaf | 2026-08-04T16:36:17.9197334+00:00 | 2 | 11.0 MiB / 2.4 MiB | 495.5 MB/s | 4/3 | 14,709 | 291.3s / 1,220,589 msg/s |
| Dekaf | 2026-08-04T16:36:35.9304001+00:00 | 2 | 11.0 MiB / 2.2 MiB | 495.5 MB/s | 4/3 | 15,480 | 309.3s / 1,159,904 msg/s |
| Dekaf | 2026-08-04T16:36:53.9430025+00:00 | 3 | 12.0 MiB / 1.1 MiB | 505.7 MB/s | 5/2 | 24,895 | 327.3s / 1,113,779 msg/s |
| Dekaf | 2026-08-04T16:37:11.9475663+00:00 | 3 | 12.0 MiB / 4.6 MiB | 505.7 MB/s | 5/2 | 26,199 | 345.3s / 938,720 msg/s |
| Dekaf | 2026-08-04T16:37:30.9577291+00:00 | 1 | 14.0 MiB / 12.4 MiB | 487.3 MB/s | 1/3 | 19,048 | 364.3s / 1,189,775 msg/s |
| Dekaf | 2026-08-04T16:37:48.974667+00:00 | 1 | 14.0 MiB / 1.0 MiB | 506.0 MB/s | 1/3 | 19,672 | 382.3s / 1,076,699 msg/s |
| Dekaf | 2026-08-04T16:38:06.9873834+00:00 | 2 | 11.0 MiB / 1.4 MiB | 499.9 MB/s | 4/4 | 20,422 | 400.4s / 973,247 msg/s |
| Dekaf | 2026-08-04T16:38:25.0154954+00:00 | 2 | 11.0 MiB / 0.8 MiB | 499.9 MB/s | 4/5 | 21,527 | 418.4s / 1,015,810 msg/s |
| Dekaf | 2026-08-04T16:38:43.0299866+00:00 | 3 | 13.0 MiB / 3.9 MiB | 505.7 MB/s | 6/3 | 31,147 | 436.4s / 1,003,220 msg/s |
| Dekaf | 2026-08-04T16:39:01.0448549+00:00 | 3 | 11.0 MiB / 9.1 MiB | 505.7 MB/s | 6/3 | 32,316 | 454.4s / 837,154 msg/s |
| Dekaf | 2026-08-04T16:39:20.0552705+00:00 | 1 | 12.0 MiB / 1.2 MiB | 506.0 MB/s | 1/4 | 23,682 | 473.5s / 812,562 msg/s |
| Dekaf | 2026-08-04T16:39:38.0770481+00:00 | 1 | 14.0 MiB / 3.5 MiB | 506.0 MB/s | 1/5 | 24,068 | 491.5s / 940,511 msg/s |
| Dekaf | 2026-08-04T16:39:56.0909458+00:00 | 2 | 11.0 MiB / 1.2 MiB | 499.9 MB/s | 4/6 | 24,453 | 509.5s / 842,410 msg/s |
| Dekaf | 2026-08-04T16:40:14.1033922+00:00 | 2 | 11.0 MiB / 3.4 MiB | 499.9 MB/s | 4/6 | 24,532 | 527.5s / 979,838 msg/s |
| Dekaf | 2026-08-04T16:40:32.1155053+00:00 | 3 | 13.0 MiB / 1.1 MiB | 505.7 MB/s | 6/4 | 34,173 | 545.5s / 778,274 msg/s |
| Dekaf | 2026-08-04T16:40:50.1334062+00:00 | 3 | 13.0 MiB / 2.2 MiB | 505.7 MB/s | 6/4 | 34,317 | 563.5s / 721,130 msg/s |
| Dekaf | 2026-08-04T16:41:09.1418164+00:00 | 1 | 14.0 MiB / 1.6 MiB | 506.0 MB/s | 1/5 | 24,730 | 582.5s / 772,909 msg/s |
| Dekaf | 2026-08-04T16:41:27.1514071+00:00 | 1 | 14.0 MiB / 1.7 MiB | 506.0 MB/s | 1/5 | 24,865 | 600.5s / 847,566 msg/s |
| Dekaf | 2026-08-04T16:41:45.1592901+00:00 | 2 | 11.0 MiB / 1.2 MiB | 499.9 MB/s | 4/6 | 25,471 | 618.6s / 777,305 msg/s |
| Dekaf | 2026-08-04T16:42:03.17598+00:00 | 2 | 11.0 MiB / 1.2 MiB | 499.9 MB/s | 4/6 | 25,657 | 636.6s / 759,306 msg/s |
| Dekaf | 2026-08-04T16:42:21.1858669+00:00 | 3 | 13.0 MiB / 1.5 MiB | 505.7 MB/s | 6/5 | 35,539 | 654.6s / 782,237 msg/s |
| Dekaf | 2026-08-04T16:42:39.1959761+00:00 | 3 | 13.0 MiB / 0.5 MiB | 505.7 MB/s | 6/5 | 35,868 | 672.6s / 909,310 msg/s |
| Dekaf | 2026-08-04T16:42:58.2043753+00:00 | 1 | 14.0 MiB / 3.4 MiB | 506.0 MB/s | 1/5 | 26,059 | 691.6s / 1,055,063 msg/s |
| Dekaf | 2026-08-04T16:43:16.228458+00:00 | 1 | 14.0 MiB / 2.3 MiB | 506.0 MB/s | 1/5 | 26,626 | 709.6s / 1,035,358 msg/s |
| Dekaf | 2026-08-04T16:43:34.2427078+00:00 | 2 | 9.0 MiB / 1.2 MiB | 499.9 MB/s | 4/7 | 28,393 | 727.7s / 842,142 msg/s |
| Dekaf | 2026-08-04T16:43:52.2545824+00:00 | 2 | 11.0 MiB / 2.6 MiB | 499.9 MB/s | 4/8 | 28,828 | 745.7s / 1,062,148 msg/s |
| Dekaf | 2026-08-04T16:44:10.266388+00:00 | 3 | 13.0 MiB / 1.5 MiB | 505.7 MB/s | 6/5 | 40,019 | 763.7s / 1,045,058 msg/s |
| Dekaf | 2026-08-04T16:44:28.2774862+00:00 | 3 | 13.0 MiB / 3.9 MiB | 505.7 MB/s | 6/5 | 40,689 | 781.7s / 1,060,273 msg/s |
| Dekaf | 2026-08-04T16:44:47.2942314+00:00 | 1 | 14.0 MiB / 2.0 MiB | 506.0 MB/s | 1/6 | 30,029 | 800.7s / 1,001,120 msg/s |
| Dekaf | 2026-08-04T16:45:05.3156512+00:00 | 1 | 14.0 MiB / 11.0 MiB | 506.0 MB/s | 1/6 | 30,792 | 818.7s / 738,931 msg/s |
| Dekaf | 2026-08-04T16:45:23.3246007+00:00 | 2 | 11.0 MiB / 1.0 MiB | 499.9 MB/s | 4/8 | 32,045 | 836.8s / 1,004,344 msg/s |
| Dekaf | 2026-08-04T16:45:41.3494333+00:00 | 2 | 11.0 MiB / 6.8 MiB | 499.9 MB/s | 4/8 | 32,311 | 854.8s / 748,196 msg/s |
| Dekaf | 2026-08-04T16:45:59.3644765+00:00 | 3 | 13.0 MiB / 8.6 MiB | 505.7 MB/s | 6/6 | 44,964 | 872.8s / 767,027 msg/s |
| Dekaf | 2026-08-04T16:46:17.3849978+00:00 | 3 | 13.0 MiB / 6.9 MiB | 505.7 MB/s | 6/6 | 45,923 | 890.8s / 1,076,708 msg/s |
| Dekaf (3conn) | 2026-08-04T16:46:49.2944854+00:00 | 3 | 16.0 MiB / 4.2 MiB | 382.5 MB/s | 0/0 | 338 | 9.0s / 975,564 msg/s |
| Dekaf (3conn) | 2026-08-04T16:47:07.3225707+00:00 | 3 | 16.0 MiB / 1.1 MiB | 383.1 MB/s | 0/0 | 1,016 | 27.1s / 921,861 msg/s |
| Dekaf (3conn) | 2026-08-04T16:47:26.3316427+00:00 | 1 | 16.0 MiB / 3.7 MiB | 428.1 MB/s | 0/1 | 1,297 | 46.1s / 946,648 msg/s |
| Dekaf (3conn) | 2026-08-04T16:47:44.3385277+00:00 | 1 | 16.0 MiB / 5.1 MiB | 428.1 MB/s | 0/1 | 1,916 | 64.1s / 1,064,038 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:02.3585234+00:00 | 2 | 16.0 MiB / 2.9 MiB | 498.5 MB/s | 0/1 | 2,687 | 82.1s / 1,004,641 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:20.3740611+00:00 | 2 | 16.0 MiB / 1.5 MiB | 529.1 MB/s | 0/1 | 2,971 | 100.1s / 1,186,255 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:38.3783961+00:00 | 3 | 18.0 MiB / 1.2 MiB | 521.1 MB/s | 0/1 | 3,288 | 118.1s / 1,141,758 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:56.3946117+00:00 | 3 | 16.0 MiB / 1.4 MiB | 565.4 MB/s | 0/2 | 3,523 | 136.2s / 1,100,455 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:15.4056327+00:00 | 1 | 12.0 MiB / 1.3 MiB | 540.0 MB/s | 1/1 | 5,631 | 155.2s / 1,251,420 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:33.4264355+00:00 | 1 | 10.0 MiB / 2.2 MiB | 540.0 MB/s | 2/1 | 6,604 | 173.2s / 1,114,539 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:51.4449612+00:00 | 2 | 12.0 MiB / 2.3 MiB | 590.2 MB/s | 2/2 | 4,967 | 191.2s / 1,092,655 msg/s |
| Dekaf (3conn) | 2026-08-04T16:50:09.4616953+00:00 | 2 | 12.0 MiB / 5.1 MiB | 590.2 MB/s | 2/2 | 5,491 | 209.2s / 1,154,716 msg/s |
| Dekaf (3conn) | 2026-08-04T16:50:27.4711853+00:00 | 3 | 16.0 MiB / 0.9 MiB | 565.4 MB/s | 0/2 | 5,284 | 227.3s / 974,264 msg/s |
| Dekaf (3conn) | 2026-08-04T16:50:45.4815153+00:00 | 3 | 14.0 MiB / 1.4 MiB | 565.4 MB/s | 0/2 | 5,360 | 245.3s / 1,033,587 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:04.4936344+00:00 | 1 | 8.0 MiB / 6.0 MiB | 540.0 MB/s | 4/1 | 14,914 | 264.3s / 1,127,361 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:22.5097336+00:00 | 1 | 9.0 MiB / 7.4 MiB | 540.0 MB/s | 5/1 | 16,601 | 282.3s / 1,118,923 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:40.5208574+00:00 | 2 | 8.0 MiB / 1.2 MiB | 590.2 MB/s | 4/2 | 10,109 | 300.4s / 1,041,689 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:58.529621+00:00 | 2 | 7.0 MiB / 7.0 MiB | 590.2 MB/s | 4/2 | 11,877 | 318.4s / 998,287 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:16.5434373+00:00 | 3 | 12.0 MiB / 1.2 MiB | 565.4 MB/s | 2/2 | 6,774 | 336.4s / 1,068,503 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:34.5526678+00:00 | 3 | 12.0 MiB / 2.9 MiB | 565.4 MB/s | 2/3 | 7,362 | 354.4s / 1,224,917 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:53.5672993+00:00 | 1 | 9.0 MiB / 7.3 MiB | 540.0 MB/s | 5/2 | 22,848 | 373.4s / 1,222,778 msg/s |
| Dekaf (3conn) | 2026-08-04T16:53:11.5805827+00:00 | 1 | 9.0 MiB / 8.1 MiB | 540.0 MB/s | 5/2 | 24,336 | 391.4s / 1,180,259 msg/s |
| Dekaf (3conn) | 2026-08-04T16:53:29.589793+00:00 | 2 | 8.0 MiB / 3.3 MiB | 590.2 MB/s | 4/4 | 20,578 | 409.4s / 848,075 msg/s |
| Dekaf (3conn) | 2026-08-04T16:53:47.6002729+00:00 | 2 | 8.0 MiB / 0.5 MiB | 590.2 MB/s | 4/4 | 23,368 | 427.5s / 1,169,439 msg/s |
| Dekaf (3conn) | 2026-08-04T16:54:05.614378+00:00 | 3 | 12.0 MiB / 12.0 MiB | 565.4 MB/s | 2/4 | 10,010 | 445.5s / 1,166,484 msg/s |
| Dekaf (3conn) | 2026-08-04T16:54:23.6227775+00:00 | 3 | 10.0 MiB / 3.4 MiB | 565.4 MB/s | 3/4 | 10,914 | 463.5s / 1,163,495 msg/s |
| Dekaf (3conn) | 2026-08-04T16:54:42.6433863+00:00 | 1 | 9.0 MiB / 2.2 MiB | 540.0 MB/s | 5/3 | 30,398 | 482.5s / 1,181,070 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:00.6547501+00:00 | 1 | 9.0 MiB / 2.1 MiB | 540.0 MB/s | 5/3 | 31,745 | 500.5s / 989,725 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:18.6653554+00:00 | 2 | 9.0 MiB / 3.0 MiB | 590.2 MB/s | 4/4 | 31,942 | 518.5s / 875,997 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:36.6931087+00:00 | 2 | 8.0 MiB / 2.0 MiB | 590.2 MB/s | 4/5 | 32,925 | 536.6s / 907,197 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:54.713768+00:00 | 3 | 9.0 MiB / 2.7 MiB | 565.4 MB/s | 5/4 | 15,445 | 554.6s / 947,818 msg/s |
| Dekaf (3conn) | 2026-08-04T16:56:12.7388869+00:00 | 3 | 10.0 MiB / 1.7 MiB | 565.4 MB/s | 6/4 | 16,092 | 572.6s / 836,916 msg/s |
| Dekaf (3conn) | 2026-08-04T16:56:31.7576859+00:00 | 1 | 9.0 MiB / 3.8 MiB | 540.0 MB/s | 5/5 | 36,701 | 591.6s / 860,859 msg/s |
| Dekaf (3conn) | 2026-08-04T16:56:49.7857401+00:00 | 1 | 10.0 MiB / 1.5 MiB | 540.0 MB/s | 5/5 | 37,210 | 609.6s / 745,019 msg/s |
| Dekaf (3conn) | 2026-08-04T16:57:07.8243921+00:00 | 2 | 8.0 MiB / 2.2 MiB | 590.2 MB/s | 4/6 | 39,701 | 627.7s / 832,912 msg/s |
| Dekaf (3conn) | 2026-08-04T16:57:25.8326161+00:00 | 2 | 8.0 MiB / 6.5 MiB | 590.2 MB/s | 4/6 | 40,810 | 645.7s / 653,772 msg/s |
| Dekaf (3conn) | 2026-08-04T16:57:43.8465467+00:00 | 3 | 10.0 MiB / 1.1 MiB | 565.4 MB/s | 6/5 | 18,142 | 663.7s / 855,169 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:01.8654667+00:00 | 3 | 8.0 MiB / 3.5 MiB | 565.4 MB/s | 6/5 | 18,727 | 681.7s / 718,338 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:20.8774668+00:00 | 1 | 9.0 MiB / 0.2 MiB | 540.0 MB/s | 5/7 | 41,726 | 700.7s / 667,059 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:38.890408+00:00 | 1 | 9.0 MiB / 1.4 MiB | 540.0 MB/s | 5/7 | 42,308 | 718.8s / 840,726 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:56.899387+00:00 | 2 | 8.0 MiB / 2.7 MiB | 590.2 MB/s | 4/6 | 45,189 | 736.8s / 842,170 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:14.9197788+00:00 | 2 | 8.0 MiB / 2.5 MiB | 590.2 MB/s | 4/6 | 46,441 | 754.8s / 841,323 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:32.9375781+00:00 | 3 | 9.0 MiB / 1.1 MiB | 565.4 MB/s | 7/6 | 20,527 | 772.8s / 714,289 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:50.9482877+00:00 | 3 | 11.0 MiB / 1.7 MiB | 565.4 MB/s | 7/7 | 20,786 | 790.9s / 731,649 msg/s |
| Dekaf (3conn) | 2026-08-04T17:00:09.9701265+00:00 | 1 | 9.0 MiB / 4.6 MiB | 540.0 MB/s | 5/7 | 46,439 | 809.9s / 716,281 msg/s |
| Dekaf (3conn) | 2026-08-04T17:00:27.9900577+00:00 | 1 | 9.0 MiB / 3.4 MiB | 540.0 MB/s | 5/7 | 47,551 | 827.9s / 908,671 msg/s |
| Dekaf (3conn) | 2026-08-04T17:00:46.0034062+00:00 | 2 | 8.0 MiB / 0.4 MiB | 590.2 MB/s | 4/7 | 54,598 | 845.9s / 968,397 msg/s |
| Dekaf (3conn) | 2026-08-04T17:01:04.0284146+00:00 | 2 | 9.0 MiB / 3.2 MiB | 590.2 MB/s | 4/7 | 54,890 | 863.9s / 868,214 msg/s |
| Dekaf (3conn) | 2026-08-04T17:01:22.0414189+00:00 | 3 | 9.0 MiB / 2.8 MiB | 565.4 MB/s | 7/8 | 22,532 | 882.0s / 1,004,907 msg/s |
| Dekaf (3conn) | 2026-08-04T17:01:40.0521903+00:00 | 3 | 9.0 MiB / 4.8 MiB | 565.4 MB/s | 8/8 | 23,334 | 900.0s / 743,834 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-04T16:31:56.8868537+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.2 MiB |
| Dekaf | 2026-08-04T16:31:56.9424928+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-04T16:32:11.9415603+00:00 | 1 | capacity | succeeded | 15,054ms | 14.0 MiB / 5.9 MiB |
| Dekaf | 2026-08-04T16:32:12.0235581+00:00 | 2 | capacity | succeeded | 15,081ms | 14.0 MiB / 0.9 MiB |
| Dekaf | 2026-08-04T16:32:15.0422296+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-04T16:32:30.112642+00:00 | 2 | capacity | succeeded | 15,070ms | 12.0 MiB / 1.9 MiB |
| Dekaf | 2026-08-04T16:32:42.1721109+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 5.0 MiB |
| Dekaf | 2026-08-04T16:32:57.1921898+00:00 | 1 | capacity | failed | 15,090ms | 14.0 MiB / 2.6 MiB |
| Dekaf | 2026-08-04T16:32:57.2404593+00:00 | 3 | capacity | failed | 15,071ms | 14.0 MiB / 1.1 MiB |
| Dekaf | 2026-08-04T16:33:15.3498734+00:00 | 2 | capacity | failed | 15,098ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:33:45.5112837+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 1.2 MiB |
| Dekaf | 2026-08-04T16:33:57.5575066+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 10.4 MiB |
| Dekaf | 2026-08-04T16:33:58.5542554+00:00 | 2 | capacity | failed | 13,042ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:34:12.6334188+00:00 | 3 | capacity | succeeded | 15,075ms | 12.0 MiB / 1.9 MiB |
| Dekaf | 2026-08-04T16:34:15.6462755+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 5.2 MiB |
| Dekaf | 2026-08-04T16:34:29.6712611+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 9.2 MiB |
| Dekaf | 2026-08-04T16:34:30.7247989+00:00 | 3 | capacity | succeeded | 15,078ms | 10.0 MiB / 1.9 MiB |
| Dekaf | 2026-08-04T16:34:33.7286614+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.8 MiB |
| Dekaf | 2026-08-04T16:34:44.7283208+00:00 | 1 | capacity | failed | 15,055ms | 14.0 MiB / 5.0 MiB |
| Dekaf | 2026-08-04T16:34:48.8260633+00:00 | 3 | capacity | failed | 15,097ms | 10.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-04T16:35:29.0982102+00:00 | 2 | capacity | failed | 15,133ms | 10.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-04T16:35:49.0851875+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 7.7 MiB |
| Dekaf | 2026-08-04T16:36:04.146542+00:00 | 3 | capacity | succeeded | 15,061ms | 11.0 MiB / 2.5 MiB |
| Dekaf | 2026-08-04T16:36:14.2919933+00:00 | 2 | capacity | succeeded | 15,064ms | 11.0 MiB / 2.4 MiB |
| Dekaf | 2026-08-04T16:36:34.2675177+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 0.6 MiB |
| Dekaf | 2026-08-04T16:36:49.3979968+00:00 | 3 | capacity | succeeded | 15,130ms | 12.0 MiB / 1.0 MiB |
| Dekaf | 2026-08-04T16:36:59.5282406+00:00 | 2 | capacity | failed | 15,062ms | 11.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-04T16:37:34.5763722+00:00 | 3 | capacity | succeeded | 15,048ms | 13.0 MiB / 3.0 MiB |
| Dekaf | 2026-08-04T16:37:59.824014+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.0 MiB |
| Dekaf | 2026-08-04T16:38:14.9377427+00:00 | 2 | capacity | failed | 15,113ms | 11.0 MiB / 6.9 MiB |
| Dekaf | 2026-08-04T16:38:17.3064411+00:00 | 3 | capacity | failed | 12,588ms | 13.0 MiB / 11.5 MiB |
| Dekaf | 2026-08-04T16:38:45.1356767+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 7.6 MiB |
| Dekaf | 2026-08-04T16:38:47.5404553+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-04T16:38:49.5601128+00:00 | 1 | capacity | failed | 3,516ms | 14.0 MiB / 8.8 MiB |
| Dekaf | 2026-08-04T16:39:02.5956666+00:00 | 3 | capacity | failed | 15,055ms | 13.0 MiB / 1.1 MiB |
| Dekaf | 2026-08-04T16:39:19.695018+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:41:03.1780122+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 1.1 MiB |
| Dekaf | 2026-08-04T16:41:18.2307895+00:00 | 3 | capacity | failed | 15,053ms | 13.0 MiB / 0.7 MiB |
| Dekaf | 2026-08-04T16:43:03.9199101+00:00 | 2 | capacity | failed | 2,518ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:43:34.0755634+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:43:36.0398468+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-04T16:43:51.1006683+00:00 | 1 | capacity | failed | 15,060ms | 14.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-04T16:45:19.4497032+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:10.5247883+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:10.5789926+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:25.6457572+00:00 | 2 | capacity | failed | 15,121ms | 16.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:25.7182929+00:00 | 3 | capacity | failed | 15,139ms | 16.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:25.7780376+00:00 | 1 | capacity | failed | 15,100ms | 16.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:26.0228135+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 9.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:26.2046442+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:41.2069881+00:00 | 3 | capacity | failed | 15,184ms | 16.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:41.2913937+00:00 | 1 | capacity | succeeded | 15,085ms | 14.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:11.4495981+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:26.4044892+00:00 | 2 | capacity | succeeded | 15,145ms | 14.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:26.5246774+00:00 | 1 | capacity | succeeded | 15,075ms | 12.0 MiB / 0.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:29.5509619+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:44.4851912+00:00 | 2 | capacity | succeeded | 15,070ms | 12.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:50:14.6895596+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:50:14.8073309+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:50:29.8719376+00:00 | 1 | capacity | succeeded | 15,064ms | 8.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:50:41.9064218+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:50:56.9760817+00:00 | 3 | capacity | succeeded | 15,069ms | 14.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:00.0323977+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:15.0216705+00:00 | 2 | capacity | succeeded | 15,065ms | 8.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:27.1432266+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:42.2164622+00:00 | 3 | capacity | succeeded | 15,073ms | 12.0 MiB / 8.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:45.3329967+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:00.4194639+00:00 | 2 | capacity | failed | 15,236ms | 8.0 MiB / 1.0 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:12.5008933+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:27.6557112+00:00 | 3 | capacity | failed | 15,154ms | 12.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:53:00.7638142+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:53:15.8309737+00:00 | 2 | capacity | failed | 15,067ms | 8.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:53:15.8974182+00:00 | 1 | capacity | failed | 15,087ms | 9.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:53:29.9805683+00:00 | 3 | capacity | failed | 2,020ms | 12.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-08-04T16:54:00.1472737+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-08-04T16:54:45.3483289+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:00.4161247+00:00 | 3 | capacity | succeeded | 15,067ms | 8.0 MiB / 0.7 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:03.4283928+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:16.740976+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:18.5069752+00:00 | 3 | capacity | succeeded | 15,078ms | 9.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:31.8499507+00:00 | 1 | capacity | failed | 15,108ms | 9.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:48.7852876+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:02.0956586+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:03.8685349+00:00 | 3 | capacity | succeeded | 15,083ms | 10.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:16.3804953+00:00 | 2 | capacity | failed | 15,065ms | 8.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:34.0435127+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:47.4013586+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:02.002663+00:00 | 1 | capacity | failed | 14,601ms | 9.0 MiB / 5.8 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:32.2202001+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:49.5208014+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:58:04.6648607+00:00 | 3 | capacity | failed | 15,144ms | 10.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:58:49.9184439+00:00 | 3 | capacity | succeeded | 15,078ms | 11.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:59:20.1628008+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:59:35.2633311+00:00 | 3 | capacity | failed | 15,100ms | 11.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-08-04T17:00:22.0425478+00:00 | 2 | capacity | failed | 4,048ms | 8.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:00:35.6837445+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:00:52.2755998+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-08-04T17:01:07.328148+00:00 | 2 | capacity | succeeded | 15,052ms | 9.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-08-04T17:01:23.9290317+00:00 | 3 | capacity | succeeded | 15,080ms | 9.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:01:37.4947164+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 0.5 MiB |
*43 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1 |
| Dekaf | 1 | 0.002–0.004ms | 4 |
| Dekaf | 1 | 0.004–0.008ms | 5 |
| Dekaf | 1 | 0.008–0.016ms | 13 |
| Dekaf | 1 | 0.016–0.032ms | 56 |
| Dekaf | 1 | 0.032–0.064ms | 86 |
| Dekaf | 1 | 0.064–0.128ms | 110 |
| Dekaf | 1 | 0.128–0.256ms | 131 |
| Dekaf | 1 | 0.256–0.512ms | 212 |
| Dekaf | 1 | 0.512–1.024ms | 344 |
| Dekaf | 1 | 1.024–2.048ms | 500 |
| Dekaf | 1 | 2.048–4.096ms | 615 |
| Dekaf | 1 | 4.096–8.192ms | 657 |
| Dekaf | 1 | 8.192–16.384ms | 440 |
| Dekaf | 1 | 16.384–32.768ms | 175 |
| Dekaf | 1 | 32.768–65.536ms | 21 |
| Dekaf | 2 | 0.001–0.002ms | 2 |
| Dekaf | 2 | 0.002–0.004ms | 2 |
| Dekaf | 2 | 0.004–0.008ms | 5 |
| Dekaf | 2 | 0.008–0.016ms | 23 |
| Dekaf | 2 | 0.016–0.032ms | 93 |
| Dekaf | 2 | 0.032–0.064ms | 157 |
| Dekaf | 2 | 0.064–0.128ms | 201 |
| Dekaf | 2 | 0.128–0.256ms | 216 |
| Dekaf | 2 | 0.256–0.512ms | 321 |
| Dekaf | 2 | 0.512–1.024ms | 579 |
| Dekaf | 2 | 1.024–2.048ms | 750 |
| Dekaf | 2 | 2.048–4.096ms | 907 |
| Dekaf | 2 | 4.096–8.192ms | 693 |
| Dekaf | 2 | 8.192–16.384ms | 301 |
| Dekaf | 2 | 16.384–32.768ms | 71 |
| Dekaf | 2 | 32.768–65.536ms | 9 |
| Dekaf | 2 | 65.536–131.072ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 3 |
| Dekaf | 3 | 0.002–0.004ms | 2 |
| Dekaf | 3 | 0.004–0.008ms | 11 |
| Dekaf | 3 | 0.008–0.016ms | 40 |
| Dekaf | 3 | 0.016–0.032ms | 90 |
| Dekaf | 3 | 0.032–0.064ms | 203 |
| Dekaf | 3 | 0.064–0.128ms | 236 |
| Dekaf | 3 | 0.128–0.256ms | 278 |
| Dekaf | 3 | 0.256–0.512ms | 415 |
| Dekaf | 3 | 0.512–1.024ms | 735 |
| Dekaf | 3 | 1.024–2.048ms | 992 |
| Dekaf | 3 | 2.048–4.096ms | 1,153 |
| Dekaf | 3 | 4.096–8.192ms | 964 |
| Dekaf | 3 | 8.192–16.384ms | 503 |
| Dekaf | 3 | 16.384–32.768ms | 142 |
| Dekaf | 3 | 32.768–65.536ms | 9 |
| Dekaf | 3 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 3 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 6 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 10 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 29 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 115 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 259 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 307 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 305 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 415 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 674 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,010 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,159 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 973 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 509 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 163 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 20 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 1 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 3 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 4 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 20 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 27 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 101 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 269 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 401 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 447 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 474 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 809 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 1,144 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 1,406 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,120 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 517 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 124 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 6 |
| Dekaf (3conn) | 2 | 131.072–262.144ms | 1 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 1 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 2 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 3 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 13 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 45 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 120 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 137 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 142 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 199 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 331 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 435 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 547 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 453 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 271 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 72 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 4 |
| Dekaf (3conn) | 3 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 3 | 262.144–524.288ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 17,000 | 2026-08-04T16:16:26.595909+00:00 | 117.2ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 20,000 | 2026-08-04T16:16:26.5997957+00:00 | 159.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 23,000 | 2026-08-04T16:16:26.6041763+00:00 | 172.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 27,000 | 2026-08-04T16:16:26.6114041+00:00 | 161.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 31,000 | 2026-08-04T16:16:26.6188013+00:00 | 187.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 34,000 | 2026-08-04T16:16:26.6245009+00:00 | 192.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 41,000 | 2026-08-04T16:16:26.6372649+00:00 | 234.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 42,000 | 2026-08-04T16:16:26.638664+00:00 | 165.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 51,000 | 2026-08-04T16:16:26.6572834+00:00 | 235.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 53,000 | 2026-08-04T16:16:26.6623432+00:00 | 150.6ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 57,000 | 2026-08-04T16:16:26.6750114+00:00 | 220.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 60,000 | 2026-08-04T16:16:26.6801405+00:00 | 140.2ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 63,000 | 2026-08-04T16:16:26.6844795+00:00 | 136.1ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 72,000 | 2026-08-04T16:16:26.7007693+00:00 | 126.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 73,000 | 2026-08-04T16:16:26.7020502+00:00 | 129.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 77,000 | 2026-08-04T16:16:26.7112119+00:00 | 247.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 81,000 | 2026-08-04T16:16:26.7192989+00:00 | 255.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 83,000 | 2026-08-04T16:16:26.7225707+00:00 | 115.0ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 90,000 | 2026-08-04T16:16:26.7357874+00:00 | 108.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 91,000 | 2026-08-04T16:16:26.7379477+00:00 | 240.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 101,000 | 2026-08-04T16:16:26.7575938+00:00 | 283.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 114,000 | 2026-08-04T16:16:26.8007697+00:00 | 271.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 128,000 | 2026-08-04T16:16:26.837114+00:00 | 284.2ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 131,000 | 2026-08-04T16:16:26.85479+00:00 | 286.1ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 141,000 | 2026-08-04T16:16:26.8764522+00:00 | 284.6ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 148,000 | 2026-08-04T16:16:26.8894505+00:00 | 274.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 211,000 | 2026-08-04T16:16:27.0668123+00:00 | 213.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 217,000 | 2026-08-04T16:16:27.1222003+00:00 | 164.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 221,000 | 2026-08-04T16:16:27.1304532+00:00 | 156.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 228,000 | 2026-08-04T16:16:27.1799448+00:00 | 115.6ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 257,000 | 2026-08-04T16:16:27.2138287+00:00 | 203.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 258,000 | 2026-08-04T16:16:27.2168415+00:00 | 200.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 261,000 | 2026-08-04T16:16:27.2212428+00:00 | 196.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 267,000 | 2026-08-04T16:16:27.2307582+00:00 | 187.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 268,000 | 2026-08-04T16:16:27.232226+00:00 | 188.8ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 271,000 | 2026-08-04T16:16:27.2348532+00:00 | 186.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 278,000 | 2026-08-04T16:16:27.2454212+00:00 | 176.2ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 281,000 | 2026-08-04T16:16:27.2488697+00:00 | 180.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 291,000 | 2026-08-04T16:16:27.264089+00:00 | 177.8ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 294,000 | 2026-08-04T16:16:27.2671537+00:00 | 179.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 295,000 | 2026-08-04T16:16:27.2681891+00:00 | 121.8ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 296,000 | 2026-08-04T16:16:27.2700325+00:00 | 202.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 297,000 | 2026-08-04T16:16:27.2712146+00:00 | 171.1ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 299,000 | 2026-08-04T16:16:27.2731511+00:00 | 200.0ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 301,000 | 2026-08-04T16:16:27.2792572+00:00 | 163.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 309,000 | 2026-08-04T16:16:27.2899862+00:00 | 223.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 310,000 | 2026-08-04T16:16:27.2907497+00:00 | 112.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 312,000 | 2026-08-04T16:16:27.2932925+00:00 | 113.2ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 313,000 | 2026-08-04T16:16:27.2978956+00:00 | 108.7ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 314,000 | 2026-08-04T16:16:27.2991084+00:00 | 157.4ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 316,000 | 2026-08-04T16:16:27.300512+00:00 | 215.0ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 317,000 | 2026-08-04T16:16:27.3013144+00:00 | 151.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 318,000 | 2026-08-04T16:16:27.3019455+00:00 | 151.0ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 323,000 | 2026-08-04T16:16:27.3079173+00:00 | 106.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 324,000 | 2026-08-04T16:16:27.3100549+00:00 | 148.3ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 326,000 | 2026-08-04T16:16:27.3130458+00:00 | 247.7ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 329,000 | 2026-08-04T16:16:27.3196568+00:00 | 245.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 334,000 | 2026-08-04T16:16:27.3287342+00:00 | 165.2ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 336,000 | 2026-08-04T16:16:27.3320032+00:00 | 267.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 338,000 | 2026-08-04T16:16:27.3877913+00:00 | 120.8ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 341,000 | 2026-08-04T16:16:27.3929638+00:00 | 115.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 346,000 | 2026-08-04T16:16:27.4035071+00:00 | 238.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 348,000 | 2026-08-04T16:16:27.4054119+00:00 | 123.9ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 351,000 | 2026-08-04T16:16:27.4104772+00:00 | 123.5ms | GC pause | - | - | 1.0s / 422,293 msg/s | Gen2 +1 / pause +87.3ms |
| Confluent | 355,000 | 2026-08-04T16:16:27.4170423+00:00 | 232.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 356,000 | 2026-08-04T16:16:27.4200089+00:00 | 229.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 361,000 | 2026-08-04T16:16:27.4267598+00:00 | 143.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 365,000 | 2026-08-04T16:16:27.4395412+00:00 | 229.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 368,000 | 2026-08-04T16:16:27.4450865+00:00 | 129.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 375,000 | 2026-08-04T16:16:27.4619694+00:00 | 210.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 377,000 | 2026-08-04T16:16:27.4669154+00:00 | 145.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 381,000 | 2026-08-04T16:16:27.4736668+00:00 | 139.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 388,000 | 2026-08-04T16:16:27.4835748+00:00 | 133.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 409,000 | 2026-08-04T16:16:27.5233904+00:00 | 212.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 417,000 | 2026-08-04T16:16:27.5349588+00:00 | 134.2ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +1 / pause +201.6ms |
| Confluent | 424,000 | 2026-08-04T16:16:27.5462192+00:00 | 132.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 425,000 | 2026-08-04T16:16:27.5469498+00:00 | 193.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 426,000 | 2026-08-04T16:16:27.5477561+00:00 | 193.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 436,000 | 2026-08-04T16:16:27.5629948+00:00 | 203.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 437,000 | 2026-08-04T16:16:27.5645598+00:00 | 121.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 439,000 | 2026-08-04T16:16:27.5677999+00:00 | 198.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 444,000 | 2026-08-04T16:16:27.5759601+00:00 | 112.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 449,000 | 2026-08-04T16:16:27.588586+00:00 | 183.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 465,000 | 2026-08-04T16:16:27.6200454+00:00 | 171.3ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 475,000 | 2026-08-04T16:16:27.6816879+00:00 | 127.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 476,000 | 2026-08-04T16:16:27.6835759+00:00 | 125.3ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 485,000 | 2026-08-04T16:16:27.7015796+00:00 | 117.2ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 496,000 | 2026-08-04T16:16:27.7145652+00:00 | 105.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 499,000 | 2026-08-04T16:16:27.7166657+00:00 | 108.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 516,000 | 2026-08-04T16:16:27.7317401+00:00 | 221.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 519,000 | 2026-08-04T16:16:27.7341072+00:00 | 220.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 535,000 | 2026-08-04T16:16:27.7580565+00:00 | 231.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 539,000 | 2026-08-04T16:16:27.7640262+00:00 | 234.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 545,000 | 2026-08-04T16:16:27.7707915+00:00 | 234.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 546,000 | 2026-08-04T16:16:27.7714382+00:00 | 234.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 555,000 | 2026-08-04T16:16:27.7827133+00:00 | 227.2ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 566,000 | 2026-08-04T16:16:27.7940719+00:00 | 225.2ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 569,000 | 2026-08-04T16:16:27.8049559+00:00 | 216.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 575,000 | 2026-08-04T16:16:27.8172684+00:00 | 205.2ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 579,000 | 2026-08-04T16:16:27.8262104+00:00 | 196.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 582,000 | 2026-08-04T16:16:27.8297454+00:00 | 110.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 583,000 | 2026-08-04T16:16:27.8307945+00:00 | 119.7ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 586,000 | 2026-08-04T16:16:27.8347062+00:00 | 200.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 593,000 | 2026-08-04T16:16:27.8478071+00:00 | 106.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 602,000 | 2026-08-04T16:16:27.8602698+00:00 | 100.3ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 613,000 | 2026-08-04T16:16:27.8719763+00:00 | 101.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 625,000 | 2026-08-04T16:16:27.9047193+00:00 | 155.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 635,000 | 2026-08-04T16:16:27.9127072+00:00 | 151.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 639,000 | 2026-08-04T16:16:27.9157356+00:00 | 148.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 649,000 | 2026-08-04T16:16:27.9240766+00:00 | 141.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 655,000 | 2026-08-04T16:16:27.9329257+00:00 | 143.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 659,000 | 2026-08-04T16:16:27.9369086+00:00 | 140.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 665,000 | 2026-08-04T16:16:27.94422+00:00 | 133.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 666,000 | 2026-08-04T16:16:27.9449261+00:00 | 132.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 670,000 | 2026-08-04T16:16:27.9479334+00:00 | 109.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 675,000 | 2026-08-04T16:16:27.9599389+00:00 | 128.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 676,000 | 2026-08-04T16:16:27.9625131+00:00 | 126.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 679,000 | 2026-08-04T16:16:27.9662925+00:00 | 122.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 686,000 | 2026-08-04T16:16:27.9753833+00:00 | 122.7ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 692,000 | 2026-08-04T16:16:27.9805853+00:00 | 101.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 695,000 | 2026-08-04T16:16:27.9859275+00:00 | 127.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 705,000 | 2026-08-04T16:16:28.0040098+00:00 | 120.5ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 706,000 | 2026-08-04T16:16:28.0057367+00:00 | 118.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 716,000 | 2026-08-04T16:16:28.0213385+00:00 | 118.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 729,000 | 2026-08-04T16:16:28.0535169+00:00 | 127.6ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 736,000 | 2026-08-04T16:16:28.0694519+00:00 | 114.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 739,000 | 2026-08-04T16:16:28.0787153+00:00 | 105.9ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 806,000 | 2026-08-04T16:16:28.179271+00:00 | 213.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 816,000 | 2026-08-04T16:16:28.1937196+00:00 | 204.1ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 819,000 | 2026-08-04T16:16:28.1983376+00:00 | 205.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 825,000 | 2026-08-04T16:16:28.2029882+00:00 | 200.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 834,000 | 2026-08-04T16:16:28.214033+00:00 | 110.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 837,000 | 2026-08-04T16:16:28.2177245+00:00 | 107.4ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 839,000 | 2026-08-04T16:16:28.2195495+00:00 | 194.8ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 845,000 | 2026-08-04T16:16:28.2295323+00:00 | 185.0ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 849,000 | 2026-08-04T16:16:28.2355873+00:00 | 186.2ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 856,000 | 2026-08-04T16:16:28.2447647+00:00 | 177.3ms | GC pause | - | - | 2.0s / 591,667 msg/s | Gen2 +0 / pause +114.3ms |
| Confluent | 1,034,000 | 2026-08-04T16:16:28.5754189+00:00 | 104.2ms | GC pause | - | - | 3.0s / 557,543 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 1,524,000 | 2026-08-04T16:16:29.4779959+00:00 | 129.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +337.0ms |
| Confluent | 1,537,000 | 2026-08-04T16:16:29.5031476+00:00 | 162.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +337.0ms |
| Confluent | 1,551,000 | 2026-08-04T16:16:29.5222861+00:00 | 160.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +337.0ms |
| Confluent | 1,554,000 | 2026-08-04T16:16:29.5245539+00:00 | 178.9ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +337.0ms |
| Confluent | 1,557,000 | 2026-08-04T16:16:29.5266938+00:00 | 164.2ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +337.0ms |
| Confluent | 1,561,000 | 2026-08-04T16:16:29.5304302+00:00 | 160.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +337.0ms |
| Confluent | 1,584,000 | 2026-08-04T16:16:29.560832+00:00 | 208.7ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,598,000 | 2026-08-04T16:16:29.5761213+00:00 | 158.0ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,604,000 | 2026-08-04T16:16:29.582565+00:00 | 201.2ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,612,000 | 2026-08-04T16:16:29.5887481+00:00 | 100.0ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,622,000 | 2026-08-04T16:16:29.6050749+00:00 | 113.0ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,623,000 | 2026-08-04T16:16:29.6058414+00:00 | 102.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,627,000 | 2026-08-04T16:16:29.6118636+00:00 | 189.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,631,000 | 2026-08-04T16:16:29.6148232+00:00 | 194.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,632,000 | 2026-08-04T16:16:29.6168129+00:00 | 105.9ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,633,000 | 2026-08-04T16:16:29.6177454+00:00 | 106.3ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,634,000 | 2026-08-04T16:16:29.6183555+00:00 | 248.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,643,000 | 2026-08-04T16:16:29.6252571+00:00 | 105.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,648,000 | 2026-08-04T16:16:29.6287015+00:00 | 204.0ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,650,000 | 2026-08-04T16:16:29.6306867+00:00 | 100.7ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,651,000 | 2026-08-04T16:16:29.6312309+00:00 | 210.2ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,653,000 | 2026-08-04T16:16:29.6329775+00:00 | 117.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,654,000 | 2026-08-04T16:16:29.6336482+00:00 | 244.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,664,000 | 2026-08-04T16:16:29.6403338+00:00 | 239.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,667,000 | 2026-08-04T16:16:29.6433187+00:00 | 204.3ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,668,000 | 2026-08-04T16:16:29.6444913+00:00 | 205.0ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,670,000 | 2026-08-04T16:16:29.6459544+00:00 | 139.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,677,000 | 2026-08-04T16:16:29.6542588+00:00 | 210.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,680,000 | 2026-08-04T16:16:29.6599197+00:00 | 140.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,681,000 | 2026-08-04T16:16:29.6612641+00:00 | 204.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,683,000 | 2026-08-04T16:16:29.6635119+00:00 | 137.7ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,684,000 | 2026-08-04T16:16:29.6660648+00:00 | 221.0ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,687,000 | 2026-08-04T16:16:29.6697974+00:00 | 196.6ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,688,000 | 2026-08-04T16:16:29.6705834+00:00 | 195.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,690,000 | 2026-08-04T16:16:29.6788136+00:00 | 130.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,691,000 | 2026-08-04T16:16:29.6796893+00:00 | 191.2ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,701,000 | 2026-08-04T16:16:29.7215061+00:00 | 150.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,718,000 | 2026-08-04T16:16:29.7514778+00:00 | 128.2ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,721,000 | 2026-08-04T16:16:29.7567059+00:00 | 123.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,727,000 | 2026-08-04T16:16:29.7612154+00:00 | 120.9ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,728,000 | 2026-08-04T16:16:29.7619397+00:00 | 120.3ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,730,000 | 2026-08-04T16:16:29.7631823+00:00 | 156.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,731,000 | 2026-08-04T16:16:29.764155+00:00 | 122.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,733,000 | 2026-08-04T16:16:29.7654408+00:00 | 154.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,738,000 | 2026-08-04T16:16:29.7717192+00:00 | 115.6ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,741,000 | 2026-08-04T16:16:29.7765097+00:00 | 111.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,747,000 | 2026-08-04T16:16:29.7838371+00:00 | 104.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,751,000 | 2026-08-04T16:16:29.7871305+00:00 | 109.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,757,000 | 2026-08-04T16:16:29.7926908+00:00 | 109.2ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,761,000 | 2026-08-04T16:16:29.7997765+00:00 | 114.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,762,000 | 2026-08-04T16:16:29.8018239+00:00 | 125.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,763,000 | 2026-08-04T16:16:29.8024736+00:00 | 135.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,770,000 | 2026-08-04T16:16:29.8100909+00:00 | 131.7ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,771,000 | 2026-08-04T16:16:29.8108454+00:00 | 108.6ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,778,000 | 2026-08-04T16:16:29.8181409+00:00 | 117.5ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,781,000 | 2026-08-04T16:16:29.8213369+00:00 | 114.6ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,787,000 | 2026-08-04T16:16:29.8318207+00:00 | 111.3ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,792,000 | 2026-08-04T16:16:29.8395889+00:00 | 104.3ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,793,000 | 2026-08-04T16:16:29.8404594+00:00 | 121.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,800,000 | 2026-08-04T16:16:29.8526611+00:00 | 115.6ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,801,000 | 2026-08-04T16:16:29.8536449+00:00 | 117.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,803,000 | 2026-08-04T16:16:29.8565281+00:00 | 112.3ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,807,000 | 2026-08-04T16:16:29.8692421+00:00 | 111.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,811,000 | 2026-08-04T16:16:29.877026+00:00 | 104.1ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,814,000 | 2026-08-04T16:16:29.8861266+00:00 | 159.8ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,844,000 | 2026-08-04T16:16:29.9648469+00:00 | 109.7ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 1,847,000 | 2026-08-04T16:16:29.9745705+00:00 | 100.4ms | GC pause | - | - | 4.0s / 498,178 msg/s | Gen2 +0 / pause +193.1ms |
| Confluent | 2,432,000 | 2026-08-04T16:16:31.2628449+00:00 | 113.1ms | GC pause | - | - | 5.0s / 491,966 msg/s | Gen2 +0 / pause +131.7ms |
| Confluent | 4,170,000 | 2026-08-04T16:16:34.2455707+00:00 | 100.4ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,172,000 | 2026-08-04T16:16:34.2471816+00:00 | 126.3ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,173,000 | 2026-08-04T16:16:34.2484356+00:00 | 115.0ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,182,000 | 2026-08-04T16:16:34.2587691+00:00 | 125.7ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,190,000 | 2026-08-04T16:16:34.270057+00:00 | 116.8ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,202,000 | 2026-08-04T16:16:34.2807761+00:00 | 123.5ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,203,000 | 2026-08-04T16:16:34.2814747+00:00 | 122.6ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,212,000 | 2026-08-04T16:16:34.29378+00:00 | 115.2ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,220,000 | 2026-08-04T16:16:34.3020643+00:00 | 133.2ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,223,000 | 2026-08-04T16:16:34.3040623+00:00 | 131.8ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,230,000 | 2026-08-04T16:16:34.3123068+00:00 | 127.0ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,252,000 | 2026-08-04T16:16:34.3438294+00:00 | 102.9ms | GC pause | - | - | 8.0s / 609,228 msg/s | Gen2 +0 / pause +103.9ms |
| Confluent | 4,303,000 | 2026-08-04T16:16:34.4578742+00:00 | 108.3ms | GC pause | - | - | 9.0s / 487,891 msg/s | Gen2 +0 / pause +238.2ms |
| Confluent | 397,495,000 | 2026-08-04T16:25:06.9884925+00:00 | 134.1ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,510,000 | 2026-08-04T16:25:07.0065422+00:00 | 145.6ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,515,000 | 2026-08-04T16:25:07.0115508+00:00 | 132.4ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,525,000 | 2026-08-04T16:25:07.0200191+00:00 | 126.4ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,529,000 | 2026-08-04T16:25:07.022615+00:00 | 128.3ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,530,000 | 2026-08-04T16:25:07.0231753+00:00 | 141.6ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,533,000 | 2026-08-04T16:25:07.0250667+00:00 | 139.9ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,542,000 | 2026-08-04T16:25:07.03061+00:00 | 146.4ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,546,000 | 2026-08-04T16:25:07.0329343+00:00 | 122.1ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,555,000 | 2026-08-04T16:25:07.0389645+00:00 | 127.5ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,556,000 | 2026-08-04T16:25:07.0394861+00:00 | 127.0ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,559,000 | 2026-08-04T16:25:07.0411978+00:00 | 125.7ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,560,000 | 2026-08-04T16:25:07.0418989+00:00 | 144.1ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,566,000 | 2026-08-04T16:25:07.0459649+00:00 | 129.7ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,570,000 | 2026-08-04T16:25:07.0483482+00:00 | 151.0ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,572,000 | 2026-08-04T16:25:07.0494932+00:00 | 156.7ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,573,000 | 2026-08-04T16:25:07.0499637+00:00 | 155.3ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,580,000 | 2026-08-04T16:25:07.0542835+00:00 | 151.4ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,582,000 | 2026-08-04T16:25:07.056386+00:00 | 170.3ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,589,000 | 2026-08-04T16:25:07.0621218+00:00 | 138.4ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,590,000 | 2026-08-04T16:25:07.0625909+00:00 | 155.2ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,595,000 | 2026-08-04T16:25:07.0657241+00:00 | 141.4ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,599,000 | 2026-08-04T16:25:07.0679436+00:00 | 139.7ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,610,000 | 2026-08-04T16:25:07.0751942+00:00 | 171.5ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,613,000 | 2026-08-04T16:25:07.0777267+00:00 | 169.1ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,630,000 | 2026-08-04T16:25:07.0891043+00:00 | 163.9ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,633,000 | 2026-08-04T16:25:07.0920943+00:00 | 161.5ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,635,000 | 2026-08-04T16:25:07.0946561+00:00 | 141.3ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,639,000 | 2026-08-04T16:25:07.0997244+00:00 | 136.5ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,646,000 | 2026-08-04T16:25:07.107107+00:00 | 130.5ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,649,000 | 2026-08-04T16:25:07.1102451+00:00 | 128.0ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,650,000 | 2026-08-04T16:25:07.1111244+00:00 | 151.3ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,655,000 | 2026-08-04T16:25:07.1165074+00:00 | 125.5ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,659,000 | 2026-08-04T16:25:07.1205627+00:00 | 122.6ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,660,000 | 2026-08-04T16:25:07.1216831+00:00 | 142.9ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,670,000 | 2026-08-04T16:25:07.1366098+00:00 | 136.7ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 397,672,000 | 2026-08-04T16:25:07.1466273+00:00 | 143.1ms | GC pause | - | - | 521.4s / 784,168 msg/s | Gen2 +0 / pause +98.1ms |
| Dekaf | 2,443,000 | 2026-08-04T16:31:29.2889133+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 959,140 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,449,000 | 2026-08-04T16:31:29.2990961+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 959,140 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,453,000 | 2026-08-04T16:31:29.3008623+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 959,140 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,479,000 | 2026-08-04T16:31:30.2689774+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,085,707 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,483,000 | 2026-08-04T16:31:30.2709487+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,085,707 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,488,000 | 2026-08-04T16:31:30.2819471+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,085,707 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,489,000 | 2026-08-04T16:31:30.2825042+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,085,707 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,659,000 | 2026-08-04T16:31:32.2855647+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,122,665 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 5,665,000 | 2026-08-04T16:31:32.2992155+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,122,665 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 5,668,000 | 2026-08-04T16:31:32.3013558+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,122,665 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 9,540,000 | 2026-08-04T16:31:35.7723548+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 995,771 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,550,000 | 2026-08-04T16:31:35.7786066+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 995,771 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,560,000 | 2026-08-04T16:31:35.7830668+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 995,771 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,564,000 | 2026-08-04T16:31:35.787574+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 995,771 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,570,000 | 2026-08-04T16:31:35.8037463+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 995,771 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,719,000 | 2026-08-04T16:31:43.2623578+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,057,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,548,000 | 2026-08-04T16:31:56.7483406+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,551,000 | 2026-08-04T16:31:56.7495014+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,552,000 | 2026-08-04T16:31:56.7501128+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,555,000 | 2026-08-04T16:31:56.7515166+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,558,000 | 2026-08-04T16:31:56.7564858+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,562,000 | 2026-08-04T16:31:56.7581228+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,565,000 | 2026-08-04T16:31:56.7630112+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 32,568,000 | 2026-08-04T16:31:56.7650629+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 1,095,480 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 39,600,000 | 2026-08-04T16:32:03.2447337+00:00 | 113.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,606,000 | 2026-08-04T16:32:03.2522175+00:00 | 108.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,610,000 | 2026-08-04T16:32:03.2567596+00:00 | 151.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,614,000 | 2026-08-04T16:32:03.2651179+00:00 | 137.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,616,000 | 2026-08-04T16:32:03.2657164+00:00 | 136.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,624,000 | 2026-08-04T16:32:03.2793216+00:00 | 142.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,630,000 | 2026-08-04T16:32:03.2893539+00:00 | 132.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,634,000 | 2026-08-04T16:32:03.3039184+00:00 | 120.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,636,000 | 2026-08-04T16:32:03.3105989+00:00 | 113.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,640,000 | 2026-08-04T16:32:03.3125269+00:00 | 115.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 39,644,000 | 2026-08-04T16:32:03.3165562+00:00 | 114.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 999,372 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 53,255,000 | 2026-08-04T16:32:14.7787341+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 49.1s / 1,014,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,258,000 | 2026-08-04T16:32:14.7832279+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 49.1s / 1,014,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,265,000 | 2026-08-04T16:32:14.7867825+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 49.1s / 1,014,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,268,000 | 2026-08-04T16:32:14.7906391+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 49.1s / 1,014,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 57,767,000 | 2026-08-04T16:32:18.8204786+00:00 | 110.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 53.1s / 1,003,184 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 68,122,000 | 2026-08-04T16:32:28.2813867+00:00 | 113.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 62.1s / 1,051,416 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 68,125,000 | 2026-08-04T16:32:28.2834426+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 62.1s / 1,051,416 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 68,131,000 | 2026-08-04T16:32:28.2921866+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 62.1s / 1,051,416 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 81,148,000 | 2026-08-04T16:32:40.7939341+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 75.1s / 949,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 85,119,000 | 2026-08-04T16:32:44.7518476+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 79.1s / 973,976 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 85,133,000 | 2026-08-04T16:32:44.7593588+00:00 | 112.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 79.1s / 973,976 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 85,139,000 | 2026-08-04T16:32:44.7631357+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 79.1s / 973,976 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 86,642,000 | 2026-08-04T16:32:46.2577812+00:00 | 105.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,648,000 | 2026-08-04T16:32:46.2653934+00:00 | 128.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,651,000 | 2026-08-04T16:32:46.2673109+00:00 | 108.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,652,000 | 2026-08-04T16:32:46.2707003+00:00 | 105.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,655,000 | 2026-08-04T16:32:46.2725535+00:00 | 120.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,658,000 | 2026-08-04T16:32:46.276452+00:00 | 117.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,662,000 | 2026-08-04T16:32:46.2789718+00:00 | 114.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,665,000 | 2026-08-04T16:32:46.2836301+00:00 | 109.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,668,000 | 2026-08-04T16:32:46.2854239+00:00 | 115.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,672,000 | 2026-08-04T16:32:46.2877101+00:00 | 112.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,675,000 | 2026-08-04T16:32:46.2922382+00:00 | 119.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 80.1s / 1,024,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 89,855,000 | 2026-08-04T16:32:49.2502438+00:00 | 102.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 83.1s / 1,081,407 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf | 89,861,000 | 2026-08-04T16:32:49.2551267+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 83.1s / 1,081,407 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf | 89,865,000 | 2026-08-04T16:32:49.2593305+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 83.1s / 1,081,407 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf | 89,868,000 | 2026-08-04T16:32:49.2605705+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 83.1s / 1,081,407 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf | 89,872,000 | 2026-08-04T16:32:49.2651212+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 83.1s / 1,081,407 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf | 90,913,000 | 2026-08-04T16:32:50.2868987+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 84.1s / 1,004,648 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 90,925,000 | 2026-08-04T16:32:50.2962878+00:00 | 107.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 84.1s / 1,004,648 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 90,928,000 | 2026-08-04T16:32:50.2980734+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 84.1s / 1,004,648 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,962,000 | 2026-08-04T16:32:51.2824905+00:00 | 112.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,968,000 | 2026-08-04T16:32:51.2854834+00:00 | 119.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,971,000 | 2026-08-04T16:32:51.2871905+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,972,000 | 2026-08-04T16:32:51.287833+00:00 | 118.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,975,000 | 2026-08-04T16:32:51.2894847+00:00 | 116.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,978,000 | 2026-08-04T16:32:51.3042081+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,985,000 | 2026-08-04T16:32:51.3101237+00:00 | 109.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,991,000 | 2026-08-04T16:32:51.3166181+00:00 | 106.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,992,000 | 2026-08-04T16:32:51.3172421+00:00 | 106.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,995,000 | 2026-08-04T16:32:51.3189382+00:00 | 111.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 85.1s / 1,106,185 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,160,000 | 2026-08-04T16:32:56.2594765+00:00 | 108.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 90.1s / 989,884 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,174,000 | 2026-08-04T16:32:56.2721472+00:00 | 103.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 90.1s / 989,884 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,176,000 | 2026-08-04T16:32:56.2805412+00:00 | 111.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 90.1s / 989,884 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,184,000 | 2026-08-04T16:32:56.2853735+00:00 | 108.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 90.1s / 989,884 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,186,000 | 2026-08-04T16:32:56.2865549+00:00 | 107.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 90.1s / 989,884 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 99,291,000 | 2026-08-04T16:32:58.2718209+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 92.1s / 1,017,278 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 117,952,000 | 2026-08-04T16:33:15.7457598+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 977,405 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 117,955,000 | 2026-08-04T16:33:15.7506396+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 977,405 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 117,958,000 | 2026-08-04T16:33:15.7593955+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 977,405 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 117,961,000 | 2026-08-04T16:33:15.7611555+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 977,405 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 117,981,000 | 2026-08-04T16:33:15.793207+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 977,405 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 118,929,000 | 2026-08-04T16:33:16.7841172+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 111.1s / 1,030,218 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 118,937,000 | 2026-08-04T16:33:16.7925001+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 111.1s / 1,030,218 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 118,943,000 | 2026-08-04T16:33:16.798679+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 111.1s / 1,030,218 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,447,000 | 2026-08-04T16:33:30.2694247+00:00 | 109.7ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,449,000 | 2026-08-04T16:33:30.2705543+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,457,000 | 2026-08-04T16:33:30.2745686+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,459,000 | 2026-08-04T16:33:30.275432+00:00 | 130.9ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,463,000 | 2026-08-04T16:33:30.2771953+00:00 | 143.4ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,469,000 | 2026-08-04T16:33:30.2799236+00:00 | 142.1ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,473,000 | 2026-08-04T16:33:30.2818976+00:00 | 140.1ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 133,477,000 | 2026-08-04T16:33:30.2836165+00:00 | 145.9ms | broker/backlog (no scale or GC event) | - | - | 124.1s / 1,033,543 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,405,000 | 2026-08-04T16:33:31.2693527+00:00 | 158.9ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,408,000 | 2026-08-04T16:33:31.2709914+00:00 | 153.4ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,412,000 | 2026-08-04T16:33:31.2728598+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,418,000 | 2026-08-04T16:33:31.2757812+00:00 | 148.6ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,421,000 | 2026-08-04T16:33:31.2771952+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,425,000 | 2026-08-04T16:33:31.2788569+00:00 | 145.5ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,432,000 | 2026-08-04T16:33:31.2819378+00:00 | 142.4ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,438,000 | 2026-08-04T16:33:31.2983401+00:00 | 137.9ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,441,000 | 2026-08-04T16:33:31.299562+00:00 | 136.7ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,445,000 | 2026-08-04T16:33:31.3018407+00:00 | 136.6ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,448,000 | 2026-08-04T16:33:31.3066095+00:00 | 146.4ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 947,958 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 137,581,000 | 2026-08-04T16:33:34.3392734+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 1,048,817 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 137,582,000 | 2026-08-04T16:33:34.3398433+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 1,048,817 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 140,546,000 | 2026-08-04T16:33:37.2812444+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 131.1s / 978,711 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 145,732,000 | 2026-08-04T16:33:42.2522625+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 136.2s / 990,655 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 145,751,000 | 2026-08-04T16:33:42.27608+00:00 | 121.4ms | broker/backlog (no scale or GC event) | - | - | 136.2s / 990,655 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 145,758,000 | 2026-08-04T16:33:42.2816427+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 136.2s / 990,655 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 145,762,000 | 2026-08-04T16:33:42.2859018+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 136.2s / 990,655 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 145,765,000 | 2026-08-04T16:33:42.2875462+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 136.2s / 990,655 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 145,768,000 | 2026-08-04T16:33:42.2891045+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 136.2s / 990,655 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 167,913,000 | 2026-08-04T16:34:01.7630907+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 155.2s / 1,062,972 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 168,933,000 | 2026-08-04T16:34:02.7781709+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 157.2s / 1,061,511 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 219,541,000 | 2026-08-04T16:34:49.2868533+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 203.2s / 1,099,448 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 272,865,000 | 2026-08-04T16:35:35.7776342+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 249.2s / 1,081,470 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf | 272,868,000 | 2026-08-04T16:35:35.78128+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 249.2s / 1,081,470 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf | 272,875,000 | 2026-08-04T16:35:35.7878606+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 249.2s / 1,081,470 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf | 272,878,000 | 2026-08-04T16:35:35.7888991+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 249.2s / 1,081,470 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf | 294,608,000 | 2026-08-04T16:35:53.8083022+00:00 | 105.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 267.2s / 988,890 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 294,625,000 | 2026-08-04T16:35:53.826041+00:00 | 105.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 267.2s / 988,890 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 294,628,000 | 2026-08-04T16:35:53.8317014+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 267.2s / 988,890 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 324,767,000 | 2026-08-04T16:36:20.2708409+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 294.3s / 998,676 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf | 324,783,000 | 2026-08-04T16:36:20.2888163+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 294.3s / 998,676 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf | 351,555,000 | 2026-08-04T16:36:44.2671922+00:00 | 120.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 318.3s / 758,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 351,568,000 | 2026-08-04T16:36:44.2766603+00:00 | 115.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 318.3s / 758,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 351,575,000 | 2026-08-04T16:36:44.2867535+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 318.3s / 758,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 351,578,000 | 2026-08-04T16:36:44.2880903+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 318.3s / 758,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 351,935,000 | 2026-08-04T16:36:44.7620503+00:00 | 108.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed | - | 318.3s / 758,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 423,651,000 | 2026-08-04T16:37:49.2489816+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 383.3s / 791,933 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 423,652,000 | 2026-08-04T16:37:49.2493609+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 383.3s / 791,933 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 423,655,000 | 2026-08-04T16:37:49.2511295+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 383.3s / 791,933 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 423,658,000 | 2026-08-04T16:37:49.2531291+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 383.3s / 791,933 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 423,661,000 | 2026-08-04T16:37:49.2550229+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 383.3s / 791,933 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 423,668,000 | 2026-08-04T16:37:49.258673+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 383.3s / 791,933 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 429,561,000 | 2026-08-04T16:37:55.4554875+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 389.3s / 763,576 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 430,533,000 | 2026-08-04T16:37:56.7454894+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 390.3s / 771,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 448,395,000 | 2026-08-04T16:38:15.7828239+00:00 | 102.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 409.4s / 777,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 448,398,000 | 2026-08-04T16:38:15.7867335+00:00 | 105.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 409.4s / 777,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 448,402,000 | 2026-08-04T16:38:15.7902438+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 409.4s / 777,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 448,425,000 | 2026-08-04T16:38:15.8068943+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 409.4s / 777,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 449,099,000 | 2026-08-04T16:38:16.760183+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 410.4s / 702,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 450,253,000 | 2026-08-04T16:38:18.2767629+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 412.4s / 827,976 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 450,258,000 | 2026-08-04T16:38:18.2848958+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 412.4s / 827,976 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 450,268,000 | 2026-08-04T16:38:18.296203+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 412.4s / 827,976 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 458,346,000 | 2026-08-04T16:38:26.7447111+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 420.4s / 876,653 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 469,613,000 | 2026-08-04T16:38:37.7809233+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 431.4s / 933,878 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 469,633,000 | 2026-08-04T16:38:37.792554+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 431.4s / 933,878 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 469,643,000 | 2026-08-04T16:38:37.7998443+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 431.4s / 933,878 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 470,368,000 | 2026-08-04T16:38:38.7838858+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 432.4s / 759,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 470,375,000 | 2026-08-04T16:38:38.790619+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 432.4s / 759,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 472,479,000 | 2026-08-04T16:38:41.2931176+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 435.4s / 987,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 472,483,000 | 2026-08-04T16:38:41.2951245+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 435.4s / 987,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,515,000 | 2026-08-04T16:38:49.2760178+00:00 | 111.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,518,000 | 2026-08-04T16:38:49.2788122+00:00 | 108.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,528,000 | 2026-08-04T16:38:49.3054937+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,531,000 | 2026-08-04T16:38:49.3095208+00:00 | 113.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,532,000 | 2026-08-04T16:38:49.3107662+00:00 | 112.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,535,000 | 2026-08-04T16:38:49.3121279+00:00 | 120.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 480,542,000 | 2026-08-04T16:38:49.3296618+00:00 | 105.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 443.4s / 770,545 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,562,000 | 2026-08-04T16:39:00.7225939+00:00 | 110.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,565,000 | 2026-08-04T16:39:00.7261602+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,568,000 | 2026-08-04T16:39:00.7285456+00:00 | 108.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,571,000 | 2026-08-04T16:39:00.7319866+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,572,000 | 2026-08-04T16:39:00.732304+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,575,000 | 2026-08-04T16:39:00.7347519+00:00 | 102.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 491,578,000 | 2026-08-04T16:39:00.7360787+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 454.4s / 837,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 642,595,000 | 2026-08-04T16:41:59.7647746+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,598,000 | 2026-08-04T16:41:59.765538+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,602,000 | 2026-08-04T16:41:59.7699766+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,605,000 | 2026-08-04T16:41:59.771267+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,612,000 | 2026-08-04T16:41:59.7803027+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,618,000 | 2026-08-04T16:41:59.7877515+00:00 | 130.7ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,621,000 | 2026-08-04T16:41:59.7899084+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,625,000 | 2026-08-04T16:41:59.7933011+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,628,000 | 2026-08-04T16:41:59.8127758+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 642,631,000 | 2026-08-04T16:41:59.8172976+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 633.6s / 785,401 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 651,858,000 | 2026-08-04T16:42:11.281268+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 645.6s / 905,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 669,801,000 | 2026-08-04T16:42:31.7705307+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 665.6s / 927,730 msg/s | Gen2 +0 / pause +4.9ms |
| Dekaf | 669,802,000 | 2026-08-04T16:42:31.7711961+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 665.6s / 927,730 msg/s | Gen2 +0 / pause +4.9ms |
| Dekaf | 669,805,000 | 2026-08-04T16:42:31.7727921+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 665.6s / 927,730 msg/s | Gen2 +0 / pause +4.9ms |
| Dekaf | 669,808,000 | 2026-08-04T16:42:31.7813375+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 665.6s / 927,730 msg/s | Gen2 +0 / pause +4.9ms |
| Dekaf | 669,811,000 | 2026-08-04T16:42:31.7923394+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 665.6s / 927,730 msg/s | Gen2 +0 / pause +4.9ms |
| Dekaf | 669,825,000 | 2026-08-04T16:42:31.8134493+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 665.6s / 927,730 msg/s | Gen2 +0 / pause +4.9ms |
| Dekaf | 694,878,000 | 2026-08-04T16:42:57.2687217+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 694,885,000 | 2026-08-04T16:42:57.2722298+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 694,891,000 | 2026-08-04T16:42:57.2750229+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 694,892,000 | 2026-08-04T16:42:57.2754564+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 694,895,000 | 2026-08-04T16:42:57.2767475+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 694,905,000 | 2026-08-04T16:42:57.2957708+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 694,921,000 | 2026-08-04T16:42:57.339234+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 691.6s / 1,055,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 701,216,000 | 2026-08-04T16:43:03.7979748+00:00 | 122.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 697.6s / 703,028 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 701,220,000 | 2026-08-04T16:43:03.814962+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 697.6s / 703,028 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 701,226,000 | 2026-08-04T16:43:03.8234786+00:00 | 110.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 697.6s / 703,028 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 701,230,000 | 2026-08-04T16:43:03.8283721+00:00 | 119.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 697.6s / 703,028 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 701,236,000 | 2026-08-04T16:43:03.8339174+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 697.6s / 703,028 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 702,438,000 | 2026-08-04T16:43:05.251525+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 699.6s / 927,946 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 702,448,000 | 2026-08-04T16:43:05.2602632+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 699.6s / 927,946 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 702,455,000 | 2026-08-04T16:43:05.2674242+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 699.6s / 927,946 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 702,461,000 | 2026-08-04T16:43:05.2706661+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 699.6s / 927,946 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 702,462,000 | 2026-08-04T16:43:05.2750161+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 699.6s / 927,946 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 705,942,000 | 2026-08-04T16:43:09.2432319+00:00 | 117.6ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,945,000 | 2026-08-04T16:43:09.247414+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,948,000 | 2026-08-04T16:43:09.2487328+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,951,000 | 2026-08-04T16:43:09.2524259+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,952,000 | 2026-08-04T16:43:09.252801+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,955,000 | 2026-08-04T16:43:09.2591504+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,958,000 | 2026-08-04T16:43:09.2634674+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,965,000 | 2026-08-04T16:43:09.2682981+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 705,968,000 | 2026-08-04T16:43:09.2761753+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 703.6s / 835,123 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,487,000 | 2026-08-04T16:43:30.2764996+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,858,000 | 2026-08-04T16:43:30.7686226+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,865,000 | 2026-08-04T16:43:30.7761317+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,868,000 | 2026-08-04T16:43:30.7781593+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,871,000 | 2026-08-04T16:43:30.7806963+00:00 | 115.3ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,872,000 | 2026-08-04T16:43:30.7812005+00:00 | 114.8ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,875,000 | 2026-08-04T16:43:30.7850385+00:00 | 123.2ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,878,000 | 2026-08-04T16:43:30.7876442+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,881,000 | 2026-08-04T16:43:30.791543+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,882,000 | 2026-08-04T16:43:30.7921473+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,885,000 | 2026-08-04T16:43:30.7948476+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,888,000 | 2026-08-04T16:43:30.7974278+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 724.6s / 750,330 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 727,640,000 | 2026-08-04T16:43:31.7868063+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 725.6s / 754,244 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 727,643,000 | 2026-08-04T16:43:31.788639+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 725.6s / 754,244 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 727,647,000 | 2026-08-04T16:43:31.8103824+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 725.6s / 754,244 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 786,908,000 | 2026-08-04T16:44:29.266492+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 782.7s / 927,202 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 786,918,000 | 2026-08-04T16:44:29.2704917+00:00 | 125.7ms | broker/backlog (no scale or GC event) | - | - | 782.7s / 927,202 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 786,928,000 | 2026-08-04T16:44:29.2760129+00:00 | 131.6ms | broker/backlog (no scale or GC event) | - | - | 783.7s / 977,758 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 786,932,000 | 2026-08-04T16:44:29.2779056+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 782.7s / 927,202 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 786,938,000 | 2026-08-04T16:44:29.291351+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 783.7s / 977,758 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 807,896,000 | 2026-08-04T16:44:49.3034753+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 802.7s / 854,591 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 807,906,000 | 2026-08-04T16:44:49.309678+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 803.7s / 1,078,202 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 807,910,000 | 2026-08-04T16:44:49.3143901+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 803.7s / 1,078,202 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 823,575,000 | 2026-08-04T16:45:05.2556395+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 818.7s / 738,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 823,581,000 | 2026-08-04T16:45:05.2585828+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 818.7s / 738,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 823,585,000 | 2026-08-04T16:45:05.261679+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 818.7s / 738,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 824,885,000 | 2026-08-04T16:45:06.7723612+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 820.7s / 899,503 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 824,888,000 | 2026-08-04T16:45:06.77381+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 820.7s / 899,503 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 824,892,000 | 2026-08-04T16:45:06.7761387+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 820.7s / 899,503 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 824,901,000 | 2026-08-04T16:45:06.7848179+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 820.7s / 899,503 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 824,905,000 | 2026-08-04T16:45:06.7869263+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 820.7s / 899,503 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 838,970,000 | 2026-08-04T16:45:20.7761782+00:00 | 105.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 834.7s / 808,424 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 838,974,000 | 2026-08-04T16:45:20.780635+00:00 | 109.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 834.7s / 808,424 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 838,976,000 | 2026-08-04T16:45:20.784613+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 834.7s / 808,424 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 860,779,000 | 2026-08-04T16:45:46.7848779+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 860.8s / 805,878 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 860,783,000 | 2026-08-04T16:45:46.7879806+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 860.8s / 805,878 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 881,925,000 | 2026-08-04T16:46:12.2848911+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 885.8s / 829,085 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 881,928,000 | 2026-08-04T16:46:12.2895883+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 885.8s / 829,085 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 881,932,000 | 2026-08-04T16:46:12.2946161+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 885.8s / 829,085 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 891,875,000 | 2026-08-04T16:46:22.8107929+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 896.8s / 765,548 msg/s | Gen2 +0 / pause +4.6ms |
| Dekaf (3conn) | 100,000 | 2026-08-04T16:46:40.5549293+00:00 | 127.1ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 104,000 | 2026-08-04T16:46:40.5609873+00:00 | 100.1ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 110,000 | 2026-08-04T16:46:40.5678635+00:00 | 118.9ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 114,000 | 2026-08-04T16:46:40.5730177+00:00 | 109.0ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 116,000 | 2026-08-04T16:46:40.5762965+00:00 | 105.7ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 120,000 | 2026-08-04T16:46:40.5809367+00:00 | 112.7ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 130,000 | 2026-08-04T16:46:40.5928612+00:00 | 127.6ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 140,000 | 2026-08-04T16:46:40.6258172+00:00 | 104.1ms | throughput collapse | - | - | 1.0s / 458,595 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 518,000 | 2026-08-04T16:46:41.3937797+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 525,000 | 2026-08-04T16:46:41.4015975+00:00 | 131.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 527,000 | 2026-08-04T16:46:41.408975+00:00 | 133.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 528,000 | 2026-08-04T16:46:41.4100629+00:00 | 122.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,000 | 2026-08-04T16:46:41.4161734+00:00 | 133.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 537,000 | 2026-08-04T16:46:41.4200317+00:00 | 164.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 538,000 | 2026-08-04T16:46:41.4235651+00:00 | 170.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 545,000 | 2026-08-04T16:46:41.4370003+00:00 | 159.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 547,000 | 2026-08-04T16:46:41.4414799+00:00 | 191.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 548,000 | 2026-08-04T16:46:41.4425605+00:00 | 154.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 555,000 | 2026-08-04T16:46:41.4566681+00:00 | 170.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 557,000 | 2026-08-04T16:46:41.4586435+00:00 | 181.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 558,000 | 2026-08-04T16:46:41.4595871+00:00 | 177.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 565,000 | 2026-08-04T16:46:41.4826166+00:00 | 160.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 567,000 | 2026-08-04T16:46:41.4844407+00:00 | 183.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 568,000 | 2026-08-04T16:46:41.4860359+00:00 | 157.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 575,000 | 2026-08-04T16:46:41.5198853+00:00 | 151.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 577,000 | 2026-08-04T16:46:41.5356656+00:00 | 159.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 578,000 | 2026-08-04T16:46:41.5363215+00:00 | 135.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 587,000 | 2026-08-04T16:46:41.5895213+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 520,631 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,804,000 | 2026-08-04T16:46:45.5381719+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,806,000 | 2026-08-04T16:46:45.5422039+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,814,000 | 2026-08-04T16:46:45.5472628+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,816,000 | 2026-08-04T16:46:45.5480596+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,824,000 | 2026-08-04T16:46:45.5580978+00:00 | 124.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,826,000 | 2026-08-04T16:46:45.5676174+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,834,000 | 2026-08-04T16:46:45.5869402+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,836,000 | 2026-08-04T16:46:45.5882003+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 960,072 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,245,000 | 2026-08-04T16:46:49.0089438+00:00 | 117.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 975,564 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,247,000 | 2026-08-04T16:46:49.0120427+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 975,564 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,248,000 | 2026-08-04T16:46:49.0248275+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 975,564 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,041,000 | 2026-08-04T16:46:49.92275+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,042,000 | 2026-08-04T16:46:49.9239725+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,051,000 | 2026-08-04T16:46:49.9309952+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,052,000 | 2026-08-04T16:46:49.932394+00:00 | 126.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,061,000 | 2026-08-04T16:46:49.9447916+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,062,000 | 2026-08-04T16:46:49.9451278+00:00 | 119.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,071,000 | 2026-08-04T16:46:49.9502513+00:00 | 115.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,072,000 | 2026-08-04T16:46:49.9508388+00:00 | 114.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,081,000 | 2026-08-04T16:46:49.9568842+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,082,000 | 2026-08-04T16:46:49.9575634+00:00 | 145.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,091,000 | 2026-08-04T16:46:49.9730847+00:00 | 191.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,092,000 | 2026-08-04T16:46:49.9744465+00:00 | 189.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,101,000 | 2026-08-04T16:46:49.9871626+00:00 | 188.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,102,000 | 2026-08-04T16:46:49.9877131+00:00 | 188.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,111,000 | 2026-08-04T16:46:50.0034365+00:00 | 172.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,112,000 | 2026-08-04T16:46:50.0041339+00:00 | 184.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,121,000 | 2026-08-04T16:46:50.0555253+00:00 | 139.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,122,000 | 2026-08-04T16:46:50.0562501+00:00 | 139.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,131,000 | 2026-08-04T16:46:50.0661577+00:00 | 150.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,132,000 | 2026-08-04T16:46:50.0670587+00:00 | 149.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,141,000 | 2026-08-04T16:46:50.0742273+00:00 | 172.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,142,000 | 2026-08-04T16:46:50.0751323+00:00 | 174.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,151,000 | 2026-08-04T16:46:50.1059235+00:00 | 144.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,152,000 | 2026-08-04T16:46:50.1062548+00:00 | 143.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,001,000 | 2026-08-04T16:46:53.2686525+00:00 | 121.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,002,000 | 2026-08-04T16:46:53.2696582+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,011,000 | 2026-08-04T16:46:53.2774518+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,012,000 | 2026-08-04T16:46:53.2779858+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,021,000 | 2026-08-04T16:46:53.2833389+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,022,000 | 2026-08-04T16:46:53.2840976+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,031,000 | 2026-08-04T16:46:53.2894354+00:00 | 130.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,032,000 | 2026-08-04T16:46:53.2897578+00:00 | 130.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,041,000 | 2026-08-04T16:46:53.3104882+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,042,000 | 2026-08-04T16:46:53.3108004+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,685,000 | 2026-08-04T16:46:54.0335851+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,688,000 | 2026-08-04T16:46:54.0367238+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,695,000 | 2026-08-04T16:46:54.0432499+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,698,000 | 2026-08-04T16:46:54.0448637+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,625,000 | 2026-08-04T16:46:58.2981268+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 839,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,628,000 | 2026-08-04T16:46:58.3007447+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 839,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,635,000 | 2026-08-04T16:46:58.3072596+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 839,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,269,000 | 2026-08-04T16:46:59.0652765+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 839,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,273,000 | 2026-08-04T16:46:59.0677295+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 839,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,279,000 | 2026-08-04T16:46:59.0888851+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 839,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,020,000 | 2026-08-04T16:47:03.5715176+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 24.1s / 789,850 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,030,000 | 2026-08-04T16:47:03.5773753+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 24.1s / 789,850 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,036,000 | 2026-08-04T16:47:03.5847559+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 24.1s / 789,850 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 26,899,000 | 2026-08-04T16:47:11.0300906+00:00 | 104.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 31.1s / 957,923 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,227,000 | 2026-08-04T16:47:12.5413354+00:00 | 104.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,237,000 | 2026-08-04T16:47:12.5481492+00:00 | 120.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,247,000 | 2026-08-04T16:47:12.553129+00:00 | 116.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,257,000 | 2026-08-04T16:47:12.5578045+00:00 | 119.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,267,000 | 2026-08-04T16:47:12.5631508+00:00 | 121.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,277,000 | 2026-08-04T16:47:12.5701734+00:00 | 121.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,287,000 | 2026-08-04T16:47:12.5778187+00:00 | 114.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,300,000 | 2026-08-04T16:47:12.6459778+00:00 | 102.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 33.1s / 826,315 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 49,515,000 | 2026-08-04T16:47:34.5361557+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 55.1s / 817,162 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 49,518,000 | 2026-08-04T16:47:34.5373874+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 55.1s / 817,162 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,397,000 | 2026-08-04T16:47:44.5349737+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,001,778 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,407,000 | 2026-08-04T16:47:44.5419597+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,001,778 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,417,000 | 2026-08-04T16:47:44.5504552+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,001,778 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,427,000 | 2026-08-04T16:47:44.5606826+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,001,778 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 117,048,000 | 2026-08-04T16:48:36.0523436+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 116.1s / 1,125,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 117,055,000 | 2026-08-04T16:48:36.0547594+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 116.1s / 1,125,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 117,058,000 | 2026-08-04T16:48:36.0563814+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 116.1s / 1,125,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 117,061,000 | 2026-08-04T16:48:36.0572515+00:00 | 111.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 116.1s / 1,125,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 117,062,000 | 2026-08-04T16:48:36.0574993+00:00 | 111.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 116.1s / 1,125,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 117,065,000 | 2026-08-04T16:48:36.0586841+00:00 | 102.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 116.1s / 1,125,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,926,000 | 2026-08-04T16:49:15.9402814+00:00 | 375.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,934,000 | 2026-08-04T16:49:15.947749+00:00 | 378.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,936,000 | 2026-08-04T16:49:15.9485646+00:00 | 377.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,940,000 | 2026-08-04T16:49:15.9524624+00:00 | 363.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,944,000 | 2026-08-04T16:49:15.9561245+00:00 | 379.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,946,000 | 2026-08-04T16:49:15.9571773+00:00 | 378.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,950,000 | 2026-08-04T16:49:15.9603474+00:00 | 386.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,954,000 | 2026-08-04T16:49:15.9620311+00:00 | 380.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,956,000 | 2026-08-04T16:49:15.9629047+00:00 | 379.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,960,000 | 2026-08-04T16:49:15.9644884+00:00 | 382.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,964,000 | 2026-08-04T16:49:15.9662068+00:00 | 393.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,966,000 | 2026-08-04T16:49:15.9669694+00:00 | 392.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,970,000 | 2026-08-04T16:49:15.9685159+00:00 | 378.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,974,000 | 2026-08-04T16:49:15.970428+00:00 | 391.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,976,000 | 2026-08-04T16:49:15.9709074+00:00 | 390.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,980,000 | 2026-08-04T16:49:15.9727135+00:00 | 374.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 156.2s / 722,681 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,948,000 | 2026-08-04T16:49:21.5763492+00:00 | 304.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,955,000 | 2026-08-04T16:49:21.5808749+00:00 | 310.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,958,000 | 2026-08-04T16:49:21.5881463+00:00 | 302.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,965,000 | 2026-08-04T16:49:21.5919296+00:00 | 298.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,967,000 | 2026-08-04T16:49:21.5932373+00:00 | 298.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,968,000 | 2026-08-04T16:49:21.5942487+00:00 | 300.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,975,000 | 2026-08-04T16:49:21.6073709+00:00 | 290.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,977,000 | 2026-08-04T16:49:21.6096326+00:00 | 285.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,978,000 | 2026-08-04T16:49:21.6099971+00:00 | 287.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,985,000 | 2026-08-04T16:49:21.6159829+00:00 | 281.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,987,000 | 2026-08-04T16:49:21.6169613+00:00 | 280.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,988,000 | 2026-08-04T16:49:21.617326+00:00 | 286.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,995,000 | 2026-08-04T16:49:21.6199564+00:00 | 284.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 168,997,000 | 2026-08-04T16:49:21.6208429+00:00 | 276.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 162.2s / 874,322 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 269,605,000 | 2026-08-04T16:50:54.2407791+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 254.3s / 1,000,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 269,608,000 | 2026-08-04T16:50:54.2473406+00:00 | 101.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 254.3s / 1,000,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,600,000 | 2026-08-04T16:51:50.0561477+00:00 | 150.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,606,000 | 2026-08-04T16:51:50.0589279+00:00 | 153.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,610,000 | 2026-08-04T16:51:50.0664043+00:00 | 146.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,614,000 | 2026-08-04T16:51:50.0680369+00:00 | 144.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,616,000 | 2026-08-04T16:51:50.0723966+00:00 | 143.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,620,000 | 2026-08-04T16:51:50.0738536+00:00 | 138.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,624,000 | 2026-08-04T16:51:50.0776274+00:00 | 141.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,626,000 | 2026-08-04T16:51:50.0782573+00:00 | 140.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,630,000 | 2026-08-04T16:51:50.0812273+00:00 | 136.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,634,000 | 2026-08-04T16:51:50.0834931+00:00 | 137.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 310.4s / 982,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,011,000 | 2026-08-04T16:51:55.1775622+00:00 | 228.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,012,000 | 2026-08-04T16:51:55.1783518+00:00 | 227.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,021,000 | 2026-08-04T16:51:55.1875141+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,022,000 | 2026-08-04T16:51:55.1878122+00:00 | 217.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,023,000 | 2026-08-04T16:51:55.1884261+00:00 | 220.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,029,000 | 2026-08-04T16:51:55.1917023+00:00 | 217.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,031,000 | 2026-08-04T16:51:55.1923721+00:00 | 216.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,032,000 | 2026-08-04T16:51:55.1937349+00:00 | 215.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 335,033,000 | 2026-08-04T16:51:55.1941741+00:00 | 217.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 315.4s / 859,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 378,824,000 | 2026-08-04T16:52:36.070823+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 356.4s / 932,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 378,826,000 | 2026-08-04T16:52:36.0713756+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 356.4s / 932,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 378,834,000 | 2026-08-04T16:52:36.0885446+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 356.4s / 932,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 403,688,000 | 2026-08-04T16:52:58.5230225+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 378.4s / 969,122 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 407,827,000 | 2026-08-04T16:53:02.5420706+00:00 | 104.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 382.4s / 1,034,469 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 407,837,000 | 2026-08-04T16:53:02.5463478+00:00 | 110.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 382.4s / 1,034,469 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 549,587,000 | 2026-08-04T16:55:12.0507397+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 512.5s / 687,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 633,164,000 | 2026-08-04T16:56:48.5669451+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 608.6s / 672,726 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 633,166,000 | 2026-08-04T16:56:48.5675447+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 608.6s / 672,726 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 643,477,000 | 2026-08-04T16:57:01.5445563+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 621.6s / 705,604 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 643,487,000 | 2026-08-04T16:57:01.552744+00:00 | 110.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 621.6s / 705,604 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 657,524,000 | 2026-08-04T16:57:19.0503914+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 639.7s / 828,575 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 747,924,000 | 2026-08-04T16:59:09.5397043+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 749.8s / 752,999 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 747,926,000 | 2026-08-04T16:59:09.5424832+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 749.8s / 752,999 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 747,934,000 | 2026-08-04T16:59:09.5553813+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 749.8s / 752,999 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 796,766,000 | 2026-08-04T17:00:08.018676+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 807.9s / 764,165 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 796,786,000 | 2026-08-04T17:00:08.0408266+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 807.9s / 764,165 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 796,794,000 | 2026-08-04T17:00:08.0488769+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 807.9s / 764,165 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 797,474,000 | 2026-08-04T17:00:09.0340781+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 808.9s / 711,051 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 797,476,000 | 2026-08-04T17:00:09.0356955+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 808.9s / 711,051 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*606 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.58x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.37x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,288,197 | 1,205,458–1,376,614 | 1.10 | 1.28x |
| Confluent | 2 | 1,004,250 | 935,831–1,077,671 | 1.67 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 1.05 | 1078.06 | 1,369,706 | 1,376,614 | +1.6% | +0.10% | 1306.25 | 1,369,706 | 0 | 1.44 |
| Dekaf (dekaf-first) | 1.16 | 1184.41 | 1,203,548 | 1,205,458 | -13.4% | -1.36% | 1147.79 | 1,203,548 | 0 | 1.39 |
| Confluent (confluent-first) | 1.56 | - | 1,078,469 | 1,077,671 | +7.8% | +0.63% | 1028.51 | 1,078,469 | 0 | 1.68 |
| Confluent (dekaf-first) | 1.77 | - | 934,529 | 935,831 | +1.7% | +0.05% | 891.24 | 934,529 | 0 | 1.66 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,057,637 | 1175.13 | 1018.09 KB |
| Dekaf | 1 | 1,200,694 | 1334.08 | 1020.60 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:20.9469199+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 448,628 msg/s |
| Dekaf | 2026-08-04T16:31:38.9483253+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1510.5 MB/s | 0/0 | 19,174 | 18.0s / 1,370,985 msg/s |
| Dekaf | 2026-08-04T16:31:56.9546605+00:00 | 1 | 16.0 MiB / 14.4 MiB | 1569.9 MB/s | 0/0 | 45,501 | 36.0s / 1,465,322 msg/s |
| Dekaf | 2026-08-04T16:32:15.9627186+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1569.9 MB/s | 1/0 | 76,575 | 55.0s / 1,466,649 msg/s |
| Dekaf | 2026-08-04T16:32:33.9658777+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1569.9 MB/s | 1/0 | 109,378 | 73.0s / 1,137,931 msg/s |
| Dekaf | 2026-08-04T16:32:51.9730357+00:00 | 1 | 14.0 MiB / 12.4 MiB | 1569.9 MB/s | 1/1 | 140,343 | 91.0s / 1,193,058 msg/s |
| Dekaf | 2026-08-04T16:33:09.9819526+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1569.9 MB/s | 1/1 | 172,817 | 109.0s / 1,290,844 msg/s |
| Dekaf | 2026-08-04T16:33:27.9870687+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1605.8 MB/s | 1/1 | 206,073 | 127.0s / 1,467,576 msg/s |
| Dekaf | 2026-08-04T16:33:45.9939454+00:00 | 1 | 14.0 MiB / 12.0 MiB | 1605.8 MB/s | 1/1 | 241,582 | 145.0s / 1,413,757 msg/s |
| Dekaf | 2026-08-04T16:34:05.0008772+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1605.8 MB/s | 1/1 | 275,537 | 164.1s / 1,358,312 msg/s |
| Dekaf | 2026-08-04T16:34:23.012167+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1605.8 MB/s | 2/1 | 304,557 | 182.1s / 1,397,808 msg/s |
| Dekaf | 2026-08-04T16:34:41.0195968+00:00 | 1 | 15.0 MiB / 12.6 MiB | 1605.8 MB/s | 2/1 | 333,222 | 200.1s / 1,393,487 msg/s |
| Dekaf | 2026-08-04T16:34:59.0244271+00:00 | 1 | 16.0 MiB / 14.4 MiB | 1605.8 MB/s | 3/1 | 356,185 | 218.1s / 1,396,969 msg/s |
| Dekaf | 2026-08-04T16:35:17.0294424+00:00 | 1 | 16.0 MiB / 15.2 MiB | 1605.8 MB/s | 3/1 | 379,056 | 236.1s / 1,344,601 msg/s |
| Dekaf | 2026-08-04T16:35:35.0362319+00:00 | 1 | 18.0 MiB / 16.8 MiB | 1605.8 MB/s | 3/1 | 400,684 | 254.1s / 1,468,426 msg/s |
| Dekaf | 2026-08-04T16:35:54.0495142+00:00 | 1 | 18.0 MiB / 17.7 MiB | 1605.8 MB/s | 4/1 | 420,042 | 273.1s / 1,326,108 msg/s |
| Dekaf | 2026-08-04T16:36:12.0553173+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1605.8 MB/s | 4/1 | 441,389 | 291.1s / 1,477,369 msg/s |
| Dekaf | 2026-08-04T16:36:30.0601777+00:00 | 1 | 15.0 MiB / 13.4 MiB | 1609.5 MB/s | 5/1 | 469,241 | 309.1s / 1,484,359 msg/s |
| Dekaf | 2026-08-04T16:36:48.0702552+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1609.5 MB/s | 5/1 | 497,452 | 327.1s / 1,457,658 msg/s |
| Dekaf | 2026-08-04T16:37:06.0779905+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 5/1 | 529,927 | 345.1s / 1,470,269 msg/s |
| Dekaf | 2026-08-04T16:37:24.0851983+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 6/1 | 566,023 | 363.1s / 1,441,910 msg/s |
| Dekaf | 2026-08-04T16:37:43.0897993+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1609.5 MB/s | 6/1 | 605,972 | 382.1s / 1,320,037 msg/s |
| Dekaf | 2026-08-04T16:38:01.0944687+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 6/2 | 641,400 | 400.1s / 1,304,966 msg/s |
| Dekaf | 2026-08-04T16:38:19.0976275+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 6/2 | 676,559 | 418.1s / 1,375,754 msg/s |
| Dekaf | 2026-08-04T16:38:37.1016913+00:00 | 1 | 13.0 MiB / 10.5 MiB | 1609.5 MB/s | 6/2 | 711,442 | 436.1s / 1,354,805 msg/s |
| Dekaf | 2026-08-04T16:38:55.103388+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1609.5 MB/s | 6/2 | 745,879 | 454.1s / 1,309,710 msg/s |
| Dekaf | 2026-08-04T16:39:13.1075664+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1609.5 MB/s | 6/3 | 781,032 | 472.2s / 1,348,666 msg/s |
| Dekaf | 2026-08-04T16:39:32.1128298+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1609.5 MB/s | 6/3 | 818,680 | 491.2s / 1,388,378 msg/s |
| Dekaf | 2026-08-04T16:39:50.1193701+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1609.5 MB/s | 6/3 | 851,330 | 509.2s / 1,393,202 msg/s |
| Dekaf | 2026-08-04T16:40:08.1247097+00:00 | 1 | 13.0 MiB / 12.0 MiB | 1609.5 MB/s | 6/3 | 885,051 | 527.2s / 1,337,715 msg/s |
| Dekaf | 2026-08-04T16:40:26.1319762+00:00 | 1 | 13.0 MiB / 11.3 MiB | 1609.5 MB/s | 6/3 | 918,764 | 545.2s / 1,352,014 msg/s |
| Dekaf | 2026-08-04T16:40:44.1393106+00:00 | 1 | 13.0 MiB / 11.6 MiB | 1609.5 MB/s | 6/3 | 953,223 | 563.2s / 1,414,500 msg/s |
| Dekaf | 2026-08-04T16:41:02.1456926+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 6/3 | 986,155 | 581.2s / 1,376,922 msg/s |
| Dekaf | 2026-08-04T16:41:21.153313+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1609.5 MB/s | 6/3 | 1,026,891 | 600.2s / 1,385,445 msg/s |
| Dekaf | 2026-08-04T16:41:39.1607026+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1609.5 MB/s | 6/4 | 1,063,895 | 618.2s / 1,393,060 msg/s |
| Dekaf | 2026-08-04T16:41:57.1679347+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 6/4 | 1,099,511 | 636.2s / 1,366,694 msg/s |
| Dekaf | 2026-08-04T16:42:15.1757753+00:00 | 1 | 13.0 MiB / 12.5 MiB | 1609.5 MB/s | 6/4 | 1,134,880 | 654.2s / 1,378,744 msg/s |
| Dekaf | 2026-08-04T16:42:33.1838716+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1609.5 MB/s | 6/4 | 1,171,097 | 672.2s / 1,364,319 msg/s |
| Dekaf | 2026-08-04T16:42:52.1905126+00:00 | 1 | 13.0 MiB / 11.2 MiB | 1609.5 MB/s | 6/4 | 1,208,206 | 691.2s / 1,371,559 msg/s |
| Dekaf | 2026-08-04T16:43:10.1978298+00:00 | 1 | 13.0 MiB / 11.4 MiB | 1609.5 MB/s | 6/4 | 1,244,135 | 709.2s / 1,433,684 msg/s |
| Dekaf | 2026-08-04T16:43:28.2069685+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1609.5 MB/s | 6/4 | 1,276,853 | 727.2s / 1,336,838 msg/s |
| Dekaf | 2026-08-04T16:43:46.2119805+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1609.5 MB/s | 6/4 | 1,311,347 | 745.2s / 1,386,755 msg/s |
| Dekaf | 2026-08-04T16:44:04.2193176+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1609.5 MB/s | 6/4 | 1,348,404 | 763.2s / 1,395,127 msg/s |
| Dekaf | 2026-08-04T16:44:22.2311564+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1609.5 MB/s | 6/4 | 1,392,213 | 781.2s / 1,445,828 msg/s |
| Dekaf | 2026-08-04T16:44:41.2399981+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1612.9 MB/s | 6/4 | 1,442,027 | 800.2s / 1,419,632 msg/s |
| Dekaf | 2026-08-04T16:44:59.2469096+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1612.9 MB/s | 6/4 | 1,489,948 | 818.2s / 1,457,479 msg/s |
| Dekaf | 2026-08-04T16:45:17.254545+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1612.9 MB/s | 6/4 | 1,536,722 | 836.2s / 1,420,955 msg/s |
| Dekaf | 2026-08-04T16:45:35.2633136+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1612.9 MB/s | 6/4 | 1,581,402 | 854.3s / 1,413,280 msg/s |
| Dekaf | 2026-08-04T16:45:53.2795265+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1612.9 MB/s | 7/4 | 1,621,694 | 872.3s / 1,358,774 msg/s |
| Dekaf | 2026-08-04T16:46:11.2896515+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1612.9 MB/s | 7/4 | 1,661,692 | 890.3s / 1,343,390 msg/s |
| Dekaf | 2026-08-04T16:46:30.8471907+00:00 | 1 | 16.0 MiB / 4.6 MiB | 1496.0 MB/s | 0/0 | 8,433 | 9.0s / 1,173,900 msg/s |
| Dekaf | 2026-08-04T16:46:48.8497698+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1496.0 MB/s | 0/0 | 30,019 | 27.0s / 1,281,809 msg/s |
| Dekaf | 2026-08-04T16:47:06.8565477+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1496.0 MB/s | 0/0 | 54,439 | 45.0s / 1,276,689 msg/s |
| Dekaf | 2026-08-04T16:47:24.8629703+00:00 | 1 | 14.0 MiB / 12.7 MiB | 1496.0 MB/s | 1/0 | 83,190 | 63.0s / 1,217,905 msg/s |
| Dekaf | 2026-08-04T16:47:42.8684212+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1496.0 MB/s | 1/0 | 115,140 | 81.0s / 1,271,903 msg/s |
| Dekaf | 2026-08-04T16:48:00.874176+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1496.0 MB/s | 2/0 | 150,324 | 99.0s / 1,228,172 msg/s |
| Dekaf | 2026-08-04T16:48:19.8806323+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1496.0 MB/s | 2/0 | 189,106 | 118.0s / 1,303,270 msg/s |
| Dekaf | 2026-08-04T16:48:37.8846379+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1513.9 MB/s | 2/1 | 218,760 | 136.0s / 1,404,019 msg/s |
| Dekaf | 2026-08-04T16:48:55.8902836+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1513.9 MB/s | 2/1 | 255,922 | 154.1s / 1,380,109 msg/s |
| Dekaf | 2026-08-04T16:49:13.8947941+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1513.9 MB/s | 2/1 | 293,325 | 172.1s / 1,364,590 msg/s |
| Dekaf | 2026-08-04T16:49:31.8963456+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1513.9 MB/s | 2/1 | 327,800 | 190.1s / 1,299,615 msg/s |
| Dekaf | 2026-08-04T16:49:49.8990369+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1513.9 MB/s | 2/1 | 357,566 | 208.1s / 1,152,534 msg/s |
| Dekaf | 2026-08-04T16:50:08.9024951+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1513.9 MB/s | 2/2 | 389,157 | 227.1s / 1,123,849 msg/s |
| Dekaf | 2026-08-04T16:50:26.9096451+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1513.9 MB/s | 2/2 | 422,326 | 245.1s / 1,262,489 msg/s |
| Dekaf | 2026-08-04T16:50:44.9145595+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1518.0 MB/s | 2/2 | 455,884 | 263.1s / 1,295,199 msg/s |
| Dekaf | 2026-08-04T16:51:02.9203955+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1518.0 MB/s | 2/2 | 492,518 | 281.1s / 1,268,933 msg/s |
| Dekaf | 2026-08-04T16:51:20.9302659+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1519.4 MB/s | 2/2 | 527,375 | 299.1s / 1,228,505 msg/s |
| Dekaf | 2026-08-04T16:51:39.9383372+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1534.8 MB/s | 2/2 | 567,222 | 318.1s / 1,426,881 msg/s |
| Dekaf | 2026-08-04T16:51:57.9438262+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1534.8 MB/s | 2/2 | 599,283 | 336.1s / 1,259,380 msg/s |
| Dekaf | 2026-08-04T16:52:15.9470716+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1534.8 MB/s | 2/3 | 629,411 | 354.1s / 1,379,739 msg/s |
| Dekaf | 2026-08-04T16:52:33.9540389+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1534.8 MB/s | 2/3 | 667,826 | 372.1s / 1,393,554 msg/s |
| Dekaf | 2026-08-04T16:52:51.9605838+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1534.8 MB/s | 2/3 | 706,713 | 390.1s / 1,333,336 msg/s |
| Dekaf | 2026-08-04T16:53:09.9646225+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1554.9 MB/s | 2/3 | 746,197 | 408.1s / 1,454,443 msg/s |
| Dekaf | 2026-08-04T16:53:28.9699047+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1568.8 MB/s | 2/3 | 788,797 | 427.2s / 1,333,984 msg/s |
| Dekaf | 2026-08-04T16:53:46.9727365+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1568.8 MB/s | 2/3 | 825,793 | 445.2s / 1,338,808 msg/s |
| Dekaf | 2026-08-04T16:54:04.9783934+00:00 | 1 | 12.0 MiB / 9.3 MiB | 1568.8 MB/s | 2/3 | 858,361 | 463.2s / 1,125,346 msg/s |
| Dekaf | 2026-08-04T16:54:22.9875281+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1568.8 MB/s | 2/3 | 895,974 | 481.2s / 1,295,335 msg/s |
| Dekaf | 2026-08-04T16:54:40.9936438+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1568.8 MB/s | 2/3 | 937,877 | 499.2s / 1,230,023 msg/s |
| Dekaf | 2026-08-04T16:54:59.0062895+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1568.8 MB/s | 2/3 | 976,097 | 517.2s / 1,007,154 msg/s |
| Dekaf | 2026-08-04T16:55:18.0160283+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1568.8 MB/s | 2/3 | 1,017,369 | 536.2s / 1,229,235 msg/s |
| Dekaf | 2026-08-04T16:55:36.0206049+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1568.8 MB/s | 2/3 | 1,055,370 | 554.2s / 1,095,962 msg/s |
| Dekaf | 2026-08-04T16:55:54.027774+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1568.8 MB/s | 2/3 | 1,093,167 | 572.2s / 1,169,259 msg/s |
| Dekaf | 2026-08-04T16:56:12.03285+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1568.8 MB/s | 2/3 | 1,132,843 | 590.2s / 1,193,021 msg/s |
| Dekaf | 2026-08-04T16:56:30.0384412+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1568.8 MB/s | 3/3 | 1,172,264 | 608.2s / 1,285,077 msg/s |
| Dekaf | 2026-08-04T16:56:48.0447113+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1568.8 MB/s | 3/3 | 1,210,525 | 626.2s / 1,271,185 msg/s |
| Dekaf | 2026-08-04T16:57:07.050865+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1568.8 MB/s | 3/3 | 1,251,183 | 645.2s / 1,145,563 msg/s |
| Dekaf | 2026-08-04T16:57:25.0574702+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1568.8 MB/s | 4/3 | 1,286,928 | 663.2s / 1,214,199 msg/s |
| Dekaf | 2026-08-04T16:57:43.064043+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1568.8 MB/s | 4/3 | 1,322,338 | 681.2s / 1,101,969 msg/s |
| Dekaf | 2026-08-04T16:58:01.0737999+00:00 | 1 | 15.0 MiB / 14.4 MiB | 1568.8 MB/s | 5/3 | 1,351,475 | 699.2s / 1,154,373 msg/s |
| Dekaf | 2026-08-04T16:58:19.083527+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1568.8 MB/s | 5/3 | 1,379,383 | 717.2s / 1,029,805 msg/s |
| Dekaf | 2026-08-04T16:58:37.0898412+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1568.8 MB/s | 5/3 | 1,404,172 | 735.3s / 1,103,353 msg/s |
| Dekaf | 2026-08-04T16:58:56.0979763+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1568.8 MB/s | 6/3 | 1,429,952 | 754.3s / 1,156,214 msg/s |
| Dekaf | 2026-08-04T16:59:14.1078575+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1568.8 MB/s | 6/3 | 1,455,189 | 772.3s / 1,160,471 msg/s |
| Dekaf | 2026-08-04T16:59:32.1146364+00:00 | 1 | 16.0 MiB / 13.9 MiB | 1568.8 MB/s | 6/4 | 1,480,700 | 790.3s / 944,456 msg/s |
| Dekaf | 2026-08-04T16:59:50.1224139+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1568.8 MB/s | 6/4 | 1,502,993 | 808.3s / 1,196,581 msg/s |
| Dekaf | 2026-08-04T17:00:08.1383268+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1568.8 MB/s | 6/4 | 1,523,504 | 826.3s / 1,090,910 msg/s |
| Dekaf | 2026-08-04T17:00:26.1511306+00:00 | 1 | 14.0 MiB / 11.3 MiB | 1568.8 MB/s | 6/4 | 1,543,684 | 844.3s / 927,846 msg/s |
| Dekaf | 2026-08-04T17:00:45.1555132+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1568.8 MB/s | 6/5 | 1,566,214 | 863.3s / 1,062,940 msg/s |
| Dekaf | 2026-08-04T17:01:03.1667998+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1568.8 MB/s | 6/5 | 1,586,137 | 881.3s / 1,043,063 msg/s |
| Dekaf | 2026-08-04T17:01:21.1769582+00:00 | 1 | 14.0 MiB / 12.1 MiB | 1568.8 MB/s | 6/5 | 1,608,943 | 899.3s / 995,695 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-04T16:31:51.0595594+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.4 MiB |
| Dekaf | 2026-08-04T16:32:06.0798525+00:00 | 1 | capacity | succeeded | 15,020ms | 14.0 MiB / 13.9 MiB |
| Dekaf | 2026-08-04T16:32:36.1075205+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:32:51.1232332+00:00 | 1 | capacity | failed | 15,015ms | 14.0 MiB / 11.7 MiB |
| Dekaf | 2026-08-04T16:33:51.1665876+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 11.8 MiB |
| Dekaf | 2026-08-04T16:34:06.1821963+00:00 | 1 | capacity | succeeded | 15,015ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:34:36.2106145+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:34:51.2283247+00:00 | 1 | capacity | succeeded | 15,017ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:35:21.2613221+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:35:36.2875027+00:00 | 1 | capacity | succeeded | 15,026ms | 18.0 MiB / 16.1 MiB |
| Dekaf | 2026-08-04T16:36:06.3359045+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 14.2 MiB |
| Dekaf | 2026-08-04T16:36:21.358704+00:00 | 1 | capacity | succeeded | 15,022ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:36:51.424366+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:37:06.4355433+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:37:36.4589395+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:37:51.4739752+00:00 | 1 | capacity | failed | 15,015ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-04T16:38:51.5363248+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:39:06.5464416+00:00 | 1 | capacity | failed | 15,010ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:41:06.6532262+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:41:21.6658494+00:00 | 1 | capacity | failed | 15,012ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-04T16:45:21.8983159+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf | 2026-08-04T16:45:36.920204+00:00 | 1 | capacity | succeeded | 15,022ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:46:06.9508407+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.9 MiB |
| Dekaf | 2026-08-04T16:46:51.9595566+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.7 MiB |
| Dekaf | 2026-08-04T16:47:06.9794133+00:00 | 1 | capacity | succeeded | 15,020ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-08-04T16:47:37.0038655+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:47:52.0171833+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:48:22.0457604+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:48:37.0595122+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-04T16:49:37.1159953+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:49:52.1300727+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 11.3 MiB |
| Dekaf | 2026-08-04T16:51:52.2397519+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-04T16:52:07.2500086+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.6 MiB |
| Dekaf | 2026-08-04T16:56:07.4860998+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:56:22.5059341+00:00 | 1 | capacity | succeeded | 15,019ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:56:52.542254+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf | 2026-08-04T16:57:07.5576942+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:57:37.5908117+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:57:52.6073566+00:00 | 1 | capacity | succeeded | 15,016ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:58:22.6543766+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 8.6 MiB |
| Dekaf | 2026-08-04T16:58:37.6785449+00:00 | 1 | capacity | succeeded | 15,024ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-04T16:59:07.7195726+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-04T16:59:22.7343645+00:00 | 1 | capacity | failed | 15,014ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T17:00:22.8260286+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.6 MiB |
| Dekaf | 2026-08-04T17:00:37.849632+00:00 | 1 | capacity | failed | 15,023ms | 16.0 MiB / 12.7 MiB |
| Dekaf | 2026-08-04T17:01:07.9281469+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 16.0 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,112 |
| Dekaf | 1 | 0.002–0.004ms | 1,165 |
| Dekaf | 1 | 0.004–0.008ms | 3,075 |
| Dekaf | 1 | 0.008–0.016ms | 18,811 |
| Dekaf | 1 | 0.016–0.032ms | 39,037 |
| Dekaf | 1 | 0.032–0.064ms | 36,299 |
| Dekaf | 1 | 0.064–0.128ms | 54,320 |
| Dekaf | 1 | 0.128–0.256ms | 157,397 |
| Dekaf | 1 | 0.256–0.512ms | 232,873 |
| Dekaf | 1 | 0.512–1.024ms | 96,602 |
| Dekaf | 1 | 1.024–2.048ms | 28,889 |
| Dekaf | 1 | 2.048–4.096ms | 5,737 |
| Dekaf | 1 | 4.096–8.192ms | 1,654 |
| Dekaf | 1 | 8.192–16.384ms | 307 |
| Dekaf | 1 | 16.384–32.768ms | 12 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 65.536–131.072ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,353 |
| Dekaf | 1 | 0.002–0.004ms | 1,569 |
| Dekaf | 1 | 0.004–0.008ms | 4,516 |
| Dekaf | 1 | 0.008–0.016ms | 41,394 |
| Dekaf | 1 | 0.016–0.032ms | 46,611 |
| Dekaf | 1 | 0.032–0.064ms | 38,473 |
| Dekaf | 1 | 0.064–0.128ms | 72,181 |
| Dekaf | 1 | 0.128–0.256ms | 192,192 |
| Dekaf | 1 | 0.256–0.512ms | 222,567 |
| Dekaf | 1 | 0.512–1.024ms | 75,353 |
| Dekaf | 1 | 1.024–2.048ms | 18,796 |
| Dekaf | 1 | 2.048–4.096ms | 4,661 |
| Dekaf | 1 | 4.096–8.192ms | 1,519 |
| Dekaf | 1 | 8.192–16.384ms | 222 |
| Dekaf | 1 | 16.384–32.768ms | 5 |

## Delivery Latency Outliers - Producer (Acks All)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 705,871,000 | 2026-08-04T16:27:21.3806197+00:00 | 101.9ms | GC pause | - | - | 661.5s / 852,814 msg/s | Gen2 +0 / pause +99.8ms |
| Confluent | 14,103,000 | 2026-08-04T17:01:40.3964232+00:00 | 104.9ms | GC pause | - | - | 19.0s / 859,257 msg/s | Gen2 +0 / pause +117.5ms |
| Confluent | 14,110,000 | 2026-08-04T17:01:40.4023671+00:00 | 100.9ms | GC pause | - | - | 19.0s / 859,257 msg/s | Gen2 +0 / pause +117.5ms |
| Confluent | 645,574,000 | 2026-08-04T17:12:44.3206482+00:00 | 103.8ms | GC pause | - | - | 682.6s / 920,007 msg/s | Gen2 +0 / pause +138.3ms |
| Confluent | 696,360,000 | 2026-08-04T17:13:41.4475724+00:00 | 105.5ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,364,000 | 2026-08-04T17:13:41.4514526+00:00 | 103.2ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,366,000 | 2026-08-04T17:13:41.4534651+00:00 | 100.0ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,370,000 | 2026-08-04T17:13:41.4568067+00:00 | 108.7ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,374,000 | 2026-08-04T17:13:41.4595597+00:00 | 106.4ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,375,000 | 2026-08-04T17:13:41.4602292+00:00 | 105.8ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,376,000 | 2026-08-04T17:13:41.4614008+00:00 | 104.7ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,377,000 | 2026-08-04T17:13:41.4619389+00:00 | 104.5ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,378,000 | 2026-08-04T17:13:41.4629161+00:00 | 103.6ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,379,000 | 2026-08-04T17:13:41.4639456+00:00 | 102.3ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,380,000 | 2026-08-04T17:13:41.4651493+00:00 | 107.6ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,383,000 | 2026-08-04T17:13:41.4687102+00:00 | 104.2ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 696,388,000 | 2026-08-04T17:13:41.4732391+00:00 | 100.4ms | GC pause | - | - | 739.6s / 832,223 msg/s | Gen2 +0 / pause +143.3ms |
| Confluent | 786,078,000 | 2026-08-04T17:15:19.79954+00:00 | 109.0ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,081,000 | 2026-08-04T17:15:19.8035161+00:00 | 105.4ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,087,000 | 2026-08-04T17:15:19.8110026+00:00 | 113.7ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,091,000 | 2026-08-04T17:15:19.8156013+00:00 | 123.9ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,094,000 | 2026-08-04T17:15:19.8188639+00:00 | 103.3ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,097,000 | 2026-08-04T17:15:19.8216459+00:00 | 124.1ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,098,000 | 2026-08-04T17:15:19.8223627+00:00 | 123.5ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,099,000 | 2026-08-04T17:15:19.8233973+00:00 | 100.7ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,101,000 | 2026-08-04T17:15:19.8247154+00:00 | 121.2ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,103,000 | 2026-08-04T17:15:19.8260245+00:00 | 114.2ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,105,000 | 2026-08-04T17:15:19.8316533+00:00 | 102.3ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,106,000 | 2026-08-04T17:15:19.8326319+00:00 | 101.4ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,107,000 | 2026-08-04T17:15:19.833765+00:00 | 122.5ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,109,000 | 2026-08-04T17:15:19.8362453+00:00 | 109.3ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,111,000 | 2026-08-04T17:15:19.8382371+00:00 | 118.3ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,112,000 | 2026-08-04T17:15:19.8398475+00:00 | 106.5ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,113,000 | 2026-08-04T17:15:19.8409895+00:00 | 105.6ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,114,000 | 2026-08-04T17:15:19.8441168+00:00 | 102.6ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,117,000 | 2026-08-04T17:15:19.8485322+00:00 | 109.5ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,118,000 | 2026-08-04T17:15:19.8497099+00:00 | 108.4ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 786,121,000 | 2026-08-04T17:15:19.857861+00:00 | 107.7ms | GC pause | - | - | 838.7s / 867,819 msg/s | Gen2 +0 / pause +107.9ms |
| Confluent | 800,730,000 | 2026-08-04T17:15:35.8368092+00:00 | 104.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,731,000 | 2026-08-04T17:15:35.8377168+00:00 | 111.3ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,733,000 | 2026-08-04T17:15:35.8396148+00:00 | 116.1ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,735,000 | 2026-08-04T17:15:35.8417839+00:00 | 106.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,737,000 | 2026-08-04T17:15:35.8430848+00:00 | 119.2ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,738,000 | 2026-08-04T17:15:35.8437546+00:00 | 118.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,739,000 | 2026-08-04T17:15:35.8443701+00:00 | 115.5ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,740,000 | 2026-08-04T17:15:35.8466266+00:00 | 120.3ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,743,000 | 2026-08-04T17:15:35.8483599+00:00 | 118.6ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,745,000 | 2026-08-04T17:15:35.8499863+00:00 | 117.4ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,746,000 | 2026-08-04T17:15:35.8505745+00:00 | 116.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,747,000 | 2026-08-04T17:15:35.8512318+00:00 | 118.9ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,750,000 | 2026-08-04T17:15:35.8536448+00:00 | 130.0ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,751,000 | 2026-08-04T17:15:35.8576942+00:00 | 112.6ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,753,000 | 2026-08-04T17:15:35.8606619+00:00 | 123.2ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,755,000 | 2026-08-04T17:15:35.8629765+00:00 | 107.1ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,756,000 | 2026-08-04T17:15:35.8641483+00:00 | 120.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,757,000 | 2026-08-04T17:15:35.8648706+00:00 | 120.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,758,000 | 2026-08-04T17:15:35.8656764+00:00 | 120.0ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,759,000 | 2026-08-04T17:15:35.866439+00:00 | 118.6ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,760,000 | 2026-08-04T17:15:35.868364+00:00 | 133.0ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,763,000 | 2026-08-04T17:15:35.8707394+00:00 | 136.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,766,000 | 2026-08-04T17:15:35.8750701+00:00 | 127.9ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,768,000 | 2026-08-04T17:15:35.8774649+00:00 | 132.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,771,000 | 2026-08-04T17:15:35.882157+00:00 | 141.4ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,776,000 | 2026-08-04T17:15:35.8907056+00:00 | 129.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,778,000 | 2026-08-04T17:15:35.8954005+00:00 | 141.2ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,779,000 | 2026-08-04T17:15:35.8972816+00:00 | 123.5ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,780,000 | 2026-08-04T17:15:35.8986135+00:00 | 142.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,783,000 | 2026-08-04T17:15:35.9045807+00:00 | 137.4ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,785,000 | 2026-08-04T17:15:35.9076749+00:00 | 126.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,786,000 | 2026-08-04T17:15:35.9095587+00:00 | 139.1ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,787,000 | 2026-08-04T17:15:35.9107733+00:00 | 139.4ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,788,000 | 2026-08-04T17:15:35.9121055+00:00 | 154.1ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,790,000 | 2026-08-04T17:15:35.916919+00:00 | 143.6ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,793,000 | 2026-08-04T17:15:35.9231812+00:00 | 150.0ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,795,000 | 2026-08-04T17:15:35.9389396+00:00 | 124.4ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,796,000 | 2026-08-04T17:15:35.9402918+00:00 | 123.2ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,797,000 | 2026-08-04T17:15:35.9444286+00:00 | 141.7ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,799,000 | 2026-08-04T17:15:35.9507854+00:00 | 130.2ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,801,000 | 2026-08-04T17:15:35.9554547+00:00 | 141.0ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,803,000 | 2026-08-04T17:15:35.9602801+00:00 | 136.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,805,000 | 2026-08-04T17:15:35.9642693+00:00 | 128.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,807,000 | 2026-08-04T17:15:35.9720907+00:00 | 127.1ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,808,000 | 2026-08-04T17:15:35.9733311+00:00 | 125.9ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,809,000 | 2026-08-04T17:15:35.9743169+00:00 | 124.2ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,810,000 | 2026-08-04T17:15:35.9752667+00:00 | 126.4ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,815,000 | 2026-08-04T17:15:35.9965984+00:00 | 106.8ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 800,816,000 | 2026-08-04T17:15:35.9973327+00:00 | 106.1ms | GC pause | - | - | 854.7s / 774,282 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 821,627,000 | 2026-08-04T17:15:59.4755399+00:00 | 103.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,628,000 | 2026-08-04T17:15:59.4762622+00:00 | 102.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,637,000 | 2026-08-04T17:15:59.4827248+00:00 | 105.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,638,000 | 2026-08-04T17:15:59.4833938+00:00 | 105.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,641,000 | 2026-08-04T17:15:59.4854516+00:00 | 110.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,647,000 | 2026-08-04T17:15:59.4896247+00:00 | 117.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,648,000 | 2026-08-04T17:15:59.4916633+00:00 | 115.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,667,000 | 2026-08-04T17:15:59.5140355+00:00 | 113.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,668,000 | 2026-08-04T17:15:59.5155441+00:00 | 116.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,671,000 | 2026-08-04T17:15:59.519182+00:00 | 112.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,677,000 | 2026-08-04T17:15:59.5264895+00:00 | 112.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,678,000 | 2026-08-04T17:15:59.5278094+00:00 | 111.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,681,000 | 2026-08-04T17:15:59.5325706+00:00 | 112.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,688,000 | 2026-08-04T17:15:59.5418384+00:00 | 112.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,697,000 | 2026-08-04T17:15:59.552906+00:00 | 113.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,701,000 | 2026-08-04T17:15:59.5642859+00:00 | 102.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,708,000 | 2026-08-04T17:15:59.5691973+00:00 | 110.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,711,000 | 2026-08-04T17:15:59.5709733+00:00 | 109.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,717,000 | 2026-08-04T17:15:59.5750523+00:00 | 111.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,723,000 | 2026-08-04T17:15:59.5787788+00:00 | 100.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,727,000 | 2026-08-04T17:15:59.5813134+00:00 | 116.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,728,000 | 2026-08-04T17:15:59.5818777+00:00 | 116.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,730,000 | 2026-08-04T17:15:59.583447+00:00 | 102.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,731,000 | 2026-08-04T17:15:59.5840075+00:00 | 123.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,733,000 | 2026-08-04T17:15:59.5855149+00:00 | 102.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,738,000 | 2026-08-04T17:15:59.5887646+00:00 | 122.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,740,000 | 2026-08-04T17:15:59.5899203+00:00 | 103.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,743,000 | 2026-08-04T17:15:59.5918885+00:00 | 101.5ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,746,000 | 2026-08-04T17:15:59.5938903+00:00 | 104.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,747,000 | 2026-08-04T17:15:59.5945335+00:00 | 122.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,749,000 | 2026-08-04T17:15:59.5957122+00:00 | 102.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,750,000 | 2026-08-04T17:15:59.5965571+00:00 | 112.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,753,000 | 2026-08-04T17:15:59.5985293+00:00 | 110.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,756,000 | 2026-08-04T17:15:59.600847+00:00 | 107.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,760,000 | 2026-08-04T17:15:59.6042737+00:00 | 109.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,762,000 | 2026-08-04T17:15:59.6074166+00:00 | 104.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,763,000 | 2026-08-04T17:15:59.6084288+00:00 | 115.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,764,000 | 2026-08-04T17:15:59.6090212+00:00 | 104.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,766,000 | 2026-08-04T17:15:59.6108666+00:00 | 104.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,767,000 | 2026-08-04T17:15:59.6115185+00:00 | 142.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,769,000 | 2026-08-04T17:15:59.6127881+00:00 | 102.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,771,000 | 2026-08-04T17:15:59.6161272+00:00 | 139.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,772,000 | 2026-08-04T17:15:59.6168913+00:00 | 112.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,773,000 | 2026-08-04T17:15:59.6174433+00:00 | 112.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,774,000 | 2026-08-04T17:15:59.6181632+00:00 | 105.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,775,000 | 2026-08-04T17:15:59.6204282+00:00 | 107.5ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,776,000 | 2026-08-04T17:15:59.6211902+00:00 | 111.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,778,000 | 2026-08-04T17:15:59.6228517+00:00 | 143.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,779,000 | 2026-08-04T17:15:59.6241304+00:00 | 108.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,780,000 | 2026-08-04T17:15:59.6258209+00:00 | 127.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,781,000 | 2026-08-04T17:15:59.6292878+00:00 | 137.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,783,000 | 2026-08-04T17:15:59.633923+00:00 | 119.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,784,000 | 2026-08-04T17:15:59.6355588+00:00 | 102.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,785,000 | 2026-08-04T17:15:59.636888+00:00 | 103.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,787,000 | 2026-08-04T17:15:59.6414888+00:00 | 132.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,788,000 | 2026-08-04T17:15:59.6431838+00:00 | 130.5ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,789,000 | 2026-08-04T17:15:59.6442757+00:00 | 109.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,791,000 | 2026-08-04T17:15:59.6470941+00:00 | 135.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,792,000 | 2026-08-04T17:15:59.6488211+00:00 | 105.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,793,000 | 2026-08-04T17:15:59.6501963+00:00 | 109.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,794,000 | 2026-08-04T17:15:59.6517523+00:00 | 103.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,795,000 | 2026-08-04T17:15:59.652816+00:00 | 102.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,796,000 | 2026-08-04T17:15:59.6544614+00:00 | 101.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,797,000 | 2026-08-04T17:15:59.6559074+00:00 | 135.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,798,000 | 2026-08-04T17:15:59.6567005+00:00 | 135.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,801,000 | 2026-08-04T17:15:59.6601841+00:00 | 136.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,802,000 | 2026-08-04T17:15:59.6608916+00:00 | 106.5ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,803,000 | 2026-08-04T17:15:59.6616126+00:00 | 116.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,805,000 | 2026-08-04T17:15:59.664562+00:00 | 108.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,806,000 | 2026-08-04T17:15:59.6652203+00:00 | 107.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,807,000 | 2026-08-04T17:15:59.6657953+00:00 | 136.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,808,000 | 2026-08-04T17:15:59.6678402+00:00 | 135.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,809,000 | 2026-08-04T17:15:59.6684591+00:00 | 104.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,810,000 | 2026-08-04T17:15:59.6690502+00:00 | 118.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,812,000 | 2026-08-04T17:15:59.6711353+00:00 | 106.3ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,813,000 | 2026-08-04T17:15:59.6716808+00:00 | 115.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,814,000 | 2026-08-04T17:15:59.6722749+00:00 | 107.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,816,000 | 2026-08-04T17:15:59.6738229+00:00 | 108.5ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,818,000 | 2026-08-04T17:15:59.6750312+00:00 | 139.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,820,000 | 2026-08-04T17:15:59.676199+00:00 | 120.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,822,000 | 2026-08-04T17:15:59.6777088+00:00 | 118.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,825,000 | 2026-08-04T17:15:59.6826237+00:00 | 114.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,827,000 | 2026-08-04T17:15:59.6837252+00:00 | 138.2ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,828,000 | 2026-08-04T17:15:59.684336+00:00 | 137.7ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,829,000 | 2026-08-04T17:15:59.6874225+00:00 | 113.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,831,000 | 2026-08-04T17:15:59.6893533+00:00 | 137.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,832,000 | 2026-08-04T17:15:59.6909099+00:00 | 116.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,833,000 | 2026-08-04T17:15:59.6916582+00:00 | 119.9ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,834,000 | 2026-08-04T17:15:59.6922422+00:00 | 109.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,835,000 | 2026-08-04T17:15:59.6934489+00:00 | 115.0ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,836,000 | 2026-08-04T17:15:59.6943866+00:00 | 114.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,837,000 | 2026-08-04T17:15:59.6950063+00:00 | 139.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,838,000 | 2026-08-04T17:15:59.6956664+00:00 | 138.7ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,841,000 | 2026-08-04T17:15:59.6990932+00:00 | 140.6ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,842,000 | 2026-08-04T17:15:59.7001452+00:00 | 111.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,843,000 | 2026-08-04T17:15:59.7005259+00:00 | 120.5ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,845,000 | 2026-08-04T17:15:59.7023902+00:00 | 111.6ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,846,000 | 2026-08-04T17:15:59.7031358+00:00 | 110.8ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,848,000 | 2026-08-04T17:15:59.7045876+00:00 | 145.4ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,849,000 | 2026-08-04T17:15:59.7053222+00:00 | 116.1ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,850,000 | 2026-08-04T17:15:59.7064052+00:00 | 126.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,851,000 | 2026-08-04T17:15:59.7099466+00:00 | 140.2ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,852,000 | 2026-08-04T17:15:59.7106792+00:00 | 113.4ms | GC pause | - | - | 877.7s / 851,885 msg/s | Gen2 +0 / pause +91.5ms |
| Confluent | 821,855,000 | 2026-08-04T17:15:59.7136248+00:00 | 113.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,857,000 | 2026-08-04T17:15:59.7168695+00:00 | 138.8ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,860,000 | 2026-08-04T17:15:59.7183829+00:00 | 117.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,861,000 | 2026-08-04T17:15:59.72293+00:00 | 139.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,862,000 | 2026-08-04T17:15:59.7235033+00:00 | 111.2ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,865,000 | 2026-08-04T17:15:59.7277782+00:00 | 110.1ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,866,000 | 2026-08-04T17:15:59.7283628+00:00 | 109.5ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,867,000 | 2026-08-04T17:15:59.7288854+00:00 | 138.8ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,868,000 | 2026-08-04T17:15:59.7294607+00:00 | 138.2ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,869,000 | 2026-08-04T17:15:59.733282+00:00 | 104.7ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,870,000 | 2026-08-04T17:15:59.7339574+00:00 | 116.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,871,000 | 2026-08-04T17:15:59.7346717+00:00 | 139.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,872,000 | 2026-08-04T17:15:59.7352763+00:00 | 105.5ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,874,000 | 2026-08-04T17:15:59.7380495+00:00 | 111.5ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,878,000 | 2026-08-04T17:15:59.7500501+00:00 | 137.8ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,879,000 | 2026-08-04T17:15:59.7515275+00:00 | 100.5ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,880,000 | 2026-08-04T17:15:59.7569567+00:00 | 106.6ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,887,000 | 2026-08-04T17:15:59.7707498+00:00 | 126.0ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,888,000 | 2026-08-04T17:15:59.7728789+00:00 | 123.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,891,000 | 2026-08-04T17:15:59.7772713+00:00 | 119.7ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,901,000 | 2026-08-04T17:15:59.7988975+00:00 | 109.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,907,000 | 2026-08-04T17:15:59.8100222+00:00 | 107.4ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +230.2ms |
| Confluent | 821,921,000 | 2026-08-04T17:15:59.8327035+00:00 | 105.1ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,957,000 | 2026-08-04T17:15:59.8810362+00:00 | 100.2ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,961,000 | 2026-08-04T17:15:59.8847712+00:00 | 102.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,968,000 | 2026-08-04T17:15:59.8948995+00:00 | 103.5ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,977,000 | 2026-08-04T17:15:59.9056326+00:00 | 103.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,978,000 | 2026-08-04T17:15:59.906353+00:00 | 104.7ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,981,000 | 2026-08-04T17:15:59.9104988+00:00 | 100.7ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,997,000 | 2026-08-04T17:15:59.9253748+00:00 | 103.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 821,998,000 | 2026-08-04T17:15:59.9289207+00:00 | 100.6ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,001,000 | 2026-08-04T17:15:59.9319319+00:00 | 100.2ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,008,000 | 2026-08-04T17:15:59.9389087+00:00 | 102.5ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,011,000 | 2026-08-04T17:15:59.941526+00:00 | 100.0ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,018,000 | 2026-08-04T17:15:59.9479253+00:00 | 100.6ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,027,000 | 2026-08-04T17:15:59.9557934+00:00 | 108.1ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,031,000 | 2026-08-04T17:15:59.9596836+00:00 | 116.0ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,037,000 | 2026-08-04T17:15:59.9644676+00:00 | 112.8ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,038,000 | 2026-08-04T17:15:59.9653931+00:00 | 111.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,041,000 | 2026-08-04T17:15:59.971189+00:00 | 106.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,047,000 | 2026-08-04T17:15:59.9775995+00:00 | 101.7ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,048,000 | 2026-08-04T17:15:59.9785117+00:00 | 110.2ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,051,000 | 2026-08-04T17:15:59.9846967+00:00 | 104.1ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,057,000 | 2026-08-04T17:15:59.992883+00:00 | 106.9ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,058,000 | 2026-08-04T17:15:59.9952284+00:00 | 104.6ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,061,000 | 2026-08-04T17:15:59.9977744+00:00 | 106.8ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,067,000 | 2026-08-04T17:16:00.0040685+00:00 | 102.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,068,000 | 2026-08-04T17:16:00.0051302+00:00 | 101.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,071,000 | 2026-08-04T17:16:00.0077155+00:00 | 105.4ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,078,000 | 2026-08-04T17:16:00.0195755+00:00 | 103.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,088,000 | 2026-08-04T17:16:00.0266234+00:00 | 109.4ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,091,000 | 2026-08-04T17:16:00.0344624+00:00 | 102.0ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,117,000 | 2026-08-04T17:16:00.0598639+00:00 | 106.0ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,118,000 | 2026-08-04T17:16:00.0606541+00:00 | 105.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 822,121,000 | 2026-08-04T17:16:00.0647627+00:00 | 101.3ms | GC pause | - | - | 878.7s / 817,858 msg/s | Gen2 +0 / pause +138.8ms |
| Confluent | 826,631,000 | 2026-08-04T17:16:05.021653+00:00 | 104.2ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,638,000 | 2026-08-04T17:16:05.0255431+00:00 | 126.8ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,641,000 | 2026-08-04T17:16:05.0272736+00:00 | 136.6ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,648,000 | 2026-08-04T17:16:05.0309527+00:00 | 134.7ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,651,000 | 2026-08-04T17:16:05.0326893+00:00 | 133.1ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,657,000 | 2026-08-04T17:16:05.0363366+00:00 | 138.2ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,661,000 | 2026-08-04T17:16:05.0390793+00:00 | 137.1ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,667,000 | 2026-08-04T17:16:05.0507834+00:00 | 128.8ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,668,000 | 2026-08-04T17:16:05.0523761+00:00 | 127.3ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,671,000 | 2026-08-04T17:16:05.0562873+00:00 | 126.7ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 826,672,000 | 2026-08-04T17:16:05.0590198+00:00 | 105.6ms | GC pause | - | - | 883.7s / 857,532 msg/s | Gen2 +0 / pause +112.2ms |
| Confluent | 833,714,000 | 2026-08-04T17:16:13.3109039+00:00 | 100.3ms | GC pause | - | - | 891.7s / 849,116 msg/s | Gen2 +0 / pause +106.6ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*121 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.51x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.28x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.42 | 1426.43 | 900,047 | 893,359 | +3.8% | +0.38% | 858.35 | 900,047 | 0 | 1.27 |
| Confluent | 2.31 | - | 677,693 | 683,207 | +2.9% | +0.30% | 646.30 | 677,693 | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 273,270 | 303.63 | 1013.08 KB |
| Dekaf | 2 | 266,098 | 295.66 | 999.22 KB |
| Dekaf | 3 | 264,721 | 294.13 | 991.64 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:29.578688+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 552,588 msg/s |
| Dekaf | 2026-08-04T16:31:38.5809276+00:00 | 3 | 16.0 MiB / 3.1 MiB | 308.1 MB/s | 0/0 | 16 | 9.0s / 855,211 msg/s |
| Dekaf | 2026-08-04T16:31:47.5826338+00:00 | 3 | 16.0 MiB / 5.6 MiB | 308.1 MB/s | 0/0 | 16 | 18.0s / 863,868 msg/s |
| Dekaf | 2026-08-04T16:31:57.5878534+00:00 | 1 | 16.0 MiB / 10.9 MiB | 354.9 MB/s | 0/0 | 8,699 | 28.0s / 912,936 msg/s |
| Dekaf | 2026-08-04T16:32:06.5966203+00:00 | 1 | 16.0 MiB / 11.6 MiB | 354.9 MB/s | 0/0 | 9,365 | 37.0s / 915,890 msg/s |
| Dekaf | 2026-08-04T16:32:15.5981798+00:00 | 1 | 14.0 MiB / 3.6 MiB | 354.9 MB/s | 1/0 | 11,912 | 46.0s / 887,689 msg/s |
| Dekaf | 2026-08-04T16:32:24.6051433+00:00 | 1 | 14.0 MiB / 13.6 MiB | 354.9 MB/s | 1/0 | 13,951 | 55.0s / 877,649 msg/s |
| Dekaf | 2026-08-04T16:32:33.6079532+00:00 | 2 | 12.0 MiB / 3.7 MiB | 356.6 MB/s | 2/0 | 4,914 | 64.1s / 1,039,359 msg/s |
| Dekaf | 2026-08-04T16:32:42.6157211+00:00 | 2 | 12.0 MiB / 1.8 MiB | 359.8 MB/s | 2/0 | 5,453 | 73.1s / 967,982 msg/s |
| Dekaf | 2026-08-04T16:32:51.6176991+00:00 | 2 | 10.0 MiB / 4.0 MiB | 361.7 MB/s | 3/0 | 6,244 | 82.1s / 949,314 msg/s |
| Dekaf | 2026-08-04T16:33:00.6237266+00:00 | 2 | 10.0 MiB / 5.4 MiB | 361.7 MB/s | 3/0 | 7,736 | 91.1s / 886,222 msg/s |
| Dekaf | 2026-08-04T16:33:09.6271587+00:00 | 3 | 12.0 MiB / 7.6 MiB | 367.6 MB/s | 2/1 | 3,148 | 100.1s / 893,359 msg/s |
| Dekaf | 2026-08-04T16:33:18.6390573+00:00 | 3 | 12.0 MiB / 5.0 MiB | 367.6 MB/s | 2/1 | 3,234 | 109.1s / 848,714 msg/s |
| Dekaf | 2026-08-04T16:33:27.6492836+00:00 | 3 | 12.0 MiB / 5.6 MiB | 367.6 MB/s | 2/2 | 3,477 | 118.1s / 955,703 msg/s |
| Dekaf | 2026-08-04T16:33:36.6544178+00:00 | 3 | 12.0 MiB / 5.9 MiB | 367.6 MB/s | 2/2 | 3,702 | 127.1s / 955,047 msg/s |
| Dekaf | 2026-08-04T16:33:46.6596121+00:00 | 1 | 12.0 MiB / 11.4 MiB | 393.1 MB/s | 2/2 | 66,447 | 137.1s / 963,852 msg/s |
| Dekaf | 2026-08-04T16:33:55.6642667+00:00 | 1 | 12.0 MiB / 11.6 MiB | 393.1 MB/s | 2/2 | 72,846 | 146.1s / 987,912 msg/s |
| Dekaf | 2026-08-04T16:34:04.6717104+00:00 | 1 | 12.0 MiB / 12.0 MiB | 393.1 MB/s | 2/2 | 78,876 | 155.1s / 938,392 msg/s |
| Dekaf | 2026-08-04T16:34:13.6819146+00:00 | 1 | 12.0 MiB / 8.6 MiB | 393.1 MB/s | 2/2 | 83,746 | 164.1s / 908,501 msg/s |
| Dekaf | 2026-08-04T16:34:22.6868503+00:00 | 2 | 8.0 MiB / 6.9 MiB | 361.7 MB/s | 3/1 | 16,705 | 173.1s / 797,452 msg/s |
| Dekaf | 2026-08-04T16:34:31.6886601+00:00 | 2 | 8.0 MiB / 3.7 MiB | 361.7 MB/s | 4/1 | 18,191 | 182.1s / 801,763 msg/s |
| Dekaf | 2026-08-04T16:34:40.694388+00:00 | 2 | 7.0 MiB / 2.9 MiB | 361.7 MB/s | 4/1 | 19,352 | 191.1s / 771,210 msg/s |
| Dekaf | 2026-08-04T16:34:49.7000827+00:00 | 2 | 8.0 MiB / 4.8 MiB | 361.7 MB/s | 4/2 | 20,159 | 200.1s / 850,462 msg/s |
| Dekaf | 2026-08-04T16:34:58.7064579+00:00 | 3 | 8.0 MiB / 7.2 MiB | 367.6 MB/s | 4/3 | 11,869 | 209.1s / 854,252 msg/s |
| Dekaf | 2026-08-04T16:35:07.7096298+00:00 | 3 | 8.0 MiB / 5.9 MiB | 367.6 MB/s | 4/3 | 13,088 | 218.2s / 880,612 msg/s |
| Dekaf | 2026-08-04T16:35:16.7126132+00:00 | 3 | 9.0 MiB / 3.1 MiB | 367.6 MB/s | 4/3 | 13,887 | 227.2s / 930,094 msg/s |
| Dekaf | 2026-08-04T16:35:25.722448+00:00 | 3 | 8.0 MiB / 2.8 MiB | 367.6 MB/s | 4/4 | 15,396 | 236.2s / 939,694 msg/s |
| Dekaf | 2026-08-04T16:35:35.7266675+00:00 | 1 | 10.0 MiB / 8.1 MiB | 393.1 MB/s | 3/4 | 118,197 | 246.2s / 961,540 msg/s |
| Dekaf | 2026-08-04T16:35:44.7349223+00:00 | 1 | 10.0 MiB / 9.7 MiB | 393.1 MB/s | 3/4 | 121,696 | 255.2s / 797,016 msg/s |
| Dekaf | 2026-08-04T16:35:53.7378782+00:00 | 1 | 10.0 MiB / 9.1 MiB | 393.1 MB/s | 3/4 | 125,697 | 264.2s / 841,781 msg/s |
| Dekaf | 2026-08-04T16:36:02.7471941+00:00 | 1 | 10.0 MiB / 5.4 MiB | 393.1 MB/s | 3/4 | 130,369 | 273.2s / 793,238 msg/s |
| Dekaf | 2026-08-04T16:36:11.7527533+00:00 | 2 | 8.0 MiB / 3.6 MiB | 377.3 MB/s | 4/3 | 35,952 | 282.2s / 835,821 msg/s |
| Dekaf | 2026-08-04T16:36:20.7563773+00:00 | 2 | 8.0 MiB / 5.8 MiB | 377.3 MB/s | 4/3 | 37,715 | 291.2s / 825,840 msg/s |
| Dekaf | 2026-08-04T16:36:29.7571965+00:00 | 2 | 7.0 MiB / 6.2 MiB | 377.3 MB/s | 4/3 | 39,154 | 300.2s / 888,912 msg/s |
| Dekaf | 2026-08-04T16:36:38.7661199+00:00 | 2 | 8.0 MiB / 2.7 MiB | 377.3 MB/s | 4/3 | 40,075 | 309.2s / 873,190 msg/s |
| Dekaf | 2026-08-04T16:36:47.7697187+00:00 | 2 | 8.0 MiB / 2.9 MiB | 377.3 MB/s | 5/3 | 41,327 | 318.2s / 837,944 msg/s |
| Dekaf | 2026-08-04T16:36:56.7739441+00:00 | 3 | 8.0 MiB / 3.5 MiB | 367.6 MB/s | 4/6 | 24,446 | 327.2s / 866,901 msg/s |
| Dekaf | 2026-08-04T16:37:05.7759568+00:00 | 3 | 9.0 MiB / 4.9 MiB | 367.6 MB/s | 4/6 | 24,712 | 336.2s / 869,624 msg/s |
| Dekaf | 2026-08-04T16:37:14.7856046+00:00 | 3 | 8.0 MiB / 5.7 MiB | 367.6 MB/s | 4/6 | 25,102 | 345.2s / 844,355 msg/s |
| Dekaf | 2026-08-04T16:37:23.7897109+00:00 | 3 | 9.0 MiB / 2.7 MiB | 367.6 MB/s | 5/6 | 25,273 | 354.2s / 897,778 msg/s |
| Dekaf | 2026-08-04T16:37:33.795899+00:00 | 1 | 6.0 MiB / 5.2 MiB | 393.1 MB/s | 6/5 | 182,699 | 364.2s / 931,372 msg/s |
| Dekaf | 2026-08-04T16:37:42.8008844+00:00 | 1 | 6.0 MiB / 4.9 MiB | 393.1 MB/s | 6/5 | 187,255 | 373.2s / 836,293 msg/s |
| Dekaf | 2026-08-04T16:37:51.8027581+00:00 | 1 | 5.0 MiB / 5.0 MiB | 393.1 MB/s | 7/5 | 194,384 | 382.2s / 846,194 msg/s |
| Dekaf | 2026-08-04T16:38:00.8101949+00:00 | 1 | 5.0 MiB / 5.0 MiB | 393.1 MB/s | 7/5 | 199,725 | 391.2s / 784,353 msg/s |
| Dekaf | 2026-08-04T16:38:09.8194302+00:00 | 2 | 6.0 MiB / 2.8 MiB | 377.3 MB/s | 6/5 | 52,437 | 400.2s / 829,727 msg/s |
| Dekaf | 2026-08-04T16:38:18.8214852+00:00 | 2 | 6.0 MiB / 5.6 MiB | 377.3 MB/s | 6/5 | 53,116 | 409.3s / 885,627 msg/s |
| Dekaf | 2026-08-04T16:38:27.8261402+00:00 | 2 | 6.0 MiB / 3.7 MiB | 377.3 MB/s | 6/5 | 54,599 | 418.3s / 889,500 msg/s |
| Dekaf | 2026-08-04T16:38:36.8293361+00:00 | 2 | 6.0 MiB / 2.0 MiB | 377.3 MB/s | 6/5 | 55,604 | 427.3s / 979,429 msg/s |
| Dekaf | 2026-08-04T16:38:45.8329077+00:00 | 3 | 7.0 MiB / 2.9 MiB | 367.6 MB/s | 6/7 | 28,036 | 436.3s / 954,532 msg/s |
| Dekaf | 2026-08-04T16:38:54.8382904+00:00 | 3 | 7.0 MiB / 2.1 MiB | 367.6 MB/s | 6/7 | 28,119 | 445.3s / 846,705 msg/s |
| Dekaf | 2026-08-04T16:39:03.841313+00:00 | 3 | 7.0 MiB / 2.7 MiB | 367.6 MB/s | 6/7 | 28,474 | 454.3s / 988,384 msg/s |
| Dekaf | 2026-08-04T16:39:12.8484209+00:00 | 3 | 7.0 MiB / 1.5 MiB | 367.6 MB/s | 6/7 | 28,676 | 463.3s / 850,091 msg/s |
| Dekaf | 2026-08-04T16:39:22.8494857+00:00 | 1 | 4.0 MiB / 3.2 MiB | 393.1 MB/s | 8/7 | 256,066 | 473.3s / 790,865 msg/s |
| Dekaf | 2026-08-04T16:39:31.8518112+00:00 | 1 | 4.0 MiB / 2.8 MiB | 393.1 MB/s | 8/7 | 264,143 | 482.3s / 969,119 msg/s |
| Dekaf | 2026-08-04T16:39:40.8548277+00:00 | 1 | 5.0 MiB / 4.9 MiB | 393.1 MB/s | 8/7 | 272,386 | 491.3s / 941,139 msg/s |
| Dekaf | 2026-08-04T16:39:49.8586959+00:00 | 1 | 4.0 MiB / 3.1 MiB | 428.3 MB/s | 8/7 | 282,025 | 500.3s / 955,108 msg/s |
| Dekaf | 2026-08-04T16:39:58.8656532+00:00 | 2 | 6.0 MiB / 5.1 MiB | 407.0 MB/s | 6/6 | 65,155 | 509.3s / 837,320 msg/s |
| Dekaf | 2026-08-04T16:40:07.8683316+00:00 | 2 | 6.0 MiB / 3.1 MiB | 407.0 MB/s | 6/6 | 66,690 | 518.3s / 957,561 msg/s |
| Dekaf | 2026-08-04T16:40:16.8712599+00:00 | 2 | 6.0 MiB / 4.3 MiB | 407.0 MB/s | 6/6 | 68,986 | 527.3s / 875,555 msg/s |
| Dekaf | 2026-08-04T16:40:25.8736776+00:00 | 2 | 6.0 MiB / 6.0 MiB | 407.0 MB/s | 6/6 | 70,989 | 536.3s / 856,568 msg/s |
| Dekaf | 2026-08-04T16:40:34.8772879+00:00 | 3 | 9.0 MiB / 4.0 MiB | 403.0 MB/s | 8/7 | 30,545 | 545.3s / 1,014,235 msg/s |
| Dekaf | 2026-08-04T16:40:43.8829972+00:00 | 3 | 9.0 MiB / 3.3 MiB | 403.0 MB/s | 8/7 | 30,598 | 554.3s / 926,090 msg/s |
| Dekaf | 2026-08-04T16:40:52.8843788+00:00 | 3 | 9.0 MiB / 2.5 MiB | 403.0 MB/s | 8/7 | 30,829 | 563.3s / 922,906 msg/s |
| Dekaf | 2026-08-04T16:41:01.8856013+00:00 | 3 | 9.0 MiB / 8.2 MiB | 403.0 MB/s | 8/7 | 31,105 | 572.3s / 826,467 msg/s |
| Dekaf | 2026-08-04T16:41:11.893011+00:00 | 1 | 5.0 MiB / 5.0 MiB | 428.3 MB/s | 9/8 | 336,912 | 582.3s / 852,605 msg/s |
| Dekaf | 2026-08-04T16:41:20.8932996+00:00 | 1 | 5.0 MiB / 5.0 MiB | 428.3 MB/s | 9/8 | 342,448 | 591.3s / 889,732 msg/s |
| Dekaf | 2026-08-04T16:41:29.8988416+00:00 | 1 | 5.0 MiB / 5.0 MiB | 428.3 MB/s | 9/8 | 348,173 | 600.3s / 885,091 msg/s |
| Dekaf | 2026-08-04T16:41:38.9023738+00:00 | 1 | 5.0 MiB / 3.9 MiB | 428.3 MB/s | 9/8 | 353,587 | 609.3s / 905,519 msg/s |
| Dekaf | 2026-08-04T16:41:47.9018831+00:00 | 1 | 5.0 MiB / 4.5 MiB | 428.3 MB/s | 9/8 | 359,938 | 618.3s / 870,292 msg/s |
| Dekaf | 2026-08-04T16:41:56.9039252+00:00 | 2 | 5.0 MiB / 3.1 MiB | 407.0 MB/s | 6/7 | 90,102 | 627.3s / 799,145 msg/s |
| Dekaf | 2026-08-04T16:42:05.9147361+00:00 | 2 | 5.0 MiB / 3.6 MiB | 407.0 MB/s | 6/7 | 91,435 | 636.3s / 795,288 msg/s |
| Dekaf | 2026-08-04T16:42:14.9207774+00:00 | 2 | 6.0 MiB / 4.2 MiB | 407.0 MB/s | 6/8 | 93,118 | 645.3s / 944,812 msg/s |
| Dekaf | 2026-08-04T16:42:23.9249484+00:00 | 2 | 6.0 MiB / 4.5 MiB | 407.0 MB/s | 6/8 | 94,502 | 654.4s / 886,852 msg/s |
| Dekaf | 2026-08-04T16:42:32.927216+00:00 | 3 | 11.0 MiB / 2.2 MiB | 403.0 MB/s | 10/7 | 31,639 | 663.4s / 947,483 msg/s |
| Dekaf | 2026-08-04T16:42:41.9303162+00:00 | 3 | 12.0 MiB / 2.9 MiB | 403.0 MB/s | 11/7 | 31,655 | 672.4s / 901,833 msg/s |
| Dekaf | 2026-08-04T16:42:50.9329843+00:00 | 3 | 12.0 MiB / 4.4 MiB | 403.0 MB/s | 11/7 | 31,655 | 681.4s / 885,240 msg/s |
| Dekaf | 2026-08-04T16:42:59.935024+00:00 | 3 | 12.0 MiB / 4.4 MiB | 403.0 MB/s | 11/7 | 31,655 | 690.4s / 845,360 msg/s |
| Dekaf | 2026-08-04T16:43:09.9386841+00:00 | 1 | 6.0 MiB / 5.5 MiB | 428.3 MB/s | 12/8 | 411,518 | 700.4s / 899,651 msg/s |
| Dekaf | 2026-08-04T16:43:18.9502847+00:00 | 1 | 6.0 MiB / 4.6 MiB | 428.3 MB/s | 12/8 | 415,194 | 709.4s / 817,804 msg/s |
| Dekaf | 2026-08-04T16:43:27.9536208+00:00 | 1 | 6.0 MiB / 5.9 MiB | 428.3 MB/s | 12/8 | 418,689 | 718.4s / 809,284 msg/s |
| Dekaf | 2026-08-04T16:43:36.9561593+00:00 | 1 | 6.0 MiB / 2.4 MiB | 428.3 MB/s | 12/8 | 423,514 | 727.4s / 932,236 msg/s |
| Dekaf | 2026-08-04T16:43:45.9590766+00:00 | 2 | 6.0 MiB / 3.9 MiB | 407.0 MB/s | 6/9 | 112,441 | 736.4s / 816,930 msg/s |
| Dekaf | 2026-08-04T16:43:54.9608143+00:00 | 2 | 6.0 MiB / 3.6 MiB | 407.0 MB/s | 6/9 | 114,198 | 745.4s / 954,038 msg/s |
| Dekaf | 2026-08-04T16:44:03.9633943+00:00 | 2 | 6.0 MiB / 3.3 MiB | 407.0 MB/s | 6/9 | 117,144 | 754.4s / 872,150 msg/s |
| Dekaf | 2026-08-04T16:44:12.9660955+00:00 | 2 | 6.0 MiB / 6.0 MiB | 407.0 MB/s | 6/9 | 119,662 | 763.4s / 854,695 msg/s |
| Dekaf | 2026-08-04T16:44:21.9691115+00:00 | 3 | 12.0 MiB / 5.1 MiB | 403.0 MB/s | 11/8 | 32,146 | 772.4s / 981,557 msg/s |
| Dekaf | 2026-08-04T16:44:30.9709007+00:00 | 3 | 12.0 MiB / 4.8 MiB | 403.0 MB/s | 11/8 | 32,221 | 781.4s / 867,757 msg/s |
| Dekaf | 2026-08-04T16:44:39.9754294+00:00 | 3 | 10.0 MiB / 3.4 MiB | 403.0 MB/s | 12/8 | 32,482 | 790.4s / 919,261 msg/s |
| Dekaf | 2026-08-04T16:44:48.9798224+00:00 | 3 | 10.0 MiB / 3.1 MiB | 403.0 MB/s | 12/8 | 32,584 | 799.4s / 933,962 msg/s |
| Dekaf | 2026-08-04T16:44:58.9842663+00:00 | 1 | 6.0 MiB / 6.0 MiB | 428.3 MB/s | 12/10 | 466,912 | 809.5s / 841,835 msg/s |
| Dekaf | 2026-08-04T16:45:07.9907157+00:00 | 1 | 6.0 MiB / 6.0 MiB | 428.3 MB/s | 12/10 | 472,195 | 818.5s / 960,505 msg/s |
| Dekaf | 2026-08-04T16:45:16.9934335+00:00 | 1 | 6.0 MiB / 5.2 MiB | 428.3 MB/s | 12/11 | 478,611 | 827.5s / 954,275 msg/s |
| Dekaf | 2026-08-04T16:45:25.9973614+00:00 | 1 | 6.0 MiB / 5.5 MiB | 428.3 MB/s | 12/11 | 484,507 | 836.5s / 1,017,182 msg/s |
| Dekaf | 2026-08-04T16:45:35.0006196+00:00 | 2 | 6.0 MiB / 4.3 MiB | 407.0 MB/s | 6/9 | 143,778 | 845.5s / 1,047,688 msg/s |
| Dekaf | 2026-08-04T16:45:44.0036988+00:00 | 2 | 6.0 MiB / 5.5 MiB | 407.0 MB/s | 6/9 | 146,548 | 854.5s / 962,521 msg/s |
| Dekaf | 2026-08-04T16:45:53.0047731+00:00 | 2 | 6.0 MiB / 6.0 MiB | 407.0 MB/s | 6/9 | 148,639 | 863.5s / 987,902 msg/s |
| Dekaf | 2026-08-04T16:46:02.0079783+00:00 | 2 | 6.0 MiB / 5.4 MiB | 407.0 MB/s | 6/9 | 151,665 | 872.5s / 1,016,772 msg/s |
| Dekaf | 2026-08-04T16:46:11.010418+00:00 | 3 | 9.0 MiB / 4.9 MiB | 403.0 MB/s | 14/9 | 36,864 | 881.5s / 977,938 msg/s |
| Dekaf | 2026-08-04T16:46:20.0108691+00:00 | 3 | 9.0 MiB / 5.9 MiB | 403.0 MB/s | 14/9 | 36,891 | 890.5s / 957,575 msg/s |
| Dekaf | 2026-08-04T16:46:29.0176189+00:00 | 3 | 9.0 MiB / 5.1 MiB | 403.0 MB/s | 14/9 | 37,159 | 899.5s / 941,545 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-04T16:31:59.7369017+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 1.3 MiB |
| Dekaf | 2026-08-04T16:31:59.7373819+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 0.9 MiB |
| Dekaf | 2026-08-04T16:32:14.8418811+00:00 | 2 | capacity | succeeded | 15,104ms | 14.0 MiB / 10.8 MiB |
| Dekaf | 2026-08-04T16:32:14.8443265+00:00 | 1 | capacity | succeeded | 15,106ms | 14.0 MiB / 12.6 MiB |
| Dekaf | 2026-08-04T16:32:14.8473109+00:00 | 3 | capacity | succeeded | 15,108ms | 14.0 MiB / 8.0 MiB |
| Dekaf | 2026-08-04T16:32:17.8553656+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 7.6 MiB |
| Dekaf | 2026-08-04T16:32:17.8590178+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:32:32.9213768+00:00 | 2 | capacity | succeeded | 15,064ms | 12.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-04T16:32:32.922175+00:00 | 3 | capacity | succeeded | 15,066ms | 12.0 MiB / 7.2 MiB |
| Dekaf | 2026-08-04T16:32:32.9304443+00:00 | 1 | capacity | succeeded | 15,071ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:32:35.930872+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 6.2 MiB |
| Dekaf | 2026-08-04T16:32:35.9387337+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.9 MiB |
| Dekaf | 2026-08-04T16:32:50.9812933+00:00 | 3 | capacity | failed | 15,049ms | 12.0 MiB / 5.7 MiB |
| Dekaf | 2026-08-04T16:32:50.9827915+00:00 | 2 | capacity | succeeded | 15,054ms | 10.0 MiB / 5.8 MiB |
| Dekaf | 2026-08-04T16:32:53.9899186+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:33:09.0413591+00:00 | 2 | capacity | failed | 15,051ms | 10.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:33:21.1041274+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:33:21.1327016+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-04T16:33:36.176811+00:00 | 1 | capacity | failed | 15,072ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:33:54.2701173+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 7.2 MiB |
| Dekaf | 2026-08-04T16:34:06.3228198+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:34:09.3258587+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.7 MiB |
| Dekaf | 2026-08-04T16:34:12.3775231+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:34:21.3889575+00:00 | 1 | capacity | failed | 15,066ms | 12.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-04T16:34:24.3889573+00:00 | 2 | capacity | succeeded | 15,063ms | 8.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-04T16:34:27.4001834+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 6.8 MiB |
| Dekaf | 2026-08-04T16:34:30.456644+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-04T16:34:42.4822273+00:00 | 2 | capacity | failed | 15,082ms | 8.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:34:45.5097338+00:00 | 3 | capacity | failed | 15,053ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-04T16:34:51.5237179+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.7 MiB |
| Dekaf | 2026-08-04T16:35:09.5886489+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-04T16:35:15.6258581+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-04T16:35:17.1351688+00:00 | 3 | capacity | failed | 1,509ms | 8.0 MiB / 5.5 MiB |
| Dekaf | 2026-08-04T16:35:24.648003+00:00 | 1 | capacity | failed | 15,059ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-04T16:35:47.2571555+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.9 MiB |
| Dekaf | 2026-08-04T16:35:54.8083136+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:35:57.7814596+00:00 | 2 | capacity | failed | 15,063ms | 8.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-04T16:36:02.3570975+00:00 | 3 | capacity | failed | 15,100ms | 8.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-04T16:36:12.8963293+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-04T16:36:27.9263691+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.5 MiB |
| Dekaf | 2026-08-04T16:36:27.954992+00:00 | 1 | capacity | succeeded | 15,058ms | 7.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-04T16:36:30.9599044+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-04T16:36:35.4919693+00:00 | 3 | capacity | failed | 3,018ms | 8.0 MiB / 6.0 MiB |
| Dekaf | 2026-08-04T16:36:42.9987393+00:00 | 2 | capacity | succeeded | 15,072ms | 7.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-04T16:36:46.0095653+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-04T16:36:46.0188098+00:00 | 1 | capacity | succeeded | 15,058ms | 6.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-04T16:36:49.0369323+00:00 | 2 | capacity | failed | 3,027ms | 7.0 MiB / 3.5 MiB |
| Dekaf | 2026-08-04T16:37:04.1166915+00:00 | 1 | capacity | failed | 15,085ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:37:05.6133606+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-04T16:37:19.1673346+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-04T16:37:34.2128048+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-04T16:37:34.2315477+00:00 | 2 | capacity | succeeded | 15,064ms | 6.0 MiB / 1.8 MiB |
| Dekaf | 2026-08-04T16:37:37.2501287+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-04T16:37:49.2696488+00:00 | 1 | capacity | succeeded | 15,057ms | 5.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-04T16:37:52.3121838+00:00 | 1 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:37:52.3339231+00:00 | 2 | capacity | failed | 15,083ms | 6.0 MiB / 0.6 MiB |
| Dekaf | 2026-08-04T16:38:05.8670416+00:00 | 3 | capacity | succeeded | 15,057ms | 7.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-04T16:38:07.3877088+00:00 | 1 | capacity | succeeded | 15,075ms | 4.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-04T16:38:10.4031311+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 3.0 MiB |
| Dekaf | 2026-08-04T16:38:23.9472095+00:00 | 3 | capacity | failed | 15,068ms | 7.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-04T16:38:25.4527427+00:00 | 1 | capacity | failed | 15,049ms | 4.0 MiB / 3.5 MiB |
| Dekaf | 2026-08-04T16:38:52.5683623+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:39:07.6169663+00:00 | 2 | capacity | failed | 15,048ms | 6.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-04T16:39:10.6068594+00:00 | 1 | capacity | failed | 15,046ms | 4.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-04T16:39:24.2050856+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-04T16:39:39.2677783+00:00 | 3 | capacity | succeeded | 15,060ms | 8.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-04T16:39:55.7512849+00:00 | 1 | capacity | succeeded | 15,034ms | 5.0 MiB / 4.0 MiB |
| Dekaf | 2026-08-04T16:40:09.4038746+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 2.8 MiB |
| Dekaf | 2026-08-04T16:40:24.4571256+00:00 | 3 | capacity | succeeded | 15,053ms | 9.0 MiB / 4.4 MiB |
| Dekaf | 2026-08-04T16:40:25.9227695+00:00 | 1 | capacity | started | 0ms | 4.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-04T16:40:54.5948347+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-04T16:41:08.1391426+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.8 MiB |
| Dekaf | 2026-08-04T16:41:09.6548497+00:00 | 3 | capacity | succeeded | 15,060ms | 10.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-04T16:41:23.1866855+00:00 | 2 | capacity | failed | 15,047ms | 6.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-04T16:41:41.1892532+00:00 | 1 | capacity | started | 0ms | 4.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-04T16:41:53.3377908+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-04T16:41:54.8659133+00:00 | 3 | capacity | succeeded | 15,100ms | 11.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-04T16:41:56.2386334+00:00 | 1 | capacity | succeeded | 15,049ms | 4.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-04T16:42:08.3997518+00:00 | 2 | capacity | failed | 15,061ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:42:14.3352468+00:00 | 1 | capacity | succeeded | 15,083ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:42:24.9814436+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 2.9 MiB |
| Dekaf | 2026-08-04T16:42:38.5312186+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 2.7 MiB |
| Dekaf | 2026-08-04T16:42:44.4177447+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:42:53.5912105+00:00 | 2 | capacity | failed | 15,059ms | 6.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-04T16:42:59.4767972+00:00 | 1 | capacity | succeeded | 15,059ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:43:10.1635911+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 2.3 MiB |
| Dekaf | 2026-08-04T16:43:29.5859323+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 4.4 MiB |
| Dekaf | 2026-08-04T16:43:44.6536585+00:00 | 1 | capacity | failed | 15,067ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:44:14.7678016+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-04T16:44:23.0343966+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 8.2 MiB |
| Dekaf | 2026-08-04T16:44:38.0904548+00:00 | 3 | capacity | succeeded | 15,056ms | 10.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-04T16:44:41.0979862+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-04T16:44:56.1548277+00:00 | 3 | capacity | succeeded | 15,056ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-08-04T16:44:59.1608102+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.8 MiB |
| Dekaf | 2026-08-04T16:45:14.2223542+00:00 | 3 | capacity | succeeded | 15,061ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-04T16:45:15.0057049+00:00 | 1 | capacity | failed | 15,059ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:45:44.3686121+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-04T16:45:45.1009558+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:46:00.1507585+00:00 | 1 | capacity | failed | 15,049ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-04T16:46:29.5193112+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.9 MiB |
*25 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 89 |
| Dekaf | 1 | 0.002–0.004ms | 91 |
| Dekaf | 1 | 0.004–0.008ms | 200 |
| Dekaf | 1 | 0.008–0.016ms | 586 |
| Dekaf | 1 | 0.016–0.032ms | 1,956 |
| Dekaf | 1 | 0.032–0.064ms | 3,815 |
| Dekaf | 1 | 0.064–0.128ms | 5,956 |
| Dekaf | 1 | 0.128–0.256ms | 7,948 |
| Dekaf | 1 | 0.256–0.512ms | 15,935 |
| Dekaf | 1 | 0.512–1.024ms | 28,313 |
| Dekaf | 1 | 1.024–2.048ms | 32,488 |
| Dekaf | 1 | 2.048–4.096ms | 17,545 |
| Dekaf | 1 | 4.096–8.192ms | 5,073 |
| Dekaf | 1 | 8.192–16.384ms | 1,254 |
| Dekaf | 1 | 16.384–32.768ms | 304 |
| Dekaf | 1 | 32.768–65.536ms | 13 |
| Dekaf | 2 | 0.001–0.002ms | 26 |
| Dekaf | 2 | 0.002–0.004ms | 26 |
| Dekaf | 2 | 0.004–0.008ms | 94 |
| Dekaf | 2 | 0.008–0.016ms | 264 |
| Dekaf | 2 | 0.016–0.032ms | 728 |
| Dekaf | 2 | 0.032–0.064ms | 1,277 |
| Dekaf | 2 | 0.064–0.128ms | 1,906 |
| Dekaf | 2 | 0.128–0.256ms | 2,499 |
| Dekaf | 2 | 0.256–0.512ms | 4,848 |
| Dekaf | 2 | 0.512–1.024ms | 8,203 |
| Dekaf | 2 | 1.024–2.048ms | 9,703 |
| Dekaf | 2 | 2.048–4.096ms | 5,479 |
| Dekaf | 2 | 4.096–8.192ms | 1,973 |
| Dekaf | 2 | 8.192–16.384ms | 619 |
| Dekaf | 2 | 16.384–32.768ms | 164 |
| Dekaf | 2 | 32.768–65.536ms | 4 |
| Dekaf | 3 | 0.001–0.002ms | 7 |
| Dekaf | 3 | 0.002–0.004ms | 7 |
| Dekaf | 3 | 0.004–0.008ms | 30 |
| Dekaf | 3 | 0.008–0.016ms | 62 |
| Dekaf | 3 | 0.016–0.032ms | 173 |
| Dekaf | 3 | 0.032–0.064ms | 353 |
| Dekaf | 3 | 0.064–0.128ms | 429 |
| Dekaf | 3 | 0.128–0.256ms | 639 |
| Dekaf | 3 | 0.256–0.512ms | 1,217 |
| Dekaf | 3 | 0.512–1.024ms | 1,953 |
| Dekaf | 3 | 1.024–2.048ms | 2,326 |
| Dekaf | 3 | 2.048–4.096ms | 1,338 |
| Dekaf | 3 | 4.096–8.192ms | 387 |
| Dekaf | 3 | 8.192–16.384ms | 74 |
| Dekaf | 3 | 16.384–32.768ms | 25 |
| Dekaf | 3 | 32.768–65.536ms | 2 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 11,000 | 2026-08-04T16:16:29.4833879+00:00 | 210.3ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 19,000 | 2026-08-04T16:16:29.5170067+00:00 | 257.1ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 32,000 | 2026-08-04T16:16:29.5456903+00:00 | 259.9ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 34,000 | 2026-08-04T16:16:29.5491784+00:00 | 258.9ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 65,000 | 2026-08-04T16:16:29.6122267+00:00 | 509.2ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 73,000 | 2026-08-04T16:16:29.6343873+00:00 | 500.7ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 77,000 | 2026-08-04T16:16:29.6429179+00:00 | 437.3ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 82,000 | 2026-08-04T16:16:29.6531262+00:00 | 464.6ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 96,000 | 2026-08-04T16:16:29.6777577+00:00 | 547.9ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 136,000 | 2026-08-04T16:16:29.7669871+00:00 | 589.0ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 162,000 | 2026-08-04T16:16:29.8256853+00:00 | 651.5ms | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 165,000 | 2026-08-04T16:16:29.8312692+00:00 | 590.6ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 175,000 | 2026-08-04T16:16:29.8706733+00:00 | 564.5ms | GC pause | - | - | 1.0s / 383,549 msg/s | Gen2 +0 / pause +64.0ms |
| Confluent | 245,000 | 2026-08-04T16:16:30.1090044+00:00 | 694.4ms | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 255,000 | 2026-08-04T16:16:30.1272174+00:00 | 696.9ms | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 258,000 | 2026-08-04T16:16:30.1336831+00:00 | 852.7ms | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 264,000 | 2026-08-04T16:16:30.1647494+00:00 | 1.0s | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 300,000 | 2026-08-04T16:16:30.2348916+00:00 | 1.0s | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 332,000 | 2026-08-04T16:16:30.3230978+00:00 | 627.4ms | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 362,000 | 2026-08-04T16:16:30.383098+00:00 | 639.0ms | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 375,000 | 2026-08-04T16:16:30.416331+00:00 | 1.0s | GC pause | - | - | 2.0s / 375,129 msg/s | Gen2 +0 / pause +184.2ms |
| Confluent | 386,000 | 2026-08-04T16:16:30.4400622+00:00 | 1.1s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 426,000 | 2026-08-04T16:16:30.5128685+00:00 | 1.2s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 448,000 | 2026-08-04T16:16:30.5705206+00:00 | 1.2s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 464,000 | 2026-08-04T16:16:30.6132789+00:00 | 1.5s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 472,000 | 2026-08-04T16:16:30.6314561+00:00 | 943.1ms | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 474,000 | 2026-08-04T16:16:30.6461606+00:00 | 1.5s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 481,000 | 2026-08-04T16:16:30.6674956+00:00 | 1.3s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 499,000 | 2026-08-04T16:16:30.7716074+00:00 | 1.1s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 518,000 | 2026-08-04T16:16:30.8033823+00:00 | 1.2s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 537,000 | 2026-08-04T16:16:30.835613+00:00 | 1.3s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 541,000 | 2026-08-04T16:16:30.8451253+00:00 | 1.3s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 543,000 | 2026-08-04T16:16:30.8484594+00:00 | 1.6s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +519.7ms |
| Confluent | 566,000 | 2026-08-04T16:16:30.9146581+00:00 | 1.3s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 578,000 | 2026-08-04T16:16:30.9359211+00:00 | 1.5s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 585,000 | 2026-08-04T16:16:30.9549229+00:00 | 1.3s | GC pause | - | - | 3.0s / 356,903 msg/s | Gen2 +0 / pause +387.4ms |
| Confluent | 689,000 | 2026-08-04T16:16:31.1834149+00:00 | 1.4s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +519.7ms |
| Confluent | 727,000 | 2026-08-04T16:16:31.2609783+00:00 | 1.6s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +519.7ms |
| Confluent | 758,000 | 2026-08-04T16:16:31.4352148+00:00 | 1.6s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +519.7ms |
| Confluent | 769,000 | 2026-08-04T16:16:31.4812611+00:00 | 1.3s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 776,000 | 2026-08-04T16:16:31.4966171+00:00 | 1.3s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 781,000 | 2026-08-04T16:16:31.5066271+00:00 | 1.6s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 806,000 | 2026-08-04T16:16:31.5566302+00:00 | 1.3s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 815,000 | 2026-08-04T16:16:31.5874898+00:00 | 1.3s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 837,000 | 2026-08-04T16:16:31.6479232+00:00 | 1.6s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 838,000 | 2026-08-04T16:16:31.654244+00:00 | 1.5s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 842,000 | 2026-08-04T16:16:31.6695977+00:00 | 1.1s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 908,000 | 2026-08-04T16:16:31.98117+00:00 | 1.3s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 948,000 | 2026-08-04T16:16:32.1813994+00:00 | 1.4s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 973,000 | 2026-08-04T16:16:32.2238792+00:00 | 1.5s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 994,000 | 2026-08-04T16:16:32.2672134+00:00 | 1.2s | GC pause | - | - | 4.0s / 338,479 msg/s | Gen2 +0 / pause +399.5ms |
| Confluent | 1,028,000 | 2026-08-04T16:16:32.3225611+00:00 | 1.4s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,040,000 | 2026-08-04T16:16:32.3400939+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,049,000 | 2026-08-04T16:16:32.3574268+00:00 | 1.1s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,050,000 | 2026-08-04T16:16:32.359769+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,055,000 | 2026-08-04T16:16:32.3656962+00:00 | 1.1s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,090,000 | 2026-08-04T16:16:32.4054152+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,114,000 | 2026-08-04T16:16:32.4343112+00:00 | 1.3s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +545.6ms |
| Confluent | 1,130,000 | 2026-08-04T16:16:32.4730939+00:00 | 1.7s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,142,000 | 2026-08-04T16:16:32.5091758+00:00 | 990.0ms | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,146,000 | 2026-08-04T16:16:32.5188093+00:00 | 1.1s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,178,000 | 2026-08-04T16:16:32.6227016+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,190,000 | 2026-08-04T16:16:32.6494567+00:00 | 1.8s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,191,000 | 2026-08-04T16:16:32.6507687+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,200,000 | 2026-08-04T16:16:32.6694455+00:00 | 1.7s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,207,000 | 2026-08-04T16:16:32.701555+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,211,000 | 2026-08-04T16:16:32.7141682+00:00 | 1.6s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,226,000 | 2026-08-04T16:16:32.7573388+00:00 | 1.1s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,293,000 | 2026-08-04T16:16:32.9091579+00:00 | 1.7s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +379.4ms |
| Confluent | 1,294,000 | 2026-08-04T16:16:32.9110965+00:00 | 1.4s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,303,000 | 2026-08-04T16:16:32.9427909+00:00 | 1.7s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +379.4ms |
| Confluent | 1,355,000 | 2026-08-04T16:16:33.0977494+00:00 | 1.1s | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,386,000 | 2026-08-04T16:16:33.239127+00:00 | 975.0ms | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,411,000 | 2026-08-04T16:16:33.3063448+00:00 | 1.5s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +379.4ms |
| Confluent | 1,413,000 | 2026-08-04T16:16:33.3138201+00:00 | 1.4s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +379.4ms |
| Confluent | 1,422,000 | 2026-08-04T16:16:33.3404549+00:00 | 827.7ms | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,426,000 | 2026-08-04T16:16:33.3502906+00:00 | 999.2ms | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +278.4ms |
| Confluent | 1,437,000 | 2026-08-04T16:16:33.3836787+00:00 | 1.5s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +379.4ms |
| Confluent | 1,480,000 | 2026-08-04T16:16:33.5303213+00:00 | 1.3s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,489,000 | 2026-08-04T16:16:33.5682991+00:00 | 954.0ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,512,000 | 2026-08-04T16:16:33.6216857+00:00 | 732.7ms | GC pause | - | - | 5.0s / 392,877 msg/s | Gen2 +0 / pause +146.1ms |
| Confluent | 1,531,000 | 2026-08-04T16:16:33.6669999+00:00 | 1.4s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,537,000 | 2026-08-04T16:16:33.680197+00:00 | 1.4s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,581,000 | 2026-08-04T16:16:33.7511511+00:00 | 1.5s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,604,000 | 2026-08-04T16:16:33.823124+00:00 | 1.2s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,625,000 | 2026-08-04T16:16:33.8729006+00:00 | 925.1ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,635,000 | 2026-08-04T16:16:33.898303+00:00 | 942.1ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,648,000 | 2026-08-04T16:16:33.9314789+00:00 | 1.4s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,671,000 | 2026-08-04T16:16:33.9908969+00:00 | 1.4s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,680,000 | 2026-08-04T16:16:34.0127583+00:00 | 1.3s | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,692,000 | 2026-08-04T16:16:34.0317589+00:00 | 759.5ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,705,000 | 2026-08-04T16:16:34.0605601+00:00 | 911.8ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,707,000 | 2026-08-04T16:16:34.0633634+00:00 | 1.4s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,727,000 | 2026-08-04T16:16:34.1000751+00:00 | 1.4s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,732,000 | 2026-08-04T16:16:34.1081105+00:00 | 769.6ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,743,000 | 2026-08-04T16:16:34.1493337+00:00 | 1.3s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,762,000 | 2026-08-04T16:16:34.1960956+00:00 | 782.2ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,768,000 | 2026-08-04T16:16:34.2080851+00:00 | 1.4s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,778,000 | 2026-08-04T16:16:34.2298245+00:00 | 1.4s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,783,000 | 2026-08-04T16:16:34.2590065+00:00 | 1.2s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,785,000 | 2026-08-04T16:16:34.2687218+00:00 | 857.3ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,790,000 | 2026-08-04T16:16:34.2766347+00:00 | 1.2s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,792,000 | 2026-08-04T16:16:34.2852488+00:00 | 746.4ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,793,000 | 2026-08-04T16:16:34.2870044+00:00 | 1.2s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +346.1ms |
| Confluent | 1,796,000 | 2026-08-04T16:16:34.2918727+00:00 | 843.0ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,805,000 | 2026-08-04T16:16:34.3134027+00:00 | 833.5ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,822,000 | 2026-08-04T16:16:34.3584536+00:00 | 718.2ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +247.1ms |
| Confluent | 1,862,000 | 2026-08-04T16:16:34.4859659+00:00 | 933.8ms | GC pause | - | - | 6.0s / 378,612 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 1,882,000 | 2026-08-04T16:16:34.5682517+00:00 | 902.3ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 1,907,000 | 2026-08-04T16:16:34.6308664+00:00 | 1.2s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 1,934,000 | 2026-08-04T16:16:34.6837315+00:00 | 833.7ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 1,936,000 | 2026-08-04T16:16:34.6917648+00:00 | 779.7ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 1,965,000 | 2026-08-04T16:16:34.7675513+00:00 | 771.2ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 1,968,000 | 2026-08-04T16:16:34.7755156+00:00 | 1.2s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 1,982,000 | 2026-08-04T16:16:34.7938255+00:00 | 882.8ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,015,000 | 2026-08-04T16:16:34.8646327+00:00 | 724.8ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,022,000 | 2026-08-04T16:16:34.8763335+00:00 | 826.4ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,034,000 | 2026-08-04T16:16:34.8982985+00:00 | 837.3ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,058,000 | 2026-08-04T16:16:34.9491087+00:00 | 1.2s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,062,000 | 2026-08-04T16:16:34.9590239+00:00 | 900.1ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,085,000 | 2026-08-04T16:16:35.0258009+00:00 | 652.2ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,086,000 | 2026-08-04T16:16:35.0275692+00:00 | 650.5ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,118,000 | 2026-08-04T16:16:35.1317501+00:00 | 1.1s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,134,000 | 2026-08-04T16:16:35.1757997+00:00 | 743.8ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,136,000 | 2026-08-04T16:16:35.1848108+00:00 | 661.1ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,197,000 | 2026-08-04T16:16:35.3392357+00:00 | 1.1s | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,205,000 | 2026-08-04T16:16:35.3684078+00:00 | 619.2ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +200.0ms |
| Confluent | 2,253,000 | 2026-08-04T16:16:35.5651884+00:00 | 1.0s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,258,000 | 2026-08-04T16:16:35.5766715+00:00 | 1.0s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,261,000 | 2026-08-04T16:16:35.5860623+00:00 | 997.4ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,270,000 | 2026-08-04T16:16:35.605662+00:00 | 1.0s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,274,000 | 2026-08-04T16:16:35.6173877+00:00 | 478.0ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,279,000 | 2026-08-04T16:16:35.6228906+00:00 | 469.7ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,325,000 | 2026-08-04T16:16:35.6922634+00:00 | 463.6ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,385,000 | 2026-08-04T16:16:35.7886527+00:00 | 468.6ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,388,000 | 2026-08-04T16:16:35.7944447+00:00 | 1.1s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,399,000 | 2026-08-04T16:16:35.8092364+00:00 | 477.1ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,401,000 | 2026-08-04T16:16:35.8147003+00:00 | 1.1s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,405,000 | 2026-08-04T16:16:35.8258662+00:00 | 478.1ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,415,000 | 2026-08-04T16:16:35.840556+00:00 | 471.0ms | GC pause | - | - | 7.0s / 404,699 msg/s | Gen2 +0 / pause +99.0ms |
| Confluent | 2,417,000 | 2026-08-04T16:16:35.8442106+00:00 | 1.1s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,418,000 | 2026-08-04T16:16:35.8463433+00:00 | 1.1s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,478,000 | 2026-08-04T16:16:36.0554645+00:00 | 945.3ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,509,000 | 2026-08-04T16:16:36.1257358+00:00 | 400.1ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,519,000 | 2026-08-04T16:16:36.1422383+00:00 | 392.6ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,533,000 | 2026-08-04T16:16:36.1709666+00:00 | 1.2s | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,585,000 | 2026-08-04T16:16:36.2980381+00:00 | 367.0ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,604,000 | 2026-08-04T16:16:36.3543657+00:00 | 587.5ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,628,000 | 2026-08-04T16:16:36.418829+00:00 | 820.7ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 2,648,000 | 2026-08-04T16:16:36.481215+00:00 | 770.8ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 2,681,000 | 2026-08-04T16:16:36.5763687+00:00 | 711.6ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 2,717,000 | 2026-08-04T16:16:36.7064966+00:00 | 642.9ms | GC pause | - | - | 8.0s / 286,124 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 2,871,000 | 2026-08-04T16:16:37.2876519+00:00 | 259.2ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +206.8ms |
| Confluent | 2,975,000 | 2026-08-04T16:16:37.5847976+00:00 | 101.3ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 2,999,000 | 2026-08-04T16:16:37.6353263+00:00 | 109.6ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,040,000 | 2026-08-04T16:16:37.7526371+00:00 | 486.0ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,048,000 | 2026-08-04T16:16:37.7776781+00:00 | 125.1ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,050,000 | 2026-08-04T16:16:37.7800555+00:00 | 479.6ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,065,000 | 2026-08-04T16:16:37.8032998+00:00 | 138.6ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,066,000 | 2026-08-04T16:16:37.8042014+00:00 | 137.8ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,070,000 | 2026-08-04T16:16:37.8084283+00:00 | 488.4ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,079,000 | 2026-08-04T16:16:37.819521+00:00 | 145.2ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,140,000 | 2026-08-04T16:16:38.0055227+00:00 | 422.7ms | GC pause | - | - | 9.0s / 384,063 msg/s | Gen2 +0 / pause +112.6ms |
| Confluent | 3,203,000 | 2026-08-04T16:16:38.1240753+00:00 | 364.0ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +239.9ms |
| Confluent | 3,273,000 | 2026-08-04T16:16:38.360064+00:00 | 250.2ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +239.9ms |
| Confluent | 3,360,000 | 2026-08-04T16:16:38.6088005+00:00 | 266.2ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,402,000 | 2026-08-04T16:16:38.745829+00:00 | 123.6ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,403,000 | 2026-08-04T16:16:38.7505226+00:00 | 210.9ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,433,000 | 2026-08-04T16:16:38.8048725+00:00 | 209.9ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,448,000 | 2026-08-04T16:16:38.8429204+00:00 | 107.5ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,455,000 | 2026-08-04T16:16:38.8597242+00:00 | 113.3ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,463,000 | 2026-08-04T16:16:38.875402+00:00 | 167.7ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,470,000 | 2026-08-04T16:16:38.8835301+00:00 | 167.1ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,472,000 | 2026-08-04T16:16:38.8863943+00:00 | 164.7ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,484,000 | 2026-08-04T16:16:38.9040166+00:00 | 116.2ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,636,000 | 2026-08-04T16:16:39.3418923+00:00 | 101.3ms | GC pause | - | - | 10.0s / 382,196 msg/s | Gen2 +0 / pause +127.3ms |
| Confluent | 3,763,000 | 2026-08-04T16:16:39.6195517+00:00 | 101.9ms | GC pause | - | - | 11.0s / 425,903 msg/s | Gen2 +0 / pause +118.3ms |
| Confluent | 3,812,000 | 2026-08-04T16:16:39.7306261+00:00 | 120.6ms | GC pause | - | - | 11.0s / 425,903 msg/s | Gen2 +0 / pause +118.3ms |
| Confluent | 3,904,000 | 2026-08-04T16:16:39.9671601+00:00 | 100.6ms | GC pause | - | - | 11.0s / 425,903 msg/s | Gen2 +0 / pause +118.3ms |
| Confluent | 4,245,000 | 2026-08-04T16:16:40.7448497+00:00 | 125.6ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,286,000 | 2026-08-04T16:16:40.8221913+00:00 | 124.8ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,290,000 | 2026-08-04T16:16:40.8266635+00:00 | 155.1ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,318,000 | 2026-08-04T16:16:40.8691545+00:00 | 258.2ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,326,000 | 2026-08-04T16:16:40.880151+00:00 | 174.5ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,328,000 | 2026-08-04T16:16:40.8835539+00:00 | 282.6ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,354,000 | 2026-08-04T16:16:40.9379461+00:00 | 143.6ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,374,000 | 2026-08-04T16:16:40.9981966+00:00 | 116.0ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,391,000 | 2026-08-04T16:16:41.0256983+00:00 | 283.6ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,396,000 | 2026-08-04T16:16:41.032445+00:00 | 167.1ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,443,000 | 2026-08-04T16:16:41.1701847+00:00 | 103.6ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,463,000 | 2026-08-04T16:16:41.2215633+00:00 | 133.7ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,473,000 | 2026-08-04T16:16:41.23544+00:00 | 147.2ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,488,000 | 2026-08-04T16:16:41.2765259+00:00 | 280.4ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +252.9ms |
| Confluent | 4,499,000 | 2026-08-04T16:16:41.2922007+00:00 | 148.4ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,503,000 | 2026-08-04T16:16:41.2984609+00:00 | 131.1ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,507,000 | 2026-08-04T16:16:41.3030642+00:00 | 291.7ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +252.9ms |
| Confluent | 4,510,000 | 2026-08-04T16:16:41.3098986+00:00 | 128.1ms | GC pause | - | - | 12.0s / 456,346 msg/s | Gen2 +0 / pause +119.7ms |
| Confluent | 4,537,000 | 2026-08-04T16:16:41.3641663+00:00 | 318.7ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +252.9ms |
| Confluent | 4,551,000 | 2026-08-04T16:16:41.3917331+00:00 | 310.5ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +252.9ms |
| Confluent | 4,555,000 | 2026-08-04T16:16:41.3978324+00:00 | 178.8ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +252.9ms |
| Confluent | 4,568,000 | 2026-08-04T16:16:41.4447292+00:00 | 286.1ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +252.9ms |
| Confluent | 4,606,000 | 2026-08-04T16:16:41.5265929+00:00 | 157.7ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,617,000 | 2026-08-04T16:16:41.5507746+00:00 | 296.2ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,625,000 | 2026-08-04T16:16:41.5651724+00:00 | 156.0ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,631,000 | 2026-08-04T16:16:41.5747698+00:00 | 283.8ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,649,000 | 2026-08-04T16:16:41.6236817+00:00 | 161.9ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,676,000 | 2026-08-04T16:16:41.7122704+00:00 | 110.1ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,682,000 | 2026-08-04T16:16:41.7339174+00:00 | 118.0ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,685,000 | 2026-08-04T16:16:41.7547866+00:00 | 106.7ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,687,000 | 2026-08-04T16:16:41.7580063+00:00 | 210.2ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,697,000 | 2026-08-04T16:16:41.7795486+00:00 | 224.9ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,711,000 | 2026-08-04T16:16:41.8020627+00:00 | 226.9ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,787,000 | 2026-08-04T16:16:41.9298023+00:00 | 216.2ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,793,000 | 2026-08-04T16:16:41.9385913+00:00 | 114.7ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,805,000 | 2026-08-04T16:16:41.9546991+00:00 | 134.4ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,807,000 | 2026-08-04T16:16:41.9564433+00:00 | 200.0ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,817,000 | 2026-08-04T16:16:41.9857809+00:00 | 181.5ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,859,000 | 2026-08-04T16:16:42.1020578+00:00 | 138.0ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 4,966,000 | 2026-08-04T16:16:42.3524646+00:00 | 108.1ms | GC pause | - | - | 13.0s / 438,389 msg/s | Gen2 +0 / pause +133.2ms |
| Confluent | 5,170,000 | 2026-08-04T16:16:42.8681591+00:00 | 223.0ms | GC pause | - | - | 14.0s / 440,486 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 5,183,000 | 2026-08-04T16:16:42.8838666+00:00 | 213.4ms | GC pause | - | - | 14.0s / 440,486 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 5,218,000 | 2026-08-04T16:16:42.9371762+00:00 | 135.1ms | GC pause | - | - | 14.0s / 440,486 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 5,228,000 | 2026-08-04T16:16:42.9520375+00:00 | 138.4ms | GC pause | - | - | 14.0s / 440,486 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 5,283,000 | 2026-08-04T16:16:43.0572407+00:00 | 203.8ms | GC pause | - | - | 14.0s / 440,486 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 5,300,000 | 2026-08-04T16:16:43.1132381+00:00 | 207.4ms | GC pause | - | - | 14.0s / 440,486 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 5,413,000 | 2026-08-04T16:16:43.3814006+00:00 | 111.6ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +252.0ms |
| Confluent | 5,672,000 | 2026-08-04T16:16:43.9333146+00:00 | 119.9ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,709,000 | 2026-08-04T16:16:43.9937034+00:00 | 124.4ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,711,000 | 2026-08-04T16:16:43.9959997+00:00 | 181.7ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,721,000 | 2026-08-04T16:16:44.0139855+00:00 | 168.3ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,726,000 | 2026-08-04T16:16:44.0321507+00:00 | 106.5ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,727,000 | 2026-08-04T16:16:44.0347404+00:00 | 151.5ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,735,000 | 2026-08-04T16:16:44.051379+00:00 | 103.7ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,851,000 | 2026-08-04T16:16:44.3141575+00:00 | 138.4ms | GC pause | - | - | 15.0s / 475,780 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 5,875,000 | 2026-08-04T16:16:44.3492379+00:00 | 137.3ms | GC pause | - | - | 16.0s / 470,617 msg/s | Gen2 +0 / pause +238.6ms |
| Confluent | 5,916,000 | 2026-08-04T16:16:44.4349771+00:00 | 122.0ms | GC pause | - | - | 16.0s / 470,617 msg/s | Gen2 +0 / pause +238.6ms |
| Confluent | 5,919,000 | 2026-08-04T16:16:44.4422814+00:00 | 115.0ms | GC pause | - | - | 16.0s / 470,617 msg/s | Gen2 +0 / pause +238.6ms |
| Confluent | 5,923,000 | 2026-08-04T16:16:44.4530847+00:00 | 129.9ms | GC pause | - | - | 16.0s / 470,617 msg/s | Gen2 +0 / pause +238.6ms |
| Confluent | 6,324,000 | 2026-08-04T16:16:45.3001956+00:00 | 107.8ms | GC pause | - | - | 16.0s / 470,617 msg/s | Gen2 +0 / pause +118.1ms |
| Confluent | 6,340,000 | 2026-08-04T16:16:45.3232323+00:00 | 142.5ms | GC pause | - | - | 17.0s / 468,508 msg/s | Gen2 +0 / pause +248.7ms |
| Confluent | 6,353,000 | 2026-08-04T16:16:45.348515+00:00 | 123.3ms | GC pause | - | - | 17.0s / 468,508 msg/s | Gen2 +0 / pause +248.7ms |
| Confluent | 10,715,000 | 2026-08-04T16:16:53.1826423+00:00 | 100.7ms | GC pause | - | - | 24.0s / 553,537 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 85,751,000 | 2026-08-04T16:18:57.7720425+00:00 | 123.6ms | GC pause | - | - | 149.1s / 912,998 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 85,759,000 | 2026-08-04T16:18:57.783311+00:00 | 114.6ms | GC pause | - | - | 149.1s / 912,998 msg/s | Gen2 +0 / pause +94.2ms |
| Confluent | 86,830,000 | 2026-08-04T16:18:58.938741+00:00 | 113.6ms | GC pause | - | - | 150.1s / 763,712 msg/s | Gen2 +0 / pause +95.6ms |
| Confluent | 86,873,000 | 2026-08-04T16:18:58.9824079+00:00 | 101.8ms | GC pause | - | - | 150.1s / 763,712 msg/s | Gen2 +0 / pause +95.6ms |
| Confluent | 123,282,000 | 2026-08-04T16:19:52.7743304+00:00 | 111.9ms | GC pause | - | - | 204.2s / 737,071 msg/s | Gen2 +0 / pause +84.3ms |
| Confluent | 123,302,000 | 2026-08-04T16:19:52.7935919+00:00 | 116.1ms | GC pause | - | - | 204.2s / 737,071 msg/s | Gen2 +0 / pause +84.3ms |
| Confluent | 123,316,000 | 2026-08-04T16:19:52.8047096+00:00 | 114.5ms | GC pause | - | - | 204.2s / 737,071 msg/s | Gen2 +0 / pause +84.3ms |
| Confluent | 123,331,000 | 2026-08-04T16:19:52.8244202+00:00 | 132.5ms | GC pause | - | - | 204.2s / 737,071 msg/s | Gen2 +0 / pause +84.3ms |
| Confluent | 139,788,000 | 2026-08-04T16:20:16.2543018+00:00 | 105.5ms | GC pause | - | - | 227.2s / 1,007,901 msg/s | Gen2 +0 / pause +99.8ms |
| Confluent | 139,857,000 | 2026-08-04T16:20:16.3132024+00:00 | 105.7ms | GC pause | - | - | 227.2s / 1,007,901 msg/s | Gen2 +0 / pause +99.8ms |
| Confluent | 139,891,000 | 2026-08-04T16:20:16.3461539+00:00 | 103.4ms | GC pause | - | - | 227.2s / 1,007,901 msg/s | Gen2 +0 / pause +99.8ms |
| Confluent | 334,503,000 | 2026-08-04T16:24:39.3128976+00:00 | 112.9ms | GC pause | - | - | 490.4s / 749,187 msg/s | Gen2 +0 / pause +88.4ms |
| Confluent | 601,354,000 | 2026-08-04T16:31:18.7269906+00:00 | 119.9ms | GC pause | - | - | 889.7s / 647,689 msg/s | Gen2 +0 / pause +84.4ms |
| Confluent | 601,372,000 | 2026-08-04T16:31:18.7601387+00:00 | 116.2ms | GC pause | - | - | 889.7s / 647,689 msg/s | Gen2 +0 / pause +84.4ms |
| Dekaf | 3,321,000 | 2026-08-04T16:31:33.7604477+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 872,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,329,000 | 2026-08-04T16:31:33.7709853+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 872,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,332,000 | 2026-08-04T16:31:33.7720105+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 872,129 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,031,000 | 2026-08-04T16:31:35.775232+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 810,501 msg/s | Gen2 +0 / pause +2.3ms |
| Dekaf | 7,132,000 | 2026-08-04T16:31:38.2578357+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 855,211 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,159,000 | 2026-08-04T16:31:38.283873+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 855,211 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,808,000 | 2026-08-04T16:31:49.2412927+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 927,921 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,825,000 | 2026-08-04T16:31:49.2547615+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 927,921 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,828,000 | 2026-08-04T16:31:49.256591+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 927,921 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,830,000 | 2026-08-04T16:31:49.2606693+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 927,921 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,835,000 | 2026-08-04T16:31:49.2653882+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 927,921 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 102,424,000 | 2026-08-04T16:33:21.5081303+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 112.1s / 835,126 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 102,427,000 | 2026-08-04T16:33:21.5108526+00:00 | 109.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 112.1s / 835,126 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 152,606,000 | 2026-08-04T16:34:16.732855+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded, 3:capacity/succeeded | - | 168.1s / 740,610 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 152,607,000 | 2026-08-04T16:34:16.733445+00:00 | 105.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded, 3:capacity/succeeded | - | 168.1s / 740,610 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 152,617,000 | 2026-08-04T16:34:16.7482431+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded, 3:capacity/succeeded | - | 168.1s / 740,610 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 152,624,000 | 2026-08-04T16:34:16.7609728+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded, 3:capacity/succeeded | - | 168.1s / 740,610 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 152,627,000 | 2026-08-04T16:34:16.7663884+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded, 3:capacity/succeeded | - | 168.1s / 740,610 msg/s | Gen2 +0 / pause +0.7ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*4,063 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.63x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.31x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,391,493 | 1,359,294–1,424,454 | 0.99 | 1.28x |
| Confluent | 2 | 1,089,585 | 1,056,830–1,123,356 | 1.59 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.93 | 937.88 | 1,414,197 | 1,424,454 | -3.2% | -0.43% | 1348.68 | 1,414,197 | 0 | 1.32 |
| Dekaf (3conn) | 0.82 | 759.31 | 1,442,321 | 1,416,254 | -7.4% | -0.61% | 1375.50 | 1,442,321 | 0 | 1.19 |
| Dekaf (confluent-first) | 1.05 | 1068.62 | 1,348,981 | 1,359,294 | +6.3% | +0.53% | 1286.49 | 1,348,981 | 0 | 1.42 |
| Confluent (confluent-first) | 1.57 | - | 1,098,216 | 1,123,356 | -27.6% | -2.55% | 1047.34 | 1,098,216 | 0 | 1.72 |
| Confluent (dekaf-first) | 1.61 | - | 1,055,552 | 1,056,830 | +4.2% | +0.24% | 1006.65 | 1,055,552 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,267,400 | 1408.20 | 998.29 KB |
| Dekaf | 1 | 1,195,093 | 1327.86 | 1009.87 KB |
| Dekaf (3conn) | 1 | 1,409,862 | 1566.50 | 915.26 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:22.1726208+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 431,804 msg/s |
| Dekaf | 2026-08-04T16:31:49.1849891+00:00 | 1 | 16.0 MiB / 15.8 MiB | 1603.1 MB/s | 0/0 | 38,832 | 27.0s / 1,399,924 msg/s |
| Dekaf | 2026-08-04T16:32:17.1899463+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1603.1 MB/s | 1/0 | 82,291 | 55.0s / 1,164,700 msg/s |
| Dekaf | 2026-08-04T16:32:44.1984184+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1603.1 MB/s | 2/0 | 121,741 | 82.0s / 1,386,026 msg/s |
| Dekaf | 2026-08-04T16:33:11.207151+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1603.1 MB/s | 2/1 | 148,613 | 109.0s / 1,281,319 msg/s |
| Dekaf | 2026-08-04T16:33:38.2111623+00:00 | 1 | 12.0 MiB / 4.6 MiB | 1603.1 MB/s | 2/1 | 182,853 | 136.0s / 1,373,001 msg/s |
| Dekaf | 2026-08-04T16:34:06.2146092+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1603.1 MB/s | 2/2 | 214,643 | 164.1s / 1,470,396 msg/s |
| Dekaf | 2026-08-04T16:34:33.2239516+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1603.1 MB/s | 2/2 | 250,024 | 191.1s / 1,297,458 msg/s |
| Dekaf | 2026-08-04T16:35:00.2308107+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1603.1 MB/s | 2/2 | 285,065 | 218.1s / 1,492,322 msg/s |
| Dekaf | 2026-08-04T16:35:27.2417936+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1659.2 MB/s | 2/2 | 331,886 | 245.1s / 1,513,468 msg/s |
| Dekaf | 2026-08-04T16:35:55.248333+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1722.1 MB/s | 2/2 | 387,970 | 273.1s / 1,557,977 msg/s |
| Dekaf | 2026-08-04T16:36:22.2519524+00:00 | 1 | 13.0 MiB / 8.8 MiB | 1769.3 MB/s | 3/2 | 439,455 | 300.1s / 1,544,361 msg/s |
| Dekaf | 2026-08-04T16:36:49.2660949+00:00 | 1 | 13.0 MiB / 11.4 MiB | 1769.3 MB/s | 3/2 | 487,582 | 327.1s / 1,087,794 msg/s |
| Dekaf | 2026-08-04T16:37:16.2784491+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1769.3 MB/s | 4/2 | 530,347 | 354.1s / 1,424,516 msg/s |
| Dekaf | 2026-08-04T16:37:44.2839781+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1769.3 MB/s | 4/3 | 574,589 | 382.1s / 1,091,983 msg/s |
| Dekaf | 2026-08-04T16:38:11.2887823+00:00 | 1 | 14.0 MiB / 8.9 MiB | 1769.3 MB/s | 4/3 | 603,558 | 409.1s / 1,204,055 msg/s |
| Dekaf | 2026-08-04T16:38:38.2958454+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1769.3 MB/s | 4/3 | 626,127 | 436.1s / 1,098,709 msg/s |
| Dekaf | 2026-08-04T16:39:05.3019116+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1769.3 MB/s | 5/3 | 665,661 | 463.2s / 1,415,214 msg/s |
| Dekaf | 2026-08-04T16:39:33.3130191+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1769.3 MB/s | 5/3 | 709,456 | 491.2s / 1,408,325 msg/s |
| Dekaf | 2026-08-04T16:40:00.3190016+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1769.3 MB/s | 5/4 | 756,276 | 518.2s / 1,429,401 msg/s |
| Dekaf | 2026-08-04T16:40:27.3256073+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1769.3 MB/s | 5/4 | 803,138 | 545.2s / 1,321,044 msg/s |
| Dekaf | 2026-08-04T16:40:55.3338231+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1769.3 MB/s | 5/4 | 857,849 | 573.2s / 1,421,511 msg/s |
| Dekaf | 2026-08-04T16:41:22.340326+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1769.3 MB/s | 5/5 | 911,882 | 600.2s / 1,495,315 msg/s |
| Dekaf | 2026-08-04T16:41:49.3466386+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1769.3 MB/s | 5/5 | 961,446 | 627.2s / 1,349,694 msg/s |
| Dekaf | 2026-08-04T16:42:16.358277+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1769.3 MB/s | 5/5 | 1,004,939 | 654.2s / 1,302,686 msg/s |
| Dekaf | 2026-08-04T16:42:44.3675633+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1769.3 MB/s | 5/5 | 1,049,397 | 682.2s / 1,518,234 msg/s |
| Dekaf | 2026-08-04T16:43:11.3851557+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1769.3 MB/s | 5/6 | 1,101,122 | 709.2s / 1,336,429 msg/s |
| Dekaf | 2026-08-04T16:43:38.3960745+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1769.3 MB/s | 5/6 | 1,153,109 | 736.2s / 1,301,463 msg/s |
| Dekaf | 2026-08-04T16:44:05.3995314+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1769.3 MB/s | 5/6 | 1,206,976 | 763.2s / 1,453,720 msg/s |
| Dekaf | 2026-08-04T16:44:33.4099196+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1769.3 MB/s | 5/6 | 1,264,073 | 791.2s / 1,521,923 msg/s |
| Dekaf | 2026-08-04T16:45:00.4162623+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1769.3 MB/s | 5/6 | 1,319,139 | 818.2s / 1,511,761 msg/s |
| Dekaf | 2026-08-04T16:45:27.423507+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1769.3 MB/s | 5/6 | 1,370,419 | 845.3s / 1,437,624 msg/s |
| Dekaf | 2026-08-04T16:45:54.4294668+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1769.3 MB/s | 5/6 | 1,421,775 | 872.3s / 1,347,501 msg/s |
| Dekaf | 2026-08-04T16:46:23.1563552+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 451,225 msg/s |
| Dekaf | 2026-08-04T16:46:50.162622+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1524.7 MB/s | 0/0 | 41,241 | 27.0s / 1,268,649 msg/s |
| Dekaf | 2026-08-04T16:47:17.1735618+00:00 | 1 | 16.0 MiB / 15.2 MiB | 1732.9 MB/s | 0/1 | 92,770 | 54.0s / 1,664,024 msg/s |
| Dekaf | 2026-08-04T16:47:44.1903872+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1735.8 MB/s | 0/1 | 144,342 | 81.0s / 1,504,764 msg/s |
| Dekaf | 2026-08-04T16:48:12.209008+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1738.9 MB/s | 0/1 | 200,345 | 109.1s / 1,394,928 msg/s |
| Dekaf | 2026-08-04T16:48:39.2218923+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1738.9 MB/s | 1/1 | 259,843 | 136.1s / 1,486,875 msg/s |
| Dekaf | 2026-08-04T16:49:06.2319118+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1738.9 MB/s | 1/1 | 322,243 | 163.1s / 1,394,174 msg/s |
| Dekaf | 2026-08-04T16:49:34.240423+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.9 MB/s | 2/1 | 391,339 | 191.1s / 1,550,269 msg/s |
| Dekaf | 2026-08-04T16:50:01.2510425+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.9 MB/s | 2/2 | 459,703 | 218.1s / 1,495,996 msg/s |
| Dekaf | 2026-08-04T16:50:28.2575347+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.9 MB/s | 2/2 | 527,098 | 245.1s / 1,480,403 msg/s |
| Dekaf | 2026-08-04T16:50:55.2669293+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1738.9 MB/s | 2/2 | 591,620 | 272.1s / 1,368,650 msg/s |
| Dekaf | 2026-08-04T16:51:23.2755453+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.9 MB/s | 2/3 | 659,611 | 300.1s / 1,467,175 msg/s |
| Dekaf | 2026-08-04T16:51:50.2805403+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.9 MB/s | 2/3 | 726,724 | 327.1s / 1,538,089 msg/s |
| Dekaf | 2026-08-04T16:52:17.2892627+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.9 MB/s | 2/3 | 789,678 | 354.1s / 1,589,463 msg/s |
| Dekaf | 2026-08-04T16:52:44.3028757+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1738.9 MB/s | 2/3 | 851,890 | 381.1s / 1,412,917 msg/s |
| Dekaf | 2026-08-04T16:53:12.3105222+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1738.9 MB/s | 2/3 | 921,661 | 409.1s / 1,436,164 msg/s |
| Dekaf | 2026-08-04T16:53:39.3130729+00:00 | 1 | 10.0 MiB / 5.8 MiB | 1741.8 MB/s | 3/3 | 989,497 | 436.1s / 1,552,622 msg/s |
| Dekaf | 2026-08-04T16:54:06.3162679+00:00 | 1 | 8.0 MiB / 7.7 MiB | 1741.8 MB/s | 3/3 | 1,048,235 | 463.1s / 1,337,897 msg/s |
| Dekaf | 2026-08-04T16:54:33.3180927+00:00 | 1 | 8.0 MiB / 8.0 MiB | 1741.8 MB/s | 4/3 | 1,082,902 | 490.2s / 1,314,193 msg/s |
| Dekaf | 2026-08-04T16:55:01.3218714+00:00 | 1 | 8.0 MiB / 5.2 MiB | 1741.8 MB/s | 4/4 | 1,114,667 | 518.2s / 1,312,633 msg/s |
| Dekaf | 2026-08-04T16:55:28.3280001+00:00 | 1 | 8.0 MiB / 8.0 MiB | 1741.8 MB/s | 4/4 | 1,145,245 | 545.2s / 1,155,276 msg/s |
| Dekaf | 2026-08-04T16:55:55.3317183+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1741.8 MB/s | 4/4 | 1,189,730 | 572.2s / 1,499,310 msg/s |
| Dekaf | 2026-08-04T16:56:22.340973+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1741.8 MB/s | 5/4 | 1,224,628 | 599.2s / 1,409,512 msg/s |
| Dekaf | 2026-08-04T16:56:50.3484006+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1741.8 MB/s | 5/4 | 1,269,738 | 627.2s / 1,523,953 msg/s |
| Dekaf | 2026-08-04T16:57:17.3540605+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1741.8 MB/s | 6/4 | 1,318,221 | 654.2s / 1,387,817 msg/s |
| Dekaf | 2026-08-04T16:57:44.3607676+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/4 | 1,381,426 | 681.2s / 1,420,355 msg/s |
| Dekaf | 2026-08-04T16:58:11.3740974+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 7/4 | 1,443,953 | 708.2s / 1,521,743 msg/s |
| Dekaf | 2026-08-04T16:58:39.3867371+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/5 | 1,513,657 | 736.2s / 1,537,974 msg/s |
| Dekaf | 2026-08-04T16:59:06.3931328+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/5 | 1,584,872 | 763.2s / 1,475,770 msg/s |
| Dekaf | 2026-08-04T16:59:33.3977981+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/5 | 1,646,848 | 790.2s / 1,386,643 msg/s |
| Dekaf | 2026-08-04T17:00:01.4034111+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1741.8 MB/s | 7/6 | 1,708,212 | 818.2s / 1,415,122 msg/s |
| Dekaf | 2026-08-04T17:00:28.4109924+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/6 | 1,768,280 | 845.2s / 1,480,955 msg/s |
| Dekaf | 2026-08-04T17:00:55.4159203+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/6 | 1,827,763 | 872.2s / 1,303,421 msg/s |
| Dekaf | 2026-08-04T17:01:22.4208162+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1741.8 MB/s | 7/6 | 1,883,834 | 899.2s / 1,216,160 msg/s |
| Dekaf (3conn) | 2026-08-04T17:16:51.4237992+00:00 | 1 | 16.0 MiB / 12.3 MiB | 1876.6 MB/s | 0/0 | 1,245 | 27.0s / 1,492,211 msg/s |
| Dekaf (3conn) | 2026-08-04T17:17:18.4312775+00:00 | 1 | 14.0 MiB / 8.4 MiB | 1876.6 MB/s | 1/0 | 3,201 | 54.0s / 1,391,384 msg/s |
| Dekaf (3conn) | 2026-08-04T17:17:45.4350569+00:00 | 1 | 14.0 MiB / 7.4 MiB | 1876.6 MB/s | 1/0 | 6,195 | 81.0s / 1,326,260 msg/s |
| Dekaf (3conn) | 2026-08-04T17:18:12.4472728+00:00 | 1 | 12.0 MiB / 3.4 MiB | 1876.6 MB/s | 2/0 | 10,093 | 108.0s / 1,344,929 msg/s |
| Dekaf (3conn) | 2026-08-04T17:18:40.4578018+00:00 | 1 | 12.0 MiB / 6.3 MiB | 1876.6 MB/s | 2/1 | 15,340 | 136.1s / 1,535,021 msg/s |
| Dekaf (3conn) | 2026-08-04T17:19:07.4680318+00:00 | 1 | 12.0 MiB / 5.5 MiB | 1886.2 MB/s | 2/1 | 21,522 | 163.1s / 1,566,721 msg/s |
| Dekaf (3conn) | 2026-08-04T17:19:34.475824+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1899.2 MB/s | 2/1 | 29,165 | 190.1s / 1,515,709 msg/s |
| Dekaf (3conn) | 2026-08-04T17:20:01.4943523+00:00 | 1 | 12.0 MiB / 8.7 MiB | 1899.2 MB/s | 2/2 | 35,764 | 217.1s / 1,590,676 msg/s |
| Dekaf (3conn) | 2026-08-04T17:20:29.5165206+00:00 | 1 | 12.0 MiB / 6.3 MiB | 1899.2 MB/s | 2/2 | 43,047 | 245.1s / 1,543,026 msg/s |
| Dekaf (3conn) | 2026-08-04T17:20:56.535719+00:00 | 1 | 12.0 MiB / 5.1 MiB | 1956.3 MB/s | 2/2 | 50,730 | 272.1s / 1,696,456 msg/s |
| Dekaf (3conn) | 2026-08-04T17:21:23.5478293+00:00 | 1 | 12.0 MiB / 6.8 MiB | 1956.3 MB/s | 2/2 | 58,685 | 299.1s / 1,612,877 msg/s |
| Dekaf (3conn) | 2026-08-04T17:21:50.5618329+00:00 | 1 | 12.0 MiB / 6.4 MiB | 1956.3 MB/s | 2/2 | 66,137 | 326.2s / 1,387,600 msg/s |
| Dekaf (3conn) | 2026-08-04T17:22:18.5720477+00:00 | 1 | 12.0 MiB / 2.9 MiB | 1956.3 MB/s | 2/3 | 72,881 | 354.2s / 1,665,520 msg/s |
| Dekaf (3conn) | 2026-08-04T17:22:45.5839212+00:00 | 1 | 12.0 MiB / 5.6 MiB | 1956.3 MB/s | 2/3 | 77,115 | 381.2s / 1,352,738 msg/s |
| Dekaf (3conn) | 2026-08-04T17:23:12.5958847+00:00 | 1 | 12.0 MiB / 1.8 MiB | 1956.3 MB/s | 2/3 | 81,989 | 408.2s / 1,446,183 msg/s |
| Dekaf (3conn) | 2026-08-04T17:23:40.6089615+00:00 | 1 | 12.0 MiB / 8.4 MiB | 1956.3 MB/s | 2/3 | 86,638 | 436.2s / 1,455,073 msg/s |
| Dekaf (3conn) | 2026-08-04T17:24:07.6185332+00:00 | 1 | 12.0 MiB / 9.3 MiB | 2217.8 MB/s | 2/3 | 94,484 | 463.2s / 1,704,157 msg/s |
| Dekaf (3conn) | 2026-08-04T17:24:34.6301156+00:00 | 1 | 12.0 MiB / 12.0 MiB | 2217.8 MB/s | 2/3 | 104,829 | 490.2s / 1,790,543 msg/s |
| Dekaf (3conn) | 2026-08-04T17:25:01.646028+00:00 | 1 | 12.0 MiB / 2.5 MiB | 2217.8 MB/s | 2/3 | 113,984 | 517.2s / 1,674,509 msg/s |
| Dekaf (3conn) | 2026-08-04T17:25:29.6649923+00:00 | 1 | 12.0 MiB / 11.3 MiB | 2217.8 MB/s | 2/3 | 122,941 | 545.3s / 1,261,622 msg/s |
| Dekaf (3conn) | 2026-08-04T17:25:56.685321+00:00 | 1 | 12.0 MiB / 3.8 MiB | 2217.8 MB/s | 2/3 | 129,225 | 572.3s / 1,355,168 msg/s |
| Dekaf (3conn) | 2026-08-04T17:26:23.6912548+00:00 | 1 | 10.0 MiB / 8.6 MiB | 2217.8 MB/s | 2/3 | 138,325 | 599.3s / 1,471,882 msg/s |
| Dekaf (3conn) | 2026-08-04T17:26:50.7070791+00:00 | 1 | 12.0 MiB / 7.6 MiB | 2217.8 MB/s | 2/4 | 143,166 | 626.3s / 1,140,756 msg/s |
| Dekaf (3conn) | 2026-08-04T17:27:18.7158376+00:00 | 1 | 12.0 MiB / 5.2 MiB | 2217.8 MB/s | 2/4 | 148,023 | 654.3s / 1,561,577 msg/s |
| Dekaf (3conn) | 2026-08-04T17:27:45.7249232+00:00 | 1 | 12.0 MiB / 7.9 MiB | 2217.8 MB/s | 2/4 | 155,204 | 681.3s / 1,664,508 msg/s |
| Dekaf (3conn) | 2026-08-04T17:28:12.7383398+00:00 | 1 | 12.0 MiB / 6.7 MiB | 2217.8 MB/s | 2/4 | 161,789 | 708.3s / 1,387,322 msg/s |
| Dekaf (3conn) | 2026-08-04T17:28:39.7508437+00:00 | 1 | 12.0 MiB / 8.3 MiB | 2217.8 MB/s | 2/4 | 165,112 | 735.4s / 1,467,620 msg/s |
| Dekaf (3conn) | 2026-08-04T17:29:07.7711162+00:00 | 1 | 12.0 MiB / 11.8 MiB | 2217.8 MB/s | 2/4 | 170,443 | 763.4s / 1,373,402 msg/s |
| Dekaf (3conn) | 2026-08-04T17:29:34.7820043+00:00 | 1 | 12.0 MiB / 12.0 MiB | 2217.8 MB/s | 2/4 | 176,405 | 790.4s / 1,324,572 msg/s |
| Dekaf (3conn) | 2026-08-04T17:30:01.7988363+00:00 | 1 | 12.0 MiB / 4.8 MiB | 2217.8 MB/s | 2/4 | 180,951 | 817.4s / 1,252,300 msg/s |
| Dekaf (3conn) | 2026-08-04T17:30:28.8178192+00:00 | 1 | 13.0 MiB / 5.4 MiB | 2217.8 MB/s | 2/4 | 184,655 | 844.4s / 1,323,471 msg/s |
| Dekaf (3conn) | 2026-08-04T17:30:56.8298226+00:00 | 1 | 12.0 MiB / 8.1 MiB | 2217.8 MB/s | 2/5 | 188,905 | 872.4s / 1,275,675 msg/s |
| Dekaf (3conn) | 2026-08-04T17:31:23.8398423+00:00 | 1 | 12.0 MiB / 7.0 MiB | 2217.8 MB/s | 2/5 | 193,207 | 899.4s / 1,101,641 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-04T16:31:52.2716939+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.7 MiB |
| Dekaf | 2026-08-04T16:32:07.2809104+00:00 | 1 | capacity | succeeded | 15,009ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-08-04T16:32:10.2826959+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:32:25.2980843+00:00 | 1 | capacity | succeeded | 15,015ms | 12.0 MiB / 10.6 MiB |
| Dekaf | 2026-08-04T16:32:55.3219411+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:33:10.3539152+00:00 | 1 | capacity | failed | 15,031ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:33:40.3854675+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:33:55.4030053+00:00 | 1 | capacity | failed | 15,017ms | 12.0 MiB / 9.0 MiB |
| Dekaf | 2026-08-04T16:35:55.5036245+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:36:10.5156569+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:36:40.5408885+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.9 MiB |
| Dekaf | 2026-08-04T16:36:55.5535512+00:00 | 1 | capacity | succeeded | 15,012ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:37:25.5758573+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.0 MiB |
| Dekaf | 2026-08-04T16:37:40.5889872+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-04T16:38:40.6490193+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-04T16:38:55.6601294+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-04T16:39:25.6892329+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:39:40.7071253+00:00 | 1 | capacity | failed | 15,017ms | 12.0 MiB / 2.0 MiB |
| Dekaf | 2026-08-04T16:40:40.7612605+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:40:55.7743509+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-04T16:42:55.9034225+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 12.0 MiB |
| Dekaf | 2026-08-04T16:43:10.9120149+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-04T16:46:53.2570387+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-04T16:47:08.2668961+00:00 | 1 | capacity | failed | 15,009ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:48:08.3424921+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-04T16:48:23.3585713+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-08-04T16:48:53.3876785+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-04T16:49:08.4007851+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:49:38.4219367+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:49:53.4350288+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 7.9 MiB |
| Dekaf | 2026-08-04T16:50:53.4840808+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.4 MiB |
| Dekaf | 2026-08-04T16:51:08.4963545+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 12.6 MiB |
| Dekaf | 2026-08-04T16:53:08.5951737+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:53:23.6068781+00:00 | 1 | capacity | succeeded | 15,011ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:53:53.6262088+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:54:08.641258+00:00 | 1 | capacity | succeeded | 15,015ms | 8.0 MiB / 6.5 MiB |
| Dekaf | 2026-08-04T16:54:38.669069+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-04T16:54:53.6822117+00:00 | 1 | capacity | failed | 15,013ms | 8.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-04T16:55:53.7374973+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.7 MiB |
| Dekaf | 2026-08-04T16:56:08.7514098+00:00 | 1 | capacity | succeeded | 15,013ms | 9.0 MiB / 8.0 MiB |
| Dekaf | 2026-08-04T16:56:38.7816665+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 8.2 MiB |
| Dekaf | 2026-08-04T16:56:53.793031+00:00 | 1 | capacity | succeeded | 15,011ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-04T16:57:23.8167357+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 9.6 MiB |
| Dekaf | 2026-08-04T16:57:38.8460826+00:00 | 1 | capacity | succeeded | 15,029ms | 11.0 MiB / 9.6 MiB |
| Dekaf | 2026-08-04T16:58:08.8839036+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 9.7 MiB |
| Dekaf | 2026-08-04T16:58:23.8960138+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-04T16:59:23.936353+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.4 MiB |
| Dekaf | 2026-08-04T16:59:38.9512461+00:00 | 1 | capacity | failed | 15,015ms | 11.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-08-04T17:16:54.5444522+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 9.5 MiB |
| Dekaf (3conn) | 2026-08-04T17:17:09.5676835+00:00 | 1 | capacity | succeeded | 15,023ms | 14.0 MiB / 7.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:17:39.6052237+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-08-04T17:17:54.6274816+00:00 | 1 | capacity | succeeded | 15,022ms | 12.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:18:24.6751513+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:18:39.6994481+00:00 | 1 | capacity | failed | 15,024ms | 12.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-08-04T17:19:39.7877178+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-08-04T17:19:54.8063922+00:00 | 1 | capacity | failed | 15,018ms | 12.0 MiB / 12.4 MiB |
| Dekaf (3conn) | 2026-08-04T17:21:55.0051577+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:22:10.0313334+00:00 | 1 | capacity | failed | 15,025ms | 12.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:26:10.4273116+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:26:25.457086+00:00 | 1 | capacity | failed | 15,029ms | 12.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-04T17:30:25.8309283+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-08-04T17:30:40.879301+00:00 | 1 | capacity | failed | 15,048ms | 12.0 MiB / 11.1 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,451 |
| Dekaf | 1 | 0.002–0.004ms | 1,723 |
| Dekaf | 1 | 0.004–0.008ms | 6,110 |
| Dekaf | 1 | 0.008–0.016ms | 35,367 |
| Dekaf | 1 | 0.016–0.032ms | 47,285 |
| Dekaf | 1 | 0.032–0.064ms | 45,954 |
| Dekaf | 1 | 0.064–0.128ms | 91,022 |
| Dekaf | 1 | 0.128–0.256ms | 235,751 |
| Dekaf | 1 | 0.256–0.512ms | 341,571 |
| Dekaf | 1 | 0.512–1.024ms | 75,835 |
| Dekaf | 1 | 1.024–2.048ms | 6,464 |
| Dekaf | 1 | 2.048–4.096ms | 4,902 |
| Dekaf | 1 | 4.096–8.192ms | 1,131 |
| Dekaf | 1 | 8.192–16.384ms | 133 |
| Dekaf | 1 | 16.384–32.768ms | 6 |
| Dekaf | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 85 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 79 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 189 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 603 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 2,126 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 7,386 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 6,197 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 8,821 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 10,817 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 10,966 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 7,515 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 2,059 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 398 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 31 |
| Dekaf | 1 | 0.001–0.002ms | 1,866 |
| Dekaf | 1 | 0.002–0.004ms | 2,157 |
| Dekaf | 1 | 0.004–0.008ms | 5,521 |
| Dekaf | 1 | 0.008–0.016ms | 24,165 |
| Dekaf | 1 | 0.016–0.032ms | 42,276 |
| Dekaf | 1 | 0.032–0.064ms | 48,428 |
| Dekaf | 1 | 0.064–0.128ms | 87,030 |
| Dekaf | 1 | 0.128–0.256ms | 200,092 |
| Dekaf | 1 | 0.256–0.512ms | 239,764 |
| Dekaf | 1 | 0.512–1.024ms | 52,656 |
| Dekaf | 1 | 1.024–2.048ms | 4,374 |
| Dekaf | 1 | 2.048–4.096ms | 4,830 |
| Dekaf | 1 | 4.096–8.192ms | 1,067 |
| Dekaf | 1 | 8.192–16.384ms | 132 |
| Dekaf | 1 | 16.384–32.768ms | 8 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 770,598,000 | 2026-08-04T16:27:12.2720736+00:00 | 100.7ms | GC pause | - | - | 650.5s / 1,054,067 msg/s | Gen2 +0 / pause +87.1ms |
| Confluent | 771,011,000 | 2026-08-04T16:27:12.6443978+00:00 | 104.7ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,048,000 | 2026-08-04T16:27:12.6745989+00:00 | 120.7ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,077,000 | 2026-08-04T16:27:12.6993304+00:00 | 129.0ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,115,000 | 2026-08-04T16:27:12.7245912+00:00 | 105.3ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,129,000 | 2026-08-04T16:27:12.7323431+00:00 | 109.0ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,131,000 | 2026-08-04T16:27:12.7333149+00:00 | 155.7ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,172,000 | 2026-08-04T16:27:12.7653178+00:00 | 124.0ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 771,191,000 | 2026-08-04T16:27:12.8061337+00:00 | 157.9ms | GC pause | - | - | 651.5s / 971,965 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 792,476,000 | 2026-08-04T16:27:36.3575376+00:00 | 119.8ms | GC pause | - | - | 674.5s / 983,304 msg/s | Gen2 +0 / pause +93.4ms |
| Confluent | 792,479,000 | 2026-08-04T16:27:36.359748+00:00 | 122.5ms | GC pause | - | - | 674.5s / 983,304 msg/s | Gen2 +0 / pause +93.4ms |
| Confluent | 792,494,000 | 2026-08-04T16:27:36.3680966+00:00 | 123.5ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 792,498,000 | 2026-08-04T16:27:36.3721682+00:00 | 138.9ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 792,504,000 | 2026-08-04T16:27:36.3779056+00:00 | 129.9ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 792,510,000 | 2026-08-04T16:27:36.3867468+00:00 | 130.9ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 792,553,000 | 2026-08-04T16:27:36.4317194+00:00 | 142.4ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +194.5ms |
| Confluent | 792,637,000 | 2026-08-04T16:27:36.5480459+00:00 | 111.0ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 792,758,000 | 2026-08-04T16:27:36.6718789+00:00 | 134.0ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 792,764,000 | 2026-08-04T16:27:36.6779215+00:00 | 118.5ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 792,767,000 | 2026-08-04T16:27:36.6805417+00:00 | 141.8ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 792,798,000 | 2026-08-04T16:27:36.7312183+00:00 | 114.2ms | GC pause | - | - | 675.5s / 798,374 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 794,280,000 | 2026-08-04T16:27:38.5116504+00:00 | 102.4ms | GC pause | - | - | 677.5s / 742,507 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 798,151,000 | 2026-08-04T16:27:43.3656557+00:00 | 137.7ms | GC pause | - | - | 682.5s / 752,420 msg/s | Gen2 +0 / pause +191.2ms |
| Confluent | 799,797,000 | 2026-08-04T16:27:45.253087+00:00 | 109.8ms | GC pause | - | - | 683.5s / 1,064,254 msg/s | Gen2 +0 / pause +84.6ms |
| Confluent | 799,806,000 | 2026-08-04T16:27:45.2611277+00:00 | 101.2ms | GC pause | - | - | 683.5s / 1,064,254 msg/s | Gen2 +0 / pause +84.6ms |
| Confluent | 799,894,000 | 2026-08-04T16:27:45.3395732+00:00 | 118.9ms | GC pause | - | - | 683.5s / 1,064,254 msg/s | Gen2 +0 / pause +84.6ms |
| Confluent | 800,088,000 | 2026-08-04T16:27:45.5512764+00:00 | 101.1ms | GC pause | - | - | 684.5s / 963,261 msg/s | Gen2 +0 / pause +59.9ms |
| Confluent | 801,854,000 | 2026-08-04T16:27:47.39995+00:00 | 104.0ms | GC pause | - | - | 686.5s / 918,772 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 801,855,000 | 2026-08-04T16:27:47.4009976+00:00 | 102.1ms | GC pause | - | - | 686.5s / 918,772 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 802,737,000 | 2026-08-04T16:27:48.4099165+00:00 | 100.2ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +195.1ms |
| Confluent | 802,793,000 | 2026-08-04T16:27:48.4565113+00:00 | 111.2ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +195.1ms |
| Confluent | 802,813,000 | 2026-08-04T16:27:48.4777141+00:00 | 107.5ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +195.1ms |
| Confluent | 802,816,000 | 2026-08-04T16:27:48.4801209+00:00 | 106.3ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +195.1ms |
| Confluent | 802,856,000 | 2026-08-04T16:27:48.5144351+00:00 | 107.0ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 802,880,000 | 2026-08-04T16:27:48.535221+00:00 | 105.6ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 802,898,000 | 2026-08-04T16:27:48.5705188+00:00 | 122.6ms | GC pause | - | - | 687.5s / 693,215 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 810,497,000 | 2026-08-04T16:27:58.2713033+00:00 | 101.5ms | GC pause | - | - | 696.5s / 828,933 msg/s | Gen2 +0 / pause +91.6ms |
| Confluent | 811,252,000 | 2026-08-04T16:27:59.2049348+00:00 | 102.0ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,272,000 | 2026-08-04T16:27:59.229286+00:00 | 108.6ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,321,000 | 2026-08-04T16:27:59.2619714+00:00 | 158.1ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,339,000 | 2026-08-04T16:27:59.2737425+00:00 | 140.6ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,350,000 | 2026-08-04T16:27:59.2807669+00:00 | 160.9ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,367,000 | 2026-08-04T16:27:59.2940212+00:00 | 167.3ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,371,000 | 2026-08-04T16:27:59.2971001+00:00 | 164.3ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,386,000 | 2026-08-04T16:27:59.3140906+00:00 | 141.7ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,419,000 | 2026-08-04T16:27:59.3455069+00:00 | 147.3ms | GC pause | - | - | 697.5s / 859,593 msg/s | Gen2 +0 / pause +91.8ms |
| Confluent | 811,430,000 | 2026-08-04T16:27:59.3697362+00:00 | 152.6ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +200.5ms |
| Confluent | 811,454,000 | 2026-08-04T16:27:59.4025323+00:00 | 132.5ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +200.5ms |
| Confluent | 811,459,000 | 2026-08-04T16:27:59.4094755+00:00 | 133.9ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +200.5ms |
| Confluent | 811,503,000 | 2026-08-04T16:27:59.4714456+00:00 | 130.6ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +200.5ms |
| Confluent | 811,565,000 | 2026-08-04T16:27:59.5226414+00:00 | 120.5ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,613,000 | 2026-08-04T16:27:59.5666645+00:00 | 140.4ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,616,000 | 2026-08-04T16:27:59.572436+00:00 | 117.9ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,699,000 | 2026-08-04T16:27:59.6468583+00:00 | 133.2ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,713,000 | 2026-08-04T16:27:59.6603703+00:00 | 151.1ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,716,000 | 2026-08-04T16:27:59.6630039+00:00 | 129.6ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,724,000 | 2026-08-04T16:27:59.6719368+00:00 | 129.1ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,738,000 | 2026-08-04T16:27:59.6914334+00:00 | 166.0ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,750,000 | 2026-08-04T16:27:59.7082133+00:00 | 150.2ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,759,000 | 2026-08-04T16:27:59.7195848+00:00 | 131.4ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,762,000 | 2026-08-04T16:27:59.7216921+00:00 | 136.4ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,781,000 | 2026-08-04T16:27:59.7391078+00:00 | 161.5ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,787,000 | 2026-08-04T16:27:59.75179+00:00 | 152.3ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 811,800,000 | 2026-08-04T16:27:59.7831542+00:00 | 122.0ms | GC pause | - | - | 698.5s / 713,405 msg/s | Gen2 +0 / pause +108.7ms |
| Confluent | 812,337,000 | 2026-08-04T16:28:00.61477+00:00 | 102.6ms | GC pause | - | - | 699.5s / 749,426 msg/s | Gen2 +0 / pause +135.0ms |
| Confluent | 812,968,000 | 2026-08-04T16:28:01.4709702+00:00 | 117.6ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +251.7ms |
| Confluent | 813,110,000 | 2026-08-04T16:28:01.6083847+00:00 | 124.8ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,205,000 | 2026-08-04T16:28:01.692486+00:00 | 118.0ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,300,000 | 2026-08-04T16:28:01.793707+00:00 | 115.6ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,327,000 | 2026-08-04T16:28:01.8148476+00:00 | 117.8ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,339,000 | 2026-08-04T16:28:01.8259871+00:00 | 102.2ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,345,000 | 2026-08-04T16:28:01.8293292+00:00 | 102.7ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,347,000 | 2026-08-04T16:28:01.830333+00:00 | 118.5ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,356,000 | 2026-08-04T16:28:01.8357993+00:00 | 105.1ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,361,000 | 2026-08-04T16:28:01.8391735+00:00 | 124.8ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,526,000 | 2026-08-04T16:28:02.0005038+00:00 | 108.2ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,568,000 | 2026-08-04T16:28:02.0286896+00:00 | 184.3ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,588,000 | 2026-08-04T16:28:02.0430825+00:00 | 193.2ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,625,000 | 2026-08-04T16:28:02.1207651+00:00 | 137.3ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,626,000 | 2026-08-04T16:28:02.123515+00:00 | 134.6ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,673,000 | 2026-08-04T16:28:02.2147164+00:00 | 107.6ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 813,711,000 | 2026-08-04T16:28:02.3098008+00:00 | 113.4ms | GC pause | - | - | 700.5s / 812,254 msg/s | Gen2 +0 / pause +116.7ms |
| Confluent | 816,027,000 | 2026-08-04T16:28:05.3529889+00:00 | 116.4ms | GC pause | - | - | 703.5s / 826,898 msg/s | Gen2 +0 / pause +96.0ms |
| Confluent | 816,030,000 | 2026-08-04T16:28:05.3551288+00:00 | 116.9ms | GC pause | - | - | 703.5s / 826,898 msg/s | Gen2 +0 / pause +96.0ms |
| Confluent | 816,126,000 | 2026-08-04T16:28:05.4474942+00:00 | 153.2ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +214.3ms |
| Confluent | 816,147,000 | 2026-08-04T16:28:05.470074+00:00 | 157.1ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +214.3ms |
| Confluent | 816,157,000 | 2026-08-04T16:28:05.4792715+00:00 | 158.4ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +214.3ms |
| Confluent | 816,160,000 | 2026-08-04T16:28:05.4837616+00:00 | 159.1ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +214.3ms |
| Confluent | 816,182,000 | 2026-08-04T16:28:05.5096043+00:00 | 138.8ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +214.3ms |
| Confluent | 816,199,000 | 2026-08-04T16:28:05.5348178+00:00 | 130.6ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +118.3ms |
| Confluent | 816,231,000 | 2026-08-04T16:28:05.5828558+00:00 | 133.1ms | GC pause | - | - | 704.5s / 620,087 msg/s | Gen2 +0 / pause +118.3ms |
| Confluent | 819,886,000 | 2026-08-04T16:28:10.7420663+00:00 | 105.8ms | GC pause | - | - | 709.5s / 746,887 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 820,841,000 | 2026-08-04T16:28:11.9562997+00:00 | 102.8ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,000,000 | 2026-08-04T16:28:12.131133+00:00 | 105.1ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,057,000 | 2026-08-04T16:28:12.1985811+00:00 | 125.0ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,070,000 | 2026-08-04T16:28:12.2141432+00:00 | 118.5ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,083,000 | 2026-08-04T16:28:12.2275344+00:00 | 129.7ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,092,000 | 2026-08-04T16:28:12.2409241+00:00 | 115.7ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,119,000 | 2026-08-04T16:28:12.2736631+00:00 | 126.4ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,147,000 | 2026-08-04T16:28:12.3132211+00:00 | 160.5ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,151,000 | 2026-08-04T16:28:12.3212592+00:00 | 152.7ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,162,000 | 2026-08-04T16:28:12.335707+00:00 | 114.3ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,182,000 | 2026-08-04T16:28:12.3604473+00:00 | 124.2ms | GC pause | - | - | 710.5s / 842,572 msg/s | Gen2 +0 / pause +84.7ms |
| Confluent | 821,213,000 | 2026-08-04T16:28:12.4124586+00:00 | 123.5ms | GC pause | - | - | 711.5s / 745,445 msg/s | Gen2 +0 / pause +247.9ms |
| Confluent | 829,032,000 | 2026-08-04T16:28:23.0243888+00:00 | 110.6ms | GC pause | - | - | 721.5s / 842,980 msg/s | Gen2 +0 / pause +136.6ms |
| Confluent | 829,035,000 | 2026-08-04T16:28:23.0265765+00:00 | 123.2ms | GC pause | - | - | 721.5s / 842,980 msg/s | Gen2 +0 / pause +136.6ms |
| Confluent | 829,078,000 | 2026-08-04T16:28:23.0955209+00:00 | 104.0ms | GC pause | - | - | 721.5s / 842,980 msg/s | Gen2 +0 / pause +136.6ms |
| Confluent | 830,234,000 | 2026-08-04T16:28:24.8682473+00:00 | 100.2ms | GC pause | - | - | 723.5s / 696,710 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 830,300,000 | 2026-08-04T16:28:24.9532827+00:00 | 109.7ms | GC pause | - | - | 723.5s / 696,710 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 830,341,000 | 2026-08-04T16:28:25.0095855+00:00 | 109.8ms | GC pause | - | - | 723.5s / 696,710 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 830,953,000 | 2026-08-04T16:28:25.7570441+00:00 | 124.8ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 830,994,000 | 2026-08-04T16:28:25.7885128+00:00 | 135.0ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,010,000 | 2026-08-04T16:28:25.8010771+00:00 | 139.0ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,051,000 | 2026-08-04T16:28:25.834326+00:00 | 170.6ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,078,000 | 2026-08-04T16:28:25.854365+00:00 | 192.7ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,092,000 | 2026-08-04T16:28:25.8658772+00:00 | 176.2ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,116,000 | 2026-08-04T16:28:25.884744+00:00 | 179.0ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,127,000 | 2026-08-04T16:28:25.8993595+00:00 | 197.8ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,143,000 | 2026-08-04T16:28:25.9232002+00:00 | 165.7ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,147,000 | 2026-08-04T16:28:25.9301652+00:00 | 188.2ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,160,000 | 2026-08-04T16:28:25.9498809+00:00 | 153.9ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,165,000 | 2026-08-04T16:28:25.9586633+00:00 | 158.8ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,169,000 | 2026-08-04T16:28:25.9615552+00:00 | 156.0ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,190,000 | 2026-08-04T16:28:25.9859177+00:00 | 151.5ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,276,000 | 2026-08-04T16:28:26.0848718+00:00 | 163.4ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,337,000 | 2026-08-04T16:28:26.1534959+00:00 | 209.9ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,365,000 | 2026-08-04T16:28:26.1846131+00:00 | 177.6ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,379,000 | 2026-08-04T16:28:26.1991268+00:00 | 183.3ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,380,000 | 2026-08-04T16:28:26.1996298+00:00 | 173.2ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,406,000 | 2026-08-04T16:28:26.2252035+00:00 | 186.4ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,433,000 | 2026-08-04T16:28:26.2676259+00:00 | 169.7ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,491,000 | 2026-08-04T16:28:26.3330996+00:00 | 195.2ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,518,000 | 2026-08-04T16:28:26.3582763+00:00 | 218.6ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,521,000 | 2026-08-04T16:28:26.3601418+00:00 | 218.2ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,536,000 | 2026-08-04T16:28:26.3710441+00:00 | 178.8ms | GC pause | - | - | 724.5s / 1,021,545 msg/s | Gen2 +0 / pause +86.4ms |
| Confluent | 831,553,000 | 2026-08-04T16:28:26.3831002+00:00 | 182.8ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,559,000 | 2026-08-04T16:28:26.3884636+00:00 | 195.8ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,579,000 | 2026-08-04T16:28:26.4022082+00:00 | 211.3ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,594,000 | 2026-08-04T16:28:26.4119901+00:00 | 216.9ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,631,000 | 2026-08-04T16:28:26.4593483+00:00 | 245.8ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,681,000 | 2026-08-04T16:28:26.5251599+00:00 | 248.0ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,683,000 | 2026-08-04T16:28:26.526998+00:00 | 194.2ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,685,000 | 2026-08-04T16:28:26.5292818+00:00 | 198.4ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,701,000 | 2026-08-04T16:28:26.5473484+00:00 | 261.2ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +215.5ms |
| Confluent | 831,717,000 | 2026-08-04T16:28:26.5641672+00:00 | 277.7ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +129.0ms |
| Confluent | 831,725,000 | 2026-08-04T16:28:26.5710859+00:00 | 212.1ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +129.0ms |
| Confluent | 831,777,000 | 2026-08-04T16:28:26.6614043+00:00 | 243.4ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +129.0ms |
| Confluent | 831,838,000 | 2026-08-04T16:28:26.7868669+00:00 | 188.2ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +129.0ms |
| Confluent | 831,968,000 | 2026-08-04T16:28:27.0210844+00:00 | 119.0ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +129.0ms |
| Confluent | 831,997,000 | 2026-08-04T16:28:27.0548045+00:00 | 118.1ms | GC pause | - | - | 725.5s / 596,775 msg/s | Gen2 +0 / pause +129.0ms |
| Confluent | 834,790,000 | 2026-08-04T16:28:31.1046967+00:00 | 106.1ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,801,000 | 2026-08-04T16:28:31.1186888+00:00 | 108.1ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,846,000 | 2026-08-04T16:28:31.1564213+00:00 | 105.2ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,868,000 | 2026-08-04T16:28:31.1737574+00:00 | 119.8ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,905,000 | 2026-08-04T16:28:31.2070643+00:00 | 113.9ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,924,000 | 2026-08-04T16:28:31.2271305+00:00 | 125.3ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,979,000 | 2026-08-04T16:28:31.2790683+00:00 | 134.7ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,982,000 | 2026-08-04T16:28:31.2817454+00:00 | 129.1ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 834,989,000 | 2026-08-04T16:28:31.2882536+00:00 | 137.6ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 835,001,000 | 2026-08-04T16:28:31.2987588+00:00 | 146.5ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 835,025,000 | 2026-08-04T16:28:31.3149761+00:00 | 146.7ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 835,049,000 | 2026-08-04T16:28:31.3295466+00:00 | 157.0ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 835,086,000 | 2026-08-04T16:28:31.3545884+00:00 | 164.6ms | GC pause | - | - | 729.5s / 1,055,062 msg/s | Gen2 +0 / pause +74.9ms |
| Confluent | 835,145,000 | 2026-08-04T16:28:31.41237+00:00 | 165.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,151,000 | 2026-08-04T16:28:31.4178417+00:00 | 178.6ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,158,000 | 2026-08-04T16:28:31.4235371+00:00 | 180.7ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,173,000 | 2026-08-04T16:28:31.4360208+00:00 | 178.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,235,000 | 2026-08-04T16:28:31.4878578+00:00 | 180.9ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,265,000 | 2026-08-04T16:28:31.5295611+00:00 | 168.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,271,000 | 2026-08-04T16:28:31.5365803+00:00 | 188.0ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,274,000 | 2026-08-04T16:28:31.5396902+00:00 | 163.7ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,282,000 | 2026-08-04T16:28:31.5482658+00:00 | 152.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +155.3ms |
| Confluent | 835,327,000 | 2026-08-04T16:28:31.5834765+00:00 | 209.6ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,345,000 | 2026-08-04T16:28:31.6001429+00:00 | 201.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,356,000 | 2026-08-04T16:28:31.6099584+00:00 | 198.3ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,455,000 | 2026-08-04T16:28:31.72977+00:00 | 187.3ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,464,000 | 2026-08-04T16:28:31.745327+00:00 | 172.2ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,489,000 | 2026-08-04T16:28:31.7838239+00:00 | 173.5ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,529,000 | 2026-08-04T16:28:31.818591+00:00 | 179.6ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,542,000 | 2026-08-04T16:28:31.8282899+00:00 | 159.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,543,000 | 2026-08-04T16:28:31.8290649+00:00 | 182.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,554,000 | 2026-08-04T16:28:31.836513+00:00 | 184.2ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,557,000 | 2026-08-04T16:28:31.8389612+00:00 | 204.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,568,000 | 2026-08-04T16:28:31.8460634+00:00 | 209.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,584,000 | 2026-08-04T16:28:31.8556144+00:00 | 190.8ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,600,000 | 2026-08-04T16:28:31.8694151+00:00 | 188.3ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,609,000 | 2026-08-04T16:28:31.8791911+00:00 | 185.5ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,652,000 | 2026-08-04T16:28:31.921194+00:00 | 167.3ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,655,000 | 2026-08-04T16:28:31.923986+00:00 | 189.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,662,000 | 2026-08-04T16:28:31.9304623+00:00 | 172.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,669,000 | 2026-08-04T16:28:31.9374207+00:00 | 188.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,731,000 | 2026-08-04T16:28:32.0052083+00:00 | 209.8ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,766,000 | 2026-08-04T16:28:32.0359044+00:00 | 178.9ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,828,000 | 2026-08-04T16:28:32.1063997+00:00 | 214.4ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,860,000 | 2026-08-04T16:28:32.1383763+00:00 | 182.1ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,923,000 | 2026-08-04T16:28:32.1897222+00:00 | 215.8ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,934,000 | 2026-08-04T16:28:32.1983638+00:00 | 228.8ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,956,000 | 2026-08-04T16:28:32.2152853+00:00 | 220.8ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 835,971,000 | 2026-08-04T16:28:32.2271104+00:00 | 276.9ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 836,000,000 | 2026-08-04T16:28:32.2506713+00:00 | 247.0ms | GC pause | - | - | 730.5s / 905,946 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 836,126,000 | 2026-08-04T16:28:32.4126169+00:00 | 229.1ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +181.1ms |
| Confluent | 836,173,000 | 2026-08-04T16:28:32.5092919+00:00 | 208.5ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +181.1ms |
| Confluent | 836,200,000 | 2026-08-04T16:28:32.566764+00:00 | 182.1ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 836,206,000 | 2026-08-04T16:28:32.5768601+00:00 | 159.6ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 836,211,000 | 2026-08-04T16:28:32.5831154+00:00 | 213.3ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 836,243,000 | 2026-08-04T16:28:32.6121373+00:00 | 185.5ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 836,313,000 | 2026-08-04T16:28:32.6798567+00:00 | 194.1ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 836,332,000 | 2026-08-04T16:28:32.7164671+00:00 | 146.3ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 836,374,000 | 2026-08-04T16:28:32.8183921+00:00 | 107.6ms | GC pause | - | - | 731.5s / 656,246 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 851,408,000 | 2026-08-04T16:28:51.5611484+00:00 | 101.7ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +162.3ms |
| Confluent | 851,518,000 | 2026-08-04T16:28:51.6392393+00:00 | 122.1ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,602,000 | 2026-08-04T16:28:51.6884191+00:00 | 111.6ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,612,000 | 2026-08-04T16:28:51.6970535+00:00 | 117.7ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,616,000 | 2026-08-04T16:28:51.6998578+00:00 | 118.7ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,685,000 | 2026-08-04T16:28:51.7709341+00:00 | 102.8ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,738,000 | 2026-08-04T16:28:51.8118106+00:00 | 139.7ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,791,000 | 2026-08-04T16:28:51.8593295+00:00 | 152.6ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,815,000 | 2026-08-04T16:28:51.880286+00:00 | 102.5ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,818,000 | 2026-08-04T16:28:51.884532+00:00 | 137.3ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 851,825,000 | 2026-08-04T16:28:51.8911798+00:00 | 107.0ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 852,261,000 | 2026-08-04T16:28:52.3160157+00:00 | 138.2ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 852,333,000 | 2026-08-04T16:28:52.3722531+00:00 | 109.6ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 852,367,000 | 2026-08-04T16:28:52.4041658+00:00 | 155.3ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 852,388,000 | 2026-08-04T16:28:52.4351286+00:00 | 140.8ms | GC pause | - | - | 750.6s / 1,080,988 msg/s | Gen2 +0 / pause +55.2ms |
| Confluent | 852,417,000 | 2026-08-04T16:28:52.4648646+00:00 | 147.6ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +154.9ms |
| Confluent | 852,471,000 | 2026-08-04T16:28:52.5398964+00:00 | 132.8ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +154.9ms |
| Confluent | 852,578,000 | 2026-08-04T16:28:52.6526392+00:00 | 151.3ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 852,581,000 | 2026-08-04T16:28:52.6566761+00:00 | 148.4ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 852,657,000 | 2026-08-04T16:28:52.7172032+00:00 | 154.2ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 852,675,000 | 2026-08-04T16:28:52.7333486+00:00 | 105.8ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 852,695,000 | 2026-08-04T16:28:52.7504149+00:00 | 101.4ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 852,810,000 | 2026-08-04T16:28:52.8610263+00:00 | 103.2ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 852,828,000 | 2026-08-04T16:28:52.8952166+00:00 | 132.7ms | GC pause | - | - | 751.6s / 743,011 msg/s | Gen2 +0 / pause +99.7ms |
| Confluent | 887,957,000 | 2026-08-04T16:29:36.6835471+00:00 | 101.2ms | GC pause | - | - | 795.6s / 689,711 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 887,991,000 | 2026-08-04T16:29:36.7248793+00:00 | 114.8ms | GC pause | - | - | 795.6s / 689,711 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 888,031,000 | 2026-08-04T16:29:36.7732631+00:00 | 114.6ms | GC pause | - | - | 795.6s / 689,711 msg/s | Gen2 +0 / pause +180.8ms |
| Confluent | 889,938,000 | 2026-08-04T16:29:39.3187457+00:00 | 111.6ms | GC pause | - | - | 797.6s / 918,576 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 890,048,000 | 2026-08-04T16:29:39.4187124+00:00 | 125.8ms | GC pause | - | - | 797.6s / 918,576 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 890,109,000 | 2026-08-04T16:29:39.4749115+00:00 | 115.2ms | GC pause | - | - | 797.6s / 918,576 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 890,129,000 | 2026-08-04T16:29:39.4966932+00:00 | 112.6ms | GC pause | - | - | 797.6s / 918,576 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 890,147,000 | 2026-08-04T16:29:39.5117658+00:00 | 135.3ms | GC pause | - | - | 797.6s / 918,576 msg/s | Gen2 +0 / pause +96.6ms |
| Confluent | 890,158,000 | 2026-08-04T16:29:39.521217+00:00 | 140.2ms | GC pause | - | - | 798.6s / 587,775 msg/s | Gen2 +0 / pause +208.2ms |
| Confluent | 890,185,000 | 2026-08-04T16:29:39.5438592+00:00 | 130.5ms | GC pause | - | - | 798.6s / 587,775 msg/s | Gen2 +0 / pause +208.2ms |
| Confluent | 890,228,000 | 2026-08-04T16:29:39.5800374+00:00 | 170.3ms | GC pause | - | - | 798.6s / 587,775 msg/s | Gen2 +0 / pause +208.2ms |
| Confluent | 890,245,000 | 2026-08-04T16:29:39.594916+00:00 | 154.9ms | GC pause | - | - | 798.6s / 587,775 msg/s | Gen2 +0 / pause +208.2ms |
| Confluent | 893,629,000 | 2026-08-04T16:29:44.3485865+00:00 | 101.7ms | GC pause | - | - | 802.7s / 917,993 msg/s | Gen2 +0 / pause +126.9ms |
| Confluent | 893,690,000 | 2026-08-04T16:29:44.4008809+00:00 | 131.5ms | GC pause | - | - | 802.7s / 917,993 msg/s | Gen2 +0 / pause +126.9ms |
| Confluent | 893,694,000 | 2026-08-04T16:29:44.4036482+00:00 | 127.3ms | GC pause | - | - | 802.7s / 917,993 msg/s | Gen2 +0 / pause +126.9ms |
| Confluent | 893,706,000 | 2026-08-04T16:29:44.4143828+00:00 | 138.1ms | GC pause | - | - | 802.7s / 917,993 msg/s | Gen2 +0 / pause +126.9ms |
| Confluent | 893,756,000 | 2026-08-04T16:29:44.4563091+00:00 | 176.2ms | GC pause | - | - | 802.7s / 917,993 msg/s | Gen2 +0 / pause +126.9ms |
| Confluent | 893,816,000 | 2026-08-04T16:29:44.5295287+00:00 | 199.8ms | GC pause | - | - | 803.7s / 551,443 msg/s | Gen2 +0 / pause +297.8ms |
| Confluent | 893,855,000 | 2026-08-04T16:29:44.6018436+00:00 | 175.8ms | GC pause | - | - | 803.7s / 551,443 msg/s | Gen2 +0 / pause +297.8ms |
| Confluent | 893,860,000 | 2026-08-04T16:29:44.6116298+00:00 | 181.3ms | GC pause | - | - | 803.7s / 551,443 msg/s | Gen2 +0 / pause +297.8ms |
| Confluent | 893,877,000 | 2026-08-04T16:29:44.6360403+00:00 | 203.1ms | GC pause | - | - | 803.7s / 551,443 msg/s | Gen2 +0 / pause +297.8ms |
| Confluent | 893,971,000 | 2026-08-04T16:29:44.8433095+00:00 | 130.0ms | GC pause | - | - | 803.7s / 551,443 msg/s | Gen2 +0 / pause +170.9ms |
| Confluent | 894,304,000 | 2026-08-04T16:29:45.378242+00:00 | 100.1ms | GC pause | - | - | 803.7s / 551,443 msg/s | Gen2 +0 / pause +170.9ms |
| Dekaf | 846,706,000 | 2026-08-04T16:56:18.5308863+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 596.2s / 1,277,948 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 846,707,000 | 2026-08-04T16:56:18.5344608+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 596.2s / 1,277,948 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 72,967,000 | 2026-08-04T17:02:35.5692907+00:00 | 109.4ms | GC pause | - | - | 73.1s / 1,124,447 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 72,968,000 | 2026-08-04T17:02:35.5698811+00:00 | 108.8ms | GC pause | - | - | 73.1s / 1,124,447 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 73,021,000 | 2026-08-04T17:02:35.6042736+00:00 | 101.3ms | GC pause | - | - | 73.1s / 1,124,447 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 106,658,000 | 2026-08-04T17:03:08.5875848+00:00 | 103.1ms | GC pause | - | - | 106.1s / 988,161 msg/s | Gen2 +0 / pause +83.6ms |
| Confluent | 106,867,000 | 2026-08-04T17:03:08.7523822+00:00 | 100.5ms | GC pause | - | - | 106.1s / 988,161 msg/s | Gen2 +0 / pause +83.6ms |
| Confluent | 121,677,000 | 2026-08-04T17:03:23.2965399+00:00 | 104.9ms | GC pause | - | - | 120.1s / 1,364,219 msg/s | Gen2 +0 / pause +106.4ms |
| Confluent | 121,687,000 | 2026-08-04T17:03:23.3025217+00:00 | 104.6ms | GC pause | - | - | 120.1s / 1,364,219 msg/s | Gen2 +0 / pause +106.4ms |
| Confluent | 121,721,000 | 2026-08-04T17:03:23.3252592+00:00 | 124.4ms | GC pause | - | - | 120.1s / 1,364,219 msg/s | Gen2 +0 / pause +106.4ms |
| Confluent | 121,837,000 | 2026-08-04T17:03:23.4410002+00:00 | 103.7ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,841,000 | 2026-08-04T17:03:23.4429718+00:00 | 105.9ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,861,000 | 2026-08-04T17:03:23.4604402+00:00 | 110.1ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,878,000 | 2026-08-04T17:03:23.4709628+00:00 | 112.6ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,887,000 | 2026-08-04T17:03:23.4794266+00:00 | 105.9ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,898,000 | 2026-08-04T17:03:23.4861251+00:00 | 112.8ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,917,000 | 2026-08-04T17:03:23.4985324+00:00 | 109.2ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +225.8ms |
| Confluent | 121,971,000 | 2026-08-04T17:03:23.5376957+00:00 | 112.3ms | GC pause | - | - | 121.1s / 1,246,156 msg/s | Gen2 +0 / pause +119.5ms |
| Confluent | 137,441,000 | 2026-08-04T17:03:38.521192+00:00 | 101.5ms | GC pause | - | - | 136.1s / 948,267 msg/s | Gen2 +0 / pause +133.6ms |
| Confluent | 143,428,000 | 2026-08-04T17:03:44.4972103+00:00 | 106.9ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +187.9ms |
| Confluent | 143,431,000 | 2026-08-04T17:03:44.4989337+00:00 | 107.2ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +187.9ms |
| Confluent | 143,437,000 | 2026-08-04T17:03:44.5034602+00:00 | 108.3ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +187.9ms |
| Confluent | 143,465,000 | 2026-08-04T17:03:44.5216996+00:00 | 100.1ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +101.3ms |
| Confluent | 143,488,000 | 2026-08-04T17:03:44.5365592+00:00 | 115.0ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +101.3ms |
| Confluent | 143,511,000 | 2026-08-04T17:03:44.5611206+00:00 | 108.4ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +101.3ms |
| Confluent | 143,521,000 | 2026-08-04T17:03:44.5752696+00:00 | 108.6ms | GC pause | - | - | 142.1s / 846,905 msg/s | Gen2 +0 / pause +101.3ms |
| Confluent | 176,168,000 | 2026-08-04T17:04:16.4781628+00:00 | 103.2ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 176,181,000 | 2026-08-04T17:04:16.4862303+00:00 | 105.2ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 176,207,000 | 2026-08-04T17:04:16.5042023+00:00 | 109.7ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +183.4ms |
| Confluent | 176,258,000 | 2026-08-04T17:04:16.5411201+00:00 | 124.2ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,267,000 | 2026-08-04T17:04:16.5463433+00:00 | 120.7ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,268,000 | 2026-08-04T17:04:16.5467931+00:00 | 120.3ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,289,000 | 2026-08-04T17:04:16.5632553+00:00 | 101.6ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,297,000 | 2026-08-04T17:04:16.5743415+00:00 | 122.5ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,300,000 | 2026-08-04T17:04:16.5761436+00:00 | 106.2ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,303,000 | 2026-08-04T17:04:16.5793637+00:00 | 103.2ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,311,000 | 2026-08-04T17:04:16.5952661+00:00 | 110.1ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,318,000 | 2026-08-04T17:04:16.6039025+00:00 | 108.6ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,327,000 | 2026-08-04T17:04:16.6157391+00:00 | 103.6ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 176,331,000 | 2026-08-04T17:04:16.6182563+00:00 | 101.2ms | GC pause | - | - | 174.1s / 916,713 msg/s | Gen2 +0 / pause +100.0ms |
| Confluent | 186,691,000 | 2026-08-04T17:04:26.4076689+00:00 | 100.0ms | GC pause | - | - | 183.1s / 1,232,162 msg/s | Gen2 +0 / pause +114.1ms |
| Confluent | 186,712,000 | 2026-08-04T17:04:26.4202111+00:00 | 102.3ms | GC pause | - | - | 183.1s / 1,232,162 msg/s | Gen2 +0 / pause +114.1ms |
| Confluent | 186,716,000 | 2026-08-04T17:04:26.4227607+00:00 | 110.1ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,723,000 | 2026-08-04T17:04:26.4274709+00:00 | 106.4ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,734,000 | 2026-08-04T17:04:26.4340595+00:00 | 117.0ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,735,000 | 2026-08-04T17:04:26.4346302+00:00 | 115.3ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,736,000 | 2026-08-04T17:04:26.4352442+00:00 | 114.7ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,738,000 | 2026-08-04T17:04:26.4366489+00:00 | 114.9ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,745,000 | 2026-08-04T17:04:26.4537661+00:00 | 100.5ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,748,000 | 2026-08-04T17:04:26.4561002+00:00 | 104.6ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,754,000 | 2026-08-04T17:04:26.4621399+00:00 | 101.7ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 186,768,000 | 2026-08-04T17:04:26.490604+00:00 | 106.4ms | GC pause | - | - | 184.1s / 847,328 msg/s | Gen2 +0 / pause +237.0ms |
| Confluent | 236,031,000 | 2026-08-04T17:05:17.3339+00:00 | 102.4ms | GC pause | - | - | 234.2s / 1,300,766 msg/s | Gen2 +0 / pause +107.1ms |
| Confluent | 243,001,000 | 2026-08-04T17:05:24.332799+00:00 | 102.7ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,011,000 | 2026-08-04T17:05:24.3416278+00:00 | 103.6ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,023,000 | 2026-08-04T17:05:24.3489378+00:00 | 106.1ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,029,000 | 2026-08-04T17:05:24.3522599+00:00 | 103.2ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,034,000 | 2026-08-04T17:05:24.3577699+00:00 | 100.9ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,040,000 | 2026-08-04T17:05:24.3600296+00:00 | 118.1ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,044,000 | 2026-08-04T17:05:24.3621761+00:00 | 115.2ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,045,000 | 2026-08-04T17:05:24.3626505+00:00 | 115.8ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,046,000 | 2026-08-04T17:05:24.3631262+00:00 | 115.3ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,051,000 | 2026-08-04T17:05:24.3656816+00:00 | 116.7ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,053,000 | 2026-08-04T17:05:24.3668186+00:00 | 115.2ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,059,000 | 2026-08-04T17:05:24.3711435+00:00 | 111.1ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,060,000 | 2026-08-04T17:05:24.3717317+00:00 | 111.6ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,069,000 | 2026-08-04T17:05:24.3772412+00:00 | 106.2ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,072,000 | 2026-08-04T17:05:24.3792092+00:00 | 103.4ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,077,000 | 2026-08-04T17:05:24.3819714+00:00 | 102.9ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,087,000 | 2026-08-04T17:05:24.3872965+00:00 | 105.4ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,094,000 | 2026-08-04T17:05:24.3913864+00:00 | 102.6ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,099,000 | 2026-08-04T17:05:24.3960565+00:00 | 100.0ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,111,000 | 2026-08-04T17:05:24.4101004+00:00 | 106.1ms | GC pause | - | - | 241.2s / 1,218,452 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 243,237,000 | 2026-08-04T17:05:24.527879+00:00 | 103.0ms | GC pause | - | - | 242.2s / 813,161 msg/s | Gen2 +0 / pause +149.4ms |
| Confluent | 243,241,000 | 2026-08-04T17:05:24.5301274+00:00 | 110.8ms | GC pause | - | - | 242.2s / 813,161 msg/s | Gen2 +0 / pause +149.4ms |
| Confluent | 243,254,000 | 2026-08-04T17:05:24.5384557+00:00 | 111.3ms | GC pause | - | - | 242.2s / 813,161 msg/s | Gen2 +0 / pause +149.4ms |
| Confluent | 251,621,000 | 2026-08-04T17:05:33.3183142+00:00 | 102.7ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,641,000 | 2026-08-04T17:05:33.3285008+00:00 | 110.3ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,653,000 | 2026-08-04T17:05:33.3350974+00:00 | 101.1ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,660,000 | 2026-08-04T17:05:33.3409442+00:00 | 102.9ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,661,000 | 2026-08-04T17:05:33.3415118+00:00 | 115.7ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,668,000 | 2026-08-04T17:05:33.3460709+00:00 | 120.2ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,680,000 | 2026-08-04T17:05:33.3533964+00:00 | 104.4ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,687,000 | 2026-08-04T17:05:33.3592984+00:00 | 127.5ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,691,000 | 2026-08-04T17:05:33.3624388+00:00 | 125.8ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,705,000 | 2026-08-04T17:05:33.3733706+00:00 | 100.5ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,715,000 | 2026-08-04T17:05:33.3807919+00:00 | 104.6ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,720,000 | 2026-08-04T17:05:33.3842329+00:00 | 112.3ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,725,000 | 2026-08-04T17:05:33.3876379+00:00 | 100.4ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,730,000 | 2026-08-04T17:05:33.3910932+00:00 | 112.2ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,741,000 | 2026-08-04T17:05:33.3986963+00:00 | 133.8ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,745,000 | 2026-08-04T17:05:33.4014326+00:00 | 101.1ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,747,000 | 2026-08-04T17:05:33.4025439+00:00 | 131.9ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,780,000 | 2026-08-04T17:05:33.4316878+00:00 | 118.3ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,783,000 | 2026-08-04T17:05:33.4352664+00:00 | 125.8ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,790,000 | 2026-08-04T17:05:33.4421266+00:00 | 120.6ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,791,000 | 2026-08-04T17:05:33.4426846+00:00 | 131.6ms | GC pause | - | - | 251.2s / 983,836 msg/s | Gen2 +0 / pause +182.7ms |
| Confluent | 251,803,000 | 2026-08-04T17:05:33.4601415+00:00 | 112.7ms | GC pause | - | - | 251.2s / 983,836 msg/s | Gen2 +0 / pause +182.7ms |
| Confluent | 251,805,000 | 2026-08-04T17:05:33.4626586+00:00 | 107.2ms | GC pause | - | - | 250.2s / 1,346,339 msg/s | Gen2 +0 / pause +82.0ms |
| Confluent | 251,811,000 | 2026-08-04T17:05:33.4694808+00:00 | 123.9ms | GC pause | - | - | 251.2s / 983,836 msg/s | Gen2 +0 / pause +182.7ms |
| Confluent | 275,447,000 | 2026-08-04T17:05:55.9797088+00:00 | 104.4ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,448,000 | 2026-08-04T17:05:55.9816385+00:00 | 102.5ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,778,000 | 2026-08-04T17:05:56.2444871+00:00 | 102.1ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,830,000 | 2026-08-04T17:05:56.2777266+00:00 | 105.1ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,833,000 | 2026-08-04T17:05:56.2794232+00:00 | 104.7ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,887,000 | 2026-08-04T17:05:56.3185073+00:00 | 121.4ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,893,000 | 2026-08-04T17:05:56.3219424+00:00 | 105.0ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,898,000 | 2026-08-04T17:05:56.3260136+00:00 | 121.7ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,900,000 | 2026-08-04T17:05:56.3271126+00:00 | 101.4ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,903,000 | 2026-08-04T17:05:56.3301045+00:00 | 104.8ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,917,000 | 2026-08-04T17:05:56.3388659+00:00 | 122.4ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,921,000 | 2026-08-04T17:05:56.3411903+00:00 | 125.9ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,923,000 | 2026-08-04T17:05:56.3424022+00:00 | 108.2ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,927,000 | 2026-08-04T17:05:56.3451356+00:00 | 126.8ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,931,000 | 2026-08-04T17:05:56.3479268+00:00 | 131.2ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,933,000 | 2026-08-04T17:05:56.3503449+00:00 | 109.0ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,941,000 | 2026-08-04T17:05:56.3557068+00:00 | 126.8ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,950,000 | 2026-08-04T17:05:56.3637992+00:00 | 110.6ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,976,000 | 2026-08-04T17:05:56.3816727+00:00 | 100.6ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 275,988,000 | 2026-08-04T17:05:56.3936609+00:00 | 126.5ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,003,000 | 2026-08-04T17:05:56.4092529+00:00 | 109.3ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,017,000 | 2026-08-04T17:05:56.4208021+00:00 | 125.6ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,020,000 | 2026-08-04T17:05:56.4242868+00:00 | 106.1ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,027,000 | 2026-08-04T17:05:56.4328088+00:00 | 118.9ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,057,000 | 2026-08-04T17:05:56.4568919+00:00 | 120.1ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,058,000 | 2026-08-04T17:05:56.4587464+00:00 | 118.2ms | GC pause | - | - | 273.2s / 1,321,317 msg/s | Gen2 +0 / pause +70.2ms |
| Confluent | 276,077,000 | 2026-08-04T17:05:56.471681+00:00 | 123.8ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,097,000 | 2026-08-04T17:05:56.4885938+00:00 | 121.3ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,107,000 | 2026-08-04T17:05:56.4961333+00:00 | 117.4ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,110,000 | 2026-08-04T17:05:56.4981343+00:00 | 103.0ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,148,000 | 2026-08-04T17:05:56.5294838+00:00 | 111.8ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,157,000 | 2026-08-04T17:05:56.5365982+00:00 | 112.6ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,171,000 | 2026-08-04T17:05:56.5497043+00:00 | 106.7ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,178,000 | 2026-08-04T17:05:56.5548911+00:00 | 109.9ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 276,207,000 | 2026-08-04T17:05:56.5849838+00:00 | 102.1ms | GC pause | - | - | 274.2s / 1,001,010 msg/s | Gen2 +0 / pause +138.6ms |
| Confluent | 286,101,000 | 2026-08-04T17:06:06.294553+00:00 | 108.3ms | GC pause | - | - | 283.2s / 1,132,216 msg/s | Gen2 +0 / pause +110.8ms |
| Confluent | 430,631,000 | 2026-08-04T17:08:17.8851331+00:00 | 104.8ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,638,000 | 2026-08-04T17:08:17.8905322+00:00 | 104.7ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,647,000 | 2026-08-04T17:08:17.898615+00:00 | 107.5ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,648,000 | 2026-08-04T17:08:17.8994705+00:00 | 108.2ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,651,000 | 2026-08-04T17:08:17.9021256+00:00 | 105.6ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,668,000 | 2026-08-04T17:08:17.91596+00:00 | 108.3ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,681,000 | 2026-08-04T17:08:17.9248348+00:00 | 110.0ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,711,000 | 2026-08-04T17:08:17.9500536+00:00 | 106.4ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,718,000 | 2026-08-04T17:08:17.954742+00:00 | 106.5ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,777,000 | 2026-08-04T17:08:18.0038235+00:00 | 100.6ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,778,000 | 2026-08-04T17:08:18.0042587+00:00 | 100.2ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,788,000 | 2026-08-04T17:08:18.0130191+00:00 | 112.4ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,791,000 | 2026-08-04T17:08:18.0155913+00:00 | 109.9ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,827,000 | 2026-08-04T17:08:18.0454462+00:00 | 104.0ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,847,000 | 2026-08-04T17:08:18.0605874+00:00 | 100.3ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,878,000 | 2026-08-04T17:08:18.0831422+00:00 | 102.5ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,931,000 | 2026-08-04T17:08:18.1212857+00:00 | 106.1ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,937,000 | 2026-08-04T17:08:18.1246092+00:00 | 104.7ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 430,948,000 | 2026-08-04T17:08:18.1349286+00:00 | 105.3ms | GC pause | - | - | 415.3s / 1,053,746 msg/s | Gen2 +0 / pause +62.6ms |
| Confluent | 434,038,000 | 2026-08-04T17:08:21.2846054+00:00 | 104.2ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,061,000 | 2026-08-04T17:08:21.3044027+00:00 | 105.3ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,071,000 | 2026-08-04T17:08:21.3113419+00:00 | 100.6ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,087,000 | 2026-08-04T17:08:21.3215446+00:00 | 104.4ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,097,000 | 2026-08-04T17:08:21.3316825+00:00 | 103.5ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,131,000 | 2026-08-04T17:08:21.3526929+00:00 | 113.2ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,148,000 | 2026-08-04T17:08:21.3637107+00:00 | 121.8ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,171,000 | 2026-08-04T17:08:21.3800392+00:00 | 118.4ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,218,000 | 2026-08-04T17:08:21.413686+00:00 | 127.2ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,221,000 | 2026-08-04T17:08:21.4156654+00:00 | 125.3ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,227,000 | 2026-08-04T17:08:21.4204991+00:00 | 127.9ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,237,000 | 2026-08-04T17:08:21.4271774+00:00 | 126.7ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,253,000 | 2026-08-04T17:08:21.4386581+00:00 | 102.6ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,270,000 | 2026-08-04T17:08:21.4496105+00:00 | 101.3ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,291,000 | 2026-08-04T17:08:21.4628486+00:00 | 135.4ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,311,000 | 2026-08-04T17:08:21.4789825+00:00 | 131.3ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,321,000 | 2026-08-04T17:08:21.485663+00:00 | 133.5ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,328,000 | 2026-08-04T17:08:21.490388+00:00 | 132.3ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,360,000 | 2026-08-04T17:08:21.5181951+00:00 | 100.6ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,361,000 | 2026-08-04T17:08:21.5186835+00:00 | 126.4ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,401,000 | 2026-08-04T17:08:21.5526183+00:00 | 134.7ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,407,000 | 2026-08-04T17:08:21.5572897+00:00 | 134.3ms | GC pause | - | - | 418.3s / 1,335,174 msg/s | Gen2 +0 / pause +63.3ms |
| Confluent | 434,428,000 | 2026-08-04T17:08:21.5756278+00:00 | 122.1ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +121.4ms |
| Confluent | 434,437,000 | 2026-08-04T17:08:21.5849071+00:00 | 114.8ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +121.4ms |
| Confluent | 434,478,000 | 2026-08-04T17:08:21.6208134+00:00 | 100.4ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +121.4ms |
| Confluent | 434,487,000 | 2026-08-04T17:08:21.6266762+00:00 | 101.6ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +121.4ms |
| Confluent | 434,557,000 | 2026-08-04T17:08:21.6884443+00:00 | 100.0ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +121.4ms |
| Confluent | 435,298,000 | 2026-08-04T17:08:22.2432692+00:00 | 100.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,308,000 | 2026-08-04T17:08:22.2498143+00:00 | 112.9ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,311,000 | 2026-08-04T17:08:22.2514927+00:00 | 119.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,328,000 | 2026-08-04T17:08:22.2608566+00:00 | 124.6ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,338,000 | 2026-08-04T17:08:22.2664421+00:00 | 129.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,397,000 | 2026-08-04T17:08:22.3085561+00:00 | 135.9ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,401,000 | 2026-08-04T17:08:22.3113752+00:00 | 133.2ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,417,000 | 2026-08-04T17:08:22.3216149+00:00 | 137.6ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,421,000 | 2026-08-04T17:08:22.3238552+00:00 | 140.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,427,000 | 2026-08-04T17:08:22.3267858+00:00 | 142.1ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,431,000 | 2026-08-04T17:08:22.3287354+00:00 | 145.5ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,443,000 | 2026-08-04T17:08:22.3357406+00:00 | 101.8ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,450,000 | 2026-08-04T17:08:22.3396213+00:00 | 102.6ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,468,000 | 2026-08-04T17:08:22.3575905+00:00 | 145.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,478,000 | 2026-08-04T17:08:22.368148+00:00 | 144.1ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,491,000 | 2026-08-04T17:08:22.3801008+00:00 | 142.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,497,000 | 2026-08-04T17:08:22.3837514+00:00 | 143.4ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,507,000 | 2026-08-04T17:08:22.3901716+00:00 | 146.2ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,518,000 | 2026-08-04T17:08:22.3978685+00:00 | 150.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,520,000 | 2026-08-04T17:08:22.3993503+00:00 | 102.5ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,531,000 | 2026-08-04T17:08:22.4063101+00:00 | 148.9ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,533,000 | 2026-08-04T17:08:22.4077347+00:00 | 103.5ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,540,000 | 2026-08-04T17:08:22.4126599+00:00 | 101.6ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,577,000 | 2026-08-04T17:08:22.4434968+00:00 | 153.5ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,580,000 | 2026-08-04T17:08:22.4454573+00:00 | 103.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,581,000 | 2026-08-04T17:08:22.4461162+00:00 | 155.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,583,000 | 2026-08-04T17:08:22.4471047+00:00 | 101.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,584,000 | 2026-08-04T17:08:22.4476406+00:00 | 100.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,587,000 | 2026-08-04T17:08:22.4502069+00:00 | 155.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,600,000 | 2026-08-04T17:08:22.4595216+00:00 | 103.8ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,607,000 | 2026-08-04T17:08:22.4650903+00:00 | 154.5ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,611,000 | 2026-08-04T17:08:22.4682242+00:00 | 156.2ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,613,000 | 2026-08-04T17:08:22.4714707+00:00 | 111.9ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,620,000 | 2026-08-04T17:08:22.4778518+00:00 | 110.2ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,627,000 | 2026-08-04T17:08:22.4849043+00:00 | 157.3ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,634,000 | 2026-08-04T17:08:22.4909402+00:00 | 101.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,660,000 | 2026-08-04T17:08:22.5132368+00:00 | 109.0ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,673,000 | 2026-08-04T17:08:22.5230521+00:00 | 108.4ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,697,000 | 2026-08-04T17:08:22.5439518+00:00 | 151.3ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,703,000 | 2026-08-04T17:08:22.5519311+00:00 | 104.0ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,720,000 | 2026-08-04T17:08:22.5648707+00:00 | 104.7ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,737,000 | 2026-08-04T17:08:22.5778157+00:00 | 149.2ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,738,000 | 2026-08-04T17:08:22.5785245+00:00 | 148.5ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,751,000 | 2026-08-04T17:08:22.5875145+00:00 | 142.3ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,753,000 | 2026-08-04T17:08:22.5889101+00:00 | 103.2ms | GC pause | - | - | 419.3s / 1,316,105 msg/s | Gen2 +0 / pause +58.1ms |
| Confluent | 435,767,000 | 2026-08-04T17:08:22.604385+00:00 | 138.2ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,773,000 | 2026-08-04T17:08:22.6082977+00:00 | 103.1ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,787,000 | 2026-08-04T17:08:22.6210096+00:00 | 137.5ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,808,000 | 2026-08-04T17:08:22.6377719+00:00 | 136.7ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,847,000 | 2026-08-04T17:08:22.6683924+00:00 | 134.8ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,877,000 | 2026-08-04T17:08:22.6947146+00:00 | 121.6ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +124.6ms |
| Confluent | 435,887,000 | 2026-08-04T17:08:22.7030606+00:00 | 118.0ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 435,907,000 | 2026-08-04T17:08:22.7213989+00:00 | 111.2ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 435,948,000 | 2026-08-04T17:08:22.7616626+00:00 | 102.1ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,447,000 | 2026-08-04T17:08:23.1205014+00:00 | 101.9ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,537,000 | 2026-08-04T17:08:23.1860372+00:00 | 104.5ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,538,000 | 2026-08-04T17:08:23.1865706+00:00 | 104.0ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,618,000 | 2026-08-04T17:08:23.2432915+00:00 | 115.3ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,641,000 | 2026-08-04T17:08:23.2595226+00:00 | 117.1ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,658,000 | 2026-08-04T17:08:23.2716714+00:00 | 120.7ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,738,000 | 2026-08-04T17:08:23.3321486+00:00 | 116.4ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,758,000 | 2026-08-04T17:08:23.3425954+00:00 | 121.4ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,768,000 | 2026-08-04T17:08:23.350429+00:00 | 120.1ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,778,000 | 2026-08-04T17:08:23.3579331+00:00 | 130.0ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,787,000 | 2026-08-04T17:08:23.364245+00:00 | 124.4ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,807,000 | 2026-08-04T17:08:23.3818344+00:00 | 116.4ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 436,828,000 | 2026-08-04T17:08:23.4006535+00:00 | 104.7ms | GC pause | - | - | 420.3s / 1,197,746 msg/s | Gen2 +0 / pause +66.5ms |
| Confluent | 682,717,000 | 2026-08-04T17:12:08.3039846+00:00 | 110.7ms | GC pause | - | - | 645.4s / 1,209,215 msg/s | Gen2 +0 / pause +110.0ms |
| Confluent | 871,858,000 | 2026-08-04T17:14:58.1648518+00:00 | 101.2ms | GC pause | - | - | 815.6s / 984,795 msg/s | Gen2 +0 / pause +89.5ms |
| Confluent | 904,717,000 | 2026-08-04T17:15:30.7346173+00:00 | 102.3ms | GC pause | - | - | 847.6s / 1,167,280 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 904,718,000 | 2026-08-04T17:15:30.7351497+00:00 | 101.8ms | GC pause | - | - | 847.6s / 1,167,280 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 926,197,000 | 2026-08-04T17:15:55.7369977+00:00 | 100.2ms | GC pause | - | - | 872.6s / 1,131,933 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 926,241,000 | 2026-08-04T17:15:55.7635018+00:00 | 104.9ms | GC pause | - | - | 872.6s / 1,131,933 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 926,247,000 | 2026-08-04T17:15:55.7670918+00:00 | 109.0ms | GC pause | - | - | 872.6s / 1,131,933 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 926,248,000 | 2026-08-04T17:15:55.7677159+00:00 | 108.4ms | GC pause | - | - | 872.6s / 1,131,933 msg/s | Gen2 +0 / pause +79.2ms |
| Confluent | 926,257,000 | 2026-08-04T17:15:55.7787022+00:00 | 108.3ms | GC pause | - | - | 872.6s / 1,131,933 msg/s | Gen2 +0 / pause +79.2ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*8,397 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.60x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.28x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.50 | 1411.19 | 875,515 | 892,011 | -4.7% | -0.55% | 834.96 | 875,515 | 0 | 1.31 |
| Dekaf | 1.60 | 1571.23 | 823,253 | 852,018 | +16.6% | +1.45% | 785.12 | 823,253 | 0 | 1.32 |
| Confluent | 2.66 | - | 606,363 | 611,482 | -10.4% | -0.97% | 578.27 | 606,363 | 0 | 1.61 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 247,642 | 275.15 | 975.25 KB |
| Dekaf | 2 | 256,243 | 284.71 | 988.22 KB |
| Dekaf | 3 | 251,022 | 278.91 | 963.30 KB |
| Dekaf (3conn) | 1 | 274,921 | 305.46 | 934.24 KB |
| Dekaf (3conn) | 2 | 276,964 | 307.73 | 932.17 KB |
| Dekaf (3conn) | 3 | 283,041 | 314.48 | 947.85 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:31.6876042+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 440,802 msg/s |
| Dekaf | 2026-08-04T16:31:49.6953782+00:00 | 3 | 16.0 MiB / 5.4 MiB | 322.0 MB/s | 0/0 | 237 | 18.0s / 871,697 msg/s |
| Dekaf | 2026-08-04T16:32:08.7074597+00:00 | 1 | 16.0 MiB / 11.3 MiB | 318.8 MB/s | 0/0 | 1,130 | 37.0s / 880,021 msg/s |
| Dekaf | 2026-08-04T16:32:26.7173927+00:00 | 1 | 14.0 MiB / 8.0 MiB | 325.2 MB/s | 1/0 | 1,607 | 55.0s / 898,660 msg/s |
| Dekaf | 2026-08-04T16:32:44.7263361+00:00 | 2 | 12.0 MiB / 10.0 MiB | 345.8 MB/s | 2/0 | 13,641 | 73.0s / 865,994 msg/s |
| Dekaf | 2026-08-04T16:33:02.7305022+00:00 | 2 | 10.0 MiB / 9.1 MiB | 345.8 MB/s | 3/0 | 15,783 | 91.0s / 697,226 msg/s |
| Dekaf | 2026-08-04T16:33:20.737363+00:00 | 3 | 8.0 MiB / 5.4 MiB | 332.0 MB/s | 4/1 | 1,765 | 109.1s / 707,824 msg/s |
| Dekaf | 2026-08-04T16:33:38.7443328+00:00 | 3 | 8.0 MiB / 6.6 MiB | 332.0 MB/s | 4/1 | 2,402 | 127.1s / 677,755 msg/s |
| Dekaf | 2026-08-04T16:33:57.7509361+00:00 | 1 | 8.0 MiB / 2.6 MiB | 330.3 MB/s | 4/1 | 5,745 | 146.1s / 685,965 msg/s |
| Dekaf | 2026-08-04T16:34:15.7577374+00:00 | 1 | 8.0 MiB / 6.5 MiB | 330.3 MB/s | 4/2 | 6,255 | 164.1s / 686,070 msg/s |
| Dekaf | 2026-08-04T16:34:33.7666929+00:00 | 2 | 7.0 MiB / 6.2 MiB | 345.8 MB/s | 5/2 | 26,532 | 182.1s / 652,750 msg/s |
| Dekaf | 2026-08-04T16:34:51.7724342+00:00 | 2 | 6.0 MiB / 2.6 MiB | 345.8 MB/s | 5/2 | 28,684 | 200.1s / 661,855 msg/s |
| Dekaf | 2026-08-04T16:35:09.782302+00:00 | 3 | 8.0 MiB / 1.9 MiB | 332.0 MB/s | 4/3 | 4,831 | 218.1s / 656,587 msg/s |
| Dekaf | 2026-08-04T16:35:27.7852495+00:00 | 3 | 8.0 MiB / 7.8 MiB | 332.0 MB/s | 4/4 | 5,304 | 236.1s / 674,801 msg/s |
| Dekaf | 2026-08-04T16:35:46.7863808+00:00 | 1 | 8.0 MiB / 3.3 MiB | 330.3 MB/s | 4/4 | 9,091 | 255.1s / 677,223 msg/s |
| Dekaf | 2026-08-04T16:36:04.794358+00:00 | 1 | 7.0 MiB / 2.7 MiB | 330.3 MB/s | 5/4 | 10,218 | 273.2s / 694,617 msg/s |
| Dekaf | 2026-08-04T16:36:22.8057388+00:00 | 2 | 7.0 MiB / 7.0 MiB | 345.8 MB/s | 5/5 | 39,359 | 291.2s / 838,382 msg/s |
| Dekaf | 2026-08-04T16:36:40.8138925+00:00 | 2 | 7.0 MiB / 6.1 MiB | 345.8 MB/s | 5/5 | 41,214 | 309.2s / 769,134 msg/s |
| Dekaf | 2026-08-04T16:36:58.8200353+00:00 | 3 | 8.0 MiB / 3.4 MiB | 332.0 MB/s | 4/6 | 6,984 | 327.2s / 695,748 msg/s |
| Dekaf | 2026-08-04T16:37:16.8264648+00:00 | 3 | 8.0 MiB / 4.1 MiB | 332.0 MB/s | 4/6 | 7,255 | 345.2s / 789,033 msg/s |
| Dekaf | 2026-08-04T16:37:35.8352863+00:00 | 1 | 6.0 MiB / 1.7 MiB | 330.3 MB/s | 6/6 | 21,148 | 364.2s / 806,471 msg/s |
| Dekaf | 2026-08-04T16:37:53.846482+00:00 | 1 | 6.0 MiB / 3.9 MiB | 330.3 MB/s | 6/6 | 22,590 | 382.2s / 827,955 msg/s |
| Dekaf | 2026-08-04T16:38:11.8647682+00:00 | 2 | 7.0 MiB / 5.7 MiB | 345.8 MB/s | 5/7 | 50,778 | 400.2s / 881,609 msg/s |
| Dekaf | 2026-08-04T16:38:29.8659176+00:00 | 2 | 7.0 MiB / 3.7 MiB | 346.0 MB/s | 5/7 | 53,689 | 418.2s / 969,289 msg/s |
| Dekaf | 2026-08-04T16:38:47.8743618+00:00 | 3 | 8.0 MiB / 4.6 MiB | 336.9 MB/s | 4/7 | 9,664 | 436.2s / 900,136 msg/s |
| Dekaf | 2026-08-04T16:39:05.8831234+00:00 | 3 | 8.0 MiB / 3.2 MiB | 336.9 MB/s | 4/7 | 10,338 | 454.2s / 855,746 msg/s |
| Dekaf | 2026-08-04T16:39:24.8856233+00:00 | 1 | 6.0 MiB / 4.9 MiB | 336.7 MB/s | 8/7 | 34,027 | 473.3s / 972,022 msg/s |
| Dekaf | 2026-08-04T16:39:42.8919439+00:00 | 1 | 6.0 MiB / 4.5 MiB | 336.7 MB/s | 8/7 | 36,997 | 491.3s / 931,567 msg/s |
| Dekaf | 2026-08-04T16:40:00.9053089+00:00 | 2 | 7.0 MiB / 3.0 MiB | 363.0 MB/s | 5/7 | 68,061 | 509.3s / 914,052 msg/s |
| Dekaf | 2026-08-04T16:40:18.9162975+00:00 | 2 | 7.0 MiB / 4.8 MiB | 363.0 MB/s | 5/7 | 71,290 | 527.3s / 871,601 msg/s |
| Dekaf | 2026-08-04T16:40:36.9277547+00:00 | 3 | 8.0 MiB / 2.4 MiB | 339.2 MB/s | 4/7 | 13,407 | 545.3s / 881,577 msg/s |
| Dekaf | 2026-08-04T16:40:54.9402832+00:00 | 3 | 8.0 MiB / 2.5 MiB | 343.7 MB/s | 4/7 | 14,082 | 563.3s / 923,950 msg/s |
| Dekaf | 2026-08-04T16:41:13.9484887+00:00 | 1 | 8.0 MiB / 6.9 MiB | 340.0 MB/s | 9/8 | 47,215 | 582.3s / 938,663 msg/s |
| Dekaf | 2026-08-04T16:41:31.9615328+00:00 | 1 | 8.0 MiB / 5.2 MiB | 340.0 MB/s | 10/8 | 48,382 | 600.3s / 847,462 msg/s |
| Dekaf | 2026-08-04T16:41:49.9752961+00:00 | 2 | 7.0 MiB / 4.1 MiB | 363.6 MB/s | 5/7 | 89,527 | 618.3s / 831,460 msg/s |
| Dekaf | 2026-08-04T16:42:07.9842189+00:00 | 2 | 7.0 MiB / 3.1 MiB | 363.6 MB/s | 5/8 | 93,933 | 636.3s / 896,872 msg/s |
| Dekaf | 2026-08-04T16:42:25.9862688+00:00 | 3 | 8.0 MiB / 7.8 MiB | 343.7 MB/s | 4/8 | 16,673 | 654.3s / 882,294 msg/s |
| Dekaf | 2026-08-04T16:42:44.0006874+00:00 | 3 | 8.0 MiB / 6.1 MiB | 343.7 MB/s | 4/8 | 17,812 | 672.3s / 933,245 msg/s |
| Dekaf | 2026-08-04T16:43:03.0022841+00:00 | 1 | 7.0 MiB / 4.0 MiB | 340.0 MB/s | 10/9 | 55,768 | 691.4s / 902,853 msg/s |
| Dekaf | 2026-08-04T16:43:21.0133846+00:00 | 1 | 6.0 MiB / 2.2 MiB | 340.0 MB/s | 11/9 | 58,104 | 709.4s / 871,646 msg/s |
| Dekaf | 2026-08-04T16:43:39.02207+00:00 | 2 | 7.0 MiB / 6.6 MiB | 363.6 MB/s | 5/8 | 111,953 | 727.4s / 869,073 msg/s |
| Dekaf | 2026-08-04T16:43:57.0326902+00:00 | 2 | 7.0 MiB / 5.1 MiB | 363.6 MB/s | 5/8 | 114,936 | 745.4s / 876,150 msg/s |
| Dekaf | 2026-08-04T16:44:15.0363101+00:00 | 3 | 8.0 MiB / 5.5 MiB | 343.7 MB/s | 4/8 | 20,454 | 763.4s / 889,887 msg/s |
| Dekaf | 2026-08-04T16:44:33.0434685+00:00 | 3 | 8.0 MiB / 3.0 MiB | 343.7 MB/s | 4/8 | 20,748 | 781.4s / 844,921 msg/s |
| Dekaf | 2026-08-04T16:44:52.0496536+00:00 | 1 | 6.0 MiB / 4.0 MiB | 340.0 MB/s | 11/11 | 74,044 | 800.4s / 876,517 msg/s |
| Dekaf | 2026-08-04T16:45:10.0575719+00:00 | 1 | 7.0 MiB / 3.3 MiB | 340.0 MB/s | 11/12 | 75,975 | 818.4s / 754,054 msg/s |
| Dekaf | 2026-08-04T16:45:28.0608102+00:00 | 2 | 7.0 MiB / 5.4 MiB | 363.6 MB/s | 5/8 | 131,228 | 836.4s / 869,738 msg/s |
| Dekaf | 2026-08-04T16:45:46.0675147+00:00 | 2 | 7.0 MiB / 6.6 MiB | 363.6 MB/s | 5/8 | 134,387 | 854.4s / 772,781 msg/s |
| Dekaf | 2026-08-04T16:46:04.0768855+00:00 | 3 | 7.0 MiB / 5.1 MiB | 343.7 MB/s | 4/8 | 23,448 | 872.4s / 937,821 msg/s |
| Dekaf | 2026-08-04T16:46:22.0876767+00:00 | 3 | 8.0 MiB / 3.7 MiB | 343.7 MB/s | 4/9 | 23,997 | 890.4s / 843,823 msg/s |
| Dekaf (3conn) | 2026-08-04T16:46:54.0841769+00:00 | 3 | 16.0 MiB / 5.6 MiB | 294.2 MB/s | 0/0 | 1,560 | 9.0s / 797,281 msg/s |
| Dekaf (3conn) | 2026-08-04T16:47:12.091598+00:00 | 3 | 16.0 MiB / 9.5 MiB | 351.1 MB/s | 0/0 | 3,705 | 27.0s / 941,863 msg/s |
| Dekaf (3conn) | 2026-08-04T16:47:31.108782+00:00 | 1 | 14.0 MiB / 1.1 MiB | 350.8 MB/s | 1/0 | 2,469 | 46.1s / 899,526 msg/s |
| Dekaf (3conn) | 2026-08-04T16:47:49.1154637+00:00 | 1 | 12.0 MiB / 5.2 MiB | 350.8 MB/s | 2/0 | 2,936 | 64.1s / 943,314 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:07.1214393+00:00 | 2 | 10.0 MiB / 2.1 MiB | 353.0 MB/s | 3/0 | 2,721 | 82.1s / 899,389 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:25.1270725+00:00 | 2 | 10.0 MiB / 5.5 MiB | 353.0 MB/s | 3/1 | 3,342 | 100.1s / 864,754 msg/s |
| Dekaf (3conn) | 2026-08-04T16:48:43.1447714+00:00 | 3 | 10.0 MiB / 3.0 MiB | 362.5 MB/s | 3/1 | 8,428 | 118.1s / 891,083 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:01.1553527+00:00 | 3 | 10.0 MiB / 1.4 MiB | 362.5 MB/s | 3/1 | 9,207 | 136.1s / 944,344 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:20.1663775+00:00 | 1 | 10.0 MiB / 6.3 MiB | 350.8 MB/s | 3/1 | 6,142 | 155.1s / 978,035 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:38.1725059+00:00 | 1 | 8.0 MiB / 3.1 MiB | 351.5 MB/s | 3/1 | 6,693 | 173.1s / 888,716 msg/s |
| Dekaf (3conn) | 2026-08-04T16:49:56.1803457+00:00 | 2 | 10.0 MiB / 4.2 MiB | 353.8 MB/s | 3/2 | 5,817 | 191.2s / 945,785 msg/s |
| Dekaf (3conn) | 2026-08-04T16:50:14.186628+00:00 | 2 | 10.0 MiB / 6.2 MiB | 353.8 MB/s | 3/2 | 6,336 | 209.2s / 917,538 msg/s |
| Dekaf (3conn) | 2026-08-04T16:50:32.198559+00:00 | 3 | 10.0 MiB / 3.0 MiB | 365.2 MB/s | 3/2 | 13,917 | 227.2s / 948,270 msg/s |
| Dekaf (3conn) | 2026-08-04T16:50:50.2050711+00:00 | 3 | 10.0 MiB / 10.0 MiB | 365.2 MB/s | 3/2 | 14,635 | 245.2s / 912,814 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:09.2169264+00:00 | 1 | 10.0 MiB / 3.2 MiB | 351.5 MB/s | 3/2 | 10,091 | 264.2s / 902,252 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:27.2326199+00:00 | 1 | 10.0 MiB / 4.0 MiB | 351.5 MB/s | 3/2 | 10,659 | 282.2s / 862,562 msg/s |
| Dekaf (3conn) | 2026-08-04T16:51:45.2411765+00:00 | 2 | 10.0 MiB / 1.7 MiB | 353.8 MB/s | 3/2 | 9,535 | 300.2s / 898,106 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:03.2492821+00:00 | 2 | 10.0 MiB / 9.7 MiB | 353.8 MB/s | 3/3 | 10,066 | 318.2s / 895,110 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:21.2712878+00:00 | 3 | 10.0 MiB / 8.8 MiB | 375.6 MB/s | 3/3 | 20,124 | 336.2s / 959,266 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:39.2784663+00:00 | 3 | 10.0 MiB / 9.3 MiB | 375.6 MB/s | 3/4 | 21,546 | 354.2s / 903,067 msg/s |
| Dekaf (3conn) | 2026-08-04T16:52:58.288802+00:00 | 1 | 11.0 MiB / 8.6 MiB | 351.5 MB/s | 4/3 | 12,790 | 373.3s / 908,787 msg/s |
| Dekaf (3conn) | 2026-08-04T16:53:16.2981132+00:00 | 1 | 11.0 MiB / 9.9 MiB | 354.0 MB/s | 4/3 | 12,927 | 391.3s / 963,159 msg/s |
| Dekaf (3conn) | 2026-08-04T16:53:34.3059666+00:00 | 2 | 8.0 MiB / 5.1 MiB | 359.4 MB/s | 4/4 | 14,947 | 409.3s / 888,190 msg/s |
| Dekaf (3conn) | 2026-08-04T16:53:52.3114283+00:00 | 2 | 8.0 MiB / 7.9 MiB | 359.4 MB/s | 4/4 | 16,122 | 427.3s / 927,736 msg/s |
| Dekaf (3conn) | 2026-08-04T16:54:10.3193351+00:00 | 3 | 10.0 MiB / 4.0 MiB | 375.6 MB/s | 3/4 | 25,685 | 445.3s / 873,634 msg/s |
| Dekaf (3conn) | 2026-08-04T16:54:28.3381423+00:00 | 3 | 10.0 MiB / 5.3 MiB | 375.6 MB/s | 3/4 | 26,224 | 463.3s / 875,419 msg/s |
| Dekaf (3conn) | 2026-08-04T16:54:47.3535151+00:00 | 1 | 8.0 MiB / 3.9 MiB | 354.0 MB/s | 6/4 | 16,034 | 482.4s / 799,816 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:05.3639604+00:00 | 1 | 8.0 MiB / 2.8 MiB | 354.0 MB/s | 6/4 | 17,129 | 500.4s / 764,668 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:23.3776117+00:00 | 2 | 7.0 MiB / 6.1 MiB | 359.4 MB/s | 5/5 | 24,138 | 518.4s / 771,845 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:41.3868127+00:00 | 2 | 7.0 MiB / 4.0 MiB | 359.4 MB/s | 5/5 | 25,437 | 536.4s / 778,369 msg/s |
| Dekaf (3conn) | 2026-08-04T16:55:59.3999229+00:00 | 3 | 10.0 MiB / 4.3 MiB | 375.6 MB/s | 3/4 | 28,152 | 554.4s / 637,487 msg/s |
| Dekaf (3conn) | 2026-08-04T16:56:17.4070077+00:00 | 3 | 10.0 MiB / 3.5 MiB | 375.6 MB/s | 3/4 | 28,322 | 572.4s / 818,315 msg/s |
| Dekaf (3conn) | 2026-08-04T16:56:36.4248448+00:00 | 1 | 9.0 MiB / 4.6 MiB | 354.0 MB/s | 7/5 | 21,214 | 591.4s / 848,143 msg/s |
| Dekaf (3conn) | 2026-08-04T16:56:54.4432675+00:00 | 1 | 9.0 MiB / 2.2 MiB | 354.0 MB/s | 7/5 | 21,655 | 609.4s / 890,283 msg/s |
| Dekaf (3conn) | 2026-08-04T16:57:12.4546946+00:00 | 2 | 6.0 MiB / 4.3 MiB | 359.4 MB/s | 6/7 | 33,904 | 627.5s / 919,197 msg/s |
| Dekaf (3conn) | 2026-08-04T16:57:30.4725557+00:00 | 2 | 6.0 MiB / 4.9 MiB | 359.4 MB/s | 6/7 | 35,606 | 645.5s / 863,600 msg/s |
| Dekaf (3conn) | 2026-08-04T16:57:48.491282+00:00 | 3 | 8.0 MiB / 1.9 MiB | 375.6 MB/s | 6/4 | 29,833 | 663.5s / 851,124 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:06.5002599+00:00 | 3 | 8.0 MiB / 7.5 MiB | 375.6 MB/s | 6/4 | 30,626 | 681.5s / 896,323 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:25.5143342+00:00 | 1 | 9.0 MiB / 1.6 MiB | 354.0 MB/s | 7/7 | 23,187 | 700.5s / 791,994 msg/s |
| Dekaf (3conn) | 2026-08-04T16:58:43.5282229+00:00 | 1 | 9.0 MiB / 1.9 MiB | 354.0 MB/s | 7/7 | 23,597 | 718.5s / 857,644 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:01.5394089+00:00 | 2 | 7.0 MiB / 5.8 MiB | 359.4 MB/s | 7/8 | 43,632 | 736.5s / 934,764 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:19.5526508+00:00 | 2 | 7.0 MiB / 7.0 MiB | 359.4 MB/s | 7/8 | 45,009 | 754.6s / 892,612 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:37.5663935+00:00 | 3 | 9.0 MiB / 6.8 MiB | 375.6 MB/s | 7/5 | 34,139 | 772.6s / 850,471 msg/s |
| Dekaf (3conn) | 2026-08-04T16:59:55.571281+00:00 | 3 | 9.0 MiB / 3.9 MiB | 375.6 MB/s | 7/5 | 34,919 | 790.6s / 924,838 msg/s |
| Dekaf (3conn) | 2026-08-04T17:00:14.5813478+00:00 | 1 | 9.0 MiB / 8.2 MiB | 354.0 MB/s | 7/8 | 26,194 | 809.6s / 857,206 msg/s |
| Dekaf (3conn) | 2026-08-04T17:00:32.5882475+00:00 | 1 | 9.0 MiB / 4.1 MiB | 354.0 MB/s | 7/8 | 26,837 | 827.6s / 834,786 msg/s |
| Dekaf (3conn) | 2026-08-04T17:00:50.5989956+00:00 | 2 | 7.0 MiB / 1.5 MiB | 359.4 MB/s | 8/8 | 50,730 | 845.6s / 863,666 msg/s |
| Dekaf (3conn) | 2026-08-04T17:01:08.6081266+00:00 | 2 | 8.0 MiB / 3.7 MiB | 359.4 MB/s | 8/9 | 51,780 | 863.6s / 858,913 msg/s |
| Dekaf (3conn) | 2026-08-04T17:01:26.6202609+00:00 | 3 | 11.0 MiB / 2.2 MiB | 375.6 MB/s | 9/5 | 37,614 | 881.6s / 836,370 msg/s |
| Dekaf (3conn) | 2026-08-04T17:01:44.6353794+00:00 | 3 | 11.0 MiB / 4.6 MiB | 375.6 MB/s | 9/5 | 38,259 | 899.7s / 962,861 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-04T16:32:01.8336713+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-08-04T16:32:01.8584645+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-04T16:32:16.9198382+00:00 | 3 | capacity | succeeded | 15,061ms | 14.0 MiB / 5.9 MiB |
| Dekaf | 2026-08-04T16:32:19.9063878+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 12.6 MiB |
| Dekaf | 2026-08-04T16:32:19.9371334+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.9 MiB |
| Dekaf | 2026-08-04T16:32:34.9775791+00:00 | 3 | capacity | succeeded | 15,052ms | 12.0 MiB / 8.2 MiB |
| Dekaf | 2026-08-04T16:32:37.9719618+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 9.9 MiB |
| Dekaf | 2026-08-04T16:32:37.9942951+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-04T16:32:53.0453075+00:00 | 3 | capacity | succeeded | 15,053ms | 10.0 MiB / 6.7 MiB |
| Dekaf | 2026-08-04T16:32:56.0394875+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 8.6 MiB |
| Dekaf | 2026-08-04T16:32:56.0831401+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.3 MiB |
| Dekaf | 2026-08-04T16:33:11.1481455+00:00 | 3 | capacity | succeeded | 15,093ms | 8.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-04T16:33:14.1199539+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.1 MiB |
| Dekaf | 2026-08-04T16:33:14.1733355+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-04T16:33:16.7037678+00:00 | 3 | capacity | failed | 2,537ms | 8.0 MiB / 5.3 MiB |
| Dekaf | 2026-08-04T16:33:46.2920403+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-08-04T16:33:59.3998014+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:34:01.9541239+00:00 | 3 | capacity | failed | 15,067ms | 8.0 MiB / 2.3 MiB |
| Dekaf | 2026-08-04T16:34:14.447033+00:00 | 1 | capacity | failed | 15,047ms | 8.0 MiB / 5.7 MiB |
| Dekaf | 2026-08-04T16:34:32.0846301+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-04T16:34:44.5930423+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.7 MiB |
| Dekaf | 2026-08-04T16:34:49.566936+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 2.3 MiB |
| Dekaf | 2026-08-04T16:35:04.6194182+00:00 | 2 | capacity | failed | 15,052ms | 7.0 MiB / 5.0 MiB |
| Dekaf | 2026-08-04T16:35:19.303452+00:00 | 1 | capacity | failed | 3,008ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-08-04T16:35:34.7928119+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-04T16:35:49.5204217+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 1.9 MiB |
| Dekaf | 2026-08-04T16:36:02.0862678+00:00 | 3 | capacity | failed | 12,565ms | 8.0 MiB / 6.5 MiB |
| Dekaf | 2026-08-04T16:36:07.5383846+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.7 MiB |
| Dekaf | 2026-08-04T16:36:21.5306987+00:00 | 2 | capacity | failed | 1,507ms | 7.0 MiB / 6.5 MiB |
| Dekaf | 2026-08-04T16:36:25.6134037+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 2.8 MiB |
| Dekaf | 2026-08-04T16:36:32.3027218+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-04T16:36:51.6599457+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-04T16:36:59.7907153+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-04T16:37:17.491293+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-04T16:37:32.5418896+00:00 | 3 | capacity | failed | 15,050ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-04T16:37:44.9602968+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-04T16:38:30.1374264+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 4.8 MiB |
| Dekaf | 2026-08-04T16:38:48.2693972+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 1.1 MiB |
| Dekaf | 2026-08-04T16:39:31.9462839+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 2.1 MiB |
| Dekaf | 2026-08-04T16:40:17.1186988+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.5 MiB |
| Dekaf | 2026-08-04T16:40:59.8463337+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-04T16:41:33.5723545+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 5.5 MiB |
| Dekaf | 2026-08-04T16:41:45.0447349+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-04T16:41:56.0001591+00:00 | 2 | capacity | failed | 15,063ms | 7.0 MiB / 4.3 MiB |
| Dekaf | 2026-08-04T16:43:00.3943428+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-04T16:43:18.4685554+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 2.7 MiB |
| Dekaf | 2026-08-04T16:44:03.6245785+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-04T16:44:48.8723256+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 7.0 MiB |
| Dekaf | 2026-08-04T16:45:34.0498562+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-04T16:45:49.748788+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-04T16:45:57.0788271+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-04T16:46:12.1376464+00:00 | 2 | capacity | succeeded | 15,058ms | 6.0 MiB / 5.0 MiB |
| Dekaf | 2026-08-04T16:46:30.2385276+00:00 | 2 | capacity | succeeded | 15,092ms | 5.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:15.400858+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:30.3755096+00:00 | 2 | capacity | succeeded | 15,052ms | 14.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:30.5054001+00:00 | 1 | capacity | succeeded | 15,085ms | 14.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:33.5125744+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:48.4402703+00:00 | 2 | capacity | succeeded | 15,056ms | 12.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:48.5967518+00:00 | 1 | capacity | succeeded | 15,079ms | 12.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:47:51.6074508+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:06.5018633+00:00 | 2 | capacity | succeeded | 15,052ms | 10.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:06.6692627+00:00 | 3 | capacity | succeeded | 15,061ms | 10.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:09.6759237+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:24.6114274+00:00 | 2 | capacity | failed | 15,089ms | 10.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:48:24.7428253+00:00 | 3 | capacity | failed | 15,066ms | 10.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:25.0144173+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:39.957053+00:00 | 2 | capacity | failed | 15,070ms | 10.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:49:40.0960091+00:00 | 3 | capacity | failed | 15,068ms | 10.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:40.6466434+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:43.1659336+00:00 | 3 | capacity | failed | 2,519ms | 10.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:51:55.7981811+00:00 | 1 | capacity | succeeded | 15,087ms | 11.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:25.7597345+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:28.4049348+00:00 | 3 | capacity | failed | 15,078ms | 10.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:41.0072824+00:00 | 1 | capacity | failed | 15,054ms | 11.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:52:58.9489548+00:00 | 2 | capacity | failed | 15,082ms | 8.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:53:56.4127966+00:00 | 1 | capacity | succeeded | 15,079ms | 9.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:53:59.425328+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:54:14.5097399+00:00 | 1 | capacity | succeeded | 15,084ms | 8.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-08-04T16:54:17.5193185+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-04T16:54:32.5935921+00:00 | 1 | capacity | failed | 15,074ms | 8.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:32.9544316+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-08-04T16:55:48.0211604+00:00 | 1 | capacity | failed | 15,069ms | 8.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:18.1567124+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:33.0939616+00:00 | 2 | capacity | succeeded | 15,068ms | 6.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:36.1026928+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:56:51.1784323+00:00 | 2 | capacity | failed | 15,075ms | 6.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:14.8323304+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:29.9028297+00:00 | 3 | capacity | succeeded | 15,070ms | 9.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:47.9866236+00:00 | 3 | capacity | succeeded | 15,058ms | 8.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-08-04T16:57:51.5033727+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-08-04T16:58:06.5691178+00:00 | 2 | capacity | succeeded | 15,065ms | 7.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-08-04T16:58:33.1919206+00:00 | 3 | capacity | succeeded | 15,070ms | 9.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-08-04T16:58:36.7057073+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-08-04T16:58:51.7851339+00:00 | 2 | capacity | failed | 15,079ms | 7.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-04T16:59:18.4757275+00:00 | 3 | capacity | failed | 15,073ms | 9.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-08-04T17:00:07.1992464+00:00 | 2 | capacity | succeeded | 15,078ms | 8.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:00:33.8672746+00:00 | 3 | capacity | succeeded | 15,109ms | 10.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-08-04T17:00:52.4737074+00:00 | 2 | capacity | failed | 15,083ms | 8.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:01:19.101158+00:00 | 3 | capacity | succeeded | 15,052ms | 11.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-04T17:01:37.7112254+00:00 | 2 | capacity | failed | 15,085ms | 8.0 MiB / 4.4 MiB |
*98 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 5 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 9 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 8 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 23 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 107 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 216 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 267 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 254 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 420 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 729 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,068 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,103 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 802 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 420 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 105 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 5 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 8 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 8 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 13 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 53 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 226 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 428 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 551 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 569 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 847 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 1,501 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 1,961 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 2,100 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,407 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 540 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 107 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 3 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 4 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 6 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 4 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 62 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 137 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 293 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 338 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 346 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 562 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 923 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 1,346 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 1,450 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 1,061 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 474 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 92 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 7 |
| Dekaf (3conn) | 3 | 131.072–262.144ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 24 |
| Dekaf | 1 | 0.002–0.004ms | 14 |
| Dekaf | 1 | 0.004–0.008ms | 48 |
| Dekaf | 1 | 0.008–0.016ms | 159 |
| Dekaf | 1 | 0.016–0.032ms | 515 |
| Dekaf | 1 | 0.032–0.064ms | 968 |
| Dekaf | 1 | 0.064–0.128ms | 1,387 |
| Dekaf | 1 | 0.128–0.256ms | 1,696 |
| Dekaf | 1 | 0.256–0.512ms | 2,786 |
| Dekaf | 1 | 0.512–1.024ms | 4,119 |
| Dekaf | 1 | 1.024–2.048ms | 4,127 |
| Dekaf | 1 | 2.048–4.096ms | 2,899 |
| Dekaf | 1 | 4.096–8.192ms | 1,571 |
| Dekaf | 1 | 8.192–16.384ms | 631 |
| Dekaf | 1 | 16.384–32.768ms | 262 |
| Dekaf | 1 | 32.768–65.536ms | 21 |
| Dekaf | 1 | 65.536–131.072ms | 1 |
| Dekaf | 2 | 0.001–0.002ms | 36 |
| Dekaf | 2 | 0.002–0.004ms | 44 |
| Dekaf | 2 | 0.004–0.008ms | 90 |
| Dekaf | 2 | 0.008–0.016ms | 259 |
| Dekaf | 2 | 0.016–0.032ms | 855 |
| Dekaf | 2 | 0.032–0.064ms | 1,749 |
| Dekaf | 2 | 0.064–0.128ms | 2,548 |
| Dekaf | 2 | 0.128–0.256ms | 3,275 |
| Dekaf | 2 | 0.256–0.512ms | 5,433 |
| Dekaf | 2 | 0.512–1.024ms | 7,715 |
| Dekaf | 2 | 1.024–2.048ms | 7,634 |
| Dekaf | 2 | 2.048–4.096ms | 5,107 |
| Dekaf | 2 | 4.096–8.192ms | 2,356 |
| Dekaf | 2 | 8.192–16.384ms | 950 |
| Dekaf | 2 | 16.384–32.768ms | 412 |
| Dekaf | 2 | 32.768–65.536ms | 36 |
| Dekaf | 3 | 0.001–0.002ms | 10 |
| Dekaf | 3 | 0.002–0.004ms | 7 |
| Dekaf | 3 | 0.004–0.008ms | 20 |
| Dekaf | 3 | 0.008–0.016ms | 48 |
| Dekaf | 3 | 0.016–0.032ms | 173 |
| Dekaf | 3 | 0.032–0.064ms | 317 |
| Dekaf | 3 | 0.064–0.128ms | 434 |
| Dekaf | 3 | 0.128–0.256ms | 514 |
| Dekaf | 3 | 0.256–0.512ms | 841 |
| Dekaf | 3 | 0.512–1.024ms | 1,264 |
| Dekaf | 3 | 1.024–2.048ms | 1,316 |
| Dekaf | 3 | 2.048–4.096ms | 949 |
| Dekaf | 3 | 4.096–8.192ms | 465 |
| Dekaf | 3 | 8.192–16.384ms | 149 |
| Dekaf | 3 | 16.384–32.768ms | 43 |
| Dekaf | 3 | 32.768–65.536ms | 6 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 4,000 | 2026-08-04T16:16:31.5297007+00:00 | 129.3ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 5,000 | 2026-08-04T16:16:31.5319886+00:00 | 132.1ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 3,000 | 2026-08-04T16:16:31.53264+00:00 | 121.0ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 6,000 | 2026-08-04T16:16:31.5369277+00:00 | 127.2ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 7,000 | 2026-08-04T16:16:31.5463375+00:00 | 121.9ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 8,000 | 2026-08-04T16:16:31.5508951+00:00 | 117.4ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 9,000 | 2026-08-04T16:16:31.5557817+00:00 | 139.5ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 11,000 | 2026-08-04T16:16:31.6043717+00:00 | 110.8ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 17,000 | 2026-08-04T16:16:31.6691391+00:00 | 111.3ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 101,000 | 2026-08-04T16:16:32.1952712+00:00 | 118.4ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 104,000 | 2026-08-04T16:16:32.2062853+00:00 | 103.3ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 111,000 | 2026-08-04T16:16:32.3146596+00:00 | 122.3ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 114,000 | 2026-08-04T16:16:32.3207916+00:00 | 129.7ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 115,000 | 2026-08-04T16:16:32.3331611+00:00 | 122.1ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 116,000 | 2026-08-04T16:16:32.3460153+00:00 | 109.3ms | GC pause | - | - | 1.0s / 144,165 msg/s | Gen2 +0 / pause +16.7ms |
| Confluent | 181,000 | 2026-08-04T16:16:32.6159647+00:00 | 126.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 184,000 | 2026-08-04T16:16:32.6206909+00:00 | 108.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 187,000 | 2026-08-04T16:16:32.6272152+00:00 | 159.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 188,000 | 2026-08-04T16:16:32.6280884+00:00 | 158.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 189,000 | 2026-08-04T16:16:32.629389+00:00 | 102.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 190,000 | 2026-08-04T16:16:32.6331525+00:00 | 120.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 191,000 | 2026-08-04T16:16:32.6356952+00:00 | 151.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 193,000 | 2026-08-04T16:16:32.6444593+00:00 | 128.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 194,000 | 2026-08-04T16:16:32.6460924+00:00 | 154.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 195,000 | 2026-08-04T16:16:32.6469689+00:00 | 109.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 196,000 | 2026-08-04T16:16:32.649325+00:00 | 107.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 197,000 | 2026-08-04T16:16:32.6500593+00:00 | 155.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 198,000 | 2026-08-04T16:16:32.6551913+00:00 | 150.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 199,000 | 2026-08-04T16:16:32.6561259+00:00 | 100.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 200,000 | 2026-08-04T16:16:32.660331+00:00 | 128.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 201,000 | 2026-08-04T16:16:32.6613546+00:00 | 176.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 202,000 | 2026-08-04T16:16:32.6624096+00:00 | 105.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 203,000 | 2026-08-04T16:16:32.6653173+00:00 | 123.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 204,000 | 2026-08-04T16:16:32.6663301+00:00 | 153.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 205,000 | 2026-08-04T16:16:32.6670002+00:00 | 107.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 206,000 | 2026-08-04T16:16:32.6699338+00:00 | 104.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 207,000 | 2026-08-04T16:16:32.6706718+00:00 | 178.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 208,000 | 2026-08-04T16:16:32.6734814+00:00 | 175.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 209,000 | 2026-08-04T16:16:32.6746246+00:00 | 117.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 210,000 | 2026-08-04T16:16:32.6787864+00:00 | 163.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 211,000 | 2026-08-04T16:16:32.6798626+00:00 | 199.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 212,000 | 2026-08-04T16:16:32.6805307+00:00 | 108.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 213,000 | 2026-08-04T16:16:32.6833819+00:00 | 158.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 214,000 | 2026-08-04T16:16:32.6847532+00:00 | 179.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 215,000 | 2026-08-04T16:16:32.6871678+00:00 | 115.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 216,000 | 2026-08-04T16:16:32.6877339+00:00 | 114.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 217,000 | 2026-08-04T16:16:32.6922188+00:00 | 208.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 218,000 | 2026-08-04T16:16:32.6935281+00:00 | 207.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 219,000 | 2026-08-04T16:16:32.6948751+00:00 | 129.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 220,000 | 2026-08-04T16:16:32.6959087+00:00 | 168.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 221,000 | 2026-08-04T16:16:32.7039394+00:00 | 197.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 222,000 | 2026-08-04T16:16:32.7107813+00:00 | 104.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 223,000 | 2026-08-04T16:16:32.7123594+00:00 | 191.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 224,000 | 2026-08-04T16:16:32.7139878+00:00 | 196.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 225,000 | 2026-08-04T16:16:32.7177092+00:00 | 134.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 226,000 | 2026-08-04T16:16:32.7194025+00:00 | 132.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 227,000 | 2026-08-04T16:16:32.7246872+00:00 | 212.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 228,000 | 2026-08-04T16:16:32.7297989+00:00 | 207.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 229,000 | 2026-08-04T16:16:32.7343445+00:00 | 118.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 230,000 | 2026-08-04T16:16:32.7507363+00:00 | 171.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 231,000 | 2026-08-04T16:16:32.7533239+00:00 | 184.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 232,000 | 2026-08-04T16:16:32.7601459+00:00 | 107.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 233,000 | 2026-08-04T16:16:32.7622793+00:00 | 160.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 234,000 | 2026-08-04T16:16:32.7639388+00:00 | 155.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 235,000 | 2026-08-04T16:16:32.7710538+00:00 | 109.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 236,000 | 2026-08-04T16:16:32.7754841+00:00 | 104.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 237,000 | 2026-08-04T16:16:32.7942752+00:00 | 164.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 238,000 | 2026-08-04T16:16:32.8029591+00:00 | 155.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 239,000 | 2026-08-04T16:16:32.8042028+00:00 | 117.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 240,000 | 2026-08-04T16:16:32.80498+00:00 | 166.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 241,000 | 2026-08-04T16:16:32.8154747+00:00 | 156.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 242,000 | 2026-08-04T16:16:32.8163013+00:00 | 104.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 243,000 | 2026-08-04T16:16:32.8380322+00:00 | 134.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 244,000 | 2026-08-04T16:16:32.8426334+00:00 | 126.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 247,000 | 2026-08-04T16:16:32.8505592+00:00 | 138.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 248,000 | 2026-08-04T16:16:32.8529071+00:00 | 135.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 249,000 | 2026-08-04T16:16:32.8557427+00:00 | 117.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 250,000 | 2026-08-04T16:16:32.8594792+00:00 | 118.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 251,000 | 2026-08-04T16:16:32.8649444+00:00 | 161.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 252,000 | 2026-08-04T16:16:32.8658846+00:00 | 112.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 253,000 | 2026-08-04T16:16:32.8715354+00:00 | 117.4ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 254,000 | 2026-08-04T16:16:32.8771162+00:00 | 119.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 255,000 | 2026-08-04T16:16:32.8795609+00:00 | 108.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 290,000 | 2026-08-04T16:16:33.1314699+00:00 | 123.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 292,000 | 2026-08-04T16:16:33.1362601+00:00 | 131.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 293,000 | 2026-08-04T16:16:33.1372457+00:00 | 297.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 295,000 | 2026-08-04T16:16:33.1416202+00:00 | 152.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 296,000 | 2026-08-04T16:16:33.1423223+00:00 | 152.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 299,000 | 2026-08-04T16:16:33.1483169+00:00 | 146.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 300,000 | 2026-08-04T16:16:33.1490916+00:00 | 288.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 302,000 | 2026-08-04T16:16:33.1512207+00:00 | 154.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 303,000 | 2026-08-04T16:16:33.1548699+00:00 | 285.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 305,000 | 2026-08-04T16:16:33.1577349+00:00 | 278.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 306,000 | 2026-08-04T16:16:33.1620541+00:00 | 274.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 309,000 | 2026-08-04T16:16:33.1665071+00:00 | 270.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 310,000 | 2026-08-04T16:16:33.1696474+00:00 | 278.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 312,000 | 2026-08-04T16:16:33.1741527+00:00 | 262.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 313,000 | 2026-08-04T16:16:33.1747999+00:00 | 273.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 315,000 | 2026-08-04T16:16:33.1762267+00:00 | 261.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 316,000 | 2026-08-04T16:16:33.1783318+00:00 | 259.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 319,000 | 2026-08-04T16:16:33.1820713+00:00 | 256.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 320,000 | 2026-08-04T16:16:33.1847796+00:00 | 293.5ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 322,000 | 2026-08-04T16:16:33.1884529+00:00 | 251.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 323,000 | 2026-08-04T16:16:33.1891206+00:00 | 312.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 325,000 | 2026-08-04T16:16:33.1943412+00:00 | 245.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 326,000 | 2026-08-04T16:16:33.1986912+00:00 | 240.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 329,000 | 2026-08-04T16:16:33.2086933+00:00 | 231.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 330,000 | 2026-08-04T16:16:33.2093895+00:00 | 301.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 332,000 | 2026-08-04T16:16:33.2129062+00:00 | 228.0ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 333,000 | 2026-08-04T16:16:33.2157844+00:00 | 306.1ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 335,000 | 2026-08-04T16:16:33.2240123+00:00 | 216.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 336,000 | 2026-08-04T16:16:33.2251091+00:00 | 218.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 339,000 | 2026-08-04T16:16:33.2362984+00:00 | 207.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 340,000 | 2026-08-04T16:16:33.2376532+00:00 | 290.1ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 342,000 | 2026-08-04T16:16:33.2408655+00:00 | 237.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 343,000 | 2026-08-04T16:16:33.244751+00:00 | 283.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 345,000 | 2026-08-04T16:16:33.2468133+00:00 | 209.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 346,000 | 2026-08-04T16:16:33.2519162+00:00 | 204.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 347,000 | 2026-08-04T16:16:33.2527425+00:00 | 181.8ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 348,000 | 2026-08-04T16:16:33.2547789+00:00 | 179.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 349,000 | 2026-08-04T16:16:33.2557026+00:00 | 223.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 350,000 | 2026-08-04T16:16:33.2659015+00:00 | 273.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 351,000 | 2026-08-04T16:16:33.2668785+00:00 | 170.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 352,000 | 2026-08-04T16:16:33.2686722+00:00 | 258.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 353,000 | 2026-08-04T16:16:33.2698382+00:00 | 290.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 354,000 | 2026-08-04T16:16:33.2740646+00:00 | 162.6ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 355,000 | 2026-08-04T16:16:33.2761254+00:00 | 226.7ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 356,000 | 2026-08-04T16:16:33.2787483+00:00 | 224.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 357,000 | 2026-08-04T16:16:33.2797846+00:00 | 159.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 358,000 | 2026-08-04T16:16:33.2837917+00:00 | 155.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 359,000 | 2026-08-04T16:16:33.285091+00:00 | 229.1ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 360,000 | 2026-08-04T16:16:33.2907825+00:00 | 277.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 361,000 | 2026-08-04T16:16:33.2918305+00:00 | 149.3ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 362,000 | 2026-08-04T16:16:33.2947509+00:00 | 265.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 363,000 | 2026-08-04T16:16:33.2955998+00:00 | 287.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 364,000 | 2026-08-04T16:16:33.3001166+00:00 | 140.2ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 365,000 | 2026-08-04T16:16:33.300864+00:00 | 236.3ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 366,000 | 2026-08-04T16:16:33.306153+00:00 | 231.1ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 367,000 | 2026-08-04T16:16:33.3069549+00:00 | 137.9ms | GC pause | - | - | 2.0s / 233,062 msg/s | Gen2 +0 / pause +43.9ms |
| Confluent | 370,000 | 2026-08-04T16:16:33.4640212+00:00 | 132.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 372,000 | 2026-08-04T16:16:33.4743307+00:00 | 114.1ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 373,000 | 2026-08-04T16:16:33.4811527+00:00 | 116.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 380,000 | 2026-08-04T16:16:33.5257063+00:00 | 108.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 383,000 | 2026-08-04T16:16:33.5343662+00:00 | 100.4ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 390,000 | 2026-08-04T16:16:33.5506747+00:00 | 108.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 392,000 | 2026-08-04T16:16:33.552515+00:00 | 105.1ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 393,000 | 2026-08-04T16:16:33.5566012+00:00 | 130.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 400,000 | 2026-08-04T16:16:33.5672967+00:00 | 151.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 402,000 | 2026-08-04T16:16:33.5693701+00:00 | 105.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 403,000 | 2026-08-04T16:16:33.5698357+00:00 | 148.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 410,000 | 2026-08-04T16:16:33.5897236+00:00 | 145.4ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 412,000 | 2026-08-04T16:16:33.5997824+00:00 | 118.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 413,000 | 2026-08-04T16:16:33.6005585+00:00 | 134.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 420,000 | 2026-08-04T16:16:33.6182424+00:00 | 132.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 422,000 | 2026-08-04T16:16:33.6208717+00:00 | 115.5ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 423,000 | 2026-08-04T16:16:33.6229654+00:00 | 152.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 430,000 | 2026-08-04T16:16:33.6366877+00:00 | 145.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 432,000 | 2026-08-04T16:16:33.6406797+00:00 | 120.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 433,000 | 2026-08-04T16:16:33.6415039+00:00 | 150.3ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 435,000 | 2026-08-04T16:16:33.6454164+00:00 | 106.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 436,000 | 2026-08-04T16:16:33.6466791+00:00 | 104.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 439,000 | 2026-08-04T16:16:33.6537587+00:00 | 104.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 440,000 | 2026-08-04T16:16:33.6580854+00:00 | 157.5ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 442,000 | 2026-08-04T16:16:33.6620086+00:00 | 114.3ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 443,000 | 2026-08-04T16:16:33.6646916+00:00 | 151.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 450,000 | 2026-08-04T16:16:33.6849678+00:00 | 153.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 453,000 | 2026-08-04T16:16:33.6928097+00:00 | 153.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 460,000 | 2026-08-04T16:16:33.7116284+00:00 | 219.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 463,000 | 2026-08-04T16:16:33.7232778+00:00 | 213.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 470,000 | 2026-08-04T16:16:33.7364658+00:00 | 218.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 473,000 | 2026-08-04T16:16:33.7443673+00:00 | 210.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 480,000 | 2026-08-04T16:16:33.7562549+00:00 | 244.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 483,000 | 2026-08-04T16:16:33.7612533+00:00 | 239.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 490,000 | 2026-08-04T16:16:33.7773022+00:00 | 234.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 492,000 | 2026-08-04T16:16:33.7812414+00:00 | 149.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 493,000 | 2026-08-04T16:16:33.7850583+00:00 | 236.5ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 496,000 | 2026-08-04T16:16:33.7882772+00:00 | 140.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 499,000 | 2026-08-04T16:16:33.7975914+00:00 | 131.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 500,000 | 2026-08-04T16:16:33.7990705+00:00 | 233.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 502,000 | 2026-08-04T16:16:33.8136057+00:00 | 124.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 503,000 | 2026-08-04T16:16:33.815041+00:00 | 218.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 505,000 | 2026-08-04T16:16:33.8174004+00:00 | 113.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 506,000 | 2026-08-04T16:16:33.8207506+00:00 | 110.3ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 507,000 | 2026-08-04T16:16:33.8249065+00:00 | 104.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 508,000 | 2026-08-04T16:16:33.8289786+00:00 | 100.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 509,000 | 2026-08-04T16:16:33.8297311+00:00 | 105.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 510,000 | 2026-08-04T16:16:33.8308733+00:00 | 216.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 512,000 | 2026-08-04T16:16:33.8335947+00:00 | 112.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 513,000 | 2026-08-04T16:16:33.8343438+00:00 | 213.3ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 515,000 | 2026-08-04T16:16:33.8414544+00:00 | 100.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 520,000 | 2026-08-04T16:16:33.9367184+00:00 | 125.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 523,000 | 2026-08-04T16:16:33.9444937+00:00 | 129.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 530,000 | 2026-08-04T16:16:33.9561572+00:00 | 142.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 533,000 | 2026-08-04T16:16:33.9585666+00:00 | 143.5ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 540,000 | 2026-08-04T16:16:33.9735093+00:00 | 136.4ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 543,000 | 2026-08-04T16:16:33.9790019+00:00 | 131.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 550,000 | 2026-08-04T16:16:33.9884508+00:00 | 131.2ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 553,000 | 2026-08-04T16:16:33.9944092+00:00 | 132.7ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 560,000 | 2026-08-04T16:16:34.0031207+00:00 | 132.4ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 563,000 | 2026-08-04T16:16:34.0102707+00:00 | 138.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 570,000 | 2026-08-04T16:16:34.0220648+00:00 | 168.9ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 573,000 | 2026-08-04T16:16:34.0353505+00:00 | 155.8ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 580,000 | 2026-08-04T16:16:34.0729566+00:00 | 158.4ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 583,000 | 2026-08-04T16:16:34.1011244+00:00 | 130.4ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 590,000 | 2026-08-04T16:16:34.1127391+00:00 | 132.0ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 593,000 | 2026-08-04T16:16:34.1163555+00:00 | 144.1ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 600,000 | 2026-08-04T16:16:34.1254117+00:00 | 141.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 603,000 | 2026-08-04T16:16:34.1336175+00:00 | 133.5ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 610,000 | 2026-08-04T16:16:34.144046+00:00 | 148.5ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 613,000 | 2026-08-04T16:16:34.1690732+00:00 | 123.6ms | GC pause | - | - | 3.0s / 378,814 msg/s | Gen2 +0 / pause +37.1ms |
| Confluent | 1,462,000 | 2026-08-04T16:16:36.1891715+00:00 | 100.1ms | GC pause | - | - | 5.0s / 409,825 msg/s | Gen2 +0 / pause +85.8ms |
| Confluent | 2,066,000 | 2026-08-04T16:16:37.5899343+00:00 | 100.2ms | GC pause | - | - | 7.0s / 478,110 msg/s | Gen2 +0 / pause +113.0ms |
| Confluent | 5,338,000 | 2026-08-04T16:16:44.1023668+00:00 | 109.2ms | GC pause | - | - | 13.0s / 449,941 msg/s | Gen2 +0 / pause +80.2ms |
| Confluent | 5,341,000 | 2026-08-04T16:16:44.1111866+00:00 | 100.7ms | GC pause | - | - | 13.0s / 449,941 msg/s | Gen2 +0 / pause +80.2ms |
| Confluent | 5,347,000 | 2026-08-04T16:16:44.1232759+00:00 | 103.2ms | GC pause | - | - | 13.0s / 449,941 msg/s | Gen2 +0 / pause +80.2ms |
| Confluent | 5,348,000 | 2026-08-04T16:16:44.1260832+00:00 | 100.5ms | GC pause | - | - | 13.0s / 449,941 msg/s | Gen2 +0 / pause +80.2ms |
| Confluent | 5,351,000 | 2026-08-04T16:16:44.1303312+00:00 | 106.1ms | GC pause | - | - | 13.0s / 449,941 msg/s | Gen2 +0 / pause +80.2ms |
| Confluent | 5,357,000 | 2026-08-04T16:16:44.1436233+00:00 | 100.3ms | GC pause | - | - | 13.0s / 449,941 msg/s | Gen2 +0 / pause +80.2ms |
| Confluent | 313,520,000 | 2026-08-04T16:24:43.1565242+00:00 | 106.0ms | GC pause | - | - | 492.4s / 550,096 msg/s | Gen2 +0 / pause +242.6ms |
| Confluent | 313,521,000 | 2026-08-04T16:24:43.1586329+00:00 | 104.8ms | GC pause | - | - | 492.4s / 550,096 msg/s | Gen2 +0 / pause +242.6ms |
| Confluent | 313,522,000 | 2026-08-04T16:24:43.1595013+00:00 | 103.0ms | GC pause | - | - | 492.4s / 550,096 msg/s | Gen2 +0 / pause +242.6ms |
| Confluent | 313,523,000 | 2026-08-04T16:24:43.1601385+00:00 | 102.6ms | GC pause | - | - | 492.4s / 550,096 msg/s | Gen2 +0 / pause +242.6ms |
| Confluent | 363,489,000 | 2026-08-04T16:26:07.1117801+00:00 | 155.7ms | GC pause | - | - | 576.5s / 363,114 msg/s | Gen2 +0 / pause +283.0ms |
| Confluent | 363,492,000 | 2026-08-04T16:26:07.1544986+00:00 | 129.5ms | GC pause | - | - | 576.5s / 363,114 msg/s | Gen2 +0 / pause +283.0ms |
| Confluent | 363,493,000 | 2026-08-04T16:26:07.155678+00:00 | 110.9ms | GC pause | - | - | 576.5s / 363,114 msg/s | Gen2 +0 / pause +283.0ms |
| Confluent | 363,494,000 | 2026-08-04T16:26:07.1567468+00:00 | 110.0ms | GC pause | - | - | 576.5s / 363,114 msg/s | Gen2 +0 / pause +283.0ms |
| Confluent | 363,495,000 | 2026-08-04T16:26:07.1758314+00:00 | 131.6ms | GC pause | - | - | 576.5s / 363,114 msg/s | Gen2 +0 / pause +283.0ms |
| Dekaf | 1,571,000 | 2026-08-04T16:31:34.1305642+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,572,000 | 2026-08-04T16:31:34.1317659+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,581,000 | 2026-08-04T16:31:34.1401405+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,582,000 | 2026-08-04T16:31:34.142491+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,591,000 | 2026-08-04T16:31:34.1577242+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,592,000 | 2026-08-04T16:31:34.1592172+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,601,000 | 2026-08-04T16:31:34.1672955+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,602,000 | 2026-08-04T16:31:34.1678977+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 839,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,307,000 | 2026-08-04T16:31:36.115728+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 886,427 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,327,000 | 2026-08-04T16:31:36.1404639+00:00 | 122.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 886,427 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,767,000 | 2026-08-04T16:31:36.6354856+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 886,334 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,777,000 | 2026-08-04T16:31:36.6456922+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 886,334 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,132,000 | 2026-08-04T16:31:38.175343+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 881,210 msg/s | Gen2 +0 / pause +1.9ms |
| Dekaf | 6,021,000 | 2026-08-04T16:31:39.2219954+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 868,306 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,032,000 | 2026-08-04T16:31:39.2389599+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 868,306 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,862,000 | 2026-08-04T16:31:40.1188506+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 883,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,872,000 | 2026-08-04T16:31:40.1296431+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 883,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,882,000 | 2026-08-04T16:31:40.1388838+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 883,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,891,000 | 2026-08-04T16:31:40.1488412+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 883,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,892,000 | 2026-08-04T16:31:40.1497144+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 883,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,902,000 | 2026-08-04T16:31:40.1642909+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 883,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,291,000 | 2026-08-04T16:31:40.6373894+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 904,419 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,311,000 | 2026-08-04T16:31:40.6566431+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 904,419 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,501,000 | 2026-08-04T16:31:43.1216027+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 875,533 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,512,000 | 2026-08-04T16:31:43.1318546+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 875,533 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,521,000 | 2026-08-04T16:31:43.1435634+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 875,533 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,522,000 | 2026-08-04T16:31:43.1447373+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 875,533 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,381,000 | 2026-08-04T16:31:44.1409995+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 850,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,391,000 | 2026-08-04T16:31:44.1511643+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 850,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,401,000 | 2026-08-04T16:31:44.1614659+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 850,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,402,000 | 2026-08-04T16:31:44.1620264+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 850,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,411,000 | 2026-08-04T16:31:44.1694376+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 850,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,412,000 | 2026-08-04T16:31:44.1699371+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 850,433 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,261,000 | 2026-08-04T16:31:45.1245294+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,272,000 | 2026-08-04T16:31:45.1357379+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,281,000 | 2026-08-04T16:31:45.1439285+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,282,000 | 2026-08-04T16:31:45.1452763+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,291,000 | 2026-08-04T16:31:45.1561644+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,301,000 | 2026-08-04T16:31:45.1656878+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,681,000 | 2026-08-04T16:31:45.5976796+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,682,000 | 2026-08-04T16:31:45.5987592+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 895,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,701,000 | 2026-08-04T16:31:45.6208398+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 11,702,000 | 2026-08-04T16:31:45.6213938+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 11,712,000 | 2026-08-04T16:31:45.6326789+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 11,721,000 | 2026-08-04T16:31:45.6434401+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 11,731,000 | 2026-08-04T16:31:45.6561645+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,151,000 | 2026-08-04T16:31:46.114891+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,171,000 | 2026-08-04T16:31:46.1309626+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,182,000 | 2026-08-04T16:31:46.1406522+00:00 | 126.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,191,000 | 2026-08-04T16:31:46.1508561+00:00 | 123.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,192,000 | 2026-08-04T16:31:46.1517754+00:00 | 123.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,201,000 | 2026-08-04T16:31:46.1618835+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,953 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,571,000 | 2026-08-04T16:31:46.6307958+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,572,000 | 2026-08-04T16:31:46.6312085+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,591,000 | 2026-08-04T16:31:46.6488043+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,611,000 | 2026-08-04T16:31:46.6709416+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 12,981,000 | 2026-08-04T16:31:47.1068599+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,991,000 | 2026-08-04T16:31:47.1168916+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,001,000 | 2026-08-04T16:31:47.1247615+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,002,000 | 2026-08-04T16:31:47.1251735+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,012,000 | 2026-08-04T16:31:47.1419674+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,021,000 | 2026-08-04T16:31:47.1528151+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,022,000 | 2026-08-04T16:31:47.156495+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,031,000 | 2026-08-04T16:31:47.1648122+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 901,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,494,000 | 2026-08-04T16:31:47.6574843+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,504,000 | 2026-08-04T16:31:47.6633964+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,934,000 | 2026-08-04T16:31:48.141417+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,946,000 | 2026-08-04T16:31:48.1517125+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,954,000 | 2026-08-04T16:31:48.15928+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,956,000 | 2026-08-04T16:31:48.1614421+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,964,000 | 2026-08-04T16:31:48.1808593+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,966,000 | 2026-08-04T16:31:48.1845025+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 888,268 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,861,000 | 2026-08-04T16:31:49.1667939+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 871,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,871,000 | 2026-08-04T16:31:49.1730258+00:00 | 124.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 871,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,872,000 | 2026-08-04T16:31:49.1734141+00:00 | 123.7ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 871,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,881,000 | 2026-08-04T16:31:49.1987363+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 871,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,662,000 | 2026-08-04T16:31:50.1311481+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,671,000 | 2026-08-04T16:31:50.1400387+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,672,000 | 2026-08-04T16:31:50.1418808+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,681,000 | 2026-08-04T16:31:50.1505818+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,691,000 | 2026-08-04T16:31:50.1615847+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,692,000 | 2026-08-04T16:31:50.1621936+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,701,000 | 2026-08-04T16:31:50.1749137+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 841,652 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,531,000 | 2026-08-04T16:31:51.1209901+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,532,000 | 2026-08-04T16:31:51.121914+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,552,000 | 2026-08-04T16:31:51.1516558+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,561,000 | 2026-08-04T16:31:51.1600679+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,562,000 | 2026-08-04T16:31:51.1610254+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,571,000 | 2026-08-04T16:31:51.167375+00:00 | 138.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,582,000 | 2026-08-04T16:31:51.1788778+00:00 | 141.4ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,591,000 | 2026-08-04T16:31:51.2130823+00:00 | 118.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,612,000 | 2026-08-04T16:31:51.2466264+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,622,000 | 2026-08-04T16:31:51.2594124+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 847,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,352,000 | 2026-08-04T16:31:52.1371955+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,362,000 | 2026-08-04T16:31:52.147776+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,367,000 | 2026-08-04T16:31:52.1531058+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,374,000 | 2026-08-04T16:31:52.1614865+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,376,000 | 2026-08-04T16:31:52.1636175+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,381,000 | 2026-08-04T16:31:52.168728+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,386,000 | 2026-08-04T16:31:52.172771+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,387,000 | 2026-08-04T16:31:52.1739526+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,391,000 | 2026-08-04T16:31:52.1766197+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,392,000 | 2026-08-04T16:31:52.1771202+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,396,000 | 2026-08-04T16:31:52.190688+00:00 | 109.7ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,404,000 | 2026-08-04T16:31:52.2076038+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,504,000 | 2026-08-04T16:31:52.3485542+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,524,000 | 2026-08-04T16:31:52.3747334+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,614,000 | 2026-08-04T16:31:52.4952844+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,634,000 | 2026-08-04T16:31:52.5158757+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,636,000 | 2026-08-04T16:31:52.5179148+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,646,000 | 2026-08-04T16:31:52.5279936+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,654,000 | 2026-08-04T16:31:52.5372242+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,664,000 | 2026-08-04T16:31:52.5522162+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,676,000 | 2026-08-04T16:31:52.5726166+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,686,000 | 2026-08-04T16:31:52.5873244+00:00 | 136.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,696,000 | 2026-08-04T16:31:52.6063959+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 763,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,001,000 | 2026-08-04T16:31:54.1398485+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,002,000 | 2026-08-04T16:31:54.1411945+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,007,000 | 2026-08-04T16:31:54.1472774+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,017,000 | 2026-08-04T16:31:54.1644152+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,021,000 | 2026-08-04T16:31:54.1682587+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,022,000 | 2026-08-04T16:31:54.1692752+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,027,000 | 2026-08-04T16:31:54.172826+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 874,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,451,000 | 2026-08-04T16:31:54.6672258+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,832,000 | 2026-08-04T16:31:55.0992742+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,852,000 | 2026-08-04T16:31:55.1183179+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,871,000 | 2026-08-04T16:31:55.1423174+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,872,000 | 2026-08-04T16:31:55.1440112+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,881,000 | 2026-08-04T16:31:55.1539053+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,891,000 | 2026-08-04T16:31:55.16945+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 855,647 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,661,000 | 2026-08-04T16:31:56.1045785+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 819,080 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,671,000 | 2026-08-04T16:31:56.1149381+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 819,080 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,682,000 | 2026-08-04T16:31:56.1278222+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 819,080 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,702,000 | 2026-08-04T16:31:56.1493168+00:00 | 122.4ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 819,080 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,712,000 | 2026-08-04T16:31:56.15876+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 819,080 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,421,000 | 2026-08-04T16:31:58.1628955+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,422,000 | 2026-08-04T16:31:58.163536+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,431,000 | 2026-08-04T16:31:58.16947+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,437,000 | 2026-08-04T16:31:58.1729008+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,441,000 | 2026-08-04T16:31:58.1751871+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,442,000 | 2026-08-04T16:31:58.185578+00:00 | 109.7ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,781,000 | 2026-08-04T16:31:58.6113274+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 843,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,271,000 | 2026-08-04T16:31:59.1226894+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,272,000 | 2026-08-04T16:31:59.1236629+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,281,000 | 2026-08-04T16:31:59.1311961+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,282,000 | 2026-08-04T16:31:59.1318906+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,291,000 | 2026-08-04T16:31:59.1411335+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,292,000 | 2026-08-04T16:31:59.1419376+00:00 | 129.6ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,311,000 | 2026-08-04T16:31:59.1613957+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,721,000 | 2026-08-04T16:31:59.6332219+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,722,000 | 2026-08-04T16:31:59.6336293+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 923,639 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 23,741,000 | 2026-08-04T16:31:59.6538954+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 24,142,000 | 2026-08-04T16:32:00.0976682+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,157,000 | 2026-08-04T16:32:00.1173853+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,171,000 | 2026-08-04T16:32:00.138342+00:00 | 118.7ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,172,000 | 2026-08-04T16:32:00.1389083+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,182,000 | 2026-08-04T16:32:00.1482924+00:00 | 113.0ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,191,000 | 2026-08-04T16:32:00.1594283+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,192,000 | 2026-08-04T16:32:00.1606753+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 825,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,551,000 | 2026-08-04T16:32:00.6323002+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,571,000 | 2026-08-04T16:32:00.6560254+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,572,000 | 2026-08-04T16:32:00.6569209+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,971,000 | 2026-08-04T16:32:01.1096736+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,972,000 | 2026-08-04T16:32:01.1104652+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,991,000 | 2026-08-04T16:32:01.1328306+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,001,000 | 2026-08-04T16:32:01.142141+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,011,000 | 2026-08-04T16:32:01.1516378+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,022,000 | 2026-08-04T16:32:01.1638226+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,032,000 | 2026-08-04T16:32:01.1856721+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,391,000 | 2026-08-04T16:32:01.6273208+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,392,000 | 2026-08-04T16:32:01.6278984+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 847,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,401,000 | 2026-08-04T16:32:01.6358349+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,402,000 | 2026-08-04T16:32:01.6364476+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,821,000 | 2026-08-04T16:32:02.1108554+00:00 | 107.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,822,000 | 2026-08-04T16:32:02.1118864+00:00 | 106.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,831,000 | 2026-08-04T16:32:02.1202742+00:00 | 103.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,832,000 | 2026-08-04T16:32:02.1207662+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,841,000 | 2026-08-04T16:32:02.1328536+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,842,000 | 2026-08-04T16:32:02.1335108+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,851,000 | 2026-08-04T16:32:02.1433313+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 31.0s / 832,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,112,000 | 2026-08-04T16:32:03.627029+00:00 | 100.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 32.0s / 880,266 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,561,000 | 2026-08-04T16:32:04.1702818+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 33.0s / 820,676 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,562,000 | 2026-08-04T16:32:04.1706914+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 33.0s / 820,676 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,571,000 | 2026-08-04T16:32:04.1776895+00:00 | 109.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 33.0s / 820,676 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,572,000 | 2026-08-04T16:32:04.1789501+00:00 | 107.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 33.0s / 820,676 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,341,000 | 2026-08-04T16:32:05.1212529+00:00 | 105.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 34.0s / 785,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,342,000 | 2026-08-04T16:32:05.1224885+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 34.0s / 785,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,361,000 | 2026-08-04T16:32:05.1467105+00:00 | 119.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 34.0s / 785,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,372,000 | 2026-08-04T16:32:05.1593267+00:00 | 126.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 34.0s / 785,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,381,000 | 2026-08-04T16:32:05.1836005+00:00 | 114.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 34.0s / 785,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,382,000 | 2026-08-04T16:32:05.1846098+00:00 | 113.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 34.0s / 785,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,617,000 | 2026-08-04T16:32:06.6534579+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 29,981,000 | 2026-08-04T16:32:07.1024998+00:00 | 104.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,982,000 | 2026-08-04T16:32:07.1046404+00:00 | 102.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,992,000 | 2026-08-04T16:32:07.1190604+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,001,000 | 2026-08-04T16:32:07.1285612+00:00 | 113.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,011,000 | 2026-08-04T16:32:07.1376939+00:00 | 120.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,021,000 | 2026-08-04T16:32:07.1521631+00:00 | 126.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,022,000 | 2026-08-04T16:32:07.1534203+00:00 | 125.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,031,000 | 2026-08-04T16:32:07.1642824+00:00 | 122.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,032,000 | 2026-08-04T16:32:07.1648377+00:00 | 122.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,352,000 | 2026-08-04T16:32:07.5990357+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 36.0s / 785,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,382,000 | 2026-08-04T16:32:07.6296248+00:00 | 112.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,391,000 | 2026-08-04T16:32:07.6402612+00:00 | 104.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,401,000 | 2026-08-04T16:32:07.6531054+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,822,000 | 2026-08-04T16:32:08.1223717+00:00 | 119.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,831,000 | 2026-08-04T16:32:08.131217+00:00 | 115.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,832,000 | 2026-08-04T16:32:08.131709+00:00 | 115.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,841,000 | 2026-08-04T16:32:08.1420682+00:00 | 124.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,851,000 | 2026-08-04T16:32:08.1533425+00:00 | 119.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,852,000 | 2026-08-04T16:32:08.1548745+00:00 | 125.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,862,000 | 2026-08-04T16:32:08.1666774+00:00 | 117.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 880,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 31,747,000 | 2026-08-04T16:32:09.177201+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 38.0s / 855,834 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,631,000 | 2026-08-04T16:32:10.1737918+00:00 | 133.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,634,000 | 2026-08-04T16:32:10.1754563+00:00 | 117.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,641,000 | 2026-08-04T16:32:10.1805865+00:00 | 131.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,642,000 | 2026-08-04T16:32:10.1815908+00:00 | 139.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,646,000 | 2026-08-04T16:32:10.1836264+00:00 | 127.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,647,000 | 2026-08-04T16:32:10.1839138+00:00 | 110.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,651,000 | 2026-08-04T16:32:10.1859406+00:00 | 142.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,652,000 | 2026-08-04T16:32:10.1864174+00:00 | 142.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,654,000 | 2026-08-04T16:32:10.1877063+00:00 | 126.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,656,000 | 2026-08-04T16:32:10.20498+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,657,000 | 2026-08-04T16:32:10.206926+00:00 | 105.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,662,000 | 2026-08-04T16:32:10.2240223+00:00 | 116.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,671,000 | 2026-08-04T16:32:10.247353+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,981,000 | 2026-08-04T16:32:10.6217292+00:00 | 106.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,982,000 | 2026-08-04T16:32:10.6235457+00:00 | 104.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,991,000 | 2026-08-04T16:32:10.6340521+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,992,000 | 2026-08-04T16:32:10.6352902+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 876,104 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,011,000 | 2026-08-04T16:32:10.6590721+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,021,000 | 2026-08-04T16:32:10.6737759+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,022,000 | 2026-08-04T16:32:10.6747435+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,372,000 | 2026-08-04T16:32:11.1134668+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,382,000 | 2026-08-04T16:32:11.1263186+00:00 | 113.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,401,000 | 2026-08-04T16:32:11.1464525+00:00 | 115.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,412,000 | 2026-08-04T16:32:11.1614632+00:00 | 114.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,422,000 | 2026-08-04T16:32:11.1693452+00:00 | 113.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,811,000 | 2026-08-04T16:32:11.627159+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,812,000 | 2026-08-04T16:32:11.6285268+00:00 | 102.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 817,270 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,821,000 | 2026-08-04T16:32:11.6386994+00:00 | 105.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 881,813 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 33,822,000 | 2026-08-04T16:32:11.6416814+00:00 | 110.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 881,813 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 33,832,000 | 2026-08-04T16:32:11.6507597+00:00 | 101.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 881,813 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 33,841,000 | 2026-08-04T16:32:11.6597838+00:00 | 101.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 881,813 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 33,852,000 | 2026-08-04T16:32:11.6713299+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 881,813 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 37,991,000 | 2026-08-04T16:32:16.3553935+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 45.0s / 865,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 37,992,000 | 2026-08-04T16:32:16.3569106+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 45.0s / 865,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,451,000 | 2026-08-04T16:32:19.1502566+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 882,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,452,000 | 2026-08-04T16:32:19.1511009+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 882,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,462,000 | 2026-08-04T16:32:19.162162+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 882,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,471,000 | 2026-08-04T16:32:19.1807151+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 882,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,472,000 | 2026-08-04T16:32:19.1819893+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 882,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,491,000 | 2026-08-04T16:32:27.1556489+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 56.0s / 881,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,492,000 | 2026-08-04T16:32:27.1563831+00:00 | 104.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 56.0s / 881,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,501,000 | 2026-08-04T16:32:27.1656512+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 56.0s / 881,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,502,000 | 2026-08-04T16:32:27.1667205+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 56.0s / 881,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 60,000 | 2026-08-04T16:46:45.282179+00:00 | 151.9ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 61,000 | 2026-08-04T16:46:45.2837988+00:00 | 123.1ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 64,000 | 2026-08-04T16:46:45.288743+00:00 | 116.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 66,000 | 2026-08-04T16:46:45.292105+00:00 | 137.7ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 71,000 | 2026-08-04T16:46:45.305192+00:00 | 136.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 73,000 | 2026-08-04T16:46:45.3084232+00:00 | 160.5ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 76,000 | 2026-08-04T16:46:45.3148348+00:00 | 141.8ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 77,000 | 2026-08-04T16:46:45.3172538+00:00 | 117.5ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 79,000 | 2026-08-04T16:46:45.3219448+00:00 | 164.0ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 84,000 | 2026-08-04T16:46:45.3340706+00:00 | 172.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 87,000 | 2026-08-04T16:46:45.3439868+00:00 | 115.3ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 96,000 | 2026-08-04T16:46:45.3762454+00:00 | 208.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 104,000 | 2026-08-04T16:46:45.4002143+00:00 | 214.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 114,000 | 2026-08-04T16:46:45.452427+00:00 | 175.5ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 120,000 | 2026-08-04T16:46:45.4715408+00:00 | 176.3ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 122,000 | 2026-08-04T16:46:45.4820451+00:00 | 228.1ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 124,000 | 2026-08-04T16:46:45.4869268+00:00 | 174.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 130,000 | 2026-08-04T16:46:45.4989483+00:00 | 168.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 132,000 | 2026-08-04T16:46:45.5051115+00:00 | 242.3ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 134,000 | 2026-08-04T16:46:45.5179822+00:00 | 158.0ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 140,000 | 2026-08-04T16:46:45.5604276+00:00 | 114.6ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 162,000 | 2026-08-04T16:46:45.6422082+00:00 | 209.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 164,000 | 2026-08-04T16:46:45.6594591+00:00 | 109.0ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 165,000 | 2026-08-04T16:46:45.6617403+00:00 | 124.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 168,000 | 2026-08-04T16:46:45.6667931+00:00 | 119.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 176,000 | 2026-08-04T16:46:45.7036356+00:00 | 113.9ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 184,000 | 2026-08-04T16:46:45.733756+00:00 | 110.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 200,000 | 2026-08-04T16:46:45.766022+00:00 | 119.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 201,000 | 2026-08-04T16:46:45.7708381+00:00 | 268.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 202,000 | 2026-08-04T16:46:45.7730118+00:00 | 291.7ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 212,000 | 2026-08-04T16:46:45.8105318+00:00 | 317.6ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 213,000 | 2026-08-04T16:46:45.8116628+00:00 | 162.3ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 215,000 | 2026-08-04T16:46:45.8210179+00:00 | 108.6ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 219,000 | 2026-08-04T16:46:45.8412445+00:00 | 141.5ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 222,000 | 2026-08-04T16:46:45.8490502+00:00 | 300.5ms | GC pause | - | - | 2.0s / 391,899 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 224,000 | 2026-08-04T16:46:45.8526911+00:00 | 168.9ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 227,000 | 2026-08-04T16:46:45.8597489+00:00 | 133.9ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 230,000 | 2026-08-04T16:46:45.8662016+00:00 | 142.2ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 231,000 | 2026-08-04T16:46:45.8676898+00:00 | 294.5ms | GC pause | - | - | 2.0s / 391,899 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 232,000 | 2026-08-04T16:46:45.869644+00:00 | 302.4ms | GC pause | - | - | 2.0s / 391,899 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 233,000 | 2026-08-04T16:46:45.8784417+00:00 | 131.4ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 244,000 | 2026-08-04T16:46:45.9413411+00:00 | 191.1ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 253,000 | 2026-08-04T16:46:45.9784987+00:00 | 120.0ms | GC pause | - | - | 1.0s / 277,205 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 264,000 | 2026-08-04T16:46:46.0552401+00:00 | 122.2ms | GC pause | - | - | 2.0s / 391,899 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 271,000 | 2026-08-04T16:46:46.1024138+00:00 | 172.9ms | GC pause | - | - | 2.0s / 391,899 msg/s | Gen2 +1 / pause +1.4ms |
| Dekaf (3conn) | 281,000 | 2026-08-04T16:46:46.1510192+00:00 | 157.2ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 290,000 | 2026-08-04T16:46:46.1717873+00:00 | 114.2ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 292,000 | 2026-08-04T16:46:46.1769283+00:00 | 157.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 294,000 | 2026-08-04T16:46:46.1812991+00:00 | 101.5ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 300,000 | 2026-08-04T16:46:46.1952517+00:00 | 120.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 303,000 | 2026-08-04T16:46:46.2012531+00:00 | 110.7ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 309,000 | 2026-08-04T16:46:46.2146813+00:00 | 121.0ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 311,000 | 2026-08-04T16:46:46.2245171+00:00 | 143.0ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 320,000 | 2026-08-04T16:46:46.2567406+00:00 | 127.7ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 321,000 | 2026-08-04T16:46:46.2582935+00:00 | 119.0ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,000 | 2026-08-04T16:46:46.2788386+00:00 | 119.1ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 330,000 | 2026-08-04T16:46:46.2806389+00:00 | 125.6ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 340,000 | 2026-08-04T16:46:46.2988104+00:00 | 122.9ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 351,000 | 2026-08-04T16:46:46.3337585+00:00 | 121.0ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 361,000 | 2026-08-04T16:46:46.366422+00:00 | 144.3ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 363,000 | 2026-08-04T16:46:46.3733105+00:00 | 119.0ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 370,000 | 2026-08-04T16:46:46.3900721+00:00 | 167.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 371,000 | 2026-08-04T16:46:46.394132+00:00 | 137.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 381,000 | 2026-08-04T16:46:46.4173161+00:00 | 132.5ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 383,000 | 2026-08-04T16:46:46.420476+00:00 | 126.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 389,000 | 2026-08-04T16:46:46.4308715+00:00 | 127.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 392,000 | 2026-08-04T16:46:46.4346606+00:00 | 152.3ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 393,000 | 2026-08-04T16:46:46.4380142+00:00 | 130.9ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 401,000 | 2026-08-04T16:46:46.4529344+00:00 | 161.9ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 410,000 | 2026-08-04T16:46:46.4918415+00:00 | 150.1ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 411,000 | 2026-08-04T16:46:46.4970219+00:00 | 128.9ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 420,000 | 2026-08-04T16:46:46.5428876+00:00 | 142.7ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 440,000 | 2026-08-04T16:46:46.5831333+00:00 | 131.1ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 442,000 | 2026-08-04T16:46:46.5845348+00:00 | 105.9ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 449,000 | 2026-08-04T16:46:46.5971537+00:00 | 109.1ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 451,000 | 2026-08-04T16:46:46.602015+00:00 | 110.8ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 460,000 | 2026-08-04T16:46:46.6176071+00:00 | 118.0ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 469,000 | 2026-08-04T16:46:46.6461921+00:00 | 108.9ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 480,000 | 2026-08-04T16:46:46.6854781+00:00 | 113.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 501,000 | 2026-08-04T16:46:46.7433806+00:00 | 133.8ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 512,000 | 2026-08-04T16:46:46.7681688+00:00 | 121.6ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 522,000 | 2026-08-04T16:46:46.7850096+00:00 | 124.6ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 529,000 | 2026-08-04T16:46:46.7935039+00:00 | 100.8ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 538,000 | 2026-08-04T16:46:46.8097362+00:00 | 107.6ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 568,000 | 2026-08-04T16:46:46.9097071+00:00 | 124.1ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 580,000 | 2026-08-04T16:46:46.9280103+00:00 | 126.4ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 585,000 | 2026-08-04T16:46:46.9334964+00:00 | 122.2ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 588,000 | 2026-08-04T16:46:46.9370152+00:00 | 128.3ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 598,000 | 2026-08-04T16:46:46.94841+00:00 | 123.3ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 618,000 | 2026-08-04T16:46:46.9895492+00:00 | 117.1ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,000 | 2026-08-04T16:46:46.9970227+00:00 | 137.7ms | throughput collapse | - | - | 2.0s / 391,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 630,000 | 2026-08-04T16:46:47.0246288+00:00 | 121.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 760,000 | 2026-08-04T16:46:47.2798838+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 765,000 | 2026-08-04T16:46:47.284037+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 768,000 | 2026-08-04T16:46:47.2854068+00:00 | 134.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 770,000 | 2026-08-04T16:46:47.2865359+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 771,000 | 2026-08-04T16:46:47.2896629+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 772,000 | 2026-08-04T16:46:47.2903524+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 801,000 | 2026-08-04T16:46:47.3449272+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 802,000 | 2026-08-04T16:46:47.347238+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 815,000 | 2026-08-04T16:46:47.3864721+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 818,000 | 2026-08-04T16:46:47.3953588+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 895,000 | 2026-08-04T16:46:47.51553+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 948,000 | 2026-08-04T16:46:47.5963068+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 961,000 | 2026-08-04T16:46:47.6178103+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 962,000 | 2026-08-04T16:46:47.6192143+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 965,000 | 2026-08-04T16:46:47.6255915+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 968,000 | 2026-08-04T16:46:47.6295123+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 971,000 | 2026-08-04T16:46:47.6347147+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 972,000 | 2026-08-04T16:46:47.636706+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 981,000 | 2026-08-04T16:46:47.6513512+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,000 | 2026-08-04T16:46:47.6533466+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 985,000 | 2026-08-04T16:46:47.6576887+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 995,000 | 2026-08-04T16:46:47.6717585+00:00 | 152.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 998,000 | 2026-08-04T16:46:47.6823665+00:00 | 150.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,001,000 | 2026-08-04T16:46:47.6869808+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,005,000 | 2026-08-04T16:46:47.6953797+00:00 | 146.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,010,000 | 2026-08-04T16:46:47.7038311+00:00 | 124.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,011,000 | 2026-08-04T16:46:47.7049678+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,012,000 | 2026-08-04T16:46:47.7103428+00:00 | 131.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,015,000 | 2026-08-04T16:46:47.715054+00:00 | 132.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,020,000 | 2026-08-04T16:46:47.728209+00:00 | 134.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,022,000 | 2026-08-04T16:46:47.73215+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,042,000 | 2026-08-04T16:46:47.77356+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,048,000 | 2026-08-04T16:46:47.7789018+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,050,000 | 2026-08-04T16:46:47.781122+00:00 | 146.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,060,000 | 2026-08-04T16:46:47.8282253+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,080,000 | 2026-08-04T16:46:47.8731907+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,090,000 | 2026-08-04T16:46:47.8880101+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,100,000 | 2026-08-04T16:46:47.9023497+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,178,000 | 2026-08-04T16:46:48.0288483+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 569,491 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,228,000 | 2026-08-04T16:46:48.1123896+00:00 | 161.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,245,000 | 2026-08-04T16:46:48.1518661+00:00 | 175.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,247,000 | 2026-08-04T16:46:48.1537434+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,260,000 | 2026-08-04T16:46:48.1739738+00:00 | 136.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,268,000 | 2026-08-04T16:46:48.18844+00:00 | 178.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,270,000 | 2026-08-04T16:46:48.1911284+00:00 | 148.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,275,000 | 2026-08-04T16:46:48.2207025+00:00 | 160.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,278,000 | 2026-08-04T16:46:48.2234777+00:00 | 157.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,280,000 | 2026-08-04T16:46:48.225986+00:00 | 125.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,288,000 | 2026-08-04T16:46:48.2598052+00:00 | 134.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,505,000 | 2026-08-04T16:46:48.6686313+00:00 | 153.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,528,000 | 2026-08-04T16:46:48.6938151+00:00 | 168.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,555,000 | 2026-08-04T16:46:48.7317908+00:00 | 170.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 562,915 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,832,000 | 2026-08-04T16:46:49.181425+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,851,000 | 2026-08-04T16:46:49.2064924+00:00 | 130.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,852,000 | 2026-08-04T16:46:49.2071486+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,864,000 | 2026-08-04T16:46:49.2226952+00:00 | 119.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,866,000 | 2026-08-04T16:46:49.2260272+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,872,000 | 2026-08-04T16:46:49.2427425+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,876,000 | 2026-08-04T16:46:49.2501519+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,881,000 | 2026-08-04T16:46:49.2587451+00:00 | 133.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,882,000 | 2026-08-04T16:46:49.2593119+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,886,000 | 2026-08-04T16:46:49.2688273+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,981,000 | 2026-08-04T16:46:49.4710786+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,998,000 | 2026-08-04T16:46:49.4923488+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,001,000 | 2026-08-04T16:46:49.4959075+00:00 | 123.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,002,000 | 2026-08-04T16:46:49.4967311+00:00 | 122.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,005,000 | 2026-08-04T16:46:49.4994901+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,012,000 | 2026-08-04T16:46:49.5123835+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,015,000 | 2026-08-04T16:46:49.5162502+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,025,000 | 2026-08-04T16:46:49.5278732+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,032,000 | 2026-08-04T16:46:49.5378581+00:00 | 170.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,038,000 | 2026-08-04T16:46:49.5456329+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,052,000 | 2026-08-04T16:46:49.5886948+00:00 | 129.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,055,000 | 2026-08-04T16:46:49.5927396+00:00 | 143.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,062,000 | 2026-08-04T16:46:49.5992203+00:00 | 128.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,068,000 | 2026-08-04T16:46:49.6064603+00:00 | 141.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,071,000 | 2026-08-04T16:46:49.6093309+00:00 | 123.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,087,000 | 2026-08-04T16:46:49.6486073+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,088,000 | 2026-08-04T16:46:49.6492789+00:00 | 129.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,098,000 | 2026-08-04T16:46:49.6687354+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,127,000 | 2026-08-04T16:46:49.7507609+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,128,000 | 2026-08-04T16:46:49.7521414+00:00 | 132.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,148,000 | 2026-08-04T16:46:49.787879+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,280,000 | 2026-08-04T16:46:50.0181495+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 555,690 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,330,000 | 2026-08-04T16:46:50.0993598+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,350,000 | 2026-08-04T16:46:50.1283125+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,462,000 | 2026-08-04T16:46:50.3139137+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,480,000 | 2026-08-04T16:46:50.383671+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,500,000 | 2026-08-04T16:46:50.4146423+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,750,000 | 2026-08-04T16:46:50.7566548+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,752,000 | 2026-08-04T16:46:50.7583312+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,770,000 | 2026-08-04T16:46:50.7858131+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,772,000 | 2026-08-04T16:46:50.7910477+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,800,000 | 2026-08-04T16:46:50.8507724+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 640,562 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,499,000 | 2026-08-04T16:46:51.7672641+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,502,000 | 2026-08-04T16:46:51.7711936+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,512,000 | 2026-08-04T16:46:51.7872894+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,522,000 | 2026-08-04T16:46:51.7977738+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,530,000 | 2026-08-04T16:46:51.8140286+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,571,000 | 2026-08-04T16:46:51.904761+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,572,000 | 2026-08-04T16:46:51.9056008+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,601,000 | 2026-08-04T16:46:51.9443926+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 763,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,268,000 | 2026-08-04T16:46:52.7192119+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 810,292 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,290,000 | 2026-08-04T16:46:52.741962+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 810,292 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,310,000 | 2026-08-04T16:46:52.7813125+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 810,292 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,600,000 | 2026-08-04T16:46:53.1826217+00:00 | 122.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,650,000 | 2026-08-04T16:46:53.2902208+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,660,000 | 2026-08-04T16:46:53.3030362+00:00 | 118.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,670,000 | 2026-08-04T16:46:53.3168762+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,680,000 | 2026-08-04T16:46:53.3430346+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,042,000 | 2026-08-04T16:46:53.7398218+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,045,000 | 2026-08-04T16:46:53.7417493+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,048,000 | 2026-08-04T16:46:53.7433116+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,058,000 | 2026-08-04T16:46:53.7697152+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 797,281 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,485,000 | 2026-08-04T16:46:54.2835211+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,488,000 | 2026-08-04T16:46:54.2952689+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,810,000 | 2026-08-04T16:46:54.7116325+00:00 | 121.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,820,000 | 2026-08-04T16:46:54.7221537+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,821,000 | 2026-08-04T16:46:54.7287792+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,840,000 | 2026-08-04T16:46:54.7553735+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,842,000 | 2026-08-04T16:46:54.7569272+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 796,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,645,000 | 2026-08-04T16:46:55.7290632+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 670,905 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,647,000 | 2026-08-04T16:46:55.7300383+00:00 | 135.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 670,905 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,668,000 | 2026-08-04T16:46:55.7640953+00:00 | 221.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 670,905 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,695,000 | 2026-08-04T16:46:55.849772+00:00 | 172.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 670,905 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,950,000 | 2026-08-04T16:46:56.2676052+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 760,502 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,225,000 | 2026-08-04T16:46:56.6541778+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 760,502 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,227,000 | 2026-08-04T16:46:56.6560854+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 760,502 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,237,000 | 2026-08-04T16:46:56.6766062+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 760,502 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,245,000 | 2026-08-04T16:46:56.6857964+00:00 | 134.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 760,502 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,960,000 | 2026-08-04T16:46:57.6910518+00:00 | 126.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 685,965 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,970,000 | 2026-08-04T16:46:57.7016665+00:00 | 131.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 685,965 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,990,000 | 2026-08-04T16:46:57.7292126+00:00 | 138.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 685,965 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,000,000 | 2026-08-04T16:46:57.7438928+00:00 | 130.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 685,965 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,370,000 | 2026-08-04T16:46:58.2283914+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,415,000 | 2026-08-04T16:46:58.3070042+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,428,000 | 2026-08-04T16:46:58.3393101+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,430,000 | 2026-08-04T16:46:58.3414445+00:00 | 154.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,518,000 | 2026-08-04T16:46:58.5120593+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,525,000 | 2026-08-04T16:46:58.5177084+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,528,000 | 2026-08-04T16:46:58.5194549+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,648,000 | 2026-08-04T16:46:58.7009954+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,655,000 | 2026-08-04T16:46:58.7066877+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,665,000 | 2026-08-04T16:46:58.7235114+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,668,000 | 2026-08-04T16:46:58.7294435+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,678,000 | 2026-08-04T16:46:58.7391123+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,741,000 | 2026-08-04T16:46:58.864246+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,752,000 | 2026-08-04T16:46:58.8746256+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,771,000 | 2026-08-04T16:46:58.9059649+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,772,000 | 2026-08-04T16:46:58.9077033+00:00 | 118.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,782,000 | 2026-08-04T16:46:58.9211646+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 623,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,852,000 | 2026-08-04T16:47:00.2294391+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 702,849 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,881,000 | 2026-08-04T16:47:00.255346+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 702,849 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,255,000 | 2026-08-04T16:47:00.7499122+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 702,849 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,265,000 | 2026-08-04T16:47:00.7592919+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 702,849 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,268,000 | 2026-08-04T16:47:00.7614741+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 702,849 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,090,000 | 2026-08-04T16:47:01.7547347+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 974,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,590,000 | 2026-08-04T16:47:02.2599729+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 934,670 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,600,000 | 2026-08-04T16:47:02.2669542+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 934,670 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,925,000 | 2026-08-04T16:47:12.2652175+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 919,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 29,490,000 | 2026-08-04T16:47:21.2770063+00:00 | 111.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 37.0s / 927,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 595,609,000 | 2026-08-04T16:58:02.6294646+00:00 | 176.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded | - | 678.5s / 758,492 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 595,619,000 | 2026-08-04T16:58:02.6407524+00:00 | 182.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded | - | 678.5s / 758,492 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 595,621,000 | 2026-08-04T16:58:02.6412784+00:00 | 145.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/succeeded | - | 678.5s / 758,492 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*713 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.66x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.39x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.03 | 3968.69 | 1,245,018 | 2,194,741 | +8.9% | +149.24% | 151.98 | 1,245,018 | 0 | 1.28 |
| Confluent | 1.84 | - | 124,265 | 1,526,241 | +4.2% | +21.87% | 15.17 | 124,265 | 0 | 0.23 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 5,112 | 529.72 | 737.33 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:19:05.2127662+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 873,411 msg/s |
| Dekaf | 2026-08-04T16:19:10.1777825+00:00 | 1 | 16.0 MiB / 4.1 MiB | 555.0 MB/s | 0/0 | 0 | 5.0s / 2,484,608 msg/s |
| Dekaf | 2026-08-04T16:19:11.1776016+00:00 | 1 | 16.0 MiB / 1.4 MiB | 555.0 MB/s | 0/0 | 0 | 6.0s / 1,676,800 msg/s |
| Dekaf | 2026-08-04T16:19:12.1795844+00:00 | 1 | 16.0 MiB / 1.9 MiB | 555.0 MB/s | 0/0 | 0 | 7.0s / 1,854,034 msg/s |
| Dekaf | 2026-08-04T16:19:13.1819931+00:00 | 1 | 16.0 MiB / 2.9 MiB | 555.0 MB/s | 0/0 | 0 | 8.0s / 2,046,058 msg/s |
| Dekaf | 2026-08-04T16:19:14.1841762+00:00 | 1 | 16.0 MiB / 2.6 MiB | 555.0 MB/s | 0/0 | 0 | 9.0s / 2,282,817 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.80x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.44x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 400.17 | 400.16 | 260 | 349 | +3.9% | +0.28% | 0.25 | 347 | 0 | 0.14 |
| Confluent | 274.51 | - | 123 | 166 | +7.1% | +0.72% | 0.12 | 164 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 104,020 | 115.54 | 1.16 KB |
| Dekaf | 2 | 104,473 | 116.04 | 1.16 KB |
| Dekaf | 3 | 103,713 | 115.20 | 1.16 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-04T16:31:42.0079331+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 238 msg/s |
| Dekaf | 2026-08-04T16:31:51.0141711+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 9.0s / 352 msg/s |
| Dekaf | 2026-08-04T16:32:00.0184515+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 18.0s / 361 msg/s |
| Dekaf | 2026-08-04T16:32:10.0246082+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 28.0s / 352 msg/s |
| Dekaf | 2026-08-04T16:32:19.0607397+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 37.0s / 357 msg/s |
| Dekaf | 2026-08-04T16:32:28.0807368+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 46.0s / 363 msg/s |
| Dekaf | 2026-08-04T16:32:37.1006952+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 55.0s / 353 msg/s |
| Dekaf | 2026-08-04T16:32:46.1082913+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 64.0s / 333 msg/s |
| Dekaf | 2026-08-04T16:32:55.1242333+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 73.0s / 342 msg/s |
| Dekaf | 2026-08-04T16:33:04.141917+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 82.0s / 345 msg/s |
| Dekaf | 2026-08-04T16:33:13.1556824+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 348 msg/s |
| Dekaf | 2026-08-04T16:33:22.1763104+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 100.0s / 352 msg/s |
| Dekaf | 2026-08-04T16:33:31.1830713+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 109.0s / 342 msg/s |
| Dekaf | 2026-08-04T16:33:40.1916986+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 118.0s / 358 msg/s |
| Dekaf | 2026-08-04T16:33:49.1969486+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 336 msg/s |
| Dekaf | 2026-08-04T16:33:58.2058225+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 136.0s / 340 msg/s |
| Dekaf | 2026-08-04T16:34:07.2274466+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 145.0s / 341 msg/s |
| Dekaf | 2026-08-04T16:34:17.2360447+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 344 msg/s |
| Dekaf | 2026-08-04T16:34:26.2794511+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 337 msg/s |
| Dekaf | 2026-08-04T16:34:35.2980657+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 338 msg/s |
| Dekaf | 2026-08-04T16:34:44.3036407+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 342 msg/s |
| Dekaf | 2026-08-04T16:34:53.3434784+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 350 msg/s |
| Dekaf | 2026-08-04T16:35:02.3473506+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 306 msg/s |
| Dekaf | 2026-08-04T16:35:11.3683261+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 329 msg/s |
| Dekaf | 2026-08-04T16:35:20.3735839+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 351 msg/s |
| Dekaf | 2026-08-04T16:35:29.3759563+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 358 msg/s |
| Dekaf | 2026-08-04T16:35:38.3812161+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 321 msg/s |
| Dekaf | 2026-08-04T16:35:47.387365+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 245.0s / 344 msg/s |
| Dekaf | 2026-08-04T16:35:56.3961963+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 254.0s / 333 msg/s |
| Dekaf | 2026-08-04T16:36:05.3983217+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 263.0s / 332 msg/s |
| Dekaf | 2026-08-04T16:36:14.4214382+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 272.0s / 342 msg/s |
| Dekaf | 2026-08-04T16:36:24.4446721+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 347 msg/s |
| Dekaf | 2026-08-04T16:36:33.4491728+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 291.0s / 332 msg/s |
| Dekaf | 2026-08-04T16:36:42.4656565+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 300.0s / 352 msg/s |
| Dekaf | 2026-08-04T16:36:51.4826269+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 309.0s / 293 msg/s |
| Dekaf | 2026-08-04T16:37:00.4876309+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 318.0s / 337 msg/s |
| Dekaf | 2026-08-04T16:37:09.5084905+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 327.0s / 339 msg/s |
| Dekaf | 2026-08-04T16:37:18.5146798+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.0s / 339 msg/s |
| Dekaf | 2026-08-04T16:37:27.5272603+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 345.0s / 350 msg/s |
| Dekaf | 2026-08-04T16:37:36.5525468+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 367 msg/s |
| Dekaf | 2026-08-04T16:37:45.5612546+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 363.1s / 311 msg/s |
| Dekaf | 2026-08-04T16:37:54.5702301+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 372.1s / 342 msg/s |
| Dekaf | 2026-08-04T16:38:03.5729282+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 381.1s / 353 msg/s |
| Dekaf | 2026-08-04T16:38:12.5755599+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 390.1s / 351 msg/s |
| Dekaf | 2026-08-04T16:38:21.5971642+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 400.1s / 335 msg/s |
| Dekaf | 2026-08-04T16:38:31.6066687+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 410.1s / 336 msg/s |
| Dekaf | 2026-08-04T16:38:40.610449+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 419.1s / 336 msg/s |
| Dekaf | 2026-08-04T16:38:49.6770067+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 428.1s / 355 msg/s |
| Dekaf | 2026-08-04T16:38:58.700289+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 437.1s / 353 msg/s |
| Dekaf | 2026-08-04T16:39:07.7075681+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 446.1s / 348 msg/s |
| Dekaf | 2026-08-04T16:39:16.7092499+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 455.1s / 360 msg/s |
| Dekaf | 2026-08-04T16:39:25.728509+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 464.1s / 348 msg/s |
| Dekaf | 2026-08-04T16:39:34.7328191+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 338 msg/s |
| Dekaf | 2026-08-04T16:39:43.7358196+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 349 msg/s |
| Dekaf | 2026-08-04T16:39:52.7449233+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 358 msg/s |
| Dekaf | 2026-08-04T16:40:01.7511848+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 345 msg/s |
| Dekaf | 2026-08-04T16:40:10.7542823+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 509.1s / 349 msg/s |
| Dekaf | 2026-08-04T16:40:19.789485+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 518.1s / 350 msg/s |
| Dekaf | 2026-08-04T16:40:28.811728+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 527.1s / 356 msg/s |
| Dekaf | 2026-08-04T16:40:38.8125943+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 537.1s / 359 msg/s |
| Dekaf | 2026-08-04T16:40:47.8180734+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 546.1s / 337 msg/s |
| Dekaf | 2026-08-04T16:40:56.8437692+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 555.1s / 327 msg/s |
| Dekaf | 2026-08-04T16:41:05.8722797+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 564.1s / 333 msg/s |
| Dekaf | 2026-08-04T16:41:14.8789586+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 573.1s / 337 msg/s |
| Dekaf | 2026-08-04T16:41:23.8881275+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 582.1s / 329 msg/s |
| Dekaf | 2026-08-04T16:41:32.8940156+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 591.1s / 345 msg/s |
| Dekaf | 2026-08-04T16:41:41.9001615+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 600.1s / 308 msg/s |
| Dekaf | 2026-08-04T16:41:50.9062625+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 609.1s / 346 msg/s |
| Dekaf | 2026-08-04T16:41:59.9242912+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 618.1s / 336 msg/s |
| Dekaf | 2026-08-04T16:42:08.9297942+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 627.1s / 358 msg/s |
| Dekaf | 2026-08-04T16:42:17.9485439+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 636.1s / 354 msg/s |
| Dekaf | 2026-08-04T16:42:26.9625609+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 645.1s / 337 msg/s |
| Dekaf | 2026-08-04T16:42:35.9728006+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 654.1s / 357 msg/s |
| Dekaf | 2026-08-04T16:42:44.976932+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 663.1s / 341 msg/s |
| Dekaf | 2026-08-04T16:42:55.0005379+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 366 msg/s |
| Dekaf | 2026-08-04T16:43:04.0211906+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 364 msg/s |
| Dekaf | 2026-08-04T16:43:13.0216002+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 359 msg/s |
| Dekaf | 2026-08-04T16:43:22.0493991+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 700.1s / 357 msg/s |
| Dekaf | 2026-08-04T16:43:31.0610765+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 709.1s / 363 msg/s |
| Dekaf | 2026-08-04T16:43:40.0664502+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 718.1s / 351 msg/s |
| Dekaf | 2026-08-04T16:43:49.07413+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 727.1s / 353 msg/s |
| Dekaf | 2026-08-04T16:43:58.0906649+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 736.1s / 347 msg/s |
| Dekaf | 2026-08-04T16:44:07.0948632+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 745.1s / 349 msg/s |
| Dekaf | 2026-08-04T16:44:16.1011807+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 754.1s / 350 msg/s |
| Dekaf | 2026-08-04T16:44:25.1045111+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 763.1s / 354 msg/s |
| Dekaf | 2026-08-04T16:44:34.1487106+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 772.1s / 365 msg/s |
| Dekaf | 2026-08-04T16:44:43.1843418+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 781.1s / 363 msg/s |
| Dekaf | 2026-08-04T16:44:52.1872973+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 790.1s / 354 msg/s |
| Dekaf | 2026-08-04T16:45:02.19928+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 357 msg/s |
| Dekaf | 2026-08-04T16:45:11.2396808+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 809.1s / 364 msg/s |
| Dekaf | 2026-08-04T16:45:20.2595798+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 818.1s / 357 msg/s |
| Dekaf | 2026-08-04T16:45:29.2738497+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 827.1s / 364 msg/s |
| Dekaf | 2026-08-04T16:45:38.2773045+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 836.1s / 359 msg/s |
| Dekaf | 2026-08-04T16:45:47.2786925+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 845.1s / 355 msg/s |
| Dekaf | 2026-08-04T16:45:56.2860898+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 854.1s / 362 msg/s |
| Dekaf | 2026-08-04T16:46:05.3114405+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 863.1s / 355 msg/s |
| Dekaf | 2026-08-04T16:46:14.3144256+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 872.1s / 366 msg/s |
| Dekaf | 2026-08-04T16:46:23.3683923+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 881.1s / 345 msg/s |
| Dekaf | 2026-08-04T16:46:32.3892559+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 890.1s / 356 msg/s |
| Dekaf | 2026-08-04T16:46:41.3980831+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 899.1s / 348 msg/s |
*2,595 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 147,300 | 110,500 | 36,800 | 110,500 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 312,200 | 234,200 | 78,000 | 234,200 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.46x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.10x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.86 | - | 1,564,065 | 1,551,981 | -15.8% | -1.37% | 1491.61 | - | 0 | 1.34 |
| Confluent | 1.19 | - | 1,095,710 | 1,159,116 | -1.9% | -0.06% | 1044.95 | - | 0 | 1.31 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.39x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.34x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.86 | - | 1,553,352 | 1,563,173 | +7.2% | +0.74% | 1481.39 | - | 0 | 1.34 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.46 | - | 3,424,139 | 3,451,174 | -4.9% | -0.49% | 3265.51 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.39 | - | 3,920,046 | 3,966,343 | -9.0% | -0.77% | 3738.45 | - | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 19368 | 72 | 1 | 2240.93 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 239417 | 15 | 1 | 1146.63 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 172102 | 1 | 1 | 862.68 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 167972 | 1 | 1 | 805.66 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 201457 | 6 | 1 | 1009.36 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 211821 | 1 | 1 | 1164.75 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 128022 | 0 | 0 | 731.94 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 238274 | 21 | 1 | 1140.10 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 233956 | 1 | 1 | 1186.11 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 136890 | 1 | 1 | 654.90 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5046 | 1 | 1 | 17.57 GB | 953 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 89 | 2 | 1 | 237.97 MB | 1.65 KB |
| Dekaf | Consumer | 23395 | 40 | 2 | 2653.98 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 69285 | 4 | 1 | 2635.89 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 479.29 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 15 | 3 | 1 | 991.29 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 266 | 2 | 2 | 983.37 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 181 | 2 | 2 | 173.15 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 218 | 6 | 2 | 161.54 MB | 0 B |
| Dekaf | Producer (Acks All) | 339 | 2 | 2 | 1.26 GB | 1 B |
| Dekaf | Producer (Acks All) | 388 | 2 | 2 | 153.82 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 179 | 3 | 2 | 161.64 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 367 | 2 | 2 | 1.33 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 349 | 2 | 2 | 138.55 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 152 | 2 | 2 | 116.60 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 600 | 2 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 35 | 1 | 1 | 89.63 MB | 301 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 292 | 2 | 2 | 1.09 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 218 | 5 | 1 | 785.19 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 299 | 2 | 2 | 1.15 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 170 | 2 | 1 | 794.48 MB | 1 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Median Interval Throughput**: table order and comparison ratios use median sampled client-side msg/s when available, which is less sensitive to short late-run stalls than the whole-run mean
- **Same-VM Pairing**: comparable Dekaf and Confluent scenarios run sequentially inside one job/VM; 1-broker producer acceptance lanes run twice in opposite client orders and publish a geometric-mean aggregate, while other lanes alternate order by workflow run number
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured; table headings report the 16KB seed batch size because it amplifies per-batch costs relative to well-batched workloads
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **Round-Trip Correctness**: Bounded sequenced payloads are consumed back and checked for corruption, wrong partitions, gaps, duplicates, and reordering
- **Round-Trip CPU Scope**: CPU time covers both bulk production and consumer validation; it is not a producer-only metric
- **Round-Trip Alloc Scope**: the GC/alloc window likewise spans production plus consume-side validation; values are deliberately consumed as byte[] on both clients for parity, so each consumed payload is materialized as a fresh array (~152 B at 128 B messages) — the expected allocation floor for this lane, not a leak
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
