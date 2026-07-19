---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-19 04:22 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,541,968 | 1,522,512–1,561,674 | 0.93 | 1.36x |
| Confluent | 2 | 1,134,550 | 1,123,939–1,145,261 | 1.52 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.74 | 693.57 | 1,688,155 | 1,708,375 | +3.3% | +0.17% | 1609.95 | 1,688,155 | 0 | 1.24 |
| Dekaf (confluent-first) | 0.94 | 964.97 | 1,509,735 | 1,561,674 | +5.2% | +0.51% | 1439.80 | 1,509,735 | 0 | 1.42 |
| Dekaf (dekaf-first) | 0.93 | 940.67 | 1,488,148 | 1,522,512 | +14.7% | +1.32% | 1419.21 | 1,488,148 | 0 | 1.38 |
| Confluent (confluent-first) | 1.52 | - | 1,118,315 | 1,145,261 | +10.0% | +0.78% | 1066.51 | 1,118,315 | 0 | 1.70 |
| Confluent (dekaf-first) | 1.51 | - | 1,110,398 | 1,123,939 | +21.2% | +1.69% | 1058.96 | 1,110,398 | 0 | 1.68 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,324,612 | 1471.77 | 1019.73 KB |
| Dekaf | 1 | 1,318,592 | 1465.08 | 1009.73 KB |
| Dekaf (3conn) | 1 | 1,613,006 | 1792.22 | 936.37 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:45.1144205+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 433,511 msg/s |
| Dekaf | 2026-07-19T03:07:12.1172615+00:00 | 1 | 16.0 MiB / 14.9 MiB | 1566.3 MB/s | 0/0 | 21,602 | 27.0s / 1,121,151 msg/s |
| Dekaf | 2026-07-19T03:07:40.1245233+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1566.3 MB/s | 1/0 | 48,313 | 55.0s / 1,322,745 msg/s |
| Dekaf | 2026-07-19T03:08:07.1302353+00:00 | 1 | 14.0 MiB / 7.8 MiB | 1566.3 MB/s | 1/0 | 80,597 | 82.0s / 1,327,913 msg/s |
| Dekaf | 2026-07-19T03:08:34.1383361+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1657.8 MB/s | 2/0 | 126,948 | 109.0s / 1,531,988 msg/s |
| Dekaf | 2026-07-19T03:09:01.142204+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1708.8 MB/s | 2/1 | 164,133 | 136.1s / 1,572,559 msg/s |
| Dekaf | 2026-07-19T03:09:29.1546235+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1760.4 MB/s | 2/1 | 218,486 | 164.1s / 1,514,477 msg/s |
| Dekaf | 2026-07-19T03:09:56.1622656+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1760.4 MB/s | 2/1 | 273,000 | 191.1s / 1,393,464 msg/s |
| Dekaf | 2026-07-19T03:10:23.1776362+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1760.4 MB/s | 2/2 | 318,048 | 218.1s / 1,284,809 msg/s |
| Dekaf | 2026-07-19T03:10:50.1812659+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1808.9 MB/s | 2/2 | 367,392 | 245.1s / 1,684,079 msg/s |
| Dekaf | 2026-07-19T03:11:18.1902161+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1808.9 MB/s | 2/2 | 432,289 | 273.1s / 1,519,500 msg/s |
| Dekaf | 2026-07-19T03:11:45.1933147+00:00 | 1 | 12.0 MiB / 10.2 MiB | 1817.6 MB/s | 2/2 | 497,791 | 300.1s / 1,500,476 msg/s |
| Dekaf | 2026-07-19T03:12:12.1983507+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1817.6 MB/s | 2/2 | 559,606 | 327.1s / 1,554,466 msg/s |
| Dekaf | 2026-07-19T03:12:39.2141749+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1817.6 MB/s | 2/3 | 602,820 | 354.1s / 1,638,334 msg/s |
| Dekaf | 2026-07-19T03:13:07.2233358+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1817.6 MB/s | 2/3 | 661,810 | 382.1s / 1,541,481 msg/s |
| Dekaf | 2026-07-19T03:13:34.2337203+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1817.6 MB/s | 2/3 | 708,025 | 409.1s / 1,368,284 msg/s |
| Dekaf | 2026-07-19T03:14:01.2378409+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1817.6 MB/s | 2/3 | 760,330 | 436.2s / 1,371,737 msg/s |
| Dekaf | 2026-07-19T03:14:28.252428+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1817.6 MB/s | 2/3 | 820,301 | 463.2s / 1,468,227 msg/s |
| Dekaf | 2026-07-19T03:14:56.2588541+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1817.6 MB/s | 2/3 | 888,632 | 491.2s / 1,637,519 msg/s |
| Dekaf | 2026-07-19T03:15:23.2681768+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/3 | 961,196 | 518.2s / 1,694,352 msg/s |
| Dekaf | 2026-07-19T03:15:50.2783247+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1838.2 MB/s | 2/3 | 1,022,255 | 545.2s / 1,674,818 msg/s |
| Dekaf | 2026-07-19T03:16:18.2859339+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/3 | 1,085,992 | 573.2s / 1,695,722 msg/s |
| Dekaf | 2026-07-19T03:16:45.2927892+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1838.2 MB/s | 2/3 | 1,153,207 | 600.2s / 1,689,813 msg/s |
| Dekaf | 2026-07-19T03:17:12.2994792+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/4 | 1,214,559 | 627.2s / 1,610,598 msg/s |
| Dekaf | 2026-07-19T03:17:39.3082185+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/5 | 1,274,607 | 654.2s / 1,668,085 msg/s |
| Dekaf | 2026-07-19T03:18:07.3171973+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/5 | 1,341,148 | 682.2s / 1,656,041 msg/s |
| Dekaf | 2026-07-19T03:18:34.3236754+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1838.2 MB/s | 2/5 | 1,406,779 | 709.2s / 1,642,311 msg/s |
| Dekaf | 2026-07-19T03:19:01.3288185+00:00 | 1 | 12.0 MiB / 10.4 MiB | 1838.2 MB/s | 2/5 | 1,472,562 | 736.2s / 1,562,519 msg/s |
| Dekaf | 2026-07-19T03:19:28.3328131+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/5 | 1,537,041 | 763.2s / 1,668,135 msg/s |
| Dekaf | 2026-07-19T03:19:56.339489+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1838.2 MB/s | 2/5 | 1,611,169 | 791.2s / 1,642,663 msg/s |
| Dekaf | 2026-07-19T03:20:23.344444+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/5 | 1,667,833 | 818.3s / 1,462,192 msg/s |
| Dekaf | 2026-07-19T03:20:50.3487351+00:00 | 1 | 12.0 MiB / 8.3 MiB | 1838.2 MB/s | 2/5 | 1,728,831 | 845.3s / 1,481,760 msg/s |
| Dekaf | 2026-07-19T03:21:17.3548922+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1838.2 MB/s | 2/5 | 1,781,604 | 872.3s / 1,520,656 msg/s |
| Dekaf | 2026-07-19T03:51:46.4698978+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 475,577 msg/s |
| Dekaf | 2026-07-19T03:52:13.4804583+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1788.9 MB/s | 0/0 | 32,851 | 27.0s / 1,607,277 msg/s |
| Dekaf | 2026-07-19T03:52:40.4928821+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1788.9 MB/s | 0/1 | 74,418 | 54.0s / 1,591,182 msg/s |
| Dekaf | 2026-07-19T03:53:07.5048813+00:00 | 1 | 16.0 MiB / 14.3 MiB | 1788.9 MB/s | 0/1 | 114,145 | 81.0s / 1,618,109 msg/s |
| Dekaf | 2026-07-19T03:53:35.5125143+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1795.8 MB/s | 0/1 | 151,745 | 109.0s / 1,597,276 msg/s |
| Dekaf | 2026-07-19T03:54:02.5263466+00:00 | 1 | 16.0 MiB / 3.0 MiB | 1806.8 MB/s | 0/2 | 184,872 | 136.1s / 1,581,702 msg/s |
| Dekaf | 2026-07-19T03:54:29.5385816+00:00 | 1 | 16.0 MiB / 13.2 MiB | 1821.4 MB/s | 0/2 | 223,706 | 163.1s / 1,502,512 msg/s |
| Dekaf | 2026-07-19T03:54:57.5474815+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1821.4 MB/s | 0/2 | 258,196 | 191.1s / 1,383,110 msg/s |
| Dekaf | 2026-07-19T03:55:24.5587637+00:00 | 1 | 16.0 MiB / 14.9 MiB | 1821.4 MB/s | 0/2 | 289,017 | 218.1s / 1,300,749 msg/s |
| Dekaf | 2026-07-19T03:55:51.5753883+00:00 | 1 | 16.0 MiB / 14.9 MiB | 1821.4 MB/s | 0/2 | 324,327 | 245.1s / 1,628,877 msg/s |
| Dekaf | 2026-07-19T03:56:18.5843451+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1821.4 MB/s | 0/3 | 361,989 | 272.1s / 1,584,924 msg/s |
| Dekaf | 2026-07-19T03:56:46.5933678+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1821.4 MB/s | 0/3 | 397,288 | 300.1s / 1,380,862 msg/s |
| Dekaf | 2026-07-19T03:57:13.6005101+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1821.4 MB/s | 0/3 | 430,058 | 327.1s / 1,306,990 msg/s |
| Dekaf | 2026-07-19T03:57:40.6102067+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1821.4 MB/s | 0/3 | 460,583 | 354.1s / 1,550,605 msg/s |
| Dekaf | 2026-07-19T03:58:07.6224207+00:00 | 1 | 16.0 MiB / 14.2 MiB | 1821.4 MB/s | 0/3 | 491,648 | 381.1s / 1,459,835 msg/s |
| Dekaf | 2026-07-19T03:58:35.6292894+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1821.4 MB/s | 0/3 | 528,877 | 409.1s / 1,400,340 msg/s |
| Dekaf | 2026-07-19T03:59:02.6441434+00:00 | 1 | 16.0 MiB / 11.2 MiB | 1821.4 MB/s | 0/3 | 561,651 | 436.1s / 1,244,835 msg/s |
| Dekaf | 2026-07-19T03:59:29.6548079+00:00 | 1 | 16.0 MiB / 14.9 MiB | 1821.4 MB/s | 0/3 | 596,856 | 463.1s / 1,442,210 msg/s |
| Dekaf | 2026-07-19T03:59:56.6622852+00:00 | 1 | 16.0 MiB / 14.4 MiB | 1821.4 MB/s | 0/3 | 630,627 | 490.2s / 1,425,537 msg/s |
| Dekaf | 2026-07-19T04:00:24.6685648+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1821.4 MB/s | 1/3 | 665,855 | 518.2s / 1,584,965 msg/s |
| Dekaf | 2026-07-19T04:00:51.6821082+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1821.4 MB/s | 1/3 | 696,601 | 545.2s / 1,337,262 msg/s |
| Dekaf | 2026-07-19T04:01:18.6901722+00:00 | 1 | 18.0 MiB / 15.1 MiB | 1821.4 MB/s | 1/4 | 732,287 | 572.2s / 1,575,953 msg/s |
| Dekaf | 2026-07-19T04:01:45.6990412+00:00 | 1 | 18.0 MiB / 1.0 MiB | 1821.4 MB/s | 1/4 | 763,048 | 599.2s / 1,504,124 msg/s |
| Dekaf | 2026-07-19T04:02:13.7073741+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1821.4 MB/s | 1/4 | 801,443 | 627.2s / 1,592,136 msg/s |
| Dekaf | 2026-07-19T04:02:40.7176454+00:00 | 1 | 15.0 MiB / 11.2 MiB | 1821.4 MB/s | 2/4 | 846,157 | 654.2s / 1,665,508 msg/s |
| Dekaf | 2026-07-19T04:03:07.7274702+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1821.4 MB/s | 3/4 | 901,478 | 681.2s / 1,605,770 msg/s |
| Dekaf | 2026-07-19T04:03:34.7477113+00:00 | 1 | 11.0 MiB / 10.4 MiB | 1821.4 MB/s | 3/4 | 966,937 | 708.2s / 1,651,426 msg/s |
| Dekaf | 2026-07-19T04:04:02.7631748+00:00 | 1 | 13.0 MiB / 11.8 MiB | 1821.4 MB/s | 3/5 | 1,025,940 | 736.3s / 1,554,572 msg/s |
| Dekaf | 2026-07-19T04:04:29.7698019+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1821.4 MB/s | 3/5 | 1,090,974 | 763.3s / 1,666,636 msg/s |
| Dekaf | 2026-07-19T04:04:56.7787956+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1821.4 MB/s | 3/5 | 1,154,237 | 790.3s / 1,576,241 msg/s |
| Dekaf | 2026-07-19T04:05:24.7859862+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1821.4 MB/s | 4/5 | 1,222,350 | 818.3s / 1,564,915 msg/s |
| Dekaf | 2026-07-19T04:05:51.806001+00:00 | 1 | 15.0 MiB / 14.1 MiB | 1821.4 MB/s | 5/5 | 1,293,039 | 845.3s / 1,605,297 msg/s |
| Dekaf | 2026-07-19T04:06:18.8140042+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1821.4 MB/s | 5/5 | 1,345,189 | 872.3s / 1,590,202 msg/s |
| Dekaf | 2026-07-19T04:06:45.8327428+00:00 | 1 | 16.0 MiB / 14.2 MiB | 1821.4 MB/s | 6/5 | 1,387,864 | 899.3s / 1,576,966 msg/s |
| Dekaf (3conn) | 2026-07-19T04:07:14.3939497+00:00 | 1 | 16.0 MiB / 8.6 MiB | 2274.4 MB/s | 0/0 | 2,014 | 27.0s / 1,531,350 msg/s |
| Dekaf (3conn) | 2026-07-19T04:07:41.4071366+00:00 | 1 | 14.0 MiB / 2.4 MiB | 2451.8 MB/s | 1/0 | 5,465 | 54.0s / 1,852,502 msg/s |
| Dekaf (3conn) | 2026-07-19T04:08:08.4172252+00:00 | 1 | 14.0 MiB / 5.6 MiB | 2451.8 MB/s | 1/0 | 10,088 | 81.0s / 1,808,640 msg/s |
| Dekaf (3conn) | 2026-07-19T04:08:35.4215637+00:00 | 1 | 14.0 MiB / 1.8 MiB | 2451.8 MB/s | 1/1 | 14,511 | 108.1s / 1,500,445 msg/s |
| Dekaf (3conn) | 2026-07-19T04:09:03.4321973+00:00 | 1 | 14.0 MiB / 8.0 MiB | 2451.8 MB/s | 1/1 | 18,515 | 136.1s / 1,873,324 msg/s |
| Dekaf (3conn) | 2026-07-19T04:09:30.4473933+00:00 | 1 | 15.0 MiB / 3.9 MiB | 2451.8 MB/s | 1/1 | 22,944 | 163.1s / 1,734,824 msg/s |
| Dekaf (3conn) | 2026-07-19T04:09:57.4535157+00:00 | 1 | 14.0 MiB / 5.1 MiB | 2451.8 MB/s | 1/2 | 27,510 | 190.1s / 1,634,229 msg/s |
| Dekaf (3conn) | 2026-07-19T04:10:24.4637517+00:00 | 1 | 14.0 MiB / 1.8 MiB | 2451.8 MB/s | 1/2 | 30,867 | 217.1s / 1,190,979 msg/s |
| Dekaf (3conn) | 2026-07-19T04:10:52.4706319+00:00 | 1 | 14.0 MiB / 4.5 MiB | 2451.8 MB/s | 1/2 | 33,945 | 245.1s / 1,298,493 msg/s |
| Dekaf (3conn) | 2026-07-19T04:11:19.4827345+00:00 | 1 | 14.0 MiB / 10.1 MiB | 2451.8 MB/s | 1/2 | 37,178 | 272.1s / 1,913,163 msg/s |
| Dekaf (3conn) | 2026-07-19T04:11:46.5005064+00:00 | 1 | 12.0 MiB / 5.9 MiB | 2451.8 MB/s | 1/2 | 41,183 | 299.2s / 1,769,706 msg/s |
| Dekaf (3conn) | 2026-07-19T04:12:13.5065477+00:00 | 1 | 14.0 MiB / 12.7 MiB | 2456.6 MB/s | 1/3 | 45,178 | 326.2s / 2,002,069 msg/s |
| Dekaf (3conn) | 2026-07-19T04:12:41.5122049+00:00 | 1 | 14.0 MiB / 8.2 MiB | 2456.6 MB/s | 1/3 | 48,890 | 354.2s / 1,979,804 msg/s |
| Dekaf (3conn) | 2026-07-19T04:13:08.5190944+00:00 | 1 | 14.0 MiB / 2.1 MiB | 2456.6 MB/s | 1/3 | 52,716 | 381.2s / 1,728,080 msg/s |
| Dekaf (3conn) | 2026-07-19T04:13:35.5307825+00:00 | 1 | 14.0 MiB / 1.7 MiB | 2456.6 MB/s | 1/3 | 55,793 | 408.2s / 1,553,536 msg/s |
| Dekaf (3conn) | 2026-07-19T04:14:03.5390843+00:00 | 1 | 14.0 MiB / 3.2 MiB | 2456.6 MB/s | 1/3 | 58,601 | 436.3s / 1,683,660 msg/s |
| Dekaf (3conn) | 2026-07-19T04:14:30.5482214+00:00 | 1 | 14.0 MiB / 2.3 MiB | 2456.6 MB/s | 1/3 | 60,992 | 463.3s / 1,509,324 msg/s |
| Dekaf (3conn) | 2026-07-19T04:14:57.5599174+00:00 | 1 | 14.0 MiB / 3.5 MiB | 2475.8 MB/s | 1/3 | 64,044 | 490.3s / 1,851,072 msg/s |
| Dekaf (3conn) | 2026-07-19T04:15:24.5711904+00:00 | 1 | 14.0 MiB / 7.3 MiB | 2475.8 MB/s | 1/3 | 67,598 | 517.3s / 1,349,876 msg/s |
| Dekaf (3conn) | 2026-07-19T04:15:52.5846477+00:00 | 1 | 14.0 MiB / 4.8 MiB | 2475.8 MB/s | 1/3 | 70,460 | 545.3s / 1,772,428 msg/s |
| Dekaf (3conn) | 2026-07-19T04:16:19.5954211+00:00 | 1 | 15.0 MiB / 11.7 MiB | 2558.5 MB/s | 2/3 | 73,107 | 572.3s / 1,394,087 msg/s |
| Dekaf (3conn) | 2026-07-19T04:16:46.6069004+00:00 | 1 | 16.0 MiB / 3.4 MiB | 2558.5 MB/s | 2/3 | 75,796 | 599.3s / 1,846,979 msg/s |
| Dekaf (3conn) | 2026-07-19T04:17:13.619418+00:00 | 1 | 16.0 MiB / 3.6 MiB | 2558.5 MB/s | 3/3 | 78,393 | 626.4s / 1,643,946 msg/s |
| Dekaf (3conn) | 2026-07-19T04:17:41.6235013+00:00 | 1 | 18.0 MiB / 6.0 MiB | 2558.5 MB/s | 4/3 | 80,012 | 654.4s / 1,954,600 msg/s |
| Dekaf (3conn) | 2026-07-19T04:18:08.6415223+00:00 | 1 | 18.0 MiB / 8.7 MiB | 2558.5 MB/s | 4/3 | 81,692 | 681.4s / 2,070,604 msg/s |
| Dekaf (3conn) | 2026-07-19T04:18:35.6493419+00:00 | 1 | 18.0 MiB / 7.4 MiB | 2558.5 MB/s | 4/4 | 83,541 | 708.4s / 2,094,561 msg/s |
| Dekaf (3conn) | 2026-07-19T04:19:02.659484+00:00 | 1 | 18.0 MiB / 2.4 MiB | 2558.5 MB/s | 4/4 | 85,436 | 735.4s / 1,333,205 msg/s |
| Dekaf (3conn) | 2026-07-19T04:19:30.6737634+00:00 | 1 | 15.0 MiB / 1.8 MiB | 2558.5 MB/s | 4/4 | 87,741 | 763.4s / 2,103,200 msg/s |
| Dekaf (3conn) | 2026-07-19T04:19:57.6885119+00:00 | 1 | 18.0 MiB / 2.7 MiB | 2558.5 MB/s | 4/5 | 90,375 | 790.4s / 1,845,489 msg/s |
| Dekaf (3conn) | 2026-07-19T04:20:24.700188+00:00 | 1 | 18.0 MiB / 9.5 MiB | 2558.5 MB/s | 4/5 | 92,150 | 817.4s / 1,488,496 msg/s |
| Dekaf (3conn) | 2026-07-19T04:20:51.7044511+00:00 | 1 | 18.0 MiB / 5.8 MiB | 2558.5 MB/s | 4/5 | 93,698 | 844.5s / 1,757,571 msg/s |
| Dekaf (3conn) | 2026-07-19T04:21:19.7095967+00:00 | 1 | 18.0 MiB / 8.4 MiB | 2558.5 MB/s | 4/5 | 95,087 | 872.5s / 1,989,770 msg/s |
| Dekaf (3conn) | 2026-07-19T04:21:46.7269277+00:00 | 1 | 20.0 MiB / 5.4 MiB | 2558.5 MB/s | 4/5 | 96,626 | 899.5s / 1,447,787 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-19T03:07:15.2663503+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:07:30.2788447+00:00 | 1 | capacity | succeeded | 15,012ms | 14.0 MiB / 12.4 MiB |
| Dekaf | 2026-07-19T03:08:00.3051847+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:08:15.3198009+00:00 | 1 | capacity | succeeded | 15,014ms | 12.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-19T03:08:45.342652+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:09:00.3572235+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-19T03:10:00.4095364+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:10:15.4208983+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T03:12:15.5193998+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:12:30.5355435+00:00 | 1 | capacity | failed | 15,015ms | 12.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-19T03:16:30.7242414+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-19T03:16:45.753651+00:00 | 1 | capacity | failed | 15,029ms | 12.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-19T03:17:15.791319+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.4 MiB |
| Dekaf | 2026-07-19T03:17:30.8020226+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:21:30.9992281+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:52:16.5752775+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.2 MiB |
| Dekaf | 2026-07-19T03:52:31.5879648+00:00 | 1 | capacity | failed | 15,012ms | 16.0 MiB / 13.4 MiB |
| Dekaf | 2026-07-19T03:53:31.6602946+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:53:46.6769696+00:00 | 1 | capacity | failed | 15,016ms | 16.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-19T03:55:46.8548014+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 13.9 MiB |
| Dekaf | 2026-07-19T03:56:01.8703809+00:00 | 1 | capacity | failed | 15,015ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T04:00:02.1622085+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-19T04:00:17.1812882+00:00 | 1 | capacity | succeeded | 15,018ms | 18.0 MiB / 15.7 MiB |
| Dekaf | 2026-07-19T04:00:47.2205412+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-19T04:01:02.2540047+00:00 | 1 | capacity | failed | 15,033ms | 18.0 MiB / 17.4 MiB |
| Dekaf | 2026-07-19T04:02:02.3507169+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-19T04:02:17.3674313+00:00 | 1 | capacity | succeeded | 15,016ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T04:02:47.3906277+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T04:03:02.4025752+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T04:03:32.4283319+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T04:03:47.4404714+00:00 | 1 | capacity | failed | 15,012ms | 13.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-19T04:04:47.493913+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-19T04:05:02.5087041+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T04:05:32.5367428+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T04:05:47.5505141+00:00 | 1 | capacity | succeeded | 15,013ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T04:06:17.5798549+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 15.0 MiB |
| Dekaf | 2026-07-19T04:06:32.5949332+00:00 | 1 | capacity | succeeded | 15,015ms | 16.0 MiB / 13.1 MiB |
| Dekaf (3conn) | 2026-07-19T04:07:17.5218203+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-19T04:07:32.5402729+00:00 | 1 | capacity | succeeded | 15,018ms | 14.0 MiB / 6.5 MiB |
| Dekaf (3conn) | 2026-07-19T04:08:02.5978225+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-19T04:08:17.6275723+00:00 | 1 | capacity | failed | 15,029ms | 14.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:09:17.7302924+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-19T04:09:32.7566886+00:00 | 1 | capacity | failed | 15,026ms | 14.0 MiB / 13.1 MiB |
| Dekaf (3conn) | 2026-07-19T04:11:32.9316736+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:11:47.9529272+00:00 | 1 | capacity | failed | 15,021ms | 14.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-07-19T04:15:48.5047369+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-19T04:16:03.5316831+00:00 | 1 | capacity | succeeded | 15,026ms | 15.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:16:33.5800311+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 8.9 MiB |
| Dekaf (3conn) | 2026-07-19T04:16:48.6055106+00:00 | 1 | capacity | succeeded | 15,025ms | 16.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-19T04:17:18.6568762+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-19T04:17:33.6763592+00:00 | 1 | capacity | succeeded | 15,019ms | 18.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:18:03.7214878+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-19T04:18:18.9432363+00:00 | 1 | capacity | failed | 15,221ms | 18.0 MiB / 13.9 MiB |
| Dekaf (3conn) | 2026-07-19T04:19:19.090459+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 6.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:19:34.1107035+00:00 | 1 | capacity | failed | 15,020ms | 18.0 MiB / 8.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:21:34.3251212+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 3.5 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 43 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 45 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 134 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 426 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 1,219 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 2,734 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 2,880 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 4,891 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 6,279 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 4,964 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 2,881 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,047 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 277 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 21 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 4 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 14 |
| Dekaf | 1 | 0.001–0.002ms | 1,133 |
| Dekaf | 1 | 0.002–0.004ms | 1,403 |
| Dekaf | 1 | 0.004–0.008ms | 4,819 |
| Dekaf | 1 | 0.008–0.016ms | 28,256 |
| Dekaf | 1 | 0.016–0.032ms | 39,797 |
| Dekaf | 1 | 0.032–0.064ms | 28,283 |
| Dekaf | 1 | 0.064–0.128ms | 45,027 |
| Dekaf | 1 | 0.128–0.256ms | 107,419 |
| Dekaf | 1 | 0.256–0.512ms | 125,997 |
| Dekaf | 1 | 0.512–1.024ms | 105,877 |
| Dekaf | 1 | 1.024–2.048ms | 38,192 |
| Dekaf | 1 | 2.048–4.096ms | 4,650 |
| Dekaf | 1 | 4.096–8.192ms | 1,479 |
| Dekaf | 1 | 8.192–16.384ms | 246 |
| Dekaf | 1 | 16.384–32.768ms | 11 |
| Dekaf | 1 | 0.001–0.002ms | 1,835 |
| Dekaf | 1 | 0.002–0.004ms | 2,359 |
| Dekaf | 1 | 0.004–0.008ms | 6,254 |
| Dekaf | 1 | 0.008–0.016ms | 24,356 |
| Dekaf | 1 | 0.016–0.032ms | 35,976 |
| Dekaf | 1 | 0.032–0.064ms | 46,642 |
| Dekaf | 1 | 0.064–0.128ms | 84,596 |
| Dekaf | 1 | 0.128–0.256ms | 230,395 |
| Dekaf | 1 | 0.256–0.512ms | 256,702 |
| Dekaf | 1 | 0.512–1.024ms | 49,895 |
| Dekaf | 1 | 1.024–2.048ms | 11,340 |
| Dekaf | 1 | 2.048–4.096ms | 4,267 |
| Dekaf | 1 | 4.096–8.192ms | 1,007 |
| Dekaf | 1 | 8.192–16.384ms | 117 |
| Dekaf | 1 | 16.384–32.768ms | 3 |
| Dekaf | 1 | 65.536–131.072ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 79,571,000 | 2026-07-19T03:23:12.650338+00:00 | 137.2ms | GC pause | - | - | 88.0s / 1,212,658 msg/s | Gen2 +0 / pause +67.3ms |
| Confluent | 79,706,000 | 2026-07-19T03:23:12.7459615+00:00 | 123.3ms | GC pause | - | - | 88.0s / 1,212,658 msg/s | Gen2 +0 / pause +67.3ms |
| Confluent | 79,787,000 | 2026-07-19T03:23:12.8049931+00:00 | 159.3ms | GC pause | - | - | 88.0s / 1,212,658 msg/s | Gen2 +0 / pause +67.3ms |
| Confluent | 80,174,000 | 2026-07-19T03:23:13.1204453+00:00 | 125.2ms | GC pause | - | - | 88.0s / 1,212,658 msg/s | Gen2 +0 / pause +67.3ms |
| Confluent | 80,184,000 | 2026-07-19T03:23:13.127564+00:00 | 130.6ms | GC pause | - | - | 88.0s / 1,212,658 msg/s | Gen2 +0 / pause +67.3ms |
| Confluent | 80,253,000 | 2026-07-19T03:23:13.1762748+00:00 | 142.7ms | GC pause | - | - | 88.0s / 1,212,658 msg/s | Gen2 +0 / pause +67.3ms |
| Confluent | 80,388,000 | 2026-07-19T03:23:13.3631938+00:00 | 103.5ms | GC pause | - | - | 89.0s / 1,166,060 msg/s | Gen2 +0 / pause +150.5ms |
| Confluent | 164,269,000 | 2026-07-19T03:24:41.0145148+00:00 | 102.3ms | GC pause | - | - | 176.1s / 1,170,215 msg/s | Gen2 +0 / pause +97.6ms |
| Confluent | 166,887,000 | 2026-07-19T03:24:43.135431+00:00 | 129.7ms | GC pause | - | - | 178.1s / 1,290,974 msg/s | Gen2 +0 / pause +89.4ms |
| Confluent | 167,187,000 | 2026-07-19T03:24:43.3796439+00:00 | 141.8ms | GC pause | - | - | 179.1s / 1,156,801 msg/s | Gen2 +0 / pause +148.2ms |
| Confluent | 168,203,000 | 2026-07-19T03:24:44.2815397+00:00 | 148.5ms | GC pause | - | - | 179.1s / 1,156,801 msg/s | Gen2 +0 / pause +58.8ms |
| Confluent | 168,310,000 | 2026-07-19T03:24:44.3689954+00:00 | 153.0ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +144.5ms |
| Confluent | 168,506,000 | 2026-07-19T03:24:44.5333452+00:00 | 156.1ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +85.7ms |
| Confluent | 168,616,000 | 2026-07-19T03:24:44.6350378+00:00 | 157.2ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +85.7ms |
| Confluent | 168,622,000 | 2026-07-19T03:24:44.6396598+00:00 | 148.6ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +85.7ms |
| Confluent | 168,863,000 | 2026-07-19T03:24:44.8947716+00:00 | 125.6ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +85.7ms |
| Confluent | 168,869,000 | 2026-07-19T03:24:44.9000275+00:00 | 107.0ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +85.7ms |
| Confluent | 169,263,000 | 2026-07-19T03:24:45.2414583+00:00 | 147.3ms | GC pause | - | - | 180.1s / 1,050,957 msg/s | Gen2 +0 / pause +85.7ms |
| Confluent | 169,388,000 | 2026-07-19T03:24:45.3689702+00:00 | 203.6ms | GC pause | - | - | 181.1s / 1,109,930 msg/s | Gen2 +0 / pause +188.9ms |
| Confluent | 169,460,000 | 2026-07-19T03:24:45.4590178+00:00 | 108.4ms | GC pause | - | - | 181.1s / 1,109,930 msg/s | Gen2 +0 / pause +103.3ms |
| Confluent | 169,518,000 | 2026-07-19T03:24:45.5415682+00:00 | 150.9ms | GC pause | - | - | 181.1s / 1,109,930 msg/s | Gen2 +0 / pause +103.3ms |
| Confluent | 169,857,000 | 2026-07-19T03:24:45.8350675+00:00 | 135.0ms | GC pause | - | - | 181.1s / 1,109,930 msg/s | Gen2 +0 / pause +103.3ms |
| Confluent | 266,547,000 | 2026-07-19T03:26:18.6544808+00:00 | 134.8ms | GC pause | - | - | 274.1s / 1,084,557 msg/s | Gen2 +0 / pause +62.8ms |
| Confluent | 266,609,000 | 2026-07-19T03:26:18.6993773+00:00 | 114.8ms | GC pause | - | - | 274.1s / 1,084,557 msg/s | Gen2 +0 / pause +62.8ms |
| Confluent | 266,687,000 | 2026-07-19T03:26:18.7929116+00:00 | 120.4ms | GC pause | - | - | 274.1s / 1,084,557 msg/s | Gen2 +0 / pause +62.8ms |
| Confluent | 271,568,000 | 2026-07-19T03:26:22.8108427+00:00 | 117.7ms | GC pause | - | - | 278.1s / 1,169,085 msg/s | Gen2 +0 / pause +96.4ms |
| Confluent | 312,747,000 | 2026-07-19T03:26:56.4655676+00:00 | 124.7ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +169.6ms |
| Confluent | 312,820,000 | 2026-07-19T03:26:56.510061+00:00 | 122.9ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 312,828,000 | 2026-07-19T03:26:56.5159641+00:00 | 143.4ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 312,855,000 | 2026-07-19T03:26:56.5335562+00:00 | 137.2ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 312,935,000 | 2026-07-19T03:26:56.6063457+00:00 | 133.6ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 313,045,000 | 2026-07-19T03:26:56.6967673+00:00 | 140.0ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 313,597,000 | 2026-07-19T03:26:57.199787+00:00 | 124.0ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 313,762,000 | 2026-07-19T03:26:57.3292763+00:00 | 102.4ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 313,814,000 | 2026-07-19T03:26:57.3665587+00:00 | 119.7ms | GC pause | - | - | 312.2s / 1,192,397 msg/s | Gen2 +0 / pause +90.6ms |
| Confluent | 313,851,000 | 2026-07-19T03:26:57.3934839+00:00 | 153.9ms | GC pause | - | - | 313.2s / 1,158,941 msg/s | Gen2 +0 / pause +162.6ms |
| Confluent | 314,064,000 | 2026-07-19T03:26:57.5484197+00:00 | 141.0ms | GC pause | - | - | 313.2s / 1,158,941 msg/s | Gen2 +0 / pause +72.0ms |
| Confluent | 314,281,000 | 2026-07-19T03:26:57.7374247+00:00 | 189.7ms | GC pause | - | - | 313.2s / 1,158,941 msg/s | Gen2 +0 / pause +72.0ms |
| Confluent | 314,295,000 | 2026-07-19T03:26:57.752245+00:00 | 142.8ms | GC pause | - | - | 313.2s / 1,158,941 msg/s | Gen2 +0 / pause +72.0ms |
| Confluent | 314,828,000 | 2026-07-19T03:26:58.2494965+00:00 | 145.4ms | GC pause | - | - | 313.2s / 1,158,941 msg/s | Gen2 +0 / pause +72.0ms |
| Confluent | 314,963,000 | 2026-07-19T03:26:58.3564997+00:00 | 118.4ms | GC pause | - | - | 313.2s / 1,158,941 msg/s | Gen2 +0 / pause +72.0ms |
| Confluent | 315,020,000 | 2026-07-19T03:26:58.398176+00:00 | 125.4ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +122.1ms |
| Confluent | 315,097,000 | 2026-07-19T03:26:58.4527235+00:00 | 198.1ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +122.1ms |
| Confluent | 315,505,000 | 2026-07-19T03:26:58.8103554+00:00 | 155.6ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +50.1ms |
| Confluent | 315,610,000 | 2026-07-19T03:26:58.8912778+00:00 | 214.1ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +50.1ms |
| Confluent | 315,715,000 | 2026-07-19T03:26:59.0173694+00:00 | 149.6ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +50.1ms |
| Confluent | 315,805,000 | 2026-07-19T03:26:59.0784019+00:00 | 166.9ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +50.1ms |
| Confluent | 315,824,000 | 2026-07-19T03:26:59.0912223+00:00 | 170.1ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +50.1ms |
| Confluent | 315,899,000 | 2026-07-19T03:26:59.156051+00:00 | 175.8ms | GC pause | - | - | 314.2s / 1,182,925 msg/s | Gen2 +0 / pause +50.1ms |
| Confluent | 316,191,000 | 2026-07-19T03:26:59.3826443+00:00 | 315.7ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +127.1ms |
| Confluent | 316,224,000 | 2026-07-19T03:26:59.4103138+00:00 | 215.2ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +127.1ms |
| Confluent | 316,293,000 | 2026-07-19T03:26:59.4622171+00:00 | 244.2ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +127.1ms |
| Confluent | 316,316,000 | 2026-07-19T03:26:59.4841181+00:00 | 215.6ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +127.1ms |
| Confluent | 316,317,000 | 2026-07-19T03:26:59.4848759+00:00 | 333.7ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +127.1ms |
| Confluent | 316,416,000 | 2026-07-19T03:26:59.5740688+00:00 | 224.5ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +77.0ms |
| Confluent | 316,442,000 | 2026-07-19T03:26:59.5960343+00:00 | 204.0ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +77.0ms |
| Confluent | 316,520,000 | 2026-07-19T03:26:59.6805338+00:00 | 214.4ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +77.0ms |
| Confluent | 316,575,000 | 2026-07-19T03:26:59.7346648+00:00 | 175.6ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +77.0ms |
| Confluent | 316,717,000 | 2026-07-19T03:26:59.9614827+00:00 | 207.8ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +77.0ms |
| Confluent | 316,967,000 | 2026-07-19T03:27:00.1758224+00:00 | 210.9ms | GC pause | - | - | 315.2s / 1,030,939 msg/s | Gen2 +0 / pause +77.0ms |
| Confluent | 317,478,000 | 2026-07-19T03:27:00.5900316+00:00 | 250.9ms | GC pause | - | - | 316.2s / 1,134,148 msg/s | Gen2 +0 / pause +65.0ms |
| Confluent | 317,536,000 | 2026-07-19T03:27:00.6348587+00:00 | 116.3ms | GC pause | - | - | 316.2s / 1,134,148 msg/s | Gen2 +0 / pause +65.0ms |
| Confluent | 317,865,000 | 2026-07-19T03:27:00.9567761+00:00 | 101.2ms | GC pause | - | - | 316.2s / 1,134,148 msg/s | Gen2 +0 / pause +65.0ms |
| Confluent | 318,186,000 | 2026-07-19T03:27:01.2382384+00:00 | 119.1ms | GC pause | - | - | 316.2s / 1,134,148 msg/s | Gen2 +0 / pause +65.0ms |
| Confluent | 318,499,000 | 2026-07-19T03:27:01.4923015+00:00 | 157.7ms | GC pause | - | - | 317.2s / 1,134,975 msg/s | Gen2 +0 / pause +105.4ms |
| Confluent | 318,745,000 | 2026-07-19T03:27:01.7128565+00:00 | 157.4ms | GC pause | - | - | 317.2s / 1,134,975 msg/s | Gen2 +0 / pause +40.4ms |
| Confluent | 318,845,000 | 2026-07-19T03:27:01.8242455+00:00 | 127.5ms | GC pause | - | - | 317.2s / 1,134,975 msg/s | Gen2 +0 / pause +40.4ms |
| Confluent | 319,400,000 | 2026-07-19T03:27:02.2897261+00:00 | 181.0ms | GC pause | - | - | 317.2s / 1,134,975 msg/s | Gen2 +0 / pause +40.4ms |
| Confluent | 319,523,000 | 2026-07-19T03:27:02.4014506+00:00 | 177.3ms | GC pause | - | - | 318.2s / 1,129,459 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 319,547,000 | 2026-07-19T03:27:02.4185678+00:00 | 351.9ms | GC pause | - | - | 318.2s / 1,129,459 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 319,750,000 | 2026-07-19T03:27:02.5941825+00:00 | 212.3ms | GC pause | - | - | 318.2s / 1,129,459 msg/s | Gen2 +0 / pause +47.9ms |
| Confluent | 319,937,000 | 2026-07-19T03:27:02.7488707+00:00 | 388.4ms | GC pause | - | - | 318.2s / 1,129,459 msg/s | Gen2 +0 / pause +47.9ms |
| Confluent | 320,155,000 | 2026-07-19T03:27:02.9379506+00:00 | 178.7ms | GC pause | - | - | 318.2s / 1,129,459 msg/s | Gen2 +0 / pause +47.9ms |
| Confluent | 320,381,000 | 2026-07-19T03:27:03.1258005+00:00 | 409.7ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 320,406,000 | 2026-07-19T03:27:03.145367+00:00 | 191.2ms | GC pause | - | - | 318.2s / 1,129,459 msg/s | Gen2 +0 / pause +47.9ms |
| Confluent | 320,654,000 | 2026-07-19T03:27:03.3848013+00:00 | 160.4ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 320,798,000 | 2026-07-19T03:27:03.5231932+00:00 | 414.5ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 320,850,000 | 2026-07-19T03:27:03.5619475+00:00 | 233.9ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 320,954,000 | 2026-07-19T03:27:03.6444253+00:00 | 175.5ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 321,241,000 | 2026-07-19T03:27:03.8882683+00:00 | 475.2ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 321,281,000 | 2026-07-19T03:27:03.9196874+00:00 | 483.8ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 321,364,000 | 2026-07-19T03:27:03.9875954+00:00 | 233.0ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 321,483,000 | 2026-07-19T03:27:04.0911804+00:00 | 308.7ms | GC pause | - | - | 319.2s / 1,087,781 msg/s | Gen2 +0 / pause +55.8ms |
| Confluent | 321,743,000 | 2026-07-19T03:27:04.3752327+00:00 | 247.6ms | GC pause | - | - | 320.2s / 1,133,689 msg/s | Gen2 +0 / pause +105.1ms |
| Confluent | 321,955,000 | 2026-07-19T03:27:04.5989481+00:00 | 162.8ms | GC pause | - | - | 320.2s / 1,133,689 msg/s | Gen2 +0 / pause +49.3ms |
| Confluent | 322,104,000 | 2026-07-19T03:27:04.7207032+00:00 | 151.9ms | GC pause | - | - | 320.2s / 1,133,689 msg/s | Gen2 +0 / pause +49.3ms |
| Confluent | 322,127,000 | 2026-07-19T03:27:04.7360325+00:00 | 458.9ms | GC pause | - | - | 320.2s / 1,133,689 msg/s | Gen2 +0 / pause +49.3ms |
| Confluent | 322,533,000 | 2026-07-19T03:27:05.1412127+00:00 | 218.6ms | GC pause | - | - | 320.2s / 1,133,689 msg/s | Gen2 +0 / pause +49.3ms |
| Confluent | 323,009,000 | 2026-07-19T03:27:05.5133169+00:00 | 241.2ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +115.6ms |
| Confluent | 323,060,000 | 2026-07-19T03:27:05.5549079+00:00 | 310.7ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,162,000 | 2026-07-19T03:27:05.6351705+00:00 | 192.4ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,194,000 | 2026-07-19T03:27:05.657732+00:00 | 244.4ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,392,000 | 2026-07-19T03:27:05.8403502+00:00 | 195.2ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,420,000 | 2026-07-19T03:27:05.8614971+00:00 | 343.0ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,461,000 | 2026-07-19T03:27:05.8965762+00:00 | 562.7ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,575,000 | 2026-07-19T03:27:05.9946453+00:00 | 270.2ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,760,000 | 2026-07-19T03:27:06.1463271+00:00 | 346.7ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,766,000 | 2026-07-19T03:27:06.1513609+00:00 | 273.3ms | GC pause | - | - | 321.2s / 1,059,065 msg/s | Gen2 +0 / pause +66.3ms |
| Confluent | 323,860,000 | 2026-07-19T03:27:06.2342967+00:00 | 350.0ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 324,316,000 | 2026-07-19T03:27:06.7302103+00:00 | 204.5ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 324,361,000 | 2026-07-19T03:27:06.7678988+00:00 | 556.3ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 324,378,000 | 2026-07-19T03:27:06.7821933+00:00 | 555.9ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 324,534,000 | 2026-07-19T03:27:06.9138679+00:00 | 214.6ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 324,564,000 | 2026-07-19T03:27:06.9384524+00:00 | 208.7ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 324,828,000 | 2026-07-19T03:27:07.1417649+00:00 | 622.1ms | GC pause | - | - | 323.2s / 1,064,560 msg/s | Gen2 +0 / pause +124.8ms |
| Confluent | 324,935,000 | 2026-07-19T03:27:07.2366241+00:00 | 257.8ms | GC pause | - | - | 322.2s / 1,157,577 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 325,468,000 | 2026-07-19T03:27:07.7438928+00:00 | 618.6ms | GC pause | - | - | 323.2s / 1,064,560 msg/s | Gen2 +0 / pause +59.1ms |
| Confluent | 325,575,000 | 2026-07-19T03:27:07.8680638+00:00 | 211.6ms | GC pause | - | - | 323.2s / 1,064,560 msg/s | Gen2 +0 / pause +59.1ms |
| Confluent | 325,666,000 | 2026-07-19T03:27:07.9730084+00:00 | 189.3ms | GC pause | - | - | 323.2s / 1,064,560 msg/s | Gen2 +0 / pause +59.1ms |
| Confluent | 325,858,000 | 2026-07-19T03:27:08.1353968+00:00 | 571.8ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +102.3ms |
| Confluent | 325,869,000 | 2026-07-19T03:27:08.1439873+00:00 | 213.1ms | GC pause | - | - | 323.2s / 1,064,560 msg/s | Gen2 +0 / pause +59.1ms |
| Confluent | 325,959,000 | 2026-07-19T03:27:08.2168104+00:00 | 216.3ms | GC pause | - | - | 323.2s / 1,064,560 msg/s | Gen2 +0 / pause +59.1ms |
| Confluent | 326,140,000 | 2026-07-19T03:27:08.3606025+00:00 | 313.6ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +102.3ms |
| Confluent | 326,214,000 | 2026-07-19T03:27:08.4201717+00:00 | 205.6ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +102.3ms |
| Confluent | 326,251,000 | 2026-07-19T03:27:08.4504766+00:00 | 633.6ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +102.3ms |
| Confluent | 326,349,000 | 2026-07-19T03:27:08.5891873+00:00 | 199.0ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +43.2ms |
| Confluent | 326,402,000 | 2026-07-19T03:27:08.6372886+00:00 | 120.7ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +43.2ms |
| Confluent | 326,415,000 | 2026-07-19T03:27:08.6481803+00:00 | 204.9ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +43.2ms |
| Confluent | 326,479,000 | 2026-07-19T03:27:08.6992325+00:00 | 206.0ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +43.2ms |
| Confluent | 326,482,000 | 2026-07-19T03:27:08.7013463+00:00 | 125.6ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +43.2ms |
| Confluent | 326,699,000 | 2026-07-19T03:27:08.8886328+00:00 | 210.2ms | GC pause | - | - | 324.2s / 1,046,352 msg/s | Gen2 +0 / pause +43.2ms |
| Confluent | 327,038,000 | 2026-07-19T03:27:09.2476636+00:00 | 556.8ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +93.5ms |
| Confluent | 327,133,000 | 2026-07-19T03:27:09.3328587+00:00 | 226.6ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +93.5ms |
| Confluent | 327,281,000 | 2026-07-19T03:27:09.4645667+00:00 | 565.9ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +93.5ms |
| Confluent | 327,395,000 | 2026-07-19T03:27:09.5638672+00:00 | 141.9ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,479,000 | 2026-07-19T03:27:09.6383333+00:00 | 154.9ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,543,000 | 2026-07-19T03:27:09.6971414+00:00 | 259.4ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,693,000 | 2026-07-19T03:27:09.8268896+00:00 | 269.0ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,747,000 | 2026-07-19T03:27:09.8739091+00:00 | 582.9ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,779,000 | 2026-07-19T03:27:09.9046118+00:00 | 156.9ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,883,000 | 2026-07-19T03:27:10.0015053+00:00 | 262.0ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,894,000 | 2026-07-19T03:27:10.0102968+00:00 | 135.0ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 327,975,000 | 2026-07-19T03:27:10.0819206+00:00 | 158.6ms | GC pause | - | - | 325.2s / 1,083,002 msg/s | Gen2 +0 / pause +50.3ms |
| Confluent | 328,337,000 | 2026-07-19T03:27:10.4399464+00:00 | 420.0ms | GC pause | - | - | 326.2s / 912,513 msg/s | Gen2 +0 / pause +160.4ms |
| Confluent | 328,381,000 | 2026-07-19T03:27:10.4772978+00:00 | 387.0ms | GC pause | - | - | 326.2s / 912,513 msg/s | Gen2 +0 / pause +160.4ms |
| Confluent | 328,443,000 | 2026-07-19T03:27:10.5340814+00:00 | 199.4ms | GC pause | - | - | 326.2s / 912,513 msg/s | Gen2 +0 / pause +110.0ms |
| Confluent | 328,465,000 | 2026-07-19T03:27:10.5535521+00:00 | 152.0ms | GC pause | - | - | 326.2s / 912,513 msg/s | Gen2 +0 / pause +110.0ms |
| Confluent | 330,069,000 | 2026-07-19T03:27:12.0433286+00:00 | 121.2ms | GC pause | - | - | 327.2s / 1,373,782 msg/s | Gen2 +0 / pause +62.1ms |
| Confluent | 330,366,000 | 2026-07-19T03:27:12.2786234+00:00 | 132.1ms | GC pause | - | - | 327.2s / 1,373,782 msg/s | Gen2 +0 / pause +62.1ms |
| Confluent | 330,372,000 | 2026-07-19T03:27:12.2821801+00:00 | 128.2ms | GC pause | - | - | 327.2s / 1,373,782 msg/s | Gen2 +0 / pause +62.1ms |
| Confluent | 330,524,000 | 2026-07-19T03:27:12.3859687+00:00 | 148.7ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 330,550,000 | 2026-07-19T03:27:12.4054898+00:00 | 162.0ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 330,660,000 | 2026-07-19T03:27:12.4845878+00:00 | 177.0ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 330,751,000 | 2026-07-19T03:27:12.5544908+00:00 | 221.1ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 330,961,000 | 2026-07-19T03:27:12.7212978+00:00 | 228.2ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,059,000 | 2026-07-19T03:27:12.8280717+00:00 | 161.5ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,149,000 | 2026-07-19T03:27:12.8926098+00:00 | 174.7ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,162,000 | 2026-07-19T03:27:12.9123136+00:00 | 165.8ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,254,000 | 2026-07-19T03:27:13.0068651+00:00 | 190.6ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,288,000 | 2026-07-19T03:27:13.0345969+00:00 | 233.3ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,344,000 | 2026-07-19T03:27:13.075784+00:00 | 196.1ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,409,000 | 2026-07-19T03:27:13.1278536+00:00 | 186.8ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,476,000 | 2026-07-19T03:27:13.1730704+00:00 | 202.3ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,659,000 | 2026-07-19T03:27:13.4122822+00:00 | 101.6ms | GC pause | - | - | 328.2s / 1,084,582 msg/s | Gen2 +0 / pause +49.0ms |
| Confluent | 331,785,000 | 2026-07-19T03:27:13.5123557+00:00 | 112.8ms | GC pause | - | - | 329.2s / 1,148,368 msg/s | Gen2 +0 / pause +110.8ms |
| Confluent | 332,117,000 | 2026-07-19T03:27:13.8202839+00:00 | 186.3ms | GC pause | - | - | 329.2s / 1,148,368 msg/s | Gen2 +0 / pause +61.9ms |
| Confluent | 332,270,000 | 2026-07-19T03:27:13.9497819+00:00 | 126.0ms | GC pause | - | - | 329.2s / 1,148,368 msg/s | Gen2 +0 / pause +61.9ms |
| Confluent | 332,651,000 | 2026-07-19T03:27:14.278999+00:00 | 185.9ms | GC pause | - | - | 329.2s / 1,148,368 msg/s | Gen2 +0 / pause +61.9ms |
| Confluent | 332,717,000 | 2026-07-19T03:27:14.3328473+00:00 | 194.5ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +152.0ms |
| Confluent | 332,777,000 | 2026-07-19T03:27:14.3832501+00:00 | 189.1ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +152.0ms |
| Confluent | 333,004,000 | 2026-07-19T03:27:14.568954+00:00 | 111.5ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +90.1ms |
| Confluent | 333,011,000 | 2026-07-19T03:27:14.5746084+00:00 | 267.4ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +90.1ms |
| Confluent | 333,100,000 | 2026-07-19T03:27:14.6506518+00:00 | 172.7ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +90.1ms |
| Confluent | 333,189,000 | 2026-07-19T03:27:14.7206492+00:00 | 133.1ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +90.1ms |
| Confluent | 333,215,000 | 2026-07-19T03:27:14.7416563+00:00 | 136.2ms | GC pause | - | - | 330.2s / 1,064,365 msg/s | Gen2 +0 / pause +90.1ms |
| Confluent | 339,077,000 | 2026-07-19T03:27:19.4732925+00:00 | 146.1ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +105.6ms |
| Confluent | 339,088,000 | 2026-07-19T03:27:19.4798964+00:00 | 144.2ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +105.6ms |
| Confluent | 339,197,000 | 2026-07-19T03:27:19.5519655+00:00 | 165.7ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,229,000 | 2026-07-19T03:27:19.5751057+00:00 | 151.6ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,247,000 | 2026-07-19T03:27:19.5872837+00:00 | 186.5ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,557,000 | 2026-07-19T03:27:19.8647491+00:00 | 194.7ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,561,000 | 2026-07-19T03:27:19.8676299+00:00 | 191.9ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,604,000 | 2026-07-19T03:27:19.8988161+00:00 | 157.5ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,645,000 | 2026-07-19T03:27:19.9273952+00:00 | 176.9ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,856,000 | 2026-07-19T03:27:20.0950127+00:00 | 204.5ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,885,000 | 2026-07-19T03:27:20.118516+00:00 | 201.2ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 339,982,000 | 2026-07-19T03:27:20.203672+00:00 | 184.5ms | GC pause | - | - | 335.2s / 1,216,898 msg/s | Gen2 +0 / pause +42.2ms |
| Confluent | 340,188,000 | 2026-07-19T03:27:20.3769569+00:00 | 279.4ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +108.2ms |
| Confluent | 340,217,000 | 2026-07-19T03:27:20.4023986+00:00 | 279.3ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +108.2ms |
| Confluent | 340,477,000 | 2026-07-19T03:27:20.6232979+00:00 | 289.3ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 340,728,000 | 2026-07-19T03:27:20.8674105+00:00 | 250.4ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 340,789,000 | 2026-07-19T03:27:20.9143515+00:00 | 193.0ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 340,801,000 | 2026-07-19T03:27:20.9227309+00:00 | 260.5ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 340,814,000 | 2026-07-19T03:27:20.9383304+00:00 | 180.3ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 341,029,000 | 2026-07-19T03:27:21.1424878+00:00 | 167.1ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 341,059,000 | 2026-07-19T03:27:21.163098+00:00 | 176.0ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 341,134,000 | 2026-07-19T03:27:21.2294573+00:00 | 163.6ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 341,159,000 | 2026-07-19T03:27:21.2484547+00:00 | 172.5ms | GC pause | - | - | 336.2s / 1,120,080 msg/s | Gen2 +0 / pause +66.0ms |
| Confluent | 341,283,000 | 2026-07-19T03:27:21.3542642+00:00 | 192.4ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 341,408,000 | 2026-07-19T03:27:21.4624373+00:00 | 259.8ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +131.6ms |
| Confluent | 341,727,000 | 2026-07-19T03:27:21.7523923+00:00 | 262.1ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 341,744,000 | 2026-07-19T03:27:21.7861453+00:00 | 138.5ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 341,943,000 | 2026-07-19T03:27:22.0093079+00:00 | 117.3ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 342,157,000 | 2026-07-19T03:27:22.1819605+00:00 | 243.4ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 342,158,000 | 2026-07-19T03:27:22.1829079+00:00 | 242.5ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 342,173,000 | 2026-07-19T03:27:22.1949686+00:00 | 157.4ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 342,286,000 | 2026-07-19T03:27:22.2910786+00:00 | 131.4ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 342,303,000 | 2026-07-19T03:27:22.3070312+00:00 | 150.7ms | GC pause | - | - | 337.2s / 1,052,906 msg/s | Gen2 +0 / pause +65.7ms |
| Confluent | 343,011,000 | 2026-07-19T03:27:22.9773595+00:00 | 109.1ms | GC pause | - | - | 338.2s / 1,098,476 msg/s | Gen2 +0 / pause +87.2ms |
| Confluent | 344,311,000 | 2026-07-19T03:27:23.9757607+00:00 | 119.7ms | GC pause | - | - | 339.2s / 1,406,783 msg/s | Gen2 +0 / pause +61.5ms |
| Confluent | 344,358,000 | 2026-07-19T03:27:24.0057043+00:00 | 128.3ms | GC pause | - | - | 339.2s / 1,406,783 msg/s | Gen2 +0 / pause +61.5ms |
| Confluent | 344,390,000 | 2026-07-19T03:27:24.0248688+00:00 | 127.3ms | GC pause | - | - | 339.2s / 1,406,783 msg/s | Gen2 +0 / pause +61.5ms |
| Confluent | 344,500,000 | 2026-07-19T03:27:24.1004639+00:00 | 142.7ms | GC pause | - | - | 339.2s / 1,406,783 msg/s | Gen2 +0 / pause +61.5ms |
| Confluent | 344,531,000 | 2026-07-19T03:27:24.1377593+00:00 | 143.8ms | GC pause | - | - | 339.2s / 1,406,783 msg/s | Gen2 +0 / pause +61.5ms |
| Confluent | 345,112,000 | 2026-07-19T03:27:24.5709298+00:00 | 139.3ms | GC pause | - | - | 340.2s / 1,059,287 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 345,158,000 | 2026-07-19T03:27:24.6071719+00:00 | 219.7ms | GC pause | - | - | 340.2s / 1,059,287 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 345,679,000 | 2026-07-19T03:27:25.0662407+00:00 | 159.4ms | GC pause | - | - | 340.2s / 1,059,287 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 345,735,000 | 2026-07-19T03:27:25.1109744+00:00 | 159.1ms | GC pause | - | - | 340.2s / 1,059,287 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 345,792,000 | 2026-07-19T03:27:25.1524391+00:00 | 158.4ms | GC pause | - | - | 340.2s / 1,059,287 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 349,259,000 | 2026-07-19T03:27:27.7725799+00:00 | 137.0ms | GC pause | - | - | 343.2s / 1,315,255 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 349,314,000 | 2026-07-19T03:27:27.8119889+00:00 | 145.7ms | GC pause | - | - | 343.2s / 1,315,255 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 349,426,000 | 2026-07-19T03:27:27.903731+00:00 | 133.1ms | GC pause | - | - | 343.2s / 1,315,255 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 349,701,000 | 2026-07-19T03:27:28.1722336+00:00 | 127.0ms | GC pause | - | - | 343.2s / 1,315,255 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 349,928,000 | 2026-07-19T03:27:28.3266295+00:00 | 173.9ms | GC pause | - | - | 343.2s / 1,315,255 msg/s | Gen2 +0 / pause +72.3ms |
| Confluent | 350,122,000 | 2026-07-19T03:27:28.4659841+00:00 | 148.7ms | GC pause | - | - | 344.2s / 1,121,213 msg/s | Gen2 +0 / pause +123.5ms |
| Confluent | 350,208,000 | 2026-07-19T03:27:28.5285525+00:00 | 226.4ms | GC pause | - | - | 344.2s / 1,121,213 msg/s | Gen2 +0 / pause +123.5ms |
| Confluent | 350,278,000 | 2026-07-19T03:27:28.5808992+00:00 | 244.0ms | GC pause | - | - | 344.2s / 1,121,213 msg/s | Gen2 +0 / pause +51.1ms |
| Confluent | 350,325,000 | 2026-07-19T03:27:28.6169722+00:00 | 178.5ms | GC pause | - | - | 344.2s / 1,121,213 msg/s | Gen2 +0 / pause +51.1ms |
| Confluent | 350,644,000 | 2026-07-19T03:27:28.9592792+00:00 | 102.3ms | GC pause | - | - | 344.2s / 1,121,213 msg/s | Gen2 +0 / pause +51.1ms |
| Confluent | 350,721,000 | 2026-07-19T03:27:29.0145173+00:00 | 190.6ms | GC pause | - | - | 344.2s / 1,121,213 msg/s | Gen2 +0 / pause +51.1ms |
| Confluent | 351,297,000 | 2026-07-19T03:27:29.5063321+00:00 | 204.2ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +122.2ms |
| Confluent | 351,439,000 | 2026-07-19T03:27:29.6146194+00:00 | 133.7ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,497,000 | 2026-07-19T03:27:29.6628054+00:00 | 243.5ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,537,000 | 2026-07-19T03:27:29.6916443+00:00 | 259.4ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,635,000 | 2026-07-19T03:27:29.7858227+00:00 | 155.2ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,650,000 | 2026-07-19T03:27:29.7986604+00:00 | 174.1ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,734,000 | 2026-07-19T03:27:29.8682649+00:00 | 131.3ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,971,000 | 2026-07-19T03:27:30.0957415+00:00 | 183.8ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 351,993,000 | 2026-07-19T03:27:30.1125905+00:00 | 124.4ms | GC pause | - | - | 345.2s / 1,098,347 msg/s | Gen2 +0 / pause +71.0ms |
| Confluent | 376,478,000 | 2026-07-19T03:27:55.7215754+00:00 | 110.1ms | GC pause | - | - | 371.2s / 1,179,743 msg/s | Gen2 +0 / pause +73.4ms |
| Confluent | 377,003,000 | 2026-07-19T03:27:56.1260721+00:00 | 103.7ms | GC pause | - | - | 371.2s / 1,179,743 msg/s | Gen2 +0 / pause +73.4ms |
| Confluent | 377,239,000 | 2026-07-19T03:27:56.3096061+00:00 | 102.1ms | GC pause | - | - | 371.2s / 1,179,743 msg/s | Gen2 +0 / pause +73.4ms |
| Confluent | 379,796,000 | 2026-07-19T03:27:58.4817629+00:00 | 109.1ms | GC pause | - | - | 374.2s / 1,261,069 msg/s | Gen2 +0 / pause +188.7ms |
| Confluent | 380,786,000 | 2026-07-19T03:27:59.3114312+00:00 | 102.8ms | GC pause | - | - | 374.2s / 1,261,069 msg/s | Gen2 +0 / pause +96.7ms |
| Confluent | 381,051,000 | 2026-07-19T03:27:59.5099273+00:00 | 171.0ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +168.8ms |
| Confluent | 381,140,000 | 2026-07-19T03:27:59.5819909+00:00 | 144.3ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,276,000 | 2026-07-19T03:27:59.6794857+00:00 | 163.2ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,420,000 | 2026-07-19T03:27:59.7929929+00:00 | 206.4ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,478,000 | 2026-07-19T03:27:59.8337941+00:00 | 239.6ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,499,000 | 2026-07-19T03:27:59.849935+00:00 | 200.5ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,527,000 | 2026-07-19T03:27:59.8761514+00:00 | 252.4ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,579,000 | 2026-07-19T03:27:59.9208582+00:00 | 208.7ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,677,000 | 2026-07-19T03:27:59.9917371+00:00 | 296.8ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,750,000 | 2026-07-19T03:28:00.0647436+00:00 | 255.8ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,903,000 | 2026-07-19T03:28:00.1982987+00:00 | 251.1ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 381,917,000 | 2026-07-19T03:28:00.2099961+00:00 | 301.6ms | GC pause | - | - | 375.2s / 1,175,261 msg/s | Gen2 +0 / pause +72.1ms |
| Confluent | 382,130,000 | 2026-07-19T03:28:00.4238894+00:00 | 249.1ms | GC pause | - | - | 376.2s / 932,716 msg/s | Gen2 +0 / pause +203.3ms |
| Confluent | 382,504,000 | 2026-07-19T03:28:00.812365+00:00 | 217.7ms | GC pause | - | - | 376.2s / 932,716 msg/s | Gen2 +0 / pause +131.2ms |
| Confluent | 382,517,000 | 2026-07-19T03:28:00.8303518+00:00 | 274.0ms | GC pause | - | - | 376.2s / 932,716 msg/s | Gen2 +0 / pause +131.2ms |
| Confluent | 382,622,000 | 2026-07-19T03:28:00.9101733+00:00 | 202.7ms | GC pause | - | - | 376.2s / 932,716 msg/s | Gen2 +0 / pause +131.2ms |
| Confluent | 919,521,000 | 2026-07-19T03:35:30.8077542+00:00 | 370.0ms | GC pause | - | - | 826.5s / 836,856 msg/s | Gen2 +0 / pause +162.0ms |
| Confluent | 919,544,000 | 2026-07-19T03:35:30.8362702+00:00 | 350.7ms | GC pause | - | - | 826.5s / 836,856 msg/s | Gen2 +0 / pause +76.7ms |
| Confluent | 919,547,000 | 2026-07-19T03:35:30.8399249+00:00 | 347.4ms | GC pause | - | - | 826.5s / 836,856 msg/s | Gen2 +0 / pause +76.7ms |
| Confluent | 919,678,000 | 2026-07-19T03:35:30.9996574+00:00 | 270.0ms | GC pause | - | - | 826.5s / 836,856 msg/s | Gen2 +0 / pause +76.7ms |
| Confluent | 919,713,000 | 2026-07-19T03:35:31.0434046+00:00 | 241.4ms | GC pause | - | - | 826.5s / 836,856 msg/s | Gen2 +0 / pause +76.7ms |
| Confluent | 919,721,000 | 2026-07-19T03:35:31.0533815+00:00 | 241.0ms | GC pause | - | - | 826.5s / 836,856 msg/s | Gen2 +0 / pause +76.7ms |
| Confluent | 153,757,000 | 2026-07-19T03:39:02.718156+00:00 | 101.9ms | GC pause | - | - | 137.1s / 1,248,844 msg/s | Gen2 +0 / pause +97.9ms |
| Confluent | 177,747,000 | 2026-07-19T03:39:25.1686726+00:00 | 101.4ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,771,000 | 2026-07-19T03:39:25.1857158+00:00 | 114.1ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,784,000 | 2026-07-19T03:39:25.1954419+00:00 | 107.3ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,785,000 | 2026-07-19T03:39:25.1958988+00:00 | 103.7ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,787,000 | 2026-07-19T03:39:25.2009264+00:00 | 114.0ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,796,000 | 2026-07-19T03:39:25.2067228+00:00 | 101.5ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,812,000 | 2026-07-19T03:39:25.2179875+00:00 | 101.4ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,819,000 | 2026-07-19T03:39:25.2239924+00:00 | 106.7ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,820,000 | 2026-07-19T03:39:25.2254566+00:00 | 100.3ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,830,000 | 2026-07-19T03:39:25.2351059+00:00 | 110.3ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,839,000 | 2026-07-19T03:39:25.2442653+00:00 | 120.2ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,840,000 | 2026-07-19T03:39:25.2452081+00:00 | 105.9ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,850,000 | 2026-07-19T03:39:25.257607+00:00 | 112.0ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,851,000 | 2026-07-19T03:39:25.2586939+00:00 | 129.3ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,852,000 | 2026-07-19T03:39:25.259618+00:00 | 109.9ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,853,000 | 2026-07-19T03:39:25.2605526+00:00 | 113.8ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,862,000 | 2026-07-19T03:39:25.2733374+00:00 | 104.4ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,864,000 | 2026-07-19T03:39:25.2761536+00:00 | 110.9ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,869,000 | 2026-07-19T03:39:25.2825947+00:00 | 104.9ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,874,000 | 2026-07-19T03:39:25.2871532+00:00 | 108.1ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 177,881,000 | 2026-07-19T03:39:25.3014071+00:00 | 109.4ms | GC pause | - | - | 159.1s / 1,060,799 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 219,358,000 | 2026-07-19T03:40:00.1098757+00:00 | 115.4ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,361,000 | 2026-07-19T03:40:00.1115744+00:00 | 114.0ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,362,000 | 2026-07-19T03:40:00.1123417+00:00 | 103.9ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,364,000 | 2026-07-19T03:40:00.1136239+00:00 | 121.4ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,370,000 | 2026-07-19T03:40:00.1177857+00:00 | 122.3ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,373,000 | 2026-07-19T03:40:00.1206456+00:00 | 119.5ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,378,000 | 2026-07-19T03:40:00.1314264+00:00 | 117.2ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,379,000 | 2026-07-19T03:40:00.1337034+00:00 | 107.9ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,381,000 | 2026-07-19T03:40:00.1361077+00:00 | 117.7ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,385,000 | 2026-07-19T03:40:00.1423665+00:00 | 103.8ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,387,000 | 2026-07-19T03:40:00.1470402+00:00 | 109.2ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,388,000 | 2026-07-19T03:40:00.1494893+00:00 | 106.8ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,391,000 | 2026-07-19T03:40:00.1546482+00:00 | 107.0ms | GC pause | - | - | 194.1s / 1,085,463 msg/s | Gen2 +0 / pause +90.9ms |
| Confluent | 219,957,000 | 2026-07-19T03:40:00.6737212+00:00 | 106.8ms | GC pause | - | - | 195.1s / 772,596 msg/s | Gen2 +0 / pause +123.6ms |
| Confluent | 219,958,000 | 2026-07-19T03:40:00.6743153+00:00 | 106.3ms | GC pause | - | - | 195.1s / 772,596 msg/s | Gen2 +0 / pause +123.6ms |
| Confluent | 219,967,000 | 2026-07-19T03:40:00.686996+00:00 | 108.9ms | GC pause | - | - | 195.1s / 772,596 msg/s | Gen2 +0 / pause +123.6ms |
| Confluent | 266,109,000 | 2026-07-19T03:40:49.1063489+00:00 | 117.0ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,110,000 | 2026-07-19T03:40:49.1068873+00:00 | 100.2ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,115,000 | 2026-07-19T03:40:49.1098166+00:00 | 120.0ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,125,000 | 2026-07-19T03:40:49.1156214+00:00 | 116.3ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,130,000 | 2026-07-19T03:40:49.1183935+00:00 | 106.3ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,150,000 | 2026-07-19T03:40:49.1313468+00:00 | 110.2ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,153,000 | 2026-07-19T03:40:49.1338009+00:00 | 107.9ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,165,000 | 2026-07-19T03:40:49.142504+00:00 | 123.1ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,175,000 | 2026-07-19T03:40:49.1534075+00:00 | 122.7ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,176,000 | 2026-07-19T03:40:49.1540938+00:00 | 122.0ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,200,000 | 2026-07-19T03:40:49.1727491+00:00 | 108.2ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,219,000 | 2026-07-19T03:40:49.1875663+00:00 | 119.1ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,223,000 | 2026-07-19T03:40:49.1907205+00:00 | 121.7ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,230,000 | 2026-07-19T03:40:49.19476+00:00 | 129.0ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,239,000 | 2026-07-19T03:40:49.2082229+00:00 | 116.7ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,243,000 | 2026-07-19T03:40:49.2110272+00:00 | 119.8ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,256,000 | 2026-07-19T03:40:49.2214512+00:00 | 114.2ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 266,265,000 | 2026-07-19T03:40:49.2345712+00:00 | 111.1ms | GC pause | - | - | 243.2s / 1,007,785 msg/s | Gen2 +0 / pause +90.2ms |
| Confluent | 311,872,000 | 2026-07-19T03:41:33.8251017+00:00 | 101.0ms | GC pause | - | - | 288.2s / 1,153,943 msg/s | Gen2 +0 / pause +54.5ms |
| Confluent | 313,378,000 | 2026-07-19T03:41:35.0849887+00:00 | 110.1ms | GC pause | - | - | 289.2s / 1,237,796 msg/s | Gen2 +0 / pause +67.2ms |
| Confluent | 313,471,000 | 2026-07-19T03:41:35.1605383+00:00 | 101.6ms | GC pause | - | - | 289.2s / 1,237,796 msg/s | Gen2 +0 / pause +67.2ms |
| Confluent | 314,141,000 | 2026-07-19T03:41:35.7084656+00:00 | 100.1ms | GC pause | - | - | 290.2s / 1,195,918 msg/s | Gen2 +0 / pause +75.2ms |
| Confluent | 320,697,000 | 2026-07-19T03:41:41.2486546+00:00 | 102.2ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,700,000 | 2026-07-19T03:41:41.2502022+00:00 | 100.3ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,701,000 | 2026-07-19T03:41:41.2509057+00:00 | 100.7ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,707,000 | 2026-07-19T03:41:41.2544126+00:00 | 102.2ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,708,000 | 2026-07-19T03:41:41.2553468+00:00 | 101.3ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,713,000 | 2026-07-19T03:41:41.2588185+00:00 | 100.5ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,717,000 | 2026-07-19T03:41:41.2617253+00:00 | 104.9ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,738,000 | 2026-07-19T03:41:41.283076+00:00 | 100.4ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,741,000 | 2026-07-19T03:41:41.2856629+00:00 | 104.8ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,750,000 | 2026-07-19T03:41:41.2929841+00:00 | 101.6ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,751,000 | 2026-07-19T03:41:41.2936038+00:00 | 108.7ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,787,000 | 2026-07-19T03:41:41.3274003+00:00 | 106.7ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,788,000 | 2026-07-19T03:41:41.3278817+00:00 | 106.2ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,807,000 | 2026-07-19T03:41:41.3402355+00:00 | 109.2ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,813,000 | 2026-07-19T03:41:41.3438615+00:00 | 108.4ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,818,000 | 2026-07-19T03:41:41.3520861+00:00 | 108.2ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,821,000 | 2026-07-19T03:41:41.3545381+00:00 | 111.4ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,827,000 | 2026-07-19T03:41:41.3579758+00:00 | 115.1ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,828,000 | 2026-07-19T03:41:41.3585507+00:00 | 114.5ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,831,000 | 2026-07-19T03:41:41.3603618+00:00 | 112.7ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,832,000 | 2026-07-19T03:41:41.3610941+00:00 | 104.3ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,835,000 | 2026-07-19T03:41:41.3627412+00:00 | 103.2ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,839,000 | 2026-07-19T03:41:41.365436+00:00 | 106.0ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,841,000 | 2026-07-19T03:41:41.3664915+00:00 | 117.3ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,842,000 | 2026-07-19T03:41:41.367198+00:00 | 106.7ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,850,000 | 2026-07-19T03:41:41.3717296+00:00 | 114.0ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,852,000 | 2026-07-19T03:41:41.3745565+00:00 | 104.8ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,861,000 | 2026-07-19T03:41:41.3815631+00:00 | 125.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,866,000 | 2026-07-19T03:41:41.384883+00:00 | 109.0ms | GC pause | - | - | 295.2s / 1,194,264 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 320,877,000 | 2026-07-19T03:41:41.3967215+00:00 | 124.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,883,000 | 2026-07-19T03:41:41.4050649+00:00 | 118.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,884,000 | 2026-07-19T03:41:41.4067033+00:00 | 111.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,885,000 | 2026-07-19T03:41:41.4080165+00:00 | 112.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,887,000 | 2026-07-19T03:41:41.4110697+00:00 | 121.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,889,000 | 2026-07-19T03:41:41.4137493+00:00 | 106.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,891,000 | 2026-07-19T03:41:41.4167653+00:00 | 121.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,892,000 | 2026-07-19T03:41:41.4180477+00:00 | 105.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,896,000 | 2026-07-19T03:41:41.4237211+00:00 | 103.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,897,000 | 2026-07-19T03:41:41.425327+00:00 | 119.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,898,000 | 2026-07-19T03:41:41.4264752+00:00 | 118.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,900,000 | 2026-07-19T03:41:41.429393+00:00 | 106.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,907,000 | 2026-07-19T03:41:41.4343147+00:00 | 121.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,912,000 | 2026-07-19T03:41:41.4373841+00:00 | 109.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,913,000 | 2026-07-19T03:41:41.437879+00:00 | 114.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,917,000 | 2026-07-19T03:41:41.4402612+00:00 | 120.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,918,000 | 2026-07-19T03:41:41.4409224+00:00 | 127.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,924,000 | 2026-07-19T03:41:41.4445249+00:00 | 108.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,926,000 | 2026-07-19T03:41:41.4455906+00:00 | 115.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,931,000 | 2026-07-19T03:41:41.4483184+00:00 | 138.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,936,000 | 2026-07-19T03:41:41.4518817+00:00 | 116.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,938,000 | 2026-07-19T03:41:41.4529232+00:00 | 135.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,942,000 | 2026-07-19T03:41:41.4553364+00:00 | 130.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,943,000 | 2026-07-19T03:41:41.4557959+00:00 | 131.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,945,000 | 2026-07-19T03:41:41.4570244+00:00 | 129.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,955,000 | 2026-07-19T03:41:41.4626959+00:00 | 125.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,958,000 | 2026-07-19T03:41:41.4640742+00:00 | 129.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,964,000 | 2026-07-19T03:41:41.4676643+00:00 | 122.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,966,000 | 2026-07-19T03:41:41.4688177+00:00 | 121.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,968,000 | 2026-07-19T03:41:41.4699329+00:00 | 127.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,970,000 | 2026-07-19T03:41:41.4713048+00:00 | 122.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,974,000 | 2026-07-19T03:41:41.4734929+00:00 | 120.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,975,000 | 2026-07-19T03:41:41.4741202+00:00 | 119.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,989,000 | 2026-07-19T03:41:41.4833081+00:00 | 117.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,993,000 | 2026-07-19T03:41:41.4857902+00:00 | 121.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 320,997,000 | 2026-07-19T03:41:41.4879881+00:00 | 132.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 321,007,000 | 2026-07-19T03:41:41.4941134+00:00 | 134.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +149.0ms |
| Confluent | 321,013,000 | 2026-07-19T03:41:41.499084+00:00 | 129.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,018,000 | 2026-07-19T03:41:41.5020105+00:00 | 135.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,022,000 | 2026-07-19T03:41:41.5049188+00:00 | 123.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,025,000 | 2026-07-19T03:41:41.5080794+00:00 | 120.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,037,000 | 2026-07-19T03:41:41.5154314+00:00 | 133.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,038,000 | 2026-07-19T03:41:41.5159326+00:00 | 133.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,055,000 | 2026-07-19T03:41:41.5292698+00:00 | 119.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,057,000 | 2026-07-19T03:41:41.5306732+00:00 | 137.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,059,000 | 2026-07-19T03:41:41.5320202+00:00 | 123.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,061,000 | 2026-07-19T03:41:41.5334805+00:00 | 134.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,073,000 | 2026-07-19T03:41:41.5415383+00:00 | 135.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,089,000 | 2026-07-19T03:41:41.5504204+00:00 | 127.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,090,000 | 2026-07-19T03:41:41.5508926+00:00 | 136.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,099,000 | 2026-07-19T03:41:41.5568708+00:00 | 130.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,101,000 | 2026-07-19T03:41:41.5582303+00:00 | 139.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,103,000 | 2026-07-19T03:41:41.5593413+00:00 | 132.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,108,000 | 2026-07-19T03:41:41.5625708+00:00 | 142.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,110,000 | 2026-07-19T03:41:41.5637778+00:00 | 134.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,119,000 | 2026-07-19T03:41:41.5693279+00:00 | 134.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,124,000 | 2026-07-19T03:41:41.5722522+00:00 | 129.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,126,000 | 2026-07-19T03:41:41.5733144+00:00 | 136.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,127,000 | 2026-07-19T03:41:41.5737879+00:00 | 144.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,136,000 | 2026-07-19T03:41:41.5795258+00:00 | 137.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,139,000 | 2026-07-19T03:41:41.5823651+00:00 | 134.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,140,000 | 2026-07-19T03:41:41.5829804+00:00 | 138.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,144,000 | 2026-07-19T03:41:41.5891838+00:00 | 122.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,145,000 | 2026-07-19T03:41:41.591075+00:00 | 131.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,148,000 | 2026-07-19T03:41:41.5930099+00:00 | 166.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,152,000 | 2026-07-19T03:41:41.5960571+00:00 | 113.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,158,000 | 2026-07-19T03:41:41.6005126+00:00 | 166.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,164,000 | 2026-07-19T03:41:41.6048788+00:00 | 134.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,173,000 | 2026-07-19T03:41:41.6115423+00:00 | 158.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,183,000 | 2026-07-19T03:41:41.6255021+00:00 | 149.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,184,000 | 2026-07-19T03:41:41.626048+00:00 | 149.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,192,000 | 2026-07-19T03:41:41.6326285+00:00 | 129.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,198,000 | 2026-07-19T03:41:41.6365693+00:00 | 166.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,216,000 | 2026-07-19T03:41:41.6503977+00:00 | 152.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,226,000 | 2026-07-19T03:41:41.6598782+00:00 | 149.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,227,000 | 2026-07-19T03:41:41.6607824+00:00 | 162.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,229,000 | 2026-07-19T03:41:41.6623619+00:00 | 159.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,234,000 | 2026-07-19T03:41:41.6666117+00:00 | 148.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,240,000 | 2026-07-19T03:41:41.6707606+00:00 | 151.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,242,000 | 2026-07-19T03:41:41.678443+00:00 | 133.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,243,000 | 2026-07-19T03:41:41.6791422+00:00 | 150.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,245,000 | 2026-07-19T03:41:41.6805505+00:00 | 153.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,248,000 | 2026-07-19T03:41:41.6826827+00:00 | 157.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,250,000 | 2026-07-19T03:41:41.6838281+00:00 | 150.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,262,000 | 2026-07-19T03:41:41.6936522+00:00 | 134.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,265,000 | 2026-07-19T03:41:41.6955126+00:00 | 154.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,273,000 | 2026-07-19T03:41:41.7001768+00:00 | 158.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,282,000 | 2026-07-19T03:41:41.7052557+00:00 | 137.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,283,000 | 2026-07-19T03:41:41.7057442+00:00 | 159.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,288,000 | 2026-07-19T03:41:41.7083904+00:00 | 166.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,297,000 | 2026-07-19T03:41:41.7135044+00:00 | 165.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,298,000 | 2026-07-19T03:41:41.7140945+00:00 | 170.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,300,000 | 2026-07-19T03:41:41.7150398+00:00 | 167.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,303,000 | 2026-07-19T03:41:41.7170419+00:00 | 167.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,304,000 | 2026-07-19T03:41:41.7174918+00:00 | 148.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,305,000 | 2026-07-19T03:41:41.7179483+00:00 | 164.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,311,000 | 2026-07-19T03:41:41.7208682+00:00 | 178.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,315,000 | 2026-07-19T03:41:41.7229081+00:00 | 165.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,319,000 | 2026-07-19T03:41:41.7249046+00:00 | 167.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,321,000 | 2026-07-19T03:41:41.7258194+00:00 | 184.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,326,000 | 2026-07-19T03:41:41.7284757+00:00 | 166.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,329,000 | 2026-07-19T03:41:41.7316132+00:00 | 163.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,330,000 | 2026-07-19T03:41:41.7321291+00:00 | 169.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,338,000 | 2026-07-19T03:41:41.7365682+00:00 | 185.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,341,000 | 2026-07-19T03:41:41.7389662+00:00 | 182.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,342,000 | 2026-07-19T03:41:41.7394584+00:00 | 157.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,344,000 | 2026-07-19T03:41:41.7403493+00:00 | 157.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,347,000 | 2026-07-19T03:41:41.7418214+00:00 | 183.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,348,000 | 2026-07-19T03:41:41.7422796+00:00 | 182.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,352,000 | 2026-07-19T03:41:41.7446198+00:00 | 161.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,354,000 | 2026-07-19T03:41:41.7458822+00:00 | 160.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,356,000 | 2026-07-19T03:41:41.7470388+00:00 | 172.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,364,000 | 2026-07-19T03:41:41.7521668+00:00 | 162.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,375,000 | 2026-07-19T03:41:41.7603637+00:00 | 170.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,377,000 | 2026-07-19T03:41:41.7622978+00:00 | 184.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,384,000 | 2026-07-19T03:41:41.7692215+00:00 | 161.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,385,000 | 2026-07-19T03:41:41.7707642+00:00 | 166.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,389,000 | 2026-07-19T03:41:41.7742448+00:00 | 162.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,392,000 | 2026-07-19T03:41:41.7786213+00:00 | 156.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,395,000 | 2026-07-19T03:41:41.7835647+00:00 | 158.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,399,000 | 2026-07-19T03:41:41.7887313+00:00 | 158.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,400,000 | 2026-07-19T03:41:41.7903035+00:00 | 165.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,407,000 | 2026-07-19T03:41:41.796291+00:00 | 179.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,409,000 | 2026-07-19T03:41:41.8001032+00:00 | 155.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,410,000 | 2026-07-19T03:41:41.8041699+00:00 | 161.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,416,000 | 2026-07-19T03:41:41.8091401+00:00 | 152.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,420,000 | 2026-07-19T03:41:41.8128142+00:00 | 166.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,421,000 | 2026-07-19T03:41:41.8134215+00:00 | 168.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,428,000 | 2026-07-19T03:41:41.8201091+00:00 | 166.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,433,000 | 2026-07-19T03:41:41.8242603+00:00 | 161.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,435,000 | 2026-07-19T03:41:41.8258574+00:00 | 149.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,436,000 | 2026-07-19T03:41:41.8268228+00:00 | 148.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,438,000 | 2026-07-19T03:41:41.8280026+00:00 | 168.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,449,000 | 2026-07-19T03:41:41.8389221+00:00 | 149.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,455,000 | 2026-07-19T03:41:41.8435541+00:00 | 152.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,456,000 | 2026-07-19T03:41:41.8444356+00:00 | 152.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,466,000 | 2026-07-19T03:41:41.8517895+00:00 | 148.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,471,000 | 2026-07-19T03:41:41.8561549+00:00 | 166.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,473,000 | 2026-07-19T03:41:41.8575927+00:00 | 156.4ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,475,000 | 2026-07-19T03:41:41.8588213+00:00 | 150.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,482,000 | 2026-07-19T03:41:41.863829+00:00 | 145.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,485,000 | 2026-07-19T03:41:41.8656754+00:00 | 149.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,493,000 | 2026-07-19T03:41:41.8725455+00:00 | 157.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,494,000 | 2026-07-19T03:41:41.8760949+00:00 | 134.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,498,000 | 2026-07-19T03:41:41.8797256+00:00 | 160.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,500,000 | 2026-07-19T03:41:41.8809096+00:00 | 151.2ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,503,000 | 2026-07-19T03:41:41.8829976+00:00 | 153.7ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,505,000 | 2026-07-19T03:41:41.8850511+00:00 | 144.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,515,000 | 2026-07-19T03:41:41.8991752+00:00 | 141.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,519,000 | 2026-07-19T03:41:41.9061703+00:00 | 134.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,527,000 | 2026-07-19T03:41:41.9237067+00:00 | 140.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,529,000 | 2026-07-19T03:41:41.9267236+00:00 | 121.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,539,000 | 2026-07-19T03:41:41.9420228+00:00 | 114.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,542,000 | 2026-07-19T03:41:41.9457569+00:00 | 107.9ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,548,000 | 2026-07-19T03:41:41.9521184+00:00 | 131.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,550,000 | 2026-07-19T03:41:41.9546956+00:00 | 125.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,556,000 | 2026-07-19T03:41:41.9604151+00:00 | 119.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,569,000 | 2026-07-19T03:41:41.9788897+00:00 | 103.8ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,571,000 | 2026-07-19T03:41:41.9809761+00:00 | 120.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,577,000 | 2026-07-19T03:41:41.9863818+00:00 | 116.6ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,587,000 | 2026-07-19T03:41:41.9943614+00:00 | 115.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,597,000 | 2026-07-19T03:41:42.0035519+00:00 | 114.0ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,617,000 | 2026-07-19T03:41:42.0199399+00:00 | 109.1ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,618,000 | 2026-07-19T03:41:42.0205679+00:00 | 108.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,628,000 | 2026-07-19T03:41:42.0285936+00:00 | 109.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,667,000 | 2026-07-19T03:41:42.0658111+00:00 | 104.5ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 321,681,000 | 2026-07-19T03:41:42.0761325+00:00 | 104.3ms | GC pause | - | - | 296.2s / 1,159,017 msg/s | Gen2 +0 / pause +61.1ms |
| Dekaf (3conn) | 371,517,000 | 2026-07-19T04:10:27.4063418+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,518,000 | 2026-07-19T04:10:27.4068485+00:00 | 222.9ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,525,000 | 2026-07-19T04:10:27.4101208+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,527,000 | 2026-07-19T04:10:27.4111142+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,528,000 | 2026-07-19T04:10:27.4113786+00:00 | 218.3ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,529,000 | 2026-07-19T04:10:27.4118923+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,530,000 | 2026-07-19T04:10:27.4121439+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,531,000 | 2026-07-19T04:10:27.4125676+00:00 | 218.7ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,532,000 | 2026-07-19T04:10:27.4138809+00:00 | 218.0ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,533,000 | 2026-07-19T04:10:27.4153233+00:00 | 215.4ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,534,000 | 2026-07-19T04:10:27.4155534+00:00 | 217.1ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,535,000 | 2026-07-19T04:10:27.4159157+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,536,000 | 2026-07-19T04:10:27.4161475+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 371,537,000 | 2026-07-19T04:10:27.4167822+00:00 | 215.0ms | broker/backlog (no scale or GC event) | - | - | 221.1s / 1,547,231 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 388,178,000 | 2026-07-19T04:10:36.4969017+00:00 | 216.3ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,185,000 | 2026-07-19T04:10:36.5007178+00:00 | 215.9ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,187,000 | 2026-07-19T04:10:36.5013454+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,188,000 | 2026-07-19T04:10:36.5015853+00:00 | 215.0ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,189,000 | 2026-07-19T04:10:36.502903+00:00 | 211.9ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,190,000 | 2026-07-19T04:10:36.5033081+00:00 | 213.3ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,191,000 | 2026-07-19T04:10:36.5039584+00:00 | 212.7ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,192,000 | 2026-07-19T04:10:36.5043782+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,193,000 | 2026-07-19T04:10:36.504788+00:00 | 211.8ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,194,000 | 2026-07-19T04:10:36.5051755+00:00 | 211.4ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,195,000 | 2026-07-19T04:10:36.5054797+00:00 | 211.1ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,196,000 | 2026-07-19T04:10:36.5060043+00:00 | 213.2ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 388,197,000 | 2026-07-19T04:10:36.5068486+00:00 | 210.5ms | broker/backlog (no scale or GC event) | - | - | 230.1s / 1,246,007 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 419,437,000 | 2026-07-19T04:10:58.9317039+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,438,000 | 2026-07-19T04:10:58.9320573+00:00 | 222.8ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,439,000 | 2026-07-19T04:10:58.9325273+00:00 | 222.6ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,440,000 | 2026-07-19T04:10:58.932727+00:00 | 224.3ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,441,000 | 2026-07-19T04:10:58.9329266+00:00 | 224.1ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,442,000 | 2026-07-19T04:10:58.9331256+00:00 | 223.9ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,443,000 | 2026-07-19T04:10:58.933907+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,444,000 | 2026-07-19T04:10:58.934278+00:00 | 221.6ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,445,000 | 2026-07-19T04:10:58.9347331+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,446,000 | 2026-07-19T04:10:58.9349271+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,447,000 | 2026-07-19T04:10:58.9351231+00:00 | 221.9ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,448,000 | 2026-07-19T04:10:58.9353277+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,449,000 | 2026-07-19T04:10:58.935652+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,450,000 | 2026-07-19T04:10:58.9362463+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,451,000 | 2026-07-19T04:10:58.936727+00:00 | 222.6ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 419,452,000 | 2026-07-19T04:10:58.9369455+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 252.1s / 1,239,013 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 557,705,000 | 2026-07-19T04:12:18.0325037+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,707,000 | 2026-07-19T04:12:18.0351886+00:00 | 215.9ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,708,000 | 2026-07-19T04:12:18.0354176+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,715,000 | 2026-07-19T04:12:18.0378777+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,717,000 | 2026-07-19T04:12:18.0386658+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,718,000 | 2026-07-19T04:12:18.0392172+00:00 | 213.6ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,719,000 | 2026-07-19T04:12:18.0395019+00:00 | 211.6ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,720,000 | 2026-07-19T04:12:18.0400688+00:00 | 212.1ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,721,000 | 2026-07-19T04:12:18.0403729+00:00 | 210.9ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,722,000 | 2026-07-19T04:12:18.0406602+00:00 | 210.6ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,723,000 | 2026-07-19T04:12:18.0412231+00:00 | 211.6ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,724,000 | 2026-07-19T04:12:18.0428294+00:00 | 211.5ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,725,000 | 2026-07-19T04:12:18.0430794+00:00 | 211.3ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 557,726,000 | 2026-07-19T04:12:18.0433305+00:00 | 211.0ms | broker/backlog (no scale or GC event) | - | - | 331.2s / 1,368,624 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 687,917,000 | 2026-07-19T04:13:37.6876918+00:00 | 218.5ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,918,000 | 2026-07-19T04:13:37.6880085+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,920,000 | 2026-07-19T04:13:37.690535+00:00 | 222.2ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,924,000 | 2026-07-19T04:13:37.6929569+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,925,000 | 2026-07-19T04:13:37.6932216+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,926,000 | 2026-07-19T04:13:37.6949781+00:00 | 217.2ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,927,000 | 2026-07-19T04:13:37.695304+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,928,000 | 2026-07-19T04:13:37.6956138+00:00 | 214.6ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,929,000 | 2026-07-19T04:13:37.695923+00:00 | 212.3ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,930,000 | 2026-07-19T04:13:37.6965874+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,931,000 | 2026-07-19T04:13:37.6968899+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,932,000 | 2026-07-19T04:13:37.6975909+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 687,933,000 | 2026-07-19T04:13:37.6980502+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 411.2s / 1,101,767 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 785,533,000 | 2026-07-19T04:14:38.1554546+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,534,000 | 2026-07-19T04:14:38.1571242+00:00 | 224.9ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,535,000 | 2026-07-19T04:14:38.1574565+00:00 | 224.5ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,536,000 | 2026-07-19T04:14:38.1581687+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,537,000 | 2026-07-19T04:14:38.1591544+00:00 | 223.4ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,538,000 | 2026-07-19T04:14:38.160549+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,539,000 | 2026-07-19T04:14:38.1608556+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,540,000 | 2026-07-19T04:14:38.1615963+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,541,000 | 2026-07-19T04:14:38.1619067+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,542,000 | 2026-07-19T04:14:38.1622231+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,543,000 | 2026-07-19T04:14:38.162529+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,544,000 | 2026-07-19T04:14:38.1632642+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 785,545,000 | 2026-07-19T04:14:38.1635594+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 471.3s / 1,082,859 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,330,000 | 2026-07-19T04:18:16.220538+00:00 | 221.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,333,000 | 2026-07-19T04:18:16.2224886+00:00 | 218.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,339,000 | 2026-07-19T04:18:16.224665+00:00 | 221.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,340,000 | 2026-07-19T04:18:16.2250186+00:00 | 219.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,343,000 | 2026-07-19T04:18:16.2281106+00:00 | 217.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,348,000 | 2026-07-19T04:18:16.2298529+00:00 | 213.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,349,000 | 2026-07-19T04:18:16.2302496+00:00 | 215.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,350,000 | 2026-07-19T04:18:16.2306339+00:00 | 216.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,351,000 | 2026-07-19T04:18:16.2308877+00:00 | 214.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,352,000 | 2026-07-19T04:18:16.2322108+00:00 | 213.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,353,000 | 2026-07-19T04:18:16.2324677+00:00 | 213.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,354,000 | 2026-07-19T04:18:16.2330702+00:00 | 212.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,355,000 | 2026-07-19T04:18:16.2343776+00:00 | 211.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,356,000 | 2026-07-19T04:18:16.2346125+00:00 | 211.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,357,000 | 2026-07-19T04:18:16.2348445+00:00 | 210.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,358,000 | 2026-07-19T04:18:16.235219+00:00 | 210.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,359,000 | 2026-07-19T04:18:16.2354462+00:00 | 210.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,360,000 | 2026-07-19T04:18:16.2360716+00:00 | 213.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,361,000 | 2026-07-19T04:18:16.2364627+00:00 | 210.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,154,362,000 | 2026-07-19T04:18:16.2368463+00:00 | 209.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 689.4s / 1,544,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,360,000 | 2026-07-19T04:18:46.1720494+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,363,000 | 2026-07-19T04:18:46.1740519+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,369,000 | 2026-07-19T04:18:46.1764878+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,370,000 | 2026-07-19T04:18:46.1769084+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,371,000 | 2026-07-19T04:18:46.1774441+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,372,000 | 2026-07-19T04:18:46.1778522+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,373,000 | 2026-07-19T04:18:46.1782803+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,374,000 | 2026-07-19T04:18:46.1785808+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,375,000 | 2026-07-19T04:18:46.1788871+00:00 | 217.2ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,376,000 | 2026-07-19T04:18:46.1804117+00:00 | 217.1ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,377,000 | 2026-07-19T04:18:46.1808452+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,378,000 | 2026-07-19T04:18:46.1823468+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,379,000 | 2026-07-19T04:18:46.1827804+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,380,000 | 2026-07-19T04:18:46.1830543+00:00 | 216.0ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,381,000 | 2026-07-19T04:18:46.183347+00:00 | 214.2ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,382,000 | 2026-07-19T04:18:46.1838943+00:00 | 213.7ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,383,000 | 2026-07-19T04:18:46.1843012+00:00 | 213.6ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,208,384,000 | 2026-07-19T04:18:46.1848342+00:00 | 213.1ms | broker/backlog (no scale or GC event) | - | - | 719.4s / 1,324,006 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,559,000 | 2026-07-19T04:18:47.832393+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,560,000 | 2026-07-19T04:18:47.8331264+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,563,000 | 2026-07-19T04:18:47.8340978+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,565,000 | 2026-07-19T04:18:47.8353101+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,567,000 | 2026-07-19T04:18:47.836277+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,568,000 | 2026-07-19T04:18:47.8366262+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,569,000 | 2026-07-19T04:18:47.8369586+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,570,000 | 2026-07-19T04:18:47.8386641+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,571,000 | 2026-07-19T04:18:47.8389955+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,572,000 | 2026-07-19T04:18:47.8394792+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,573,000 | 2026-07-19T04:18:47.8399488+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,574,000 | 2026-07-19T04:18:47.8402935+00:00 | 214.6ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,575,000 | 2026-07-19T04:18:47.8406257+00:00 | 212.1ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,576,000 | 2026-07-19T04:18:47.8415206+00:00 | 213.3ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,577,000 | 2026-07-19T04:18:47.8418582+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,578,000 | 2026-07-19T04:18:47.8423384+00:00 | 214.5ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,579,000 | 2026-07-19T04:18:47.8428106+00:00 | 213.9ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,210,580,000 | 2026-07-19T04:18:47.8431492+00:00 | 213.6ms | broker/backlog (no scale or GC event) | - | - | 721.4s / 1,104,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,122,000 | 2026-07-19T04:20:02.4478073+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,126,000 | 2026-07-19T04:20:02.450131+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,127,000 | 2026-07-19T04:20:02.4506515+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,130,000 | 2026-07-19T04:20:02.451462+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,131,000 | 2026-07-19T04:20:02.4527824+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,132,000 | 2026-07-19T04:20:02.4542464+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,133,000 | 2026-07-19T04:20:02.454617+00:00 | 212.1ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,134,000 | 2026-07-19T04:20:02.4548434+00:00 | 213.9ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,135,000 | 2026-07-19T04:20:02.4550684+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,136,000 | 2026-07-19T04:20:02.455306+00:00 | 213.4ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,137,000 | 2026-07-19T04:20:02.4565974+00:00 | 212.1ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,138,000 | 2026-07-19T04:20:02.4568059+00:00 | 211.8ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,139,000 | 2026-07-19T04:20:02.4574212+00:00 | 210.1ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,140,000 | 2026-07-19T04:20:02.4577662+00:00 | 213.9ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,141,000 | 2026-07-19T04:20:02.4579871+00:00 | 213.7ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,142,000 | 2026-07-19T04:20:02.4582062+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,143,000 | 2026-07-19T04:20:02.4585467+00:00 | 213.1ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,144,000 | 2026-07-19T04:20:02.4588878+00:00 | 209.8ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,346,145,000 | 2026-07-19T04:20:02.4596414+00:00 | 212.0ms | broker/backlog (no scale or GC event) | - | - | 795.4s / 1,522,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,978,000 | 2026-07-19T04:20:06.277074+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,979,000 | 2026-07-19T04:20:06.27857+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,980,000 | 2026-07-19T04:20:06.2799559+00:00 | 223.5ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,981,000 | 2026-07-19T04:20:06.2804101+00:00 | 224.9ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,982,000 | 2026-07-19T04:20:06.2807048+00:00 | 224.6ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,983,000 | 2026-07-19T04:20:06.2809995+00:00 | 218.3ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,984,000 | 2026-07-19T04:20:06.2814385+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,985,000 | 2026-07-19T04:20:06.2818517+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,986,000 | 2026-07-19T04:20:06.2825425+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,987,000 | 2026-07-19T04:20:06.2829415+00:00 | 222.3ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,988,000 | 2026-07-19T04:20:06.2832243+00:00 | 219.5ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,989,000 | 2026-07-19T04:20:06.283504+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,990,000 | 2026-07-19T04:20:06.2837767+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,991,000 | 2026-07-19T04:20:06.2841849+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,992,000 | 2026-07-19T04:20:06.2848407+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,352,993,000 | 2026-07-19T04:20:06.2853708+00:00 | 219.9ms | broker/backlog (no scale or GC event) | - | - | 799.4s / 1,302,071 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,565,000 | 2026-07-19T04:20:23.9550197+00:00 | 222.2ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,567,000 | 2026-07-19T04:20:23.9556382+00:00 | 225.3ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,568,000 | 2026-07-19T04:20:23.9566202+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,569,000 | 2026-07-19T04:20:23.9569248+00:00 | 221.9ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,570,000 | 2026-07-19T04:20:23.9572164+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,571,000 | 2026-07-19T04:20:23.9575015+00:00 | 222.6ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,572,000 | 2026-07-19T04:20:23.9577648+00:00 | 222.3ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,573,000 | 2026-07-19T04:20:23.9591002+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,574,000 | 2026-07-19T04:20:23.9606085+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,575,000 | 2026-07-19T04:20:23.9619471+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,576,000 | 2026-07-19T04:20:23.9622289+00:00 | 218.8ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,577,000 | 2026-07-19T04:20:23.9625026+00:00 | 218.5ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,578,000 | 2026-07-19T04:20:23.9627646+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,579,000 | 2026-07-19T04:20:23.9634397+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,580,000 | 2026-07-19T04:20:23.9638252+00:00 | 219.1ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,581,000 | 2026-07-19T04:20:23.964356+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,582,000 | 2026-07-19T04:20:23.9645929+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,583,000 | 2026-07-19T04:20:23.9648344+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,382,584,000 | 2026-07-19T04:20:23.9650672+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,488,496 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,415,715,000 | 2026-07-19T04:20:42.7974561+00:00 | 225.0ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,716,000 | 2026-07-19T04:20:42.7978039+00:00 | 224.6ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,717,000 | 2026-07-19T04:20:42.7981225+00:00 | 224.3ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,718,000 | 2026-07-19T04:20:42.7985946+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,719,000 | 2026-07-19T04:20:42.798901+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,720,000 | 2026-07-19T04:20:42.8006498+00:00 | 221.9ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,721,000 | 2026-07-19T04:20:42.8011696+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,722,000 | 2026-07-19T04:20:42.8014889+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,723,000 | 2026-07-19T04:20:42.8018078+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,724,000 | 2026-07-19T04:20:42.8023216+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,725,000 | 2026-07-19T04:20:42.8026363+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,726,000 | 2026-07-19T04:20:42.8034491+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,727,000 | 2026-07-19T04:20:42.8039653+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,728,000 | 2026-07-19T04:20:42.8044915+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,729,000 | 2026-07-19T04:20:42.8048195+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,415,730,000 | 2026-07-19T04:20:42.8053321+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 836.5s / 1,195,342 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,485,625,000 | 2026-07-19T04:21:25.7291755+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,627,000 | 2026-07-19T04:21:25.729703+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,628,000 | 2026-07-19T04:21:25.7301655+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,635,000 | 2026-07-19T04:21:25.7323952+00:00 | 216.6ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,637,000 | 2026-07-19T04:21:25.7332134+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,638,000 | 2026-07-19T04:21:25.7335652+00:00 | 215.4ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,645,000 | 2026-07-19T04:21:25.736194+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,647,000 | 2026-07-19T04:21:25.7374438+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,648,000 | 2026-07-19T04:21:25.7378847+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,655,000 | 2026-07-19T04:21:25.7791424+00:00 | 176.1ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,656,000 | 2026-07-19T04:21:25.7793871+00:00 | 169.0ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,657,000 | 2026-07-19T04:21:25.8153373+00:00 | 141.9ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,658,000 | 2026-07-19T04:21:25.8162747+00:00 | 139.0ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,659,000 | 2026-07-19T04:21:25.8164878+00:00 | 131.8ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,660,000 | 2026-07-19T04:21:25.8170396+00:00 | 131.6ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,485,661,000 | 2026-07-19T04:21:25.8186487+00:00 | 134.6ms | broker/backlog (no scale or GC event) | - | - | 878.5s / 1,467,910 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,388,000 | 2026-07-19T04:21:29.4129959+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,390,000 | 2026-07-19T04:21:29.4168913+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,391,000 | 2026-07-19T04:21:29.4193801+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,392,000 | 2026-07-19T04:21:29.4196788+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,393,000 | 2026-07-19T04:21:29.4199707+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,394,000 | 2026-07-19T04:21:29.4202701+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,395,000 | 2026-07-19T04:21:29.4217577+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,396,000 | 2026-07-19T04:21:29.4223359+00:00 | 214.7ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,397,000 | 2026-07-19T04:21:29.4228938+00:00 | 214.1ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,398,000 | 2026-07-19T04:21:29.4232004+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,399,000 | 2026-07-19T04:21:29.4234954+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,400,000 | 2026-07-19T04:21:29.423778+00:00 | 213.9ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,401,000 | 2026-07-19T04:21:29.4243421+00:00 | 212.7ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,402,000 | 2026-07-19T04:21:29.4249002+00:00 | 214.5ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,403,000 | 2026-07-19T04:21:29.4253416+00:00 | 213.2ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,491,404,000 | 2026-07-19T04:21:29.425771+00:00 | 214.1ms | broker/backlog (no scale or GC event) | - | - | 882.5s / 873,078 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*32,663 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.62x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.36x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.01 | 970.87 | 1,264,894 | 1,263,716 | -1.4% | -0.09% | 1206.30 | 1,264,894 | 0 | 1.28 |
| Dekaf | 1.05 | 994.82 | 1,193,079 | 1,206,135 | -2.3% | -0.13% | 1137.81 | 1,193,079 | 0 | 1.25 |
| Confluent | 1.67 | - | 904,371 | 909,651 | +8.6% | +0.80% | 862.48 | 904,371 | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 371,822 | 413.13 | 942.50 KB |
| Dekaf | 2 | 380,836 | 423.14 | 953.54 KB |
| Dekaf | 3 | 377,169 | 419.07 | 938.20 KB |
| Dekaf (3conn) | 1 | 393,767 | 437.51 | 952.75 KB |
| Dekaf (3conn) | 2 | 402,582 | 447.30 | 962.81 KB |
| Dekaf (3conn) | 3 | 386,926 | 429.91 | 953.51 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:45.7442261+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 500,987 msg/s |
| Dekaf | 2026-07-19T03:07:03.7484149+00:00 | 3 | 16.0 MiB / 1.2 MiB | 403.1 MB/s | 0/0 | 3,434 | 18.0s / 920,775 msg/s |
| Dekaf | 2026-07-19T03:07:22.7749037+00:00 | 1 | 16.0 MiB / 1.2 MiB | 433.5 MB/s | 0/0 | 3,434 | 37.1s / 1,199,167 msg/s |
| Dekaf | 2026-07-19T03:07:40.7849412+00:00 | 1 | 16.0 MiB / 7.6 MiB | 433.5 MB/s | 0/1 | 4,376 | 55.1s / 1,223,612 msg/s |
| Dekaf | 2026-07-19T03:07:58.7933986+00:00 | 2 | 16.0 MiB / 6.7 MiB | 483.0 MB/s | 0/1 | 8,457 | 73.1s / 1,185,022 msg/s |
| Dekaf | 2026-07-19T03:08:16.8044599+00:00 | 2 | 16.0 MiB / 1.7 MiB | 503.4 MB/s | 0/2 | 10,380 | 91.1s / 1,268,947 msg/s |
| Dekaf | 2026-07-19T03:08:34.8234437+00:00 | 3 | 16.0 MiB / 2.5 MiB | 481.2 MB/s | 0/2 | 9,019 | 109.1s / 1,136,211 msg/s |
| Dekaf | 2026-07-19T03:08:52.8510989+00:00 | 3 | 16.0 MiB / 5.6 MiB | 481.2 MB/s | 0/2 | 10,487 | 127.2s / 1,224,424 msg/s |
| Dekaf | 2026-07-19T03:09:11.8654475+00:00 | 1 | 16.0 MiB / 3.6 MiB | 492.9 MB/s | 0/2 | 8,284 | 146.2s / 1,250,532 msg/s |
| Dekaf | 2026-07-19T03:09:29.8723317+00:00 | 1 | 14.0 MiB / 10.9 MiB | 492.9 MB/s | 0/2 | 8,821 | 164.2s / 1,280,507 msg/s |
| Dekaf | 2026-07-19T03:09:47.8801017+00:00 | 2 | 10.0 MiB / 2.1 MiB | 503.4 MB/s | 3/2 | 21,953 | 182.2s / 1,194,313 msg/s |
| Dekaf | 2026-07-19T03:10:05.8942222+00:00 | 2 | 8.0 MiB / 1.3 MiB | 503.4 MB/s | 4/2 | 24,227 | 200.2s / 1,188,965 msg/s |
| Dekaf | 2026-07-19T03:10:23.910462+00:00 | 3 | 16.0 MiB / 4.9 MiB | 481.2 MB/s | 0/2 | 13,563 | 218.2s / 1,018,183 msg/s |
| Dekaf | 2026-07-19T03:10:41.9155924+00:00 | 3 | 16.0 MiB / 5.5 MiB | 481.2 MB/s | 0/2 | 13,735 | 236.2s / 1,248,933 msg/s |
| Dekaf | 2026-07-19T03:11:00.930556+00:00 | 1 | 14.0 MiB / 1.8 MiB | 492.9 MB/s | 1/3 | 10,433 | 255.2s / 1,221,355 msg/s |
| Dekaf | 2026-07-19T03:11:18.9511214+00:00 | 1 | 15.0 MiB / 1.3 MiB | 492.9 MB/s | 1/3 | 11,385 | 273.2s / 1,198,275 msg/s |
| Dekaf | 2026-07-19T03:11:36.9633611+00:00 | 2 | 10.0 MiB / 3.2 MiB | 503.4 MB/s | 6/2 | 37,547 | 291.3s / 1,263,293 msg/s |
| Dekaf | 2026-07-19T03:11:54.9674775+00:00 | 2 | 11.0 MiB / 3.2 MiB | 503.4 MB/s | 6/2 | 40,582 | 309.3s / 1,199,918 msg/s |
| Dekaf | 2026-07-19T03:12:12.9825212+00:00 | 3 | 16.0 MiB / 2.9 MiB | 481.2 MB/s | 0/3 | 16,938 | 327.3s / 1,303,431 msg/s |
| Dekaf | 2026-07-19T03:12:30.9904025+00:00 | 3 | 16.0 MiB / 0.5 MiB | 481.2 MB/s | 0/3 | 17,538 | 345.3s / 1,207,165 msg/s |
| Dekaf | 2026-07-19T03:12:50.0026151+00:00 | 1 | 18.0 MiB / 10.4 MiB | 492.9 MB/s | 3/3 | 14,957 | 364.3s / 1,204,907 msg/s |
| Dekaf | 2026-07-19T03:13:08.0116332+00:00 | 1 | 16.0 MiB / 3.7 MiB | 492.9 MB/s | 3/4 | 15,618 | 382.3s / 1,200,445 msg/s |
| Dekaf | 2026-07-19T03:13:26.0259428+00:00 | 2 | 8.0 MiB / 2.6 MiB | 503.4 MB/s | 7/3 | 57,188 | 400.3s / 1,174,639 msg/s |
| Dekaf | 2026-07-19T03:13:44.0302751+00:00 | 2 | 9.0 MiB / 4.1 MiB | 503.4 MB/s | 8/3 | 59,988 | 418.4s / 1,252,434 msg/s |
| Dekaf | 2026-07-19T03:14:02.0488564+00:00 | 3 | 16.0 MiB / 2.0 MiB | 481.2 MB/s | 0/3 | 20,975 | 436.4s / 1,283,515 msg/s |
| Dekaf | 2026-07-19T03:14:20.0584763+00:00 | 3 | 16.0 MiB / 15.0 MiB | 481.2 MB/s | 0/3 | 21,624 | 454.4s / 1,261,789 msg/s |
| Dekaf | 2026-07-19T03:14:39.0655838+00:00 | 1 | 14.0 MiB / 0.6 MiB | 492.9 MB/s | 4/4 | 17,648 | 473.4s / 1,233,189 msg/s |
| Dekaf | 2026-07-19T03:14:57.0767451+00:00 | 1 | 14.0 MiB / 6.4 MiB | 492.9 MB/s | 4/4 | 18,997 | 491.4s / 1,190,884 msg/s |
| Dekaf | 2026-07-19T03:15:15.0885398+00:00 | 2 | 11.0 MiB / 11.0 MiB | 503.4 MB/s | 10/3 | 78,880 | 509.4s / 1,287,551 msg/s |
| Dekaf | 2026-07-19T03:15:33.0996199+00:00 | 2 | 11.0 MiB / 3.6 MiB | 503.4 MB/s | 10/3 | 82,127 | 527.4s / 1,286,516 msg/s |
| Dekaf | 2026-07-19T03:15:51.1059445+00:00 | 3 | 12.0 MiB / 8.4 MiB | 481.2 MB/s | 2/3 | 29,438 | 545.4s / 1,229,375 msg/s |
| Dekaf | 2026-07-19T03:16:09.1122643+00:00 | 3 | 10.0 MiB / 8.1 MiB | 481.2 MB/s | 3/3 | 31,725 | 563.4s / 1,259,040 msg/s |
| Dekaf | 2026-07-19T03:16:28.1160287+00:00 | 1 | 8.0 MiB / 0.9 MiB | 492.9 MB/s | 7/5 | 31,997 | 582.4s / 1,240,834 msg/s |
| Dekaf | 2026-07-19T03:16:46.1297786+00:00 | 1 | 8.0 MiB / 8.0 MiB | 492.9 MB/s | 7/5 | 35,206 | 600.5s / 1,207,036 msg/s |
| Dekaf | 2026-07-19T03:17:04.1315643+00:00 | 2 | 9.0 MiB / 9.0 MiB | 503.4 MB/s | 11/4 | 95,965 | 618.5s / 1,226,787 msg/s |
| Dekaf | 2026-07-19T03:17:22.1402224+00:00 | 2 | 9.0 MiB / 7.1 MiB | 503.4 MB/s | 11/4 | 99,465 | 636.5s / 1,161,862 msg/s |
| Dekaf | 2026-07-19T03:17:40.1490836+00:00 | 3 | 9.0 MiB / 8.4 MiB | 481.2 MB/s | 5/3 | 43,601 | 654.5s / 1,211,264 msg/s |
| Dekaf | 2026-07-19T03:17:58.1604734+00:00 | 3 | 10.0 MiB / 6.5 MiB | 481.2 MB/s | 6/3 | 45,795 | 672.5s / 1,256,864 msg/s |
| Dekaf | 2026-07-19T03:18:17.1737282+00:00 | 1 | 8.0 MiB / 8.0 MiB | 492.9 MB/s | 7/6 | 46,738 | 691.5s / 1,129,205 msg/s |
| Dekaf | 2026-07-19T03:18:35.1836384+00:00 | 1 | 8.0 MiB / 3.1 MiB | 492.9 MB/s | 7/6 | 48,466 | 709.5s / 1,052,697 msg/s |
| Dekaf | 2026-07-19T03:18:53.1926517+00:00 | 2 | 9.0 MiB / 2.5 MiB | 503.4 MB/s | 11/6 | 111,735 | 727.6s / 1,095,049 msg/s |
| Dekaf | 2026-07-19T03:19:11.2084576+00:00 | 2 | 9.0 MiB / 1.9 MiB | 503.4 MB/s | 11/6 | 112,792 | 745.6s / 1,118,611 msg/s |
| Dekaf | 2026-07-19T03:19:29.2174457+00:00 | 3 | 11.0 MiB / 5.7 MiB | 481.2 MB/s | 7/4 | 51,241 | 763.6s / 1,194,823 msg/s |
| Dekaf | 2026-07-19T03:19:47.2240723+00:00 | 3 | 11.0 MiB / 2.6 MiB | 481.2 MB/s | 7/4 | 52,107 | 781.6s / 1,154,637 msg/s |
| Dekaf | 2026-07-19T03:20:06.2311671+00:00 | 1 | 8.0 MiB / 2.1 MiB | 492.9 MB/s | 7/7 | 53,889 | 800.6s / 1,195,033 msg/s |
| Dekaf | 2026-07-19T03:20:24.2405001+00:00 | 1 | 8.0 MiB / 3.3 MiB | 492.9 MB/s | 7/7 | 55,157 | 818.6s / 1,138,997 msg/s |
| Dekaf | 2026-07-19T03:20:42.2496685+00:00 | 2 | 9.0 MiB / 5.1 MiB | 503.4 MB/s | 11/6 | 120,310 | 836.6s / 1,224,277 msg/s |
| Dekaf | 2026-07-19T03:21:00.262484+00:00 | 2 | 8.0 MiB / 3.1 MiB | 503.4 MB/s | 11/6 | 122,369 | 854.6s / 1,198,834 msg/s |
| Dekaf | 2026-07-19T03:21:18.2681754+00:00 | 3 | 11.0 MiB / 2.6 MiB | 481.2 MB/s | 7/5 | 55,010 | 872.6s / 1,186,480 msg/s |
| Dekaf | 2026-07-19T03:21:36.278476+00:00 | 3 | 11.0 MiB / 2.4 MiB | 481.2 MB/s | 7/5 | 55,681 | 890.7s / 1,202,823 msg/s |
| Dekaf (3conn) | 2026-07-19T03:37:08.1054882+00:00 | 3 | 16.0 MiB / 3.4 MiB | 452.7 MB/s | 0/0 | 621 | 9.0s / 1,013,657 msg/s |
| Dekaf (3conn) | 2026-07-19T03:37:26.1297691+00:00 | 3 | 16.0 MiB / 3.0 MiB | 505.6 MB/s | 0/0 | 1,716 | 27.0s / 1,230,417 msg/s |
| Dekaf (3conn) | 2026-07-19T03:37:45.147093+00:00 | 1 | 14.0 MiB / 0.4 MiB | 515.8 MB/s | 1/0 | 3,735 | 46.1s / 1,243,669 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:03.1654527+00:00 | 1 | 14.0 MiB / 2.2 MiB | 515.8 MB/s | 1/1 | 4,636 | 64.1s / 1,149,986 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:21.1761221+00:00 | 2 | 14.0 MiB / 9.4 MiB | 543.3 MB/s | 1/1 | 7,349 | 82.1s / 1,299,526 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:39.1849234+00:00 | 2 | 14.0 MiB / 0.9 MiB | 543.3 MB/s | 1/1 | 9,491 | 100.1s / 1,230,847 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:57.2044811+00:00 | 3 | 14.0 MiB / 2.4 MiB | 520.6 MB/s | 1/1 | 6,406 | 118.1s / 1,274,580 msg/s |
| Dekaf (3conn) | 2026-07-19T03:39:15.2219154+00:00 | 3 | 15.0 MiB / 7.2 MiB | 520.6 MB/s | 1/1 | 7,172 | 136.1s / 1,320,105 msg/s |
| Dekaf (3conn) | 2026-07-19T03:39:34.2497255+00:00 | 1 | 10.0 MiB / 6.7 MiB | 528.8 MB/s | 2/1 | 14,083 | 155.2s / 1,273,995 msg/s |
| Dekaf (3conn) | 2026-07-19T03:39:52.2715892+00:00 | 1 | 12.0 MiB / 0.4 MiB | 548.9 MB/s | 2/2 | 17,144 | 173.2s / 1,316,249 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:10.2849465+00:00 | 2 | 15.0 MiB / 0.0 MiB | 578.4 MB/s | 2/2 | 16,869 | 191.2s / 1,111,671 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:28.3008366+00:00 | 2 | 15.0 MiB / 5.7 MiB | 578.4 MB/s | 2/2 | 17,966 | 209.2s / 1,171,677 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:46.3124696+00:00 | 3 | 18.0 MiB / 2.2 MiB | 567.4 MB/s | 3/1 | 9,644 | 227.2s / 1,245,089 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:04.3256491+00:00 | 3 | 16.0 MiB / 2.3 MiB | 567.4 MB/s | 3/2 | 10,321 | 245.3s / 1,273,309 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:23.3291246+00:00 | 1 | 12.0 MiB / 3.9 MiB | 548.9 MB/s | 2/3 | 27,312 | 264.3s / 1,222,111 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:41.3530152+00:00 | 1 | 12.0 MiB / 11.0 MiB | 565.9 MB/s | 2/3 | 29,079 | 282.3s / 1,360,119 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:59.3664515+00:00 | 2 | 13.0 MiB / 11.9 MiB | 607.3 MB/s | 3/2 | 25,970 | 300.3s / 1,197,771 msg/s |
| Dekaf (3conn) | 2026-07-19T03:42:17.3767871+00:00 | 2 | 11.0 MiB / 4.5 MiB | 607.3 MB/s | 4/2 | 28,208 | 318.3s / 1,174,583 msg/s |
| Dekaf (3conn) | 2026-07-19T03:42:35.3828281+00:00 | 3 | 12.0 MiB / 5.9 MiB | 567.4 MB/s | 4/2 | 13,163 | 336.3s / 1,248,904 msg/s |
| Dekaf (3conn) | 2026-07-19T03:42:53.3888657+00:00 | 3 | 14.0 MiB / 7.1 MiB | 567.4 MB/s | 4/3 | 13,869 | 354.3s / 1,238,229 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:12.4013319+00:00 | 1 | 8.0 MiB / 6.8 MiB | 565.9 MB/s | 3/3 | 36,735 | 373.4s / 1,213,407 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:30.4105207+00:00 | 1 | 8.0 MiB / 1.2 MiB | 565.9 MB/s | 4/3 | 40,183 | 391.4s / 1,294,151 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:48.4202649+00:00 | 2 | 11.0 MiB / 0.2 MiB | 607.3 MB/s | 4/4 | 41,094 | 409.4s / 1,341,868 msg/s |
| Dekaf (3conn) | 2026-07-19T03:44:06.4354436+00:00 | 2 | 11.0 MiB / 5.6 MiB | 607.3 MB/s | 4/4 | 42,242 | 427.4s / 1,271,574 msg/s |
| Dekaf (3conn) | 2026-07-19T03:44:24.4363951+00:00 | 3 | 15.0 MiB / 4.1 MiB | 567.4 MB/s | 5/3 | 15,833 | 445.4s / 1,275,008 msg/s |
| Dekaf (3conn) | 2026-07-19T03:44:42.4484397+00:00 | 3 | 15.0 MiB / 14.5 MiB | 567.4 MB/s | 5/3 | 16,223 | 463.4s / 1,241,676 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:01.4624446+00:00 | 1 | 8.0 MiB / 2.9 MiB | 565.9 MB/s | 4/4 | 58,964 | 482.4s / 1,332,932 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:19.4678826+00:00 | 1 | 8.0 MiB / 3.5 MiB | 565.9 MB/s | 4/4 | 63,282 | 500.5s / 1,178,995 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:37.4895851+00:00 | 2 | 9.0 MiB / 8.6 MiB | 607.3 MB/s | 4/4 | 51,970 | 518.5s / 1,299,973 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:55.5109553+00:00 | 2 | 8.0 MiB / 5.4 MiB | 607.3 MB/s | 5/4 | 55,893 | 536.5s / 1,253,194 msg/s |
| Dekaf (3conn) | 2026-07-19T03:46:13.5190113+00:00 | 3 | 13.0 MiB / 3.1 MiB | 567.4 MB/s | 6/4 | 18,984 | 554.5s / 1,362,815 msg/s |
| Dekaf (3conn) | 2026-07-19T03:46:31.5274916+00:00 | 3 | 13.0 MiB / 4.6 MiB | 567.4 MB/s | 6/4 | 19,673 | 572.5s / 1,349,750 msg/s |
| Dekaf (3conn) | 2026-07-19T03:46:50.5364426+00:00 | 1 | 9.0 MiB / 5.1 MiB | 565.9 MB/s | 5/5 | 77,899 | 591.6s / 1,212,581 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:08.5564459+00:00 | 1 | 9.0 MiB / 5.0 MiB | 565.9 MB/s | 5/5 | 80,227 | 609.6s / 1,368,912 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:26.574848+00:00 | 2 | 9.0 MiB / 2.4 MiB | 607.3 MB/s | 5/6 | 72,663 | 627.6s / 1,219,182 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:44.5996114+00:00 | 2 | 9.0 MiB / 4.6 MiB | 607.3 MB/s | 5/6 | 74,874 | 645.6s / 1,237,508 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:02.6083852+00:00 | 3 | 9.0 MiB / 0.6 MiB | 567.4 MB/s | 8/4 | 24,734 | 663.6s / 1,300,905 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:20.6185084+00:00 | 3 | 8.0 MiB / 4.2 MiB | 567.4 MB/s | 8/4 | 26,745 | 681.6s / 1,347,130 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:39.6403147+00:00 | 1 | 9.0 MiB / 7.4 MiB | 565.9 MB/s | 7/5 | 93,900 | 700.7s / 1,236,594 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:57.6504514+00:00 | 1 | 9.0 MiB / 3.6 MiB | 565.9 MB/s | 7/6 | 95,842 | 718.7s / 1,134,747 msg/s |
| Dekaf (3conn) | 2026-07-19T03:49:15.6670298+00:00 | 2 | 9.0 MiB / 5.2 MiB | 607.3 MB/s | 5/6 | 88,195 | 736.7s / 1,339,717 msg/s |
| Dekaf (3conn) | 2026-07-19T03:49:33.6820684+00:00 | 2 | 9.0 MiB / 3.0 MiB | 607.3 MB/s | 5/6 | 91,743 | 754.7s / 1,182,094 msg/s |
| Dekaf (3conn) | 2026-07-19T03:49:51.6965349+00:00 | 3 | 9.0 MiB / 5.5 MiB | 567.4 MB/s | 10/5 | 39,031 | 772.7s / 1,196,327 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:09.7089334+00:00 | 3 | 9.0 MiB / 3.7 MiB | 567.4 MB/s | 10/5 | 40,734 | 790.8s / 1,352,875 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:28.7174442+00:00 | 1 | 9.0 MiB / 3.2 MiB | 565.9 MB/s | 9/6 | 108,590 | 809.8s / 1,218,770 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:46.730515+00:00 | 1 | 9.0 MiB / 2.1 MiB | 565.9 MB/s | 9/6 | 110,781 | 827.8s / 1,280,531 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:04.7502986+00:00 | 2 | 9.0 MiB / 6.5 MiB | 607.3 MB/s | 5/7 | 106,185 | 845.8s / 1,157,528 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:22.7569974+00:00 | 2 | 9.0 MiB / 3.0 MiB | 607.3 MB/s | 5/7 | 109,714 | 863.8s / 1,314,160 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:40.7795601+00:00 | 3 | 9.0 MiB / 3.6 MiB | 567.4 MB/s | 10/6 | 51,569 | 881.8s / 1,214,736 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:58.7879933+00:00 | 3 | 9.0 MiB / 2.2 MiB | 567.4 MB/s | 10/6 | 53,976 | 899.8s / 1,277,914 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-19T03:07:15.9480887+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-19T03:07:16.0274796+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:07:31.0684512+00:00 | 3 | capacity | failed | 15,064ms | 16.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-19T03:07:31.1108516+00:00 | 2 | capacity | failed | 15,083ms | 16.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-19T03:08:14.807513+00:00 | 2 | capacity | failed | 13,550ms | 16.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-19T03:08:31.3623349+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 0.7 MiB |
| Dekaf | 2026-07-19T03:08:44.9671826+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-19T03:09:00.0238184+00:00 | 2 | capacity | succeeded | 15,056ms | 14.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-19T03:09:03.0311872+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-19T03:09:18.154155+00:00 | 2 | capacity | succeeded | 15,122ms | 12.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-19T03:09:31.6658192+00:00 | 1 | capacity | succeeded | 15,106ms | 14.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-19T03:09:39.2390758+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-19T03:10:01.7867056+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-19T03:10:24.5024889+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-19T03:10:33.3965633+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-19T03:10:48.4393638+00:00 | 3 | capacity | failed | 15,042ms | 16.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-19T03:11:17.1543601+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-19T03:11:32.2248032+00:00 | 1 | capacity | succeeded | 15,070ms | 15.0 MiB / 2.8 MiB |
| Dekaf | 2026-07-19T03:12:02.350254+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-19T03:12:09.9996929+00:00 | 2 | capacity | failed | 15,056ms | 10.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-19T03:12:47.5502349+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-19T03:13:10.3032388+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:13:28.4009634+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-19T03:14:00.9591936+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-19T03:14:13.5516436+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-19T03:14:28.6665397+00:00 | 2 | capacity | succeeded | 15,114ms | 10.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-19T03:14:49.6865656+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-19T03:15:01.2544997+00:00 | 1 | capacity | succeeded | 15,073ms | 12.0 MiB / 0.5 MiB |
| Dekaf | 2026-07-19T03:15:04.7381995+00:00 | 3 | capacity | succeeded | 15,051ms | 14.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-19T03:15:13.8850029+00:00 | 2 | capacity | succeeded | 15,061ms | 11.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-19T03:15:34.8745066+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-19T03:15:49.4467408+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-19T03:15:52.9391926+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-19T03:16:04.5159954+00:00 | 1 | capacity | succeeded | 15,069ms | 8.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-19T03:16:07.5275522+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:16:11.0059857+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-19T03:16:26.0896243+00:00 | 3 | capacity | succeeded | 15,083ms | 8.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:16:44.3051519+00:00 | 2 | capacity | succeeded | 15,064ms | 9.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-19T03:17:11.3084548+00:00 | 3 | capacity | succeeded | 15,041ms | 9.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-19T03:17:22.882357+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-19T03:17:29.4931508+00:00 | 2 | capacity | failed | 15,059ms | 9.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-19T03:17:41.4445377+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-19T03:18:26.6697645+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-19T03:18:41.7250527+00:00 | 3 | capacity | succeeded | 15,055ms | 11.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-19T03:19:11.8190148+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-19T03:19:26.8863494+00:00 | 3 | capacity | failed | 15,067ms | 11.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-19T03:19:53.4785382+00:00 | 1 | capacity | failed | 15,056ms | 8.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-19T03:20:42.2117866+00:00 | 3 | capacity | failed | 15,058ms | 11.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-19T03:21:00.4324795+00:00 | 2 | capacity | succeeded | 15,057ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-19T03:21:45.6642192+00:00 | 2 | capacity | failed | 15,094ms | 8.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:29.3026933+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:29.4565314+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:44.5064029+00:00 | 2 | capacity | succeeded | 15,082ms | 14.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:47.4637256+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 7.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:47.5615799+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:02.5169649+00:00 | 1 | capacity | failed | 15,052ms | 14.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:02.6384005+00:00 | 3 | capacity | failed | 15,076ms | 14.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:02.9295067+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 13.0 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:17.9515867+00:00 | 1 | capacity | succeeded | 15,129ms | 12.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:18.0678623+00:00 | 3 | capacity | succeeded | 15,080ms | 15.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:20.9798776+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:48.1684855+00:00 | 2 | capacity | started | 0ms | 16.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:40:03.2449708+00:00 | 2 | capacity | failed | 15,076ms | 15.0 MiB / 0.3 MiB |
| Dekaf (3conn) | 2026-07-19T03:40:33.5224835+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:40:48.6157416+00:00 | 3 | capacity | failed | 15,093ms | 16.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:41:03.7532228+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:41:18.8108074+00:00 | 2 | capacity | succeeded | 15,057ms | 13.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-19T03:41:49.0730634+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:04.143587+00:00 | 2 | capacity | succeeded | 15,070ms | 11.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:34.2762046+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:49.3811797+00:00 | 2 | capacity | failed | 15,104ms | 11.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:52.2661161+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:10.3719142+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:25.4518008+00:00 | 1 | capacity | succeeded | 15,079ms | 8.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:49.6010267+00:00 | 3 | capacity | started | 0ms | 15.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:44:04.6799255+00:00 | 3 | capacity | succeeded | 15,078ms | 15.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:44:10.6422554+00:00 | 1 | capacity | failed | 15,063ms | 8.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:44:49.969899+00:00 | 3 | capacity | failed | 15,097ms | 15.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:45:26.1096182+00:00 | 1 | capacity | succeeded | 15,092ms | 9.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-19T03:45:50.2872184+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:45:53.335539+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 7.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:45:56.2692718+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-19T03:46:08.4232707+00:00 | 2 | capacity | failed | 15,087ms | 9.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:46:35.5516131+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:08.7119064+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:20.7662678+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:23.8015759+00:00 | 2 | capacity | failed | 15,089ms | 9.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:35.8422531+00:00 | 3 | capacity | succeeded | 15,075ms | 9.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:06.0492945+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:21.1235143+00:00 | 3 | capacity | succeeded | 15,074ms | 8.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:51.2739848+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:49:06.3508892+00:00 | 3 | capacity | succeeded | 15,076ms | 9.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:49:24.5755589+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-19T03:49:36.5802955+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:49:42.5074895+00:00 | 1 | capacity | succeeded | 15,108ms | 8.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:12.6405127+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:52.0443577+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:57.9056082+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 0.2 MiB |
| Dekaf (3conn) | 2026-07-19T03:51:12.967414+00:00 | 1 | capacity | succeeded | 15,061ms | 10.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:51:58.2346027+00:00 | 1 | capacity | failed | 15,083ms | 10.0 MiB / 6.8 MiB |
*80 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 3 |
| Dekaf | 1 | 0.002–0.004ms | 4 |
| Dekaf | 1 | 0.004–0.008ms | 11 |
| Dekaf | 1 | 0.008–0.016ms | 56 |
| Dekaf | 1 | 0.016–0.032ms | 173 |
| Dekaf | 1 | 0.032–0.064ms | 307 |
| Dekaf | 1 | 0.064–0.128ms | 272 |
| Dekaf | 1 | 0.128–0.256ms | 407 |
| Dekaf | 1 | 0.256–0.512ms | 660 |
| Dekaf | 1 | 0.512–1.024ms | 1,137 |
| Dekaf | 1 | 1.024–2.048ms | 1,370 |
| Dekaf | 1 | 2.048–4.096ms | 1,486 |
| Dekaf | 1 | 4.096–8.192ms | 1,008 |
| Dekaf | 1 | 8.192–16.384ms | 388 |
| Dekaf | 1 | 16.384–32.768ms | 97 |
| Dekaf | 1 | 32.768–65.536ms | 13 |
| Dekaf | 2 | 0.001–0.002ms | 10 |
| Dekaf | 2 | 0.002–0.004ms | 13 |
| Dekaf | 2 | 0.004–0.008ms | 26 |
| Dekaf | 2 | 0.008–0.016ms | 118 |
| Dekaf | 2 | 0.016–0.032ms | 358 |
| Dekaf | 2 | 0.032–0.064ms | 668 |
| Dekaf | 2 | 0.064–0.128ms | 634 |
| Dekaf | 2 | 0.128–0.256ms | 853 |
| Dekaf | 2 | 0.256–0.512ms | 1,477 |
| Dekaf | 2 | 0.512–1.024ms | 2,291 |
| Dekaf | 2 | 1.024–2.048ms | 3,173 |
| Dekaf | 2 | 2.048–4.096ms | 3,223 |
| Dekaf | 2 | 4.096–8.192ms | 2,180 |
| Dekaf | 2 | 8.192–16.384ms | 976 |
| Dekaf | 2 | 16.384–32.768ms | 340 |
| Dekaf | 2 | 32.768–65.536ms | 24 |
| Dekaf | 3 | 0.001–0.002ms | 5 |
| Dekaf | 3 | 0.002–0.004ms | 3 |
| Dekaf | 3 | 0.004–0.008ms | 9 |
| Dekaf | 3 | 0.008–0.016ms | 38 |
| Dekaf | 3 | 0.016–0.032ms | 131 |
| Dekaf | 3 | 0.032–0.064ms | 223 |
| Dekaf | 3 | 0.064–0.128ms | 191 |
| Dekaf | 3 | 0.128–0.256ms | 323 |
| Dekaf | 3 | 0.256–0.512ms | 501 |
| Dekaf | 3 | 0.512–1.024ms | 833 |
| Dekaf | 3 | 1.024–2.048ms | 1,163 |
| Dekaf | 3 | 2.048–4.096ms | 1,302 |
| Dekaf | 3 | 4.096–8.192ms | 1,030 |
| Dekaf | 3 | 8.192–16.384ms | 506 |
| Dekaf | 3 | 16.384–32.768ms | 202 |
| Dekaf | 3 | 32.768–65.536ms | 26 |
| Dekaf | 3 | 65.536–131.072ms | 2 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 5 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 15 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 29 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 124 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 355 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 700 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 739 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 868 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 1,184 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 2,007 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 2,562 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 2,666 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 1,959 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 701 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 243 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 38 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 13 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 11 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 29 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 116 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 377 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 620 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 694 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 834 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 1,177 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 2,028 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 2,484 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 2,696 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,785 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 833 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 297 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 58 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 10 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 5 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 22 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 67 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 167 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 301 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 321 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 366 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 590 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 888 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 1,176 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 1,257 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 815 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 338 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 101 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 20 |
| Dekaf (3conn) | 3 | 131.072–262.144ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 72,000 | 2026-07-19T03:06:45.9271941+00:00 | 138.5ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 78,000 | 2026-07-19T03:06:45.9349159+00:00 | 130.7ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 81,000 | 2026-07-19T03:06:45.9376895+00:00 | 128.0ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 82,000 | 2026-07-19T03:06:45.9409746+00:00 | 124.7ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 85,000 | 2026-07-19T03:06:45.9443068+00:00 | 132.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 88,000 | 2026-07-19T03:06:45.9533704+00:00 | 123.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 91,000 | 2026-07-19T03:06:45.9587285+00:00 | 119.8ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 92,000 | 2026-07-19T03:06:45.9607418+00:00 | 117.8ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 98,000 | 2026-07-19T03:06:45.971534+00:00 | 118.1ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 101,000 | 2026-07-19T03:06:45.9752418+00:00 | 116.7ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 102,000 | 2026-07-19T03:06:45.9776089+00:00 | 116.5ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 105,000 | 2026-07-19T03:06:45.9831352+00:00 | 115.0ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 111,000 | 2026-07-19T03:06:45.9906403+00:00 | 119.4ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 118,000 | 2026-07-19T03:06:46.0082317+00:00 | 132.6ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 121,000 | 2026-07-19T03:06:46.0598825+00:00 | 105.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 122,000 | 2026-07-19T03:06:46.0608625+00:00 | 104.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 151,000 | 2026-07-19T03:06:46.1035441+00:00 | 116.5ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 155,000 | 2026-07-19T03:06:46.1081612+00:00 | 102.6ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 161,000 | 2026-07-19T03:06:46.1147709+00:00 | 105.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 258,000 | 2026-07-19T03:06:46.3067325+00:00 | 110.6ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 261,000 | 2026-07-19T03:06:46.3108966+00:00 | 109.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 262,000 | 2026-07-19T03:06:46.312954+00:00 | 107.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 268,000 | 2026-07-19T03:06:46.3230384+00:00 | 106.5ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 271,000 | 2026-07-19T03:06:46.3295677+00:00 | 102.8ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 272,000 | 2026-07-19T03:06:46.3304117+00:00 | 101.9ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 377,000 | 2026-07-19T03:06:46.5237906+00:00 | 120.8ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 379,000 | 2026-07-19T03:06:46.5255727+00:00 | 119.0ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 387,000 | 2026-07-19T03:06:46.5510213+00:00 | 102.3ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 389,000 | 2026-07-19T03:06:46.5541093+00:00 | 128.2ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 393,000 | 2026-07-19T03:06:46.5617788+00:00 | 134.7ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 397,000 | 2026-07-19T03:06:46.5694427+00:00 | 108.1ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 403,000 | 2026-07-19T03:06:46.5798843+00:00 | 119.2ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 409,000 | 2026-07-19T03:06:46.5896865+00:00 | 110.8ms | GC pause | - | - | 1.0s / 500,987 msg/s | Gen2 +1 / pause +3.1ms |
| Dekaf | 485,000 | 2026-07-19T03:06:46.7279668+00:00 | 104.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 488,000 | 2026-07-19T03:06:46.7296864+00:00 | 102.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 491,000 | 2026-07-19T03:06:46.7315369+00:00 | 100.6ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 492,000 | 2026-07-19T03:06:46.7319533+00:00 | 100.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 495,000 | 2026-07-19T03:06:46.7342236+00:00 | 165.7ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 498,000 | 2026-07-19T03:06:46.7356951+00:00 | 164.3ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 499,000 | 2026-07-19T03:06:46.7362442+00:00 | 105.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 501,000 | 2026-07-19T03:06:46.7376429+00:00 | 168.0ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 505,000 | 2026-07-19T03:06:46.7510286+00:00 | 156.3ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 508,000 | 2026-07-19T03:06:46.7553509+00:00 | 152.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 511,000 | 2026-07-19T03:06:46.7591903+00:00 | 179.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 512,000 | 2026-07-19T03:06:46.7917754+00:00 | 147.3ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +2 / pause +3.7ms |
| Dekaf | 521,000 | 2026-07-19T03:06:46.8197721+00:00 | 118.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 525,000 | 2026-07-19T03:06:46.8270321+00:00 | 112.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 528,000 | 2026-07-19T03:06:46.8297347+00:00 | 117.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 531,000 | 2026-07-19T03:06:46.8330113+00:00 | 121.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 532,000 | 2026-07-19T03:06:46.834608+00:00 | 119.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 535,000 | 2026-07-19T03:06:46.8361739+00:00 | 117.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 538,000 | 2026-07-19T03:06:46.8424389+00:00 | 111.7ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 541,000 | 2026-07-19T03:06:46.8453078+00:00 | 110.7ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 542,000 | 2026-07-19T03:06:46.846838+00:00 | 109.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 545,000 | 2026-07-19T03:06:46.8495624+00:00 | 106.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 669,000 | 2026-07-19T03:06:47.056457+00:00 | 100.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 904,000 | 2026-07-19T03:06:47.3675617+00:00 | 101.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 908,000 | 2026-07-19T03:06:47.3721038+00:00 | 104.6ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 910,000 | 2026-07-19T03:06:47.373013+00:00 | 121.8ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 911,000 | 2026-07-19T03:06:47.3735417+00:00 | 108.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 914,000 | 2026-07-19T03:06:47.37489+00:00 | 120.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 915,000 | 2026-07-19T03:06:47.3752485+00:00 | 107.8ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 916,000 | 2026-07-19T03:06:47.3798936+00:00 | 115.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 918,000 | 2026-07-19T03:06:47.3828884+00:00 | 100.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 920,000 | 2026-07-19T03:06:47.3837699+00:00 | 115.7ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 922,000 | 2026-07-19T03:06:47.3862392+00:00 | 110.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 924,000 | 2026-07-19T03:06:47.3886081+00:00 | 120.6ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 925,000 | 2026-07-19T03:06:47.3895224+00:00 | 113.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 928,000 | 2026-07-19T03:06:47.3914408+00:00 | 111.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 930,000 | 2026-07-19T03:06:47.3938727+00:00 | 113.0ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 931,000 | 2026-07-19T03:06:47.3956234+00:00 | 128.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 932,000 | 2026-07-19T03:06:47.3964795+00:00 | 128.0ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 934,000 | 2026-07-19T03:06:47.3986439+00:00 | 110.6ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 935,000 | 2026-07-19T03:06:47.3992065+00:00 | 106.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 938,000 | 2026-07-19T03:06:47.4028224+00:00 | 103.3ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 940,000 | 2026-07-19T03:06:47.4047219+00:00 | 105.6ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 942,000 | 2026-07-19T03:06:47.4062366+00:00 | 117.1ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 945,000 | 2026-07-19T03:06:47.4085147+00:00 | 104.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 958,000 | 2026-07-19T03:06:47.4948254+00:00 | 141.6ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 961,000 | 2026-07-19T03:06:47.4962688+00:00 | 141.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 962,000 | 2026-07-19T03:06:47.4965511+00:00 | 141.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 965,000 | 2026-07-19T03:06:47.4997678+00:00 | 149.2ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 968,000 | 2026-07-19T03:06:47.501122+00:00 | 147.8ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 971,000 | 2026-07-19T03:06:47.506873+00:00 | 143.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 972,000 | 2026-07-19T03:06:47.5073811+00:00 | 142.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 975,000 | 2026-07-19T03:06:47.5096061+00:00 | 145.8ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 978,000 | 2026-07-19T03:06:47.5125417+00:00 | 151.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 981,000 | 2026-07-19T03:06:47.5147203+00:00 | 148.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 982,000 | 2026-07-19T03:06:47.5151879+00:00 | 148.8ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 985,000 | 2026-07-19T03:06:47.5167263+00:00 | 147.3ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 988,000 | 2026-07-19T03:06:47.5179973+00:00 | 146.0ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 991,000 | 2026-07-19T03:06:47.5258048+00:00 | 143.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 992,000 | 2026-07-19T03:06:47.5276672+00:00 | 141.7ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 995,000 | 2026-07-19T03:06:47.5298352+00:00 | 145.3ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 998,000 | 2026-07-19T03:06:47.5328162+00:00 | 151.5ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 1,001,000 | 2026-07-19T03:06:47.5362846+00:00 | 148.4ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 1,002,000 | 2026-07-19T03:06:47.5367596+00:00 | 147.9ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 1,005,000 | 2026-07-19T03:06:47.5429156+00:00 | 144.7ms | GC pause | - | - | 2.0s / 618,804 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 1,315,000 | 2026-07-19T03:06:48.0026911+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,318,000 | 2026-07-19T03:06:48.0044072+00:00 | 126.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,321,000 | 2026-07-19T03:06:48.005794+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,322,000 | 2026-07-19T03:06:48.0071733+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,325,000 | 2026-07-19T03:06:48.0100474+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,806,000 | 2026-07-19T03:06:48.4945538+00:00 | 109.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,814,000 | 2026-07-19T03:06:48.5041386+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,824,000 | 2026-07-19T03:06:48.5222358+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,830,000 | 2026-07-19T03:06:48.5267951+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,834,000 | 2026-07-19T03:06:48.5295734+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,844,000 | 2026-07-19T03:06:48.5354796+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,846,000 | 2026-07-19T03:06:48.544673+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,850,000 | 2026-07-19T03:06:48.557028+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,854,000 | 2026-07-19T03:06:48.558599+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,856,000 | 2026-07-19T03:06:48.5599033+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 902,229 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,358,000 | 2026-07-19T03:06:50.021007+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,362,000 | 2026-07-19T03:06:50.0226311+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,371,000 | 2026-07-19T03:06:50.0277633+00:00 | 121.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,372,000 | 2026-07-19T03:06:50.0280833+00:00 | 121.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,374,000 | 2026-07-19T03:06:50.0292467+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,375,000 | 2026-07-19T03:06:50.0297458+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,376,000 | 2026-07-19T03:06:50.0304041+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,381,000 | 2026-07-19T03:06:50.0326083+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,382,000 | 2026-07-19T03:06:50.0330382+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,384,000 | 2026-07-19T03:06:50.0338045+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,385,000 | 2026-07-19T03:06:50.0341308+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,386,000 | 2026-07-19T03:06:50.0346213+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,388,000 | 2026-07-19T03:06:50.0353515+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,391,000 | 2026-07-19T03:06:50.0404742+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,392,000 | 2026-07-19T03:06:50.0411121+00:00 | 122.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 938,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,254,000 | 2026-07-19T03:06:50.985847+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,010,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,266,000 | 2026-07-19T03:06:50.994217+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,010,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,300,000 | 2026-07-19T03:06:51.0343288+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,010,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,849,000 | 2026-07-19T03:06:51.5433401+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,010,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,853,000 | 2026-07-19T03:06:51.5453074+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,010,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,365,000 | 2026-07-19T03:06:52.1076229+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 903,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,378,000 | 2026-07-19T03:06:52.1132637+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 903,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,381,000 | 2026-07-19T03:06:52.1144953+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 903,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,807,000 | 2026-07-19T03:06:52.5475172+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 903,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,809,000 | 2026-07-19T03:06:52.5489591+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 903,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,869,000 | 2026-07-19T03:06:52.6575488+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 903,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,917,000 | 2026-07-19T03:06:52.7423829+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,919,000 | 2026-07-19T03:06:52.7428778+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,923,000 | 2026-07-19T03:06:52.7616985+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,927,000 | 2026-07-19T03:06:52.7634127+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,937,000 | 2026-07-19T03:06:52.7733296+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,943,000 | 2026-07-19T03:06:52.7773037+00:00 | 147.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,947,000 | 2026-07-19T03:06:52.7817069+00:00 | 148.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,949,000 | 2026-07-19T03:06:52.7823606+00:00 | 142.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,957,000 | 2026-07-19T03:06:52.8178744+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,959,000 | 2026-07-19T03:06:52.8187687+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,963,000 | 2026-07-19T03:06:52.8217288+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,044,000 | 2026-07-19T03:06:52.9544061+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,046,000 | 2026-07-19T03:06:52.9564444+00:00 | 125.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,050,000 | 2026-07-19T03:06:52.9720907+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,056,000 | 2026-07-19T03:06:52.9776015+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,064,000 | 2026-07-19T03:06:52.9838251+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,387,000 | 2026-07-19T03:06:53.3569049+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,389,000 | 2026-07-19T03:06:53.3575629+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,393,000 | 2026-07-19T03:06:53.3595007+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,403,000 | 2026-07-19T03:06:53.3698491+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,407,000 | 2026-07-19T03:06:53.3768247+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,514,000 | 2026-07-19T03:06:53.5611807+00:00 | 125.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,516,000 | 2026-07-19T03:06:53.5621908+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,520,000 | 2026-07-19T03:06:53.5653132+00:00 | 120.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,530,000 | 2026-07-19T03:06:53.5734263+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,534,000 | 2026-07-19T03:06:53.5754231+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,536,000 | 2026-07-19T03:06:53.5763556+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,540,000 | 2026-07-19T03:06:53.5782612+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,544,000 | 2026-07-19T03:06:53.580166+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,546,000 | 2026-07-19T03:06:53.5808291+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,554,000 | 2026-07-19T03:06:53.5838708+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,556,000 | 2026-07-19T03:06:53.5851445+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 763,649 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,551,000 | 2026-07-19T03:06:54.5728957+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 986,585 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,552,000 | 2026-07-19T03:06:54.5733302+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 986,585 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,555,000 | 2026-07-19T03:06:54.574672+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 986,585 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,561,000 | 2026-07-19T03:06:54.5777435+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 986,585 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,562,000 | 2026-07-19T03:06:54.5782735+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 986,585 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,568,000 | 2026-07-19T03:06:54.5816313+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 986,585 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,726,000 | 2026-07-19T03:06:54.8270286+00:00 | 127.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,730,000 | 2026-07-19T03:06:54.8305372+00:00 | 146.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,734,000 | 2026-07-19T03:06:54.8368995+00:00 | 140.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,736,000 | 2026-07-19T03:06:54.8379475+00:00 | 139.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,744,000 | 2026-07-19T03:06:54.8438414+00:00 | 143.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,746,000 | 2026-07-19T03:06:54.8446858+00:00 | 142.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,750,000 | 2026-07-19T03:06:54.8526326+00:00 | 135.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,754,000 | 2026-07-19T03:06:54.8548965+00:00 | 132.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,756,000 | 2026-07-19T03:06:54.8579557+00:00 | 129.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,760,000 | 2026-07-19T03:06:54.8630229+00:00 | 126.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,766,000 | 2026-07-19T03:06:54.8760665+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,770,000 | 2026-07-19T03:06:54.8781956+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,774,000 | 2026-07-19T03:06:54.8801409+00:00 | 142.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 827,818 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,616,000 | 2026-07-19T03:06:57.013347+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,624,000 | 2026-07-19T03:06:57.0190243+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,626,000 | 2026-07-19T03:06:57.0203766+00:00 | 127.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,634,000 | 2026-07-19T03:06:57.024337+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,636,000 | 2026-07-19T03:06:57.0254161+00:00 | 122.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,644,000 | 2026-07-19T03:06:57.0308312+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,646,000 | 2026-07-19T03:06:57.0318378+00:00 | 135.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,650,000 | 2026-07-19T03:06:57.034576+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,654,000 | 2026-07-19T03:06:57.0364187+00:00 | 129.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,656,000 | 2026-07-19T03:06:57.0376181+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,660,000 | 2026-07-19T03:06:57.0405316+00:00 | 133.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,664,000 | 2026-07-19T03:06:57.0443383+00:00 | 138.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,096,000 | 2026-07-19T03:06:57.5339557+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,099,000 | 2026-07-19T03:06:57.537425+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,103,000 | 2026-07-19T03:06:57.5391466+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,104,000 | 2026-07-19T03:06:57.5397874+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 915,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,287,000 | 2026-07-19T03:06:59.6081261+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,289,000 | 2026-07-19T03:06:59.6089287+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,293,000 | 2026-07-19T03:06:59.6111884+00:00 | 130.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,297,000 | 2026-07-19T03:06:59.6131815+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,299,000 | 2026-07-19T03:06:59.6164549+00:00 | 125.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,303,000 | 2026-07-19T03:06:59.6187548+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,307,000 | 2026-07-19T03:06:59.6258951+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,309,000 | 2026-07-19T03:06:59.6272293+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,313,000 | 2026-07-19T03:06:59.6313187+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,317,000 | 2026-07-19T03:06:59.6336007+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,319,000 | 2026-07-19T03:06:59.6343664+00:00 | 113.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,025,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,465,000 | 2026-07-19T03:07:02.5646907+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,063,074 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,468,000 | 2026-07-19T03:07:02.566559+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,063,074 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,471,000 | 2026-07-19T03:07:02.5676813+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,063,074 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,472,000 | 2026-07-19T03:07:02.568258+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,063,074 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,939,000 | 2026-07-19T03:07:03.1156466+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 920,775 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,404,000 | 2026-07-19T03:07:03.59191+00:00 | 116.2ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 920,775 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,406,000 | 2026-07-19T03:07:03.5925212+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 920,775 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,414,000 | 2026-07-19T03:07:03.5995414+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 920,775 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,416,000 | 2026-07-19T03:07:03.6004573+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 920,775 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,535,000 | 2026-07-19T03:07:05.5596095+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,538,000 | 2026-07-19T03:07:05.5609825+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,541,000 | 2026-07-19T03:07:05.5625045+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,542,000 | 2026-07-19T03:07:05.5630478+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,545,000 | 2026-07-19T03:07:05.5647584+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,548,000 | 2026-07-19T03:07:05.5662463+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,551,000 | 2026-07-19T03:07:05.5679018+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 18,552,000 | 2026-07-19T03:07:05.5683811+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,099,882 msg/s | Gen2 +0 / pause +2.5ms |
| Dekaf | 20,201,000 | 2026-07-19T03:07:07.0627382+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,061,660 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,202,000 | 2026-07-19T03:07:07.0630483+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,061,660 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,211,000 | 2026-07-19T03:07:07.0676097+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,061,660 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,212,000 | 2026-07-19T03:07:07.068197+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,061,660 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,293,000 | 2026-07-19T03:07:08.0647728+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 1,128,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,299,000 | 2026-07-19T03:07:08.068027+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 23.1s / 1,128,005 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,892,000 | 2026-07-19T03:10:24.3888143+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,895,000 | 2026-07-19T03:10:24.3898475+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,898,000 | 2026-07-19T03:10:24.3912079+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,891,000 | 2026-07-19T03:10:24.3914851+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,901,000 | 2026-07-19T03:10:24.392116+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,902,000 | 2026-07-19T03:10:24.3925184+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,905,000 | 2026-07-19T03:10:24.3938645+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,908,000 | 2026-07-19T03:10:24.3951885+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,911,000 | 2026-07-19T03:10:24.3967993+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,918,000 | 2026-07-19T03:10:24.3996797+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,921,000 | 2026-07-19T03:10:24.4012612+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 2:capacity/started, 2:capacity/succeeded | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,922,000 | 2026-07-19T03:10:24.4015501+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 2:capacity/started, 2:capacity/succeeded | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 258,935,000 | 2026-07-19T03:10:24.4066716+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 2:capacity/started, 2:capacity/succeeded | - | 219.2s / 1,025,753 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,344,000 | 2026-07-19T03:12:13.077575+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 328.3s / 1,201,078 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 404,901,000 | 2026-07-19T03:12:25.0859901+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 340.3s / 1,192,558 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 404,902,000 | 2026-07-19T03:12:25.0864466+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 340.3s / 1,192,558 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 288,000 | 2026-07-19T03:36:59.5951909+00:00 | 100.7ms | GC pause | - | - | 1.0s / 621,546 msg/s | Gen2 +1 / pause +11.3ms |
| Dekaf (3conn) | 877,000 | 2026-07-19T03:37:00.337756+00:00 | 134.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 785,869 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 887,000 | 2026-07-19T03:37:00.3599947+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 785,869 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 897,000 | 2026-07-19T03:37:00.3708576+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 785,869 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 907,000 | 2026-07-19T03:37:00.3777182+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 785,869 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,169,000 | 2026-07-19T03:37:00.8167322+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 785,869 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,261,000 | 2026-07-19T03:37:05.3699436+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,262,000 | 2026-07-19T03:37:05.3704248+00:00 | 121.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,271,000 | 2026-07-19T03:37:05.3812715+00:00 | 136.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,272,000 | 2026-07-19T03:37:05.3815639+00:00 | 136.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,281,000 | 2026-07-19T03:37:05.4181167+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,282,000 | 2026-07-19T03:37:05.4184569+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,291,000 | 2026-07-19T03:37:05.4295436+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,292,000 | 2026-07-19T03:37:05.4303236+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 998,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,877,000 | 2026-07-19T03:37:06.8397919+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,265,519 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,449,000 | 2026-07-19T03:37:07.2672046+00:00 | 144.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,453,000 | 2026-07-19T03:37:07.2705969+00:00 | 144.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,459,000 | 2026-07-19T03:37:07.2752525+00:00 | 141.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,463,000 | 2026-07-19T03:37:07.2769929+00:00 | 144.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,469,000 | 2026-07-19T03:37:07.2812537+00:00 | 145.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,473,000 | 2026-07-19T03:37:07.2830577+00:00 | 144.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,479,000 | 2026-07-19T03:37:07.2865682+00:00 | 144.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,483,000 | 2026-07-19T03:37:07.2896343+00:00 | 141.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,489,000 | 2026-07-19T03:37:07.3151426+00:00 | 116.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,490,000 | 2026-07-19T03:37:07.3157763+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,493,000 | 2026-07-19T03:37:07.3194106+00:00 | 114.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,499,000 | 2026-07-19T03:37:07.3230917+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,013,657 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,390,000 | 2026-07-19T03:37:09.85904+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 1,191,943 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 13,344,000 | 2026-07-19T03:37:11.3289187+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,299,584 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 13,346,000 | 2026-07-19T03:37:11.3300374+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,299,584 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 135,931,000 | 2026-07-19T03:38:48.3515181+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 1,098,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 135,932,000 | 2026-07-19T03:38:48.35883+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 110.1s / 1,098,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 204,983,000 | 2026-07-19T03:39:42.8465543+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 164.2s / 1,255,124 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,106,000 | 2026-07-19T03:40:07.4913673+00:00 | 216.6ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,114,000 | 2026-07-19T03:40:07.4989829+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,116,000 | 2026-07-19T03:40:07.5025802+00:00 | 211.7ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,117,000 | 2026-07-19T03:40:07.5029778+00:00 | 211.3ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,124,000 | 2026-07-19T03:40:07.5091777+00:00 | 205.1ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,126,000 | 2026-07-19T03:40:07.5115926+00:00 | 202.7ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,127,000 | 2026-07-19T03:40:07.5123788+00:00 | 201.9ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,134,000 | 2026-07-19T03:40:07.5178618+00:00 | 196.4ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,136,000 | 2026-07-19T03:40:07.519077+00:00 | 195.2ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,137,000 | 2026-07-19T03:40:07.5197227+00:00 | 194.6ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,144,000 | 2026-07-19T03:40:07.5233264+00:00 | 191.0ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,146,000 | 2026-07-19T03:40:07.5241329+00:00 | 190.2ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,147,000 | 2026-07-19T03:40:07.5243535+00:00 | 194.2ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,154,000 | 2026-07-19T03:40:07.5298974+00:00 | 191.8ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,156,000 | 2026-07-19T03:40:07.5313966+00:00 | 190.3ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 237,157,000 | 2026-07-19T03:40:07.5316964+00:00 | 186.9ms | broker/backlog (no scale or GC event) | - | - | 189.2s / 1,048,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 240,264,000 | 2026-07-19T03:40:10.1609077+00:00 | 219.5ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,266,000 | 2026-07-19T03:40:10.1628421+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,274,000 | 2026-07-19T03:40:10.1688826+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,276,000 | 2026-07-19T03:40:10.1709641+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,277,000 | 2026-07-19T03:40:10.1713607+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,284,000 | 2026-07-19T03:40:10.1741623+00:00 | 212.7ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,286,000 | 2026-07-19T03:40:10.1746856+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,287,000 | 2026-07-19T03:40:10.1754123+00:00 | 213.2ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,294,000 | 2026-07-19T03:40:10.1821061+00:00 | 204.8ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,296,000 | 2026-07-19T03:40:10.1828134+00:00 | 204.1ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,297,000 | 2026-07-19T03:40:10.183265+00:00 | 206.3ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,304,000 | 2026-07-19T03:40:10.1860797+00:00 | 200.8ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,306,000 | 2026-07-19T03:40:10.1873713+00:00 | 201.3ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,307,000 | 2026-07-19T03:40:10.1876771+00:00 | 205.2ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 240,314,000 | 2026-07-19T03:40:10.1905422+00:00 | 200.0ms | broker/backlog (no scale or GC event) | - | - | 192.2s / 1,263,716 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 301,897,000 | 2026-07-19T03:40:57.7954097+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 239.3s / 1,223,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 301,907,000 | 2026-07-19T03:40:57.8036278+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 239.3s / 1,223,831 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 470,738,000 | 2026-07-19T03:43:09.3953345+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 371.4s / 1,350,261 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 549,341,000 | 2026-07-19T03:44:10.8312953+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 432.4s / 1,057,218 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 549,342,000 | 2026-07-19T03:44:10.8316966+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 432.4s / 1,057,218 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 560,857,000 | 2026-07-19T03:44:20.3009308+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 441.4s / 1,217,583 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 560,867,000 | 2026-07-19T03:44:20.3068535+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 441.4s / 1,217,583 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 896,182,000 | 2026-07-19T03:48:44.3324227+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 705.7s / 1,052,765 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*53 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.59x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.33x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,466,259 | 1,457,464–1,475,108 | 1.02 | 1.40x |
| Confluent | 2 | 1,049,239 | 1,030,827–1,067,980 | 1.61 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 1.02 | 1047.90 | 1,451,509 | 1,475,108 | +1.1% | +0.21% | 1384.27 | 1,451,509 | 0 | 1.49 |
| Dekaf (dekaf-first) | 1.01 | 1033.27 | 1,445,401 | 1,457,464 | +5.0% | +0.60% | 1378.44 | 1,445,401 | 0 | 1.46 |
| Confluent (dekaf-first) | 1.56 | - | 1,084,083 | 1,067,980 | +6.4% | +0.57% | 1033.86 | 1,084,083 | 0 | 1.69 |
| Confluent (confluent-first) | 1.67 | - | 1,024,065 | 1,030,827 | +1.4% | +0.08% | 976.62 | 1,024,065 | 0 | 1.71 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,275,432 | 1417.13 | 1018.21 KB |
| Dekaf | 1 | 1,271,362 | 1412.61 | 1017.16 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:49.4171189+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 325,833 msg/s |
| Dekaf | 2026-07-19T03:07:07.4209789+00:00 | 1 | 16.0 MiB / 15.5 MiB | 1468.5 MB/s | 0/0 | 14,111 | 18.0s / 1,392,469 msg/s |
| Dekaf | 2026-07-19T03:07:25.4247863+00:00 | 1 | 16.0 MiB / 14.0 MiB | 1567.0 MB/s | 0/0 | 34,202 | 36.0s / 1,421,351 msg/s |
| Dekaf | 2026-07-19T03:07:44.4280003+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1567.0 MB/s | 1/0 | 61,771 | 55.0s / 1,486,342 msg/s |
| Dekaf | 2026-07-19T03:08:02.433646+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1603.0 MB/s | 1/0 | 93,964 | 73.0s / 1,378,135 msg/s |
| Dekaf | 2026-07-19T03:08:20.4379108+00:00 | 1 | 14.0 MiB / 13.3 MiB | 1603.0 MB/s | 1/1 | 125,883 | 91.0s / 1,447,245 msg/s |
| Dekaf | 2026-07-19T03:08:38.4417695+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1617.8 MB/s | 1/1 | 158,656 | 109.0s / 1,480,649 msg/s |
| Dekaf | 2026-07-19T03:08:56.446494+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1651.7 MB/s | 1/1 | 194,071 | 127.0s / 1,382,976 msg/s |
| Dekaf | 2026-07-19T03:09:14.4524444+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1651.7 MB/s | 1/1 | 228,586 | 145.1s / 1,503,803 msg/s |
| Dekaf | 2026-07-19T03:09:33.4588872+00:00 | 1 | 15.0 MiB / 13.7 MiB | 1651.7 MB/s | 1/1 | 263,498 | 164.1s / 1,437,617 msg/s |
| Dekaf | 2026-07-19T03:09:51.4668061+00:00 | 1 | 15.0 MiB / 14.3 MiB | 1651.7 MB/s | 2/1 | 292,936 | 182.1s / 1,466,021 msg/s |
| Dekaf | 2026-07-19T03:10:09.4719581+00:00 | 1 | 15.0 MiB / 13.9 MiB | 1651.7 MB/s | 2/1 | 318,357 | 200.1s / 1,452,661 msg/s |
| Dekaf | 2026-07-19T03:10:27.4761238+00:00 | 1 | 16.0 MiB / 9.2 MiB | 1651.7 MB/s | 3/1 | 339,653 | 218.1s / 1,483,000 msg/s |
| Dekaf | 2026-07-19T03:10:45.4862178+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1651.7 MB/s | 3/1 | 360,252 | 236.1s / 1,470,145 msg/s |
| Dekaf | 2026-07-19T03:11:03.492428+00:00 | 1 | 18.0 MiB / 16.7 MiB | 1651.7 MB/s | 3/1 | 376,513 | 254.1s / 1,364,437 msg/s |
| Dekaf | 2026-07-19T03:11:22.499855+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1651.7 MB/s | 4/1 | 392,884 | 273.1s / 1,368,432 msg/s |
| Dekaf | 2026-07-19T03:11:40.5078509+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1651.7 MB/s | 4/1 | 409,331 | 291.1s / 1,466,901 msg/s |
| Dekaf | 2026-07-19T03:11:58.5136109+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1651.7 MB/s | 4/2 | 426,210 | 309.1s / 1,402,368 msg/s |
| Dekaf | 2026-07-19T03:12:16.5209796+00:00 | 1 | 18.0 MiB / 16.4 MiB | 1651.7 MB/s | 4/2 | 442,008 | 327.1s / 1,470,430 msg/s |
| Dekaf | 2026-07-19T03:12:34.5309211+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1651.7 MB/s | 4/2 | 458,295 | 345.1s / 1,416,646 msg/s |
| Dekaf | 2026-07-19T03:12:52.5357673+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1651.7 MB/s | 4/2 | 475,464 | 363.1s / 1,376,541 msg/s |
| Dekaf | 2026-07-19T03:13:11.5435247+00:00 | 1 | 15.0 MiB / 13.7 MiB | 1651.7 MB/s | 5/2 | 493,949 | 382.1s / 1,373,726 msg/s |
| Dekaf | 2026-07-19T03:13:29.549147+00:00 | 1 | 15.0 MiB / 13.4 MiB | 1651.7 MB/s | 5/2 | 518,509 | 400.1s / 1,219,915 msg/s |
| Dekaf | 2026-07-19T03:13:47.5518259+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1651.7 MB/s | 5/2 | 544,274 | 418.1s / 1,399,602 msg/s |
| Dekaf | 2026-07-19T03:14:05.5599684+00:00 | 1 | 13.0 MiB / 10.8 MiB | 1651.7 MB/s | 6/2 | 573,894 | 436.2s / 1,456,792 msg/s |
| Dekaf | 2026-07-19T03:14:23.5657801+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1651.7 MB/s | 6/2 | 603,373 | 454.2s / 1,319,416 msg/s |
| Dekaf | 2026-07-19T03:14:41.572563+00:00 | 1 | 13.0 MiB / 11.9 MiB | 1651.7 MB/s | 6/3 | 639,519 | 472.2s / 1,461,743 msg/s |
| Dekaf | 2026-07-19T03:15:00.573858+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1651.7 MB/s | 6/3 | 680,172 | 491.2s / 1,507,445 msg/s |
| Dekaf | 2026-07-19T03:15:18.581004+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1659.5 MB/s | 6/3 | 721,590 | 509.2s / 1,561,076 msg/s |
| Dekaf | 2026-07-19T03:15:36.5859173+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1680.7 MB/s | 6/3 | 764,900 | 527.2s / 1,591,118 msg/s |
| Dekaf | 2026-07-19T03:15:54.593096+00:00 | 1 | 14.0 MiB / 12.4 MiB | 1680.7 MB/s | 7/3 | 805,909 | 545.2s / 1,527,584 msg/s |
| Dekaf | 2026-07-19T03:16:12.5957188+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1734.6 MB/s | 7/3 | 843,422 | 563.2s / 1,574,903 msg/s |
| Dekaf | 2026-07-19T03:16:30.6021677+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1734.6 MB/s | 7/3 | 875,926 | 581.2s / 1,530,547 msg/s |
| Dekaf | 2026-07-19T03:16:49.6129599+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1734.6 MB/s | 7/4 | 911,845 | 600.2s / 1,462,960 msg/s |
| Dekaf | 2026-07-19T03:17:07.6179783+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1734.6 MB/s | 7/4 | 948,678 | 618.2s / 1,561,535 msg/s |
| Dekaf | 2026-07-19T03:17:25.6267601+00:00 | 1 | 14.0 MiB / 11.7 MiB | 1734.6 MB/s | 7/4 | 983,471 | 636.2s / 1,483,225 msg/s |
| Dekaf | 2026-07-19T03:17:43.6319542+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1734.6 MB/s | 7/4 | 1,023,218 | 654.2s / 1,505,368 msg/s |
| Dekaf | 2026-07-19T03:18:01.6358287+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1734.6 MB/s | 8/4 | 1,052,091 | 672.2s / 1,435,610 msg/s |
| Dekaf | 2026-07-19T03:18:20.6470296+00:00 | 1 | 10.0 MiB / 4.2 MiB | 1734.6 MB/s | 8/4 | 1,085,529 | 691.2s / 1,355,374 msg/s |
| Dekaf | 2026-07-19T03:18:38.6491113+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1734.6 MB/s | 8/5 | 1,104,596 | 709.2s / 1,468,596 msg/s |
| Dekaf | 2026-07-19T03:18:56.654811+00:00 | 1 | 12.0 MiB / 4.9 MiB | 1734.6 MB/s | 8/5 | 1,133,374 | 727.3s / 1,402,392 msg/s |
| Dekaf | 2026-07-19T03:19:14.6583156+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1734.6 MB/s | 8/5 | 1,162,859 | 745.3s / 1,394,247 msg/s |
| Dekaf | 2026-07-19T03:19:32.6600311+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1734.6 MB/s | 8/5 | 1,205,535 | 763.3s / 1,477,119 msg/s |
| Dekaf | 2026-07-19T03:19:50.6686926+00:00 | 1 | 12.0 MiB / 5.4 MiB | 1734.6 MB/s | 8/6 | 1,245,610 | 781.3s / 1,517,446 msg/s |
| Dekaf | 2026-07-19T03:20:09.6708299+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1734.6 MB/s | 8/6 | 1,290,557 | 800.3s / 1,421,416 msg/s |
| Dekaf | 2026-07-19T03:20:27.6793339+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1734.6 MB/s | 8/6 | 1,334,524 | 818.3s / 1,518,336 msg/s |
| Dekaf | 2026-07-19T03:20:45.6856326+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1734.6 MB/s | 8/6 | 1,377,300 | 836.3s / 1,525,354 msg/s |
| Dekaf | 2026-07-19T03:21:03.6948008+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1734.6 MB/s | 8/6 | 1,424,015 | 854.3s / 1,451,228 msg/s |
| Dekaf | 2026-07-19T03:21:21.698249+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1734.6 MB/s | 8/6 | 1,469,698 | 872.3s / 1,513,971 msg/s |
| Dekaf | 2026-07-19T03:21:39.7030636+00:00 | 1 | 12.0 MiB / 9.4 MiB | 1734.6 MB/s | 8/6 | 1,514,006 | 890.3s / 1,527,124 msg/s |
| Dekaf | 2026-07-19T03:51:59.7950315+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1613.5 MB/s | 0/0 | 9,329 | 9.0s / 1,524,965 msg/s |
| Dekaf | 2026-07-19T03:52:17.8051175+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1613.5 MB/s | 0/0 | 33,551 | 27.0s / 1,525,672 msg/s |
| Dekaf | 2026-07-19T03:52:35.8118504+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1652.9 MB/s | 0/0 | 58,981 | 45.0s / 1,514,361 msg/s |
| Dekaf | 2026-07-19T03:52:53.821757+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1652.9 MB/s | 0/1 | 78,694 | 63.0s / 1,537,461 msg/s |
| Dekaf | 2026-07-19T03:53:11.8309034+00:00 | 1 | 16.0 MiB / 13.4 MiB | 1652.9 MB/s | 0/1 | 90,903 | 81.0s / 1,488,278 msg/s |
| Dekaf | 2026-07-19T03:53:29.8378427+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1652.9 MB/s | 0/1 | 105,850 | 99.0s / 1,437,927 msg/s |
| Dekaf | 2026-07-19T03:53:48.8441272+00:00 | 1 | 18.0 MiB / 16.7 MiB | 1652.9 MB/s | 0/1 | 122,816 | 118.1s / 1,431,520 msg/s |
| Dekaf | 2026-07-19T03:54:06.8528383+00:00 | 1 | 16.0 MiB / 13.9 MiB | 1652.9 MB/s | 0/2 | 141,375 | 136.1s / 1,338,256 msg/s |
| Dekaf | 2026-07-19T03:54:24.8577002+00:00 | 1 | 16.0 MiB / 15.3 MiB | 1653.8 MB/s | 0/2 | 165,490 | 154.1s / 1,468,964 msg/s |
| Dekaf | 2026-07-19T03:54:42.8616879+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1653.8 MB/s | 0/2 | 188,584 | 172.1s / 1,554,413 msg/s |
| Dekaf | 2026-07-19T03:55:00.8688416+00:00 | 1 | 16.0 MiB / 13.7 MiB | 1653.8 MB/s | 0/2 | 214,680 | 190.1s / 1,525,719 msg/s |
| Dekaf | 2026-07-19T03:55:18.8759332+00:00 | 1 | 16.0 MiB / 15.2 MiB | 1654.3 MB/s | 0/2 | 237,143 | 208.1s / 1,480,402 msg/s |
| Dekaf | 2026-07-19T03:55:37.8821425+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1689.0 MB/s | 0/2 | 266,631 | 227.1s / 1,561,690 msg/s |
| Dekaf | 2026-07-19T03:55:55.8932086+00:00 | 1 | 16.0 MiB / 12.4 MiB | 1689.0 MB/s | 0/2 | 294,465 | 245.1s / 1,512,907 msg/s |
| Dekaf | 2026-07-19T03:56:13.9057737+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/2 | 327,635 | 263.1s / 1,549,327 msg/s |
| Dekaf | 2026-07-19T03:56:31.9086109+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1689.0 MB/s | 1/2 | 348,540 | 281.1s / 1,464,647 msg/s |
| Dekaf | 2026-07-19T03:56:49.9176826+00:00 | 1 | 12.0 MiB / 2.0 MiB | 1689.0 MB/s | 1/2 | 371,946 | 299.1s / 1,401,952 msg/s |
| Dekaf | 2026-07-19T03:57:08.9205101+00:00 | 1 | 14.0 MiB / 11.6 MiB | 1689.0 MB/s | 1/3 | 395,557 | 318.1s / 1,282,007 msg/s |
| Dekaf | 2026-07-19T03:57:26.9269332+00:00 | 1 | 14.0 MiB / 2.0 MiB | 1689.0 MB/s | 1/3 | 425,955 | 336.1s / 981,216 msg/s |
| Dekaf | 2026-07-19T03:57:44.9369654+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1689.0 MB/s | 1/3 | 454,854 | 354.1s / 1,536,210 msg/s |
| Dekaf | 2026-07-19T03:58:02.945693+00:00 | 1 | 15.0 MiB / 13.2 MiB | 1689.0 MB/s | 1/3 | 488,268 | 372.1s / 1,425,750 msg/s |
| Dekaf | 2026-07-19T03:58:20.9548172+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1689.0 MB/s | 1/4 | 523,525 | 390.1s / 1,357,877 msg/s |
| Dekaf | 2026-07-19T03:58:38.9633517+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1689.0 MB/s | 1/4 | 555,834 | 408.1s / 1,262,647 msg/s |
| Dekaf | 2026-07-19T03:58:57.9708495+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/4 | 587,861 | 427.2s / 1,268,592 msg/s |
| Dekaf | 2026-07-19T03:59:15.9813013+00:00 | 1 | 14.0 MiB / 8.1 MiB | 1689.0 MB/s | 1/4 | 614,961 | 445.2s / 1,475,108 msg/s |
| Dekaf | 2026-07-19T03:59:33.9885338+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1689.0 MB/s | 1/4 | 648,789 | 463.2s / 1,493,562 msg/s |
| Dekaf | 2026-07-19T03:59:51.9962196+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1689.0 MB/s | 1/4 | 687,034 | 481.2s / 1,309,226 msg/s |
| Dekaf | 2026-07-19T04:00:09.9994879+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1689.0 MB/s | 1/4 | 719,459 | 499.2s / 1,460,580 msg/s |
| Dekaf | 2026-07-19T04:00:28.0013926+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/5 | 750,475 | 517.2s / 1,533,947 msg/s |
| Dekaf | 2026-07-19T04:00:47.0119444+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1689.0 MB/s | 1/5 | 783,956 | 536.2s / 1,525,001 msg/s |
| Dekaf | 2026-07-19T04:01:05.0167614+00:00 | 1 | 14.0 MiB / 12.4 MiB | 1689.0 MB/s | 1/5 | 818,364 | 554.2s / 1,533,305 msg/s |
| Dekaf | 2026-07-19T04:01:23.0209724+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1689.0 MB/s | 1/5 | 854,388 | 572.2s / 1,548,387 msg/s |
| Dekaf | 2026-07-19T04:01:41.0271437+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1689.0 MB/s | 1/5 | 891,322 | 590.2s / 1,513,383 msg/s |
| Dekaf | 2026-07-19T04:01:59.0381736+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/5 | 927,899 | 608.2s / 1,464,969 msg/s |
| Dekaf | 2026-07-19T04:02:17.0433676+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/5 | 965,989 | 626.2s / 1,549,974 msg/s |
| Dekaf | 2026-07-19T04:02:36.0496897+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/5 | 999,451 | 645.2s / 1,511,663 msg/s |
| Dekaf | 2026-07-19T04:02:54.0582669+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1689.0 MB/s | 1/5 | 1,037,709 | 663.2s / 1,558,421 msg/s |
| Dekaf | 2026-07-19T04:03:12.0608724+00:00 | 1 | 14.0 MiB / 2.4 MiB | 1689.0 MB/s | 1/5 | 1,075,408 | 681.3s / 1,482,096 msg/s |
| Dekaf | 2026-07-19T04:03:30.0694267+00:00 | 1 | 14.0 MiB / 12.7 MiB | 1689.0 MB/s | 1/5 | 1,107,227 | 699.3s / 1,517,838 msg/s |
| Dekaf | 2026-07-19T04:03:48.0765541+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/5 | 1,141,872 | 717.3s / 1,489,341 msg/s |
| Dekaf | 2026-07-19T04:04:06.084982+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1689.0 MB/s | 1/5 | 1,182,728 | 735.3s / 1,466,643 msg/s |
| Dekaf | 2026-07-19T04:04:25.0911086+00:00 | 1 | 15.0 MiB / 8.3 MiB | 1689.0 MB/s | 1/5 | 1,218,490 | 754.3s / 1,389,317 msg/s |
| Dekaf | 2026-07-19T04:04:43.1024193+00:00 | 1 | 14.0 MiB / 12.2 MiB | 1689.0 MB/s | 1/6 | 1,253,898 | 772.3s / 1,510,589 msg/s |
| Dekaf | 2026-07-19T04:05:01.1048802+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/6 | 1,287,353 | 790.3s / 1,528,493 msg/s |
| Dekaf | 2026-07-19T04:05:19.1108635+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/6 | 1,324,528 | 808.3s / 1,475,987 msg/s |
| Dekaf | 2026-07-19T04:05:37.1180516+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1689.0 MB/s | 1/6 | 1,357,757 | 826.3s / 1,523,400 msg/s |
| Dekaf | 2026-07-19T04:05:55.1249564+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1689.0 MB/s | 1/6 | 1,391,776 | 844.3s / 1,360,689 msg/s |
| Dekaf | 2026-07-19T04:06:14.1365073+00:00 | 1 | 14.0 MiB / 12.1 MiB | 1689.0 MB/s | 1/6 | 1,428,403 | 863.3s / 1,508,455 msg/s |
| Dekaf | 2026-07-19T04:06:32.1459189+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1689.0 MB/s | 1/6 | 1,463,077 | 881.3s / 1,451,906 msg/s |
| Dekaf | 2026-07-19T04:06:50.1527338+00:00 | 1 | 14.0 MiB / 12.9 MiB | 1689.0 MB/s | 1/6 | 1,497,881 | 899.3s / 1,467,153 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-19T03:07:19.5361368+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 16.0 MiB |
| Dekaf | 2026-07-19T03:07:34.5505178+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 13.3 MiB |
| Dekaf | 2026-07-19T03:08:04.574889+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-19T03:08:19.5903393+00:00 | 1 | capacity | failed | 15,015ms | 14.0 MiB / 11.5 MiB |
| Dekaf | 2026-07-19T03:09:19.6406957+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:09:34.6525495+00:00 | 1 | capacity | succeeded | 15,011ms | 15.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-19T03:10:04.6860314+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:10:19.7049624+00:00 | 1 | capacity | succeeded | 15,018ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:10:49.7445404+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:11:04.7732786+00:00 | 1 | capacity | succeeded | 15,028ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:11:34.8388692+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-19T03:11:49.8650531+00:00 | 1 | capacity | failed | 15,026ms | 18.0 MiB / 14.8 MiB |
| Dekaf | 2026-07-19T03:12:49.9567337+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:13:04.9806282+00:00 | 1 | capacity | succeeded | 15,023ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:13:35.0150287+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-19T03:13:50.0269515+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T03:14:20.0549559+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:14:35.0694958+00:00 | 1 | capacity | failed | 15,014ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-19T03:15:35.1208339+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T03:15:50.1366459+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-19T03:16:20.1618832+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:16:35.1732578+00:00 | 1 | capacity | failed | 15,010ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:17:35.2282214+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:17:50.2383147+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-19T03:18:20.2607041+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-19T03:18:35.2844503+00:00 | 1 | capacity | failed | 15,023ms | 12.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-19T03:19:35.3382561+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:19:50.3543807+00:00 | 1 | capacity | failed | 15,016ms | 12.0 MiB / 11.8 MiB |
| Dekaf | 2026-07-19T03:52:20.9081678+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-19T03:52:35.9270902+00:00 | 1 | capacity | failed | 15,018ms | 16.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-19T03:53:35.9855175+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.0 MiB |
| Dekaf | 2026-07-19T03:53:51.0023652+00:00 | 1 | capacity | failed | 15,016ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:55:51.1302364+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:56:06.1442236+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-19T03:56:36.1735892+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:56:51.1868245+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:57:51.2429376+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:58:06.255223+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 13.4 MiB |
| Dekaf | 2026-07-19T04:00:06.3833378+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.5 MiB |
| Dekaf | 2026-07-19T04:00:21.3949035+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T04:04:21.6011836+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-19T04:04:36.6136092+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 14.1 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,637 |
| Dekaf | 1 | 0.002–0.004ms | 1,775 |
| Dekaf | 1 | 0.004–0.008ms | 4,685 |
| Dekaf | 1 | 0.008–0.016ms | 27,694 |
| Dekaf | 1 | 0.016–0.032ms | 48,357 |
| Dekaf | 1 | 0.032–0.064ms | 40,854 |
| Dekaf | 1 | 0.064–0.128ms | 67,352 |
| Dekaf | 1 | 0.128–0.256ms | 165,288 |
| Dekaf | 1 | 0.256–0.512ms | 177,928 |
| Dekaf | 1 | 0.512–1.024ms | 64,550 |
| Dekaf | 1 | 1.024–2.048ms | 16,435 |
| Dekaf | 1 | 2.048–4.096ms | 4,144 |
| Dekaf | 1 | 4.096–8.192ms | 1,158 |
| Dekaf | 1 | 8.192–16.384ms | 141 |
| Dekaf | 1 | 16.384–32.768ms | 5 |
| Dekaf | 1 | 0.001–0.002ms | 1,617 |
| Dekaf | 1 | 0.002–0.004ms | 1,732 |
| Dekaf | 1 | 0.004–0.008ms | 4,453 |
| Dekaf | 1 | 0.008–0.016ms | 23,340 |
| Dekaf | 1 | 0.016–0.032ms | 40,080 |
| Dekaf | 1 | 0.032–0.064ms | 39,015 |
| Dekaf | 1 | 0.064–0.128ms | 65,063 |
| Dekaf | 1 | 0.128–0.256ms | 165,116 |
| Dekaf | 1 | 0.256–0.512ms | 192,290 |
| Dekaf | 1 | 0.512–1.024ms | 65,702 |
| Dekaf | 1 | 1.024–2.048ms | 19,210 |
| Dekaf | 1 | 2.048–4.096ms | 4,191 |
| Dekaf | 1 | 4.096–8.192ms | 1,043 |
| Dekaf | 1 | 8.192–16.384ms | 125 |
| Dekaf | 1 | 16.384–32.768ms | 8 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 131.072–262.144ms | 1 |

## Delivery Latency Outliers - Producer (Acks All)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 560,434,000 | 2026-07-19T03:13:29.1754403+00:00 | 177.8ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,437,000 | 2026-07-19T03:13:29.1767662+00:00 | 180.0ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,436,000 | 2026-07-19T03:13:29.1772476+00:00 | 176.3ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,438,000 | 2026-07-19T03:13:29.1774708+00:00 | 188.4ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,439,000 | 2026-07-19T03:13:29.1779869+00:00 | 180.9ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,440,000 | 2026-07-19T03:13:29.178752+00:00 | 186.8ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,441,000 | 2026-07-19T03:13:29.1791304+00:00 | 187.3ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,442,000 | 2026-07-19T03:13:29.1810191+00:00 | 185.4ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,443,000 | 2026-07-19T03:13:29.1813931+00:00 | 195.5ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,444,000 | 2026-07-19T03:13:29.1820504+00:00 | 193.5ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,445,000 | 2026-07-19T03:13:29.1824908+00:00 | 194.4ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,446,000 | 2026-07-19T03:13:29.1832436+00:00 | 193.7ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,447,000 | 2026-07-19T03:13:29.1836323+00:00 | 193.3ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,448,000 | 2026-07-19T03:13:29.1846315+00:00 | 192.3ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,449,000 | 2026-07-19T03:13:29.185082+00:00 | 191.8ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 560,450,000 | 2026-07-19T03:13:29.185775+00:00 | 191.4ms | broker/backlog (no scale or GC event) | - | - | 400.1s / 1,219,915 msg/s | Gen2 +0 / pause +0.5ms |
| Confluent | 22,931,000 | 2026-07-19T03:22:10.2289534+00:00 | 102.1ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,937,000 | 2026-07-19T03:22:10.2333886+00:00 | 102.5ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,941,000 | 2026-07-19T03:22:10.2358401+00:00 | 100.2ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,947,000 | 2026-07-19T03:22:10.2393165+00:00 | 106.9ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,948,000 | 2026-07-19T03:22:10.2403287+00:00 | 105.9ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,951,000 | 2026-07-19T03:22:10.2425401+00:00 | 103.8ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,958,000 | 2026-07-19T03:22:10.2481813+00:00 | 103.1ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,961,000 | 2026-07-19T03:22:10.2520028+00:00 | 106.5ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,967,000 | 2026-07-19T03:22:10.2594244+00:00 | 101.8ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,978,000 | 2026-07-19T03:22:10.2736829+00:00 | 103.3ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,988,000 | 2026-07-19T03:22:10.2884232+00:00 | 106.3ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 22,997,000 | 2026-07-19T03:22:10.2955777+00:00 | 100.5ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,001,000 | 2026-07-19T03:22:10.2995818+00:00 | 100.7ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,007,000 | 2026-07-19T03:22:10.3043126+00:00 | 101.0ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,011,000 | 2026-07-19T03:22:10.3067475+00:00 | 103.8ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,017,000 | 2026-07-19T03:22:10.3124233+00:00 | 103.0ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,018,000 | 2026-07-19T03:22:10.3128883+00:00 | 102.6ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,027,000 | 2026-07-19T03:22:10.3188147+00:00 | 100.1ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,028,000 | 2026-07-19T03:22:10.3203264+00:00 | 104.0ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,031,000 | 2026-07-19T03:22:10.3227022+00:00 | 101.7ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,037,000 | 2026-07-19T03:22:10.326869+00:00 | 100.5ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,048,000 | 2026-07-19T03:22:10.3360089+00:00 | 101.7ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 23,051,000 | 2026-07-19T03:22:10.3391863+00:00 | 104.7ms | GC pause | - | - | 21.0s / 1,018,408 msg/s | Gen2 +0 / pause +79.6ms |
| Confluent | 335,917,000 | 2026-07-19T03:27:13.5043514+00:00 | 101.6ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,918,000 | 2026-07-19T03:27:13.5052492+00:00 | 100.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,937,000 | 2026-07-19T03:27:13.5175522+00:00 | 108.6ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,938,000 | 2026-07-19T03:27:13.5185043+00:00 | 119.3ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,940,000 | 2026-07-19T03:27:13.5199477+00:00 | 103.9ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,941,000 | 2026-07-19T03:27:13.5210236+00:00 | 116.9ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,947,000 | 2026-07-19T03:27:13.5250166+00:00 | 114.1ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,951,000 | 2026-07-19T03:27:13.5278944+00:00 | 115.4ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,953,000 | 2026-07-19T03:27:13.5297671+00:00 | 108.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,955,000 | 2026-07-19T03:27:13.5308835+00:00 | 106.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,956,000 | 2026-07-19T03:27:13.5316215+00:00 | 106.0ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,957,000 | 2026-07-19T03:27:13.5332508+00:00 | 114.9ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,958,000 | 2026-07-19T03:27:13.534481+00:00 | 113.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,959,000 | 2026-07-19T03:27:13.5359552+00:00 | 101.8ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,961,000 | 2026-07-19T03:27:13.5386986+00:00 | 116.4ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,963,000 | 2026-07-19T03:27:13.5412328+00:00 | 101.6ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,968,000 | 2026-07-19T03:27:13.5485666+00:00 | 111.2ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,970,000 | 2026-07-19T03:27:13.5505302+00:00 | 100.3ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,971,000 | 2026-07-19T03:27:13.5517724+00:00 | 108.1ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,978,000 | 2026-07-19T03:27:13.5618579+00:00 | 110.0ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,981,000 | 2026-07-19T03:27:13.56585+00:00 | 106.1ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,987,000 | 2026-07-19T03:27:13.5731547+00:00 | 104.0ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,997,000 | 2026-07-19T03:27:13.5839772+00:00 | 104.5ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 335,998,000 | 2026-07-19T03:27:13.5845825+00:00 | 103.9ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,001,000 | 2026-07-19T03:27:13.587894+00:00 | 106.5ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,008,000 | 2026-07-19T03:27:13.5943966+00:00 | 104.1ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,011,000 | 2026-07-19T03:27:13.5966409+00:00 | 102.0ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,017,000 | 2026-07-19T03:27:13.6010808+00:00 | 102.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,021,000 | 2026-07-19T03:27:13.6036889+00:00 | 110.5ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,027,000 | 2026-07-19T03:27:13.6076751+00:00 | 115.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,037,000 | 2026-07-19T03:27:13.6152303+00:00 | 119.6ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,038,000 | 2026-07-19T03:27:13.6158406+00:00 | 119.0ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,040,000 | 2026-07-19T03:27:13.6170327+00:00 | 105.8ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,041,000 | 2026-07-19T03:27:13.617959+00:00 | 116.9ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,043,000 | 2026-07-19T03:27:13.6227586+00:00 | 101.3ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,047,000 | 2026-07-19T03:27:13.627499+00:00 | 111.5ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,048,000 | 2026-07-19T03:27:13.6283844+00:00 | 110.7ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,051,000 | 2026-07-19T03:27:13.6307082+00:00 | 108.4ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,057,000 | 2026-07-19T03:27:13.6354786+00:00 | 109.0ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,067,000 | 2026-07-19T03:27:13.6536821+00:00 | 100.5ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 336,071,000 | 2026-07-19T03:27:13.6608158+00:00 | 100.3ms | GC pause | - | - | 324.2s / 1,004,486 msg/s | Gen2 +0 / pause +80.4ms |
| Confluent | 811,607,000 | 2026-07-19T03:34:22.7788546+00:00 | 101.5ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,617,000 | 2026-07-19T03:34:22.787764+00:00 | 100.4ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,647,000 | 2026-07-19T03:34:22.8163451+00:00 | 100.8ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,651,000 | 2026-07-19T03:34:22.8193143+00:00 | 100.5ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,711,000 | 2026-07-19T03:34:22.870446+00:00 | 104.9ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,718,000 | 2026-07-19T03:34:22.8746258+00:00 | 102.3ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,721,000 | 2026-07-19T03:34:22.8761455+00:00 | 104.3ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,727,000 | 2026-07-19T03:34:22.8817286+00:00 | 108.9ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,728,000 | 2026-07-19T03:34:22.8823731+00:00 | 108.3ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,731,000 | 2026-07-19T03:34:22.8842745+00:00 | 114.4ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,738,000 | 2026-07-19T03:34:22.8889976+00:00 | 112.6ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,741,000 | 2026-07-19T03:34:22.8912447+00:00 | 112.3ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,747,000 | 2026-07-19T03:34:22.8952598+00:00 | 109.6ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,751,000 | 2026-07-19T03:34:22.8983119+00:00 | 107.6ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,758,000 | 2026-07-19T03:34:22.904318+00:00 | 107.3ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,761,000 | 2026-07-19T03:34:22.9107447+00:00 | 104.7ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,837,000 | 2026-07-19T03:34:22.9848732+00:00 | 100.3ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,841,000 | 2026-07-19T03:34:22.9871277+00:00 | 103.0ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,848,000 | 2026-07-19T03:34:22.9921218+00:00 | 100.5ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,858,000 | 2026-07-19T03:34:22.9974615+00:00 | 113.2ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,867,000 | 2026-07-19T03:34:23.0080727+00:00 | 104.4ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 811,871,000 | 2026-07-19T03:34:23.0130736+00:00 | 103.5ms | GC pause | - | - | 753.5s / 1,065,024 msg/s | Gen2 +0 / pause +56.2ms |
| Confluent | 868,087,000 | 2026-07-19T03:35:17.3852967+00:00 | 100.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,118,000 | 2026-07-19T03:35:17.4083019+00:00 | 100.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,131,000 | 2026-07-19T03:35:17.4163885+00:00 | 103.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,138,000 | 2026-07-19T03:35:17.4202269+00:00 | 104.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,147,000 | 2026-07-19T03:35:17.4249281+00:00 | 109.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,151,000 | 2026-07-19T03:35:17.4269223+00:00 | 113.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,157,000 | 2026-07-19T03:35:17.4309011+00:00 | 116.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,161,000 | 2026-07-19T03:35:17.4341096+00:00 | 113.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,168,000 | 2026-07-19T03:35:17.4380021+00:00 | 116.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,171,000 | 2026-07-19T03:35:17.4405736+00:00 | 113.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,178,000 | 2026-07-19T03:35:17.4451691+00:00 | 114.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,180,000 | 2026-07-19T03:35:17.4464245+00:00 | 101.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,181,000 | 2026-07-19T03:35:17.447384+00:00 | 118.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,187,000 | 2026-07-19T03:35:17.4515781+00:00 | 118.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,188,000 | 2026-07-19T03:35:17.4522618+00:00 | 117.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,189,000 | 2026-07-19T03:35:17.4527571+00:00 | 100.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,191,000 | 2026-07-19T03:35:17.4540293+00:00 | 121.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,193,000 | 2026-07-19T03:35:17.4561278+00:00 | 100.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,194,000 | 2026-07-19T03:35:17.4573339+00:00 | 100.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,197,000 | 2026-07-19T03:35:17.4610877+00:00 | 119.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,207,000 | 2026-07-19T03:35:17.4706231+00:00 | 114.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,208,000 | 2026-07-19T03:35:17.4723493+00:00 | 118.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,210,000 | 2026-07-19T03:35:17.4735696+00:00 | 101.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,211,000 | 2026-07-19T03:35:17.4741564+00:00 | 117.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,217,000 | 2026-07-19T03:35:17.480545+00:00 | 115.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,218,000 | 2026-07-19T03:35:17.4811525+00:00 | 114.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,221,000 | 2026-07-19T03:35:17.4831935+00:00 | 119.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,223,000 | 2026-07-19T03:35:17.4843195+00:00 | 100.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,227,000 | 2026-07-19T03:35:17.4891807+00:00 | 117.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,228,000 | 2026-07-19T03:35:17.4901772+00:00 | 116.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,231,000 | 2026-07-19T03:35:17.4921146+00:00 | 119.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,233,000 | 2026-07-19T03:35:17.4938617+00:00 | 100.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,237,000 | 2026-07-19T03:35:17.4969576+00:00 | 119.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,238,000 | 2026-07-19T03:35:17.4976948+00:00 | 118.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,241,000 | 2026-07-19T03:35:17.4999653+00:00 | 116.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,248,000 | 2026-07-19T03:35:17.5052641+00:00 | 126.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,250,000 | 2026-07-19T03:35:17.507113+00:00 | 101.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,251,000 | 2026-07-19T03:35:17.5079177+00:00 | 123.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,254,000 | 2026-07-19T03:35:17.510273+00:00 | 101.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,257,000 | 2026-07-19T03:35:17.5118568+00:00 | 120.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,261,000 | 2026-07-19T03:35:17.5153246+00:00 | 126.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,263,000 | 2026-07-19T03:35:17.5171113+00:00 | 102.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,264,000 | 2026-07-19T03:35:17.5176062+00:00 | 102.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,266,000 | 2026-07-19T03:35:17.5189416+00:00 | 101.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,267,000 | 2026-07-19T03:35:17.5195894+00:00 | 123.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,268,000 | 2026-07-19T03:35:17.5203344+00:00 | 122.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,270,000 | 2026-07-19T03:35:17.5213374+00:00 | 110.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,274,000 | 2026-07-19T03:35:17.5247124+00:00 | 106.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,276,000 | 2026-07-19T03:35:17.5260126+00:00 | 105.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,277,000 | 2026-07-19T03:35:17.5268894+00:00 | 126.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,280,000 | 2026-07-19T03:35:17.5296813+00:00 | 104.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,281,000 | 2026-07-19T03:35:17.5310807+00:00 | 122.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,283,000 | 2026-07-19T03:35:17.5343573+00:00 | 100.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,288,000 | 2026-07-19T03:35:17.5431518+00:00 | 119.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,291,000 | 2026-07-19T03:35:17.547598+00:00 | 115.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,297,000 | 2026-07-19T03:35:17.5574243+00:00 | 115.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,307,000 | 2026-07-19T03:35:17.5681114+00:00 | 111.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,308,000 | 2026-07-19T03:35:17.5692943+00:00 | 110.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,311,000 | 2026-07-19T03:35:17.5713195+00:00 | 111.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,317,000 | 2026-07-19T03:35:17.5764057+00:00 | 111.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,318,000 | 2026-07-19T03:35:17.5771506+00:00 | 111.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,327,000 | 2026-07-19T03:35:17.5834558+00:00 | 113.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,328,000 | 2026-07-19T03:35:17.5839343+00:00 | 113.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,331,000 | 2026-07-19T03:35:17.5871708+00:00 | 111.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,337,000 | 2026-07-19T03:35:17.5917049+00:00 | 111.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,338,000 | 2026-07-19T03:35:17.5921647+00:00 | 111.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,341,000 | 2026-07-19T03:35:17.5945627+00:00 | 114.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,347,000 | 2026-07-19T03:35:17.599147+00:00 | 114.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,348,000 | 2026-07-19T03:35:17.5996158+00:00 | 113.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,351,000 | 2026-07-19T03:35:17.6013713+00:00 | 112.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,357,000 | 2026-07-19T03:35:17.6056246+00:00 | 117.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,358,000 | 2026-07-19T03:35:17.6063067+00:00 | 117.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,361,000 | 2026-07-19T03:35:17.6088834+00:00 | 114.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,367,000 | 2026-07-19T03:35:17.613446+00:00 | 115.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,368,000 | 2026-07-19T03:35:17.6141084+00:00 | 114.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,371,000 | 2026-07-19T03:35:17.6162261+00:00 | 118.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,377,000 | 2026-07-19T03:35:17.6213083+00:00 | 117.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,378,000 | 2026-07-19T03:35:17.6220975+00:00 | 117.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,381,000 | 2026-07-19T03:35:17.6245523+00:00 | 122.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,387,000 | 2026-07-19T03:35:17.6280788+00:00 | 121.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,391,000 | 2026-07-19T03:35:17.6301473+00:00 | 119.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,398,000 | 2026-07-19T03:35:17.6366086+00:00 | 124.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,407,000 | 2026-07-19T03:35:17.6439722+00:00 | 118.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,408,000 | 2026-07-19T03:35:17.6445675+00:00 | 118.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,411,000 | 2026-07-19T03:35:17.6469977+00:00 | 119.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,417,000 | 2026-07-19T03:35:17.6525557+00:00 | 125.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,421,000 | 2026-07-19T03:35:17.6563655+00:00 | 122.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,427,000 | 2026-07-19T03:35:17.6611324+00:00 | 121.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,431,000 | 2026-07-19T03:35:17.6638315+00:00 | 118.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,437,000 | 2026-07-19T03:35:17.6676189+00:00 | 116.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,438,000 | 2026-07-19T03:35:17.6681551+00:00 | 120.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,441,000 | 2026-07-19T03:35:17.6698005+00:00 | 118.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,444,000 | 2026-07-19T03:35:17.671644+00:00 | 105.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,445,000 | 2026-07-19T03:35:17.6741713+00:00 | 103.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,447,000 | 2026-07-19T03:35:17.675838+00:00 | 119.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,448,000 | 2026-07-19T03:35:17.6764116+00:00 | 118.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,449,000 | 2026-07-19T03:35:17.6770443+00:00 | 100.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,450,000 | 2026-07-19T03:35:17.6776792+00:00 | 100.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,451,000 | 2026-07-19T03:35:17.6783858+00:00 | 118.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,457,000 | 2026-07-19T03:35:17.6830365+00:00 | 118.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,458,000 | 2026-07-19T03:35:17.6837514+00:00 | 117.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,461,000 | 2026-07-19T03:35:17.6861721+00:00 | 118.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,467,000 | 2026-07-19T03:35:17.6900529+00:00 | 124.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,468,000 | 2026-07-19T03:35:17.690773+00:00 | 123.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,471,000 | 2026-07-19T03:35:17.6925257+00:00 | 121.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,477,000 | 2026-07-19T03:35:17.6960066+00:00 | 119.6ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,478,000 | 2026-07-19T03:35:17.6978847+00:00 | 122.1ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,481,000 | 2026-07-19T03:35:17.6997971+00:00 | 120.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,488,000 | 2026-07-19T03:35:17.7049286+00:00 | 117.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,491,000 | 2026-07-19T03:35:17.707854+00:00 | 118.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,498,000 | 2026-07-19T03:35:17.7134042+00:00 | 117.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,501,000 | 2026-07-19T03:35:17.7166753+00:00 | 118.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,507,000 | 2026-07-19T03:35:17.7215429+00:00 | 117.5ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,511,000 | 2026-07-19T03:35:17.7244308+00:00 | 114.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,517,000 | 2026-07-19T03:35:17.7289203+00:00 | 114.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,521,000 | 2026-07-19T03:35:17.7318471+00:00 | 116.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,537,000 | 2026-07-19T03:35:17.7441648+00:00 | 116.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,538,000 | 2026-07-19T03:35:17.7447778+00:00 | 116.3ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,541,000 | 2026-07-19T03:35:17.7464471+00:00 | 114.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,547,000 | 2026-07-19T03:35:17.7519466+00:00 | 113.9ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,557,000 | 2026-07-19T03:35:17.75937+00:00 | 115.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,561,000 | 2026-07-19T03:35:17.7641012+00:00 | 112.2ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,567,000 | 2026-07-19T03:35:17.7684399+00:00 | 110.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,571,000 | 2026-07-19T03:35:17.7710731+00:00 | 111.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,581,000 | 2026-07-19T03:35:17.7844301+00:00 | 103.4ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,587,000 | 2026-07-19T03:35:17.7909935+00:00 | 101.0ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,588,000 | 2026-07-19T03:35:17.7922542+00:00 | 103.8ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 868,591,000 | 2026-07-19T03:35:17.7944051+00:00 | 101.7ms | GC pause | - | - | 808.6s / 1,139,821 msg/s | Gen2 +0 / pause +61.1ms |
| Confluent | 952,577,000 | 2026-07-19T03:36:28.1816728+00:00 | 110.5ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,581,000 | 2026-07-19T03:36:28.1851405+00:00 | 108.6ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,587,000 | 2026-07-19T03:36:28.1897515+00:00 | 108.4ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,591,000 | 2026-07-19T03:36:28.1932621+00:00 | 105.1ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,597,000 | 2026-07-19T03:36:28.1982987+00:00 | 109.7ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,598,000 | 2026-07-19T03:36:28.1988248+00:00 | 116.6ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,601,000 | 2026-07-19T03:36:28.2011178+00:00 | 114.5ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,607,000 | 2026-07-19T03:36:28.2052203+00:00 | 112.1ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,611,000 | 2026-07-19T03:36:28.2078023+00:00 | 112.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,617,000 | 2026-07-19T03:36:28.2118106+00:00 | 113.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,618,000 | 2026-07-19T03:36:28.2127879+00:00 | 112.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,621,000 | 2026-07-19T03:36:28.2147416+00:00 | 111.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,627,000 | 2026-07-19T03:36:28.2251734+00:00 | 111.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,628,000 | 2026-07-19T03:36:28.2263523+00:00 | 109.8ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,637,000 | 2026-07-19T03:36:28.2341534+00:00 | 109.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,641,000 | 2026-07-19T03:36:28.2373962+00:00 | 108.4ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,648,000 | 2026-07-19T03:36:28.2427288+00:00 | 109.4ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,651,000 | 2026-07-19T03:36:28.2445507+00:00 | 112.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,657,000 | 2026-07-19T03:36:28.2495045+00:00 | 112.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 952,658,000 | 2026-07-19T03:36:28.2503819+00:00 | 112.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,661,000 | 2026-07-19T03:36:28.2529105+00:00 | 109.6ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,668,000 | 2026-07-19T03:36:28.2647994+00:00 | 107.3ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,671,000 | 2026-07-19T03:36:28.2709294+00:00 | 101.5ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,677,000 | 2026-07-19T03:36:28.2763763+00:00 | 102.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,678,000 | 2026-07-19T03:36:28.277521+00:00 | 101.8ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,681,000 | 2026-07-19T03:36:28.2798847+00:00 | 102.1ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,748,000 | 2026-07-19T03:36:28.3410189+00:00 | 107.3ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,751,000 | 2026-07-19T03:36:28.3427321+00:00 | 105.7ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,758,000 | 2026-07-19T03:36:28.3483764+00:00 | 101.6ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,761,000 | 2026-07-19T03:36:28.3524896+00:00 | 100.2ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,847,000 | 2026-07-19T03:36:28.4279302+00:00 | 101.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,848,000 | 2026-07-19T03:36:28.4284875+00:00 | 101.5ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,867,000 | 2026-07-19T03:36:28.4440219+00:00 | 108.3ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,868,000 | 2026-07-19T03:36:28.4444221+00:00 | 108.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,871,000 | 2026-07-19T03:36:28.4460636+00:00 | 113.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,881,000 | 2026-07-19T03:36:28.4558895+00:00 | 105.0ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,887,000 | 2026-07-19T03:36:28.460079+00:00 | 104.7ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,891,000 | 2026-07-19T03:36:28.4628569+00:00 | 102.1ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,897,000 | 2026-07-19T03:36:28.4675866+00:00 | 106.9ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,901,000 | 2026-07-19T03:36:28.4720151+00:00 | 104.2ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,907,000 | 2026-07-19T03:36:28.4773827+00:00 | 105.2ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 952,911,000 | 2026-07-19T03:36:28.4831433+00:00 | 101.6ms | GC pause | - | - | 879.6s / 840,698 msg/s | Gen2 +0 / pause +89.2ms |
| Confluent | 202,179,000 | 2026-07-19T03:39:51.5711204+00:00 | 104.1ms | GC pause | - | - | 181.1s / 1,182,761 msg/s | Gen2 +0 / pause +96.4ms |
| Confluent | 285,248,000 | 2026-07-19T03:41:22.4435584+00:00 | 100.7ms | GC pause | - | - | 272.2s / 939,386 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 285,268,000 | 2026-07-19T03:41:22.4584673+00:00 | 102.5ms | GC pause | - | - | 272.2s / 939,386 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 285,271,000 | 2026-07-19T03:41:22.4605173+00:00 | 100.5ms | GC pause | - | - | 272.2s / 939,386 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 285,278,000 | 2026-07-19T03:41:22.4674893+00:00 | 103.3ms | GC pause | - | - | 272.2s / 939,386 msg/s | Gen2 +0 / pause +111.0ms |
| Confluent | 316,068,000 | 2026-07-19T03:41:53.7373328+00:00 | 101.7ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 316,117,000 | 2026-07-19T03:41:53.7809148+00:00 | 101.0ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +131.5ms |
| Confluent | 316,437,000 | 2026-07-19T03:41:54.070074+00:00 | 102.2ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,447,000 | 2026-07-19T03:41:54.0790636+00:00 | 103.4ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,451,000 | 2026-07-19T03:41:54.0818468+00:00 | 105.8ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,467,000 | 2026-07-19T03:41:54.0950414+00:00 | 111.0ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,468,000 | 2026-07-19T03:41:54.095795+00:00 | 111.5ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,471,000 | 2026-07-19T03:41:54.0978365+00:00 | 109.6ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,478,000 | 2026-07-19T03:41:54.1069539+00:00 | 101.6ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,508,000 | 2026-07-19T03:41:54.1340659+00:00 | 103.9ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,538,000 | 2026-07-19T03:41:54.1555631+00:00 | 115.6ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,551,000 | 2026-07-19T03:41:54.1635336+00:00 | 124.5ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,557,000 | 2026-07-19T03:41:54.1670488+00:00 | 124.8ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,558,000 | 2026-07-19T03:41:54.1675572+00:00 | 124.3ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,577,000 | 2026-07-19T03:41:54.1849752+00:00 | 124.1ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,580,000 | 2026-07-19T03:41:54.1879473+00:00 | 105.8ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,611,000 | 2026-07-19T03:41:54.2183848+00:00 | 121.3ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,628,000 | 2026-07-19T03:41:54.2410469+00:00 | 116.9ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,671,000 | 2026-07-19T03:41:54.2835192+00:00 | 125.4ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,698,000 | 2026-07-19T03:41:54.3192448+00:00 | 106.4ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,701,000 | 2026-07-19T03:41:54.3223492+00:00 | 110.4ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,717,000 | 2026-07-19T03:41:54.3359258+00:00 | 114.1ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,731,000 | 2026-07-19T03:41:54.3471177+00:00 | 113.7ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,788,000 | 2026-07-19T03:41:54.3953641+00:00 | 122.3ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,817,000 | 2026-07-19T03:41:54.4254945+00:00 | 118.1ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,831,000 | 2026-07-19T03:41:54.4364805+00:00 | 121.0ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,858,000 | 2026-07-19T03:41:54.4569111+00:00 | 124.6ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,861,000 | 2026-07-19T03:41:54.4586567+00:00 | 131.8ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,868,000 | 2026-07-19T03:41:54.465762+00:00 | 126.0ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,888,000 | 2026-07-19T03:41:54.4844305+00:00 | 119.8ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,897,000 | 2026-07-19T03:41:54.4899855+00:00 | 127.6ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,898,000 | 2026-07-19T03:41:54.4905904+00:00 | 127.1ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,938,000 | 2026-07-19T03:41:54.5284345+00:00 | 118.3ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 316,951,000 | 2026-07-19T03:41:54.5397691+00:00 | 117.4ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,008,000 | 2026-07-19T03:41:54.5852018+00:00 | 124.1ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,011,000 | 2026-07-19T03:41:54.5870152+00:00 | 126.5ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,021,000 | 2026-07-19T03:41:54.5991299+00:00 | 133.9ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,041,000 | 2026-07-19T03:41:54.6274023+00:00 | 129.5ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,048,000 | 2026-07-19T03:41:54.63952+00:00 | 123.7ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,078,000 | 2026-07-19T03:41:54.6793671+00:00 | 110.0ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,131,000 | 2026-07-19T03:41:54.7335633+00:00 | 102.8ms | GC pause | - | - | 304.2s / 1,017,971 msg/s | Gen2 +0 / pause +54.9ms |
| Confluent | 317,138,000 | 2026-07-19T03:41:54.7430797+00:00 | 101.2ms | GC pause | - | - | 305.2s / 945,607 msg/s | Gen2 +0 / pause +147.4ms |
| Confluent | 342,072,000 | 2026-07-19T03:42:19.5183369+00:00 | 100.9ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,091,000 | 2026-07-19T03:42:19.5323078+00:00 | 107.4ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,097,000 | 2026-07-19T03:42:19.5367773+00:00 | 106.3ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,098,000 | 2026-07-19T03:42:19.5375012+00:00 | 105.6ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,108,000 | 2026-07-19T03:42:19.547173+00:00 | 106.0ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,131,000 | 2026-07-19T03:42:19.5667882+00:00 | 108.6ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,138,000 | 2026-07-19T03:42:19.5742488+00:00 | 105.9ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,167,000 | 2026-07-19T03:42:19.5980713+00:00 | 111.3ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,168,000 | 2026-07-19T03:42:19.5990385+00:00 | 110.3ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,208,000 | 2026-07-19T03:42:19.6482203+00:00 | 107.4ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,267,000 | 2026-07-19T03:42:19.7003829+00:00 | 102.6ms | GC pause | - | - | 329.2s / 1,109,295 msg/s | Gen2 +0 / pause +64.2ms |
| Confluent | 342,417,000 | 2026-07-19T03:42:19.8320644+00:00 | 102.7ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +137.3ms |
| Confluent | 342,448,000 | 2026-07-19T03:42:19.8585088+00:00 | 102.2ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +137.3ms |
| Confluent | 342,467,000 | 2026-07-19T03:42:19.876639+00:00 | 100.6ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,478,000 | 2026-07-19T03:42:19.8831184+00:00 | 102.5ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,518,000 | 2026-07-19T03:42:19.9221616+00:00 | 101.7ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,527,000 | 2026-07-19T03:42:19.929081+00:00 | 108.3ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,567,000 | 2026-07-19T03:42:19.9637112+00:00 | 101.6ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,571,000 | 2026-07-19T03:42:19.9667417+00:00 | 111.1ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,601,000 | 2026-07-19T03:42:19.9918063+00:00 | 104.8ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 342,827,000 | 2026-07-19T03:42:20.2000586+00:00 | 102.7ms | GC pause | - | - | 330.2s / 1,061,262 msg/s | Gen2 +0 / pause +73.1ms |
| Confluent | 373,667,000 | 2026-07-19T03:42:52.2532584+00:00 | 110.0ms | GC pause | - | - | 362.3s / 1,025,235 msg/s | Gen2 +0 / pause +94.6ms |
| Confluent | 388,688,000 | 2026-07-19T03:43:08.0388609+00:00 | 109.7ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,698,000 | 2026-07-19T03:43:08.0454374+00:00 | 104.6ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,701,000 | 2026-07-19T03:43:08.0477329+00:00 | 103.3ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,707,000 | 2026-07-19T03:43:08.0521278+00:00 | 103.5ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,721,000 | 2026-07-19T03:43:08.0607127+00:00 | 105.4ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,728,000 | 2026-07-19T03:43:08.0648784+00:00 | 127.5ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,737,000 | 2026-07-19T03:43:08.0724395+00:00 | 121.4ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,740,000 | 2026-07-19T03:43:08.0747323+00:00 | 104.6ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,751,000 | 2026-07-19T03:43:08.0817245+00:00 | 117.2ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,753,000 | 2026-07-19T03:43:08.0835943+00:00 | 110.8ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,791,000 | 2026-07-19T03:43:08.110092+00:00 | 115.8ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,808,000 | 2026-07-19T03:43:08.1363767+00:00 | 101.6ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,818,000 | 2026-07-19T03:43:08.1438389+00:00 | 100.7ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,847,000 | 2026-07-19T03:43:08.166429+00:00 | 109.9ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,848,000 | 2026-07-19T03:43:08.1671286+00:00 | 109.2ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,851,000 | 2026-07-19T03:43:08.1690512+00:00 | 119.9ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,861,000 | 2026-07-19T03:43:08.1750937+00:00 | 121.1ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,897,000 | 2026-07-19T03:43:08.204697+00:00 | 136.0ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,900,000 | 2026-07-19T03:43:08.2068235+00:00 | 135.5ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,907,000 | 2026-07-19T03:43:08.2122237+00:00 | 139.7ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,911,000 | 2026-07-19T03:43:08.2154697+00:00 | 136.7ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,914,000 | 2026-07-19T03:43:08.2176264+00:00 | 119.0ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,916,000 | 2026-07-19T03:43:08.2187083+00:00 | 116.2ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,925,000 | 2026-07-19T03:43:08.2235879+00:00 | 116.7ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,927,000 | 2026-07-19T03:43:08.2268274+00:00 | 143.3ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,928,000 | 2026-07-19T03:43:08.2274012+00:00 | 142.9ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,935,000 | 2026-07-19T03:43:08.232146+00:00 | 115.1ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,939,000 | 2026-07-19T03:43:08.2344985+00:00 | 117.1ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,941,000 | 2026-07-19T03:43:08.2355546+00:00 | 144.4ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,944,000 | 2026-07-19T03:43:08.2395888+00:00 | 127.8ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,948,000 | 2026-07-19T03:43:08.2427049+00:00 | 139.4ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,952,000 | 2026-07-19T03:43:08.2455764+00:00 | 121.0ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,957,000 | 2026-07-19T03:43:08.24912+00:00 | 141.2ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,958,000 | 2026-07-19T03:43:08.2496549+00:00 | 142.5ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,966,000 | 2026-07-19T03:43:08.2550603+00:00 | 118.3ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,967,000 | 2026-07-19T03:43:08.2558755+00:00 | 144.7ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,980,000 | 2026-07-19T03:43:08.2629457+00:00 | 141.3ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 388,985,000 | 2026-07-19T03:43:08.268474+00:00 | 120.5ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 389,000,000 | 2026-07-19T03:43:08.2828193+00:00 | 145.3ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 389,005,000 | 2026-07-19T03:43:08.2983734+00:00 | 103.6ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 389,008,000 | 2026-07-19T03:43:08.305118+00:00 | 128.8ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 389,017,000 | 2026-07-19T03:43:08.3323235+00:00 | 107.6ms | GC pause | - | - | 378.3s / 921,217 msg/s | Gen2 +0 / pause +93.6ms |
| Confluent | 426,698,000 | 2026-07-19T03:43:49.572041+00:00 | 100.5ms | GC pause | - | - | 419.3s / 1,156,803 msg/s | Gen2 +0 / pause +92.7ms |
| Confluent | 426,708,000 | 2026-07-19T03:43:49.5798547+00:00 | 104.2ms | GC pause | - | - | 419.3s / 1,156,803 msg/s | Gen2 +0 / pause +92.7ms |
| Confluent | 426,721,000 | 2026-07-19T03:43:49.588103+00:00 | 106.0ms | GC pause | - | - | 419.3s / 1,156,803 msg/s | Gen2 +0 / pause +92.7ms |
| Confluent | 445,031,000 | 2026-07-19T03:44:09.2587415+00:00 | 100.2ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,037,000 | 2026-07-19T03:44:09.2639515+00:00 | 102.1ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,047,000 | 2026-07-19T03:44:09.2698394+00:00 | 107.1ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,105,000 | 2026-07-19T03:44:09.3122278+00:00 | 109.8ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,106,000 | 2026-07-19T03:44:09.3127585+00:00 | 109.3ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,123,000 | 2026-07-19T03:44:09.3335314+00:00 | 111.1ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,129,000 | 2026-07-19T03:44:09.3389731+00:00 | 104.8ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,130,000 | 2026-07-19T03:44:09.3398663+00:00 | 109.4ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,135,000 | 2026-07-19T03:44:09.3436795+00:00 | 102.0ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,138,000 | 2026-07-19T03:44:09.3472983+00:00 | 121.1ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,141,000 | 2026-07-19T03:44:09.3520528+00:00 | 116.4ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,147,000 | 2026-07-19T03:44:09.360216+00:00 | 114.6ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,161,000 | 2026-07-19T03:44:09.3792861+00:00 | 102.4ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 445,177,000 | 2026-07-19T03:44:09.3975286+00:00 | 101.5ms | GC pause | - | - | 439.3s / 1,006,010 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 570,652,000 | 2026-07-19T03:46:15.9385467+00:00 | 108.9ms | GC pause | - | - | 565.5s / 1,214,456 msg/s | Gen2 +0 / pause +86.0ms |
| Confluent | 578,147,000 | 2026-07-19T03:46:23.6059325+00:00 | 104.3ms | GC pause | - | - | 573.5s / 1,257,056 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 578,148,000 | 2026-07-19T03:46:23.6065875+00:00 | 103.6ms | GC pause | - | - | 573.5s / 1,257,056 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 598,661,000 | 2026-07-19T03:46:45.8528438+00:00 | 119.4ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,675,000 | 2026-07-19T03:46:45.8641979+00:00 | 106.0ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,687,000 | 2026-07-19T03:46:45.8735312+00:00 | 126.1ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,692,000 | 2026-07-19T03:46:45.8766615+00:00 | 107.5ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,696,000 | 2026-07-19T03:46:45.8798575+00:00 | 111.4ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,699,000 | 2026-07-19T03:46:45.882175+00:00 | 110.8ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,700,000 | 2026-07-19T03:46:45.8835282+00:00 | 119.6ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,712,000 | 2026-07-19T03:46:45.8935704+00:00 | 112.5ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,714,000 | 2026-07-19T03:46:45.8948624+00:00 | 113.6ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,716,000 | 2026-07-19T03:46:45.89639+00:00 | 113.3ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,717,000 | 2026-07-19T03:46:45.8972092+00:00 | 130.9ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,720,000 | 2026-07-19T03:46:45.9000178+00:00 | 118.2ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,723,000 | 2026-07-19T03:46:45.9027817+00:00 | 120.8ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,724,000 | 2026-07-19T03:46:45.9039744+00:00 | 114.5ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,735,000 | 2026-07-19T03:46:45.9132566+00:00 | 114.6ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,740,000 | 2026-07-19T03:46:45.916848+00:00 | 122.3ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,743,000 | 2026-07-19T03:46:45.9187901+00:00 | 121.9ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,744,000 | 2026-07-19T03:46:45.9195836+00:00 | 120.2ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,745,000 | 2026-07-19T03:46:45.9201524+00:00 | 119.6ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,746,000 | 2026-07-19T03:46:45.9207337+00:00 | 119.1ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,759,000 | 2026-07-19T03:46:45.9289159+00:00 | 116.4ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,767,000 | 2026-07-19T03:46:45.9352361+00:00 | 146.5ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,771,000 | 2026-07-19T03:46:45.938313+00:00 | 143.5ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,774,000 | 2026-07-19T03:46:45.941371+00:00 | 121.1ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,778,000 | 2026-07-19T03:46:45.9445113+00:00 | 146.0ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,793,000 | 2026-07-19T03:46:45.9558253+00:00 | 134.1ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,800,000 | 2026-07-19T03:46:45.9619931+00:00 | 131.9ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,801,000 | 2026-07-19T03:46:45.9633634+00:00 | 146.2ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,804,000 | 2026-07-19T03:46:45.967721+00:00 | 126.4ms | GC pause | - | - | 595.5s / 1,054,573 msg/s | Gen2 +0 / pause +66.6ms |
| Confluent | 598,818,000 | 2026-07-19T03:46:45.9847316+00:00 | 142.6ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,829,000 | 2026-07-19T03:46:45.999451+00:00 | 116.5ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,833,000 | 2026-07-19T03:46:46.0050383+00:00 | 118.5ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,835,000 | 2026-07-19T03:46:46.0070725+00:00 | 113.3ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,838,000 | 2026-07-19T03:46:46.0102988+00:00 | 131.5ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,840,000 | 2026-07-19T03:46:46.0123041+00:00 | 120.6ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,845,000 | 2026-07-19T03:46:46.0181091+00:00 | 109.2ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,850,000 | 2026-07-19T03:46:46.0245216+00:00 | 114.9ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,854,000 | 2026-07-19T03:46:46.0271183+00:00 | 113.1ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,859,000 | 2026-07-19T03:46:46.0322742+00:00 | 108.1ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,861,000 | 2026-07-19T03:46:46.0352539+00:00 | 132.1ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,865,000 | 2026-07-19T03:46:46.0412805+00:00 | 106.3ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,877,000 | 2026-07-19T03:46:46.053325+00:00 | 124.3ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,881,000 | 2026-07-19T03:46:46.0630959+00:00 | 114.7ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 598,882,000 | 2026-07-19T03:46:46.0641917+00:00 | 102.0ms | GC pause | - | - | 596.5s / 911,369 msg/s | Gen2 +0 / pause +171.8ms |
| Confluent | 607,501,000 | 2026-07-19T03:46:55.6187981+00:00 | 105.7ms | GC pause | - | - | 605.5s / 842,673 msg/s | Gen2 +0 / pause +91.0ms |
| Confluent | 643,397,000 | 2026-07-19T03:47:30.9107182+00:00 | 101.3ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,428,000 | 2026-07-19T03:47:30.9342468+00:00 | 102.9ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,448,000 | 2026-07-19T03:47:30.9516342+00:00 | 102.1ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,466,000 | 2026-07-19T03:47:30.9656705+00:00 | 104.4ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,477,000 | 2026-07-19T03:47:30.973333+00:00 | 117.4ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,481,000 | 2026-07-19T03:47:30.9788968+00:00 | 114.6ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,484,000 | 2026-07-19T03:47:30.9821892+00:00 | 108.0ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,487,000 | 2026-07-19T03:47:30.985642+00:00 | 109.0ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,492,000 | 2026-07-19T03:47:30.9893938+00:00 | 100.5ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,494,000 | 2026-07-19T03:47:30.9912858+00:00 | 100.2ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,498,000 | 2026-07-19T03:47:30.998143+00:00 | 109.5ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 643,548,000 | 2026-07-19T03:47:31.043739+00:00 | 106.3ms | GC pause | - | - | 640.5s / 1,115,842 msg/s | Gen2 +0 / pause +81.6ms |
| Confluent | 723,058,000 | 2026-07-19T03:48:46.1993427+00:00 | 104.9ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +163.2ms |
| Confluent | 723,078,000 | 2026-07-19T03:48:46.2149764+00:00 | 113.7ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,094,000 | 2026-07-19T03:48:46.2276848+00:00 | 104.1ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,099,000 | 2026-07-19T03:48:46.2323565+00:00 | 100.6ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,101,000 | 2026-07-19T03:48:46.2346758+00:00 | 120.1ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,107,000 | 2026-07-19T03:48:46.2402601+00:00 | 117.2ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,110,000 | 2026-07-19T03:48:46.2423089+00:00 | 108.5ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,113,000 | 2026-07-19T03:48:46.2444653+00:00 | 106.4ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,114,000 | 2026-07-19T03:48:46.2451473+00:00 | 105.9ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,125,000 | 2026-07-19T03:48:46.2531021+00:00 | 107.6ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,134,000 | 2026-07-19T03:48:46.2593142+00:00 | 108.3ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,135,000 | 2026-07-19T03:48:46.2601201+00:00 | 107.7ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,138,000 | 2026-07-19T03:48:46.2617472+00:00 | 117.1ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,142,000 | 2026-07-19T03:48:46.2640701+00:00 | 107.7ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,147,000 | 2026-07-19T03:48:46.2672529+00:00 | 125.1ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,155,000 | 2026-07-19T03:48:46.2737667+00:00 | 110.0ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,161,000 | 2026-07-19T03:48:46.2780274+00:00 | 122.8ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,170,000 | 2026-07-19T03:48:46.2834862+00:00 | 113.5ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,183,000 | 2026-07-19T03:48:46.2923424+00:00 | 114.0ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,185,000 | 2026-07-19T03:48:46.2934517+00:00 | 114.1ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,187,000 | 2026-07-19T03:48:46.29513+00:00 | 127.2ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,190,000 | 2026-07-19T03:48:46.2972397+00:00 | 113.3ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,191,000 | 2026-07-19T03:48:46.2978261+00:00 | 124.6ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,200,000 | 2026-07-19T03:48:46.303443+00:00 | 115.6ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,208,000 | 2026-07-19T03:48:46.3128922+00:00 | 136.5ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,224,000 | 2026-07-19T03:48:46.3354649+00:00 | 100.2ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,229,000 | 2026-07-19T03:48:46.3405351+00:00 | 109.8ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,251,000 | 2026-07-19T03:48:46.3684612+00:00 | 123.2ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,260,000 | 2026-07-19T03:48:46.3807748+00:00 | 100.2ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,268,000 | 2026-07-19T03:48:46.3865577+00:00 | 113.0ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,271,000 | 2026-07-19T03:48:46.3906557+00:00 | 109.0ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,287,000 | 2026-07-19T03:48:46.4055318+00:00 | 107.7ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 723,307,000 | 2026-07-19T03:48:46.4252139+00:00 | 105.5ms | GC pause | - | - | 716.6s / 1,112,218 msg/s | Gen2 +0 / pause +55.1ms |
| Confluent | 746,678,000 | 2026-07-19T03:49:07.9153613+00:00 | 101.6ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,708,000 | 2026-07-19T03:49:07.9409599+00:00 | 101.1ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,717,000 | 2026-07-19T03:49:07.9474615+00:00 | 112.5ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,737,000 | 2026-07-19T03:49:07.9634112+00:00 | 109.5ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,755,000 | 2026-07-19T03:49:07.9757598+00:00 | 100.7ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,774,000 | 2026-07-19T03:49:07.9895218+00:00 | 105.9ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,775,000 | 2026-07-19T03:49:07.9902886+00:00 | 106.7ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,789,000 | 2026-07-19T03:49:07.9985707+00:00 | 103.4ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,791,000 | 2026-07-19T03:49:07.9998612+00:00 | 123.3ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,796,000 | 2026-07-19T03:49:08.0026777+00:00 | 108.7ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,803,000 | 2026-07-19T03:49:08.0086176+00:00 | 107.7ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,805,000 | 2026-07-19T03:49:08.0142238+00:00 | 107.7ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,808,000 | 2026-07-19T03:49:08.0193464+00:00 | 119.6ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,821,000 | 2026-07-19T03:49:08.0327619+00:00 | 115.2ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,828,000 | 2026-07-19T03:49:08.0384486+00:00 | 113.9ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,831,000 | 2026-07-19T03:49:08.0402957+00:00 | 123.6ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,838,000 | 2026-07-19T03:49:08.0513159+00:00 | 113.9ms | GC pause | - | - | 737.6s / 1,131,109 msg/s | Gen2 +0 / pause +109.2ms |
| Confluent | 746,961,000 | 2026-07-19T03:49:08.1906972+00:00 | 121.4ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +223.7ms |
| Confluent | 746,967,000 | 2026-07-19T03:49:08.1945497+00:00 | 121.5ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +223.7ms |
| Confluent | 746,984,000 | 2026-07-19T03:49:08.2061877+00:00 | 104.0ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +223.7ms |
| Confluent | 746,988,000 | 2026-07-19T03:49:08.2095815+00:00 | 121.5ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +223.7ms |
| Confluent | 746,991,000 | 2026-07-19T03:49:08.2131161+00:00 | 118.1ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +223.7ms |
| Confluent | 747,377,000 | 2026-07-19T03:49:08.6024371+00:00 | 101.2ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,397,000 | 2026-07-19T03:49:08.6202632+00:00 | 101.3ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,467,000 | 2026-07-19T03:49:08.6789352+00:00 | 100.1ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,487,000 | 2026-07-19T03:49:08.695929+00:00 | 100.9ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,498,000 | 2026-07-19T03:49:08.7049165+00:00 | 102.9ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,527,000 | 2026-07-19T03:49:08.7310509+00:00 | 107.4ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,547,000 | 2026-07-19T03:49:08.7484676+00:00 | 100.9ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,548,000 | 2026-07-19T03:49:08.7490015+00:00 | 100.4ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,551,000 | 2026-07-19T03:49:08.7511224+00:00 | 107.4ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,587,000 | 2026-07-19T03:49:08.7949861+00:00 | 101.7ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 747,597,000 | 2026-07-19T03:49:08.8064698+00:00 | 101.0ms | GC pause | - | - | 738.6s / 998,313 msg/s | Gen2 +0 / pause +114.5ms |
| Confluent | 852,301,000 | 2026-07-19T03:50:47.1848703+00:00 | 101.4ms | GC pause | - | - | 836.7s / 1,185,134 msg/s | Gen2 +0 / pause +60.3ms |
| Confluent | 852,311,000 | 2026-07-19T03:50:47.1948278+00:00 | 100.4ms | GC pause | - | - | 836.7s / 1,185,134 msg/s | Gen2 +0 / pause +60.3ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*1,338 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.59x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.40x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.24 | 1251.98 | 1,031,368 | 1,056,286 | +14.0% | +1.38% | 983.59 | 1,031,368 | 0 | 1.28 |
| Confluent | 1.83 | - | 841,534 | 856,903 | +2.4% | +0.23% | 802.55 | 841,534 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 301,748 | 335.27 | 996.93 KB |
| Dekaf | 2 | 306,933 | 341.03 | 996.62 KB |
| Dekaf | 3 | 310,390 | 344.87 | 1018.22 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:51.9566481+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 249,127 msg/s |
| Dekaf | 2026-07-19T03:07:00.9612555+00:00 | 3 | 16.0 MiB / 10.7 MiB | 227.0 MB/s | 0/0 | 1,797 | 9.0s / 652,052 msg/s |
| Dekaf | 2026-07-19T03:07:09.9681502+00:00 | 3 | 16.0 MiB / 7.2 MiB | 293.0 MB/s | 0/0 | 5,385 | 18.0s / 752,969 msg/s |
| Dekaf | 2026-07-19T03:07:19.9812796+00:00 | 1 | 16.0 MiB / 5.2 MiB | 315.7 MB/s | 0/0 | 4,944 | 28.0s / 846,550 msg/s |
| Dekaf | 2026-07-19T03:07:28.9892923+00:00 | 1 | 16.0 MiB / 8.1 MiB | 315.7 MB/s | 0/0 | 5,253 | 37.0s / 852,869 msg/s |
| Dekaf | 2026-07-19T03:07:37.9945659+00:00 | 1 | 14.0 MiB / 4.2 MiB | 326.2 MB/s | 1/0 | 5,335 | 46.0s / 936,958 msg/s |
| Dekaf | 2026-07-19T03:07:46.9991054+00:00 | 1 | 14.0 MiB / 4.6 MiB | 345.0 MB/s | 1/0 | 5,803 | 55.0s / 929,799 msg/s |
| Dekaf | 2026-07-19T03:07:56.0092544+00:00 | 2 | 12.0 MiB / 4.8 MiB | 354.5 MB/s | 2/0 | 2,807 | 64.1s / 951,966 msg/s |
| Dekaf | 2026-07-19T03:08:05.0112853+00:00 | 2 | 12.0 MiB / 6.6 MiB | 354.8 MB/s | 2/0 | 3,460 | 73.1s / 970,792 msg/s |
| Dekaf | 2026-07-19T03:08:14.0140816+00:00 | 2 | 10.0 MiB / 2.3 MiB | 359.2 MB/s | 3/0 | 4,140 | 82.1s / 1,006,258 msg/s |
| Dekaf | 2026-07-19T03:08:23.0158896+00:00 | 2 | 10.0 MiB / 2.5 MiB | 365.9 MB/s | 3/0 | 4,888 | 91.1s / 997,757 msg/s |
| Dekaf | 2026-07-19T03:08:32.0177235+00:00 | 3 | 10.0 MiB / 9.2 MiB | 376.3 MB/s | 3/1 | 39,195 | 100.1s / 1,027,060 msg/s |
| Dekaf | 2026-07-19T03:08:41.0202875+00:00 | 3 | 10.0 MiB / 6.5 MiB | 376.3 MB/s | 3/1 | 44,873 | 109.1s / 997,243 msg/s |
| Dekaf | 2026-07-19T03:08:50.0224583+00:00 | 3 | 10.0 MiB / 9.4 MiB | 376.3 MB/s | 3/1 | 49,371 | 118.1s / 1,015,427 msg/s |
| Dekaf | 2026-07-19T03:08:59.0266587+00:00 | 3 | 10.0 MiB / 4.7 MiB | 376.3 MB/s | 3/1 | 52,467 | 127.1s / 1,000,645 msg/s |
| Dekaf | 2026-07-19T03:09:09.0280067+00:00 | 1 | 10.0 MiB / 3.6 MiB | 363.3 MB/s | 3/1 | 11,459 | 137.1s / 762,854 msg/s |
| Dekaf | 2026-07-19T03:09:18.0294636+00:00 | 1 | 10.0 MiB / 4.6 MiB | 381.2 MB/s | 3/1 | 12,565 | 146.1s / 1,068,728 msg/s |
| Dekaf | 2026-07-19T03:09:27.0340208+00:00 | 1 | 10.0 MiB / 2.2 MiB | 381.2 MB/s | 3/1 | 13,006 | 155.1s / 1,021,377 msg/s |
| Dekaf | 2026-07-19T03:09:36.0342526+00:00 | 1 | 10.0 MiB / 5.4 MiB | 381.2 MB/s | 3/2 | 13,397 | 164.1s / 734,822 msg/s |
| Dekaf | 2026-07-19T03:09:45.0367805+00:00 | 2 | 8.0 MiB / 4.7 MiB | 386.3 MB/s | 3/1 | 9,626 | 173.1s / 1,025,500 msg/s |
| Dekaf | 2026-07-19T03:09:54.0412611+00:00 | 2 | 10.0 MiB / 1.7 MiB | 386.3 MB/s | 3/2 | 10,141 | 182.1s / 909,035 msg/s |
| Dekaf | 2026-07-19T03:10:03.0472804+00:00 | 2 | 10.0 MiB / 6.1 MiB | 386.3 MB/s | 3/2 | 10,405 | 191.1s / 978,504 msg/s |
| Dekaf | 2026-07-19T03:10:12.0488205+00:00 | 2 | 10.0 MiB / 6.9 MiB | 386.3 MB/s | 3/2 | 10,645 | 200.1s / 914,192 msg/s |
| Dekaf | 2026-07-19T03:10:21.0545621+00:00 | 3 | 8.0 MiB / 3.9 MiB | 398.9 MB/s | 4/3 | 95,310 | 209.1s / 916,246 msg/s |
| Dekaf | 2026-07-19T03:10:30.0565718+00:00 | 3 | 8.0 MiB / 4.6 MiB | 398.9 MB/s | 4/3 | 99,383 | 218.1s / 907,140 msg/s |
| Dekaf | 2026-07-19T03:10:39.0598502+00:00 | 3 | 8.0 MiB / 7.1 MiB | 398.9 MB/s | 4/3 | 102,224 | 227.1s / 846,391 msg/s |
| Dekaf | 2026-07-19T03:10:48.0641419+00:00 | 3 | 8.0 MiB / 6.7 MiB | 398.9 MB/s | 4/3 | 105,241 | 236.1s / 984,988 msg/s |
| Dekaf | 2026-07-19T03:10:58.0693371+00:00 | 1 | 10.0 MiB / 3.9 MiB | 381.2 MB/s | 3/2 | 17,084 | 246.1s / 1,021,400 msg/s |
| Dekaf | 2026-07-19T03:11:07.0725058+00:00 | 1 | 10.0 MiB / 10.0 MiB | 381.2 MB/s | 3/2 | 17,375 | 255.1s / 1,027,282 msg/s |
| Dekaf | 2026-07-19T03:11:16.0763002+00:00 | 1 | 10.0 MiB / 1.7 MiB | 381.2 MB/s | 3/2 | 17,690 | 264.1s / 1,060,029 msg/s |
| Dekaf | 2026-07-19T03:11:25.0822716+00:00 | 1 | 10.0 MiB / 3.9 MiB | 381.2 MB/s | 3/2 | 17,787 | 273.1s / 1,047,995 msg/s |
| Dekaf | 2026-07-19T03:11:34.0923636+00:00 | 2 | 8.0 MiB / 6.4 MiB | 386.3 MB/s | 4/4 | 17,916 | 282.1s / 1,082,955 msg/s |
| Dekaf | 2026-07-19T03:11:43.0979233+00:00 | 2 | 8.0 MiB / 5.1 MiB | 386.3 MB/s | 4/4 | 18,908 | 291.1s / 998,081 msg/s |
| Dekaf | 2026-07-19T03:11:52.1068682+00:00 | 2 | 8.0 MiB / 2.4 MiB | 386.3 MB/s | 4/4 | 19,432 | 300.1s / 1,115,298 msg/s |
| Dekaf | 2026-07-19T03:12:01.1072536+00:00 | 2 | 8.0 MiB / 1.5 MiB | 386.3 MB/s | 4/4 | 19,925 | 309.1s / 1,007,850 msg/s |
| Dekaf | 2026-07-19T03:12:10.1112565+00:00 | 2 | 9.0 MiB / 4.9 MiB | 386.3 MB/s | 5/4 | 20,578 | 318.1s / 989,970 msg/s |
| Dekaf | 2026-07-19T03:12:19.1199882+00:00 | 3 | 8.0 MiB / 8.0 MiB | 398.9 MB/s | 4/5 | 152,623 | 327.1s / 1,068,856 msg/s |
| Dekaf | 2026-07-19T03:12:28.1213937+00:00 | 3 | 8.0 MiB / 8.0 MiB | 398.9 MB/s | 4/5 | 158,666 | 336.1s / 1,088,356 msg/s |
| Dekaf | 2026-07-19T03:12:37.1272889+00:00 | 3 | 8.0 MiB / 7.7 MiB | 398.9 MB/s | 4/6 | 167,006 | 345.2s / 1,079,325 msg/s |
| Dekaf | 2026-07-19T03:12:46.1417142+00:00 | 3 | 8.0 MiB / 8.0 MiB | 398.9 MB/s | 4/6 | 172,645 | 354.2s / 1,022,301 msg/s |
| Dekaf | 2026-07-19T03:12:56.142272+00:00 | 1 | 9.0 MiB / 4.9 MiB | 381.9 MB/s | 5/3 | 24,600 | 364.2s / 883,951 msg/s |
| Dekaf | 2026-07-19T03:13:05.1474273+00:00 | 1 | 9.0 MiB / 2.9 MiB | 381.9 MB/s | 5/3 | 24,975 | 373.2s / 1,094,087 msg/s |
| Dekaf | 2026-07-19T03:13:14.1476568+00:00 | 1 | 9.0 MiB / 3.0 MiB | 381.9 MB/s | 5/3 | 25,493 | 382.2s / 1,102,928 msg/s |
| Dekaf | 2026-07-19T03:13:23.1495353+00:00 | 1 | 9.0 MiB / 1.7 MiB | 381.9 MB/s | 5/3 | 25,781 | 391.2s / 1,090,556 msg/s |
| Dekaf | 2026-07-19T03:13:32.1508944+00:00 | 2 | 7.0 MiB / 6.7 MiB | 386.3 MB/s | 6/5 | 33,042 | 400.2s / 1,069,357 msg/s |
| Dekaf | 2026-07-19T03:13:41.1523442+00:00 | 2 | 8.0 MiB / 3.7 MiB | 393.9 MB/s | 6/5 | 34,325 | 409.2s / 1,028,281 msg/s |
| Dekaf | 2026-07-19T03:13:50.1557967+00:00 | 2 | 7.0 MiB / 1.3 MiB | 406.1 MB/s | 6/6 | 37,476 | 418.2s / 1,145,665 msg/s |
| Dekaf | 2026-07-19T03:13:59.1576382+00:00 | 2 | 7.0 MiB / 0.0 MiB | 406.1 MB/s | 6/6 | 38,978 | 427.2s / 1,156,716 msg/s |
| Dekaf | 2026-07-19T03:14:08.1642544+00:00 | 3 | 8.0 MiB / 8.0 MiB | 415.0 MB/s | 4/8 | 237,103 | 436.2s / 1,056,698 msg/s |
| Dekaf | 2026-07-19T03:14:17.1662625+00:00 | 3 | 8.0 MiB / 8.0 MiB | 419.0 MB/s | 4/8 | 243,876 | 445.2s / 1,122,590 msg/s |
| Dekaf | 2026-07-19T03:14:26.1692627+00:00 | 3 | 8.0 MiB / 7.5 MiB | 419.0 MB/s | 4/8 | 252,310 | 454.2s / 1,073,165 msg/s |
| Dekaf | 2026-07-19T03:14:35.1716161+00:00 | 3 | 8.0 MiB / 8.0 MiB | 420.1 MB/s | 4/8 | 261,109 | 463.2s / 1,060,428 msg/s |
| Dekaf | 2026-07-19T03:14:45.1779833+00:00 | 1 | 9.0 MiB / 6.8 MiB | 406.5 MB/s | 5/4 | 34,618 | 473.2s / 1,162,818 msg/s |
| Dekaf | 2026-07-19T03:14:54.1782802+00:00 | 1 | 9.0 MiB / 5.7 MiB | 406.5 MB/s | 5/4 | 36,338 | 482.2s / 1,199,406 msg/s |
| Dekaf | 2026-07-19T03:15:03.1842657+00:00 | 1 | 9.0 MiB / 1.6 MiB | 410.0 MB/s | 5/4 | 36,717 | 491.2s / 1,142,395 msg/s |
| Dekaf | 2026-07-19T03:15:12.1862475+00:00 | 1 | 9.0 MiB / 0.3 MiB | 411.9 MB/s | 5/4 | 37,065 | 500.2s / 1,136,796 msg/s |
| Dekaf | 2026-07-19T03:15:21.1922731+00:00 | 2 | 8.0 MiB / 8.0 MiB | 414.9 MB/s | 7/7 | 51,954 | 509.2s / 1,109,349 msg/s |
| Dekaf | 2026-07-19T03:15:30.1936594+00:00 | 2 | 8.0 MiB / 6.9 MiB | 414.9 MB/s | 7/7 | 52,452 | 518.2s / 1,178,600 msg/s |
| Dekaf | 2026-07-19T03:15:39.1984389+00:00 | 2 | 8.0 MiB / 4.1 MiB | 414.9 MB/s | 7/7 | 53,327 | 527.2s / 1,159,802 msg/s |
| Dekaf | 2026-07-19T03:15:48.2013205+00:00 | 2 | 8.0 MiB / 3.1 MiB | 417.8 MB/s | 7/7 | 53,903 | 536.2s / 973,920 msg/s |
| Dekaf | 2026-07-19T03:15:57.2032087+00:00 | 3 | 8.0 MiB / 6.7 MiB | 430.5 MB/s | 4/9 | 342,455 | 545.2s / 1,173,435 msg/s |
| Dekaf | 2026-07-19T03:16:06.2064917+00:00 | 3 | 8.0 MiB / 8.0 MiB | 430.5 MB/s | 4/9 | 351,641 | 554.2s / 1,175,672 msg/s |
| Dekaf | 2026-07-19T03:16:15.208576+00:00 | 3 | 8.0 MiB / 6.8 MiB | 430.5 MB/s | 4/9 | 361,161 | 563.2s / 1,089,314 msg/s |
| Dekaf | 2026-07-19T03:16:24.2119425+00:00 | 3 | 8.0 MiB / 4.3 MiB | 430.5 MB/s | 4/9 | 367,980 | 572.2s / 1,157,541 msg/s |
| Dekaf | 2026-07-19T03:16:34.2142456+00:00 | 1 | 10.0 MiB / 1.2 MiB | 416.9 MB/s | 6/4 | 41,581 | 582.2s / 1,169,997 msg/s |
| Dekaf | 2026-07-19T03:16:43.2186444+00:00 | 1 | 10.0 MiB / 9.7 MiB | 416.9 MB/s | 6/4 | 41,897 | 591.2s / 1,029,810 msg/s |
| Dekaf | 2026-07-19T03:16:52.2219884+00:00 | 1 | 10.0 MiB / 4.7 MiB | 416.9 MB/s | 6/4 | 42,170 | 600.2s / 1,161,350 msg/s |
| Dekaf | 2026-07-19T03:17:01.2257449+00:00 | 1 | 10.0 MiB / 9.0 MiB | 416.9 MB/s | 6/4 | 42,744 | 609.2s / 1,214,976 msg/s |
| Dekaf | 2026-07-19T03:17:10.2262395+00:00 | 1 | 11.0 MiB / 6.9 MiB | 416.9 MB/s | 6/4 | 42,894 | 618.2s / 1,136,319 msg/s |
| Dekaf | 2026-07-19T03:17:19.2307613+00:00 | 2 | 9.0 MiB / 9.0 MiB | 417.8 MB/s | 8/8 | 63,676 | 627.2s / 1,168,510 msg/s |
| Dekaf | 2026-07-19T03:17:28.2343664+00:00 | 2 | 9.0 MiB / 2.3 MiB | 421.7 MB/s | 8/8 | 64,621 | 636.3s / 1,051,084 msg/s |
| Dekaf | 2026-07-19T03:17:37.2354454+00:00 | 2 | 9.0 MiB / 9.0 MiB | 421.7 MB/s | 8/8 | 66,264 | 645.3s / 1,008,925 msg/s |
| Dekaf | 2026-07-19T03:17:46.2376602+00:00 | 2 | 7.0 MiB / 4.8 MiB | 421.7 MB/s | 8/8 | 68,203 | 654.3s / 1,128,102 msg/s |
| Dekaf | 2026-07-19T03:17:55.241416+00:00 | 3 | 8.0 MiB / 8.0 MiB | 433.9 MB/s | 4/9 | 455,236 | 663.3s / 1,139,076 msg/s |
| Dekaf | 2026-07-19T03:18:04.249218+00:00 | 3 | 8.0 MiB / 7.7 MiB | 433.9 MB/s | 4/9 | 464,584 | 672.3s / 937,611 msg/s |
| Dekaf | 2026-07-19T03:18:13.2496816+00:00 | 3 | 8.0 MiB / 8.0 MiB | 433.9 MB/s | 4/9 | 474,704 | 681.3s / 1,153,014 msg/s |
| Dekaf | 2026-07-19T03:18:22.254309+00:00 | 3 | 8.0 MiB / 7.9 MiB | 433.9 MB/s | 4/9 | 483,946 | 690.3s / 792,461 msg/s |
| Dekaf | 2026-07-19T03:18:32.2584545+00:00 | 1 | 10.0 MiB / 9.9 MiB | 416.9 MB/s | 6/6 | 44,939 | 700.3s / 1,055,619 msg/s |
| Dekaf | 2026-07-19T03:18:41.2624403+00:00 | 1 | 10.0 MiB / 3.8 MiB | 416.9 MB/s | 6/6 | 45,321 | 709.3s / 969,249 msg/s |
| Dekaf | 2026-07-19T03:18:50.2624217+00:00 | 1 | 10.0 MiB / 8.0 MiB | 416.9 MB/s | 6/6 | 46,118 | 718.3s / 698,702 msg/s |
| Dekaf | 2026-07-19T03:18:59.2642533+00:00 | 1 | 10.0 MiB / 4.0 MiB | 416.9 MB/s | 6/6 | 46,392 | 727.3s / 1,017,260 msg/s |
| Dekaf | 2026-07-19T03:19:08.2667621+00:00 | 2 | 9.0 MiB / 6.5 MiB | 421.7 MB/s | 8/9 | 85,451 | 736.3s / 1,152,761 msg/s |
| Dekaf | 2026-07-19T03:19:17.2684037+00:00 | 2 | 9.0 MiB / 3.2 MiB | 421.7 MB/s | 8/9 | 86,524 | 745.3s / 1,131,694 msg/s |
| Dekaf | 2026-07-19T03:19:26.2719578+00:00 | 2 | 9.0 MiB / 8.9 MiB | 421.7 MB/s | 8/9 | 88,131 | 754.3s / 1,149,319 msg/s |
| Dekaf | 2026-07-19T03:19:35.2733323+00:00 | 2 | 9.0 MiB / 8.4 MiB | 421.7 MB/s | 8/9 | 89,741 | 763.3s / 1,071,706 msg/s |
| Dekaf | 2026-07-19T03:19:44.2748881+00:00 | 3 | 6.0 MiB / 6.0 MiB | 433.9 MB/s | 5/10 | 547,930 | 772.3s / 1,111,556 msg/s |
| Dekaf | 2026-07-19T03:19:53.276058+00:00 | 3 | 6.0 MiB / 4.1 MiB | 433.9 MB/s | 5/10 | 556,266 | 781.3s / 1,138,126 msg/s |
| Dekaf | 2026-07-19T03:20:02.2794758+00:00 | 3 | 7.0 MiB / 6.1 MiB | 433.9 MB/s | 6/10 | 564,246 | 790.3s / 1,166,566 msg/s |
| Dekaf | 2026-07-19T03:20:11.2826688+00:00 | 3 | 7.0 MiB / 7.0 MiB | 433.9 MB/s | 6/10 | 572,704 | 799.3s / 1,028,953 msg/s |
| Dekaf | 2026-07-19T03:20:21.2858798+00:00 | 1 | 11.0 MiB / 3.9 MiB | 416.9 MB/s | 7/6 | 49,896 | 809.3s / 1,111,511 msg/s |
| Dekaf | 2026-07-19T03:20:30.2910012+00:00 | 1 | 11.0 MiB / 4.8 MiB | 416.9 MB/s | 7/6 | 49,896 | 818.3s / 1,197,979 msg/s |
| Dekaf | 2026-07-19T03:20:39.2957777+00:00 | 1 | 11.0 MiB / 4.6 MiB | 416.9 MB/s | 7/6 | 49,902 | 827.3s / 999,983 msg/s |
| Dekaf | 2026-07-19T03:20:48.2995099+00:00 | 1 | 11.0 MiB / 5.1 MiB | 416.9 MB/s | 7/6 | 50,564 | 836.3s / 965,600 msg/s |
| Dekaf | 2026-07-19T03:20:57.3102651+00:00 | 2 | 7.0 MiB / 2.6 MiB | 421.7 MB/s | 9/10 | 104,097 | 845.3s / 1,172,601 msg/s |
| Dekaf | 2026-07-19T03:21:06.3120475+00:00 | 2 | 7.0 MiB / 5.4 MiB | 421.7 MB/s | 9/11 | 105,763 | 854.4s / 1,150,095 msg/s |
| Dekaf | 2026-07-19T03:21:15.3180045+00:00 | 2 | 7.0 MiB / 6.5 MiB | 421.7 MB/s | 9/11 | 107,756 | 863.4s / 1,186,362 msg/s |
| Dekaf | 2026-07-19T03:21:24.3254157+00:00 | 2 | 7.0 MiB / 3.9 MiB | 421.7 MB/s | 9/11 | 109,383 | 872.4s / 1,165,782 msg/s |
| Dekaf | 2026-07-19T03:21:33.3309987+00:00 | 3 | 7.0 MiB / 6.4 MiB | 433.9 MB/s | 9/10 | 646,718 | 881.4s / 1,087,205 msg/s |
| Dekaf | 2026-07-19T03:21:42.3336521+00:00 | 3 | 7.0 MiB / 6.1 MiB | 433.9 MB/s | 9/10 | 653,477 | 890.4s / 1,150,428 msg/s |
| Dekaf | 2026-07-19T03:21:51.3381259+00:00 | 3 | 6.0 MiB / 5.1 MiB | 433.9 MB/s | 9/10 | 660,197 | 899.4s / 1,117,357 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-19T03:07:22.2089511+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-19T03:07:22.3236246+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-19T03:07:22.4072669+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:07:37.2731182+00:00 | 2 | capacity | succeeded | 15,064ms | 14.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-19T03:07:37.3706478+00:00 | 1 | capacity | succeeded | 15,047ms | 14.0 MiB / 11.2 MiB |
| Dekaf | 2026-07-19T03:07:40.2919122+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 8.2 MiB |
| Dekaf | 2026-07-19T03:07:40.382752+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:07:40.4878886+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-19T03:07:55.3663878+00:00 | 2 | capacity | succeeded | 15,074ms | 12.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:07:55.4391705+00:00 | 1 | capacity | succeeded | 15,056ms | 12.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-19T03:07:55.5508116+00:00 | 3 | capacity | succeeded | 15,062ms | 12.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-19T03:07:58.3729453+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-19T03:07:58.4457127+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-19T03:07:58.5612874+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 11.2 MiB |
| Dekaf | 2026-07-19T03:08:13.4959541+00:00 | 1 | capacity | succeeded | 15,050ms | 10.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-19T03:08:13.6094538+00:00 | 3 | capacity | succeeded | 15,048ms | 10.0 MiB / 8.7 MiB |
| Dekaf | 2026-07-19T03:08:16.4372558+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 6.0 MiB |
| Dekaf | 2026-07-19T03:08:16.5009321+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-19T03:08:16.6196+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-19T03:08:31.4805646+00:00 | 2 | capacity | failed | 15,042ms | 10.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-19T03:08:31.550679+00:00 | 1 | capacity | failed | 15,049ms | 10.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-19T03:08:31.7095273+00:00 | 3 | capacity | failed | 15,090ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:09:01.8114003+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-19T03:09:19.8531619+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:09:31.7038312+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:09:31.7901636+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-19T03:09:34.9027876+00:00 | 3 | capacity | failed | 15,049ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-19T03:09:35.811858+00:00 | 1 | capacity | failed | 4,021ms | 10.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-19T03:09:46.7447653+00:00 | 2 | capacity | failed | 15,040ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:10:05.0039344+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:10:16.8463873+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-19T03:10:20.0587543+00:00 | 3 | capacity | failed | 15,055ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:10:34.9158076+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-19T03:10:37.9277345+00:00 | 2 | capacity | failed | 3,011ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-19T03:10:50.2246815+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:11:05.2793468+00:00 | 3 | capacity | failed | 15,054ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:11:08.0292053+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-19T03:11:23.0783878+00:00 | 2 | capacity | failed | 15,049ms | 8.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-19T03:11:35.3733815+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:11:36.3010398+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:11:50.4235458+00:00 | 3 | capacity | failed | 15,050ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:11:53.2141084+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-19T03:11:54.3650587+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:12:08.2542935+00:00 | 2 | capacity | succeeded | 15,040ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-19T03:12:09.4355279+00:00 | 1 | capacity | succeeded | 15,070ms | 9.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-19T03:12:20.5272421+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-19T03:12:35.5701629+00:00 | 3 | capacity | failed | 15,042ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:12:38.3644959+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-19T03:12:39.5288477+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-19T03:12:53.411076+00:00 | 2 | capacity | succeeded | 15,046ms | 7.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-19T03:12:56.4246961+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-19T03:13:05.7137054+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:13:09.4705097+00:00 | 2 | capacity | failed | 13,045ms | 7.0 MiB / 7.0 MiB |
| Dekaf | 2026-07-19T03:13:20.759559+00:00 | 3 | capacity | failed | 15,045ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:13:39.5580187+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-19T03:13:41.5644179+00:00 | 2 | capacity | failed | 2,006ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:13:50.8562972+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 8.0 MiB |
| Dekaf | 2026-07-19T03:13:54.8170251+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:14:05.908184+00:00 | 3 | capacity | failed | 15,053ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-19T03:14:11.6718266+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-19T03:14:26.7445447+00:00 | 2 | capacity | failed | 15,072ms | 7.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-19T03:14:35.9805555+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:14:51.0222926+00:00 | 3 | capacity | failed | 15,041ms | 8.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-19T03:14:56.8211111+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-19T03:15:11.8595857+00:00 | 2 | capacity | succeeded | 15,038ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-19T03:15:41.9664399+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-19T03:15:57.0085946+00:00 | 2 | capacity | succeeded | 15,042ms | 9.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-19T03:16:10.2849418+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-19T03:16:27.08994+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-19T03:16:42.1381439+00:00 | 2 | capacity | failed | 15,048ms | 9.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-19T03:16:55.4331505+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-19T03:17:10.4804335+00:00 | 1 | capacity | failed | 15,047ms | 10.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-19T03:17:40.5711137+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-19T03:17:42.393112+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-19T03:17:55.6188546+00:00 | 1 | capacity | failed | 15,047ms | 10.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-19T03:17:57.4396157+00:00 | 2 | capacity | failed | 15,046ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-19T03:18:51.852317+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-19T03:19:23.4385439+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:19:38.4871098+00:00 | 3 | capacity | succeeded | 15,048ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:19:41.4947884+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-19T03:19:56.0595298+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-19T03:19:56.5433073+00:00 | 3 | capacity | succeeded | 15,048ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:19:57.8900935+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-19T03:19:59.5536028+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:20:11.1138123+00:00 | 1 | capacity | succeeded | 15,054ms | 11.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-19T03:20:12.9532725+00:00 | 2 | capacity | succeeded | 15,063ms | 7.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-19T03:20:15.959861+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-19T03:20:18.4722566+00:00 | 2 | capacity | failed | 2,512ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:20:41.2765258+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:20:44.7344579+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:20:48.5683994+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:20:56.3161667+00:00 | 1 | capacity | succeeded | 15,040ms | 9.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-19T03:20:59.3256953+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-19T03:20:59.7806544+00:00 | 3 | capacity | succeeded | 15,046ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:21:02.7876677+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-19T03:21:14.3854743+00:00 | 1 | capacity | succeeded | 15,059ms | 8.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-19T03:21:17.397895+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:21:17.8389653+00:00 | 3 | capacity | succeeded | 15,051ms | 7.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-19T03:21:32.4422165+00:00 | 1 | capacity | failed | 15,044ms | 8.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:21:47.9321702+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
*11 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 6 |
| Dekaf | 1 | 0.002–0.004ms | 13 |
| Dekaf | 1 | 0.004–0.008ms | 42 |
| Dekaf | 1 | 0.008–0.016ms | 158 |
| Dekaf | 1 | 0.016–0.032ms | 260 |
| Dekaf | 1 | 0.032–0.064ms | 427 |
| Dekaf | 1 | 0.064–0.128ms | 547 |
| Dekaf | 1 | 0.128–0.256ms | 998 |
| Dekaf | 1 | 0.256–0.512ms | 1,966 |
| Dekaf | 1 | 0.512–1.024ms | 3,282 |
| Dekaf | 1 | 1.024–2.048ms | 2,935 |
| Dekaf | 1 | 2.048–4.096ms | 1,484 |
| Dekaf | 1 | 4.096–8.192ms | 709 |
| Dekaf | 1 | 8.192–16.384ms | 287 |
| Dekaf | 1 | 16.384–32.768ms | 72 |
| Dekaf | 1 | 32.768–65.536ms | 10 |
| Dekaf | 1 | 65.536–131.072ms | 2 |
| Dekaf | 2 | 0.001–0.002ms | 32 |
| Dekaf | 2 | 0.002–0.004ms | 29 |
| Dekaf | 2 | 0.004–0.008ms | 107 |
| Dekaf | 2 | 0.008–0.016ms | 272 |
| Dekaf | 2 | 0.016–0.032ms | 610 |
| Dekaf | 2 | 0.032–0.064ms | 922 |
| Dekaf | 2 | 0.064–0.128ms | 1,234 |
| Dekaf | 2 | 0.128–0.256ms | 2,269 |
| Dekaf | 2 | 0.256–0.512ms | 4,610 |
| Dekaf | 2 | 0.512–1.024ms | 7,244 |
| Dekaf | 2 | 1.024–2.048ms | 6,126 |
| Dekaf | 2 | 2.048–4.096ms | 2,878 |
| Dekaf | 2 | 4.096–8.192ms | 1,195 |
| Dekaf | 2 | 8.192–16.384ms | 257 |
| Dekaf | 2 | 16.384–32.768ms | 43 |
| Dekaf | 2 | 32.768–65.536ms | 5 |
| Dekaf | 3 | 0.001–0.002ms | 83 |
| Dekaf | 3 | 0.002–0.004ms | 120 |
| Dekaf | 3 | 0.004–0.008ms | 404 |
| Dekaf | 3 | 0.008–0.016ms | 1,108 |
| Dekaf | 3 | 0.016–0.032ms | 2,724 |
| Dekaf | 3 | 0.032–0.064ms | 4,382 |
| Dekaf | 3 | 0.064–0.128ms | 5,695 |
| Dekaf | 3 | 0.128–0.256ms | 11,161 |
| Dekaf | 3 | 0.256–0.512ms | 23,817 |
| Dekaf | 3 | 0.512–1.024ms | 43,170 |
| Dekaf | 3 | 1.024–2.048ms | 36,921 |
| Dekaf | 3 | 2.048–4.096ms | 15,040 |
| Dekaf | 3 | 4.096–8.192ms | 6,204 |
| Dekaf | 3 | 8.192–16.384ms | 1,685 |
| Dekaf | 3 | 16.384–32.768ms | 371 |
| Dekaf | 3 | 32.768–65.536ms | 22 |
| Dekaf | 3 | 65.536–131.072ms | 2 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 34,000 | 2026-07-19T03:06:52.1396365+00:00 | 141.0ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 36,000 | 2026-07-19T03:06:52.1473618+00:00 | 133.2ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 44,000 | 2026-07-19T03:06:52.1705878+00:00 | 163.2ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 47,000 | 2026-07-19T03:06:52.1803886+00:00 | 192.3ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 52,000 | 2026-07-19T03:06:52.1918335+00:00 | 120.8ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 54,000 | 2026-07-19T03:06:52.1971041+00:00 | 176.1ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 56,000 | 2026-07-19T03:06:52.2015678+00:00 | 171.6ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 64,000 | 2026-07-19T03:06:52.2194787+00:00 | 225.7ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 65,000 | 2026-07-19T03:06:52.2217233+00:00 | 137.9ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 67,000 | 2026-07-19T03:06:52.225057+00:00 | 220.1ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 76,000 | 2026-07-19T03:06:52.2501045+00:00 | 229.5ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 82,000 | 2026-07-19T03:06:52.2901714+00:00 | 115.5ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 104,000 | 2026-07-19T03:06:52.3890428+00:00 | 260.9ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 106,000 | 2026-07-19T03:06:52.3919105+00:00 | 258.0ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 116,000 | 2026-07-19T03:06:52.4545344+00:00 | 245.2ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 156,000 | 2026-07-19T03:06:52.6604534+00:00 | 184.2ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 197,000 | 2026-07-19T03:06:52.808533+00:00 | 174.0ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 204,000 | 2026-07-19T03:06:52.848023+00:00 | 180.0ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 205,000 | 2026-07-19T03:06:52.8493389+00:00 | 134.4ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 208,000 | 2026-07-19T03:06:52.8531935+00:00 | 130.5ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 211,000 | 2026-07-19T03:06:52.887502+00:00 | 110.6ms | GC pause | - | - | 1.0s / 249,127 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 214,000 | 2026-07-19T03:06:52.8903941+00:00 | 161.8ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 218,000 | 2026-07-19T03:06:52.8973167+00:00 | 134.0ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 222,000 | 2026-07-19T03:06:52.9402589+00:00 | 100.3ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 225,000 | 2026-07-19T03:06:52.9459523+00:00 | 110.5ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 232,000 | 2026-07-19T03:06:52.9610666+00:00 | 122.6ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 234,000 | 2026-07-19T03:06:52.9649102+00:00 | 176.5ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 241,000 | 2026-07-19T03:06:52.9805857+00:00 | 128.7ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 246,000 | 2026-07-19T03:06:52.9911919+00:00 | 169.6ms | GC pause | - | - | 2.0s / 302,758 msg/s | Gen2 +2 / pause +11.2ms |
| Dekaf | 256,000 | 2026-07-19T03:06:53.0338062+00:00 | 145.9ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 262,000 | 2026-07-19T03:06:53.0597967+00:00 | 101.1ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 264,000 | 2026-07-19T03:06:53.064209+00:00 | 207.5ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 298,000 | 2026-07-19T03:06:53.1824928+00:00 | 130.0ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 304,000 | 2026-07-19T03:06:53.1981651+00:00 | 222.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 305,000 | 2026-07-19T03:06:53.1999158+00:00 | 138.8ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 307,000 | 2026-07-19T03:06:53.2730407+00:00 | 147.7ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 316,000 | 2026-07-19T03:06:53.303379+00:00 | 167.7ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 317,000 | 2026-07-19T03:06:53.306476+00:00 | 164.5ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 324,000 | 2026-07-19T03:06:53.3218591+00:00 | 180.4ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 325,000 | 2026-07-19T03:06:53.3233496+00:00 | 105.1ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 331,000 | 2026-07-19T03:06:53.3316634+00:00 | 119.0ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 332,000 | 2026-07-19T03:06:53.3380738+00:00 | 112.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 336,000 | 2026-07-19T03:06:53.3450648+00:00 | 214.5ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 354,000 | 2026-07-19T03:06:53.4332059+00:00 | 157.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 356,000 | 2026-07-19T03:06:53.441607+00:00 | 149.2ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 357,000 | 2026-07-19T03:06:53.4425536+00:00 | 148.3ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 364,000 | 2026-07-19T03:06:53.472114+00:00 | 135.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 374,000 | 2026-07-19T03:06:53.5060992+00:00 | 117.0ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 376,000 | 2026-07-19T03:06:53.5076338+00:00 | 124.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 386,000 | 2026-07-19T03:06:53.5602965+00:00 | 101.3ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 387,000 | 2026-07-19T03:06:53.560998+00:00 | 136.0ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 396,000 | 2026-07-19T03:06:53.5923367+00:00 | 108.1ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 407,000 | 2026-07-19T03:06:53.6075654+00:00 | 115.8ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 414,000 | 2026-07-19T03:06:53.6210105+00:00 | 136.1ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 427,000 | 2026-07-19T03:06:53.6418793+00:00 | 208.5ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 434,000 | 2026-07-19T03:06:53.6663065+00:00 | 203.4ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 436,000 | 2026-07-19T03:06:53.6944619+00:00 | 175.2ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 437,000 | 2026-07-19T03:06:53.6948075+00:00 | 177.4ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 444,000 | 2026-07-19T03:06:53.703565+00:00 | 208.7ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 446,000 | 2026-07-19T03:06:53.7049195+00:00 | 207.3ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 447,000 | 2026-07-19T03:06:53.7056779+00:00 | 204.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 457,000 | 2026-07-19T03:06:53.7241276+00:00 | 212.2ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 462,000 | 2026-07-19T03:06:53.7418175+00:00 | 105.6ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 465,000 | 2026-07-19T03:06:53.7451905+00:00 | 114.8ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 486,000 | 2026-07-19T03:06:53.8708026+00:00 | 117.8ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 487,000 | 2026-07-19T03:06:53.8710945+00:00 | 102.3ms | throughput collapse | - | - | 2.0s / 302,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 586,000 | 2026-07-19T03:06:54.0629772+00:00 | 105.7ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 597,000 | 2026-07-19T03:06:54.0853439+00:00 | 125.4ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 606,000 | 2026-07-19T03:06:54.1074191+00:00 | 119.5ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 616,000 | 2026-07-19T03:06:54.128414+00:00 | 118.6ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 617,000 | 2026-07-19T03:06:54.1299707+00:00 | 133.3ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 626,000 | 2026-07-19T03:06:54.1385554+00:00 | 122.5ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 654,000 | 2026-07-19T03:06:54.229841+00:00 | 118.0ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 656,000 | 2026-07-19T03:06:54.2319306+00:00 | 115.9ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 674,000 | 2026-07-19T03:06:54.2653964+00:00 | 105.5ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 697,000 | 2026-07-19T03:06:54.3197342+00:00 | 112.5ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 706,000 | 2026-07-19T03:06:54.3361556+00:00 | 104.6ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 726,000 | 2026-07-19T03:06:54.3755392+00:00 | 135.3ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 737,000 | 2026-07-19T03:06:54.4107438+00:00 | 118.2ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 754,000 | 2026-07-19T03:06:54.4426341+00:00 | 112.0ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 756,000 | 2026-07-19T03:06:54.4442763+00:00 | 110.3ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 757,000 | 2026-07-19T03:06:54.4451499+00:00 | 129.0ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 767,000 | 2026-07-19T03:06:54.4613666+00:00 | 137.0ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 776,000 | 2026-07-19T03:06:54.4887178+00:00 | 134.6ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 786,000 | 2026-07-19T03:06:54.5169001+00:00 | 157.2ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 787,000 | 2026-07-19T03:06:54.528018+00:00 | 124.8ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 834,000 | 2026-07-19T03:06:54.6556363+00:00 | 102.2ms | throughput collapse | - | - | 3.0s / 466,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,027,000 | 2026-07-19T03:06:55.0276764+00:00 | 161.6ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,029,000 | 2026-07-19T03:06:55.0291838+00:00 | 139.6ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,037,000 | 2026-07-19T03:06:55.0410368+00:00 | 178.7ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,042,000 | 2026-07-19T03:06:55.0558418+00:00 | 106.7ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,049,000 | 2026-07-19T03:06:55.1026391+00:00 | 153.7ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,050,000 | 2026-07-19T03:06:55.1057894+00:00 | 149.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,051,000 | 2026-07-19T03:06:55.1066312+00:00 | 100.5ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,054,000 | 2026-07-19T03:06:55.1306666+00:00 | 112.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,056,000 | 2026-07-19T03:06:55.1334672+00:00 | 109.4ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,064,000 | 2026-07-19T03:06:55.170827+00:00 | 109.9ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,066,000 | 2026-07-19T03:06:55.172014+00:00 | 108.7ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,070,000 | 2026-07-19T03:06:55.1738814+00:00 | 133.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,074,000 | 2026-07-19T03:06:55.1914245+00:00 | 103.6ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,076,000 | 2026-07-19T03:06:55.1926933+00:00 | 113.1ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,149,000 | 2026-07-19T03:06:55.3593268+00:00 | 111.0ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,169,000 | 2026-07-19T03:06:55.4070281+00:00 | 106.9ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,183,000 | 2026-07-19T03:06:55.4354534+00:00 | 100.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,184,000 | 2026-07-19T03:06:55.4361704+00:00 | 105.8ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,189,000 | 2026-07-19T03:06:55.4423468+00:00 | 123.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,204,000 | 2026-07-19T03:06:55.4898852+00:00 | 127.6ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,213,000 | 2026-07-19T03:06:55.4947704+00:00 | 106.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,226,000 | 2026-07-19T03:06:55.5339323+00:00 | 144.1ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,227,000 | 2026-07-19T03:06:55.5348461+00:00 | 142.8ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,233,000 | 2026-07-19T03:06:55.5438256+00:00 | 123.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,240,000 | 2026-07-19T03:06:55.5796121+00:00 | 110.2ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,243,000 | 2026-07-19T03:06:55.5832033+00:00 | 106.6ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,376,000 | 2026-07-19T03:06:55.8573641+00:00 | 119.1ms | throughput collapse | - | - | 4.0s / 442,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,494,000 | 2026-07-19T03:06:56.0579994+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,749,000 | 2026-07-19T03:06:56.4865272+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,779,000 | 2026-07-19T03:06:56.549513+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,783,000 | 2026-07-19T03:06:56.5570927+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,789,000 | 2026-07-19T03:06:56.5755157+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,790,000 | 2026-07-19T03:06:56.5811765+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,793,000 | 2026-07-19T03:06:56.5865442+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,800,000 | 2026-07-19T03:06:56.5954445+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,803,000 | 2026-07-19T03:06:56.5994555+00:00 | 124.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 568,237 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,069,000 | 2026-07-19T03:06:57.087378+00:00 | 102.8ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,070,000 | 2026-07-19T03:06:57.0878148+00:00 | 102.3ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,079,000 | 2026-07-19T03:06:57.1103641+00:00 | 107.0ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,083,000 | 2026-07-19T03:06:57.1223005+00:00 | 113.3ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,109,000 | 2026-07-19T03:06:57.1609644+00:00 | 104.4ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,230,000 | 2026-07-19T03:06:57.3900023+00:00 | 102.5ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,403,000 | 2026-07-19T03:06:57.7156581+00:00 | 112.7ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,409,000 | 2026-07-19T03:06:57.7308142+00:00 | 113.3ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,430,000 | 2026-07-19T03:06:57.7650923+00:00 | 113.8ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,483,000 | 2026-07-19T03:06:57.8905937+00:00 | 122.8ms | throughput collapse | - | - | 6.0s / 507,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,500,000 | 2026-07-19T03:06:57.9321398+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 641,538 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,503,000 | 2026-07-19T03:06:57.9340056+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 641,538 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,230,000 | 2026-07-19T03:06:59.1000801+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 593,397 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,250,000 | 2026-07-19T03:06:59.1176162+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 593,397 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,517,000 | 2026-07-19T03:06:59.5779949+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 593,397 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,877,000 | 2026-07-19T03:07:00.1359875+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 652,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,147,000 | 2026-07-19T03:07:00.55138+00:00 | 116.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 652,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,154,000 | 2026-07-19T03:07:00.5729591+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 652,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,176,000 | 2026-07-19T03:07:00.6023643+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 652,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,177,000 | 2026-07-19T03:07:00.6045387+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 652,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,187,000 | 2026-07-19T03:07:00.6289646+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 652,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,464,000 | 2026-07-19T03:07:01.0873352+00:00 | 131.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 619,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,487,000 | 2026-07-19T03:07:01.1443479+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 619,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,594,000 | 2026-07-19T03:07:01.3253189+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 619,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,606,000 | 2026-07-19T03:07:01.3490717+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 619,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,617,000 | 2026-07-19T03:07:01.3702228+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 619,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,624,000 | 2026-07-19T03:07:01.3795071+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 619,069 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,096,000 | 2026-07-19T03:07:02.0825547+00:00 | 143.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,097,000 | 2026-07-19T03:07:02.0831732+00:00 | 152.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,100,000 | 2026-07-19T03:07:02.0904795+00:00 | 135.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,110,000 | 2026-07-19T03:07:02.1067845+00:00 | 129.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,114,000 | 2026-07-19T03:07:02.1096548+00:00 | 167.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,120,000 | 2026-07-19T03:07:02.1194046+00:00 | 133.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,137,000 | 2026-07-19T03:07:02.2045485+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,343,000 | 2026-07-19T03:07:02.5725663+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,349,000 | 2026-07-19T03:07:02.5795344+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,459,000 | 2026-07-19T03:07:02.7869802+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,460,000 | 2026-07-19T03:07:02.7874205+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,473,000 | 2026-07-19T03:07:02.8030851+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,479,000 | 2026-07-19T03:07:02.8177076+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,480,000 | 2026-07-19T03:07:02.8195534+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,489,000 | 2026-07-19T03:07:02.8248236+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 557,193 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,593,000 | 2026-07-19T03:07:03.0096939+00:00 | 100.7ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,596,000 | 2026-07-19T03:07:03.0119283+00:00 | 118.9ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,600,000 | 2026-07-19T03:07:03.0182693+00:00 | 111.4ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,606,000 | 2026-07-19T03:07:03.0251001+00:00 | 116.8ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,610,000 | 2026-07-19T03:07:03.0291818+00:00 | 227.2ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,613,000 | 2026-07-19T03:07:03.0422432+00:00 | 213.6ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,614,000 | 2026-07-19T03:07:03.0433957+00:00 | 178.8ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,616,000 | 2026-07-19T03:07:03.0452962+00:00 | 176.9ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,618,000 | 2026-07-19T03:07:03.0530784+00:00 | 106.0ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,620,000 | 2026-07-19T03:07:03.0547453+00:00 | 210.6ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,622,000 | 2026-07-19T03:07:03.0561981+00:00 | 156.0ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,626,000 | 2026-07-19T03:07:03.058305+00:00 | 197.5ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,630,000 | 2026-07-19T03:07:03.060074+00:00 | 226.9ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,632,000 | 2026-07-19T03:07:03.0886028+00:00 | 130.4ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,644,000 | 2026-07-19T03:07:03.1189566+00:00 | 174.8ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,646,000 | 2026-07-19T03:07:03.1319733+00:00 | 174.0ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,649,000 | 2026-07-19T03:07:03.1331502+00:00 | 195.0ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,652,000 | 2026-07-19T03:07:03.1431987+00:00 | 130.9ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,653,000 | 2026-07-19T03:07:03.1436462+00:00 | 186.7ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,700,000 | 2026-07-19T03:07:03.3354283+00:00 | 109.3ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,710,000 | 2026-07-19T03:07:03.355069+00:00 | 101.3ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,720,000 | 2026-07-19T03:07:03.3688093+00:00 | 104.5ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,803,000 | 2026-07-19T03:07:03.5284228+00:00 | 104.4ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,819,000 | 2026-07-19T03:07:03.5698446+00:00 | 113.1ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,823,000 | 2026-07-19T03:07:03.5730835+00:00 | 122.9ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,850,000 | 2026-07-19T03:07:03.6286142+00:00 | 106.0ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,863,000 | 2026-07-19T03:07:03.6483111+00:00 | 109.6ms | throughput collapse | - | - | 12.0s / 494,825 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,521,000 | 2026-07-19T03:07:04.6050806+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 660,721 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,525,000 | 2026-07-19T03:07:04.6157291+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 660,721 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,540,000 | 2026-07-19T03:07:04.654668+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 660,721 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,553,000 | 2026-07-19T03:07:04.6931981+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 660,721 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,826,000 | 2026-07-19T03:07:05.1191259+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,827,000 | 2026-07-19T03:07:05.1196491+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,828,000 | 2026-07-19T03:07:05.1201323+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,000,000 | 2026-07-19T03:07:05.4083806+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,060,000 | 2026-07-19T03:07:05.5348778+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,089,000 | 2026-07-19T03:07:05.5779245+00:00 | 140.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,090,000 | 2026-07-19T03:07:05.5786181+00:00 | 139.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,103,000 | 2026-07-19T03:07:05.62113+00:00 | 132.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,109,000 | 2026-07-19T03:07:05.635893+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,110,000 | 2026-07-19T03:07:05.636898+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 609,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,060,000 | 2026-07-19T03:07:06.9642706+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,063,000 | 2026-07-19T03:07:06.9662029+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,066,000 | 2026-07-19T03:07:06.9684339+00:00 | 125.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,093,000 | 2026-07-19T03:07:07.0434808+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,096,000 | 2026-07-19T03:07:07.0443501+00:00 | 149.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,103,000 | 2026-07-19T03:07:07.0495411+00:00 | 117.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,107,000 | 2026-07-19T03:07:07.0520506+00:00 | 158.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,113,000 | 2026-07-19T03:07:07.0822581+00:00 | 153.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,114,000 | 2026-07-19T03:07:07.0935634+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,120,000 | 2026-07-19T03:07:07.1002467+00:00 | 141.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,123,000 | 2026-07-19T03:07:07.1033452+00:00 | 153.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,126,000 | 2026-07-19T03:07:07.1179636+00:00 | 117.4ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,130,000 | 2026-07-19T03:07:07.1215918+00:00 | 129.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,134,000 | 2026-07-19T03:07:07.1229101+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,136,000 | 2026-07-19T03:07:07.1238831+00:00 | 119.9ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,139,000 | 2026-07-19T03:07:07.1367063+00:00 | 140.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,570,000 | 2026-07-19T03:07:07.8497905+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,579,000 | 2026-07-19T03:07:07.858055+00:00 | 131.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,583,000 | 2026-07-19T03:07:07.8610109+00:00 | 139.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,590,000 | 2026-07-19T03:07:07.866634+00:00 | 143.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 559,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,744,000 | 2026-07-19T03:07:10.6326426+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 848,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,747,000 | 2026-07-19T03:07:10.6344809+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 848,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,303,000 | 2026-07-19T03:07:19.6240212+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 846,550 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,309,000 | 2026-07-19T03:07:19.6288867+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 846,550 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,689,000 | 2026-07-19T03:07:20.1000911+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 812,272 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,050,000 | 2026-07-19T03:07:28.6331843+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 37.0s / 852,869 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,585,000 | 2026-07-19T03:07:32.6216758+00:00 | 105.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 849,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,591,000 | 2026-07-19T03:07:32.632025+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 849,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,592,000 | 2026-07-19T03:07:32.6330524+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 41.0s / 849,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,750,000 | 2026-07-19T03:07:48.6425896+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 57.0s / 922,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 122,586,000 | 2026-07-19T03:09:07.5969511+00:00 | 116.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 136.1s / 852,274 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 122,594,000 | 2026-07-19T03:09:07.6117739+00:00 | 123.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 136.1s / 852,274 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 122,595,000 | 2026-07-19T03:09:07.6122903+00:00 | 109.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 136.1s / 852,274 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 122,596,000 | 2026-07-19T03:09:07.6136404+00:00 | 121.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 136.1s / 852,274 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 122,605,000 | 2026-07-19T03:09:07.6331999+00:00 | 114.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 136.1s / 852,274 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 122,607,000 | 2026-07-19T03:09:07.6340837+00:00 | 118.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 136.1s / 852,274 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 150,969,000 | 2026-07-19T03:09:35.1354249+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed | - | 164.1s / 734,822 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 150,970,000 | 2026-07-19T03:09:35.1418481+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed | - | 164.1s / 734,822 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 593,795,000 | 2026-07-19T03:16:37.1240462+00:00 | 106.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 585.2s / 998,234 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 646,954,000 | 2026-07-19T03:17:26.1041212+00:00 | 124.2ms | broker/backlog (no scale or GC event) | - | - | 634.3s / 869,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 646,956,000 | 2026-07-19T03:17:26.1069926+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 634.3s / 869,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 646,967,000 | 2026-07-19T03:17:26.1233408+00:00 | 126.7ms | broker/backlog (no scale or GC event) | - | - | 634.3s / 869,271 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,036,000 | 2026-07-19T03:18:48.5965719+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 717.3s / 736,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,046,000 | 2026-07-19T03:18:48.6096646+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 717.3s / 736,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,056,000 | 2026-07-19T03:18:48.6255079+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 717.3s / 736,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,057,000 | 2026-07-19T03:18:48.6258471+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 717.3s / 736,554 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 912,425,000 | 2026-07-19T03:21:36.13073+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 884.4s / 982,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 922,330,000 | 2026-07-19T03:21:46.2342563+00:00 | 130.9ms | broker/backlog (no scale or GC event) | - | - | 894.4s / 875,203 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 922,339,000 | 2026-07-19T03:21:46.2395192+00:00 | 128.9ms | broker/backlog (no scale or GC event) | - | - | 894.4s / 875,203 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 922,343,000 | 2026-07-19T03:21:46.2444115+00:00 | 131.3ms | broker/backlog (no scale or GC event) | - | - | 894.4s / 875,203 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 326,549,000 | 2026-07-19T03:28:25.166788+00:00 | 104.9ms | GC pause | - | - | 393.2s / 769,382 msg/s | Gen2 +0 / pause +56.5ms |
| Confluent | 510,714,000 | 2026-07-19T03:32:00.6435693+00:00 | 105.5ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,715,000 | 2026-07-19T03:32:00.644047+00:00 | 103.6ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,716,000 | 2026-07-19T03:32:00.6445294+00:00 | 103.2ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,717,000 | 2026-07-19T03:32:00.6450642+00:00 | 108.4ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,718,000 | 2026-07-19T03:32:00.6467273+00:00 | 106.7ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,719,000 | 2026-07-19T03:32:00.6473171+00:00 | 100.5ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,721,000 | 2026-07-19T03:32:00.6483956+00:00 | 105.2ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,724,000 | 2026-07-19T03:32:00.6513462+00:00 | 115.8ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,725,000 | 2026-07-19T03:32:00.6518623+00:00 | 102.0ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,729,000 | 2026-07-19T03:32:00.6638333+00:00 | 103.1ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,734,000 | 2026-07-19T03:32:00.6754488+00:00 | 100.7ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,747,000 | 2026-07-19T03:32:00.6847844+00:00 | 112.0ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,748,000 | 2026-07-19T03:32:00.6853836+00:00 | 111.4ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,751,000 | 2026-07-19T03:32:00.6886434+00:00 | 108.3ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,754,000 | 2026-07-19T03:32:00.6903638+00:00 | 104.0ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,757,000 | 2026-07-19T03:32:00.6926115+00:00 | 110.5ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,758,000 | 2026-07-19T03:32:00.6945097+00:00 | 108.6ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,761,000 | 2026-07-19T03:32:00.6980666+00:00 | 108.6ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,767,000 | 2026-07-19T03:32:00.7044549+00:00 | 107.7ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,768,000 | 2026-07-19T03:32:00.7058498+00:00 | 106.4ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 510,771,000 | 2026-07-19T03:32:00.7096875+00:00 | 103.1ms | GC pause | - | - | 609.3s / 1,007,175 msg/s | Gen2 +0 / pause +116.4ms |
| Confluent | 631,564,000 | 2026-07-19T03:34:22.6213396+00:00 | 103.4ms | GC pause | - | - | 751.4s / 547,034 msg/s | Gen2 +0 / pause +348.5ms |
| Confluent | 631,573,000 | 2026-07-19T03:34:22.6371849+00:00 | 102.0ms | GC pause | - | - | 751.4s / 547,034 msg/s | Gen2 +0 / pause +348.5ms |
| Confluent | 631,574,000 | 2026-07-19T03:34:22.6391086+00:00 | 100.5ms | GC pause | - | - | 751.4s / 547,034 msg/s | Gen2 +0 / pause +348.5ms |
| Confluent | 729,265,000 | 2026-07-19T03:36:18.4545641+00:00 | 102.1ms | GC pause | - | - | 866.5s / 593,193 msg/s | Gen2 +0 / pause +207.1ms |
| Confluent | 729,266,000 | 2026-07-19T03:36:18.4553692+00:00 | 101.4ms | GC pause | - | - | 866.5s / 593,193 msg/s | Gen2 +0 / pause +207.1ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*619 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.48x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.23x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,559,329 | 1,556,962–1,561,700 | 0.95 | 1.16x |
| Confluent | 2 | 1,342,394 | 1,331,988–1,352,881 | 1.34 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.77 | 730.27 | 1,663,670 | 1,660,377 | +8.5% | +0.69% | 1586.60 | 1,663,670 | 0 | 1.29 |
| Dekaf (dekaf-first) | 0.95 | 970.35 | 1,548,461 | 1,561,700 | -2.2% | -0.15% | 1476.73 | 1,548,461 | 0 | 1.47 |
| Dekaf (confluent-first) | 0.96 | 985.38 | 1,545,476 | 1,556,962 | +0.2% | -0.00% | 1473.88 | 1,545,476 | 0 | 1.48 |
| Confluent (confluent-first) | 1.33 | - | 1,334,200 | 1,352,881 | +2.0% | +0.23% | 1272.39 | 1,334,200 | 0 | 1.78 |
| Confluent (dekaf-first) | 1.34 | - | 1,315,302 | 1,331,988 | +7.9% | +0.64% | 1254.37 | 1,315,302 | 0 | 1.77 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,355,803 | 1506.43 | 1019.86 KB |
| Dekaf | 1 | 1,361,413 | 1512.66 | 1017.61 KB |
| Dekaf (3conn) | 1 | 1,583,897 | 1759.87 | 939.75 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:39.0588536+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 503,938 msg/s |
| Dekaf | 2026-07-19T03:07:06.0633788+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1683.1 MB/s | 0/0 | 48,378 | 27.0s / 1,548,361 msg/s |
| Dekaf | 2026-07-19T03:07:34.0745305+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1683.1 MB/s | 1/0 | 105,247 | 55.0s / 1,619,692 msg/s |
| Dekaf | 2026-07-19T03:08:01.079394+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1716.1 MB/s | 1/0 | 167,269 | 82.0s / 1,610,146 msg/s |
| Dekaf | 2026-07-19T03:08:28.083396+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1732.7 MB/s | 2/0 | 230,654 | 109.0s / 1,569,345 msg/s |
| Dekaf | 2026-07-19T03:08:55.1015529+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/1 | 290,890 | 136.0s / 1,559,832 msg/s |
| Dekaf | 2026-07-19T03:09:23.1088157+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1741.8 MB/s | 2/1 | 358,498 | 164.1s / 1,529,744 msg/s |
| Dekaf | 2026-07-19T03:09:50.1194863+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1741.8 MB/s | 2/1 | 425,330 | 191.1s / 1,525,745 msg/s |
| Dekaf | 2026-07-19T03:10:17.1259793+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1741.8 MB/s | 2/2 | 485,928 | 218.1s / 1,588,242 msg/s |
| Dekaf | 2026-07-19T03:10:44.1387538+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1741.8 MB/s | 2/2 | 553,314 | 245.1s / 1,590,029 msg/s |
| Dekaf | 2026-07-19T03:11:12.1451159+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1741.8 MB/s | 2/2 | 619,968 | 273.1s / 1,572,715 msg/s |
| Dekaf | 2026-07-19T03:11:39.1545507+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/2 | 685,145 | 300.1s / 1,584,515 msg/s |
| Dekaf | 2026-07-19T03:12:06.1574121+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/2 | 751,198 | 327.1s / 1,592,681 msg/s |
| Dekaf | 2026-07-19T03:12:33.1680522+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/3 | 806,470 | 354.1s / 1,581,933 msg/s |
| Dekaf | 2026-07-19T03:13:01.1730064+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/3 | 871,720 | 382.1s / 1,580,335 msg/s |
| Dekaf | 2026-07-19T03:13:28.1822301+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/3 | 931,244 | 409.1s / 1,588,120 msg/s |
| Dekaf | 2026-07-19T03:13:55.1869554+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1741.8 MB/s | 2/3 | 996,311 | 436.1s / 1,560,632 msg/s |
| Dekaf | 2026-07-19T03:14:22.1916415+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1741.8 MB/s | 2/3 | 1,059,124 | 463.1s / 1,503,494 msg/s |
| Dekaf | 2026-07-19T03:14:50.2041153+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1741.8 MB/s | 2/3 | 1,123,958 | 491.2s / 1,592,951 msg/s |
| Dekaf | 2026-07-19T03:15:17.2142434+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/3 | 1,189,016 | 518.2s / 1,570,009 msg/s |
| Dekaf | 2026-07-19T03:15:44.2213778+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/3 | 1,253,960 | 545.2s / 1,543,879 msg/s |
| Dekaf | 2026-07-19T03:16:12.2276904+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1741.8 MB/s | 2/3 | 1,321,486 | 573.2s / 1,574,772 msg/s |
| Dekaf | 2026-07-19T03:16:39.2385004+00:00 | 1 | 13.0 MiB / 11.4 MiB | 1741.8 MB/s | 2/3 | 1,385,120 | 600.2s / 1,548,788 msg/s |
| Dekaf | 2026-07-19T03:17:06.2456085+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1741.8 MB/s | 2/4 | 1,448,390 | 627.2s / 1,524,666 msg/s |
| Dekaf | 2026-07-19T03:17:33.2500672+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1741.8 MB/s | 2/4 | 1,508,234 | 654.2s / 1,591,702 msg/s |
| Dekaf | 2026-07-19T03:18:01.2583917+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1741.8 MB/s | 2/4 | 1,570,638 | 682.2s / 1,546,918 msg/s |
| Dekaf | 2026-07-19T03:18:28.26247+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1741.8 MB/s | 2/4 | 1,634,308 | 709.2s / 1,498,354 msg/s |
| Dekaf | 2026-07-19T03:18:55.2737673+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1741.8 MB/s | 2/4 | 1,693,349 | 736.2s / 1,525,758 msg/s |
| Dekaf | 2026-07-19T03:19:22.2801323+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1741.8 MB/s | 2/4 | 1,752,083 | 763.2s / 1,558,246 msg/s |
| Dekaf | 2026-07-19T03:19:50.2864707+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1741.8 MB/s | 2/4 | 1,812,116 | 791.2s / 1,506,482 msg/s |
| Dekaf | 2026-07-19T03:20:17.2975942+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1741.8 MB/s | 2/4 | 1,867,053 | 818.3s / 1,490,713 msg/s |
| Dekaf | 2026-07-19T03:20:44.3040521+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1741.8 MB/s | 2/4 | 1,923,146 | 845.3s / 1,517,058 msg/s |
| Dekaf | 2026-07-19T03:21:11.3083951+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1741.8 MB/s | 2/5 | 1,984,768 | 872.3s / 1,562,135 msg/s |
| Dekaf | 2026-07-19T03:51:40.3374397+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 739,019 msg/s |
| Dekaf | 2026-07-19T03:52:07.3450443+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1665.3 MB/s | 0/0 | 56,912 | 27.0s / 1,551,436 msg/s |
| Dekaf | 2026-07-19T03:52:34.3578687+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1693.3 MB/s | 0/1 | 110,569 | 54.0s / 1,517,200 msg/s |
| Dekaf | 2026-07-19T03:53:01.3652581+00:00 | 1 | 16.0 MiB / 15.0 MiB | 1693.3 MB/s | 0/1 | 172,363 | 81.0s / 1,573,478 msg/s |
| Dekaf | 2026-07-19T03:53:29.3765846+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1697.5 MB/s | 0/1 | 230,180 | 109.1s / 1,505,106 msg/s |
| Dekaf | 2026-07-19T03:53:56.3892515+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1697.5 MB/s | 0/2 | 288,695 | 136.1s / 1,560,673 msg/s |
| Dekaf | 2026-07-19T03:54:23.3980632+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1697.5 MB/s | 0/2 | 345,197 | 163.1s / 1,498,660 msg/s |
| Dekaf | 2026-07-19T03:54:51.4083859+00:00 | 1 | 16.0 MiB / 10.5 MiB | 1697.5 MB/s | 0/2 | 402,862 | 191.1s / 1,552,961 msg/s |
| Dekaf | 2026-07-19T03:55:18.4245169+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1697.5 MB/s | 0/2 | 454,843 | 218.1s / 1,556,177 msg/s |
| Dekaf | 2026-07-19T03:55:45.4359189+00:00 | 1 | 16.0 MiB / 14.4 MiB | 1726.9 MB/s | 0/2 | 506,834 | 245.1s / 1,557,493 msg/s |
| Dekaf | 2026-07-19T03:56:12.4502699+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1726.9 MB/s | 1/2 | 563,625 | 272.1s / 1,557,949 msg/s |
| Dekaf | 2026-07-19T03:56:40.46262+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1727.8 MB/s | 1/2 | 625,076 | 300.1s / 1,536,812 msg/s |
| Dekaf | 2026-07-19T03:57:07.471689+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1739.1 MB/s | 2/2 | 690,666 | 327.1s / 1,578,383 msg/s |
| Dekaf | 2026-07-19T03:57:34.4821811+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.1 MB/s | 2/3 | 751,247 | 354.1s / 1,570,420 msg/s |
| Dekaf | 2026-07-19T03:58:01.4897182+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1743.1 MB/s | 2/3 | 816,406 | 381.1s / 1,554,681 msg/s |
| Dekaf | 2026-07-19T03:58:29.4977011+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.1 MB/s | 2/3 | 886,135 | 409.1s / 1,555,564 msg/s |
| Dekaf | 2026-07-19T03:58:56.5066974+00:00 | 1 | 13.0 MiB / 11.6 MiB | 1743.1 MB/s | 3/3 | 945,154 | 436.2s / 1,545,773 msg/s |
| Dekaf | 2026-07-19T03:59:23.5164429+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1743.1 MB/s | 3/3 | 1,005,894 | 463.2s / 1,567,787 msg/s |
| Dekaf | 2026-07-19T03:59:50.5289608+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.1 MB/s | 3/4 | 1,062,170 | 490.2s / 1,534,926 msg/s |
| Dekaf | 2026-07-19T04:00:18.5364697+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.1 MB/s | 4/4 | 1,130,442 | 518.2s / 1,551,616 msg/s |
| Dekaf | 2026-07-19T04:00:45.5458857+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.1 MB/s | 4/4 | 1,198,251 | 545.2s / 1,518,478 msg/s |
| Dekaf | 2026-07-19T04:01:12.5539145+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.1 MB/s | 4/5 | 1,267,962 | 572.2s / 1,577,090 msg/s |
| Dekaf | 2026-07-19T04:01:39.5584024+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.1 MB/s | 4/5 | 1,342,980 | 599.2s / 1,553,873 msg/s |
| Dekaf | 2026-07-19T04:02:07.5668913+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1743.1 MB/s | 4/5 | 1,420,298 | 627.2s / 1,537,506 msg/s |
| Dekaf | 2026-07-19T04:02:34.5819821+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1743.1 MB/s | 5/5 | 1,489,030 | 654.2s / 1,587,863 msg/s |
| Dekaf | 2026-07-19T04:03:01.5897337+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.1 MB/s | 6/5 | 1,557,572 | 681.2s / 1,513,467 msg/s |
| Dekaf | 2026-07-19T04:03:28.6025668+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1743.1 MB/s | 6/5 | 1,620,643 | 708.2s / 1,595,813 msg/s |
| Dekaf | 2026-07-19T04:03:56.6144558+00:00 | 1 | 13.0 MiB / 11.4 MiB | 1743.1 MB/s | 6/6 | 1,681,887 | 736.2s / 1,612,935 msg/s |
| Dekaf | 2026-07-19T04:04:23.628673+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.1 MB/s | 6/6 | 1,746,633 | 763.3s / 1,529,951 msg/s |
| Dekaf | 2026-07-19T04:04:50.6449371+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1743.1 MB/s | 6/6 | 1,814,328 | 790.3s / 1,603,792 msg/s |
| Dekaf | 2026-07-19T04:05:18.654454+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1743.1 MB/s | 7/6 | 1,894,323 | 818.3s / 1,556,032 msg/s |
| Dekaf | 2026-07-19T04:05:45.6638107+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1743.1 MB/s | 7/7 | 1,959,134 | 845.3s / 1,547,650 msg/s |
| Dekaf | 2026-07-19T04:06:12.668297+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.1 MB/s | 7/7 | 2,034,326 | 872.3s / 1,551,431 msg/s |
| Dekaf | 2026-07-19T04:06:39.67406+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1743.1 MB/s | 7/7 | 2,112,412 | 899.3s / 1,575,768 msg/s |
| Dekaf (3conn) | 2026-07-19T04:07:08.1753971+00:00 | 1 | 16.0 MiB / 6.6 MiB | 2224.1 MB/s | 0/0 | 2,963 | 27.0s / 1,644,807 msg/s |
| Dekaf (3conn) | 2026-07-19T04:07:35.1902978+00:00 | 1 | 16.0 MiB / 1.4 MiB | 2224.1 MB/s | 0/1 | 5,421 | 54.0s / 1,398,809 msg/s |
| Dekaf (3conn) | 2026-07-19T04:08:02.2075449+00:00 | 1 | 16.0 MiB / 3.9 MiB | 2224.1 MB/s | 0/1 | 7,413 | 81.0s / 1,612,303 msg/s |
| Dekaf (3conn) | 2026-07-19T04:08:29.2191277+00:00 | 1 | 18.0 MiB / 2.1 MiB | 2224.1 MB/s | 0/1 | 9,179 | 108.1s / 1,642,905 msg/s |
| Dekaf (3conn) | 2026-07-19T04:08:57.2286105+00:00 | 1 | 18.0 MiB / 6.1 MiB | 2224.1 MB/s | 1/1 | 10,750 | 136.1s / 1,528,382 msg/s |
| Dekaf (3conn) | 2026-07-19T04:09:24.2419041+00:00 | 1 | 20.0 MiB / 4.5 MiB | 2224.1 MB/s | 1/1 | 11,674 | 163.1s / 1,576,372 msg/s |
| Dekaf (3conn) | 2026-07-19T04:09:51.2634108+00:00 | 1 | 20.0 MiB / 6.6 MiB | 2224.1 MB/s | 2/1 | 12,573 | 190.1s / 1,524,472 msg/s |
| Dekaf (3conn) | 2026-07-19T04:10:18.2876359+00:00 | 1 | 20.0 MiB / 8.2 MiB | 2224.1 MB/s | 2/2 | 13,840 | 217.1s / 1,986,461 msg/s |
| Dekaf (3conn) | 2026-07-19T04:10:46.3022018+00:00 | 1 | 20.0 MiB / 3.9 MiB | 2224.1 MB/s | 2/2 | 15,144 | 245.1s / 1,626,256 msg/s |
| Dekaf (3conn) | 2026-07-19T04:11:13.3178044+00:00 | 1 | 17.0 MiB / 7.4 MiB | 2224.1 MB/s | 2/2 | 16,494 | 272.1s / 1,695,481 msg/s |
| Dekaf (3conn) | 2026-07-19T04:11:40.3304722+00:00 | 1 | 20.0 MiB / 2.6 MiB | 2224.1 MB/s | 2/3 | 18,037 | 299.2s / 1,797,820 msg/s |
| Dekaf (3conn) | 2026-07-19T04:12:07.3433397+00:00 | 1 | 20.0 MiB / 1.2 MiB | 2258.6 MB/s | 2/3 | 19,233 | 326.2s / 1,648,058 msg/s |
| Dekaf (3conn) | 2026-07-19T04:12:35.3527432+00:00 | 1 | 20.0 MiB / 8.1 MiB | 2258.6 MB/s | 2/3 | 20,375 | 354.2s / 1,624,538 msg/s |
| Dekaf (3conn) | 2026-07-19T04:13:02.361476+00:00 | 1 | 20.0 MiB / 7.7 MiB | 2258.6 MB/s | 2/3 | 21,384 | 381.2s / 1,701,951 msg/s |
| Dekaf (3conn) | 2026-07-19T04:13:29.3752685+00:00 | 1 | 22.0 MiB / 1.8 MiB | 2258.6 MB/s | 2/3 | 22,494 | 408.2s / 1,598,210 msg/s |
| Dekaf (3conn) | 2026-07-19T04:13:57.3893981+00:00 | 1 | 20.0 MiB / 4.7 MiB | 2258.6 MB/s | 2/4 | 24,077 | 436.2s / 1,672,983 msg/s |
| Dekaf (3conn) | 2026-07-19T04:14:24.4044114+00:00 | 1 | 20.0 MiB / 14.4 MiB | 2258.6 MB/s | 2/4 | 25,309 | 463.2s / 1,859,274 msg/s |
| Dekaf (3conn) | 2026-07-19T04:14:51.4216273+00:00 | 1 | 20.0 MiB / 4.1 MiB | 2308.6 MB/s | 2/4 | 26,822 | 490.3s / 1,722,544 msg/s |
| Dekaf (3conn) | 2026-07-19T04:15:18.4363802+00:00 | 1 | 20.0 MiB / 5.2 MiB | 2308.6 MB/s | 2/4 | 27,824 | 517.3s / 1,646,838 msg/s |
| Dekaf (3conn) | 2026-07-19T04:15:46.4411502+00:00 | 1 | 20.0 MiB / 3.8 MiB | 2308.6 MB/s | 2/4 | 28,978 | 545.3s / 1,434,397 msg/s |
| Dekaf (3conn) | 2026-07-19T04:16:13.4558397+00:00 | 1 | 20.0 MiB / 1.8 MiB | 2308.6 MB/s | 2/4 | 30,096 | 572.3s / 1,706,365 msg/s |
| Dekaf (3conn) | 2026-07-19T04:16:40.467782+00:00 | 1 | 20.0 MiB / 2.2 MiB | 2308.6 MB/s | 2/4 | 31,091 | 599.3s / 1,648,263 msg/s |
| Dekaf (3conn) | 2026-07-19T04:17:07.4748139+00:00 | 1 | 20.0 MiB / 4.4 MiB | 2308.6 MB/s | 2/4 | 31,996 | 626.3s / 1,388,171 msg/s |
| Dekaf (3conn) | 2026-07-19T04:17:35.4825337+00:00 | 1 | 20.0 MiB / 2.7 MiB | 2308.6 MB/s | 2/4 | 33,049 | 654.3s / 1,546,774 msg/s |
| Dekaf (3conn) | 2026-07-19T04:18:02.4944783+00:00 | 1 | 17.0 MiB / 1.6 MiB | 2308.6 MB/s | 3/4 | 34,602 | 681.3s / 1,760,415 msg/s |
| Dekaf (3conn) | 2026-07-19T04:18:29.5085259+00:00 | 1 | 14.0 MiB / 7.0 MiB | 2308.6 MB/s | 3/4 | 36,876 | 708.4s / 1,856,504 msg/s |
| Dekaf (3conn) | 2026-07-19T04:18:56.5200409+00:00 | 1 | 17.0 MiB / 2.5 MiB | 2308.6 MB/s | 3/5 | 39,872 | 735.4s / 1,810,949 msg/s |
| Dekaf (3conn) | 2026-07-19T04:19:24.5432391+00:00 | 1 | 17.0 MiB / 6.1 MiB | 2308.6 MB/s | 3/5 | 41,774 | 763.4s / 1,803,769 msg/s |
| Dekaf (3conn) | 2026-07-19T04:19:51.5554088+00:00 | 1 | 17.0 MiB / 4.4 MiB | 2308.6 MB/s | 3/5 | 43,888 | 790.4s / 1,602,514 msg/s |
| Dekaf (3conn) | 2026-07-19T04:20:18.5633692+00:00 | 1 | 19.0 MiB / 6.1 MiB | 2346.0 MB/s | 4/5 | 45,764 | 817.4s / 1,882,723 msg/s |
| Dekaf (3conn) | 2026-07-19T04:20:45.5755491+00:00 | 1 | 19.0 MiB / 3.8 MiB | 2346.0 MB/s | 4/6 | 47,151 | 844.4s / 1,705,743 msg/s |
| Dekaf (3conn) | 2026-07-19T04:21:13.5924208+00:00 | 1 | 19.0 MiB / 8.8 MiB | 2346.0 MB/s | 4/6 | 48,931 | 872.4s / 1,835,382 msg/s |
| Dekaf (3conn) | 2026-07-19T04:21:40.6069665+00:00 | 1 | 19.0 MiB / 5.4 MiB | 2346.0 MB/s | 4/6 | 50,503 | 899.5s / 1,761,651 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-19T03:07:09.1571491+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 16.0 MiB |
| Dekaf | 2026-07-19T03:07:24.1711352+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:07:54.1925933+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:08:09.2027391+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:08:39.2241062+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:08:54.2358329+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-19T03:09:54.294172+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:10:09.3064186+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-19T03:12:09.3992486+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:12:24.4138865+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 7.3 MiB |
| Dekaf | 2026-07-19T03:16:24.5887985+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:16:39.5972375+00:00 | 1 | capacity | failed | 15,008ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T03:20:39.8063803+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-19T03:20:54.8227219+00:00 | 1 | capacity | failed | 15,019ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-19T03:52:10.4291589+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:52:25.4408184+00:00 | 1 | capacity | failed | 15,012ms | 16.0 MiB / 13.9 MiB |
| Dekaf | 2026-07-19T03:53:25.4953469+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:53:40.504587+00:00 | 1 | capacity | failed | 15,009ms | 16.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-19T03:55:40.6030487+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-19T03:55:55.6177839+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-19T03:56:25.640525+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:56:40.6505635+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-19T03:57:10.6694611+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T03:57:25.6829976+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-19T03:58:25.7255489+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-19T03:58:40.7362503+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T03:59:10.7786854+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-19T03:59:25.7918608+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T03:59:55.8170542+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T04:00:10.827858+00:00 | 1 | capacity | succeeded | 15,010ms | 11.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-19T04:00:40.8492983+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-19T04:00:55.8600613+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-19T04:01:55.9034824+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-19T04:02:10.9155497+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T04:02:40.9397573+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-19T04:02:55.9517998+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T04:03:25.9705846+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-19T04:03:40.9837382+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-19T04:04:41.0340022+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.5 MiB |
| Dekaf | 2026-07-19T04:04:56.0440244+00:00 | 1 | capacity | succeeded | 15,010ms | 11.0 MiB / 9.7 MiB |
| Dekaf | 2026-07-19T04:05:26.0698218+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-19T04:05:41.0839652+00:00 | 1 | capacity | failed | 15,014ms | 11.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:07:11.3007576+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-19T04:07:26.326204+00:00 | 1 | capacity | failed | 15,025ms | 16.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:08:26.4347157+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:08:41.4615916+00:00 | 1 | capacity | succeeded | 15,026ms | 18.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-19T04:09:11.5144913+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-19T04:09:26.5475858+00:00 | 1 | capacity | succeeded | 15,033ms | 20.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-19T04:09:56.6013833+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:10:11.6272961+00:00 | 1 | capacity | failed | 15,025ms | 20.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-07-19T04:11:11.7134421+00:00 | 1 | capacity | started | 0ms | 17.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:11:26.7349648+00:00 | 1 | capacity | failed | 15,021ms | 20.0 MiB / 7.8 MiB |
| Dekaf (3conn) | 2026-07-19T04:13:27.1380667+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:13:42.1672948+00:00 | 1 | capacity | failed | 15,029ms | 20.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-19T04:17:42.9186525+00:00 | 1 | capacity | started | 0ms | 17.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-19T04:17:57.9419018+00:00 | 1 | capacity | succeeded | 15,023ms | 17.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:18:27.9879728+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-19T04:18:43.016927+00:00 | 1 | capacity | failed | 15,029ms | 17.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-19T04:19:43.13443+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-19T04:19:58.1574914+00:00 | 1 | capacity | succeeded | 15,023ms | 19.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-19T04:20:28.1964069+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 10.7 MiB |
| Dekaf (3conn) | 2026-07-19T04:20:43.2273442+00:00 | 1 | capacity | failed | 15,030ms | 19.0 MiB / 2.3 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,988 |
| Dekaf | 1 | 0.002–0.004ms | 2,358 |
| Dekaf | 1 | 0.004–0.008ms | 6,921 |
| Dekaf | 1 | 0.008–0.016ms | 41,742 |
| Dekaf | 1 | 0.016–0.032ms | 61,431 |
| Dekaf | 1 | 0.032–0.064ms | 58,895 |
| Dekaf | 1 | 0.064–0.128ms | 107,396 |
| Dekaf | 1 | 0.128–0.256ms | 283,095 |
| Dekaf | 1 | 0.256–0.512ms | 358,006 |
| Dekaf | 1 | 0.512–1.024ms | 61,128 |
| Dekaf | 1 | 1.024–2.048ms | 5,512 |
| Dekaf | 1 | 2.048–4.096ms | 4,145 |
| Dekaf | 1 | 4.096–8.192ms | 755 |
| Dekaf | 1 | 8.192–16.384ms | 30 |
| Dekaf | 1 | 16.384–32.768ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 24 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 30 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 72 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 279 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 772 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 1,268 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,673 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 2,678 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 3,506 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 2,841 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,610 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 673 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 172 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 17 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 1 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 10 |
| Dekaf | 1 | 0.001–0.002ms | 2,290 |
| Dekaf | 1 | 0.002–0.004ms | 2,850 |
| Dekaf | 1 | 0.004–0.008ms | 7,876 |
| Dekaf | 1 | 0.008–0.016ms | 36,686 |
| Dekaf | 1 | 0.016–0.032ms | 55,392 |
| Dekaf | 1 | 0.032–0.064ms | 62,237 |
| Dekaf | 1 | 0.064–0.128ms | 111,834 |
| Dekaf | 1 | 0.128–0.256ms | 299,161 |
| Dekaf | 1 | 0.256–0.512ms | 337,151 |
| Dekaf | 1 | 0.512–1.024ms | 43,932 |
| Dekaf | 1 | 1.024–2.048ms | 4,246 |
| Dekaf | 1 | 2.048–4.096ms | 4,317 |
| Dekaf | 1 | 4.096–8.192ms | 718 |
| Dekaf | 1 | 8.192–16.384ms | 26 |
| Dekaf | 1 | 16.384–32.768ms | 3 |
| Dekaf | 1 | 32.768–65.536ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 203,548,000 | 2026-07-19T03:24:24.3440591+00:00 | 103.8ms | GC pause | - | - | 166.1s / 1,300,350 msg/s | Gen2 +0 / pause +195.2ms |
| Confluent | 203,547,000 | 2026-07-19T03:24:24.3469002+00:00 | 100.6ms | GC pause | - | - | 166.1s / 1,300,350 msg/s | Gen2 +0 / pause +195.2ms |
| Confluent | 203,551,000 | 2026-07-19T03:24:24.3475502+00:00 | 100.5ms | GC pause | - | - | 166.1s / 1,300,350 msg/s | Gen2 +0 / pause +195.2ms |
| Confluent | 203,558,000 | 2026-07-19T03:24:24.3561356+00:00 | 101.3ms | GC pause | - | - | 166.1s / 1,300,350 msg/s | Gen2 +0 / pause +195.2ms |
| Dekaf (3conn) | 486,370,000 | 2026-07-19T04:11:41.3575948+00:00 | 228.2ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,371,000 | 2026-07-19T04:11:41.3578449+00:00 | 225.9ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,372,000 | 2026-07-19T04:11:41.3580971+00:00 | 225.6ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,374,000 | 2026-07-19T04:11:41.3592514+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,376,000 | 2026-07-19T04:11:41.3599375+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,377,000 | 2026-07-19T04:11:41.3602175+00:00 | 225.6ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,380,000 | 2026-07-19T04:11:41.3614028+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,381,000 | 2026-07-19T04:11:41.3619305+00:00 | 223.9ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,382,000 | 2026-07-19T04:11:41.3623234+00:00 | 223.5ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,384,000 | 2026-07-19T04:11:41.3639412+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,386,000 | 2026-07-19T04:11:41.3652722+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,387,000 | 2026-07-19T04:11:41.3660898+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,388,000 | 2026-07-19T04:11:41.3684115+00:00 | 212.1ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,390,000 | 2026-07-19T04:11:41.3692605+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,391,000 | 2026-07-19T04:11:41.3696303+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,392,000 | 2026-07-19T04:11:41.3700053+00:00 | 216.6ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 486,394,000 | 2026-07-19T04:11:41.3722399+00:00 | 216.3ms | broker/backlog (no scale or GC event) | - | - | 301.2s / 1,525,080 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,301,000 | 2026-07-19T04:13:17.9079329+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,302,000 | 2026-07-19T04:13:17.9082567+00:00 | 216.4ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,304,000 | 2026-07-19T04:13:17.9094784+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,305,000 | 2026-07-19T04:13:17.9097552+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,306,000 | 2026-07-19T04:13:17.9100328+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,307,000 | 2026-07-19T04:13:17.9125795+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,308,000 | 2026-07-19T04:13:17.9138934+00:00 | 213.4ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,309,000 | 2026-07-19T04:13:17.916285+00:00 | 208.7ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,310,000 | 2026-07-19T04:13:17.9166761+00:00 | 212.7ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,311,000 | 2026-07-19T04:13:17.9169336+00:00 | 212.5ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,312,000 | 2026-07-19T04:13:17.9171936+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,313,000 | 2026-07-19T04:13:17.9175996+00:00 | 207.4ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,314,000 | 2026-07-19T04:13:17.9181377+00:00 | 211.3ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,315,000 | 2026-07-19T04:13:17.9186992+00:00 | 210.7ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,316,000 | 2026-07-19T04:13:17.9191252+00:00 | 210.3ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,317,000 | 2026-07-19T04:13:17.919426+00:00 | 210.0ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,318,000 | 2026-07-19T04:13:17.9197089+00:00 | 209.7ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,319,000 | 2026-07-19T04:13:17.9201364+00:00 | 208.0ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,320,000 | 2026-07-19T04:13:17.9206836+00:00 | 210.7ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,321,000 | 2026-07-19T04:13:17.9212181+00:00 | 208.2ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 644,322,000 | 2026-07-19T04:13:17.9214937+00:00 | 207.9ms | broker/backlog (no scale or GC event) | - | - | 397.2s / 1,316,736 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 744,921,000 | 2026-07-19T04:14:16.1166888+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,922,000 | 2026-07-19T04:14:16.116882+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,926,000 | 2026-07-19T04:14:16.1179873+00:00 | 223.4ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,927,000 | 2026-07-19T04:14:16.1195981+00:00 | 221.8ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,931,000 | 2026-07-19T04:14:16.1228005+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,932,000 | 2026-07-19T04:14:16.1240932+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,933,000 | 2026-07-19T04:14:16.1244634+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,934,000 | 2026-07-19T04:14:16.1257734+00:00 | 218.8ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,935,000 | 2026-07-19T04:14:16.1260303+00:00 | 218.5ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,936,000 | 2026-07-19T04:14:16.1262604+00:00 | 218.3ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,937,000 | 2026-07-19T04:14:16.127843+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,938,000 | 2026-07-19T04:14:16.1280975+00:00 | 216.4ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,939,000 | 2026-07-19T04:14:16.1287482+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,940,000 | 2026-07-19T04:14:16.1290102+00:00 | 215.6ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,941,000 | 2026-07-19T04:14:16.1292562+00:00 | 215.4ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,942,000 | 2026-07-19T04:14:16.1295126+00:00 | 215.1ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,943,000 | 2026-07-19T04:14:16.130039+00:00 | 214.5ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,944,000 | 2026-07-19T04:14:16.1304286+00:00 | 214.2ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,945,000 | 2026-07-19T04:14:16.131089+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,946,000 | 2026-07-19T04:14:16.1313346+00:00 | 213.3ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,947,000 | 2026-07-19T04:14:16.1315812+00:00 | 213.0ms | broker/backlog (no scale or GC event) | - | - | 455.2s / 1,511,434 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,734,000 | 2026-07-19T04:15:21.5184154+00:00 | 224.1ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,736,000 | 2026-07-19T04:15:21.5190716+00:00 | 227.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,741,000 | 2026-07-19T04:15:21.5211526+00:00 | 227.2ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,742,000 | 2026-07-19T04:15:21.5216096+00:00 | 226.8ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,744,000 | 2026-07-19T04:15:21.5223786+00:00 | 228.9ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,745,000 | 2026-07-19T04:15:21.5229872+00:00 | 223.0ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,746,000 | 2026-07-19T04:15:21.5245589+00:00 | 226.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,748,000 | 2026-07-19T04:15:21.5252248+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,749,000 | 2026-07-19T04:15:21.5268267+00:00 | 224.0ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,750,000 | 2026-07-19T04:15:21.5282311+00:00 | 232.1ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,751,000 | 2026-07-19T04:15:21.5296445+00:00 | 225.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,752,000 | 2026-07-19T04:15:21.5300951+00:00 | 226.1ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,753,000 | 2026-07-19T04:15:21.5304133+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,754,000 | 2026-07-19T04:15:21.5307223+00:00 | 223.1ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,755,000 | 2026-07-19T04:15:21.5313037+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,756,000 | 2026-07-19T04:15:21.5317519+00:00 | 222.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,757,000 | 2026-07-19T04:15:21.5323531+00:00 | 223.9ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,758,000 | 2026-07-19T04:15:21.532818+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,759,000 | 2026-07-19T04:15:21.533132+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,760,000 | 2026-07-19T04:15:21.5334487+00:00 | 230.5ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 858,761,000 | 2026-07-19T04:15:21.5337591+00:00 | 226.0ms | broker/backlog (no scale or GC event) | - | - | 521.3s / 1,515,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 928,781,000 | 2026-07-19T04:16:05.9181037+00:00 | 223.5ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,782,000 | 2026-07-19T04:16:05.9184714+00:00 | 223.2ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,784,000 | 2026-07-19T04:16:05.9192248+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,786,000 | 2026-07-19T04:16:05.9203306+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,789,000 | 2026-07-19T04:16:05.9238866+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,790,000 | 2026-07-19T04:16:05.9241656+00:00 | 215.6ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,791,000 | 2026-07-19T04:16:05.9246191+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,792,000 | 2026-07-19T04:16:05.9249012+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,793,000 | 2026-07-19T04:16:05.9265663+00:00 | 216.0ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,794,000 | 2026-07-19T04:16:05.9270121+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,795,000 | 2026-07-19T04:16:05.9274673+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,796,000 | 2026-07-19T04:16:05.9277763+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,797,000 | 2026-07-19T04:16:05.9282237+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,798,000 | 2026-07-19T04:16:05.9285495+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,799,000 | 2026-07-19T04:16:05.9291421+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,800,000 | 2026-07-19T04:16:05.9297555+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,801,000 | 2026-07-19T04:16:05.9301857+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,802,000 | 2026-07-19T04:16:05.930477+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 928,803,000 | 2026-07-19T04:16:05.9311364+00:00 | 211.9ms | broker/backlog (no scale or GC event) | - | - | 565.3s / 1,249,790 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,854,000 | 2026-07-19T04:17:04.6363032+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,856,000 | 2026-07-19T04:17:04.6379519+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,858,000 | 2026-07-19T04:17:04.638672+00:00 | 225.8ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,859,000 | 2026-07-19T04:17:04.6399684+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,860,000 | 2026-07-19T04:17:04.6414153+00:00 | 224.9ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,861,000 | 2026-07-19T04:17:04.6416457+00:00 | 224.6ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,862,000 | 2026-07-19T04:17:04.641994+00:00 | 224.3ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,863,000 | 2026-07-19T04:17:04.6423379+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,864,000 | 2026-07-19T04:17:04.6425766+00:00 | 221.9ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,865,000 | 2026-07-19T04:17:04.6429413+00:00 | 221.6ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,866,000 | 2026-07-19T04:17:04.6435794+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,867,000 | 2026-07-19T04:17:04.6437887+00:00 | 222.5ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,868,000 | 2026-07-19T04:17:04.6441239+00:00 | 222.2ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,869,000 | 2026-07-19T04:17:04.6443363+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,870,000 | 2026-07-19T04:17:04.6447433+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,871,000 | 2026-07-19T04:17:04.6449639+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,872,000 | 2026-07-19T04:17:04.6455938+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,873,000 | 2026-07-19T04:17:04.6459634+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,021,874,000 | 2026-07-19T04:17:04.6463441+00:00 | 219.9ms | broker/backlog (no scale or GC event) | - | - | 624.3s / 1,214,476 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,128,126,000 | 2026-07-19T04:18:10.5822163+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,127,000 | 2026-07-19T04:18:10.5845944+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,128,000 | 2026-07-19T04:18:10.5858727+00:00 | 219.1ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,129,000 | 2026-07-19T04:18:10.586082+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,130,000 | 2026-07-19T04:18:10.586298+00:00 | 224.2ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,131,000 | 2026-07-19T04:18:10.5866685+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,132,000 | 2026-07-19T04:18:10.5870375+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,133,000 | 2026-07-19T04:18:10.5876788+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,134,000 | 2026-07-19T04:18:10.5880411+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,135,000 | 2026-07-19T04:18:10.5882719+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,136,000 | 2026-07-19T04:18:10.5885177+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,137,000 | 2026-07-19T04:18:10.5888842+00:00 | 221.8ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,138,000 | 2026-07-19T04:18:10.5891081+00:00 | 215.9ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,139,000 | 2026-07-19T04:18:10.5897274+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,140,000 | 2026-07-19T04:18:10.5901954+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,141,000 | 2026-07-19T04:18:10.590418+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,128,142,000 | 2026-07-19T04:18:10.5906338+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 690.3s / 1,487,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,586,000 | 2026-07-19T04:19:08.3206684+00:00 | 223.2ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,590,000 | 2026-07-19T04:19:08.3251923+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,591,000 | 2026-07-19T04:19:08.3255191+00:00 | 219.5ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,592,000 | 2026-07-19T04:19:08.3258225+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,593,000 | 2026-07-19T04:19:08.3261292+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,594,000 | 2026-07-19T04:19:08.3264627+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,595,000 | 2026-07-19T04:19:08.3271821+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,596,000 | 2026-07-19T04:19:08.3279128+00:00 | 217.1ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,597,000 | 2026-07-19T04:19:08.3282282+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,598,000 | 2026-07-19T04:19:08.3285556+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,599,000 | 2026-07-19T04:19:08.328884+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,600,000 | 2026-07-19T04:19:08.3292174+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,601,000 | 2026-07-19T04:19:08.3299431+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,602,000 | 2026-07-19T04:19:08.3305183+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,603,000 | 2026-07-19T04:19:08.3309598+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,229,604,000 | 2026-07-19T04:19:08.3312453+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 1,196,357 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,844,000 | 2026-07-19T04:19:52.4182695+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,846,000 | 2026-07-19T04:19:52.4188215+00:00 | 216.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,849,000 | 2026-07-19T04:19:52.4204493+00:00 | 215.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,850,000 | 2026-07-19T04:19:52.4218406+00:00 | 214.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,851,000 | 2026-07-19T04:19:52.4232397+00:00 | 215.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,852,000 | 2026-07-19T04:19:52.4235529+00:00 | 215.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,853,000 | 2026-07-19T04:19:52.423853+00:00 | 213.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,854,000 | 2026-07-19T04:19:52.4241414+00:00 | 214.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,855,000 | 2026-07-19T04:19:52.425773+00:00 | 212.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,856,000 | 2026-07-19T04:19:52.4262962+00:00 | 212.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,857,000 | 2026-07-19T04:19:52.4268449+00:00 | 212.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,858,000 | 2026-07-19T04:19:52.4271034+00:00 | 211.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,859,000 | 2026-07-19T04:19:52.4273787+00:00 | 210.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,860,000 | 2026-07-19T04:19:52.42765+00:00 | 214.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,861,000 | 2026-07-19T04:19:52.428185+00:00 | 211.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,862,000 | 2026-07-19T04:19:52.4285827+00:00 | 210.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,303,863,000 | 2026-07-19T04:19:52.4292424+00:00 | 212.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 791.4s / 1,353,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,706,000 | 2026-07-19T04:21:28.5697013+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,712,000 | 2026-07-19T04:21:28.5721787+00:00 | 222.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,713,000 | 2026-07-19T04:21:28.5724475+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,714,000 | 2026-07-19T04:21:28.5727157+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,715,000 | 2026-07-19T04:21:28.5729497+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,716,000 | 2026-07-19T04:21:28.5732073+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,717,000 | 2026-07-19T04:21:28.5735931+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,718,000 | 2026-07-19T04:21:28.5742095+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,719,000 | 2026-07-19T04:21:28.5747074+00:00 | 219.5ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,720,000 | 2026-07-19T04:21:28.5749489+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,721,000 | 2026-07-19T04:21:28.5751946+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,722,000 | 2026-07-19T04:21:28.5754459+00:00 | 219.1ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,723,000 | 2026-07-19T04:21:28.5758161+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,724,000 | 2026-07-19T04:21:28.5764748+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,725,000 | 2026-07-19T04:21:28.5769889+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,726,000 | 2026-07-19T04:21:28.577246+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,727,000 | 2026-07-19T04:21:28.5775013+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,728,000 | 2026-07-19T04:21:28.5777533+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,474,729,000 | 2026-07-19T04:21:28.578141+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 888.5s / 1,390,000 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.40x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.16x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.04 | 1050.93 | 1,219,794 | 1,224,999 | -1.6% | -0.23% | 1163.29 | 1,219,794 | 0 | 1.27 |
| Dekaf | 1.14 | 1156.10 | 1,108,042 | 1,121,327 | +1.3% | +0.13% | 1056.71 | 1,108,042 | 0 | 1.26 |
| Confluent | 1.78 | - | 879,619 | 882,831 | +0.3% | +0.10% | 838.87 | 879,619 | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 322,915 | 358.79 | 1000.83 KB |
| Dekaf | 2 | 327,779 | 364.19 | 1002.62 KB |
| Dekaf | 3 | 333,286 | 370.31 | 1018.77 KB |
| Dekaf (3conn) | 1 | 358,018 | 397.79 | 1006.55 KB |
| Dekaf (3conn) | 2 | 367,876 | 408.74 | 1009.24 KB |
| Dekaf (3conn) | 3 | 357,884 | 397.64 | 1005.10 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:56.5918134+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 335,572 msg/s |
| Dekaf | 2026-07-19T03:07:14.6019854+00:00 | 3 | 16.0 MiB / 16.0 MiB | 379.2 MB/s | 0/0 | 10,171 | 18.0s / 1,043,821 msg/s |
| Dekaf | 2026-07-19T03:07:33.6065807+00:00 | 1 | 16.0 MiB / 8.5 MiB | 401.1 MB/s | 0/0 | 5,538 | 37.0s / 1,094,765 msg/s |
| Dekaf | 2026-07-19T03:07:51.6282153+00:00 | 1 | 16.0 MiB / 2.8 MiB | 402.9 MB/s | 0/1 | 6,086 | 55.1s / 1,074,358 msg/s |
| Dekaf | 2026-07-19T03:08:09.6373158+00:00 | 2 | 16.0 MiB / 6.3 MiB | 412.5 MB/s | 0/1 | 5,718 | 73.1s / 1,073,781 msg/s |
| Dekaf | 2026-07-19T03:08:27.645418+00:00 | 2 | 14.0 MiB / 5.0 MiB | 415.9 MB/s | 1/1 | 6,572 | 91.1s / 1,155,704 msg/s |
| Dekaf | 2026-07-19T03:08:45.6508098+00:00 | 3 | 10.0 MiB / 9.7 MiB | 435.5 MB/s | 3/1 | 59,165 | 109.1s / 1,126,242 msg/s |
| Dekaf | 2026-07-19T03:09:03.6647096+00:00 | 3 | 10.0 MiB / 4.6 MiB | 435.5 MB/s | 3/1 | 68,087 | 127.1s / 1,141,658 msg/s |
| Dekaf | 2026-07-19T03:09:22.6696009+00:00 | 1 | 10.0 MiB / 6.5 MiB | 407.7 MB/s | 3/2 | 14,109 | 146.1s / 1,107,204 msg/s |
| Dekaf | 2026-07-19T03:09:40.6809272+00:00 | 1 | 10.0 MiB / 6.4 MiB | 407.7 MB/s | 3/2 | 16,718 | 164.1s / 1,158,626 msg/s |
| Dekaf | 2026-07-19T03:09:58.6895227+00:00 | 2 | 10.0 MiB / 4.8 MiB | 415.9 MB/s | 3/2 | 20,273 | 182.1s / 1,129,920 msg/s |
| Dekaf | 2026-07-19T03:10:16.697105+00:00 | 2 | 8.0 MiB / 6.6 MiB | 415.9 MB/s | 4/2 | 24,462 | 200.1s / 1,103,606 msg/s |
| Dekaf | 2026-07-19T03:10:34.7017902+00:00 | 3 | 10.0 MiB / 6.6 MiB | 435.5 MB/s | 3/3 | 96,452 | 218.2s / 1,047,710 msg/s |
| Dekaf | 2026-07-19T03:10:52.70875+00:00 | 3 | 10.0 MiB / 1.6 MiB | 435.5 MB/s | 3/4 | 100,290 | 236.2s / 1,147,073 msg/s |
| Dekaf | 2026-07-19T03:11:11.7184809+00:00 | 1 | 8.0 MiB / 7.0 MiB | 416.7 MB/s | 4/3 | 25,089 | 255.2s / 1,150,068 msg/s |
| Dekaf | 2026-07-19T03:11:29.7307593+00:00 | 1 | 8.0 MiB / 4.2 MiB | 416.7 MB/s | 4/3 | 26,971 | 273.2s / 1,147,591 msg/s |
| Dekaf | 2026-07-19T03:11:47.7388249+00:00 | 2 | 8.0 MiB / 2.7 MiB | 417.6 MB/s | 4/4 | 37,046 | 291.2s / 1,166,483 msg/s |
| Dekaf | 2026-07-19T03:12:05.7470378+00:00 | 2 | 8.0 MiB / 7.2 MiB | 417.6 MB/s | 4/5 | 38,290 | 309.2s / 1,100,180 msg/s |
| Dekaf | 2026-07-19T03:12:23.7552365+00:00 | 3 | 10.0 MiB / 9.5 MiB | 444.1 MB/s | 3/5 | 132,251 | 327.2s / 1,153,055 msg/s |
| Dekaf | 2026-07-19T03:12:41.7641971+00:00 | 3 | 10.0 MiB / 8.8 MiB | 444.1 MB/s | 3/5 | 142,038 | 345.2s / 1,106,383 msg/s |
| Dekaf | 2026-07-19T03:13:00.7690961+00:00 | 1 | 8.0 MiB / 1.9 MiB | 416.7 MB/s | 4/5 | 31,791 | 364.2s / 1,038,816 msg/s |
| Dekaf | 2026-07-19T03:13:18.7706553+00:00 | 1 | 8.0 MiB / 5.0 MiB | 416.7 MB/s | 4/5 | 33,100 | 382.2s / 1,116,459 msg/s |
| Dekaf | 2026-07-19T03:13:36.7793356+00:00 | 2 | 7.0 MiB / 3.2 MiB | 417.6 MB/s | 5/6 | 51,370 | 400.2s / 1,157,524 msg/s |
| Dekaf | 2026-07-19T03:13:54.7823352+00:00 | 2 | 7.0 MiB / 3.9 MiB | 417.6 MB/s | 5/7 | 56,098 | 418.3s / 1,153,341 msg/s |
| Dekaf | 2026-07-19T03:14:12.791739+00:00 | 3 | 10.0 MiB / 9.7 MiB | 444.1 MB/s | 3/5 | 193,203 | 436.3s / 1,151,755 msg/s |
| Dekaf | 2026-07-19T03:14:30.7975895+00:00 | 3 | 10.0 MiB / 5.0 MiB | 444.1 MB/s | 3/5 | 201,703 | 454.3s / 1,057,150 msg/s |
| Dekaf | 2026-07-19T03:14:49.8077614+00:00 | 1 | 8.0 MiB / 4.3 MiB | 416.7 MB/s | 4/5 | 43,944 | 473.3s / 1,052,140 msg/s |
| Dekaf | 2026-07-19T03:15:07.8160687+00:00 | 1 | 8.0 MiB / 7.8 MiB | 416.7 MB/s | 4/5 | 45,587 | 491.3s / 1,076,266 msg/s |
| Dekaf | 2026-07-19T03:15:25.829515+00:00 | 2 | 7.0 MiB / 6.2 MiB | 417.6 MB/s | 5/8 | 86,986 | 509.3s / 1,094,397 msg/s |
| Dekaf | 2026-07-19T03:15:43.8339668+00:00 | 2 | 7.0 MiB / 3.4 MiB | 417.6 MB/s | 5/9 | 92,287 | 527.3s / 1,182,518 msg/s |
| Dekaf | 2026-07-19T03:16:01.8411865+00:00 | 3 | 8.0 MiB / 8.0 MiB | 444.1 MB/s | 4/6 | 249,622 | 545.3s / 1,158,819 msg/s |
| Dekaf | 2026-07-19T03:16:19.8479442+00:00 | 3 | 8.0 MiB / 8.0 MiB | 444.1 MB/s | 4/6 | 261,711 | 563.3s / 1,106,293 msg/s |
| Dekaf | 2026-07-19T03:16:38.8563207+00:00 | 1 | 7.0 MiB / 7.0 MiB | 416.7 MB/s | 4/5 | 58,205 | 582.3s / 1,139,273 msg/s |
| Dekaf | 2026-07-19T03:16:56.8678397+00:00 | 1 | 8.0 MiB / 1.7 MiB | 416.7 MB/s | 4/6 | 59,056 | 600.3s / 1,084,778 msg/s |
| Dekaf | 2026-07-19T03:17:14.8729885+00:00 | 2 | 7.0 MiB / 6.6 MiB | 417.6 MB/s | 5/9 | 114,878 | 618.3s / 1,066,523 msg/s |
| Dekaf | 2026-07-19T03:17:32.8820745+00:00 | 2 | 7.0 MiB / 1.5 MiB | 417.6 MB/s | 5/9 | 117,967 | 636.3s / 1,123,881 msg/s |
| Dekaf | 2026-07-19T03:17:50.884753+00:00 | 3 | 7.0 MiB / 6.1 MiB | 444.1 MB/s | 5/7 | 326,342 | 654.3s / 1,164,201 msg/s |
| Dekaf | 2026-07-19T03:18:08.892543+00:00 | 3 | 7.0 MiB / 6.0 MiB | 444.1 MB/s | 5/7 | 338,889 | 672.3s / 1,164,531 msg/s |
| Dekaf | 2026-07-19T03:18:27.9040897+00:00 | 1 | 8.0 MiB / 1.6 MiB | 416.7 MB/s | 4/6 | 68,299 | 691.3s / 1,079,898 msg/s |
| Dekaf | 2026-07-19T03:18:45.910227+00:00 | 1 | 8.0 MiB / 3.0 MiB | 416.7 MB/s | 4/6 | 70,052 | 709.4s / 1,102,173 msg/s |
| Dekaf | 2026-07-19T03:19:03.9190514+00:00 | 2 | 7.0 MiB / 4.2 MiB | 417.6 MB/s | 5/9 | 135,588 | 727.4s / 1,134,872 msg/s |
| Dekaf | 2026-07-19T03:19:21.9228601+00:00 | 2 | 7.0 MiB / 2.8 MiB | 417.6 MB/s | 5/9 | 138,064 | 745.4s / 1,128,467 msg/s |
| Dekaf | 2026-07-19T03:19:39.9305735+00:00 | 3 | 5.0 MiB / 4.9 MiB | 444.1 MB/s | 9/7 | 411,291 | 763.4s / 967,343 msg/s |
| Dekaf | 2026-07-19T03:19:57.93986+00:00 | 3 | 5.0 MiB / 3.9 MiB | 444.1 MB/s | 9/8 | 426,388 | 781.4s / 1,105,349 msg/s |
| Dekaf | 2026-07-19T03:20:16.9442161+00:00 | 1 | 8.0 MiB / 7.2 MiB | 416.7 MB/s | 4/6 | 76,536 | 800.4s / 1,137,439 msg/s |
| Dekaf | 2026-07-19T03:20:34.95239+00:00 | 1 | 8.0 MiB / 6.1 MiB | 416.7 MB/s | 4/6 | 77,833 | 818.4s / 1,062,641 msg/s |
| Dekaf | 2026-07-19T03:20:52.9636155+00:00 | 2 | 9.0 MiB / 7.8 MiB | 417.6 MB/s | 7/9 | 148,873 | 836.4s / 1,067,764 msg/s |
| Dekaf | 2026-07-19T03:21:10.9684576+00:00 | 2 | 9.0 MiB / 6.3 MiB | 417.6 MB/s | 7/9 | 150,121 | 854.4s / 1,115,920 msg/s |
| Dekaf | 2026-07-19T03:21:28.9713387+00:00 | 3 | 5.0 MiB / 5.0 MiB | 444.1 MB/s | 9/9 | 499,244 | 872.4s / 1,110,673 msg/s |
| Dekaf | 2026-07-19T03:21:46.9763969+00:00 | 3 | 5.0 MiB / 5.0 MiB | 444.1 MB/s | 9/9 | 514,180 | 890.4s / 1,104,689 msg/s |
| Dekaf (3conn) | 2026-07-19T03:37:19.2192376+00:00 | 3 | 16.0 MiB / 4.7 MiB | 347.7 MB/s | 0/0 | 1,521 | 9.0s / 997,090 msg/s |
| Dekaf (3conn) | 2026-07-19T03:37:37.2211729+00:00 | 3 | 16.0 MiB / 14.4 MiB | 455.9 MB/s | 0/0 | 4,235 | 27.0s / 1,195,219 msg/s |
| Dekaf (3conn) | 2026-07-19T03:37:56.2255761+00:00 | 1 | 14.0 MiB / 9.0 MiB | 458.7 MB/s | 1/0 | 9,318 | 46.0s / 1,330,871 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:14.2362817+00:00 | 1 | 12.0 MiB / 5.1 MiB | 472.3 MB/s | 2/0 | 11,889 | 64.1s / 1,273,764 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:32.2478765+00:00 | 2 | 16.0 MiB / 0.7 MiB | 485.4 MB/s | 0/1 | 11,802 | 82.1s / 1,178,035 msg/s |
| Dekaf (3conn) | 2026-07-19T03:38:50.2598862+00:00 | 2 | 16.0 MiB / 8.1 MiB | 485.4 MB/s | 0/1 | 14,105 | 100.1s / 1,184,720 msg/s |
| Dekaf (3conn) | 2026-07-19T03:39:08.2666376+00:00 | 3 | 12.0 MiB / 9.0 MiB | 468.0 MB/s | 2/1 | 12,141 | 118.1s / 1,173,554 msg/s |
| Dekaf (3conn) | 2026-07-19T03:39:26.2775745+00:00 | 3 | 12.0 MiB / 7.1 MiB | 468.0 MB/s | 2/2 | 14,230 | 136.1s / 1,272,553 msg/s |
| Dekaf (3conn) | 2026-07-19T03:39:45.2842145+00:00 | 1 | 8.0 MiB / 5.6 MiB | 472.3 MB/s | 6/0 | 48,743 | 155.1s / 1,149,333 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:03.3009881+00:00 | 1 | 8.0 MiB / 7.2 MiB | 472.3 MB/s | 6/0 | 55,995 | 173.1s / 1,283,856 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:21.3105333+00:00 | 2 | 14.0 MiB / 8.2 MiB | 485.4 MB/s | 1/2 | 23,876 | 191.1s / 1,265,933 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:39.3185918+00:00 | 2 | 14.0 MiB / 9.1 MiB | 485.4 MB/s | 1/2 | 25,950 | 209.1s / 1,233,557 msg/s |
| Dekaf (3conn) | 2026-07-19T03:40:57.3289245+00:00 | 3 | 12.0 MiB / 6.6 MiB | 468.0 MB/s | 2/3 | 25,788 | 227.1s / 1,276,276 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:15.3320897+00:00 | 3 | 12.0 MiB / 5.1 MiB | 468.0 MB/s | 2/3 | 27,919 | 245.2s / 1,202,760 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:34.3486001+00:00 | 1 | 8.0 MiB / 5.1 MiB | 472.3 MB/s | 6/2 | 90,173 | 264.2s / 1,211,510 msg/s |
| Dekaf (3conn) | 2026-07-19T03:41:52.3569839+00:00 | 1 | 9.0 MiB / 5.3 MiB | 472.3 MB/s | 7/2 | 94,301 | 282.2s / 1,282,292 msg/s |
| Dekaf (3conn) | 2026-07-19T03:42:10.3630796+00:00 | 2 | 9.0 MiB / 3.7 MiB | 485.4 MB/s | 4/3 | 40,451 | 300.2s / 1,287,721 msg/s |
| Dekaf (3conn) | 2026-07-19T03:42:28.3683343+00:00 | 2 | 9.0 MiB / 8.3 MiB | 485.4 MB/s | 5/3 | 43,518 | 318.2s / 1,330,686 msg/s |
| Dekaf (3conn) | 2026-07-19T03:42:46.3745807+00:00 | 3 | 10.0 MiB / 5.5 MiB | 468.0 MB/s | 3/3 | 35,920 | 336.2s / 1,261,807 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:04.3835978+00:00 | 3 | 8.0 MiB / 2.4 MiB | 468.0 MB/s | 4/3 | 39,145 | 354.2s / 1,214,280 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:23.3855912+00:00 | 1 | 8.0 MiB / 3.3 MiB | 472.3 MB/s | 9/2 | 105,371 | 373.2s / 1,265,427 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:41.3916263+00:00 | 1 | 8.0 MiB / 7.3 MiB | 472.3 MB/s | 9/3 | 109,257 | 391.2s / 1,209,372 msg/s |
| Dekaf (3conn) | 2026-07-19T03:43:59.4031681+00:00 | 2 | 10.0 MiB / 6.5 MiB | 485.4 MB/s | 6/4 | 55,023 | 409.2s / 1,215,064 msg/s |
| Dekaf (3conn) | 2026-07-19T03:44:17.4155456+00:00 | 2 | 10.0 MiB / 8.5 MiB | 485.4 MB/s | 6/4 | 56,524 | 427.2s / 1,188,588 msg/s |
| Dekaf (3conn) | 2026-07-19T03:44:35.430879+00:00 | 3 | 8.0 MiB / 5.0 MiB | 468.0 MB/s | 7/3 | 54,071 | 445.2s / 1,249,890 msg/s |
| Dekaf (3conn) | 2026-07-19T03:44:53.4382448+00:00 | 3 | 8.0 MiB / 4.4 MiB | 468.0 MB/s | 7/3 | 56,655 | 463.2s / 1,291,963 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:12.448729+00:00 | 1 | 8.0 MiB / 7.7 MiB | 472.3 MB/s | 9/4 | 124,371 | 482.2s / 1,203,419 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:30.4552627+00:00 | 1 | 9.0 MiB / 8.0 MiB | 472.3 MB/s | 10/4 | 128,988 | 500.3s / 1,206,052 msg/s |
| Dekaf (3conn) | 2026-07-19T03:45:48.4627875+00:00 | 2 | 10.0 MiB / 5.9 MiB | 487.5 MB/s | 6/5 | 69,836 | 518.3s / 1,172,749 msg/s |
| Dekaf (3conn) | 2026-07-19T03:46:06.4742024+00:00 | 2 | 10.0 MiB / 7.5 MiB | 487.5 MB/s | 6/5 | 73,594 | 536.3s / 1,228,778 msg/s |
| Dekaf (3conn) | 2026-07-19T03:46:24.4842469+00:00 | 3 | 8.0 MiB / 8.0 MiB | 468.0 MB/s | 7/5 | 81,177 | 554.3s / 1,216,136 msg/s |
| Dekaf (3conn) | 2026-07-19T03:46:42.4886266+00:00 | 3 | 8.0 MiB / 5.2 MiB | 468.0 MB/s | 7/5 | 86,843 | 572.3s / 1,183,266 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:01.4933181+00:00 | 1 | 9.0 MiB / 7.3 MiB | 478.1 MB/s | 10/5 | 151,445 | 591.3s / 1,227,830 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:19.4966464+00:00 | 1 | 7.0 MiB / 4.4 MiB | 478.1 MB/s | 10/5 | 156,717 | 609.3s / 1,187,652 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:37.5026159+00:00 | 2 | 8.0 MiB / 4.7 MiB | 487.5 MB/s | 7/6 | 98,085 | 627.3s / 1,195,969 msg/s |
| Dekaf (3conn) | 2026-07-19T03:47:55.5130576+00:00 | 2 | 9.0 MiB / 3.1 MiB | 487.5 MB/s | 8/6 | 103,746 | 645.3s / 1,216,820 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:13.5185364+00:00 | 3 | 7.0 MiB / 4.4 MiB | 468.0 MB/s | 8/6 | 107,245 | 663.3s / 1,269,747 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:31.5200355+00:00 | 3 | 7.0 MiB / 6.1 MiB | 468.0 MB/s | 9/6 | 113,886 | 681.3s / 1,113,707 msg/s |
| Dekaf (3conn) | 2026-07-19T03:48:50.5347436+00:00 | 1 | 10.0 MiB / 9.1 MiB | 478.1 MB/s | 11/6 | 173,349 | 700.3s / 1,231,843 msg/s |
| Dekaf (3conn) | 2026-07-19T03:49:08.5391013+00:00 | 1 | 10.0 MiB / 5.2 MiB | 478.1 MB/s | 11/7 | 174,676 | 718.3s / 1,255,639 msg/s |
| Dekaf (3conn) | 2026-07-19T03:49:26.5481188+00:00 | 2 | 7.0 MiB / 4.2 MiB | 487.5 MB/s | 9/7 | 130,949 | 736.3s / 1,202,334 msg/s |
| Dekaf (3conn) | 2026-07-19T03:49:44.5532056+00:00 | 2 | 7.0 MiB / 5.8 MiB | 487.5 MB/s | 9/8 | 137,718 | 754.4s / 1,200,768 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:02.5625858+00:00 | 3 | 6.0 MiB / 5.2 MiB | 468.0 MB/s | 10/7 | 155,081 | 772.4s / 1,119,267 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:20.5713358+00:00 | 3 | 7.0 MiB / 4.9 MiB | 468.0 MB/s | 11/7 | 161,268 | 790.4s / 1,183,058 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:39.5794518+00:00 | 1 | 8.0 MiB / 6.9 MiB | 478.1 MB/s | 12/8 | 186,634 | 809.4s / 1,198,725 msg/s |
| Dekaf (3conn) | 2026-07-19T03:50:57.5868251+00:00 | 1 | 8.0 MiB / 5.4 MiB | 478.1 MB/s | 12/8 | 189,336 | 827.4s / 1,248,193 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:15.5924993+00:00 | 2 | 7.0 MiB / 6.3 MiB | 487.5 MB/s | 9/8 | 170,372 | 845.4s / 1,168,267 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:33.5958367+00:00 | 2 | 8.0 MiB / 6.4 MiB | 487.5 MB/s | 9/8 | 175,762 | 863.4s / 1,200,951 msg/s |
| Dekaf (3conn) | 2026-07-19T03:51:51.6021123+00:00 | 3 | 7.0 MiB / 1.8 MiB | 468.0 MB/s | 13/7 | 189,779 | 881.4s / 1,189,438 msg/s |
| Dekaf (3conn) | 2026-07-19T03:52:09.611115+00:00 | 3 | 8.0 MiB / 8.0 MiB | 468.0 MB/s | 14/7 | 193,565 | 899.4s / 1,173,360 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-19T03:07:26.7626263+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-19T03:07:26.8004409+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-19T03:07:41.8316606+00:00 | 2 | capacity | failed | 15,056ms | 16.0 MiB / 5.3 MiB |
| Dekaf | 2026-07-19T03:07:44.8210826+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 12.8 MiB |
| Dekaf | 2026-07-19T03:08:11.9355399+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:08:17.9348866+00:00 | 3 | capacity | succeeded | 15,042ms | 10.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-19T03:08:27.0489257+00:00 | 1 | capacity | succeeded | 15,062ms | 14.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-19T03:08:30.054995+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-19T03:08:36.0449269+00:00 | 3 | capacity | failed | 15,099ms | 10.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-19T03:08:45.1397094+00:00 | 2 | capacity | succeeded | 15,069ms | 12.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-19T03:09:03.1503875+00:00 | 1 | capacity | succeeded | 15,044ms | 10.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-19T03:09:06.1292695+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-19T03:09:06.2195733+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-19T03:09:21.2180107+00:00 | 1 | capacity | failed | 15,055ms | 10.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-19T03:09:51.2642672+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-19T03:09:51.3628854+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-19T03:10:06.359705+00:00 | 1 | capacity | succeeded | 15,044ms | 8.0 MiB / 6.6 MiB |
| Dekaf | 2026-07-19T03:10:09.4186416+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:10:24.4262929+00:00 | 1 | capacity | failed | 15,053ms | 8.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-19T03:10:39.3981292+00:00 | 3 | capacity | failed | 15,047ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:11:09.5237635+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-19T03:11:24.5686745+00:00 | 3 | capacity | failed | 15,045ms | 10.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-19T03:11:39.7015063+00:00 | 1 | capacity | failed | 15,039ms | 8.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-19T03:11:54.7561221+00:00 | 2 | capacity | failed | 15,039ms | 8.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-19T03:12:24.8385585+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-19T03:13:10.0205824+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.0 MiB |
| Dekaf | 2026-07-19T03:13:28.0784332+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-19T03:14:43.2963464+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:15:25.2990023+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-19T03:15:40.341335+00:00 | 3 | capacity | succeeded | 15,042ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:15:58.3910948+00:00 | 3 | capacity | failed | 15,041ms | 8.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-19T03:16:28.5395836+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-19T03:16:43.5848074+00:00 | 3 | capacity | succeeded | 15,045ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:17:01.6288461+00:00 | 3 | capacity | failed | 15,038ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:18:16.8520993+00:00 | 3 | capacity | succeeded | 15,043ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-19T03:18:34.9094001+00:00 | 3 | capacity | succeeded | 15,050ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-19T03:19:20.0904339+00:00 | 3 | capacity | succeeded | 15,045ms | 6.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-19T03:19:41.1511624+00:00 | 3 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-19T03:19:56.2004439+00:00 | 3 | capacity | failed | 15,049ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-19T03:20:29.4004195+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-19T03:20:44.4809707+00:00 | 2 | capacity | succeeded | 15,080ms | 9.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-19T03:20:56.6159268+00:00 | 1 | capacity | failed | 15,054ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-19T03:21:14.5731822+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:40.4197572+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 13.0 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:55.4732517+00:00 | 2 | capacity | failed | 15,058ms | 16.0 MiB / 13.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:37:55.5560469+00:00 | 3 | capacity | failed | 15,101ms | 16.0 MiB / 12.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:13.6034839+00:00 | 1 | capacity | succeeded | 15,055ms | 12.0 MiB / 8.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:25.6556363+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 7.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:34.6599838+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:43.7182039+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:38:55.7476152+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:01.7678074+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:10.782641+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:13.8148201+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:25.860077+00:00 | 1 | capacity | succeeded | 15,077ms | 8.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-19T03:39:55.9537583+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:40:17.0974365+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:40:41.145102+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-19T03:40:56.2060392+00:00 | 1 | capacity | failed | 15,060ms | 8.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:41:26.3226522+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:41:30.8210084+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:41:45.8627298+00:00 | 2 | capacity | succeeded | 15,041ms | 10.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:03.9137434+00:00 | 2 | capacity | succeeded | 15,044ms | 8.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:20.6047986+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 9.3 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:26.5416227+00:00 | 1 | capacity | succeeded | 15,098ms | 10.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:38.6548181+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:53.7053015+00:00 | 3 | capacity | succeeded | 15,050ms | 8.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:42:56.7165292+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:11.6992735+00:00 | 1 | capacity | succeeded | 15,053ms | 8.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:14.7055242+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:40.7929306+00:00 | 2 | capacity | failed | 3,517ms | 10.0 MiB / 7.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:43:56.9252621+00:00 | 3 | capacity | succeeded | 15,057ms | 7.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:44:10.8908829+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:44:25.9527522+00:00 | 2 | capacity | failed | 15,059ms | 10.0 MiB / 8.0 MiB |
| Dekaf (3conn) | 2026-07-19T03:44:45.0527429+00:00 | 1 | capacity | failed | 15,113ms | 8.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:45:00.1939861+00:00 | 3 | capacity | failed | 15,049ms | 8.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:46:00.3253297+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:46:15.3741502+00:00 | 1 | capacity | failed | 15,048ms | 9.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:46:26.4103672+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:46:45.6460538+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:09.1388312+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:24.1824648+00:00 | 2 | capacity | succeeded | 15,043ms | 8.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:30.6791196+00:00 | 1 | capacity | failed | 15,037ms | 9.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:47:42.2433326+00:00 | 2 | capacity | succeeded | 15,049ms | 9.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:02.3987003+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:15.8616173+00:00 | 1 | capacity | succeeded | 15,065ms | 10.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:20.4798609+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:30.4099643+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:38.5858006+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:48:45.9473773+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:49:15.6384651+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:49:53.8239705+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:08.874062+00:00 | 3 | capacity | succeeded | 15,049ms | 7.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:19.306748+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:39.0511806+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-19T03:50:57.1239405+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:51:34.6078919+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-19T03:51:46.1868334+00:00 | 2 | capacity | succeeded | 15,053ms | 8.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:51:52.6815367+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-19T03:52:07.7402416+00:00 | 1 | capacity | failed | 15,058ms | 7.0 MiB / 4.6 MiB |
*114 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 29 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 42 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 162 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 512 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 1,181 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 1,696 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 2,207 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 2,690 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 4,980 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 7,485 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 9,037 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 7,116 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 2,587 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 853 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 419 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 60 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 27 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 44 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 140 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 581 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 1,214 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 1,703 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 2,100 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 2,553 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 4,966 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 7,422 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 8,410 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 6,069 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 2,131 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 600 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 241 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 26 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 35 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 46 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 172 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 526 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 1,177 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 1,760 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 2,139 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 2,596 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 4,917 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 7,387 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 8,601 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 6,605 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 2,257 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 632 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 256 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 26 |
| Dekaf | 1 | 0.001–0.002ms | 12 |
| Dekaf | 1 | 0.002–0.004ms | 37 |
| Dekaf | 1 | 0.004–0.008ms | 97 |
| Dekaf | 1 | 0.008–0.016ms | 345 |
| Dekaf | 1 | 0.016–0.032ms | 658 |
| Dekaf | 1 | 0.032–0.064ms | 846 |
| Dekaf | 1 | 0.064–0.128ms | 1,134 |
| Dekaf | 1 | 0.128–0.256ms | 1,969 |
| Dekaf | 1 | 0.256–0.512ms | 3,602 |
| Dekaf | 1 | 0.512–1.024ms | 5,102 |
| Dekaf | 1 | 1.024–2.048ms | 4,603 |
| Dekaf | 1 | 2.048–4.096ms | 2,332 |
| Dekaf | 1 | 4.096–8.192ms | 809 |
| Dekaf | 1 | 8.192–16.384ms | 306 |
| Dekaf | 1 | 16.384–32.768ms | 117 |
| Dekaf | 1 | 32.768–65.536ms | 5 |
| Dekaf | 2 | 0.001–0.002ms | 34 |
| Dekaf | 2 | 0.002–0.004ms | 52 |
| Dekaf | 2 | 0.004–0.008ms | 157 |
| Dekaf | 2 | 0.008–0.016ms | 543 |
| Dekaf | 2 | 0.016–0.032ms | 1,127 |
| Dekaf | 2 | 0.032–0.064ms | 1,532 |
| Dekaf | 2 | 0.064–0.128ms | 2,044 |
| Dekaf | 2 | 0.128–0.256ms | 3,551 |
| Dekaf | 2 | 0.256–0.512ms | 6,506 |
| Dekaf | 2 | 0.512–1.024ms | 9,618 |
| Dekaf | 2 | 1.024–2.048ms | 8,139 |
| Dekaf | 2 | 2.048–4.096ms | 3,720 |
| Dekaf | 2 | 4.096–8.192ms | 1,206 |
| Dekaf | 2 | 8.192–16.384ms | 428 |
| Dekaf | 2 | 16.384–32.768ms | 178 |
| Dekaf | 2 | 32.768–65.536ms | 13 |
| Dekaf | 3 | 0.001–0.002ms | 106 |
| Dekaf | 3 | 0.002–0.004ms | 109 |
| Dekaf | 3 | 0.004–0.008ms | 423 |
| Dekaf | 3 | 0.008–0.016ms | 1,377 |
| Dekaf | 3 | 0.016–0.032ms | 3,284 |
| Dekaf | 3 | 0.032–0.064ms | 4,798 |
| Dekaf | 3 | 0.064–0.128ms | 6,369 |
| Dekaf | 3 | 0.128–0.256ms | 11,585 |
| Dekaf | 3 | 0.256–0.512ms | 22,611 |
| Dekaf | 3 | 0.512–1.024ms | 34,066 |
| Dekaf | 3 | 1.024–2.048ms | 27,883 |
| Dekaf | 3 | 2.048–4.096ms | 12,055 |
| Dekaf | 3 | 4.096–8.192ms | 3,521 |
| Dekaf | 3 | 8.192–16.384ms | 1,245 |
| Dekaf | 3 | 16.384–32.768ms | 548 |
| Dekaf | 3 | 32.768–65.536ms | 53 |
| Dekaf | 3 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 29,000 | 2026-07-19T03:06:56.7169572+00:00 | 106.9ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 35,000 | 2026-07-19T03:06:56.7239162+00:00 | 117.2ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 38,000 | 2026-07-19T03:06:56.7275967+00:00 | 120.7ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 42,000 | 2026-07-19T03:06:56.7328199+00:00 | 204.4ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 48,000 | 2026-07-19T03:06:56.7402357+00:00 | 120.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 51,000 | 2026-07-19T03:06:56.7448863+00:00 | 226.4ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 53,000 | 2026-07-19T03:06:56.7478051+00:00 | 115.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 57,000 | 2026-07-19T03:06:56.7531305+00:00 | 120.4ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 58,000 | 2026-07-19T03:06:56.7543767+00:00 | 125.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 59,000 | 2026-07-19T03:06:56.7554819+00:00 | 113.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 63,000 | 2026-07-19T03:06:56.7607552+00:00 | 112.9ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 69,000 | 2026-07-19T03:06:56.7756783+00:00 | 107.9ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 72,000 | 2026-07-19T03:06:56.7808757+00:00 | 253.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 101,000 | 2026-07-19T03:06:56.9044286+00:00 | 241.8ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 108,000 | 2026-07-19T03:06:56.9177295+00:00 | 110.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 111,000 | 2026-07-19T03:06:56.9333272+00:00 | 243.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 115,000 | 2026-07-19T03:06:56.9394339+00:00 | 152.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 119,000 | 2026-07-19T03:06:56.9600856+00:00 | 128.2ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 121,000 | 2026-07-19T03:06:56.9735543+00:00 | 259.9ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 122,000 | 2026-07-19T03:06:56.9758158+00:00 | 257.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 123,000 | 2026-07-19T03:06:56.9796763+00:00 | 122.4ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 125,000 | 2026-07-19T03:06:56.9817889+00:00 | 127.8ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 127,000 | 2026-07-19T03:06:56.9844934+00:00 | 102.7ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 131,000 | 2026-07-19T03:06:56.9993562+00:00 | 246.5ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 141,000 | 2026-07-19T03:06:57.0317147+00:00 | 243.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 143,000 | 2026-07-19T03:06:57.0363802+00:00 | 115.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 171,000 | 2026-07-19T03:06:57.1519088+00:00 | 216.4ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 180,000 | 2026-07-19T03:06:57.1785949+00:00 | 101.7ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 200,000 | 2026-07-19T03:06:57.2502805+00:00 | 117.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 204,000 | 2026-07-19T03:06:57.2602026+00:00 | 102.1ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 221,000 | 2026-07-19T03:06:57.2984153+00:00 | 149.7ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 223,000 | 2026-07-19T03:06:57.322505+00:00 | 131.8ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 239,000 | 2026-07-19T03:06:57.3694049+00:00 | 126.9ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 243,000 | 2026-07-19T03:06:57.3751345+00:00 | 126.8ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 249,000 | 2026-07-19T03:06:57.3896747+00:00 | 123.5ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 252,000 | 2026-07-19T03:06:57.3978011+00:00 | 101.6ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 263,000 | 2026-07-19T03:06:57.4197311+00:00 | 181.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 272,000 | 2026-07-19T03:06:57.4289194+00:00 | 154.3ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 273,000 | 2026-07-19T03:06:57.4297053+00:00 | 196.0ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 281,000 | 2026-07-19T03:06:57.4431589+00:00 | 147.9ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 289,000 | 2026-07-19T03:06:57.4623072+00:00 | 202.7ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 302,000 | 2026-07-19T03:06:57.4884121+00:00 | 189.5ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 304,000 | 2026-07-19T03:06:57.4934901+00:00 | 124.2ms | GC pause | - | - | 1.0s / 335,572 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 307,000 | 2026-07-19T03:06:57.4989063+00:00 | 164.4ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 309,000 | 2026-07-19T03:06:57.5012929+00:00 | 205.6ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 312,000 | 2026-07-19T03:06:57.5051582+00:00 | 218.8ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 316,000 | 2026-07-19T03:06:57.5151859+00:00 | 116.3ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 320,000 | 2026-07-19T03:06:57.5273253+00:00 | 118.6ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 329,000 | 2026-07-19T03:06:57.6052086+00:00 | 129.5ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 333,000 | 2026-07-19T03:06:57.6234721+00:00 | 117.1ms | GC pause | - | - | 2.0s / 472,378 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 338,000 | 2026-07-19T03:06:57.629961+00:00 | 108.6ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 342,000 | 2026-07-19T03:06:57.6348821+00:00 | 198.4ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 349,000 | 2026-07-19T03:06:57.6472736+00:00 | 110.1ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 372,000 | 2026-07-19T03:06:57.7397388+00:00 | 199.2ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 382,000 | 2026-07-19T03:06:57.7679538+00:00 | 198.5ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 392,000 | 2026-07-19T03:06:57.7914692+00:00 | 198.5ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 398,000 | 2026-07-19T03:06:57.802171+00:00 | 100.5ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 412,000 | 2026-07-19T03:06:57.8451297+00:00 | 170.9ms | throughput collapse | - | - | 2.0s / 472,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 763,000 | 2026-07-19T03:06:58.5635697+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 783,000 | 2026-07-19T03:06:58.5820966+00:00 | 147.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 789,000 | 2026-07-19T03:06:58.5993508+00:00 | 139.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 793,000 | 2026-07-19T03:06:58.6028781+00:00 | 143.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 803,000 | 2026-07-19T03:06:58.6110892+00:00 | 139.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 813,000 | 2026-07-19T03:06:58.6404965+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 823,000 | 2026-07-19T03:06:58.6551504+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 829,000 | 2026-07-19T03:06:58.663059+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 899,000 | 2026-07-19T03:06:58.7807618+00:00 | 139.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 919,000 | 2026-07-19T03:06:58.8061991+00:00 | 167.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 929,000 | 2026-07-19T03:06:58.8289689+00:00 | 157.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 939,000 | 2026-07-19T03:06:58.8484379+00:00 | 146.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 949,000 | 2026-07-19T03:06:58.9086574+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 959,000 | 2026-07-19T03:06:58.9204879+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 963,000 | 2026-07-19T03:06:58.9222772+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,009,000 | 2026-07-19T03:06:59.0018125+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,019,000 | 2026-07-19T03:06:59.0187348+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,129,000 | 2026-07-19T03:06:59.1687211+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,169,000 | 2026-07-19T03:06:59.2124689+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,187,000 | 2026-07-19T03:06:59.2529628+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,207,000 | 2026-07-19T03:06:59.2832188+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,217,000 | 2026-07-19T03:06:59.2954323+00:00 | 137.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,237,000 | 2026-07-19T03:06:59.3308888+00:00 | 173.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,307,000 | 2026-07-19T03:06:59.5057011+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 576,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,637,000 | 2026-07-19T03:06:59.9223998+00:00 | 132.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,649,000 | 2026-07-19T03:06:59.9412713+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,657,000 | 2026-07-19T03:06:59.9691795+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,659,000 | 2026-07-19T03:06:59.969901+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,677,000 | 2026-07-19T03:07:00.0150422+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,697,000 | 2026-07-19T03:07:00.0433202+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,707,000 | 2026-07-19T03:07:00.0560609+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,712,000 | 2026-07-19T03:07:00.0612028+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,722,000 | 2026-07-19T03:07:00.0756192+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,809,000 | 2026-07-19T03:07:00.1998911+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,817,000 | 2026-07-19T03:07:00.207232+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,827,000 | 2026-07-19T03:07:00.2186127+00:00 | 123.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,829,000 | 2026-07-19T03:07:00.2259416+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,867,000 | 2026-07-19T03:07:00.2796458+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,923,000 | 2026-07-19T03:07:00.3763582+00:00 | 131.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,927,000 | 2026-07-19T03:07:00.3807407+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,937,000 | 2026-07-19T03:07:00.3939256+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,939,000 | 2026-07-19T03:07:00.3957899+00:00 | 120.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,943,000 | 2026-07-19T03:07:00.4016228+00:00 | 119.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 681,774 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,007,000 | 2026-07-19T03:07:00.5190773+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,279,000 | 2026-07-19T03:07:00.8917144+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,309,000 | 2026-07-19T03:07:00.9316965+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,399,000 | 2026-07-19T03:07:01.0641691+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,633,000 | 2026-07-19T03:07:01.3647803+00:00 | 127.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,659,000 | 2026-07-19T03:07:01.4043876+00:00 | 144.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,672,000 | 2026-07-19T03:07:01.4142991+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,673,000 | 2026-07-19T03:07:01.4153037+00:00 | 159.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,677,000 | 2026-07-19T03:07:01.4170575+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,678,000 | 2026-07-19T03:07:01.4174855+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,682,000 | 2026-07-19T03:07:01.4220013+00:00 | 133.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,684,000 | 2026-07-19T03:07:01.4254199+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,686,000 | 2026-07-19T03:07:01.4262489+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 730,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,289,000 | 2026-07-19T03:07:02.1800822+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 853,269 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,509,000 | 2026-07-19T03:07:02.4436322+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 853,269 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,069,000 | 2026-07-19T03:07:03.076485+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,097,000 | 2026-07-19T03:07:03.1148204+00:00 | 150.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,099,000 | 2026-07-19T03:07:03.1155358+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,117,000 | 2026-07-19T03:07:03.1390285+00:00 | 159.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,119,000 | 2026-07-19T03:07:03.1407312+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,126,000 | 2026-07-19T03:07:03.1460023+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,127,000 | 2026-07-19T03:07:03.1478968+00:00 | 168.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,137,000 | 2026-07-19T03:07:03.1666902+00:00 | 159.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,297,000 | 2026-07-19T03:07:03.4278991+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 809,400 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,679,000 | 2026-07-19T03:07:03.8649611+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,699,000 | 2026-07-19T03:07:03.8913965+00:00 | 118.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,702,000 | 2026-07-19T03:07:03.897496+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,719,000 | 2026-07-19T03:07:03.9198338+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,725,000 | 2026-07-19T03:07:03.932906+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,728,000 | 2026-07-19T03:07:03.9359703+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,113,000 | 2026-07-19T03:07:04.4039878+00:00 | 133.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,119,000 | 2026-07-19T03:07:04.413294+00:00 | 128.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,127,000 | 2026-07-19T03:07:04.4249941+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 804,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,529,000 | 2026-07-19T03:07:04.8988215+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,547,000 | 2026-07-19T03:07:04.9173328+00:00 | 158.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,657,000 | 2026-07-19T03:07:05.1162648+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,917,000 | 2026-07-19T03:07:05.4031659+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,922,000 | 2026-07-19T03:07:05.4070866+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,941,000 | 2026-07-19T03:07:05.4243592+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,942,000 | 2026-07-19T03:07:05.4248498+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,957,000 | 2026-07-19T03:07:05.440836+00:00 | 125.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 830,763 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,337,000 | 2026-07-19T03:07:05.8800655+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,347,000 | 2026-07-19T03:07:05.8943677+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,373,000 | 2026-07-19T03:07:05.9304727+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,379,000 | 2026-07-19T03:07:05.9361379+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,389,000 | 2026-07-19T03:07:05.9447942+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,793,000 | 2026-07-19T03:07:06.4387355+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,799,000 | 2026-07-19T03:07:06.4446937+00:00 | 119.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,803,000 | 2026-07-19T03:07:06.4482177+00:00 | 116.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,809,000 | 2026-07-19T03:07:06.4562534+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,813,000 | 2026-07-19T03:07:06.4623732+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 836,861 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,063,000 | 2026-07-19T03:07:06.7628176+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 816,133 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,213,000 | 2026-07-19T03:07:06.9337412+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 816,133 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,629,000 | 2026-07-19T03:07:07.3733377+00:00 | 154.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 816,133 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,649,000 | 2026-07-19T03:07:07.3977845+00:00 | 174.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 816,133 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,659,000 | 2026-07-19T03:07:07.4094311+00:00 | 190.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 816,133 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,680,000 | 2026-07-19T03:07:07.4384543+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 816,133 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,689,000 | 2026-07-19T03:07:07.5078423+00:00 | 142.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,709,000 | 2026-07-19T03:07:07.558946+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,281,000 | 2026-07-19T03:07:08.2496556+00:00 | 138.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,282,000 | 2026-07-19T03:07:08.2499852+00:00 | 138.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,289,000 | 2026-07-19T03:07:08.2618304+00:00 | 132.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,309,000 | 2026-07-19T03:07:08.2948194+00:00 | 135.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,319,000 | 2026-07-19T03:07:08.3239191+00:00 | 143.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,323,000 | 2026-07-19T03:07:08.3501423+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,329,000 | 2026-07-19T03:07:08.3680887+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,363,000 | 2026-07-19T03:07:08.4076841+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,387,000 | 2026-07-19T03:07:08.4449652+00:00 | 124.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 747,245 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,247,000 | 2026-07-19T03:07:09.4109644+00:00 | 132.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 894,918 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,307,000 | 2026-07-19T03:07:09.5149837+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 894,918 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,437,000 | 2026-07-19T03:07:09.6805599+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,597,000 | 2026-07-19T03:07:09.8735882+00:00 | 117.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,923,000 | 2026-07-19T03:07:10.2411299+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,959,000 | 2026-07-19T03:07:10.2891181+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,003,000 | 2026-07-19T03:07:10.3532591+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,013,000 | 2026-07-19T03:07:10.3672554+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,029,000 | 2026-07-19T03:07:10.3904776+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,063,000 | 2026-07-19T03:07:10.4314794+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 830,548 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,503,000 | 2026-07-19T03:07:10.9057225+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 990,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,529,000 | 2026-07-19T03:07:10.926727+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 990,428 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,513,000 | 2026-07-19T03:07:11.8829305+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,010,115 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,533,000 | 2026-07-19T03:07:11.9056537+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,010,115 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,539,000 | 2026-07-19T03:07:11.9131744+00:00 | 124.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,010,115 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,543,000 | 2026-07-19T03:07:11.915398+00:00 | 126.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,010,115 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,549,000 | 2026-07-19T03:07:11.9207228+00:00 | 129.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 1,010,115 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,069,000 | 2026-07-19T03:07:13.4232861+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,035,552 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,083,000 | 2026-07-19T03:07:13.4323786+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,035,552 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,099,000 | 2026-07-19T03:07:13.4462878+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,035,552 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,103,000 | 2026-07-19T03:07:14.4166578+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,043,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,109,000 | 2026-07-19T03:07:14.4198097+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,043,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,133,000 | 2026-07-19T03:07:14.4375228+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,043,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,669,000 | 2026-07-19T03:07:14.9279154+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,090,940 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,673,000 | 2026-07-19T03:07:14.9344086+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,090,940 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,702,000 | 2026-07-19T03:07:15.8905495+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,039,419 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 15,712,000 | 2026-07-19T03:07:15.9002732+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,039,419 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 17,849,000 | 2026-07-19T03:07:17.9470938+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,076,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,853,000 | 2026-07-19T03:07:17.9490045+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,076,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,319,000 | 2026-07-19T03:07:18.3996933+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,076,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,339,000 | 2026-07-19T03:07:18.4159106+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,076,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,359,000 | 2026-07-19T03:07:18.4299255+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,076,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,373,000 | 2026-07-19T03:07:18.4433477+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,076,758 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,449,000 | 2026-07-19T03:07:19.429004+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,080,399 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,489,000 | 2026-07-19T03:07:19.4702117+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,080,399 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,043,000 | 2026-07-19T03:07:20.890877+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,095,050 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,083,000 | 2026-07-19T03:07:20.9339409+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,095,050 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,609,000 | 2026-07-19T03:07:21.3937814+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,095,050 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,629,000 | 2026-07-19T03:07:21.4095011+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,095,050 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,649,000 | 2026-07-19T03:07:21.4259167+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,095,050 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,653,000 | 2026-07-19T03:07:21.4282752+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,095,050 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,243,000 | 2026-07-19T03:07:21.9667169+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,043,372 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,689,000 | 2026-07-19T03:07:22.400096+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,043,372 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,699,000 | 2026-07-19T03:07:22.4064334+00:00 | 124.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,043,372 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,713,000 | 2026-07-19T03:07:22.4208876+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,043,372 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,723,000 | 2026-07-19T03:07:22.4309478+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,043,372 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,169,000 | 2026-07-19T03:07:22.8889201+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,049,638 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,183,000 | 2026-07-19T03:07:22.9057185+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,049,638 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,189,000 | 2026-07-19T03:07:22.9124373+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,049,638 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,767,000 | 2026-07-19T03:07:24.4345105+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,049,711 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,889,000 | 2026-07-19T03:07:26.3903589+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,069,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,899,000 | 2026-07-19T03:07:26.4010836+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,069,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,923,000 | 2026-07-19T03:07:26.4252433+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,069,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,929,000 | 2026-07-19T03:07:26.4322632+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,069,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,071,000 | 2026-07-19T03:07:31.9071568+00:00 | 109.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 36.0s / 1,045,959 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,079,000 | 2026-07-19T03:07:31.9142153+00:00 | 108.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 36.0s / 1,045,959 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,082,000 | 2026-07-19T03:07:31.9164253+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 36.0s / 1,045,959 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,099,000 | 2026-07-19T03:07:31.9329792+00:00 | 106.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 36.0s / 1,045,959 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,101,000 | 2026-07-19T03:07:31.9339805+00:00 | 121.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 36.0s / 1,045,959 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 33,112,000 | 2026-07-19T03:07:31.9475797+00:00 | 109.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 36.0s / 1,045,959 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,773,000 | 2026-07-19T03:07:34.4199791+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 38.0s / 1,109,339 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,783,000 | 2026-07-19T03:07:34.4333813+00:00 | 105.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 38.0s / 1,109,339 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 36,963,000 | 2026-07-19T03:07:35.4239851+00:00 | 102.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 39.0s / 1,171,731 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 36,983,000 | 2026-07-19T03:07:35.4347831+00:00 | 109.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 39.0s / 1,171,731 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 36,989,000 | 2026-07-19T03:07:35.4394827+00:00 | 112.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 39.0s / 1,171,731 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 36,999,000 | 2026-07-19T03:07:35.4586908+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 39.0s / 1,171,731 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,877,000 | 2026-07-19T03:07:38.9259315+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 43.0s / 1,121,542 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 41,993,000 | 2026-07-19T03:07:39.9428+00:00 | 103.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 44.0s / 1,072,850 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 41,999,000 | 2026-07-19T03:07:39.9500543+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed, 1:capacity/failed | - | 44.0s / 1,072,850 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 49,642,000 | 2026-07-19T03:07:46.9167798+00:00 | 109.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 51.1s / 1,149,549 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 49,661,000 | 2026-07-19T03:07:46.9435243+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 51.1s / 1,149,549 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 50,861,000 | 2026-07-19T03:07:47.9480301+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 52.1s / 1,122,294 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 52,437,000 | 2026-07-19T03:07:49.4009492+00:00 | 100.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 53.1s / 1,072,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 60,829,000 | 2026-07-19T03:07:56.9358507+00:00 | 111.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 61.1s / 1,063,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 60,839,000 | 2026-07-19T03:07:56.9445936+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 61.1s / 1,063,904 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 68,627,000 | 2026-07-19T03:08:03.9180644+00:00 | 106.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 68.1s / 1,097,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 69,177,000 | 2026-07-19T03:08:04.4199416+00:00 | 106.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 68.1s / 1,097,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 69,197,000 | 2026-07-19T03:08:04.4364429+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 68.1s / 1,097,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 69,207,000 | 2026-07-19T03:08:04.4469637+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 68.1s / 1,097,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 84,267,000 | 2026-07-19T03:08:17.9352985+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded | - | 82.1s / 1,050,723 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 89,772,000 | 2026-07-19T03:08:22.9327134+00:00 | 109.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded, 3:capacity/failed | - | 87.1s / 1,060,531 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 107,777,000 | 2026-07-19T03:08:38.9583813+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded | - | 103.1s / 1,144,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 108,347,000 | 2026-07-19T03:08:39.4390278+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded | - | 103.1s / 1,144,198 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 108,847,000 | 2026-07-19T03:08:39.936779+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 2:capacity/succeeded | - | 104.1s / 1,057,630 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 439,860,000 | 2026-07-19T03:13:34.8727897+00:00 | 146.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 399.2s / 1,061,096 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 439,869,000 | 2026-07-19T03:13:34.8841239+00:00 | 147.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 399.2s / 1,061,096 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 439,870,000 | 2026-07-19T03:13:34.9023497+00:00 | 123.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 399.2s / 1,061,096 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 53,000 | 2026-07-19T03:37:10.3444641+00:00 | 118.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 59,000 | 2026-07-19T03:37:10.3513095+00:00 | 118.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 63,000 | 2026-07-19T03:37:10.3576622+00:00 | 122.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 69,000 | 2026-07-19T03:37:10.3656918+00:00 | 125.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 73,000 | 2026-07-19T03:37:10.4024212+00:00 | 102.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 79,000 | 2026-07-19T03:37:10.4090621+00:00 | 111.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 83,000 | 2026-07-19T03:37:10.4183004+00:00 | 102.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 89,000 | 2026-07-19T03:37:10.4255231+00:00 | 107.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 93,000 | 2026-07-19T03:37:10.4320872+00:00 | 119.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 99,000 | 2026-07-19T03:37:10.4402429+00:00 | 122.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 103,000 | 2026-07-19T03:37:10.4468742+00:00 | 130.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 109,000 | 2026-07-19T03:37:10.4656428+00:00 | 128.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 113,000 | 2026-07-19T03:37:10.470352+00:00 | 124.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 119,000 | 2026-07-19T03:37:10.4803511+00:00 | 180.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 123,000 | 2026-07-19T03:37:10.4863561+00:00 | 203.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 129,000 | 2026-07-19T03:37:10.4997799+00:00 | 203.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 132,000 | 2026-07-19T03:37:10.5064179+00:00 | 130.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 133,000 | 2026-07-19T03:37:10.5075185+00:00 | 227.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 137,000 | 2026-07-19T03:37:10.5203831+00:00 | 108.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 138,000 | 2026-07-19T03:37:10.5215573+00:00 | 125.5ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 139,000 | 2026-07-19T03:37:10.5226262+00:00 | 225.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 140,000 | 2026-07-19T03:37:10.529829+00:00 | 111.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 141,000 | 2026-07-19T03:37:10.5308595+00:00 | 127.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 142,000 | 2026-07-19T03:37:10.5322445+00:00 | 126.5ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 143,000 | 2026-07-19T03:37:10.5332324+00:00 | 215.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 145,000 | 2026-07-19T03:37:10.5355901+00:00 | 116.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 147,000 | 2026-07-19T03:37:10.5376027+00:00 | 105.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 148,000 | 2026-07-19T03:37:10.5387357+00:00 | 113.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 149,000 | 2026-07-19T03:37:10.5398454+00:00 | 219.5ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 150,000 | 2026-07-19T03:37:10.5434901+00:00 | 107.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 151,000 | 2026-07-19T03:37:10.5448534+00:00 | 123.5ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 152,000 | 2026-07-19T03:37:10.5514667+00:00 | 116.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 153,000 | 2026-07-19T03:37:10.5525001+00:00 | 217.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 155,000 | 2026-07-19T03:37:10.5548818+00:00 | 109.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 157,000 | 2026-07-19T03:37:10.5613655+00:00 | 142.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 158,000 | 2026-07-19T03:37:10.5624005+00:00 | 101.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 159,000 | 2026-07-19T03:37:10.5635878+00:00 | 220.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 160,000 | 2026-07-19T03:37:10.5645014+00:00 | 111.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 161,000 | 2026-07-19T03:37:10.5658083+00:00 | 106.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 162,000 | 2026-07-19T03:37:10.5675317+00:00 | 108.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 163,000 | 2026-07-19T03:37:10.5690386+00:00 | 223.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 165,000 | 2026-07-19T03:37:10.57768+00:00 | 107.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 167,000 | 2026-07-19T03:37:10.5796073+00:00 | 172.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 168,000 | 2026-07-19T03:37:10.5915587+00:00 | 107.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 169,000 | 2026-07-19T03:37:10.5926907+00:00 | 210.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 173,000 | 2026-07-19T03:37:10.5964686+00:00 | 206.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 177,000 | 2026-07-19T03:37:10.6423076+00:00 | 122.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 179,000 | 2026-07-19T03:37:10.6441938+00:00 | 170.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 183,000 | 2026-07-19T03:37:10.6829641+00:00 | 138.9ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 189,000 | 2026-07-19T03:37:10.7048285+00:00 | 131.5ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 193,000 | 2026-07-19T03:37:10.7083804+00:00 | 141.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 199,000 | 2026-07-19T03:37:10.7406648+00:00 | 122.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 203,000 | 2026-07-19T03:37:10.7506447+00:00 | 112.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 209,000 | 2026-07-19T03:37:10.7616253+00:00 | 107.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 213,000 | 2026-07-19T03:37:10.7701926+00:00 | 108.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 219,000 | 2026-07-19T03:37:10.7848235+00:00 | 109.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 223,000 | 2026-07-19T03:37:10.7891273+00:00 | 121.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 229,000 | 2026-07-19T03:37:10.8001737+00:00 | 113.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 233,000 | 2026-07-19T03:37:10.8048192+00:00 | 108.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 239,000 | 2026-07-19T03:37:10.8166769+00:00 | 102.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 243,000 | 2026-07-19T03:37:10.8221587+00:00 | 104.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 249,000 | 2026-07-19T03:37:10.8337892+00:00 | 100.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 259,000 | 2026-07-19T03:37:10.8537128+00:00 | 104.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 269,000 | 2026-07-19T03:37:10.8715084+00:00 | 118.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 273,000 | 2026-07-19T03:37:10.876083+00:00 | 123.2ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 279,000 | 2026-07-19T03:37:10.8871632+00:00 | 122.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 283,000 | 2026-07-19T03:37:10.9011327+00:00 | 124.8ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 289,000 | 2026-07-19T03:37:10.9115829+00:00 | 126.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 293,000 | 2026-07-19T03:37:10.9156432+00:00 | 122.0ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 299,000 | 2026-07-19T03:37:10.9217602+00:00 | 123.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 303,000 | 2026-07-19T03:37:10.9260513+00:00 | 125.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 309,000 | 2026-07-19T03:37:10.9342031+00:00 | 123.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 310,000 | 2026-07-19T03:37:10.9359176+00:00 | 100.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 313,000 | 2026-07-19T03:37:10.9471593+00:00 | 124.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 319,000 | 2026-07-19T03:37:10.9575717+00:00 | 124.7ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 323,000 | 2026-07-19T03:37:10.9687148+00:00 | 113.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 329,000 | 2026-07-19T03:37:10.9911252+00:00 | 108.1ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 333,000 | 2026-07-19T03:37:10.9993479+00:00 | 106.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 339,000 | 2026-07-19T03:37:11.0082256+00:00 | 109.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 343,000 | 2026-07-19T03:37:11.0143985+00:00 | 112.5ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 349,000 | 2026-07-19T03:37:11.0301404+00:00 | 103.4ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 359,000 | 2026-07-19T03:37:11.0473975+00:00 | 102.3ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 379,000 | 2026-07-19T03:37:11.0792981+00:00 | 103.6ms | GC pause | - | - | 1.0s / 463,421 msg/s | Gen2 +1 / pause +0.5ms |
| Dekaf (3conn) | 950,000 | 2026-07-19T03:37:11.9521374+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 706,057 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 960,000 | 2026-07-19T03:37:11.9580254+00:00 | 109.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 706,057 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 961,000 | 2026-07-19T03:37:11.9584582+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 706,057 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 962,000 | 2026-07-19T03:37:11.9591838+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 706,057 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,350,000 | 2026-07-19T03:37:12.4493837+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,360,000 | 2026-07-19T03:37:12.4587816+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,370,000 | 2026-07-19T03:37:12.4911006+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,619,000 | 2026-07-19T03:37:12.7930824+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,623,000 | 2026-07-19T03:37:12.7971557+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,629,000 | 2026-07-19T03:37:12.8050782+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,713,000 | 2026-07-19T03:37:12.9237444+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,719,000 | 2026-07-19T03:37:12.9271078+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,723,000 | 2026-07-19T03:37:12.9288741+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,729,000 | 2026-07-19T03:37:12.9351077+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,733,000 | 2026-07-19T03:37:12.9391693+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,739,000 | 2026-07-19T03:37:12.9444189+00:00 | 114.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,743,000 | 2026-07-19T03:37:12.9476357+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,749,000 | 2026-07-19T03:37:12.9516744+00:00 | 117.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,753,000 | 2026-07-19T03:37:12.9536171+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,759,000 | 2026-07-19T03:37:12.9621771+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,763,000 | 2026-07-19T03:37:12.9810467+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 828,200 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,939,000 | 2026-07-19T03:37:13.1872253+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,943,000 | 2026-07-19T03:37:13.1896675+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,949,000 | 2026-07-19T03:37:13.1970519+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,953,000 | 2026-07-19T03:37:13.20797+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,959,000 | 2026-07-19T03:37:13.2108983+00:00 | 120.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,963,000 | 2026-07-19T03:37:13.2183712+00:00 | 122.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,969,000 | 2026-07-19T03:37:13.2243383+00:00 | 132.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,973,000 | 2026-07-19T03:37:13.2262276+00:00 | 130.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,979,000 | 2026-07-19T03:37:13.2304692+00:00 | 132.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,983,000 | 2026-07-19T03:37:13.2356601+00:00 | 133.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,989,000 | 2026-07-19T03:37:13.2420251+00:00 | 132.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,993,000 | 2026-07-19T03:37:13.2467445+00:00 | 135.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,999,000 | 2026-07-19T03:37:13.2539354+00:00 | 134.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,003,000 | 2026-07-19T03:37:13.2730429+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,009,000 | 2026-07-19T03:37:13.2786301+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,013,000 | 2026-07-19T03:37:13.2857613+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,090,000 | 2026-07-19T03:37:13.3970176+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,100,000 | 2026-07-19T03:37:13.4050413+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,110,000 | 2026-07-19T03:37:13.4149089+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,140,000 | 2026-07-19T03:37:13.464142+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,180,000 | 2026-07-19T03:37:13.5331111+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,200,000 | 2026-07-19T03:37:13.5577951+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,510,000 | 2026-07-19T03:37:13.9167398+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,520,000 | 2026-07-19T03:37:13.9215583+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,530,000 | 2026-07-19T03:37:13.9306968+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,540,000 | 2026-07-19T03:37:13.9430131+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,550,000 | 2026-07-19T03:37:13.9500162+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,560,000 | 2026-07-19T03:37:13.9604266+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 794,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,933,000 | 2026-07-19T03:37:14.3887012+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,939,000 | 2026-07-19T03:37:14.3952523+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,943,000 | 2026-07-19T03:37:14.3989265+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,949,000 | 2026-07-19T03:37:14.4021936+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,953,000 | 2026-07-19T03:37:14.4120336+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,959,000 | 2026-07-19T03:37:14.4169843+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,433,000 | 2026-07-19T03:37:14.8888333+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,439,000 | 2026-07-19T03:37:14.9006408+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,443,000 | 2026-07-19T03:37:14.9092203+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,449,000 | 2026-07-19T03:37:14.9147666+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,453,000 | 2026-07-19T03:37:14.9200889+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,459,000 | 2026-07-19T03:37:14.9274879+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,463,000 | 2026-07-19T03:37:14.931108+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,469,000 | 2026-07-19T03:37:14.935201+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,473,000 | 2026-07-19T03:37:14.937594+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,004,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,930,000 | 2026-07-19T03:37:16.3968578+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,940,000 | 2026-07-19T03:37:16.402834+00:00 | 117.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,950,000 | 2026-07-19T03:37:16.4137874+00:00 | 123.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,960,000 | 2026-07-19T03:37:16.4208163+00:00 | 132.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,970,000 | 2026-07-19T03:37:16.4421704+00:00 | 118.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,980,000 | 2026-07-19T03:37:16.4511513+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,990,000 | 2026-07-19T03:37:16.4719139+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,429,000 | 2026-07-19T03:37:16.90822+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,433,000 | 2026-07-19T03:37:16.9157332+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,439,000 | 2026-07-19T03:37:16.9246185+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,443,000 | 2026-07-19T03:37:16.929752+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,449,000 | 2026-07-19T03:37:16.9337768+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,453,000 | 2026-07-19T03:37:16.9377737+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,459,000 | 2026-07-19T03:37:16.9420472+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,463,000 | 2026-07-19T03:37:16.9469877+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 931,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,840,000 | 2026-07-19T03:37:17.3982924+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,848,000 | 2026-07-19T03:37:17.4042879+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,850,000 | 2026-07-19T03:37:17.4068312+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,855,000 | 2026-07-19T03:37:17.4127636+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,860,000 | 2026-07-19T03:37:17.4197323+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,870,000 | 2026-07-19T03:37:17.4403962+00:00 | 118.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,880,000 | 2026-07-19T03:37:17.4524348+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,350,000 | 2026-07-19T03:37:17.9234248+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 934,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,930,000 | 2026-07-19T03:37:18.5610509+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 997,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,270,000 | 2026-07-19T03:37:18.9072219+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 997,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,280,000 | 2026-07-19T03:37:18.9138866+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 997,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,300,000 | 2026-07-19T03:37:18.9375435+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 997,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,800,000 | 2026-07-19T03:37:19.4001775+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,810,000 | 2026-07-19T03:37:19.4088744+00:00 | 127.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,815,000 | 2026-07-19T03:37:19.4159102+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,818,000 | 2026-07-19T03:37:19.4210328+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,820,000 | 2026-07-19T03:37:19.4216252+00:00 | 134.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,825,000 | 2026-07-19T03:37:19.4254572+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,828,000 | 2026-07-19T03:37:19.42637+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,830,000 | 2026-07-19T03:37:19.4351791+00:00 | 130.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,835,000 | 2026-07-19T03:37:19.4383161+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,838,000 | 2026-07-19T03:37:19.4441909+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,840,000 | 2026-07-19T03:37:19.4451722+00:00 | 145.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,850,000 | 2026-07-19T03:37:19.490102+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,260,000 | 2026-07-19T03:37:19.9469098+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,270,000 | 2026-07-19T03:37:19.956632+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 853,935 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,480,000 | 2026-07-19T03:37:20.2363236+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 912,033 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,490,000 | 2026-07-19T03:37:20.2458474+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 912,033 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,781,000 | 2026-07-19T03:37:20.5965409+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 912,033 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,782,000 | 2026-07-19T03:37:20.5972099+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 912,033 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,791,000 | 2026-07-19T03:37:20.609613+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 912,033 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,792,000 | 2026-07-19T03:37:20.609952+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 912,033 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,713,000 | 2026-07-19T03:37:22.4376862+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,151,523 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,771,000 | 2026-07-19T03:37:23.381592+00:00 | 138.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,772,000 | 2026-07-19T03:37:23.3826389+00:00 | 137.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,781,000 | 2026-07-19T03:37:23.3969027+00:00 | 137.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,782,000 | 2026-07-19T03:37:23.3975491+00:00 | 139.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,791,000 | 2026-07-19T03:37:23.4033507+00:00 | 139.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,792,000 | 2026-07-19T03:37:23.4037336+00:00 | 138.6ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,801,000 | 2026-07-19T03:37:23.4134077+00:00 | 142.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,802,000 | 2026-07-19T03:37:23.4164237+00:00 | 139.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,811,000 | 2026-07-19T03:37:23.4271954+00:00 | 133.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,812,000 | 2026-07-19T03:37:23.4277794+00:00 | 136.6ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,821,000 | 2026-07-19T03:37:23.4395464+00:00 | 129.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,822,000 | 2026-07-19T03:37:23.4398778+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,831,000 | 2026-07-19T03:37:23.4499096+00:00 | 125.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,832,000 | 2026-07-19T03:37:23.4505701+00:00 | 125.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,150,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,130,000 | 2026-07-19T03:37:26.9665342+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,150,936 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,789,000 | 2026-07-19T03:37:28.4102958+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,207,803 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,793,000 | 2026-07-19T03:37:28.417222+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,207,803 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,799,000 | 2026-07-19T03:37:28.4263618+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,207,803 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,463,000 | 2026-07-19T03:37:31.4561955+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,176,650 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,469,000 | 2026-07-19T03:37:31.4619791+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,176,650 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,473,000 | 2026-07-19T03:37:31.4638343+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,176,650 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,479,000 | 2026-07-19T03:37:31.4671908+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,176,650 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,483,000 | 2026-07-19T03:37:31.4687458+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,176,650 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 28,243,000 | 2026-07-19T03:37:36.9670194+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,195,219 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 37,629,000 | 2026-07-19T03:37:44.4577408+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 35.0s / 1,175,135 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 37,633,000 | 2026-07-19T03:37:44.4595373+00:00 | 101.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 35.0s / 1,175,135 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 37,639,000 | 2026-07-19T03:37:44.4633727+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 35.0s / 1,175,135 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 42,031,000 | 2026-07-19T03:37:47.9566539+00:00 | 102.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 38.0s / 1,273,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 42,032,000 | 2026-07-19T03:37:47.9574006+00:00 | 101.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 38.0s / 1,273,892 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,863,000 | 2026-07-19T03:37:50.9310968+00:00 | 106.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,869,000 | 2026-07-19T03:37:50.9346283+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,873,000 | 2026-07-19T03:37:50.9362748+00:00 | 105.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,879,000 | 2026-07-19T03:37:50.9443207+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,883,000 | 2026-07-19T03:37:50.9472469+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,889,000 | 2026-07-19T03:37:50.9516213+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,893,000 | 2026-07-19T03:37:50.9539884+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 45,899,000 | 2026-07-19T03:37:50.9613055+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded, 3:capacity/failed | - | 41.0s / 1,228,103 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,939,000 | 2026-07-19T03:38:26.4189559+00:00 | 105.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,943,000 | 2026-07-19T03:38:26.422758+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,949,000 | 2026-07-19T03:38:26.4251788+00:00 | 112.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,953,000 | 2026-07-19T03:38:26.427056+00:00 | 110.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,959,000 | 2026-07-19T03:38:26.4309446+00:00 | 108.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,963,000 | 2026-07-19T03:38:26.4347982+00:00 | 113.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,969,000 | 2026-07-19T03:38:26.4460436+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,973,000 | 2026-07-19T03:38:26.4507469+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,979,000 | 2026-07-19T03:38:26.4582368+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 77.1s / 1,245,931 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 92,249,000 | 2026-07-19T03:38:27.4502097+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 78.1s / 1,212,665 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 92,253,000 | 2026-07-19T03:38:27.4534142+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 78.1s / 1,212,665 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 120,359,000 | 2026-07-19T03:38:49.9274389+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 100.1s / 1,184,720 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 120,363,000 | 2026-07-19T03:38:49.929974+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 100.1s / 1,184,720 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 120,373,000 | 2026-07-19T03:38:49.9398154+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 100.1s / 1,184,720 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 120,379,000 | 2026-07-19T03:38:49.9432962+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 100.1s / 1,184,720 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 432,551,000 | 2026-07-19T03:43:00.5304488+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 351.2s / 1,151,529 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 905,540,000 | 2026-07-19T03:49:28.8913692+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 739.3s / 1,158,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 905,550,000 | 2026-07-19T03:49:28.8971184+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 739.3s / 1,158,038 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*701 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.56x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.27x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.13 | 3572.48 | 1,083,608 | 1,815,682 | +63.2% | +496.90% | 132.28 | 1,083,608 | 0 | 1.22 |
| Confluent | 1.81 | - | 130,745 | 1,565,971 | +4.0% | +58.44% | 15.96 | 130,745 | 0 | 0.24 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 6,248 | 526.66 | 603.32 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:38.9398159+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 380,517 msg/s |
| Dekaf | 2026-07-19T03:06:39.9406659+00:00 | 1 | 16.0 MiB / 0.4 MiB | 67.2 MB/s | 0/0 | 0 | 1.0s / 380,517 msg/s |
| Dekaf | 2026-07-19T03:06:40.9414119+00:00 | 1 | 16.0 MiB / 1.2 MiB | 138.4 MB/s | 0/0 | 0 | 2.0s / 1,231,907 msg/s |
| Dekaf | 2026-07-19T03:06:41.9420076+00:00 | 1 | 16.0 MiB / 2.6 MiB | 328.3 MB/s | 0/0 | 0 | 3.0s / 1,815,682 msg/s |
| Dekaf | 2026-07-19T03:06:42.9435826+00:00 | 1 | 16.0 MiB / 3.3 MiB | 374.6 MB/s | 0/0 | 0 | 4.0s / 1,810,607 msg/s |
| Dekaf | 2026-07-19T03:06:43.9475525+00:00 | 1 | 16.0 MiB / 3.0 MiB | 375.6 MB/s | 0/0 | 0 | 5.0s / 1,882,322 msg/s |
| Dekaf | 2026-07-19T03:06:44.947305+00:00 | 1 | 16.0 MiB / 2.1 MiB | 375.6 MB/s | 0/0 | 0 | 6.0s / 1,850,758 msg/s |
| Dekaf | 2026-07-19T03:06:45.9493337+00:00 | 1 | 16.0 MiB / 4.5 MiB | 375.6 MB/s | 0/0 | 0 | 7.0s / 1,617,180 msg/s |
| Dekaf | 2026-07-19T03:06:46.9515429+00:00 | 1 | 16.0 MiB / 3.4 MiB | 446.3 MB/s | 0/0 | 0 | 8.0s / 2,230,470 msg/s |
| Dekaf | 2026-07-19T03:06:47.9511177+00:00 | 1 | 16.0 MiB / 3.1 MiB | 446.3 MB/s | 0/0 | 0 | 9.0s / 1,991,359 msg/s |
| Dekaf | 2026-07-19T03:06:48.9513541+00:00 | 1 | 16.0 MiB / 1.6 MiB | 446.3 MB/s | 0/0 | 0 | 10.0s / 1,873,715 msg/s |
| Dekaf | 2026-07-19T03:06:49.9525791+00:00 | 1 | 16.0 MiB / 1.1 MiB | 446.3 MB/s | 0/0 | 0 | 11.0s / 1,730,307 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.61x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.16x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 460.81 | 460.80 | 266 | 356 | +2.0% | +0.20% | 0.25 | 354 | 0 | 0.16 |
| Confluent | 306.86 | - | 129 | 172 | +0.3% | +0.03% | 0.12 | 172 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 106,052 | 117.80 | 1.20 KB |
| Dekaf | 2 | 106,883 | 118.73 | 1.20 KB |
| Dekaf | 3 | 105,871 | 117.60 | 1.20 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-19T03:06:43.9603359+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 191 msg/s |
| Dekaf | 2026-07-19T03:06:52.9784426+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 9.0s / 327 msg/s |
| Dekaf | 2026-07-19T03:07:01.9903314+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 18.0s / 330 msg/s |
| Dekaf | 2026-07-19T03:07:12.0062923+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 28.0s / 352 msg/s |
| Dekaf | 2026-07-19T03:07:21.0094114+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 37.0s / 344 msg/s |
| Dekaf | 2026-07-19T03:07:30.0178664+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 46.0s / 344 msg/s |
| Dekaf | 2026-07-19T03:07:39.020398+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 55.0s / 347 msg/s |
| Dekaf | 2026-07-19T03:07:48.0298542+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 64.0s / 329 msg/s |
| Dekaf | 2026-07-19T03:07:57.035207+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 73.0s / 337 msg/s |
| Dekaf | 2026-07-19T03:08:06.0438364+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 82.0s / 358 msg/s |
| Dekaf | 2026-07-19T03:08:15.0582173+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 356 msg/s |
| Dekaf | 2026-07-19T03:08:24.0626176+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 100.0s / 348 msg/s |
| Dekaf | 2026-07-19T03:08:33.0786274+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 109.0s / 348 msg/s |
| Dekaf | 2026-07-19T03:08:42.0830004+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 118.0s / 345 msg/s |
| Dekaf | 2026-07-19T03:08:51.1067416+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 354 msg/s |
| Dekaf | 2026-07-19T03:09:01.1289513+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 137.0s / 356 msg/s |
| Dekaf | 2026-07-19T03:09:10.1351744+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 146.0s / 347 msg/s |
| Dekaf | 2026-07-19T03:09:19.1386293+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 358 msg/s |
| Dekaf | 2026-07-19T03:09:28.1406797+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 357 msg/s |
| Dekaf | 2026-07-19T03:09:37.154081+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 364 msg/s |
| Dekaf | 2026-07-19T03:09:46.1591727+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 362 msg/s |
| Dekaf | 2026-07-19T03:09:55.1866787+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 354 msg/s |
| Dekaf | 2026-07-19T03:10:04.1900385+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 347 msg/s |
| Dekaf | 2026-07-19T03:10:13.1962125+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 350 msg/s |
| Dekaf | 2026-07-19T03:10:22.2158849+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 350 msg/s |
| Dekaf | 2026-07-19T03:10:31.2161507+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 356 msg/s |
| Dekaf | 2026-07-19T03:10:40.2211033+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 365 msg/s |
| Dekaf | 2026-07-19T03:10:50.2485648+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 246.0s / 357 msg/s |
| Dekaf | 2026-07-19T03:10:59.2526165+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 255.0s / 355 msg/s |
| Dekaf | 2026-07-19T03:11:08.2558115+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 264.0s / 357 msg/s |
| Dekaf | 2026-07-19T03:11:17.256397+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 273.0s / 355 msg/s |
| Dekaf | 2026-07-19T03:11:26.2854774+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 344 msg/s |
| Dekaf | 2026-07-19T03:11:35.2900416+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 291.1s / 349 msg/s |
| Dekaf | 2026-07-19T03:11:44.3057378+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 300.1s / 343 msg/s |
| Dekaf | 2026-07-19T03:11:53.31186+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 309.1s / 352 msg/s |
| Dekaf | 2026-07-19T03:12:02.338008+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 318.1s / 359 msg/s |
| Dekaf | 2026-07-19T03:12:11.3410084+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 327.1s / 365 msg/s |
| Dekaf | 2026-07-19T03:12:20.3443173+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.1s / 357 msg/s |
| Dekaf | 2026-07-19T03:12:29.361208+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 345.1s / 367 msg/s |
| Dekaf | 2026-07-19T03:12:38.3665328+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 356 msg/s |
| Dekaf | 2026-07-19T03:12:48.3722367+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 364.1s / 359 msg/s |
| Dekaf | 2026-07-19T03:12:57.3923974+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 373.1s / 362 msg/s |
| Dekaf | 2026-07-19T03:13:06.4024987+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 382.1s / 350 msg/s |
| Dekaf | 2026-07-19T03:13:15.4151486+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 391.1s / 368 msg/s |
| Dekaf | 2026-07-19T03:13:24.4424537+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 400.1s / 359 msg/s |
| Dekaf | 2026-07-19T03:13:33.4464676+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 409.1s / 366 msg/s |
| Dekaf | 2026-07-19T03:13:42.461008+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 418.1s / 357 msg/s |
| Dekaf | 2026-07-19T03:13:51.4792113+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 427.1s / 371 msg/s |
| Dekaf | 2026-07-19T03:14:00.4839654+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 436.1s / 331 msg/s |
| Dekaf | 2026-07-19T03:14:09.4893785+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 445.1s / 347 msg/s |
| Dekaf | 2026-07-19T03:14:18.513004+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 454.1s / 352 msg/s |
| Dekaf | 2026-07-19T03:14:27.5203313+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 463.1s / 351 msg/s |
| Dekaf | 2026-07-19T03:14:37.536612+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 359 msg/s |
| Dekaf | 2026-07-19T03:14:46.558722+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 354 msg/s |
| Dekaf | 2026-07-19T03:14:55.5755734+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 356 msg/s |
| Dekaf | 2026-07-19T03:15:04.5992119+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 365 msg/s |
| Dekaf | 2026-07-19T03:15:13.6130124+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 509.1s / 354 msg/s |
| Dekaf | 2026-07-19T03:15:22.6223245+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 518.1s / 354 msg/s |
| Dekaf | 2026-07-19T03:15:31.629224+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 527.1s / 358 msg/s |
| Dekaf | 2026-07-19T03:15:40.6410555+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 536.1s / 368 msg/s |
| Dekaf | 2026-07-19T03:15:49.6441908+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 545.1s / 354 msg/s |
| Dekaf | 2026-07-19T03:15:58.6683431+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 554.1s / 357 msg/s |
| Dekaf | 2026-07-19T03:16:07.6820282+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 563.1s / 365 msg/s |
| Dekaf | 2026-07-19T03:16:16.7140746+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 573.1s / 370 msg/s |
| Dekaf | 2026-07-19T03:16:26.7230387+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 583.1s / 361 msg/s |
| Dekaf | 2026-07-19T03:16:35.7308099+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 592.1s / 360 msg/s |
| Dekaf | 2026-07-19T03:16:44.74898+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 601.1s / 344 msg/s |
| Dekaf | 2026-07-19T03:16:53.7744693+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 610.1s / 356 msg/s |
| Dekaf | 2026-07-19T03:17:02.7815715+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 619.1s / 348 msg/s |
| Dekaf | 2026-07-19T03:17:11.810118+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 628.1s / 353 msg/s |
| Dekaf | 2026-07-19T03:17:20.8290372+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 637.1s / 351 msg/s |
| Dekaf | 2026-07-19T03:17:29.8321809+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 646.1s / 355 msg/s |
| Dekaf | 2026-07-19T03:17:38.8362742+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 655.1s / 365 msg/s |
| Dekaf | 2026-07-19T03:17:47.8422445+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 664.1s / 368 msg/s |
| Dekaf | 2026-07-19T03:17:56.8520501+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 359 msg/s |
| Dekaf | 2026-07-19T03:18:05.8653624+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 360 msg/s |
| Dekaf | 2026-07-19T03:18:14.869052+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 370 msg/s |
| Dekaf | 2026-07-19T03:18:24.886269+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 701.1s / 357 msg/s |
| Dekaf | 2026-07-19T03:18:33.8967267+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 710.1s / 359 msg/s |
| Dekaf | 2026-07-19T03:18:42.8990321+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 719.1s / 361 msg/s |
| Dekaf | 2026-07-19T03:18:51.9048026+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 728.1s / 366 msg/s |
| Dekaf | 2026-07-19T03:19:00.9087446+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 737.1s / 358 msg/s |
| Dekaf | 2026-07-19T03:19:09.9270919+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 746.1s / 367 msg/s |
| Dekaf | 2026-07-19T03:19:18.932626+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 755.1s / 356 msg/s |
| Dekaf | 2026-07-19T03:19:27.9355112+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 764.1s / 345 msg/s |
| Dekaf | 2026-07-19T03:19:36.9459875+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 773.1s / 336 msg/s |
| Dekaf | 2026-07-19T03:19:45.9630113+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 782.1s / 352 msg/s |
| Dekaf | 2026-07-19T03:19:54.9806519+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 791.1s / 343 msg/s |
| Dekaf | 2026-07-19T03:20:03.9873744+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 348 msg/s |
| Dekaf | 2026-07-19T03:20:13.9902946+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 810.1s / 351 msg/s |
| Dekaf | 2026-07-19T03:20:23.0177405+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 819.1s / 316 msg/s |
| Dekaf | 2026-07-19T03:20:32.0313969+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 828.2s / 353 msg/s |
| Dekaf | 2026-07-19T03:20:41.0397943+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 837.2s / 355 msg/s |
| Dekaf | 2026-07-19T03:20:50.0444857+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 846.2s / 339 msg/s |
| Dekaf | 2026-07-19T03:20:59.0490447+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 855.2s / 362 msg/s |
| Dekaf | 2026-07-19T03:21:08.0571011+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 864.2s / 357 msg/s |
| Dekaf | 2026-07-19T03:21:17.0623843+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 873.2s / 357 msg/s |
| Dekaf | 2026-07-19T03:21:26.0687581+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 882.2s / 359 msg/s |
| Dekaf | 2026-07-19T03:21:35.0744692+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 891.2s / 357 msg/s |
| Dekaf | 2026-07-19T03:21:44.089931+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 899.2s / 339 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 155,200 | 116,400 | 38,800 | 116,400 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 318,800 | 239,100 | 79,700 | 239,100 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.50x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.07x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.72 | - | 1,817,051 | 1,815,547 | -2.7% | -0.27% | 1732.87 | - | 0 | 1.31 |
| Confluent | 1.08 | - | 1,367,873 | 1,383,117 | +1.4% | +0.14% | 1304.51 | - | 0 | 1.47 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.50x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.31x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.83 | - | 1,640,763 | 1,645,932 | -1.9% | -0.15% | 1564.75 | - | 0 | 1.36 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.50 | - | 3,272,611 | 3,280,804 | +1.4% | +0.16% | 3121.00 | - | 0 | 1.63 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.35 | - | 4,318,582 | 4,246,893 | +4.0% | +0.54% | 4118.52 | - | 0 | 1.49 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 24179 | 115 | 0 | 2797.56 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 243065 | 1 | 1 | 1207.83 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 249989 | 7 | 1 | 1199.35 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 205962 | 1 | 1 | 976.77 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 221791 | 1 | 1 | 1106.04 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 241303 | 26 | 1 | 1170.93 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 194375 | 175 | 1 | 909.00 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 280646 | 1 | 1 | 1441.00 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 295639 | 6 | 1 | 1420.59 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 202364 | 1 | 1 | 950.14 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 8330 | 1 | 1 | 21.41 GB | 1.13 KB |
| Confluent | Producer (Transactional EOS), 3 Brokers | 91 | 1 | 1 | 131.96 MB | 892 B |
| Dekaf | Consumer | 27144 | 27 | 3 | 3083.20 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 24490 | 3 | 2 | 2784.37 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 0 | 418.83 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 10 | 2 | 1 | 1.06 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 483 | 2 | 2 | 110.40 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 432 | 1 | 1 | 1.65 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 266 | 3 | 2 | 1.17 GB | 1 B |
| Dekaf | Producer (Acks All) | 448 | 2 | 2 | 110.58 MB | 0 B |
| Dekaf | Producer (Acks All) | 429 | 3 | 2 | 1.70 GB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 254 | 4 | 3 | 1007.68 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 483 | 2 | 2 | 115.94 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 456 | 1 | 1 | 1.71 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 248 | 2 | 2 | 1022.31 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1720 | 4 | 2 | 4.14 GB | 225 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 52 | 1 | 0 | 240.47 MB | 791 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 409 | 2 | 2 | 1.56 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 278 | 3 | 2 | 1.16 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 370 | 2 | 2 | 1.53 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 267 | 2 | 1 | 1.25 GB | 1 B |

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
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
