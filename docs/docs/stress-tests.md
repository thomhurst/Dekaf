---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-29 19:14 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,402,248 | 1,392,815–1,411,744 | 1.03 | 1.23x |
| Confluent | 2 | 1,136,146 | 1,099,621–1,173,885 | 1.52 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 1.04 | 1050.70 | 1,354,376 | 1,411,744 | -15.3% | -1.40% | 1291.63 | 1,354,376 | 0 | 1.40 |
| Dekaf (confluent-first) | 1.02 | 1045.32 | 1,382,821 | 1,392,815 | -5.3% | -0.43% | 1318.76 | 1,382,821 | 0 | 1.41 |
| Dekaf (3conn) | 0.89 | 748.68 | 1,168,557 | 1,175,119 | -1.4% | -0.08% | 1114.42 | 1,168,557 | 0 | 1.04 |
| Confluent (confluent-first) | 1.49 | - | 1,138,228 | 1,173,885 | -11.7% | -1.11% | 1085.50 | 1,138,228 | 0 | 1.69 |
| Confluent (dekaf-first) | 1.55 | - | 1,067,373 | 1,099,621 | +5.4% | +0.67% | 1017.93 | 1,067,373 | 0 | 1.65 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,201,630 | 1335.12 | 1008.39 KB |
| Dekaf | 1 | 1,217,405 | 1352.66 | 1016.23 KB |
| Dekaf (3conn) | 1 | 1,250,164 | 1389.06 | 836.26 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:10.2971493+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 381,996 msg/s |
| Dekaf | 2026-07-29T17:58:37.3034588+00:00 | 1 | 16.0 MiB / 14.0 MiB | 1447.1 MB/s | 0/0 | 24,172 | 27.0s / 1,388,023 msg/s |
| Dekaf | 2026-07-29T17:59:05.3104808+00:00 | 1 | 14.0 MiB / 12.5 MiB | 1490.4 MB/s | 1/0 | 60,175 | 55.0s / 1,323,270 msg/s |
| Dekaf | 2026-07-29T17:59:32.320881+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1580.3 MB/s | 1/0 | 102,916 | 82.0s / 1,408,373 msg/s |
| Dekaf | 2026-07-29T17:59:59.3300283+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1617.3 MB/s | 2/0 | 160,705 | 109.0s / 1,487,591 msg/s |
| Dekaf | 2026-07-29T18:00:26.3334681+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1628.2 MB/s | 2/1 | 209,406 | 136.0s / 1,493,887 msg/s |
| Dekaf | 2026-07-29T18:00:54.3421161+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1628.2 MB/s | 2/1 | 264,651 | 164.1s / 1,454,943 msg/s |
| Dekaf | 2026-07-29T18:01:21.3469658+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1628.2 MB/s | 2/1 | 327,109 | 191.1s / 1,478,375 msg/s |
| Dekaf | 2026-07-29T18:01:48.3547694+00:00 | 1 | 13.0 MiB / 11.3 MiB | 1628.2 MB/s | 3/1 | 387,347 | 218.1s / 1,504,456 msg/s |
| Dekaf | 2026-07-29T18:02:15.3661441+00:00 | 1 | 13.0 MiB / 9.9 MiB | 1628.2 MB/s | 3/1 | 445,413 | 245.1s / 1,486,869 msg/s |
| Dekaf | 2026-07-29T18:02:43.3728586+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1628.2 MB/s | 4/1 | 503,086 | 273.1s / 1,485,897 msg/s |
| Dekaf | 2026-07-29T18:03:10.3835508+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1628.2 MB/s | 4/1 | 557,525 | 300.1s / 1,473,709 msg/s |
| Dekaf | 2026-07-29T18:03:37.391682+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1634.8 MB/s | 4/2 | 614,012 | 327.1s / 1,525,054 msg/s |
| Dekaf | 2026-07-29T18:04:04.4056167+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1634.8 MB/s | 4/2 | 671,944 | 354.1s / 1,472,904 msg/s |
| Dekaf | 2026-07-29T18:04:32.4182608+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1634.8 MB/s | 5/2 | 731,717 | 382.1s / 1,479,341 msg/s |
| Dekaf | 2026-07-29T18:04:59.4233714+00:00 | 1 | 10.0 MiB / 5.2 MiB | 1634.8 MB/s | 5/2 | 787,318 | 409.1s / 1,276,620 msg/s |
| Dekaf | 2026-07-29T18:05:26.4319599+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1634.8 MB/s | 5/3 | 840,334 | 436.1s / 1,420,062 msg/s |
| Dekaf | 2026-07-29T18:05:53.4373372+00:00 | 1 | 12.0 MiB / 9.9 MiB | 1634.8 MB/s | 5/3 | 902,134 | 463.1s / 1,448,082 msg/s |
| Dekaf | 2026-07-29T18:06:21.4490328+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1634.8 MB/s | 5/3 | 962,887 | 491.1s / 1,379,570 msg/s |
| Dekaf | 2026-07-29T18:06:48.4574451+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1634.8 MB/s | 5/4 | 1,018,243 | 518.1s / 1,413,280 msg/s |
| Dekaf | 2026-07-29T18:07:15.4610015+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1634.8 MB/s | 5/4 | 1,075,021 | 545.1s / 1,330,924 msg/s |
| Dekaf | 2026-07-29T18:07:43.4726177+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1634.8 MB/s | 5/4 | 1,134,590 | 573.2s / 1,479,315 msg/s |
| Dekaf | 2026-07-29T18:08:10.4787286+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1634.8 MB/s | 5/4 | 1,195,749 | 600.2s / 1,471,883 msg/s |
| Dekaf | 2026-07-29T18:08:37.4847132+00:00 | 1 | 10.0 MiB / 1.4 MiB | 1634.8 MB/s | 5/4 | 1,247,042 | 627.2s / 1,157,501 msg/s |
| Dekaf | 2026-07-29T18:09:04.4973682+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1634.8 MB/s | 5/5 | 1,290,252 | 654.2s / 1,237,239 msg/s |
| Dekaf | 2026-07-29T18:09:32.5065807+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1634.8 MB/s | 5/5 | 1,336,737 | 682.2s / 1,406,706 msg/s |
| Dekaf | 2026-07-29T18:09:59.5116825+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1634.8 MB/s | 5/5 | 1,395,740 | 709.2s / 1,429,710 msg/s |
| Dekaf | 2026-07-29T18:10:26.5169642+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1634.8 MB/s | 5/5 | 1,451,584 | 736.2s / 1,374,868 msg/s |
| Dekaf | 2026-07-29T18:10:53.5226237+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1634.8 MB/s | 5/5 | 1,495,834 | 763.2s / 1,228,334 msg/s |
| Dekaf | 2026-07-29T18:11:21.5340636+00:00 | 1 | 12.0 MiB / 6.9 MiB | 1634.8 MB/s | 5/5 | 1,532,100 | 791.2s / 1,209,117 msg/s |
| Dekaf | 2026-07-29T18:11:48.5395341+00:00 | 1 | 12.0 MiB / 2.1 MiB | 1634.8 MB/s | 5/5 | 1,556,400 | 818.2s / 901,073 msg/s |
| Dekaf | 2026-07-29T18:12:15.5485827+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1634.8 MB/s | 5/5 | 1,583,484 | 845.2s / 1,115,901 msg/s |
| Dekaf | 2026-07-29T18:12:42.5600992+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1634.8 MB/s | 5/5 | 1,606,012 | 872.2s / 1,143,491 msg/s |
| Dekaf | 2026-07-29T18:43:11.6686472+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 352,415 msg/s |
| Dekaf | 2026-07-29T18:43:38.6782685+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1610.9 MB/s | 0/0 | 35,413 | 27.0s / 1,439,058 msg/s |
| Dekaf | 2026-07-29T18:44:05.6903242+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1632.0 MB/s | 1/0 | 84,330 | 54.0s / 1,439,915 msg/s |
| Dekaf | 2026-07-29T18:44:32.6984475+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1632.0 MB/s | 1/0 | 142,386 | 81.0s / 1,425,048 msg/s |
| Dekaf | 2026-07-29T18:45:00.7044261+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1632.0 MB/s | 1/1 | 199,257 | 109.0s / 1,395,544 msg/s |
| Dekaf | 2026-07-29T18:45:27.7172589+00:00 | 1 | 14.0 MiB / 12.4 MiB | 1632.0 MB/s | 1/1 | 255,676 | 136.0s / 1,439,662 msg/s |
| Dekaf | 2026-07-29T18:45:54.7246556+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1632.0 MB/s | 1/1 | 306,932 | 163.1s / 1,368,878 msg/s |
| Dekaf | 2026-07-29T18:46:22.7293451+00:00 | 1 | 15.0 MiB / 14.3 MiB | 1632.0 MB/s | 2/1 | 356,067 | 191.1s / 1,425,042 msg/s |
| Dekaf | 2026-07-29T18:46:49.7443446+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1632.0 MB/s | 2/2 | 400,625 | 218.1s / 1,467,207 msg/s |
| Dekaf | 2026-07-29T18:47:16.7591049+00:00 | 1 | 15.0 MiB / 13.1 MiB | 1632.0 MB/s | 2/2 | 448,000 | 245.1s / 1,385,518 msg/s |
| Dekaf | 2026-07-29T18:47:43.7695066+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1632.0 MB/s | 2/2 | 492,168 | 272.1s / 1,386,267 msg/s |
| Dekaf | 2026-07-29T18:48:11.785797+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1632.0 MB/s | 3/2 | 547,595 | 300.1s / 1,441,183 msg/s |
| Dekaf | 2026-07-29T18:48:38.7964655+00:00 | 1 | 11.0 MiB / 9.6 MiB | 1632.0 MB/s | 3/2 | 605,268 | 327.1s / 1,402,186 msg/s |
| Dekaf | 2026-07-29T18:49:05.7992792+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1632.0 MB/s | 4/2 | 666,379 | 354.1s / 1,405,726 msg/s |
| Dekaf | 2026-07-29T18:49:32.8081395+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1632.0 MB/s | 4/3 | 712,980 | 381.1s / 1,417,082 msg/s |
| Dekaf | 2026-07-29T18:50:00.8153424+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1632.0 MB/s | 4/3 | 776,959 | 409.1s / 1,315,401 msg/s |
| Dekaf | 2026-07-29T18:50:27.8218294+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1632.0 MB/s | 4/3 | 836,953 | 436.1s / 1,434,350 msg/s |
| Dekaf | 2026-07-29T18:50:54.8285404+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1632.0 MB/s | 5/3 | 898,217 | 463.1s / 1,439,164 msg/s |
| Dekaf | 2026-07-29T18:51:21.8348382+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1632.0 MB/s | 5/3 | 960,430 | 490.1s / 1,489,397 msg/s |
| Dekaf | 2026-07-29T18:51:49.8443432+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1632.0 MB/s | 5/4 | 1,022,742 | 518.2s / 1,385,541 msg/s |
| Dekaf | 2026-07-29T18:52:16.8545245+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1632.0 MB/s | 5/4 | 1,084,827 | 545.2s / 1,422,945 msg/s |
| Dekaf | 2026-07-29T18:52:43.8659451+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1632.0 MB/s | 5/5 | 1,137,128 | 572.2s / 1,405,341 msg/s |
| Dekaf | 2026-07-29T18:53:10.8734053+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1632.0 MB/s | 5/5 | 1,197,052 | 599.2s / 1,394,576 msg/s |
| Dekaf | 2026-07-29T18:53:38.8789154+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1632.0 MB/s | 5/5 | 1,261,094 | 627.2s / 1,189,737 msg/s |
| Dekaf | 2026-07-29T18:54:05.8887501+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1632.0 MB/s | 5/5 | 1,318,502 | 654.2s / 1,322,856 msg/s |
| Dekaf | 2026-07-29T18:54:32.899827+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1632.0 MB/s | 5/5 | 1,381,554 | 681.2s / 1,256,739 msg/s |
| Dekaf | 2026-07-29T18:54:59.9053206+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1632.0 MB/s | 6/5 | 1,448,767 | 708.2s / 1,350,327 msg/s |
| Dekaf | 2026-07-29T18:55:27.9219236+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1632.0 MB/s | 6/5 | 1,515,056 | 736.2s / 1,375,127 msg/s |
| Dekaf | 2026-07-29T18:55:54.9372357+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1632.0 MB/s | 6/6 | 1,577,512 | 763.2s / 1,312,404 msg/s |
| Dekaf | 2026-07-29T18:56:21.9578607+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1632.0 MB/s | 6/6 | 1,639,727 | 790.2s / 1,344,322 msg/s |
| Dekaf | 2026-07-29T18:56:49.9664001+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1632.0 MB/s | 6/6 | 1,707,432 | 818.2s / 1,336,303 msg/s |
| Dekaf | 2026-07-29T18:57:16.9765996+00:00 | 1 | 11.0 MiB / 10.3 MiB | 1632.0 MB/s | 7/6 | 1,777,232 | 845.2s / 1,311,150 msg/s |
| Dekaf | 2026-07-29T18:57:43.9813493+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1632.0 MB/s | 7/7 | 1,832,748 | 872.2s / 1,389,547 msg/s |
| Dekaf | 2026-07-29T18:58:10.9904744+00:00 | 1 | 11.0 MiB / 10.4 MiB | 1632.0 MB/s | 7/7 | 1,905,181 | 899.2s / 1,370,795 msg/s |
| Dekaf (3conn) | 2026-07-29T18:58:39.652211+00:00 | 1 | 16.0 MiB / 4.2 MiB | 1373.4 MB/s | 0/0 | 620 | 27.0s / 1,207,752 msg/s |
| Dekaf (3conn) | 2026-07-29T18:59:06.659353+00:00 | 1 | 14.0 MiB / 10.2 MiB | 1373.4 MB/s | 1/0 | 1,370 | 54.0s / 1,193,106 msg/s |
| Dekaf (3conn) | 2026-07-29T18:59:33.6693026+00:00 | 1 | 14.0 MiB / 1.9 MiB | 1373.4 MB/s | 1/0 | 2,409 | 81.0s / 1,206,242 msg/s |
| Dekaf (3conn) | 2026-07-29T19:00:00.6714984+00:00 | 1 | 12.0 MiB / 5.8 MiB | 1373.4 MB/s | 2/0 | 3,809 | 108.0s / 1,190,035 msg/s |
| Dekaf (3conn) | 2026-07-29T19:00:28.6814487+00:00 | 1 | 10.0 MiB / 4.6 MiB | 1373.4 MB/s | 3/0 | 5,857 | 136.0s / 1,169,109 msg/s |
| Dekaf (3conn) | 2026-07-29T19:00:55.6926726+00:00 | 1 | 10.0 MiB / 3.6 MiB | 1387.9 MB/s | 3/0 | 8,772 | 163.1s / 991,976 msg/s |
| Dekaf (3conn) | 2026-07-29T19:01:22.711152+00:00 | 1 | 10.0 MiB / 2.0 MiB | 1387.9 MB/s | 3/1 | 11,394 | 190.1s / 1,087,055 msg/s |
| Dekaf (3conn) | 2026-07-29T19:01:49.7193181+00:00 | 1 | 10.0 MiB / 1.9 MiB | 1387.9 MB/s | 3/1 | 13,863 | 217.1s / 1,139,684 msg/s |
| Dekaf (3conn) | 2026-07-29T19:02:17.7283387+00:00 | 1 | 10.0 MiB / 2.8 MiB | 1387.9 MB/s | 3/1 | 16,975 | 245.1s / 1,166,554 msg/s |
| Dekaf (3conn) | 2026-07-29T19:02:44.7391954+00:00 | 1 | 11.0 MiB / 1.4 MiB | 1387.9 MB/s | 4/1 | 19,305 | 272.1s / 1,255,561 msg/s |
| Dekaf (3conn) | 2026-07-29T19:03:11.7503418+00:00 | 1 | 12.0 MiB / 7.5 MiB | 1387.9 MB/s | 4/1 | 21,285 | 299.1s / 1,262,911 msg/s |
| Dekaf (3conn) | 2026-07-29T19:03:38.7630089+00:00 | 1 | 12.0 MiB / 3.6 MiB | 1433.4 MB/s | 5/1 | 22,893 | 326.1s / 1,170,856 msg/s |
| Dekaf (3conn) | 2026-07-29T19:04:06.7706015+00:00 | 1 | 12.0 MiB / 3.0 MiB | 1433.4 MB/s | 5/2 | 24,635 | 354.1s / 1,204,261 msg/s |
| Dekaf (3conn) | 2026-07-29T19:04:33.7776922+00:00 | 1 | 12.0 MiB / 3.7 MiB | 1433.4 MB/s | 5/2 | 26,509 | 381.1s / 1,182,398 msg/s |
| Dekaf (3conn) | 2026-07-29T19:05:00.7881298+00:00 | 1 | 10.0 MiB / 2.0 MiB | 1433.4 MB/s | 5/2 | 28,302 | 408.1s / 1,174,310 msg/s |
| Dekaf (3conn) | 2026-07-29T19:05:28.7924131+00:00 | 1 | 12.0 MiB / 4.6 MiB | 1433.4 MB/s | 5/3 | 30,014 | 436.2s / 1,088,037 msg/s |
| Dekaf (3conn) | 2026-07-29T19:05:55.8034992+00:00 | 1 | 12.0 MiB / 1.0 MiB | 1433.4 MB/s | 5/3 | 31,447 | 463.2s / 1,112,311 msg/s |
| Dekaf (3conn) | 2026-07-29T19:06:22.8113398+00:00 | 1 | 12.0 MiB / 5.1 MiB | 1433.4 MB/s | 5/3 | 33,203 | 490.2s / 1,247,680 msg/s |
| Dekaf (3conn) | 2026-07-29T19:06:49.8174909+00:00 | 1 | 12.0 MiB / 6.1 MiB | 1433.4 MB/s | 5/3 | 35,231 | 517.2s / 1,247,347 msg/s |
| Dekaf (3conn) | 2026-07-29T19:07:17.8225464+00:00 | 1 | 12.0 MiB / 1.6 MiB | 1493.0 MB/s | 5/3 | 37,012 | 545.2s / 1,294,516 msg/s |
| Dekaf (3conn) | 2026-07-29T19:07:44.8272507+00:00 | 1 | 13.0 MiB / 5.1 MiB | 1493.0 MB/s | 6/3 | 38,420 | 572.2s / 1,200,000 msg/s |
| Dekaf (3conn) | 2026-07-29T19:08:11.8345153+00:00 | 1 | 14.0 MiB / 4.8 MiB | 1493.0 MB/s | 6/3 | 39,667 | 599.2s / 1,259,687 msg/s |
| Dekaf (3conn) | 2026-07-29T19:08:38.840263+00:00 | 1 | 13.0 MiB / 7.1 MiB | 1493.0 MB/s | 6/4 | 40,968 | 626.2s / 1,235,861 msg/s |
| Dekaf (3conn) | 2026-07-29T19:09:06.8534936+00:00 | 1 | 13.0 MiB / 2.0 MiB | 1493.0 MB/s | 6/4 | 41,840 | 654.2s / 1,077,079 msg/s |
| Dekaf (3conn) | 2026-07-29T19:09:33.8605722+00:00 | 1 | 13.0 MiB / 4.1 MiB | 1493.0 MB/s | 6/5 | 43,199 | 681.3s / 1,237,507 msg/s |
| Dekaf (3conn) | 2026-07-29T19:10:00.8725323+00:00 | 1 | 13.0 MiB / 2.3 MiB | 1493.0 MB/s | 6/5 | 44,210 | 708.3s / 1,158,545 msg/s |
| Dekaf (3conn) | 2026-07-29T19:10:27.8856514+00:00 | 1 | 13.0 MiB / 5.6 MiB | 1493.0 MB/s | 6/5 | 45,359 | 735.3s / 1,102,442 msg/s |
| Dekaf (3conn) | 2026-07-29T19:10:55.8953451+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1493.0 MB/s | 6/5 | 46,598 | 763.3s / 1,130,141 msg/s |
| Dekaf (3conn) | 2026-07-29T19:11:22.9034273+00:00 | 1 | 13.0 MiB / 5.7 MiB | 1493.0 MB/s | 6/5 | 47,587 | 790.3s / 1,152,035 msg/s |
| Dekaf (3conn) | 2026-07-29T19:11:49.9133278+00:00 | 1 | 14.0 MiB / 12.2 MiB | 1493.0 MB/s | 7/5 | 48,515 | 817.3s / 1,083,151 msg/s |
| Dekaf (3conn) | 2026-07-29T19:12:16.9211069+00:00 | 1 | 15.0 MiB / 1.7 MiB | 1493.0 MB/s | 7/5 | 49,298 | 844.3s / 1,109,782 msg/s |
| Dekaf (3conn) | 2026-07-29T19:12:44.9273502+00:00 | 1 | 15.0 MiB / 1.4 MiB | 1493.0 MB/s | 8/5 | 49,981 | 872.3s / 1,203,811 msg/s |
| Dekaf (3conn) | 2026-07-29T19:13:11.9397035+00:00 | 1 | 16.0 MiB / 2.7 MiB | 1493.0 MB/s | 8/5 | 50,775 | 899.3s / 875,065 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T17:58:40.4110753+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T17:58:55.4269391+00:00 | 1 | capacity | succeeded | 15,016ms | 14.0 MiB / 12.5 MiB |
| Dekaf | 2026-07-29T17:59:25.4546051+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T17:59:40.466319+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:00:10.4894592+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:00:25.5006587+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.5 MiB |
| Dekaf | 2026-07-29T18:01:25.5633212+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:01:40.5805335+00:00 | 1 | capacity | succeeded | 15,017ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:02:10.6082112+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:02:25.6165337+00:00 | 1 | capacity | succeeded | 15,008ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:02:55.6411508+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:03:10.6550079+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-29T18:04:10.7118711+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:04:25.7219867+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:04:55.7539366+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:05:10.7687892+00:00 | 1 | capacity | failed | 15,015ms | 12.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-29T18:06:10.8178164+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:06:25.8317867+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:08:25.9354051+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:08:40.9475721+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-29T18:12:41.2023669+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-29T18:12:56.2193348+00:00 | 1 | capacity | succeeded | 15,017ms | 13.0 MiB / 7.8 MiB |
| Dekaf | 2026-07-29T18:43:41.8070903+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-29T18:43:56.8230218+00:00 | 1 | capacity | succeeded | 15,016ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-29T18:44:26.8503335+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:44:41.8605441+00:00 | 1 | capacity | failed | 15,009ms | 14.0 MiB / 11.2 MiB |
| Dekaf | 2026-07-29T18:45:41.9190391+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:45:56.9311967+00:00 | 1 | capacity | succeeded | 15,012ms | 15.0 MiB / 13.9 MiB |
| Dekaf | 2026-07-29T18:46:26.9768493+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.7 MiB |
| Dekaf | 2026-07-29T18:46:41.9895465+00:00 | 1 | capacity | failed | 15,012ms | 15.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T18:47:42.0800111+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T18:47:57.0931371+00:00 | 1 | capacity | succeeded | 15,013ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:48:27.1196236+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-29T18:48:42.1301829+00:00 | 1 | capacity | succeeded | 15,010ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:49:12.150738+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:49:27.1638361+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T18:50:27.2098739+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-29T18:50:42.2220392+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:51:12.2446052+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-29T18:51:27.2540703+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:52:27.302075+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:52:42.3132768+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T18:54:42.4205629+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:54:57.4342125+00:00 | 1 | capacity | succeeded | 15,013ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:55:27.4666518+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:55:42.4795842+00:00 | 1 | capacity | failed | 15,012ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:56:42.557465+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.5 MiB |
| Dekaf | 2026-07-29T18:56:57.5717773+00:00 | 1 | capacity | succeeded | 15,014ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:57:27.5972622+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-29T18:57:42.6124513+00:00 | 1 | capacity | failed | 15,015ms | 11.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:58:42.7549144+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-07-29T18:58:57.7829397+00:00 | 1 | capacity | succeeded | 15,028ms | 14.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:59:27.8335933+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:59:42.8563896+00:00 | 1 | capacity | succeeded | 15,022ms | 12.0 MiB / 8.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:00:12.9104418+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:00:27.9360268+00:00 | 1 | capacity | succeeded | 15,025ms | 10.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-29T19:00:57.9939436+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-29T19:01:13.0174431+00:00 | 1 | capacity | failed | 15,023ms | 10.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:02:13.1219939+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-29T19:02:28.1402882+00:00 | 1 | capacity | succeeded | 15,018ms | 11.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-29T19:02:58.1843576+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 0.7 MiB |
| Dekaf (3conn) | 2026-07-29T19:03:13.2097009+00:00 | 1 | capacity | succeeded | 15,025ms | 12.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:03:43.266118+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:03:58.284005+00:00 | 1 | capacity | failed | 15,017ms | 12.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-29T19:04:58.3819265+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 8.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:05:13.4042397+00:00 | 1 | capacity | failed | 15,022ms | 12.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:07:13.5981317+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-29T19:07:28.6203221+00:00 | 1 | capacity | succeeded | 15,022ms | 13.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-07-29T19:07:58.6689846+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:08:13.6936407+00:00 | 1 | capacity | failed | 15,024ms | 13.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:09:13.7823662+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:09:28.8042267+00:00 | 1 | capacity | failed | 15,021ms | 13.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:11:29.0001494+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:11:44.0306439+00:00 | 1 | capacity | succeeded | 15,030ms | 14.0 MiB / 9.7 MiB |
| Dekaf (3conn) | 2026-07-29T19:12:14.0878094+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:12:29.1112772+00:00 | 1 | capacity | succeeded | 15,023ms | 15.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-29T19:12:59.1563479+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 1.3 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,670 |
| Dekaf | 1 | 0.002–0.004ms | 1,908 |
| Dekaf | 1 | 0.004–0.008ms | 5,258 |
| Dekaf | 1 | 0.008–0.016ms | 27,567 |
| Dekaf | 1 | 0.016–0.032ms | 39,049 |
| Dekaf | 1 | 0.032–0.064ms | 42,609 |
| Dekaf | 1 | 0.064–0.128ms | 85,043 |
| Dekaf | 1 | 0.128–0.256ms | 224,583 |
| Dekaf | 1 | 0.256–0.512ms | 222,752 |
| Dekaf | 1 | 0.512–1.024ms | 47,835 |
| Dekaf | 1 | 1.024–2.048ms | 9,764 |
| Dekaf | 1 | 2.048–4.096ms | 3,346 |
| Dekaf | 1 | 4.096–8.192ms | 453 |
| Dekaf | 1 | 8.192–16.384ms | 16 |
| Dekaf | 1 | 16.384–32.768ms | 3 |
| Dekaf | 1 | 32.768–65.536ms | 3 |
| Dekaf | 1 | 0.001–0.002ms | 1,429 |
| Dekaf | 1 | 0.002–0.004ms | 1,590 |
| Dekaf | 1 | 0.004–0.008ms | 4,276 |
| Dekaf | 1 | 0.008–0.016ms | 37,757 |
| Dekaf | 1 | 0.016–0.032ms | 46,499 |
| Dekaf | 1 | 0.032–0.064ms | 39,623 |
| Dekaf | 1 | 0.064–0.128ms | 77,108 |
| Dekaf | 1 | 0.128–0.256ms | 231,308 |
| Dekaf | 1 | 0.256–0.512ms | 283,383 |
| Dekaf | 1 | 0.512–1.024ms | 76,529 |
| Dekaf | 1 | 1.024–2.048ms | 15,661 |
| Dekaf | 1 | 2.048–4.096ms | 3,771 |
| Dekaf | 1 | 4.096–8.192ms | 621 |
| Dekaf | 1 | 8.192–16.384ms | 19 |
| Dekaf | 1 | 16.384–32.768ms | 3 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 16 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 10 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 34 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 109 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 326 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 1,684 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,705 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 2,173 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 2,914 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 3,260 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,953 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 384 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 33 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 1 |

:::tip
**Dekaf uses 1.47x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.23x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.35 | 1220.09 | 959,436 | 955,662 | +0.9% | -0.10% | 914.99 | 959,436 | 0 | 1.30 |
| Dekaf (3conn) | 1.47 | 1248.43 | 891,167 | 874,425 | +13.9% | +1.06% | 849.88 | 891,167 | 0 | 1.31 |
| Confluent | 2.37 | - | 652,660 | 612,589 | -3.4% | -0.34% | 622.42 | 652,660 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 314,230 | 349.14 | 895.71 KB |
| Dekaf | 2 | 317,407 | 352.67 | 891.35 KB |
| Dekaf | 3 | 323,659 | 359.61 | 908.35 KB |
| Dekaf (3conn) | 1 | 321,731 | 357.47 | 852.00 KB |
| Dekaf (3conn) | 2 | 311,389 | 345.98 | 840.61 KB |
| Dekaf (3conn) | 3 | 309,587 | 343.98 | 844.47 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:08.6092143+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 383,406 msg/s |
| Dekaf | 2026-07-29T17:58:26.623164+00:00 | 3 | 16.0 MiB / 6.6 MiB | 359.0 MB/s | 0/0 | 2,413 | 18.0s / 888,060 msg/s |
| Dekaf | 2026-07-29T17:58:45.6323308+00:00 | 1 | 16.0 MiB / 1.4 MiB | 417.5 MB/s | 0/0 | 3,746 | 37.0s / 975,136 msg/s |
| Dekaf | 2026-07-29T17:59:03.6427532+00:00 | 1 | 14.0 MiB / 1.5 MiB | 417.5 MB/s | 1/0 | 4,608 | 55.1s / 993,136 msg/s |
| Dekaf | 2026-07-29T17:59:21.65218+00:00 | 2 | 14.0 MiB / 3.3 MiB | 407.0 MB/s | 1/1 | 4,799 | 73.1s / 966,847 msg/s |
| Dekaf | 2026-07-29T17:59:39.6607182+00:00 | 2 | 14.0 MiB / 3.4 MiB | 407.0 MB/s | 1/1 | 5,843 | 91.1s / 906,276 msg/s |
| Dekaf | 2026-07-29T17:59:57.6875302+00:00 | 3 | 16.0 MiB / 0.9 MiB | 429.0 MB/s | 0/2 | 5,709 | 109.1s / 858,181 msg/s |
| Dekaf | 2026-07-29T18:00:15.709124+00:00 | 3 | 16.0 MiB / 6.5 MiB | 436.2 MB/s | 0/2 | 5,965 | 127.1s / 1,059,122 msg/s |
| Dekaf | 2026-07-29T18:00:34.728182+00:00 | 1 | 14.0 MiB / 2.7 MiB | 417.5 MB/s | 1/2 | 7,630 | 146.1s / 955,681 msg/s |
| Dekaf | 2026-07-29T18:00:52.7406995+00:00 | 1 | 14.0 MiB / 3.0 MiB | 417.5 MB/s | 1/2 | 8,227 | 164.1s / 978,225 msg/s |
| Dekaf | 2026-07-29T18:01:10.7523547+00:00 | 2 | 16.0 MiB / 1.6 MiB | 407.0 MB/s | 2/1 | 9,447 | 182.2s / 941,676 msg/s |
| Dekaf | 2026-07-29T18:01:28.7633074+00:00 | 2 | 15.0 MiB / 2.6 MiB | 407.0 MB/s | 2/2 | 9,931 | 200.2s / 901,656 msg/s |
| Dekaf | 2026-07-29T18:01:46.7802973+00:00 | 3 | 16.0 MiB / 2.7 MiB | 436.2 MB/s | 0/3 | 7,148 | 218.2s / 969,612 msg/s |
| Dekaf | 2026-07-29T18:02:04.7917765+00:00 | 3 | 16.0 MiB / 2.7 MiB | 470.1 MB/s | 0/3 | 7,502 | 236.2s / 980,217 msg/s |
| Dekaf | 2026-07-29T18:02:23.8078232+00:00 | 1 | 14.0 MiB / 1.6 MiB | 417.5 MB/s | 1/2 | 11,252 | 255.2s / 863,183 msg/s |
| Dekaf | 2026-07-29T18:02:41.8223102+00:00 | 1 | 12.0 MiB / 4.6 MiB | 417.5 MB/s | 1/2 | 11,686 | 273.2s / 1,025,463 msg/s |
| Dekaf | 2026-07-29T18:02:59.8267481+00:00 | 2 | 11.0 MiB / 7.3 MiB | 473.6 MB/s | 3/2 | 13,231 | 291.2s / 970,108 msg/s |
| Dekaf | 2026-07-29T18:03:17.8282437+00:00 | 2 | 9.0 MiB / 9.0 MiB | 487.2 MB/s | 4/2 | 15,465 | 309.3s / 1,140,360 msg/s |
| Dekaf | 2026-07-29T18:03:35.8386635+00:00 | 3 | 16.0 MiB / 3.1 MiB | 477.1 MB/s | 0/3 | 9,935 | 327.3s / 811,310 msg/s |
| Dekaf | 2026-07-29T18:03:53.844373+00:00 | 3 | 16.0 MiB / 10.1 MiB | 477.1 MB/s | 0/3 | 9,955 | 345.3s / 954,184 msg/s |
| Dekaf | 2026-07-29T18:04:12.857076+00:00 | 1 | 12.0 MiB / 3.8 MiB | 488.7 MB/s | 2/3 | 14,530 | 364.3s / 975,199 msg/s |
| Dekaf | 2026-07-29T18:04:30.8759393+00:00 | 1 | 10.0 MiB / 10.0 MiB | 488.7 MB/s | 2/3 | 15,355 | 382.3s / 960,800 msg/s |
| Dekaf | 2026-07-29T18:04:48.8993121+00:00 | 2 | 10.0 MiB / 0.8 MiB | 487.2 MB/s | 5/3 | 22,194 | 400.3s / 923,685 msg/s |
| Dekaf | 2026-07-29T18:05:06.9076209+00:00 | 2 | 10.0 MiB / 10.0 MiB | 487.2 MB/s | 6/3 | 23,552 | 418.3s / 893,470 msg/s |
| Dekaf | 2026-07-29T18:05:24.912487+00:00 | 3 | 16.0 MiB / 1.9 MiB | 477.1 MB/s | 0/5 | 12,558 | 436.3s / 852,149 msg/s |
| Dekaf | 2026-07-29T18:05:42.9242641+00:00 | 3 | 18.0 MiB / 4.2 MiB | 477.1 MB/s | 0/5 | 12,646 | 454.3s / 976,658 msg/s |
| Dekaf | 2026-07-29T18:06:01.9380705+00:00 | 1 | 8.0 MiB / 2.8 MiB | 488.7 MB/s | 4/4 | 24,531 | 473.4s / 897,402 msg/s |
| Dekaf | 2026-07-29T18:06:19.9488704+00:00 | 1 | 8.0 MiB / 3.6 MiB | 488.7 MB/s | 4/4 | 26,143 | 491.4s / 1,098,775 msg/s |
| Dekaf | 2026-07-29T18:06:37.9598308+00:00 | 2 | 10.0 MiB / 8.2 MiB | 487.2 MB/s | 6/4 | 35,256 | 509.4s / 943,985 msg/s |
| Dekaf | 2026-07-29T18:06:55.9748214+00:00 | 2 | 8.0 MiB / 2.2 MiB | 487.2 MB/s | 6/4 | 37,467 | 527.4s / 1,018,926 msg/s |
| Dekaf | 2026-07-29T18:07:13.9850241+00:00 | 3 | 15.0 MiB / 14.8 MiB | 477.1 MB/s | 1/6 | 17,098 | 545.4s / 1,067,945 msg/s |
| Dekaf | 2026-07-29T18:07:32.0072007+00:00 | 3 | 13.0 MiB / 2.4 MiB | 477.1 MB/s | 2/6 | 17,486 | 563.4s / 940,826 msg/s |
| Dekaf | 2026-07-29T18:07:51.030205+00:00 | 1 | 10.0 MiB / 5.5 MiB | 488.7 MB/s | 6/4 | 35,035 | 582.5s / 1,000,617 msg/s |
| Dekaf | 2026-07-29T18:08:09.0532735+00:00 | 1 | 10.0 MiB / 4.2 MiB | 488.7 MB/s | 6/4 | 36,159 | 600.5s / 926,898 msg/s |
| Dekaf | 2026-07-29T18:08:27.067804+00:00 | 2 | 10.0 MiB / 3.5 MiB | 487.2 MB/s | 6/5 | 43,879 | 618.5s / 865,768 msg/s |
| Dekaf | 2026-07-29T18:08:45.0740175+00:00 | 2 | 10.0 MiB / 3.5 MiB | 487.2 MB/s | 6/5 | 45,632 | 636.5s / 1,031,129 msg/s |
| Dekaf | 2026-07-29T18:09:03.0960266+00:00 | 3 | 15.0 MiB / 5.7 MiB | 477.1 MB/s | 2/8 | 19,348 | 654.5s / 916,510 msg/s |
| Dekaf | 2026-07-29T18:09:21.1085897+00:00 | 3 | 15.0 MiB / 10.2 MiB | 477.1 MB/s | 2/9 | 20,548 | 672.5s / 1,018,053 msg/s |
| Dekaf | 2026-07-29T18:09:40.1311137+00:00 | 1 | 12.0 MiB / 5.1 MiB | 488.7 MB/s | 8/4 | 41,214 | 691.5s / 1,032,298 msg/s |
| Dekaf | 2026-07-29T18:09:58.1482069+00:00 | 1 | 10.0 MiB / 3.0 MiB | 488.7 MB/s | 9/4 | 42,476 | 709.6s / 1,012,366 msg/s |
| Dekaf | 2026-07-29T18:10:16.1630034+00:00 | 2 | 10.0 MiB / 2.7 MiB | 487.2 MB/s | 6/6 | 60,497 | 727.6s / 1,063,134 msg/s |
| Dekaf | 2026-07-29T18:10:34.1643227+00:00 | 2 | 10.0 MiB / 0.4 MiB | 487.2 MB/s | 6/6 | 62,221 | 745.6s / 979,907 msg/s |
| Dekaf | 2026-07-29T18:10:52.1852809+00:00 | 3 | 15.0 MiB / 1.8 MiB | 495.8 MB/s | 2/9 | 24,507 | 763.6s / 901,564 msg/s |
| Dekaf | 2026-07-29T18:11:10.1990269+00:00 | 3 | 15.0 MiB / 2.6 MiB | 495.8 MB/s | 2/9 | 24,527 | 781.6s / 986,885 msg/s |
| Dekaf | 2026-07-29T18:11:29.2301549+00:00 | 1 | 10.0 MiB / 3.0 MiB | 488.7 MB/s | 9/5 | 49,222 | 800.7s / 1,055,532 msg/s |
| Dekaf | 2026-07-29T18:11:47.2421336+00:00 | 1 | 11.0 MiB / 2.7 MiB | 488.7 MB/s | 9/5 | 50,119 | 818.7s / 849,210 msg/s |
| Dekaf | 2026-07-29T18:12:05.2498895+00:00 | 2 | 10.0 MiB / 1.4 MiB | 487.2 MB/s | 6/6 | 69,189 | 836.7s / 789,713 msg/s |
| Dekaf | 2026-07-29T18:12:23.2611438+00:00 | 2 | 10.0 MiB / 6.5 MiB | 487.2 MB/s | 6/6 | 69,861 | 854.7s / 793,815 msg/s |
| Dekaf | 2026-07-29T18:12:41.2708684+00:00 | 3 | 15.0 MiB / 1.8 MiB | 495.8 MB/s | 2/9 | 25,376 | 872.7s / 728,730 msg/s |
| Dekaf | 2026-07-29T18:12:59.2784356+00:00 | 3 | 15.0 MiB / 1.4 MiB | 495.8 MB/s | 2/9 | 25,880 | 890.7s / 875,405 msg/s |
| Dekaf (3conn) | 2026-07-29T18:28:32.0685131+00:00 | 3 | 16.0 MiB / 2.7 MiB | 371.6 MB/s | 0/0 | 401 | 9.0s / 876,360 msg/s |
| Dekaf (3conn) | 2026-07-29T18:28:50.0794484+00:00 | 3 | 16.0 MiB / 1.5 MiB | 371.6 MB/s | 0/0 | 973 | 27.0s / 857,703 msg/s |
| Dekaf (3conn) | 2026-07-29T18:29:09.0987562+00:00 | 1 | 14.0 MiB / 1.1 MiB | 370.0 MB/s | 1/0 | 2,156 | 46.1s / 851,977 msg/s |
| Dekaf (3conn) | 2026-07-29T18:29:27.1145873+00:00 | 1 | 14.0 MiB / 2.2 MiB | 370.0 MB/s | 1/1 | 2,366 | 64.1s / 702,696 msg/s |
| Dekaf (3conn) | 2026-07-29T18:29:45.1303523+00:00 | 2 | 14.0 MiB / 3.0 MiB | 348.2 MB/s | 1/0 | 1,521 | 82.1s / 648,741 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:03.1544017+00:00 | 2 | 12.0 MiB / 6.8 MiB | 348.2 MB/s | 2/0 | 1,930 | 100.1s / 820,572 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:21.1615565+00:00 | 3 | 14.0 MiB / 10.6 MiB | 371.6 MB/s | 1/1 | 1,677 | 118.1s / 771,793 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:39.1734962+00:00 | 3 | 12.0 MiB / 1.5 MiB | 479.1 MB/s | 1/1 | 1,956 | 136.1s / 776,885 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:58.1883021+00:00 | 1 | 12.0 MiB / 3.9 MiB | 485.4 MB/s | 2/1 | 3,730 | 155.1s / 987,577 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:16.2018831+00:00 | 1 | 10.0 MiB / 8.4 MiB | 485.4 MB/s | 2/1 | 4,326 | 173.2s / 903,870 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:34.217772+00:00 | 2 | 12.0 MiB / 4.0 MiB | 471.1 MB/s | 2/1 | 3,106 | 191.2s / 910,451 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:52.2322398+00:00 | 2 | 10.0 MiB / 2.8 MiB | 530.5 MB/s | 2/1 | 4,240 | 209.2s / 1,422,170 msg/s |
| Dekaf (3conn) | 2026-07-29T18:32:10.2430745+00:00 | 3 | 12.0 MiB / 1.1 MiB | 537.2 MB/s | 2/2 | 5,643 | 227.2s / 1,476,969 msg/s |
| Dekaf (3conn) | 2026-07-29T18:32:28.2653854+00:00 | 3 | 13.0 MiB / 1.4 MiB | 537.2 MB/s | 2/2 | 6,122 | 245.2s / 812,738 msg/s |
| Dekaf (3conn) | 2026-07-29T18:32:47.2787545+00:00 | 1 | 12.0 MiB / 1.4 MiB | 559.6 MB/s | 2/3 | 8,761 | 264.2s / 981,906 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:05.2946711+00:00 | 1 | 12.0 MiB / 0.2 MiB | 591.1 MB/s | 2/3 | 9,067 | 282.2s / 847,677 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:23.304322+00:00 | 2 | 12.0 MiB / 4.2 MiB | 549.0 MB/s | 2/2 | 6,113 | 300.2s / 1,002,744 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:41.3144874+00:00 | 2 | 12.0 MiB / 1.8 MiB | 549.0 MB/s | 2/2 | 6,256 | 318.3s / 925,388 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:59.3309574+00:00 | 3 | 8.0 MiB / 3.3 MiB | 543.7 MB/s | 3/3 | 9,231 | 336.3s / 822,469 msg/s |
| Dekaf (3conn) | 2026-07-29T18:34:17.3401989+00:00 | 3 | 10.0 MiB / 1.5 MiB | 543.7 MB/s | 3/4 | 10,033 | 354.3s / 748,940 msg/s |
| Dekaf (3conn) | 2026-07-29T18:34:36.360133+00:00 | 1 | 10.0 MiB / 1.2 MiB | 591.1 MB/s | 3/4 | 12,689 | 373.3s / 802,623 msg/s |
| Dekaf (3conn) | 2026-07-29T18:34:54.3686912+00:00 | 1 | 10.0 MiB / 0.6 MiB | 591.1 MB/s | 3/4 | 13,010 | 391.3s / 732,658 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:12.3800526+00:00 | 2 | 13.0 MiB / 2.2 MiB | 549.0 MB/s | 3/3 | 6,464 | 409.4s / 716,067 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:30.3941908+00:00 | 2 | 13.0 MiB / 0.7 MiB | 549.0 MB/s | 3/3 | 6,519 | 427.4s / 903,095 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:48.4021468+00:00 | 3 | 10.0 MiB / 1.0 MiB | 543.7 MB/s | 3/5 | 11,737 | 445.4s / 883,676 msg/s |
| Dekaf (3conn) | 2026-07-29T18:36:06.4171156+00:00 | 3 | 10.0 MiB / 1.9 MiB | 543.7 MB/s | 3/5 | 12,456 | 463.4s / 864,843 msg/s |
| Dekaf (3conn) | 2026-07-29T18:36:25.4273484+00:00 | 1 | 10.0 MiB / 0.2 MiB | 591.1 MB/s | 3/5 | 15,535 | 482.4s / 806,061 msg/s |
| Dekaf (3conn) | 2026-07-29T18:36:43.4388969+00:00 | 1 | 10.0 MiB / 1.9 MiB | 591.1 MB/s | 3/5 | 17,927 | 500.4s / 1,273,824 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:01.4436154+00:00 | 2 | 13.0 MiB / 0.4 MiB | 549.0 MB/s | 3/4 | 7,385 | 518.4s / 765,558 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:19.4475989+00:00 | 2 | 13.0 MiB / 0.8 MiB | 549.0 MB/s | 3/4 | 7,533 | 536.5s / 822,686 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:37.4602343+00:00 | 3 | 8.0 MiB / 1.5 MiB | 543.7 MB/s | 4/5 | 16,277 | 554.5s / 713,575 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:55.4726612+00:00 | 3 | 9.0 MiB / 2.7 MiB | 543.7 MB/s | 4/5 | 16,911 | 572.5s / 867,273 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:14.4825443+00:00 | 1 | 10.0 MiB / 4.4 MiB | 591.1 MB/s | 3/6 | 19,518 | 591.5s / 890,713 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:32.4932636+00:00 | 1 | 10.0 MiB / 1.4 MiB | 591.1 MB/s | 3/6 | 19,652 | 609.5s / 616,627 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:50.503187+00:00 | 2 | 13.0 MiB / 0.6 MiB | 549.0 MB/s | 3/5 | 7,905 | 627.5s / 841,085 msg/s |
| Dekaf (3conn) | 2026-07-29T18:39:08.5130083+00:00 | 2 | 13.0 MiB / 2.7 MiB | 549.0 MB/s | 3/5 | 7,960 | 645.5s / 866,101 msg/s |
| Dekaf (3conn) | 2026-07-29T18:39:26.5251441+00:00 | 3 | 9.0 MiB / 1.8 MiB | 574.3 MB/s | 5/6 | 20,296 | 663.5s / 870,620 msg/s |
| Dekaf (3conn) | 2026-07-29T18:39:44.5384045+00:00 | 3 | 9.0 MiB / 0.4 MiB | 574.3 MB/s | 5/6 | 22,647 | 681.6s / 813,603 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:03.5613182+00:00 | 1 | 10.0 MiB / 3.1 MiB | 615.2 MB/s | 3/6 | 23,432 | 700.6s / 965,463 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:21.5733282+00:00 | 1 | 10.0 MiB / 2.9 MiB | 615.2 MB/s | 3/6 | 23,972 | 718.6s / 1,036,721 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:39.5827118+00:00 | 2 | 13.0 MiB / 1.8 MiB | 587.4 MB/s | 3/5 | 8,951 | 736.6s / 1,114,605 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:57.5949504+00:00 | 2 | 13.0 MiB / 1.2 MiB | 587.4 MB/s | 3/5 | 9,176 | 754.6s / 990,543 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:15.6011471+00:00 | 3 | 8.0 MiB / 1.2 MiB | 574.3 MB/s | 6/7 | 30,422 | 772.7s / 870,173 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:33.6191901+00:00 | 3 | 8.0 MiB / 4.9 MiB | 574.3 MB/s | 6/7 | 32,320 | 790.7s / 757,080 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:52.627161+00:00 | 1 | 10.0 MiB / 10.0 MiB | 638.9 MB/s | 3/6 | 29,250 | 809.7s / 917,391 msg/s |
| Dekaf (3conn) | 2026-07-29T18:42:10.6381255+00:00 | 1 | 11.0 MiB / 7.9 MiB | 638.9 MB/s | 4/6 | 31,005 | 827.7s / 1,332,823 msg/s |
| Dekaf (3conn) | 2026-07-29T18:42:28.6669561+00:00 | 2 | 11.0 MiB / 4.8 MiB | 631.6 MB/s | 3/5 | 10,244 | 845.7s / 830,649 msg/s |
| Dekaf (3conn) | 2026-07-29T18:42:46.6771178+00:00 | 2 | 13.0 MiB / 0.1 MiB | 631.6 MB/s | 3/6 | 10,554 | 863.7s / 947,701 msg/s |
| Dekaf (3conn) | 2026-07-29T18:43:04.6863318+00:00 | 3 | 9.0 MiB / 1.2 MiB | 600.2 MB/s | 7/8 | 39,920 | 881.8s / 916,364 msg/s |
| Dekaf (3conn) | 2026-07-29T18:43:22.6965868+00:00 | 3 | 9.0 MiB / 2.5 MiB | 600.2 MB/s | 7/8 | 41,139 | 899.8s / 837,183 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T17:58:38.8070445+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-29T17:58:38.9239506+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T17:58:53.9093077+00:00 | 2 | capacity | succeeded | 15,102ms | 14.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-29T17:58:53.9949353+00:00 | 3 | capacity | failed | 15,070ms | 16.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-29T17:58:56.9199434+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T17:58:57.1717123+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T17:59:12.2651758+00:00 | 1 | capacity | failed | 15,093ms | 14.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T17:59:24.1478544+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T18:00:08.3541394+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-29T18:00:12.242094+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 1.3 MiB |
| Dekaf | 2026-07-29T18:00:23.4407696+00:00 | 3 | capacity | failed | 15,086ms | 16.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T18:00:26.2277559+00:00 | 1 | capacity | failed | 13,562ms | 14.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T18:00:57.5153067+00:00 | 2 | capacity | started | 0ms | 16.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T18:01:12.5807474+00:00 | 2 | capacity | failed | 15,065ms | 15.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-29T18:02:26.806089+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T18:02:27.9949417+00:00 | 2 | capacity | succeeded | 15,055ms | 13.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-29T18:02:58.1119778+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T18:03:12.0353678+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T18:03:16.186132+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-29T18:03:27.1021093+00:00 | 1 | capacity | failed | 15,066ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T18:04:01.3920583+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T18:04:16.4462745+00:00 | 2 | capacity | failed | 15,053ms | 9.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-29T18:04:26.080571+00:00 | 3 | capacity | failed | 1,507ms | 16.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-29T18:04:27.4249376+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-29T18:04:46.5752398+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T18:04:56.2068309+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-29T18:05:11.2774031+00:00 | 3 | capacity | failed | 15,070ms | 16.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-29T18:05:12.6490445+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T18:05:31.7989056+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T18:05:41.4431395+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-29T18:05:56.5290384+00:00 | 3 | capacity | succeeded | 15,085ms | 18.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T18:05:57.9293207+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T18:06:26.6565769+00:00 | 3 | capacity | started | 0ms | 20.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-29T18:06:31.0706506+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-29T18:06:44.6775165+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T18:06:46.1240949+00:00 | 1 | capacity | succeeded | 15,053ms | 9.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-29T18:07:10.43404+00:00 | 3 | capacity | started | 0ms | 15.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T18:07:16.2916566+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-29T18:07:28.5003446+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T18:07:31.4087861+00:00 | 1 | capacity | succeeded | 15,117ms | 10.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T18:08:01.556272+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T18:08:13.6959071+00:00 | 3 | capacity | started | 0ms | 16.0 MiB / 6.5 MiB |
| Dekaf | 2026-07-29T18:08:28.7752599+00:00 | 3 | capacity | failed | 15,081ms | 15.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-29T18:08:46.7577903+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-29T18:09:00.4081075+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 7.4 MiB |
| Dekaf | 2026-07-29T18:09:01.8476657+00:00 | 1 | capacity | succeeded | 15,089ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:09:15.4828111+00:00 | 2 | capacity | failed | 15,074ms | 10.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-29T18:09:32.0106522+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-29T18:10:17.2478344+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 0.1 MiB |
| Dekaf | 2026-07-29T18:10:32.3281413+00:00 | 1 | capacity | failed | 15,080ms | 10.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-29T18:11:47.69612+00:00 | 1 | capacity | succeeded | 15,072ms | 11.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T18:12:17.8738584+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:13:03.0685189+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:28:53.3289444+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:28:53.3584165+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:08.4134869+00:00 | 2 | capacity | succeeded | 15,070ms | 14.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:08.4403491+00:00 | 3 | capacity | succeeded | 15,081ms | 14.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:11.4473654+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 0.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:26.5094604+00:00 | 1 | capacity | failed | 15,061ms | 14.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:26.5244492+00:00 | 3 | capacity | failed | 15,075ms | 14.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:53.6903524+00:00 | 2 | capacity | succeeded | 15,101ms | 12.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:23.8631579+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:26.9282074+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 14.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:38.9876767+00:00 | 2 | capacity | failed | 15,124ms | 12.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:42.0046165+00:00 | 3 | capacity | succeeded | 15,079ms | 12.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:12.1906158+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 7.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:27.2719456+00:00 | 1 | capacity | failed | 15,081ms | 12.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:27.2973822+00:00 | 3 | capacity | failed | 15,083ms | 12.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:54.3923659+00:00 | 2 | capacity | failed | 15,081ms | 12.0 MiB / 0.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:27.588358+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:42.7035901+00:00 | 1 | capacity | failed | 15,115ms | 12.0 MiB / 11.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:42.7309277+00:00 | 3 | capacity | failed | 15,127ms | 12.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:12.8643301+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:27.9133889+00:00 | 1 | capacity | succeeded | 15,076ms | 10.0 MiB / 1.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:54.9902669+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:58.0632959+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:10.0793839+00:00 | 2 | capacity | succeeded | 15,089ms | 13.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:13.1754906+00:00 | 3 | capacity | failed | 15,103ms | 10.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:40.2543367+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 0.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:43.343576+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:58.4172131+00:00 | 3 | capacity | failed | 15,073ms | 10.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:35:13.5100419+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:35:55.6172843+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:36:10.7343929+00:00 | 2 | capacity | failed | 15,117ms | 13.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:37:14.0865751+00:00 | 3 | capacity | succeeded | 15,067ms | 8.0 MiB / 0.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:37:29.1441238+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:37:44.2618429+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:37:59.3524642+00:00 | 3 | capacity | succeeded | 15,090ms | 9.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-29T18:38:26.3917718+00:00 | 2 | capacity | failed | 15,049ms | 13.0 MiB / 0.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:38:29.5299754+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:39:44.9649563+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:40:00.037768+00:00 | 3 | capacity | succeeded | 15,072ms | 8.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-29T18:40:45.3322499+00:00 | 3 | capacity | failed | 15,088ms | 8.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:41:45.5722164+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:00.6524123+00:00 | 1 | capacity | succeeded | 15,080ms | 11.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:00.7461982+00:00 | 3 | capacity | succeeded | 15,115ms | 9.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:30.8207358+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:30.956956+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:45.8970001+00:00 | 1 | capacity | failed | 15,076ms | 11.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:46.015233+00:00 | 3 | capacity | failed | 15,058ms | 9.0 MiB / 1.9 MiB |
*49 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 4 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 1 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 10 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 46 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 96 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 185 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 239 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 306 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 410 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 630 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 761 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 796 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 542 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 253 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 88 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 8 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 7 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 13 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 32 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 68 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 71 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 86 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 124 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 195 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 242 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 215 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 179 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 85 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 27 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 9 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 2 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 4 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 15 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 44 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 110 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 224 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 286 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 290 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 417 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 706 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 921 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 931 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 635 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 339 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 111 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 9 |
| Dekaf | 1 | 0.001–0.002ms | 4 |
| Dekaf | 1 | 0.002–0.004ms | 5 |
| Dekaf | 1 | 0.004–0.008ms | 27 |
| Dekaf | 1 | 0.008–0.016ms | 53 |
| Dekaf | 1 | 0.016–0.032ms | 145 |
| Dekaf | 1 | 0.032–0.064ms | 253 |
| Dekaf | 1 | 0.064–0.128ms | 330 |
| Dekaf | 1 | 0.128–0.256ms | 410 |
| Dekaf | 1 | 0.256–0.512ms | 671 |
| Dekaf | 1 | 0.512–1.024ms | 1,175 |
| Dekaf | 1 | 1.024–2.048ms | 1,413 |
| Dekaf | 1 | 2.048–4.096ms | 1,315 |
| Dekaf | 1 | 4.096–8.192ms | 897 |
| Dekaf | 1 | 8.192–16.384ms | 447 |
| Dekaf | 1 | 16.384–32.768ms | 151 |
| Dekaf | 1 | 32.768–65.536ms | 10 |
| Dekaf | 1 | 65.536–131.072ms | 2 |
| Dekaf | 2 | 0.001–0.002ms | 8 |
| Dekaf | 2 | 0.002–0.004ms | 14 |
| Dekaf | 2 | 0.004–0.008ms | 28 |
| Dekaf | 2 | 0.008–0.016ms | 57 |
| Dekaf | 2 | 0.016–0.032ms | 192 |
| Dekaf | 2 | 0.032–0.064ms | 343 |
| Dekaf | 2 | 0.064–0.128ms | 435 |
| Dekaf | 2 | 0.128–0.256ms | 466 |
| Dekaf | 2 | 0.256–0.512ms | 877 |
| Dekaf | 2 | 0.512–1.024ms | 1,421 |
| Dekaf | 2 | 1.024–2.048ms | 1,778 |
| Dekaf | 2 | 2.048–4.096ms | 1,731 |
| Dekaf | 2 | 4.096–8.192ms | 1,206 |
| Dekaf | 2 | 8.192–16.384ms | 652 |
| Dekaf | 2 | 16.384–32.768ms | 258 |
| Dekaf | 2 | 32.768–65.536ms | 32 |
| Dekaf | 2 | 65.536–131.072ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 1 |
| Dekaf | 3 | 0.004–0.008ms | 5 |
| Dekaf | 3 | 0.008–0.016ms | 12 |
| Dekaf | 3 | 0.016–0.032ms | 21 |
| Dekaf | 3 | 0.032–0.064ms | 52 |
| Dekaf | 3 | 0.064–0.128ms | 89 |
| Dekaf | 3 | 0.128–0.256ms | 124 |
| Dekaf | 3 | 0.256–0.512ms | 185 |
| Dekaf | 3 | 0.512–1.024ms | 312 |
| Dekaf | 3 | 1.024–2.048ms | 500 |
| Dekaf | 3 | 2.048–4.096ms | 673 |
| Dekaf | 3 | 4.096–8.192ms | 511 |
| Dekaf | 3 | 8.192–16.384ms | 261 |
| Dekaf | 3 | 16.384–32.768ms | 63 |
| Dekaf | 3 | 32.768–65.536ms | 13 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 97,000 | 2026-07-29T17:58:08.875459+00:00 | 119.8ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 98,000 | 2026-07-29T17:58:08.8759425+00:00 | 123.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 105,000 | 2026-07-29T17:58:08.8849751+00:00 | 118.8ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 107,000 | 2026-07-29T17:58:08.8870274+00:00 | 114.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 108,000 | 2026-07-29T17:58:08.8879846+00:00 | 115.8ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 115,000 | 2026-07-29T17:58:08.8979652+00:00 | 130.7ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 117,000 | 2026-07-29T17:58:08.9000179+00:00 | 161.1ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 118,000 | 2026-07-29T17:58:08.9009705+00:00 | 127.7ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 145,000 | 2026-07-29T17:58:08.9953837+00:00 | 110.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 147,000 | 2026-07-29T17:58:08.9980338+00:00 | 136.3ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 148,000 | 2026-07-29T17:58:08.9993501+00:00 | 135.3ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 155,000 | 2026-07-29T17:58:09.0081395+00:00 | 143.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 157,000 | 2026-07-29T17:58:09.0104557+00:00 | 141.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 158,000 | 2026-07-29T17:58:09.0116629+00:00 | 140.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 165,000 | 2026-07-29T17:58:09.03753+00:00 | 118.1ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 167,000 | 2026-07-29T17:58:09.0402004+00:00 | 129.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 175,000 | 2026-07-29T17:58:09.0634659+00:00 | 108.9ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 177,000 | 2026-07-29T17:58:09.0654538+00:00 | 111.2ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 178,000 | 2026-07-29T17:58:09.0675891+00:00 | 104.8ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 221,000 | 2026-07-29T17:58:09.1782107+00:00 | 117.9ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 222,000 | 2026-07-29T17:58:09.1791348+00:00 | 117.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 224,000 | 2026-07-29T17:58:09.181155+00:00 | 155.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 226,000 | 2026-07-29T17:58:09.1857286+00:00 | 150.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 231,000 | 2026-07-29T17:58:09.1945518+00:00 | 146.9ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 234,000 | 2026-07-29T17:58:09.2054662+00:00 | 136.1ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 241,000 | 2026-07-29T17:58:09.215604+00:00 | 133.7ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 242,000 | 2026-07-29T17:58:09.217071+00:00 | 132.2ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 244,000 | 2026-07-29T17:58:09.2195776+00:00 | 129.7ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 246,000 | 2026-07-29T17:58:09.2218372+00:00 | 127.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 251,000 | 2026-07-29T17:58:09.2275974+00:00 | 220.9ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 252,000 | 2026-07-29T17:58:09.228504+00:00 | 220.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 254,000 | 2026-07-29T17:58:09.2305021+00:00 | 134.6ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 262,000 | 2026-07-29T17:58:09.2393485+00:00 | 209.2ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 264,000 | 2026-07-29T17:58:09.2429111+00:00 | 202.3ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 266,000 | 2026-07-29T17:58:09.2451075+00:00 | 200.1ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 269,000 | 2026-07-29T17:58:09.2499305+00:00 | 178.6ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 270,000 | 2026-07-29T17:58:09.2511172+00:00 | 171.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 271,000 | 2026-07-29T17:58:09.2520317+00:00 | 200.9ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 272,000 | 2026-07-29T17:58:09.2969327+00:00 | 155.9ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 273,000 | 2026-07-29T17:58:09.2977961+00:00 | 130.7ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 274,000 | 2026-07-29T17:58:09.2986628+00:00 | 149.8ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 281,000 | 2026-07-29T17:58:09.3479572+00:00 | 115.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 284,000 | 2026-07-29T17:58:09.3516031+00:00 | 106.0ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 286,000 | 2026-07-29T17:58:09.3561842+00:00 | 101.5ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 291,000 | 2026-07-29T17:58:09.3616495+00:00 | 105.7ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 292,000 | 2026-07-29T17:58:09.3627703+00:00 | 104.6ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 294,000 | 2026-07-29T17:58:09.3664345+00:00 | 104.4ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 296,000 | 2026-07-29T17:58:09.3691398+00:00 | 101.6ms | GC pause | - | - | 1.0s / 383,406 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 485,000 | 2026-07-29T17:58:09.8833936+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 487,000 | 2026-07-29T17:58:09.8866764+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 495,000 | 2026-07-29T17:58:09.9041696+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 517,000 | 2026-07-29T17:58:09.9573579+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 527,000 | 2026-07-29T17:58:09.9667795+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 659,000 | 2026-07-29T17:58:10.2081696+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 767,000 | 2026-07-29T17:58:10.3629423+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 811,000 | 2026-07-29T17:58:10.4594794+00:00 | 126.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 812,000 | 2026-07-29T17:58:10.4613319+00:00 | 124.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 822,000 | 2026-07-29T17:58:10.4751619+00:00 | 124.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 826,000 | 2026-07-29T17:58:10.4795803+00:00 | 120.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 828,000 | 2026-07-29T17:58:10.4818991+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 831,000 | 2026-07-29T17:58:10.4854564+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 832,000 | 2026-07-29T17:58:10.4880917+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 835,000 | 2026-07-29T17:58:10.4907393+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 836,000 | 2026-07-29T17:58:10.4915201+00:00 | 118.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 841,000 | 2026-07-29T17:58:10.4957275+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 842,000 | 2026-07-29T17:58:10.4964243+00:00 | 123.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 844,000 | 2026-07-29T17:58:10.4979444+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 845,000 | 2026-07-29T17:58:10.4988979+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 846,000 | 2026-07-29T17:58:10.4997093+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 851,000 | 2026-07-29T17:58:10.5038998+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 852,000 | 2026-07-29T17:58:10.5048564+00:00 | 139.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 854,000 | 2026-07-29T17:58:10.5256315+00:00 | 124.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 512,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,298,000 | 2026-07-29T17:58:11.1925305+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,305,000 | 2026-07-29T17:58:11.1990289+00:00 | 134.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,307,000 | 2026-07-29T17:58:11.2010036+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,308,000 | 2026-07-29T17:58:11.2014968+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,315,000 | 2026-07-29T17:58:11.2183862+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,317,000 | 2026-07-29T17:58:11.2201415+00:00 | 125.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,318,000 | 2026-07-29T17:58:11.2215602+00:00 | 131.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,325,000 | 2026-07-29T17:58:11.2266541+00:00 | 133.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,327,000 | 2026-07-29T17:58:11.2277382+00:00 | 151.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,331,000 | 2026-07-29T17:58:11.2353457+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,364,000 | 2026-07-29T17:58:11.3388882+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,372,000 | 2026-07-29T17:58:11.3536575+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,374,000 | 2026-07-29T17:58:11.3576298+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,391,000 | 2026-07-29T17:58:11.3978578+00:00 | 113.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,394,000 | 2026-07-29T17:58:11.4041911+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,396,000 | 2026-07-29T17:58:11.4056555+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,402,000 | 2026-07-29T17:58:11.40948+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,404,000 | 2026-07-29T17:58:11.411247+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,406,000 | 2026-07-29T17:58:11.4121441+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,411,000 | 2026-07-29T17:58:11.4196259+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,412,000 | 2026-07-29T17:58:11.420159+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 661,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,280,000 | 2026-07-29T17:58:12.5685975+00:00 | 134.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,283,000 | 2026-07-29T17:58:12.5726296+00:00 | 130.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,289,000 | 2026-07-29T17:58:12.5771915+00:00 | 137.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,290,000 | 2026-07-29T17:58:12.5810474+00:00 | 122.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,293,000 | 2026-07-29T17:58:12.5827608+00:00 | 131.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,300,000 | 2026-07-29T17:58:12.6003543+00:00 | 131.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,303,000 | 2026-07-29T17:58:12.6017074+00:00 | 131.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,309,000 | 2026-07-29T17:58:12.6226531+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,313,000 | 2026-07-29T17:58:12.6246695+00:00 | 122.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,319,000 | 2026-07-29T17:58:12.6401828+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,320,000 | 2026-07-29T17:58:12.6409618+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 842,002 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,066,000 | 2026-07-29T17:58:14.7714161+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,074,000 | 2026-07-29T17:58:14.779853+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,084,000 | 2026-07-29T17:58:14.7868895+00:00 | 117.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,086,000 | 2026-07-29T17:58:14.7875743+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,091,000 | 2026-07-29T17:58:14.7919721+00:00 | 166.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,092,000 | 2026-07-29T17:58:14.7925011+00:00 | 165.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,096,000 | 2026-07-29T17:58:14.7963978+00:00 | 159.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,101,000 | 2026-07-29T17:58:14.8025569+00:00 | 177.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,104,000 | 2026-07-29T17:58:14.8037953+00:00 | 155.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,106,000 | 2026-07-29T17:58:14.8052538+00:00 | 153.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,111,000 | 2026-07-29T17:58:14.8315179+00:00 | 147.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,112,000 | 2026-07-29T17:58:14.8326363+00:00 | 152.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,114,000 | 2026-07-29T17:58:14.8736106+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,126,000 | 2026-07-29T17:58:14.8880547+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,131,000 | 2026-07-29T17:58:14.8905621+00:00 | 135.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,132,000 | 2026-07-29T17:58:14.8917927+00:00 | 134.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,136,000 | 2026-07-29T17:58:14.9048862+00:00 | 128.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 854,038 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,933,000 | 2026-07-29T17:58:15.7496757+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,939,000 | 2026-07-29T17:58:15.7529918+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,943,000 | 2026-07-29T17:58:15.7548041+00:00 | 132.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,953,000 | 2026-07-29T17:58:15.7596062+00:00 | 140.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,959,000 | 2026-07-29T17:58:15.7664435+00:00 | 143.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,960,000 | 2026-07-29T17:58:15.7668929+00:00 | 152.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,508,000 | 2026-07-29T17:58:16.5075246+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 799,401 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,288,000 | 2026-07-29T17:58:17.3439032+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,295,000 | 2026-07-29T17:58:17.350538+00:00 | 139.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,297,000 | 2026-07-29T17:58:17.3519703+00:00 | 147.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,298,000 | 2026-07-29T17:58:17.3522977+00:00 | 137.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,305,000 | 2026-07-29T17:58:17.3585466+00:00 | 153.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,308,000 | 2026-07-29T17:58:17.3642155+00:00 | 147.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,317,000 | 2026-07-29T17:58:17.3701988+00:00 | 155.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,318,000 | 2026-07-29T17:58:17.3707883+00:00 | 154.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,327,000 | 2026-07-29T17:58:17.4020828+00:00 | 146.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,328,000 | 2026-07-29T17:58:17.430342+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,337,000 | 2026-07-29T17:58:17.4383829+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 837,173 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,431,000 | 2026-07-29T17:58:18.7278151+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,432,000 | 2026-07-29T17:58:18.7283186+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,434,000 | 2026-07-29T17:58:18.7292539+00:00 | 125.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,441,000 | 2026-07-29T17:58:18.7354823+00:00 | 149.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,442,000 | 2026-07-29T17:58:18.736058+00:00 | 149.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,446,000 | 2026-07-29T17:58:18.7468084+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,451,000 | 2026-07-29T17:58:18.7504803+00:00 | 134.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,452,000 | 2026-07-29T17:58:18.7509546+00:00 | 134.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,454,000 | 2026-07-29T17:58:18.7516904+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,461,000 | 2026-07-29T17:58:18.7623375+00:00 | 122.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,462,000 | 2026-07-29T17:58:18.7638604+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,474,000 | 2026-07-29T17:58:18.7727056+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,640,000 | 2026-07-29T17:58:19.0360048+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 780,277 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,910,000 | 2026-07-29T17:58:20.4986627+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,913,000 | 2026-07-29T17:58:20.5033767+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,919,000 | 2026-07-29T17:58:20.5069078+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,920,000 | 2026-07-29T17:58:20.5079208+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,923,000 | 2026-07-29T17:58:20.509479+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,929,000 | 2026-07-29T17:58:20.5132145+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,930,000 | 2026-07-29T17:58:20.5136489+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,939,000 | 2026-07-29T17:58:20.529912+00:00 | 122.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,940,000 | 2026-07-29T17:58:20.5419624+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,943,000 | 2026-07-29T17:58:20.5461134+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,579,000 | 2026-07-29T17:58:21.3233992+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,580,000 | 2026-07-29T17:58:21.3274572+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,584,000 | 2026-07-29T17:58:21.3305513+00:00 | 118.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,589,000 | 2026-07-29T17:58:21.33586+00:00 | 115.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,590,000 | 2026-07-29T17:58:21.3428051+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,591,000 | 2026-07-29T17:58:21.3435304+00:00 | 125.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,592,000 | 2026-07-29T17:58:21.34639+00:00 | 122.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,601,000 | 2026-07-29T17:58:21.3606663+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,602,000 | 2026-07-29T17:58:21.3614518+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,603,000 | 2026-07-29T17:58:21.3620648+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,604,000 | 2026-07-29T17:58:21.3640221+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 814,459 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,947,000 | 2026-07-29T17:58:21.8290145+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 847,001 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,197,000 | 2026-07-29T17:58:23.2708708+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 881,301 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,205,000 | 2026-07-29T17:58:23.2842303+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 881,301 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,207,000 | 2026-07-29T17:58:23.2865849+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 881,301 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,215,000 | 2026-07-29T17:58:23.2944247+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 881,301 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,895,000 | 2026-07-29T17:58:25.1478883+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 779,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,897,000 | 2026-07-29T17:58:25.1489045+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 779,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,367,000 | 2026-07-29T17:58:25.7847918+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 888,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,377,000 | 2026-07-29T17:58:25.7916113+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 888,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,385,000 | 2026-07-29T17:58:25.7980518+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 888,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,387,000 | 2026-07-29T17:58:25.8004197+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 888,060 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,273,000 | 2026-07-29T17:58:26.821397+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,657,000 | 2026-07-29T17:58:27.2866081+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,667,000 | 2026-07-29T17:58:27.2938535+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,677,000 | 2026-07-29T17:58:27.3043988+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,685,000 | 2026-07-29T17:58:27.3113231+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,687,000 | 2026-07-29T17:58:27.3134369+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,688,000 | 2026-07-29T17:58:27.314556+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,697,000 | 2026-07-29T17:58:27.3275493+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,698,000 | 2026-07-29T17:58:27.3292148+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 809,603 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,091,000 | 2026-07-29T17:58:27.8046191+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,102,000 | 2026-07-29T17:58:27.8111525+00:00 | 135.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,104,000 | 2026-07-29T17:58:27.8122677+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,106,000 | 2026-07-29T17:58:27.8129512+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,111,000 | 2026-07-29T17:58:27.8153577+00:00 | 131.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,114,000 | 2026-07-29T17:58:27.8168364+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,116,000 | 2026-07-29T17:58:27.8177786+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,121,000 | 2026-07-29T17:58:27.8202201+00:00 | 131.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,122,000 | 2026-07-29T17:58:27.8214709+00:00 | 130.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,131,000 | 2026-07-29T17:58:27.8428045+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 926,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,106,000 | 2026-07-29T17:58:31.053219+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 952,984 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 175,413,000 | 2026-07-29T18:01:15.3109636+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 187.2s / 1,090,600 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 175,429,000 | 2026-07-29T18:01:15.317633+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 187.2s / 1,090,600 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 175,433,000 | 2026-07-29T18:01:15.319406+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 187.2s / 1,090,600 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 209,937,000 | 2026-07-29T18:01:51.3407871+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 223.2s / 1,086,407 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,381,000 | 2026-07-29T18:03:10.7928075+00:00 | 107.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,384,000 | 2026-07-29T18:03:10.7954878+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,391,000 | 2026-07-29T18:03:10.800517+00:00 | 108.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,392,000 | 2026-07-29T18:03:10.8010571+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,394,000 | 2026-07-29T18:03:10.803197+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,396,000 | 2026-07-29T18:03:10.8049313+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,401,000 | 2026-07-29T18:03:10.8081101+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,402,000 | 2026-07-29T18:03:10.8099195+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,404,000 | 2026-07-29T18:03:10.8104706+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 290,406,000 | 2026-07-29T18:03:10.8112775+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 303.2s / 1,365,317 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,965,000 | 2026-07-29T18:04:05.243037+00:00 | 139.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 357.3s / 811,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,975,000 | 2026-07-29T18:04:05.2515763+00:00 | 134.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 357.3s / 811,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,977,000 | 2026-07-29T18:04:05.2557243+00:00 | 137.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 357.3s / 811,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,985,000 | 2026-07-29T18:04:05.2628547+00:00 | 126.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 357.3s / 811,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,995,000 | 2026-07-29T18:04:05.2704381+00:00 | 121.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 357.3s / 811,309 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 364,823,000 | 2026-07-29T18:04:27.3166339+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 379.3s / 951,474 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 371,873,000 | 2026-07-29T18:04:34.8269991+00:00 | 114.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 386.3s / 890,018 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 371,879,000 | 2026-07-29T18:04:34.8311424+00:00 | 108.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 386.3s / 890,018 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,900,000 | 2026-07-29T18:04:55.3190452+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 407.3s / 1,103,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,910,000 | 2026-07-29T18:04:55.3327405+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 407.3s / 1,103,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,919,000 | 2026-07-29T18:04:55.3390687+00:00 | 110.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 407.3s / 1,103,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,930,000 | 2026-07-29T18:04:55.3531515+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 407.3s / 1,103,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,940,000 | 2026-07-29T18:04:55.3645595+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 407.3s / 1,103,090 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 400,420,000 | 2026-07-29T18:05:05.3202291+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 417.3s / 955,899 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 519,039,000 | 2026-07-29T18:07:09.8056777+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 541.4s / 932,101 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 519,043,000 | 2026-07-29T18:07:09.8082922+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 541.4s / 932,101 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 519,049,000 | 2026-07-29T18:07:09.8106338+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 541.4s / 932,101 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 519,050,000 | 2026-07-29T18:07:09.8108739+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 541.4s / 932,101 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 519,053,000 | 2026-07-29T18:07:09.8240805+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 541.4s / 932,101 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 519,063,000 | 2026-07-29T18:07:09.8403445+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 541.4s / 932,101 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 548,597,000 | 2026-07-29T18:07:40.777671+00:00 | 129.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 548,598,000 | 2026-07-29T18:07:40.7784994+00:00 | 132.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 548,605,000 | 2026-07-29T18:07:40.7867286+00:00 | 125.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 548,608,000 | 2026-07-29T18:07:40.7903354+00:00 | 126.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 548,615,000 | 2026-07-29T18:07:40.8093254+00:00 | 111.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 548,617,000 | 2026-07-29T18:07:40.8101912+00:00 | 126.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 548,618,000 | 2026-07-29T18:07:40.8105352+00:00 | 109.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 572.4s / 902,446 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 592,327,000 | 2026-07-29T18:08:26.3034614+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 618.5s / 865,768 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 595,078,000 | 2026-07-29T18:08:29.3154988+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 621.5s / 843,302 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 618,357,000 | 2026-07-29T18:08:52.3148235+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 644.5s / 1,011,835 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 618,365,000 | 2026-07-29T18:08:52.3203857+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 644.5s / 1,011,835 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 618,367,000 | 2026-07-29T18:08:52.3210247+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 644.5s / 1,011,835 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 618,375,000 | 2026-07-29T18:08:52.3271737+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 644.5s / 1,011,835 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 733,803,000 | 2026-07-29T18:10:43.8118939+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 755.6s / 952,519 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,809,000 | 2026-07-29T18:10:43.8184813+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 755.6s / 952,519 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,810,000 | 2026-07-29T18:10:43.8187828+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 755.6s / 952,519 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 733,813,000 | 2026-07-29T18:10:43.8215387+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 755.6s / 952,519 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 738,043,000 | 2026-07-29T18:10:47.8297828+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 759.6s / 1,096,918 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 658,000 | 2026-07-29T18:13:09.7988679+00:00 | 104.0ms | GC pause | - | - | 1.0s / 749,680 msg/s | Gen2 +1 / pause +64.9ms |
| Confluent | 695,000 | 2026-07-29T18:13:09.8461225+00:00 | 186.5ms | GC pause | - | - | 2.0s / 711,851 msg/s | Gen2 +1 / pause +148.5ms |
| Confluent | 733,000 | 2026-07-29T18:13:09.8929909+00:00 | 113.6ms | GC pause | - | - | 2.0s / 711,851 msg/s | Gen2 +1 / pause +148.5ms |
| Confluent | 739,000 | 2026-07-29T18:13:09.9018131+00:00 | 166.2ms | GC pause | - | - | 2.0s / 711,851 msg/s | Gen2 +1 / pause +148.5ms |
| Confluent | 816,000 | 2026-07-29T18:13:09.9974175+00:00 | 159.3ms | GC pause | - | - | 2.0s / 711,851 msg/s | Gen2 +0 / pause +83.6ms |
| Confluent | 15,005,000 | 2026-07-29T18:13:31.3841949+00:00 | 108.6ms | GC pause | - | - | 23.0s / 818,524 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 16,611,000 | 2026-07-29T18:13:33.3642937+00:00 | 107.7ms | GC pause | - | - | 25.0s / 754,095 msg/s | Gen2 +0 / pause +58.8ms |
| Confluent | 42,328,000 | 2026-07-29T18:14:15.8569578+00:00 | 115.3ms | GC pause | - | - | 68.0s / 625,899 msg/s | Gen2 +0 / pause +337.6ms |
| Confluent | 42,337,000 | 2026-07-29T18:14:15.8647262+00:00 | 113.8ms | GC pause | - | - | 68.0s / 625,899 msg/s | Gen2 +0 / pause +337.6ms |
| Confluent | 43,403,000 | 2026-07-29T18:14:17.330356+00:00 | 110.6ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,453,000 | 2026-07-29T18:14:17.4101074+00:00 | 104.3ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,487,000 | 2026-07-29T18:14:17.470328+00:00 | 102.5ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,553,000 | 2026-07-29T18:14:17.5501115+00:00 | 120.9ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,583,000 | 2026-07-29T18:14:17.5851137+00:00 | 112.6ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,778,000 | 2026-07-29T18:14:17.8081067+00:00 | 109.4ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,780,000 | 2026-07-29T18:14:17.8092771+00:00 | 143.9ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,801,000 | 2026-07-29T18:14:17.8333531+00:00 | 109.1ms | GC pause | - | - | 69.0s / 850,213 msg/s | Gen2 +0 / pause +103.2ms |
| Confluent | 43,840,000 | 2026-07-29T18:14:17.8783448+00:00 | 117.0ms | GC pause | - | - | 70.0s / 860,824 msg/s | Gen2 +0 / pause +176.4ms |
| Confluent | 52,088,000 | 2026-07-29T18:14:31.3108976+00:00 | 131.4ms | GC pause | - | - | 83.0s / 952,302 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 52,217,000 | 2026-07-29T18:14:31.4459791+00:00 | 109.7ms | GC pause | - | - | 83.0s / 952,302 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 53,036,000 | 2026-07-29T18:14:32.3317666+00:00 | 105.5ms | GC pause | - | - | 84.0s / 675,058 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 53,055,000 | 2026-07-29T18:14:32.3484626+00:00 | 101.2ms | GC pause | - | - | 84.0s / 675,058 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 53,078,000 | 2026-07-29T18:14:32.3692216+00:00 | 103.6ms | GC pause | - | - | 84.0s / 675,058 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 53,086,000 | 2026-07-29T18:14:32.3755773+00:00 | 104.0ms | GC pause | - | - | 84.0s / 675,058 msg/s | Gen2 +0 / pause +99.4ms |
| Confluent | 54,979,000 | 2026-07-29T18:14:34.8113021+00:00 | 122.1ms | GC pause | - | - | 86.0s / 951,450 msg/s | Gen2 +0 / pause +80.1ms |
| Confluent | 55,009,000 | 2026-07-29T18:14:34.8403568+00:00 | 116.3ms | GC pause | - | - | 86.0s / 951,450 msg/s | Gen2 +0 / pause +80.1ms |
| Confluent | 55,029,000 | 2026-07-29T18:14:34.8564051+00:00 | 111.4ms | GC pause | - | - | 87.0s / 833,464 msg/s | Gen2 +0 / pause +156.3ms |
| Confluent | 55,096,000 | 2026-07-29T18:14:34.9215249+00:00 | 118.2ms | GC pause | - | - | 87.0s / 833,464 msg/s | Gen2 +0 / pause +156.3ms |
| Confluent | 56,848,000 | 2026-07-29T18:14:36.8179578+00:00 | 101.6ms | GC pause | - | - | 88.1s / 1,016,116 msg/s | Gen2 +0 / pause +87.5ms |
| Confluent | 56,867,000 | 2026-07-29T18:14:36.8357161+00:00 | 123.3ms | GC pause | - | - | 88.1s / 1,016,116 msg/s | Gen2 +0 / pause +87.5ms |
| Confluent | 56,905,000 | 2026-07-29T18:14:36.8664307+00:00 | 125.7ms | GC pause | - | - | 89.1s / 827,579 msg/s | Gen2 +0 / pause +171.6ms |
| Confluent | 56,910,000 | 2026-07-29T18:14:36.871041+00:00 | 106.3ms | GC pause | - | - | 89.1s / 827,579 msg/s | Gen2 +0 / pause +171.6ms |
| Confluent | 56,921,000 | 2026-07-29T18:14:36.8840488+00:00 | 159.5ms | GC pause | - | - | 89.1s / 827,579 msg/s | Gen2 +0 / pause +171.6ms |
| Confluent | 56,981,000 | 2026-07-29T18:14:36.9645661+00:00 | 148.8ms | GC pause | - | - | 89.1s / 827,579 msg/s | Gen2 +0 / pause +171.6ms |
| Confluent | 57,017,000 | 2026-07-29T18:14:37.0184827+00:00 | 116.8ms | GC pause | - | - | 89.1s / 827,579 msg/s | Gen2 +0 / pause +84.1ms |
| Confluent | 57,027,000 | 2026-07-29T18:14:37.0391671+00:00 | 103.6ms | GC pause | - | - | 89.1s / 827,579 msg/s | Gen2 +0 / pause +84.1ms |
| Confluent | 60,057,000 | 2026-07-29T18:14:40.3369261+00:00 | 150.9ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,094,000 | 2026-07-29T18:14:40.3780599+00:00 | 111.5ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,108,000 | 2026-07-29T18:14:40.3951188+00:00 | 167.6ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,158,000 | 2026-07-29T18:14:40.4387597+00:00 | 185.8ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,197,000 | 2026-07-29T18:14:40.5140097+00:00 | 146.5ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,248,000 | 2026-07-29T18:14:40.6098379+00:00 | 127.8ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,373,000 | 2026-07-29T18:14:40.7609938+00:00 | 146.3ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,380,000 | 2026-07-29T18:14:40.7687813+00:00 | 139.1ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 60,383,000 | 2026-07-29T18:14:40.7717866+00:00 | 138.3ms | GC pause | - | - | 92.1s / 805,918 msg/s | Gen2 +0 / pause +85.6ms |
| Confluent | 61,592,000 | 2026-07-29T18:14:42.0671872+00:00 | 133.8ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,803,000 | 2026-07-29T18:14:42.311659+00:00 | 159.1ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,810,000 | 2026-07-29T18:14:42.3227413+00:00 | 173.4ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,813,000 | 2026-07-29T18:14:42.3256144+00:00 | 170.7ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,910,000 | 2026-07-29T18:14:42.4472657+00:00 | 132.6ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,920,000 | 2026-07-29T18:14:42.4590927+00:00 | 128.4ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,940,000 | 2026-07-29T18:14:42.4825138+00:00 | 122.8ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 61,943,000 | 2026-07-29T18:14:42.4872038+00:00 | 118.3ms | GC pause | - | - | 94.1s / 857,983 msg/s | Gen2 +0 / pause +71.6ms |
| Confluent | 244,052,000 | 2026-07-29T18:19:21.8636669+00:00 | 150.3ms | GC pause | - | - | 373.2s / 810,233 msg/s | Gen2 +0 / pause +145.2ms |
| Confluent | 244,076,000 | 2026-07-29T18:19:21.8957167+00:00 | 179.6ms | GC pause | - | - | 373.2s / 810,233 msg/s | Gen2 +0 / pause +145.2ms |
| Confluent | 244,119,000 | 2026-07-29T18:19:21.9636731+00:00 | 131.3ms | GC pause | - | - | 373.2s / 810,233 msg/s | Gen2 +0 / pause +145.2ms |
| Confluent | 244,125,000 | 2026-07-29T18:19:21.9710616+00:00 | 125.1ms | GC pause | - | - | 373.2s / 810,233 msg/s | Gen2 +0 / pause +145.2ms |
| Confluent | 244,129,000 | 2026-07-29T18:19:21.9769404+00:00 | 120.1ms | GC pause | - | - | 373.2s / 810,233 msg/s | Gen2 +0 / pause +145.2ms |
| Confluent | 244,145,000 | 2026-07-29T18:19:21.9908755+00:00 | 111.1ms | GC pause | - | - | 373.2s / 810,233 msg/s | Gen2 +0 / pause +145.2ms |
| Confluent | 244,432,000 | 2026-07-29T18:19:22.3057058+00:00 | 109.2ms | GC pause | - | - | 374.2s / 968,517 msg/s | Gen2 +0 / pause +100.5ms |
| Confluent | 244,442,000 | 2026-07-29T18:19:22.31667+00:00 | 116.9ms | GC pause | - | - | 374.2s / 968,517 msg/s | Gen2 +0 / pause +100.5ms |
| Confluent | 245,941,000 | 2026-07-29T18:19:23.9079023+00:00 | 128.6ms | GC pause | - | - | 375.2s / 797,888 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 245,950,000 | 2026-07-29T18:19:23.9178212+00:00 | 118.8ms | GC pause | - | - | 375.2s / 797,888 msg/s | Gen2 +0 / pause +120.5ms |
| Confluent | 254,261,000 | 2026-07-29T18:19:36.3076035+00:00 | 105.7ms | GC pause | - | - | 388.3s / 915,388 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 254,321,000 | 2026-07-29T18:19:36.3688013+00:00 | 117.0ms | GC pause | - | - | 388.3s / 915,388 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 254,411,000 | 2026-07-29T18:19:36.4837903+00:00 | 105.6ms | GC pause | - | - | 388.3s / 915,388 msg/s | Gen2 +0 / pause +87.9ms |
| Confluent | 255,230,000 | 2026-07-29T18:19:37.3785382+00:00 | 143.4ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,241,000 | 2026-07-29T18:19:37.3900184+00:00 | 136.1ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,300,000 | 2026-07-29T18:19:37.445289+00:00 | 159.9ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,318,000 | 2026-07-29T18:19:37.4644429+00:00 | 152.7ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,353,000 | 2026-07-29T18:19:37.5037374+00:00 | 168.3ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,427,000 | 2026-07-29T18:19:37.5950666+00:00 | 133.7ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,470,000 | 2026-07-29T18:19:37.6422961+00:00 | 157.4ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,493,000 | 2026-07-29T18:19:37.6684669+00:00 | 152.1ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,513,000 | 2026-07-29T18:19:37.6907794+00:00 | 162.6ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 255,713,000 | 2026-07-29T18:19:37.9081222+00:00 | 156.8ms | GC pause | - | - | 389.3s / 869,481 msg/s | Gen2 +0 / pause +90.4ms |
| Confluent | 256,984,000 | 2026-07-29T18:19:39.3691693+00:00 | 118.2ms | GC pause | - | - | 391.3s / 974,236 msg/s | Gen2 +0 / pause +86.3ms |
| Confluent | 256,994,000 | 2026-07-29T18:19:39.381301+00:00 | 120.0ms | GC pause | - | - | 391.3s / 974,236 msg/s | Gen2 +0 / pause +86.3ms |
| Confluent | 257,891,000 | 2026-07-29T18:19:40.2885377+00:00 | 107.3ms | GC pause | - | - | 392.3s / 820,393 msg/s | Gen2 +0 / pause +168.2ms |
| Confluent | 258,030,000 | 2026-07-29T18:19:40.4349893+00:00 | 139.3ms | GC pause | - | - | 392.3s / 820,393 msg/s | Gen2 +0 / pause +168.2ms |
| Confluent | 258,130,000 | 2026-07-29T18:19:40.5527819+00:00 | 102.1ms | GC pause | - | - | 392.3s / 820,393 msg/s | Gen2 +0 / pause +168.2ms |
| Confluent | 259,733,000 | 2026-07-29T18:19:42.4645926+00:00 | 103.1ms | GC pause | - | - | 394.3s / 884,091 msg/s | Gen2 +0 / pause +110.7ms |
| Confluent | 259,741,000 | 2026-07-29T18:19:42.4699469+00:00 | 110.7ms | GC pause | - | - | 394.3s / 884,091 msg/s | Gen2 +0 / pause +110.7ms |
| Confluent | 259,753,000 | 2026-07-29T18:19:42.4830774+00:00 | 119.5ms | GC pause | - | - | 394.3s / 884,091 msg/s | Gen2 +0 / pause +110.7ms |
| Confluent | 259,778,000 | 2026-07-29T18:19:42.5081926+00:00 | 105.5ms | GC pause | - | - | 394.3s / 884,091 msg/s | Gen2 +0 / pause +110.7ms |
| Confluent | 259,808,000 | 2026-07-29T18:19:42.5425118+00:00 | 105.1ms | GC pause | - | - | 394.3s / 884,091 msg/s | Gen2 +0 / pause +110.7ms |
| Confluent | 259,817,000 | 2026-07-29T18:19:42.5505432+00:00 | 111.8ms | GC pause | - | - | 394.3s / 884,091 msg/s | Gen2 +0 / pause +110.7ms |
| Confluent | 260,470,000 | 2026-07-29T18:19:43.3044583+00:00 | 168.3ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,491,000 | 2026-07-29T18:19:43.3233566+00:00 | 147.8ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,500,000 | 2026-07-29T18:19:43.3291388+00:00 | 182.3ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,538,000 | 2026-07-29T18:19:43.3764631+00:00 | 142.6ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,647,000 | 2026-07-29T18:19:43.4947307+00:00 | 125.8ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,737,000 | 2026-07-29T18:19:43.6079094+00:00 | 128.6ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,768,000 | 2026-07-29T18:19:43.6455196+00:00 | 112.9ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,781,000 | 2026-07-29T18:19:43.6645027+00:00 | 100.9ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,788,000 | 2026-07-29T18:19:43.6725584+00:00 | 100.7ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 260,840,000 | 2026-07-29T18:19:43.7367409+00:00 | 144.4ms | GC pause | - | - | 395.3s / 869,063 msg/s | Gen2 +0 / pause +85.1ms |
| Confluent | 262,824,000 | 2026-07-29T18:19:45.8847579+00:00 | 126.6ms | GC pause | - | - | 397.3s / 917,393 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 262,844,000 | 2026-07-29T18:19:45.9022263+00:00 | 119.3ms | GC pause | - | - | 397.3s / 917,393 msg/s | Gen2 +0 / pause +102.0ms |
| Confluent | 263,253,000 | 2026-07-29T18:19:46.3372098+00:00 | 132.7ms | GC pause | - | - | 398.3s / 912,860 msg/s | Gen2 +0 / pause +104.0ms |
| Confluent | 263,268,000 | 2026-07-29T18:19:46.3579971+00:00 | 158.7ms | GC pause | - | - | 398.3s / 912,860 msg/s | Gen2 +0 / pause +104.0ms |
| Confluent | 263,381,000 | 2026-07-29T18:19:46.4968772+00:00 | 116.1ms | GC pause | - | - | 398.3s / 912,860 msg/s | Gen2 +0 / pause +104.0ms |
| Confluent | 263,391,000 | 2026-07-29T18:19:46.511648+00:00 | 105.6ms | GC pause | - | - | 398.3s / 912,860 msg/s | Gen2 +0 / pause +104.0ms |
| Confluent | 317,780,000 | 2026-07-29T18:21:08.8069162+00:00 | 101.4ms | GC pause | - | - | 480.3s / 795,493 msg/s | Gen2 +0 / pause +98.3ms |
| Confluent | 317,783,000 | 2026-07-29T18:21:08.8096486+00:00 | 103.4ms | GC pause | - | - | 480.3s / 795,493 msg/s | Gen2 +0 / pause +98.3ms |
| Confluent | 328,777,000 | 2026-07-29T18:21:28.7270271+00:00 | 133.9ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 328,787,000 | 2026-07-29T18:21:28.7370212+00:00 | 133.6ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 328,903,000 | 2026-07-29T18:21:28.8194471+00:00 | 209.8ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 328,907,000 | 2026-07-29T18:21:28.8222812+00:00 | 153.7ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 328,953,000 | 2026-07-29T18:21:28.8598198+00:00 | 210.1ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 329,053,000 | 2026-07-29T18:21:28.9868361+00:00 | 164.2ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 329,120,000 | 2026-07-29T18:21:29.0516206+00:00 | 148.0ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 329,133,000 | 2026-07-29T18:21:29.0668975+00:00 | 139.7ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 329,183,000 | 2026-07-29T18:21:29.1232495+00:00 | 126.3ms | GC pause | - | - | 500.3s / 768,171 msg/s | Gen2 +0 / pause +110.6ms |
| Confluent | 329,260,000 | 2026-07-29T18:21:29.2094388+00:00 | 108.7ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +192.7ms |
| Confluent | 329,370,000 | 2026-07-29T18:21:29.3272844+00:00 | 145.7ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,443,000 | 2026-07-29T18:21:29.4030866+00:00 | 139.4ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,510,000 | 2026-07-29T18:21:29.506245+00:00 | 112.7ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,653,000 | 2026-07-29T18:21:29.6514151+00:00 | 124.7ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,680,000 | 2026-07-29T18:21:29.6802381+00:00 | 174.5ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,800,000 | 2026-07-29T18:21:29.7993516+00:00 | 211.1ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,828,000 | 2026-07-29T18:21:29.8226559+00:00 | 174.5ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,970,000 | 2026-07-29T18:21:29.9511094+00:00 | 212.4ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 329,971,000 | 2026-07-29T18:21:29.9556103+00:00 | 213.6ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 330,028,000 | 2026-07-29T18:21:30.0116534+00:00 | 194.7ms | GC pause | - | - | 501.3s / 919,839 msg/s | Gen2 +0 / pause +82.1ms |
| Confluent | 330,163,000 | 2026-07-29T18:21:30.1695689+00:00 | 118.6ms | GC pause | - | - | 502.3s / 712,804 msg/s | Gen2 +0 / pause +185.5ms |
| Confluent | 352,179,000 | 2026-07-29T18:22:03.3361963+00:00 | 107.9ms | GC pause | - | - | 535.4s / 973,276 msg/s | Gen2 +0 / pause +84.9ms |
| Confluent | 361,359,000 | 2026-07-29T18:22:15.3911194+00:00 | 111.3ms | GC pause | - | - | 547.4s / 968,398 msg/s | Gen2 +0 / pause +96.0ms |
| Confluent | 361,389,000 | 2026-07-29T18:22:15.4306455+00:00 | 124.3ms | GC pause | - | - | 547.4s / 968,398 msg/s | Gen2 +0 / pause +96.0ms |
| Confluent | 361,435,000 | 2026-07-29T18:22:15.4783848+00:00 | 106.5ms | GC pause | - | - | 547.4s / 968,398 msg/s | Gen2 +0 / pause +96.0ms |
| Confluent | 366,385,000 | 2026-07-29T18:22:20.4073264+00:00 | 104.0ms | GC pause | - | - | 552.4s / 908,732 msg/s | Gen2 +0 / pause +77.6ms |
| Confluent | 366,445,000 | 2026-07-29T18:22:20.4577249+00:00 | 142.7ms | GC pause | - | - | 552.4s / 908,732 msg/s | Gen2 +0 / pause +77.6ms |
| Confluent | 366,495,000 | 2026-07-29T18:22:20.5262501+00:00 | 112.3ms | GC pause | - | - | 552.4s / 908,732 msg/s | Gen2 +0 / pause +77.6ms |
| Confluent | 367,105,000 | 2026-07-29T18:22:21.2375476+00:00 | 106.2ms | GC pause | - | - | 553.4s / 860,720 msg/s | Gen2 +0 / pause +162.6ms |
| Confluent | 367,135,000 | 2026-07-29T18:22:21.2634553+00:00 | 130.2ms | GC pause | - | - | 553.4s / 860,720 msg/s | Gen2 +0 / pause +162.6ms |
| Confluent | 367,216,000 | 2026-07-29T18:22:21.3447907+00:00 | 135.6ms | GC pause | - | - | 553.4s / 860,720 msg/s | Gen2 +0 / pause +85.0ms |
| Confluent | 368,188,000 | 2026-07-29T18:22:22.4851418+00:00 | 106.2ms | GC pause | - | - | 554.4s / 783,806 msg/s | Gen2 +0 / pause +76.2ms |
| Confluent | 368,300,000 | 2026-07-29T18:22:22.6113616+00:00 | 119.8ms | GC pause | - | - | 554.4s / 783,806 msg/s | Gen2 +0 / pause +76.2ms |
| Confluent | 368,333,000 | 2026-07-29T18:22:22.658634+00:00 | 115.7ms | GC pause | - | - | 554.4s / 783,806 msg/s | Gen2 +0 / pause +76.2ms |
| Confluent | 368,530,000 | 2026-07-29T18:22:22.9273743+00:00 | 176.5ms | GC pause | - | - | 554.4s / 783,806 msg/s | Gen2 +0 / pause +76.2ms |
| Confluent | 368,690,000 | 2026-07-29T18:22:23.1273443+00:00 | 126.3ms | GC pause | - | - | 554.4s / 783,806 msg/s | Gen2 +0 / pause +76.2ms |
| Confluent | 370,858,000 | 2026-07-29T18:22:25.5780246+00:00 | 109.1ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 370,937,000 | 2026-07-29T18:22:25.6507027+00:00 | 189.2ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 370,948,000 | 2026-07-29T18:22:25.6598839+00:00 | 187.4ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 370,987,000 | 2026-07-29T18:22:25.7087952+00:00 | 195.3ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 370,998,000 | 2026-07-29T18:22:25.726026+00:00 | 201.9ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,018,000 | 2026-07-29T18:22:25.750836+00:00 | 211.2ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,070,000 | 2026-07-29T18:22:25.807094+00:00 | 177.6ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,153,000 | 2026-07-29T18:22:25.9205815+00:00 | 127.5ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,168,000 | 2026-07-29T18:22:25.9450748+00:00 | 176.6ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,201,000 | 2026-07-29T18:22:25.9981248+00:00 | 144.1ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,317,000 | 2026-07-29T18:22:26.1415397+00:00 | 116.2ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,337,000 | 2026-07-29T18:22:26.162171+00:00 | 115.7ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 371,340,000 | 2026-07-29T18:22:26.1643544+00:00 | 111.4ms | GC pause | - | - | 557.4s / 815,719 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 410,472,000 | 2026-07-29T18:23:31.4493095+00:00 | 121.6ms | GC pause | - | - | 623.5s / 865,964 msg/s | Gen2 +0 / pause +88.4ms |
| Confluent | 410,766,000 | 2026-07-29T18:23:31.7901529+00:00 | 111.3ms | GC pause | - | - | 623.5s / 865,964 msg/s | Gen2 +0 / pause +88.4ms |
| Confluent | 410,779,000 | 2026-07-29T18:23:31.8075574+00:00 | 120.6ms | GC pause | - | - | 623.5s / 865,964 msg/s | Gen2 +0 / pause +88.4ms |
| Confluent | 411,165,000 | 2026-07-29T18:23:32.2568416+00:00 | 130.9ms | GC pause | - | - | 623.5s / 865,964 msg/s | Gen2 +0 / pause +88.4ms |
| Confluent | 411,199,000 | 2026-07-29T18:23:32.2900017+00:00 | 129.0ms | GC pause | - | - | 624.5s / 965,336 msg/s | Gen2 +0 / pause +176.8ms |
| Confluent | 411,239,000 | 2026-07-29T18:23:32.3279787+00:00 | 166.6ms | GC pause | - | - | 624.5s / 965,336 msg/s | Gen2 +0 / pause +176.8ms |
| Confluent | 411,249,000 | 2026-07-29T18:23:32.3349414+00:00 | 168.1ms | GC pause | - | - | 624.5s / 965,336 msg/s | Gen2 +0 / pause +176.8ms |
| Confluent | 412,542,000 | 2026-07-29T18:23:33.7297643+00:00 | 139.6ms | GC pause | - | - | 625.5s / 875,568 msg/s | Gen2 +0 / pause +88.8ms |
| Confluent | 412,612,000 | 2026-07-29T18:23:33.8107376+00:00 | 130.9ms | GC pause | - | - | 625.5s / 875,568 msg/s | Gen2 +0 / pause +88.8ms |
| Confluent | 412,635,000 | 2026-07-29T18:23:33.8344295+00:00 | 101.9ms | GC pause | - | - | 625.5s / 875,568 msg/s | Gen2 +0 / pause +88.8ms |
| Confluent | 415,129,000 | 2026-07-29T18:23:36.4365017+00:00 | 115.1ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,152,000 | 2026-07-29T18:23:36.4577319+00:00 | 129.3ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,166,000 | 2026-07-29T18:23:36.4701316+00:00 | 157.4ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,265,000 | 2026-07-29T18:23:36.580221+00:00 | 140.5ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,279,000 | 2026-07-29T18:23:36.5929354+00:00 | 129.5ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,282,000 | 2026-07-29T18:23:36.5946493+00:00 | 204.8ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,315,000 | 2026-07-29T18:23:36.6289478+00:00 | 114.4ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,316,000 | 2026-07-29T18:23:36.6296072+00:00 | 119.7ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,345,000 | 2026-07-29T18:23:36.6619531+00:00 | 124.7ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,366,000 | 2026-07-29T18:23:36.6832834+00:00 | 110.7ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,482,000 | 2026-07-29T18:23:36.8122765+00:00 | 178.8ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,532,000 | 2026-07-29T18:23:36.8685508+00:00 | 172.9ms | GC pause | - | - | 628.5s / 857,941 msg/s | Gen2 +0 / pause +77.2ms |
| Confluent | 415,862,000 | 2026-07-29T18:23:37.2917335+00:00 | 102.8ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +156.7ms |
| Confluent | 415,952,000 | 2026-07-29T18:23:37.3973169+00:00 | 115.9ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 416,316,000 | 2026-07-29T18:23:37.8641686+00:00 | 151.7ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 416,323,000 | 2026-07-29T18:23:37.8722851+00:00 | 122.0ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 416,377,000 | 2026-07-29T18:23:37.9352267+00:00 | 110.2ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 416,399,000 | 2026-07-29T18:23:37.9617041+00:00 | 116.6ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 416,400,000 | 2026-07-29T18:23:37.963076+00:00 | 114.9ms | GC pause | - | - | 629.5s / 771,015 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 418,000,000 | 2026-07-29T18:23:39.7957183+00:00 | 113.3ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,056,000 | 2026-07-29T18:23:39.8469247+00:00 | 192.0ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,079,000 | 2026-07-29T18:23:39.8712416+00:00 | 183.2ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,086,000 | 2026-07-29T18:23:39.8815652+00:00 | 178.4ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,092,000 | 2026-07-29T18:23:39.8878913+00:00 | 195.1ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,097,000 | 2026-07-29T18:23:39.8930078+00:00 | 130.0ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,103,000 | 2026-07-29T18:23:39.9032619+00:00 | 101.1ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,122,000 | 2026-07-29T18:23:39.951053+00:00 | 165.8ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,129,000 | 2026-07-29T18:23:39.9690447+00:00 | 142.0ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,172,000 | 2026-07-29T18:23:40.0363103+00:00 | 131.2ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,332,000 | 2026-07-29T18:23:40.2354308+00:00 | 106.1ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,342,000 | 2026-07-29T18:23:40.246233+00:00 | 144.1ms | GC pause | - | - | 631.5s / 858,036 msg/s | Gen2 +0 / pause +89.7ms |
| Confluent | 418,452,000 | 2026-07-29T18:23:40.3577382+00:00 | 117.9ms | GC pause | - | - | 632.5s / 851,321 msg/s | Gen2 +0 / pause +185.6ms |
| Confluent | 418,472,000 | 2026-07-29T18:23:40.3765003+00:00 | 108.4ms | GC pause | - | - | 632.5s / 851,321 msg/s | Gen2 +0 / pause +185.6ms |
| Confluent | 418,489,000 | 2026-07-29T18:23:40.4033046+00:00 | 149.9ms | GC pause | - | - | 632.5s / 851,321 msg/s | Gen2 +0 / pause +95.9ms |
| Confluent | 418,545,000 | 2026-07-29T18:23:40.4772776+00:00 | 104.2ms | GC pause | - | - | 632.5s / 851,321 msg/s | Gen2 +0 / pause +95.9ms |
| Confluent | 418,549,000 | 2026-07-29T18:23:40.4800116+00:00 | 103.7ms | GC pause | - | - | 632.5s / 851,321 msg/s | Gen2 +0 / pause +95.9ms |
| Confluent | 419,315,000 | 2026-07-29T18:23:41.3666243+00:00 | 123.5ms | GC pause | - | - | 633.5s / 862,002 msg/s | Gen2 +0 / pause +190.7ms |
| Confluent | 419,346,000 | 2026-07-29T18:23:41.410786+00:00 | 113.1ms | GC pause | - | - | 633.5s / 862,002 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 419,756,000 | 2026-07-29T18:23:41.9173308+00:00 | 112.2ms | GC pause | - | - | 633.5s / 862,002 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 419,785,000 | 2026-07-29T18:23:41.9499156+00:00 | 121.3ms | GC pause | - | - | 633.5s / 862,002 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 419,815,000 | 2026-07-29T18:23:41.9804535+00:00 | 122.1ms | GC pause | - | - | 633.5s / 862,002 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 419,895,000 | 2026-07-29T18:23:42.0680262+00:00 | 112.0ms | GC pause | - | - | 633.5s / 862,002 msg/s | Gen2 +0 / pause +94.8ms |
| Confluent | 420,676,000 | 2026-07-29T18:23:42.8687123+00:00 | 110.5ms | GC pause | - | - | 634.5s / 935,449 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 420,715,000 | 2026-07-29T18:23:42.9094369+00:00 | 111.8ms | GC pause | - | - | 634.5s / 935,449 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 420,832,000 | 2026-07-29T18:23:43.0601414+00:00 | 111.6ms | GC pause | - | - | 634.5s / 935,449 msg/s | Gen2 +0 / pause +101.0ms |
| Confluent | 421,423,000 | 2026-07-29T18:23:43.727266+00:00 | 163.8ms | GC pause | - | - | 635.5s / 871,300 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 421,557,000 | 2026-07-29T18:23:43.8532148+00:00 | 117.4ms | GC pause | - | - | 635.5s / 871,300 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 421,653,000 | 2026-07-29T18:23:43.9994438+00:00 | 165.8ms | GC pause | - | - | 635.5s / 871,300 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 421,670,000 | 2026-07-29T18:23:44.0195278+00:00 | 157.9ms | GC pause | - | - | 635.5s / 871,300 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 421,693,000 | 2026-07-29T18:23:44.0517079+00:00 | 135.2ms | GC pause | - | - | 635.5s / 871,300 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 422,356,000 | 2026-07-29T18:23:44.7773288+00:00 | 100.2ms | GC pause | - | - | 636.5s / 891,761 msg/s | Gen2 +0 / pause +73.3ms |
| Confluent | 423,402,000 | 2026-07-29T18:23:46.0269617+00:00 | 107.2ms | GC pause | - | - | 637.5s / 848,230 msg/s | Gen2 +0 / pause +93.3ms |
| Confluent | 423,475,000 | 2026-07-29T18:23:46.0953577+00:00 | 148.6ms | GC pause | - | - | 637.5s / 848,230 msg/s | Gen2 +0 / pause +93.3ms |
| Confluent | 423,559,000 | 2026-07-29T18:23:46.1688741+00:00 | 223.7ms | GC pause | - | - | 637.5s / 848,230 msg/s | Gen2 +0 / pause +93.3ms |
| Confluent | 423,609,000 | 2026-07-29T18:23:46.2226521+00:00 | 248.7ms | GC pause | - | - | 638.5s / 314,903 msg/s | Gen2 +0 / pause +282.8ms |
| Confluent | 423,676,000 | 2026-07-29T18:23:46.298854+00:00 | 264.6ms | GC pause | - | - | 638.5s / 314,903 msg/s | Gen2 +0 / pause +282.8ms |
| Confluent | 423,802,000 | 2026-07-29T18:23:46.5203757+00:00 | 105.3ms | GC pause | - | - | 638.5s / 314,903 msg/s | Gen2 +0 / pause +189.5ms |
| Confluent | 484,742,000 | 2026-07-29T18:25:26.2735921+00:00 | 120.9ms | GC pause | - | - | 737.6s / 882,078 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 484,752,000 | 2026-07-29T18:25:26.2838014+00:00 | 114.8ms | GC pause | - | - | 737.6s / 882,078 msg/s | Gen2 +0 / pause +89.8ms |
| Confluent | 485,304,000 | 2026-07-29T18:25:26.8987234+00:00 | 100.1ms | GC pause | - | - | 738.6s / 900,120 msg/s | Gen2 +0 / pause +100.5ms |
| Confluent | 485,743,000 | 2026-07-29T18:25:27.4209135+00:00 | 118.6ms | GC pause | - | - | 739.6s / 860,410 msg/s | Gen2 +0 / pause +187.8ms |
| Confluent | 494,838,000 | 2026-07-29T18:25:36.8157186+00:00 | 106.0ms | GC pause | - | - | 748.6s / 955,449 msg/s | Gen2 +0 / pause +102.4ms |
| Confluent | 495,913,000 | 2026-07-29T18:25:37.942147+00:00 | 128.0ms | GC pause | - | - | 749.6s / 933,315 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 495,978,000 | 2026-07-29T18:25:38.0347117+00:00 | 110.0ms | GC pause | - | - | 749.6s / 933,315 msg/s | Gen2 +0 / pause +102.1ms |
| Confluent | 498,195,000 | 2026-07-29T18:25:40.3190948+00:00 | 125.6ms | GC pause | - | - | 751.6s / 966,567 msg/s | Gen2 +0 / pause +103.1ms |
| Confluent | 498,316,000 | 2026-07-29T18:25:40.4394246+00:00 | 103.5ms | GC pause | - | - | 752.6s / 916,888 msg/s | Gen2 +0 / pause +195.9ms |
| Confluent | 506,417,000 | 2026-07-29T18:25:52.3090186+00:00 | 111.1ms | GC pause | - | - | 763.6s / 857,006 msg/s | Gen2 +0 / pause +86.2ms |
| Confluent | 506,433,000 | 2026-07-29T18:25:52.3251368+00:00 | 103.1ms | GC pause | - | - | 763.6s / 857,006 msg/s | Gen2 +0 / pause +86.2ms |
| Confluent | 513,578,000 | 2026-07-29T18:26:05.2591513+00:00 | 189.5ms | GC pause | - | - | 776.6s / 716,548 msg/s | Gen2 +0 / pause +136.0ms |
| Confluent | 513,593,000 | 2026-07-29T18:26:05.282584+00:00 | 157.4ms | GC pause | - | - | 776.6s / 716,548 msg/s | Gen2 +0 / pause +136.0ms |
| Confluent | 514,487,000 | 2026-07-29T18:26:06.3651547+00:00 | 117.5ms | GC pause | - | - | 777.6s / 900,925 msg/s | Gen2 +0 / pause +94.7ms |
| Confluent | 532,382,000 | 2026-07-29T18:26:39.3526302+00:00 | 103.5ms | GC pause | - | - | 810.6s / 596,203 msg/s | Gen2 +0 / pause +119.2ms |
| Confluent | 544,057,000 | 2026-07-29T18:26:57.7150191+00:00 | 105.5ms | GC pause | - | - | 829.6s / 924,439 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 544,137,000 | 2026-07-29T18:26:57.7979926+00:00 | 151.8ms | GC pause | - | - | 829.6s / 924,439 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 544,217,000 | 2026-07-29T18:26:57.8875873+00:00 | 126.8ms | GC pause | - | - | 829.6s / 924,439 msg/s | Gen2 +0 / pause +81.0ms |
| Confluent | 544,686,000 | 2026-07-29T18:26:58.4289407+00:00 | 227.9ms | GC pause | - | - | 830.6s / 787,299 msg/s | Gen2 +0 / pause +150.3ms |
| Confluent | 544,716,000 | 2026-07-29T18:26:58.4524849+00:00 | 233.8ms | GC pause | - | - | 830.6s / 787,299 msg/s | Gen2 +0 / pause +150.3ms |
| Confluent | 544,765,000 | 2026-07-29T18:26:58.4939405+00:00 | 228.4ms | GC pause | - | - | 830.6s / 787,299 msg/s | Gen2 +0 / pause +150.3ms |
| Confluent | 544,796,000 | 2026-07-29T18:26:58.523667+00:00 | 223.7ms | GC pause | - | - | 830.6s / 787,299 msg/s | Gen2 +0 / pause +150.3ms |
| Confluent | 544,952,000 | 2026-07-29T18:26:58.7012722+00:00 | 231.1ms | GC pause | - | - | 830.6s / 787,299 msg/s | Gen2 +0 / pause +69.3ms |
| Confluent | 548,854,000 | 2026-07-29T18:27:05.5420596+00:00 | 236.1ms | GC pause | - | - | 837.6s / 549,709 msg/s | Gen2 +0 / pause +179.6ms |
| Confluent | 564,794,000 | 2026-07-29T18:27:34.0051805+00:00 | 143.8ms | GC pause | - | - | 865.7s / 541,062 msg/s | Gen2 +0 / pause +94.4ms |
| Confluent | 576,663,000 | 2026-07-29T18:27:54.4150348+00:00 | 117.7ms | GC pause | - | - | 885.7s / 950,284 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 580,294,000 | 2026-07-29T18:27:59.2747373+00:00 | 131.6ms | GC pause | - | - | 890.7s / 896,323 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 580,304,000 | 2026-07-29T18:27:59.2909433+00:00 | 138.0ms | GC pause | - | - | 890.7s / 896,323 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 580,344,000 | 2026-07-29T18:27:59.3323863+00:00 | 147.0ms | GC pause | - | - | 890.7s / 896,323 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 580,534,000 | 2026-07-29T18:27:59.5809552+00:00 | 113.6ms | GC pause | - | - | 891.7s / 514,367 msg/s | Gen2 +0 / pause +169.5ms |
| Confluent | 581,963,000 | 2026-07-29T18:28:01.6025832+00:00 | 130.4ms | GC pause | - | - | 893.7s / 835,665 msg/s | Gen2 +0 / pause +181.7ms |
| Confluent | 581,967,000 | 2026-07-29T18:28:01.6104086+00:00 | 227.6ms | GC pause | - | - | 893.7s / 835,665 msg/s | Gen2 +0 / pause +181.7ms |
| Confluent | 582,013,000 | 2026-07-29T18:28:01.6612627+00:00 | 137.6ms | GC pause | - | - | 893.7s / 835,665 msg/s | Gen2 +0 / pause +86.9ms |
| Confluent | 582,021,000 | 2026-07-29T18:28:01.6692497+00:00 | 249.3ms | GC pause | - | - | 893.7s / 835,665 msg/s | Gen2 +0 / pause +86.9ms |
| Confluent | 583,575,000 | 2026-07-29T18:28:03.3932961+00:00 | 108.5ms | GC pause | - | - | 894.7s / 921,999 msg/s | Gen2 +0 / pause +73.5ms |
| Dekaf (3conn) | 31,000 | 2026-07-29T18:28:23.2043517+00:00 | 185.2ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 32,000 | 2026-07-29T18:28:23.205285+00:00 | 184.3ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,000 | 2026-07-29T18:28:23.2211325+00:00 | 179.8ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 42,000 | 2026-07-29T18:28:23.2229538+00:00 | 178.0ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,000 | 2026-07-29T18:28:23.23902+00:00 | 162.0ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 52,000 | 2026-07-29T18:28:23.2403837+00:00 | 165.1ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 61,000 | 2026-07-29T18:28:23.2582129+00:00 | 149.0ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 62,000 | 2026-07-29T18:28:23.259792+00:00 | 147.4ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,000 | 2026-07-29T18:28:23.2765921+00:00 | 136.7ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 72,000 | 2026-07-29T18:28:23.2787423+00:00 | 134.6ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 81,000 | 2026-07-29T18:28:23.2946936+00:00 | 123.2ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 82,000 | 2026-07-29T18:28:23.296232+00:00 | 129.7ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 203,000 | 2026-07-29T18:28:23.5604917+00:00 | 114.2ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 209,000 | 2026-07-29T18:28:23.5712641+00:00 | 127.9ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 213,000 | 2026-07-29T18:28:23.5796247+00:00 | 119.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 219,000 | 2026-07-29T18:28:23.5944417+00:00 | 124.1ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 223,000 | 2026-07-29T18:28:23.6044883+00:00 | 111.4ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 229,000 | 2026-07-29T18:28:23.6142112+00:00 | 103.3ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 233,000 | 2026-07-29T18:28:23.6229036+00:00 | 112.3ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 239,000 | 2026-07-29T18:28:23.6309259+00:00 | 116.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 243,000 | 2026-07-29T18:28:23.636822+00:00 | 110.6ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 249,000 | 2026-07-29T18:28:23.6454346+00:00 | 119.7ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 250,000 | 2026-07-29T18:28:23.6466217+00:00 | 100.8ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 253,000 | 2026-07-29T18:28:23.6504544+00:00 | 127.2ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 259,000 | 2026-07-29T18:28:23.6604688+00:00 | 119.2ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 260,000 | 2026-07-29T18:28:23.6617349+00:00 | 134.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 263,000 | 2026-07-29T18:28:23.6657947+00:00 | 124.3ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 264,000 | 2026-07-29T18:28:23.6697417+00:00 | 125.4ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 266,000 | 2026-07-29T18:28:23.6729903+00:00 | 122.1ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 269,000 | 2026-07-29T18:28:23.6782669+00:00 | 120.6ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 270,000 | 2026-07-29T18:28:23.6843406+00:00 | 125.1ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 280,000 | 2026-07-29T18:28:23.7220897+00:00 | 103.8ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 283,000 | 2026-07-29T18:28:23.7327936+00:00 | 156.7ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 289,000 | 2026-07-29T18:28:23.7507497+00:00 | 175.9ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 293,000 | 2026-07-29T18:28:23.7589979+00:00 | 170.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 299,000 | 2026-07-29T18:28:23.7745091+00:00 | 157.1ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 303,000 | 2026-07-29T18:28:23.7799506+00:00 | 151.6ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 309,000 | 2026-07-29T18:28:23.7954698+00:00 | 141.8ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 313,000 | 2026-07-29T18:28:23.8006552+00:00 | 136.7ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 319,000 | 2026-07-29T18:28:23.8071329+00:00 | 135.0ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 323,000 | 2026-07-29T18:28:23.8122607+00:00 | 143.0ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 329,000 | 2026-07-29T18:28:23.821244+00:00 | 135.2ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 333,000 | 2026-07-29T18:28:23.8273172+00:00 | 140.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 339,000 | 2026-07-29T18:28:23.836572+00:00 | 131.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 343,000 | 2026-07-29T18:28:23.8524009+00:00 | 115.7ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 353,000 | 2026-07-29T18:28:23.8957734+00:00 | 104.6ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 361,000 | 2026-07-29T18:28:23.919131+00:00 | 193.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 362,000 | 2026-07-29T18:28:23.9203826+00:00 | 191.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 363,000 | 2026-07-29T18:28:23.9227654+00:00 | 115.6ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 369,000 | 2026-07-29T18:28:23.9363804+00:00 | 108.8ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 371,000 | 2026-07-29T18:28:23.941033+00:00 | 186.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 372,000 | 2026-07-29T18:28:23.9425937+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 373,000 | 2026-07-29T18:28:23.9437223+00:00 | 101.5ms | throughput collapse | - | - | 1.0s / 420,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 381,000 | 2026-07-29T18:28:23.9618929+00:00 | 223.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 382,000 | 2026-07-29T18:28:23.9643748+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 389,000 | 2026-07-29T18:28:23.97636+00:00 | 134.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 391,000 | 2026-07-29T18:28:23.9811478+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 392,000 | 2026-07-29T18:28:23.9824277+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 393,000 | 2026-07-29T18:28:23.984903+00:00 | 143.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 399,000 | 2026-07-29T18:28:23.9973013+00:00 | 150.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 401,000 | 2026-07-29T18:28:24.0017875+00:00 | 206.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 402,000 | 2026-07-29T18:28:24.0037334+00:00 | 206.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 403,000 | 2026-07-29T18:28:24.0060933+00:00 | 141.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 409,000 | 2026-07-29T18:28:24.0454218+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 411,000 | 2026-07-29T18:28:24.0504407+00:00 | 173.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 412,000 | 2026-07-29T18:28:24.0516537+00:00 | 172.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 413,000 | 2026-07-29T18:28:24.0530098+00:00 | 127.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 419,000 | 2026-07-29T18:28:24.0671363+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 421,000 | 2026-07-29T18:28:24.1111704+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 422,000 | 2026-07-29T18:28:24.1125532+00:00 | 120.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 431,000 | 2026-07-29T18:28:24.1344992+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 432,000 | 2026-07-29T18:28:24.1358822+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 529,000 | 2026-07-29T18:28:24.3518852+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 631,000 | 2026-07-29T18:28:24.51938+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 632,000 | 2026-07-29T18:28:24.5213158+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 641,000 | 2026-07-29T18:28:24.5299603+00:00 | 136.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 642,000 | 2026-07-29T18:28:24.5317714+00:00 | 134.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 651,000 | 2026-07-29T18:28:24.5391182+00:00 | 127.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 652,000 | 2026-07-29T18:28:24.5396525+00:00 | 126.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 661,000 | 2026-07-29T18:28:24.5477387+00:00 | 158.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 662,000 | 2026-07-29T18:28:24.5484551+00:00 | 157.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 671,000 | 2026-07-29T18:28:24.55754+00:00 | 165.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 672,000 | 2026-07-29T18:28:24.5582918+00:00 | 164.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 681,000 | 2026-07-29T18:28:24.5700462+00:00 | 156.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 682,000 | 2026-07-29T18:28:24.5717047+00:00 | 155.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 691,000 | 2026-07-29T18:28:24.5955303+00:00 | 142.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 692,000 | 2026-07-29T18:28:24.5962431+00:00 | 156.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 701,000 | 2026-07-29T18:28:24.6061595+00:00 | 155.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 702,000 | 2026-07-29T18:28:24.6234035+00:00 | 138.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 711,000 | 2026-07-29T18:28:24.6416411+00:00 | 133.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 712,000 | 2026-07-29T18:28:24.6434346+00:00 | 131.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 721,000 | 2026-07-29T18:28:24.6666852+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 722,000 | 2026-07-29T18:28:24.667822+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 731,000 | 2026-07-29T18:28:24.682676+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 732,000 | 2026-07-29T18:28:24.6830895+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 842,000 | 2026-07-29T18:28:24.8453105+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 588,836 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 950,000 | 2026-07-29T18:28:24.9929283+00:00 | 131.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 960,000 | 2026-07-29T18:28:24.9993419+00:00 | 135.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 970,000 | 2026-07-29T18:28:25.0064758+00:00 | 146.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 980,000 | 2026-07-29T18:28:25.0114068+00:00 | 169.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 984,000 | 2026-07-29T18:28:25.013877+00:00 | 163.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 986,000 | 2026-07-29T18:28:25.0320246+00:00 | 148.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 990,000 | 2026-07-29T18:28:25.0338445+00:00 | 150.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 994,000 | 2026-07-29T18:28:25.0379342+00:00 | 146.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 996,000 | 2026-07-29T18:28:25.0399845+00:00 | 144.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,000,000 | 2026-07-29T18:28:25.0437655+00:00 | 145.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,004,000 | 2026-07-29T18:28:25.0786771+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,006,000 | 2026-07-29T18:28:25.0792756+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,080,000 | 2026-07-29T18:28:25.2202841+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 844,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,884,000 | 2026-07-29T18:28:28.0992685+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 982,497 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,886,000 | 2026-07-29T18:28:28.1014533+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 982,497 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,914,000 | 2026-07-29T18:28:29.1473139+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 851,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,661,000 | 2026-07-29T18:28:32.3246727+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 737,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,662,000 | 2026-07-29T18:28:32.3250656+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 737,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,671,000 | 2026-07-29T18:28:32.3404664+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 737,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,672,000 | 2026-07-29T18:28:32.3410248+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 737,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,205,000 | 2026-07-29T18:28:33.0907609+00:00 | 119.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 878,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,208,000 | 2026-07-29T18:28:33.0933252+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 878,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,215,000 | 2026-07-29T18:28:33.1033205+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 878,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,218,000 | 2026-07-29T18:28:33.1069412+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 878,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,225,000 | 2026-07-29T18:28:33.1147232+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 878,862 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,789,000 | 2026-07-29T18:28:36.1468564+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 876,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,793,000 | 2026-07-29T18:28:36.1492743+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 876,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,803,000 | 2026-07-29T18:28:36.1559656+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 876,158 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,647,000 | 2026-07-29T18:28:37.1282146+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 881,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,657,000 | 2026-07-29T18:28:37.1345973+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 881,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,445,000 | 2026-07-29T18:28:43.657185+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,448,000 | 2026-07-29T18:28:43.6602898+00:00 | 115.3ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,455,000 | 2026-07-29T18:28:43.6639798+00:00 | 124.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,458,000 | 2026-07-29T18:28:43.6675123+00:00 | 127.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,465,000 | 2026-07-29T18:28:43.6721556+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,467,000 | 2026-07-29T18:28:43.6730566+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 17,468,000 | 2026-07-29T18:28:43.673938+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 684,010 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,177,000 | 2026-07-29T18:28:49.0999393+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 857,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,187,000 | 2026-07-29T18:28:49.1119282+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 857,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,197,000 | 2026-07-29T18:28:49.1251492+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 857,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,207,000 | 2026-07-29T18:28:49.1318452+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 857,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,217,000 | 2026-07-29T18:28:49.1421245+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 857,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 73,584,000 | 2026-07-29T18:29:57.1102974+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 73,586,000 | 2026-07-29T18:29:57.1114589+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 73,594,000 | 2026-07-29T18:29:57.1166393+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 73,596,000 | 2026-07-29T18:29:57.1182718+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 73,600,000 | 2026-07-29T18:29:57.122474+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 74,305,000 | 2026-07-29T18:29:58.0996588+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 74,308,000 | 2026-07-29T18:29:58.1022875+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 739,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,465,000 | 2026-07-29T18:30:26.8195882+00:00 | 105.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 2:capacity/failed, 1:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,468,000 | 2026-07-29T18:30:26.8226615+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 1:capacity/started, 2:capacity/failed, 1:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,475,000 | 2026-07-29T18:30:26.8329415+00:00 | 107.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,477,000 | 2026-07-29T18:30:26.8363582+00:00 | 112.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,478,000 | 2026-07-29T18:30:26.8373003+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,485,000 | 2026-07-29T18:30:26.8431346+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,487,000 | 2026-07-29T18:30:26.8467547+00:00 | 111.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,488,000 | 2026-07-29T18:30:26.8472643+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 98,497,000 | 2026-07-29T18:30:26.8568751+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 3:capacity/started, 2:capacity/failed, 1:capacity/succeeded, 3:capacity/succeeded | - | 124.1s / 619,389 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 221,997,000 | 2026-07-29T18:32:42.1192519+00:00 | 112.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 259.2s / 976,470 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 239,614,000 | 2026-07-29T18:32:59.5937652+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 277.2s / 780,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 239,616,000 | 2026-07-29T18:32:59.5962938+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 277.2s / 780,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 239,650,000 | 2026-07-29T18:32:59.6246868+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 277.2s / 780,684 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 268,039,000 | 2026-07-29T18:33:31.6105943+00:00 | 126.5ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,041,000 | 2026-07-29T18:33:31.6155739+00:00 | 125.8ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,042,000 | 2026-07-29T18:33:31.615862+00:00 | 125.5ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,043,000 | 2026-07-29T18:33:31.6161494+00:00 | 132.2ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,048,000 | 2026-07-29T18:33:31.6186566+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,049,000 | 2026-07-29T18:33:31.6190616+00:00 | 131.1ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,051,000 | 2026-07-29T18:33:31.6197912+00:00 | 141.9ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,052,000 | 2026-07-29T18:33:31.6204601+00:00 | 147.7ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,053,000 | 2026-07-29T18:33:31.6209565+00:00 | 132.2ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,055,000 | 2026-07-29T18:33:31.6215485+00:00 | 131.5ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,058,000 | 2026-07-29T18:33:31.6230892+00:00 | 130.0ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,059,000 | 2026-07-29T18:33:31.6235693+00:00 | 153.1ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,061,000 | 2026-07-29T18:33:31.6243078+00:00 | 185.3ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,062,000 | 2026-07-29T18:33:31.6246325+00:00 | 185.0ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,063,000 | 2026-07-29T18:33:31.6253206+00:00 | 151.4ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 268,065,000 | 2026-07-29T18:33:31.632277+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 309.3s / 749,794 msg/s | Gen2 +0 / pause +2.1ms |
| Dekaf (3conn) | 297,897,000 | 2026-07-29T18:34:04.1136187+00:00 | 116.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed, 1:capacity/failed | - | 341.3s / 833,517 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 297,905,000 | 2026-07-29T18:34:04.1214742+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed, 1:capacity/failed | - | 341.3s / 833,517 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 297,907,000 | 2026-07-29T18:34:04.1232767+00:00 | 106.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed, 1:capacity/failed | - | 341.3s / 833,517 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 297,908,000 | 2026-07-29T18:34:04.1239968+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed, 1:capacity/failed | - | 341.3s / 833,517 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 297,915,000 | 2026-07-29T18:34:04.1290895+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed, 1:capacity/failed | - | 341.3s / 833,517 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 495,220,000 | 2026-07-29T18:37:53.9848479+00:00 | 104.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 571.5s / 768,695 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 495,240,000 | 2026-07-29T18:37:54.0098228+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 571.5s / 768,695 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 586,629,000 | 2026-07-29T18:39:42.6117058+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 680.6s / 717,508 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*6,083 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.75x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.56x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,270,779 | 1,268,858–1,272,703 | 1.15 | 1.14x |
| Confluent | 2 | 1,114,270 | 1,108,578–1,119,992 | 1.60 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 1.13 | 1140.26 | 1,240,022 | 1,272,703 | -16.0% | -1.29% | 1182.58 | 1,240,022 | 0 | 1.40 |
| Dekaf (dekaf-first) | 1.17 | 1192.91 | 1,247,431 | 1,268,858 | -14.7% | -1.37% | 1189.64 | 1,247,431 | 0 | 1.45 |
| Confluent (dekaf-first) | 1.55 | - | 1,077,425 | 1,119,992 | +15.2% | +1.51% | 1027.51 | 1,077,425 | 0 | 1.67 |
| Confluent (confluent-first) | 1.64 | - | 1,053,863 | 1,108,578 | -11.2% | -0.80% | 1005.04 | 1,053,863 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,108,443 | 1231.58 | 1000.87 KB |
| Dekaf | 1 | 1,096,828 | 1218.68 | 1017.51 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:22.4966169+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 469,504 msg/s |
| Dekaf | 2026-07-29T17:58:40.5035982+00:00 | 1 | 16.0 MiB / 14.7 MiB | 1502.1 MB/s | 0/0 | 17,937 | 18.0s / 1,321,172 msg/s |
| Dekaf | 2026-07-29T17:58:58.5067641+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1523.8 MB/s | 0/0 | 41,960 | 36.0s / 1,401,702 msg/s |
| Dekaf | 2026-07-29T17:59:17.5145124+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1523.8 MB/s | 1/0 | 72,277 | 55.0s / 1,417,583 msg/s |
| Dekaf | 2026-07-29T17:59:35.5211174+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1577.2 MB/s | 1/0 | 106,808 | 73.0s / 1,477,590 msg/s |
| Dekaf | 2026-07-29T17:59:53.5225589+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1732.3 MB/s | 1/1 | 145,940 | 91.0s / 1,462,464 msg/s |
| Dekaf | 2026-07-29T18:00:11.5286889+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1732.3 MB/s | 1/1 | 176,657 | 109.0s / 1,437,749 msg/s |
| Dekaf | 2026-07-29T18:00:29.5383928+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1732.3 MB/s | 1/1 | 208,242 | 127.0s / 1,296,402 msg/s |
| Dekaf | 2026-07-29T18:00:47.547104+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1732.3 MB/s | 1/1 | 243,348 | 145.0s / 1,530,712 msg/s |
| Dekaf | 2026-07-29T18:01:06.5524974+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 1/1 | 279,979 | 164.1s / 1,495,958 msg/s |
| Dekaf | 2026-07-29T18:01:24.5562796+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/1 | 311,299 | 182.1s / 1,425,075 msg/s |
| Dekaf | 2026-07-29T18:01:42.5609969+00:00 | 1 | 15.0 MiB / 14.8 MiB | 1732.3 MB/s | 2/1 | 337,949 | 200.1s / 1,371,849 msg/s |
| Dekaf | 2026-07-29T18:02:00.5654893+00:00 | 1 | 15.0 MiB / 13.8 MiB | 1732.3 MB/s | 2/2 | 365,722 | 218.1s / 1,374,448 msg/s |
| Dekaf | 2026-07-29T18:02:18.5720505+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1732.3 MB/s | 2/2 | 395,258 | 236.1s / 1,478,004 msg/s |
| Dekaf | 2026-07-29T18:02:36.5758884+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/2 | 424,481 | 254.1s / 1,293,570 msg/s |
| Dekaf | 2026-07-29T18:02:55.5853+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1732.3 MB/s | 2/2 | 456,437 | 273.1s / 1,384,400 msg/s |
| Dekaf | 2026-07-29T18:03:13.5879014+00:00 | 1 | 15.0 MiB / 14.1 MiB | 1732.3 MB/s | 2/3 | 487,426 | 291.1s / 1,371,628 msg/s |
| Dekaf | 2026-07-29T18:03:31.5940531+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1732.3 MB/s | 2/3 | 514,138 | 309.1s / 1,458,669 msg/s |
| Dekaf | 2026-07-29T18:03:49.6005206+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/3 | 539,633 | 327.1s / 1,460,461 msg/s |
| Dekaf | 2026-07-29T18:04:07.6143885+00:00 | 1 | 15.0 MiB / 14.1 MiB | 1732.3 MB/s | 2/3 | 565,634 | 345.1s / 1,148,288 msg/s |
| Dekaf | 2026-07-29T18:04:25.6285745+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1732.3 MB/s | 2/3 | 590,360 | 363.1s / 1,337,436 msg/s |
| Dekaf | 2026-07-29T18:04:44.6393852+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/3 | 611,665 | 382.1s / 1,284,783 msg/s |
| Dekaf | 2026-07-29T18:05:02.6476348+00:00 | 1 | 15.0 MiB / 11.9 MiB | 1732.3 MB/s | 2/3 | 628,804 | 400.1s / 1,177,180 msg/s |
| Dekaf | 2026-07-29T18:05:20.6523774+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1732.3 MB/s | 2/3 | 650,038 | 418.1s / 1,318,649 msg/s |
| Dekaf | 2026-07-29T18:05:38.6587451+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1732.3 MB/s | 2/4 | 671,432 | 436.1s / 1,168,697 msg/s |
| Dekaf | 2026-07-29T18:05:56.6664966+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/4 | 698,044 | 454.2s / 1,303,078 msg/s |
| Dekaf | 2026-07-29T18:06:14.6703701+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1732.3 MB/s | 2/4 | 720,630 | 472.2s / 1,178,235 msg/s |
| Dekaf | 2026-07-29T18:06:33.6796265+00:00 | 1 | 15.0 MiB / 12.9 MiB | 1732.3 MB/s | 2/4 | 739,403 | 491.2s / 1,167,342 msg/s |
| Dekaf | 2026-07-29T18:06:51.6841943+00:00 | 1 | 15.0 MiB / 13.2 MiB | 1732.3 MB/s | 2/4 | 761,198 | 509.2s / 1,218,646 msg/s |
| Dekaf | 2026-07-29T18:07:09.6893851+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/4 | 785,248 | 527.2s / 1,128,893 msg/s |
| Dekaf | 2026-07-29T18:07:27.7003542+00:00 | 1 | 15.0 MiB / 14.1 MiB | 1732.3 MB/s | 2/4 | 812,991 | 545.2s / 1,062,955 msg/s |
| Dekaf | 2026-07-29T18:07:45.7114718+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1732.3 MB/s | 2/4 | 833,447 | 563.2s / 1,007,946 msg/s |
| Dekaf | 2026-07-29T18:08:03.7185902+00:00 | 1 | 15.0 MiB / 13.3 MiB | 1732.3 MB/s | 2/4 | 855,146 | 581.2s / 1,073,296 msg/s |
| Dekaf | 2026-07-29T18:08:22.7263227+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1732.3 MB/s | 2/4 | 872,222 | 600.2s / 1,017,933 msg/s |
| Dekaf | 2026-07-29T18:08:40.7297373+00:00 | 1 | 15.0 MiB / 14.6 MiB | 1732.3 MB/s | 2/4 | 892,281 | 618.2s / 1,038,147 msg/s |
| Dekaf | 2026-07-29T18:08:58.7411803+00:00 | 1 | 15.0 MiB / 11.2 MiB | 1732.3 MB/s | 2/4 | 913,008 | 636.2s / 975,648 msg/s |
| Dekaf | 2026-07-29T18:09:16.7500984+00:00 | 1 | 15.0 MiB / 13.2 MiB | 1732.3 MB/s | 2/4 | 931,070 | 654.2s / 1,048,948 msg/s |
| Dekaf | 2026-07-29T18:09:34.7568501+00:00 | 1 | 13.0 MiB / 11.9 MiB | 1732.3 MB/s | 2/4 | 952,377 | 672.2s / 1,062,635 msg/s |
| Dekaf | 2026-07-29T18:09:53.7701047+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1732.3 MB/s | 3/4 | 981,994 | 691.2s / 1,094,425 msg/s |
| Dekaf | 2026-07-29T18:10:11.7833833+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1732.3 MB/s | 3/5 | 1,005,839 | 709.2s / 1,079,648 msg/s |
| Dekaf | 2026-07-29T18:10:29.788662+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1732.3 MB/s | 3/5 | 1,028,884 | 727.2s / 1,086,259 msg/s |
| Dekaf | 2026-07-29T18:10:47.793512+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1732.3 MB/s | 3/5 | 1,049,110 | 745.3s / 1,026,346 msg/s |
| Dekaf | 2026-07-29T18:11:05.8033491+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1732.3 MB/s | 3/5 | 1,074,970 | 763.3s / 1,336,061 msg/s |
| Dekaf | 2026-07-29T18:11:23.8077663+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1732.3 MB/s | 3/6 | 1,110,863 | 781.3s / 1,324,158 msg/s |
| Dekaf | 2026-07-29T18:11:42.8098738+00:00 | 1 | 13.0 MiB / 11.8 MiB | 1732.3 MB/s | 3/6 | 1,140,660 | 800.3s / 1,282,178 msg/s |
| Dekaf | 2026-07-29T18:12:00.8157579+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1732.3 MB/s | 3/6 | 1,178,688 | 818.3s / 1,427,408 msg/s |
| Dekaf | 2026-07-29T18:12:18.823949+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1732.3 MB/s | 3/6 | 1,220,039 | 836.3s / 1,346,428 msg/s |
| Dekaf | 2026-07-29T18:12:36.8299377+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1732.3 MB/s | 3/6 | 1,261,993 | 854.3s / 1,416,328 msg/s |
| Dekaf | 2026-07-29T18:12:54.8371543+00:00 | 1 | 13.0 MiB / 12.2 MiB | 1732.3 MB/s | 3/6 | 1,302,724 | 872.3s / 1,286,803 msg/s |
| Dekaf | 2026-07-29T18:13:12.8383464+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1732.3 MB/s | 3/6 | 1,336,877 | 890.3s / 1,154,805 msg/s |
| Dekaf | 2026-07-29T18:43:32.8555127+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1554.3 MB/s | 0/0 | 10,948 | 9.0s / 1,451,291 msg/s |
| Dekaf | 2026-07-29T18:43:50.8671066+00:00 | 1 | 16.0 MiB / 14.6 MiB | 1607.9 MB/s | 0/0 | 35,570 | 27.0s / 1,495,096 msg/s |
| Dekaf | 2026-07-29T18:44:08.8809072+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1622.3 MB/s | 0/0 | 65,064 | 45.0s / 1,399,070 msg/s |
| Dekaf | 2026-07-29T18:44:26.8929841+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1622.3 MB/s | 1/0 | 101,865 | 63.0s / 1,505,945 msg/s |
| Dekaf | 2026-07-29T18:44:44.9014528+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1629.2 MB/s | 1/0 | 138,424 | 81.0s / 1,508,004 msg/s |
| Dekaf | 2026-07-29T18:45:02.9063529+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/0 | 179,170 | 99.0s / 1,459,362 msg/s |
| Dekaf | 2026-07-29T18:45:21.9131948+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/0 | 223,609 | 118.0s / 1,457,543 msg/s |
| Dekaf | 2026-07-29T18:45:39.9202759+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/1 | 255,042 | 136.1s / 1,168,694 msg/s |
| Dekaf | 2026-07-29T18:45:57.9280108+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1629.2 MB/s | 2/1 | 294,067 | 154.1s / 1,317,292 msg/s |
| Dekaf | 2026-07-29T18:46:15.9313236+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1629.2 MB/s | 2/1 | 332,718 | 172.1s / 1,272,703 msg/s |
| Dekaf | 2026-07-29T18:46:33.9358059+00:00 | 1 | 12.0 MiB / 4.1 MiB | 1629.2 MB/s | 2/1 | 369,817 | 190.1s / 1,255,168 msg/s |
| Dekaf | 2026-07-29T18:46:51.939835+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1629.2 MB/s | 2/1 | 407,495 | 208.1s / 1,282,889 msg/s |
| Dekaf | 2026-07-29T18:47:10.9453404+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/2 | 445,011 | 227.1s / 1,275,996 msg/s |
| Dekaf | 2026-07-29T18:47:28.9518561+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1629.2 MB/s | 2/2 | 481,542 | 245.1s / 1,209,677 msg/s |
| Dekaf | 2026-07-29T18:47:46.9565139+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/2 | 516,167 | 263.1s / 1,323,370 msg/s |
| Dekaf | 2026-07-29T18:48:04.962681+00:00 | 1 | 12.0 MiB / 10.2 MiB | 1629.2 MB/s | 2/2 | 548,684 | 281.1s / 1,302,892 msg/s |
| Dekaf | 2026-07-29T18:48:22.9681957+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1629.2 MB/s | 2/2 | 580,252 | 299.1s / 1,209,552 msg/s |
| Dekaf | 2026-07-29T18:48:41.9706716+00:00 | 1 | 12.0 MiB / 1.4 MiB | 1629.2 MB/s | 2/2 | 609,826 | 318.1s / 1,261,261 msg/s |
| Dekaf | 2026-07-29T18:48:59.9765922+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1629.2 MB/s | 2/2 | 635,540 | 336.1s / 1,259,021 msg/s |
| Dekaf | 2026-07-29T18:49:17.9825538+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1629.2 MB/s | 2/3 | 663,417 | 354.1s / 1,352,019 msg/s |
| Dekaf | 2026-07-29T18:49:35.9859945+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/3 | 684,101 | 372.1s / 919,845 msg/s |
| Dekaf | 2026-07-29T18:49:53.9936866+00:00 | 1 | 10.0 MiB / 1.7 MiB | 1629.2 MB/s | 2/3 | 690,621 | 390.1s / 693,311 msg/s |
| Dekaf | 2026-07-29T18:50:11.9984887+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1629.2 MB/s | 2/4 | 700,595 | 408.1s / 898,519 msg/s |
| Dekaf | 2026-07-29T18:50:31.0123962+00:00 | 1 | 12.0 MiB / 5.6 MiB | 1629.2 MB/s | 2/4 | 713,927 | 427.1s / 699,927 msg/s |
| Dekaf | 2026-07-29T18:50:49.0194043+00:00 | 1 | 12.0 MiB / 9.9 MiB | 1629.2 MB/s | 2/4 | 720,604 | 445.2s / 800,126 msg/s |
| Dekaf | 2026-07-29T18:51:07.0254857+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1629.2 MB/s | 2/4 | 746,815 | 463.2s / 1,259,989 msg/s |
| Dekaf | 2026-07-29T18:51:25.0348677+00:00 | 1 | 12.0 MiB / 10.0 MiB | 1629.2 MB/s | 2/4 | 783,836 | 481.2s / 1,477,301 msg/s |
| Dekaf | 2026-07-29T18:51:43.0376435+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/4 | 824,664 | 499.2s / 1,459,906 msg/s |
| Dekaf | 2026-07-29T18:52:01.0460513+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1629.2 MB/s | 2/4 | 867,681 | 517.2s / 1,487,153 msg/s |
| Dekaf | 2026-07-29T18:52:20.0539654+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/4 | 912,562 | 536.2s / 1,458,571 msg/s |
| Dekaf | 2026-07-29T18:52:38.0591249+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1629.2 MB/s | 2/4 | 949,124 | 554.2s / 1,427,469 msg/s |
| Dekaf | 2026-07-29T18:52:56.0664326+00:00 | 1 | 12.0 MiB / 3.9 MiB | 1629.2 MB/s | 2/4 | 987,565 | 572.2s / 1,375,707 msg/s |
| Dekaf | 2026-07-29T18:53:14.0690833+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/4 | 1,023,501 | 590.2s / 1,403,091 msg/s |
| Dekaf | 2026-07-29T18:53:32.0734036+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/4 | 1,058,194 | 608.2s / 1,293,438 msg/s |
| Dekaf | 2026-07-29T18:53:50.0806631+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1629.2 MB/s | 2/4 | 1,089,155 | 626.2s / 1,220,722 msg/s |
| Dekaf | 2026-07-29T18:54:09.0864237+00:00 | 1 | 10.0 MiB / 7.6 MiB | 1629.2 MB/s | 2/4 | 1,119,266 | 645.2s / 1,368,805 msg/s |
| Dekaf | 2026-07-29T18:54:27.0935137+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/5 | 1,156,727 | 663.2s / 1,286,858 msg/s |
| Dekaf | 2026-07-29T18:54:45.1010172+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1629.2 MB/s | 2/5 | 1,194,099 | 681.2s / 1,250,590 msg/s |
| Dekaf | 2026-07-29T18:55:03.1120728+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/5 | 1,230,596 | 699.2s / 1,429,035 msg/s |
| Dekaf | 2026-07-29T18:55:21.1163734+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1629.2 MB/s | 2/5 | 1,268,683 | 717.2s / 1,444,981 msg/s |
| Dekaf | 2026-07-29T18:55:39.1213623+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1629.2 MB/s | 2/5 | 1,288,018 | 735.2s / 1,009,551 msg/s |
| Dekaf | 2026-07-29T18:55:58.1284245+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1629.2 MB/s | 2/5 | 1,317,651 | 754.3s / 1,093,862 msg/s |
| Dekaf | 2026-07-29T18:56:16.1305479+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1629.2 MB/s | 2/5 | 1,343,193 | 772.3s / 1,126,238 msg/s |
| Dekaf | 2026-07-29T18:56:34.1349014+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1629.2 MB/s | 2/5 | 1,361,371 | 790.3s / 1,006,542 msg/s |
| Dekaf | 2026-07-29T18:56:52.1409126+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1629.2 MB/s | 2/5 | 1,381,250 | 808.3s / 1,125,419 msg/s |
| Dekaf | 2026-07-29T18:57:10.1478201+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1629.2 MB/s | 2/5 | 1,401,631 | 826.3s / 1,007,770 msg/s |
| Dekaf | 2026-07-29T18:57:28.1554171+00:00 | 1 | 12.0 MiB / 6.5 MiB | 1629.2 MB/s | 2/5 | 1,423,226 | 844.3s / 974,410 msg/s |
| Dekaf | 2026-07-29T18:57:47.1586749+00:00 | 1 | 12.0 MiB / 8.1 MiB | 1629.2 MB/s | 2/5 | 1,440,447 | 863.3s / 1,146,127 msg/s |
| Dekaf | 2026-07-29T18:58:05.1668759+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1629.2 MB/s | 2/5 | 1,459,012 | 881.3s / 1,148,856 msg/s |
| Dekaf | 2026-07-29T18:58:23.1755376+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1629.2 MB/s | 2/5 | 1,481,251 | 899.3s / 1,129,663 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T17:58:52.6158031+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T17:59:07.6309765+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 12.7 MiB |
| Dekaf | 2026-07-29T17:59:37.6574482+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T17:59:52.667951+00:00 | 1 | capacity | failed | 15,010ms | 14.0 MiB / 11.2 MiB |
| Dekaf | 2026-07-29T18:00:52.7183745+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-29T18:01:07.7347846+00:00 | 1 | capacity | succeeded | 15,016ms | 15.0 MiB / 13.9 MiB |
| Dekaf | 2026-07-29T18:01:37.7961306+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-29T18:01:52.8159563+00:00 | 1 | capacity | failed | 15,019ms | 15.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T18:02:52.882393+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:03:07.8954204+00:00 | 1 | capacity | failed | 15,013ms | 15.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:05:08.0365432+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T18:05:23.0543819+00:00 | 1 | capacity | failed | 15,017ms | 15.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T18:09:23.3978305+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.3 MiB |
| Dekaf | 2026-07-29T18:09:38.4138354+00:00 | 1 | capacity | succeeded | 15,016ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:09:41.4164113+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:09:56.4363051+00:00 | 1 | capacity | failed | 15,020ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:10:56.5092367+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 9.8 MiB |
| Dekaf | 2026-07-29T18:11:11.5210052+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:13:11.6233892+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-29T18:43:53.9588511+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T18:44:08.9718065+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:44:38.9948514+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:44:54.0036776+00:00 | 1 | capacity | succeeded | 15,008ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:45:24.0261057+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-29T18:45:39.0404863+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T18:46:39.0942247+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:46:54.1117958+00:00 | 1 | capacity | failed | 15,017ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:48:54.2290061+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:49:09.2615646+00:00 | 1 | capacity | failed | 15,032ms | 12.0 MiB / 6.3 MiB |
| Dekaf | 2026-07-29T18:49:39.3048237+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:49:54.3246102+00:00 | 1 | capacity | failed | 15,019ms | 12.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-29T18:53:54.5593589+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 8.7 MiB |
| Dekaf | 2026-07-29T18:54:09.5749942+00:00 | 1 | capacity | failed | 15,016ms | 12.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T18:58:09.8458489+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 2.8 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,358 |
| Dekaf | 1 | 0.002–0.004ms | 1,662 |
| Dekaf | 1 | 0.004–0.008ms | 4,390 |
| Dekaf | 1 | 0.008–0.016ms | 24,814 |
| Dekaf | 1 | 0.016–0.032ms | 32,333 |
| Dekaf | 1 | 0.032–0.064ms | 38,459 |
| Dekaf | 1 | 0.064–0.128ms | 66,281 |
| Dekaf | 1 | 0.128–0.256ms | 177,577 |
| Dekaf | 1 | 0.256–0.512ms | 213,736 |
| Dekaf | 1 | 0.512–1.024ms | 63,894 |
| Dekaf | 1 | 1.024–2.048ms | 10,861 |
| Dekaf | 1 | 2.048–4.096ms | 4,049 |
| Dekaf | 1 | 4.096–8.192ms | 1,040 |
| Dekaf | 1 | 8.192–16.384ms | 136 |
| Dekaf | 1 | 16.384–32.768ms | 4 |
| Dekaf | 1 | 65.536–131.072ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,311 |
| Dekaf | 1 | 0.002–0.004ms | 1,389 |
| Dekaf | 1 | 0.004–0.008ms | 3,767 |
| Dekaf | 1 | 0.008–0.016ms | 23,865 |
| Dekaf | 1 | 0.016–0.032ms | 39,313 |
| Dekaf | 1 | 0.032–0.064ms | 36,408 |
| Dekaf | 1 | 0.064–0.128ms | 54,103 |
| Dekaf | 1 | 0.128–0.256ms | 128,361 |
| Dekaf | 1 | 0.256–0.512ms | 162,079 |
| Dekaf | 1 | 0.512–1.024ms | 82,968 |
| Dekaf | 1 | 1.024–2.048ms | 30,164 |
| Dekaf | 1 | 2.048–4.096ms | 4,556 |
| Dekaf | 1 | 4.096–8.192ms | 1,206 |
| Dekaf | 1 | 8.192–16.384ms | 154 |
| Dekaf | 1 | 16.384–32.768ms | 4 |
| Dekaf | 1 | 32.768–65.536ms | 3 |
| Dekaf | 1 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Acks All)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 21,178,000 | 2026-07-29T18:13:49.4805137+00:00 | 105.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,177,000 | 2026-07-29T18:13:49.4831343+00:00 | 102.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,181,000 | 2026-07-29T18:13:49.484435+00:00 | 101.5ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,201,000 | 2026-07-29T18:13:49.5047365+00:00 | 101.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,207,000 | 2026-07-29T18:13:49.5090242+00:00 | 103.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,208,000 | 2026-07-29T18:13:49.5098819+00:00 | 102.6ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,211,000 | 2026-07-29T18:13:49.5114958+00:00 | 101.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,217,000 | 2026-07-29T18:13:49.5153212+00:00 | 101.5ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,218,000 | 2026-07-29T18:13:49.515934+00:00 | 109.3ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,221,000 | 2026-07-29T18:13:49.5178892+00:00 | 107.6ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,227,000 | 2026-07-29T18:13:49.5213239+00:00 | 109.1ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,228,000 | 2026-07-29T18:13:49.5218311+00:00 | 108.7ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,231,000 | 2026-07-29T18:13:49.5254357+00:00 | 117.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,237,000 | 2026-07-29T18:13:49.5299514+00:00 | 114.7ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,238,000 | 2026-07-29T18:13:49.5305805+00:00 | 114.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,241,000 | 2026-07-29T18:13:49.5328358+00:00 | 114.3ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,243,000 | 2026-07-29T18:13:49.5341413+00:00 | 109.5ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,244,000 | 2026-07-29T18:13:49.5350228+00:00 | 106.8ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,245,000 | 2026-07-29T18:13:49.5357005+00:00 | 106.5ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,246,000 | 2026-07-29T18:13:49.5363836+00:00 | 105.9ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,247,000 | 2026-07-29T18:13:49.5370061+00:00 | 115.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,248,000 | 2026-07-29T18:13:49.5379529+00:00 | 114.3ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,249,000 | 2026-07-29T18:13:49.5386189+00:00 | 105.7ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,250,000 | 2026-07-29T18:13:49.5393594+00:00 | 106.6ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,251,000 | 2026-07-29T18:13:49.5399736+00:00 | 112.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,252,000 | 2026-07-29T18:13:49.5406715+00:00 | 102.9ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,253,000 | 2026-07-29T18:13:49.5416655+00:00 | 104.5ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,254,000 | 2026-07-29T18:13:49.5423788+00:00 | 104.0ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,255,000 | 2026-07-29T18:13:49.5431245+00:00 | 103.8ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,256,000 | 2026-07-29T18:13:49.543825+00:00 | 103.1ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,257,000 | 2026-07-29T18:13:49.5451379+00:00 | 112.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,258,000 | 2026-07-29T18:13:49.5461468+00:00 | 120.0ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,259,000 | 2026-07-29T18:13:49.5467141+00:00 | 100.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,260,000 | 2026-07-29T18:13:49.5473975+00:00 | 104.3ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,261,000 | 2026-07-29T18:13:49.547926+00:00 | 118.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,263,000 | 2026-07-29T18:13:49.5496012+00:00 | 105.5ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,264,000 | 2026-07-29T18:13:49.550696+00:00 | 101.2ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,265,000 | 2026-07-29T18:13:49.5521141+00:00 | 100.0ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,267,000 | 2026-07-29T18:13:49.5552362+00:00 | 117.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,268,000 | 2026-07-29T18:13:49.5566598+00:00 | 116.0ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,270,000 | 2026-07-29T18:13:49.5595431+00:00 | 105.8ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,271,000 | 2026-07-29T18:13:49.5651753+00:00 | 112.7ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,277,000 | 2026-07-29T18:13:49.5771264+00:00 | 107.8ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,278,000 | 2026-07-29T18:13:49.5823177+00:00 | 102.6ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 21,281,000 | 2026-07-29T18:13:49.589369+00:00 | 102.4ms | GC pause | - | - | 27.0s / 864,147 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,138,000 | 2026-07-29T18:14:05.515864+00:00 | 101.9ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,141,000 | 2026-07-29T18:14:05.5178935+00:00 | 101.5ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,151,000 | 2026-07-29T18:14:05.5264775+00:00 | 109.0ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,157,000 | 2026-07-29T18:14:05.5329524+00:00 | 104.3ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,158,000 | 2026-07-29T18:14:05.5336073+00:00 | 103.7ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,161,000 | 2026-07-29T18:14:05.5359368+00:00 | 101.5ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,167,000 | 2026-07-29T18:14:05.5412259+00:00 | 104.1ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,168,000 | 2026-07-29T18:14:05.5429125+00:00 | 107.8ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,171,000 | 2026-07-29T18:14:05.5463717+00:00 | 104.5ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,177,000 | 2026-07-29T18:14:05.5528584+00:00 | 107.0ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,178,000 | 2026-07-29T18:14:05.5536412+00:00 | 106.2ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,181,000 | 2026-07-29T18:14:05.5562397+00:00 | 108.1ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,187,000 | 2026-07-29T18:14:05.5629005+00:00 | 109.1ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,188,000 | 2026-07-29T18:14:05.5636385+00:00 | 108.4ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,191,000 | 2026-07-29T18:14:05.5669766+00:00 | 105.2ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,193,000 | 2026-07-29T18:14:05.5690461+00:00 | 102.1ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,197,000 | 2026-07-29T18:14:05.5719658+00:00 | 110.0ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,198,000 | 2026-07-29T18:14:05.5741428+00:00 | 108.9ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,201,000 | 2026-07-29T18:14:05.5777303+00:00 | 105.5ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,207,000 | 2026-07-29T18:14:05.5856957+00:00 | 102.2ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,208,000 | 2026-07-29T18:14:05.5862605+00:00 | 101.7ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,211,000 | 2026-07-29T18:14:05.5884287+00:00 | 100.8ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,217,000 | 2026-07-29T18:14:05.5930564+00:00 | 104.4ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,218,000 | 2026-07-29T18:14:05.5969569+00:00 | 100.5ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,227,000 | 2026-07-29T18:14:05.6061185+00:00 | 102.8ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,228,000 | 2026-07-29T18:14:05.6072237+00:00 | 101.8ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,238,000 | 2026-07-29T18:14:05.6149381+00:00 | 111.7ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,241,000 | 2026-07-29T18:14:05.617663+00:00 | 109.1ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 35,247,000 | 2026-07-29T18:14:05.6277924+00:00 | 100.3ms | GC pause | - | - | 43.0s / 943,768 msg/s | Gen2 +0 / pause +110.9ms |
| Confluent | 688,217,000 | 2026-07-29T18:38:53.6900522+00:00 | 100.0ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,287,000 | 2026-07-29T18:38:53.7681603+00:00 | 118.5ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,293,000 | 2026-07-29T18:38:53.7805285+00:00 | 100.7ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,347,000 | 2026-07-29T18:38:53.8364708+00:00 | 125.1ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,358,000 | 2026-07-29T18:38:53.8500376+00:00 | 122.6ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,380,000 | 2026-07-29T18:38:53.8829796+00:00 | 102.2ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,383,000 | 2026-07-29T18:38:53.8885065+00:00 | 103.4ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,440,000 | 2026-07-29T18:38:53.9590972+00:00 | 105.8ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,460,000 | 2026-07-29T18:38:53.9839403+00:00 | 107.4ms | GC pause | - | - | 630.4s / 850,386 msg/s | Gen2 +0 / pause +66.7ms |
| Confluent | 688,488,000 | 2026-07-29T18:38:54.0256517+00:00 | 140.7ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +131.3ms |
| Confluent | 688,497,000 | 2026-07-29T18:38:54.0440737+00:00 | 126.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +131.3ms |
| Confluent | 688,511,000 | 2026-07-29T18:38:54.0690763+00:00 | 117.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +131.3ms |
| Confluent | 688,523,000 | 2026-07-29T18:38:54.0839814+00:00 | 108.7ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +131.3ms |
| Confluent | 688,531,000 | 2026-07-29T18:38:54.0953143+00:00 | 121.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +131.3ms |
| Confluent | 688,557,000 | 2026-07-29T18:38:54.1280014+00:00 | 148.1ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,561,000 | 2026-07-29T18:38:54.1334871+00:00 | 147.8ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,575,000 | 2026-07-29T18:38:54.1541162+00:00 | 103.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,595,000 | 2026-07-29T18:38:54.1825126+00:00 | 116.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,611,000 | 2026-07-29T18:38:54.1995302+00:00 | 155.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,614,000 | 2026-07-29T18:38:54.203297+00:00 | 112.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,615,000 | 2026-07-29T18:38:54.2044351+00:00 | 121.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,647,000 | 2026-07-29T18:38:54.2383878+00:00 | 162.1ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,654,000 | 2026-07-29T18:38:54.2432147+00:00 | 123.8ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,659,000 | 2026-07-29T18:38:54.2482925+00:00 | 131.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,673,000 | 2026-07-29T18:38:54.2650408+00:00 | 144.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,677,000 | 2026-07-29T18:38:54.2687093+00:00 | 170.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,686,000 | 2026-07-29T18:38:54.2821328+00:00 | 136.9ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,697,000 | 2026-07-29T18:38:54.3066903+00:00 | 159.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,727,000 | 2026-07-29T18:38:54.3479537+00:00 | 164.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,743,000 | 2026-07-29T18:38:54.3644064+00:00 | 146.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,761,000 | 2026-07-29T18:38:54.3833672+00:00 | 172.5ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,768,000 | 2026-07-29T18:38:54.3909622+00:00 | 173.9ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,769,000 | 2026-07-29T18:38:54.3919079+00:00 | 141.7ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,789,000 | 2026-07-29T18:38:54.4201399+00:00 | 135.5ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,796,000 | 2026-07-29T18:38:54.4332702+00:00 | 131.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,797,000 | 2026-07-29T18:38:54.4340942+00:00 | 173.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,808,000 | 2026-07-29T18:38:54.4517726+00:00 | 168.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,814,000 | 2026-07-29T18:38:54.4589849+00:00 | 118.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,819,000 | 2026-07-29T18:38:54.4644736+00:00 | 128.8ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,825,000 | 2026-07-29T18:38:54.4714568+00:00 | 135.7ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,840,000 | 2026-07-29T18:38:54.4880359+00:00 | 144.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,857,000 | 2026-07-29T18:38:54.5054847+00:00 | 188.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,876,000 | 2026-07-29T18:38:54.5317661+00:00 | 145.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,884,000 | 2026-07-29T18:38:54.5437242+00:00 | 132.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,885,000 | 2026-07-29T18:38:54.544352+00:00 | 139.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,902,000 | 2026-07-29T18:38:54.5671916+00:00 | 124.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,906,000 | 2026-07-29T18:38:54.571821+00:00 | 143.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,919,000 | 2026-07-29T18:38:54.5886196+00:00 | 144.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,922,000 | 2026-07-29T18:38:54.5920558+00:00 | 119.7ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,923,000 | 2026-07-29T18:38:54.59289+00:00 | 154.8ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,944,000 | 2026-07-29T18:38:54.6201622+00:00 | 141.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,979,000 | 2026-07-29T18:38:54.6602585+00:00 | 159.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,982,000 | 2026-07-29T18:38:54.6643221+00:00 | 144.1ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,992,000 | 2026-07-29T18:38:54.6752682+00:00 | 142.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,994,000 | 2026-07-29T18:38:54.6778732+00:00 | 146.4ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 688,999,000 | 2026-07-29T18:38:54.6861872+00:00 | 164.1ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,006,000 | 2026-07-29T18:38:54.6983845+00:00 | 162.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,019,000 | 2026-07-29T18:38:54.7157627+00:00 | 157.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,030,000 | 2026-07-29T18:38:54.7275816+00:00 | 173.9ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,031,000 | 2026-07-29T18:38:54.729521+00:00 | 208.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,040,000 | 2026-07-29T18:38:54.7430555+00:00 | 167.0ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,065,000 | 2026-07-29T18:38:54.7640473+00:00 | 172.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,101,000 | 2026-07-29T18:38:54.8053431+00:00 | 224.3ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,103,000 | 2026-07-29T18:38:54.8094805+00:00 | 184.9ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,109,000 | 2026-07-29T18:38:54.8169512+00:00 | 174.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,114,000 | 2026-07-29T18:38:54.8243808+00:00 | 166.1ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,122,000 | 2026-07-29T18:38:54.8343181+00:00 | 150.8ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,136,000 | 2026-07-29T18:38:54.85198+00:00 | 177.2ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,144,000 | 2026-07-29T18:38:54.8652661+00:00 | 161.5ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,146,000 | 2026-07-29T18:38:54.8669499+00:00 | 169.7ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,147,000 | 2026-07-29T18:38:54.8685253+00:00 | 224.9ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,149,000 | 2026-07-29T18:38:54.871401+00:00 | 173.6ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,165,000 | 2026-07-29T18:38:54.891752+00:00 | 170.1ms | GC pause | - | - | 631.4s / 791,157 msg/s | Gen2 +0 / pause +64.6ms |
| Confluent | 689,191,000 | 2026-07-29T18:38:54.9213405+00:00 | 227.7ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,223,000 | 2026-07-29T18:38:54.9645218+00:00 | 182.2ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,236,000 | 2026-07-29T18:38:54.9798597+00:00 | 168.7ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,254,000 | 2026-07-29T18:38:55.0000978+00:00 | 162.4ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,263,000 | 2026-07-29T18:38:55.0121689+00:00 | 190.9ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,275,000 | 2026-07-29T18:38:55.0262752+00:00 | 177.8ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,296,000 | 2026-07-29T18:38:55.0516153+00:00 | 175.5ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,297,000 | 2026-07-29T18:38:55.0526953+00:00 | 265.7ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,316,000 | 2026-07-29T18:38:55.0806302+00:00 | 200.7ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,319,000 | 2026-07-29T18:38:55.083196+00:00 | 198.4ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,328,000 | 2026-07-29T18:38:55.0955973+00:00 | 271.5ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +133.0ms |
| Confluent | 689,344,000 | 2026-07-29T18:38:55.1306361+00:00 | 172.8ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,364,000 | 2026-07-29T18:38:55.1574747+00:00 | 191.9ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,366,000 | 2026-07-29T18:38:55.1596352+00:00 | 196.3ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,374,000 | 2026-07-29T18:38:55.1700681+00:00 | 185.5ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,386,000 | 2026-07-29T18:38:55.1858227+00:00 | 199.6ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,396,000 | 2026-07-29T18:38:55.1995866+00:00 | 196.1ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,398,000 | 2026-07-29T18:38:55.2011969+00:00 | 258.8ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,417,000 | 2026-07-29T18:38:55.2291942+00:00 | 253.2ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,465,000 | 2026-07-29T18:38:55.3053798+00:00 | 176.6ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,472,000 | 2026-07-29T18:38:55.3129902+00:00 | 149.8ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,482,000 | 2026-07-29T18:38:55.323418+00:00 | 157.2ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,484,000 | 2026-07-29T18:38:55.325251+00:00 | 168.6ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,517,000 | 2026-07-29T18:38:55.3837408+00:00 | 223.6ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,519,000 | 2026-07-29T18:38:55.3881098+00:00 | 159.9ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,545,000 | 2026-07-29T18:38:55.4360995+00:00 | 140.0ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,559,000 | 2026-07-29T18:38:55.4648533+00:00 | 136.7ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,565,000 | 2026-07-29T18:38:55.47616+00:00 | 129.9ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,572,000 | 2026-07-29T18:38:55.4895647+00:00 | 100.1ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,573,000 | 2026-07-29T18:38:55.4908605+00:00 | 137.8ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,574,000 | 2026-07-29T18:38:55.4918804+00:00 | 112.8ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,583,000 | 2026-07-29T18:38:55.5092974+00:00 | 122.2ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 689,617,000 | 2026-07-29T18:38:55.5732402+00:00 | 119.7ms | GC pause | - | - | 632.4s / 824,497 msg/s | Gen2 +0 / pause +68.3ms |
| Confluent | 748,121,000 | 2026-07-29T18:39:46.2380877+00:00 | 103.2ms | GC pause | - | - | 683.4s / 896,341 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 748,168,000 | 2026-07-29T18:39:46.2872005+00:00 | 107.0ms | GC pause | - | - | 683.4s / 896,341 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 748,171,000 | 2026-07-29T18:39:46.2917161+00:00 | 102.7ms | GC pause | - | - | 683.4s / 896,341 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 755,643,000 | 2026-07-29T18:39:54.3927664+00:00 | 106.9ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,644,000 | 2026-07-29T18:39:54.393614+00:00 | 102.5ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,675,000 | 2026-07-29T18:39:54.4157693+00:00 | 108.0ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,690,000 | 2026-07-29T18:39:54.4258907+00:00 | 117.7ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,699,000 | 2026-07-29T18:39:54.4334591+00:00 | 113.1ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,722,000 | 2026-07-29T18:39:54.4505713+00:00 | 120.2ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,760,000 | 2026-07-29T18:39:54.475442+00:00 | 127.2ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,824,000 | 2026-07-29T18:39:54.5391131+00:00 | 122.6ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,832,000 | 2026-07-29T18:39:54.5479238+00:00 | 123.6ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,844,000 | 2026-07-29T18:39:54.5615928+00:00 | 123.4ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,851,000 | 2026-07-29T18:39:54.5683682+00:00 | 141.1ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,864,000 | 2026-07-29T18:39:54.5834471+00:00 | 116.2ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,865,000 | 2026-07-29T18:39:54.5840784+00:00 | 115.8ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,902,000 | 2026-07-29T18:39:54.6242625+00:00 | 119.3ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,909,000 | 2026-07-29T18:39:54.6394708+00:00 | 104.9ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,912,000 | 2026-07-29T18:39:54.6456056+00:00 | 105.4ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,921,000 | 2026-07-29T18:39:54.6577656+00:00 | 110.7ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 755,922,000 | 2026-07-29T18:39:54.6585757+00:00 | 102.8ms | GC pause | - | - | 691.5s / 972,345 msg/s | Gen2 +0 / pause +73.9ms |
| Confluent | 852,548,000 | 2026-07-29T18:41:44.8389075+00:00 | 108.5ms | GC pause | - | - | 801.5s / 1,121,841 msg/s | Gen2 +0 / pause +74.3ms |
| Confluent | 852,571,000 | 2026-07-29T18:41:44.8588363+00:00 | 103.8ms | GC pause | - | - | 801.5s / 1,121,841 msg/s | Gen2 +0 / pause +74.3ms |
| Confluent | 852,618,000 | 2026-07-29T18:41:44.9017771+00:00 | 100.4ms | GC pause | - | - | 801.5s / 1,121,841 msg/s | Gen2 +0 / pause +74.3ms |
| Confluent | 852,941,000 | 2026-07-29T18:41:45.1894434+00:00 | 118.0ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +122.7ms |
| Confluent | 852,981,000 | 2026-07-29T18:41:45.2184318+00:00 | 120.4ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +122.7ms |
| Confluent | 853,018,000 | 2026-07-29T18:41:45.2469396+00:00 | 122.4ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,047,000 | 2026-07-29T18:41:45.2928668+00:00 | 109.0ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,567,000 | 2026-07-29T18:41:45.7672805+00:00 | 112.4ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,578,000 | 2026-07-29T18:41:45.7784104+00:00 | 109.8ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,657,000 | 2026-07-29T18:41:45.844697+00:00 | 106.2ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,661,000 | 2026-07-29T18:41:45.8478108+00:00 | 108.1ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,677,000 | 2026-07-29T18:41:45.8590485+00:00 | 113.6ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,678,000 | 2026-07-29T18:41:45.8596375+00:00 | 113.0ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,701,000 | 2026-07-29T18:41:45.8843446+00:00 | 112.4ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,737,000 | 2026-07-29T18:41:45.9161366+00:00 | 103.9ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,758,000 | 2026-07-29T18:41:45.9366811+00:00 | 101.0ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,777,000 | 2026-07-29T18:41:45.9516248+00:00 | 104.3ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,798,000 | 2026-07-29T18:41:45.9661259+00:00 | 107.4ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,918,000 | 2026-07-29T18:41:46.0715172+00:00 | 110.6ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,948,000 | 2026-07-29T18:41:46.0961825+00:00 | 113.5ms | GC pause | - | - | 802.5s / 1,098,313 msg/s | Gen2 +0 / pause +48.5ms |
| Confluent | 853,988,000 | 2026-07-29T18:41:46.1370282+00:00 | 121.7ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +100.9ms |
| Confluent | 854,097,000 | 2026-07-29T18:41:46.2329408+00:00 | 133.5ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,144,000 | 2026-07-29T18:41:46.2663869+00:00 | 104.5ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,149,000 | 2026-07-29T18:41:46.2692116+00:00 | 102.0ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,154,000 | 2026-07-29T18:41:46.2726366+00:00 | 112.9ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,160,000 | 2026-07-29T18:41:46.2771638+00:00 | 103.9ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,161,000 | 2026-07-29T18:41:46.2777056+00:00 | 148.6ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,175,000 | 2026-07-29T18:41:46.2867861+00:00 | 115.2ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,177,000 | 2026-07-29T18:41:46.2877752+00:00 | 154.0ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,201,000 | 2026-07-29T18:41:46.3079579+00:00 | 158.2ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,209,000 | 2026-07-29T18:41:46.3195906+00:00 | 106.6ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,213,000 | 2026-07-29T18:41:46.3275121+00:00 | 101.1ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,219,000 | 2026-07-29T18:41:46.3362389+00:00 | 100.7ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,227,000 | 2026-07-29T18:41:46.3445806+00:00 | 142.6ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,228,000 | 2026-07-29T18:41:46.3453003+00:00 | 142.0ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,234,000 | 2026-07-29T18:41:46.3515623+00:00 | 102.9ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,248,000 | 2026-07-29T18:41:46.3671573+00:00 | 143.6ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,281,000 | 2026-07-29T18:41:46.3960479+00:00 | 143.3ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,327,000 | 2026-07-29T18:41:46.4441399+00:00 | 144.5ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,337,000 | 2026-07-29T18:41:46.4519955+00:00 | 143.7ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,457,000 | 2026-07-29T18:41:46.5638466+00:00 | 135.0ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,591,000 | 2026-07-29T18:41:46.6972023+00:00 | 127.6ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,667,000 | 2026-07-29T18:41:46.7643603+00:00 | 145.7ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,693,000 | 2026-07-29T18:41:46.789594+00:00 | 107.2ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,708,000 | 2026-07-29T18:41:46.8025779+00:00 | 141.8ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,710,000 | 2026-07-29T18:41:46.8046749+00:00 | 106.0ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,757,000 | 2026-07-29T18:41:46.8606332+00:00 | 123.8ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,797,000 | 2026-07-29T18:41:46.8938986+00:00 | 126.9ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,841,000 | 2026-07-29T18:41:46.9440596+00:00 | 115.5ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,857,000 | 2026-07-29T18:41:46.9569681+00:00 | 118.5ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 854,867,000 | 2026-07-29T18:41:46.9647111+00:00 | 112.2ms | GC pause | - | - | 803.5s / 1,019,676 msg/s | Gen2 +0 / pause +52.4ms |
| Confluent | 867,191,000 | 2026-07-29T18:41:57.6805105+00:00 | 103.1ms | GC pause | - | - | 814.5s / 1,166,349 msg/s | Gen2 +0 / pause +72.5ms |
| Confluent | 867,228,000 | 2026-07-29T18:41:57.7101419+00:00 | 105.1ms | GC pause | - | - | 814.5s / 1,166,349 msg/s | Gen2 +0 / pause +72.5ms |
| Confluent | 867,238,000 | 2026-07-29T18:41:57.7177694+00:00 | 110.8ms | GC pause | - | - | 814.5s / 1,166,349 msg/s | Gen2 +0 / pause +72.5ms |
| Confluent | 867,241,000 | 2026-07-29T18:41:57.720692+00:00 | 107.9ms | GC pause | - | - | 814.5s / 1,166,349 msg/s | Gen2 +0 / pause +72.5ms |
| Confluent | 867,267,000 | 2026-07-29T18:41:57.7412504+00:00 | 113.0ms | GC pause | - | - | 814.5s / 1,166,349 msg/s | Gen2 +0 / pause +72.5ms |
| Confluent | 872,885,000 | 2026-07-29T18:42:02.5766585+00:00 | 104.1ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,899,000 | 2026-07-29T18:42:02.586862+00:00 | 108.3ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,905,000 | 2026-07-29T18:42:02.5908314+00:00 | 109.7ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,908,000 | 2026-07-29T18:42:02.5966928+00:00 | 129.8ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,960,000 | 2026-07-29T18:42:02.6464629+00:00 | 102.8ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,978,000 | 2026-07-29T18:42:02.6614813+00:00 | 127.0ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,985,000 | 2026-07-29T18:42:02.6669157+00:00 | 105.3ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 872,995,000 | 2026-07-29T18:42:02.6775452+00:00 | 106.0ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 873,020,000 | 2026-07-29T18:42:02.6968846+00:00 | 104.0ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 873,048,000 | 2026-07-29T18:42:02.7320299+00:00 | 115.7ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 873,082,000 | 2026-07-29T18:42:02.7597674+00:00 | 107.0ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 873,118,000 | 2026-07-29T18:42:02.7917607+00:00 | 114.2ms | GC pause | - | - | 819.5s / 1,164,808 msg/s | Gen2 +0 / pause +65.5ms |
| Confluent | 874,248,000 | 2026-07-29T18:42:03.8321249+00:00 | 100.2ms | GC pause | - | - | 820.5s / 1,090,190 msg/s | Gen2 +0 / pause +86.5ms |
| Confluent | 874,257,000 | 2026-07-29T18:42:03.8380654+00:00 | 103.8ms | GC pause | - | - | 820.5s / 1,090,190 msg/s | Gen2 +0 / pause +86.5ms |
| Confluent | 874,461,000 | 2026-07-29T18:42:04.0170053+00:00 | 107.8ms | GC pause | - | - | 820.5s / 1,090,190 msg/s | Gen2 +0 / pause +86.5ms |
| Confluent | 874,838,000 | 2026-07-29T18:42:04.3974284+00:00 | 104.1ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 874,867,000 | 2026-07-29T18:42:04.4234002+00:00 | 104.7ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 874,968,000 | 2026-07-29T18:42:04.5059927+00:00 | 109.7ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,018,000 | 2026-07-29T18:42:04.5571328+00:00 | 102.0ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,038,000 | 2026-07-29T18:42:04.5748862+00:00 | 103.6ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,048,000 | 2026-07-29T18:42:04.5828019+00:00 | 104.1ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,051,000 | 2026-07-29T18:42:04.5849648+00:00 | 109.1ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,148,000 | 2026-07-29T18:42:04.6669602+00:00 | 113.1ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,151,000 | 2026-07-29T18:42:04.6691474+00:00 | 111.0ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,157,000 | 2026-07-29T18:42:04.6742394+00:00 | 114.4ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,218,000 | 2026-07-29T18:42:04.7275335+00:00 | 121.5ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,237,000 | 2026-07-29T18:42:04.7546267+00:00 | 111.8ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,251,000 | 2026-07-29T18:42:04.7751395+00:00 | 103.8ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,288,000 | 2026-07-29T18:42:04.8184187+00:00 | 102.5ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,367,000 | 2026-07-29T18:42:04.8714862+00:00 | 132.3ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,418,000 | 2026-07-29T18:42:04.9131714+00:00 | 128.1ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,518,000 | 2026-07-29T18:42:04.9945905+00:00 | 141.2ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,597,000 | 2026-07-29T18:42:05.0751927+00:00 | 138.1ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 875,601,000 | 2026-07-29T18:42:05.0795186+00:00 | 133.9ms | GC pause | - | - | 821.5s / 1,044,248 msg/s | Gen2 +0 / pause +82.6ms |
| Confluent | 883,237,000 | 2026-07-29T18:42:13.9791788+00:00 | 105.8ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,267,000 | 2026-07-29T18:42:14.009266+00:00 | 101.5ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,278,000 | 2026-07-29T18:42:14.0177918+00:00 | 106.0ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,285,000 | 2026-07-29T18:42:14.0231924+00:00 | 100.3ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,298,000 | 2026-07-29T18:42:14.0463415+00:00 | 105.1ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,360,000 | 2026-07-29T18:42:14.1038167+00:00 | 100.4ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,376,000 | 2026-07-29T18:42:14.1221642+00:00 | 104.8ms | GC pause | - | - | 830.5s / 1,005,189 msg/s | Gen2 +0 / pause +114.8ms |
| Confluent | 883,391,000 | 2026-07-29T18:42:14.142786+00:00 | 127.2ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,403,000 | 2026-07-29T18:42:14.1547399+00:00 | 114.5ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,410,000 | 2026-07-29T18:42:14.159558+00:00 | 111.1ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,411,000 | 2026-07-29T18:42:14.1602106+00:00 | 123.5ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,422,000 | 2026-07-29T18:42:14.1680996+00:00 | 114.6ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,423,000 | 2026-07-29T18:42:14.1687659+00:00 | 114.2ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,424,000 | 2026-07-29T18:42:14.1693871+00:00 | 106.5ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,438,000 | 2026-07-29T18:42:14.1801156+00:00 | 129.0ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,452,000 | 2026-07-29T18:42:14.1954666+00:00 | 109.5ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,471,000 | 2026-07-29T18:42:14.2152019+00:00 | 138.4ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,485,000 | 2026-07-29T18:42:14.2328671+00:00 | 107.2ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,499,000 | 2026-07-29T18:42:14.2441972+00:00 | 118.6ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +227.2ms |
| Confluent | 883,510,000 | 2026-07-29T18:42:14.2538475+00:00 | 120.2ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +112.5ms |
| Confluent | 883,519,000 | 2026-07-29T18:42:14.2740669+00:00 | 107.1ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +112.5ms |
| Confluent | 883,534,000 | 2026-07-29T18:42:14.2971901+00:00 | 101.7ms | GC pause | - | - | 831.5s / 854,053 msg/s | Gen2 +0 / pause +112.5ms |
| Confluent | 930,326,000 | 2026-07-29T18:43:05.4166675+00:00 | 106.5ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,346,000 | 2026-07-29T18:43:05.4305065+00:00 | 114.0ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,367,000 | 2026-07-29T18:43:05.4472404+00:00 | 119.3ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,388,000 | 2026-07-29T18:43:05.4678819+00:00 | 118.9ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,390,000 | 2026-07-29T18:43:05.4694796+00:00 | 109.2ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,391,000 | 2026-07-29T18:43:05.4701434+00:00 | 116.8ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,392,000 | 2026-07-29T18:43:05.4707202+00:00 | 114.4ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,398,000 | 2026-07-29T18:43:05.4748507+00:00 | 122.6ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,404,000 | 2026-07-29T18:43:05.4810251+00:00 | 114.5ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,413,000 | 2026-07-29T18:43:05.4893157+00:00 | 114.3ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,420,000 | 2026-07-29T18:43:05.4954445+00:00 | 110.8ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,449,000 | 2026-07-29T18:43:05.5225484+00:00 | 111.4ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,451,000 | 2026-07-29T18:43:05.5242465+00:00 | 138.6ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,469,000 | 2026-07-29T18:43:05.5408885+00:00 | 125.6ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,505,000 | 2026-07-29T18:43:05.5893907+00:00 | 113.6ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,535,000 | 2026-07-29T18:43:05.6272229+00:00 | 103.5ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 930,578,000 | 2026-07-29T18:43:05.6934231+00:00 | 105.5ms | GC pause | - | - | 882.6s / 882,765 msg/s | Gen2 +0 / pause +87.7ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*2,922 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.39x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.14x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.38 | 1385.91 | 953,817 | 1,020,587 | +22.4% | +1.82% | 909.63 | 953,817 | 0 | 1.31 |
| Confluent | 2.11 | - | 732,528 | 758,782 | +5.0% | +0.43% | 698.59 | 732,528 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 288,935 | 321.03 | 1011.54 KB |
| Dekaf | 2 | 281,721 | 313.02 | 993.22 KB |
| Dekaf | 3 | 282,099 | 313.44 | 997.04 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:13.2252562+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 303,859 msg/s |
| Dekaf | 2026-07-29T17:58:22.2394332+00:00 | 3 | 16.0 MiB / 4.0 MiB | 263.2 MB/s | 0/0 | 511 | 9.0s / 748,981 msg/s |
| Dekaf | 2026-07-29T17:58:31.2500323+00:00 | 3 | 16.0 MiB / 5.7 MiB | 326.2 MB/s | 0/0 | 2,473 | 18.0s / 928,207 msg/s |
| Dekaf | 2026-07-29T17:58:41.2552671+00:00 | 1 | 16.0 MiB / 16.0 MiB | 349.3 MB/s | 0/0 | 10,135 | 28.0s / 912,830 msg/s |
| Dekaf | 2026-07-29T17:58:50.2574162+00:00 | 1 | 16.0 MiB / 10.6 MiB | 377.8 MB/s | 0/0 | 12,816 | 37.0s / 917,456 msg/s |
| Dekaf | 2026-07-29T17:58:59.2579477+00:00 | 1 | 14.0 MiB / 12.6 MiB | 396.3 MB/s | 1/0 | 15,750 | 46.0s / 1,042,190 msg/s |
| Dekaf | 2026-07-29T17:59:08.264494+00:00 | 1 | 14.0 MiB / 13.5 MiB | 396.3 MB/s | 1/0 | 19,437 | 55.0s / 1,034,580 msg/s |
| Dekaf | 2026-07-29T17:59:17.2679474+00:00 | 2 | 14.0 MiB / 4.2 MiB | 390.5 MB/s | 1/1 | 2,624 | 64.0s / 1,131,524 msg/s |
| Dekaf | 2026-07-29T17:59:26.2769085+00:00 | 2 | 14.0 MiB / 6.8 MiB | 390.5 MB/s | 1/1 | 2,722 | 73.0s / 1,006,444 msg/s |
| Dekaf | 2026-07-29T17:59:35.2836377+00:00 | 2 | 14.0 MiB / 9.7 MiB | 390.5 MB/s | 1/1 | 3,199 | 82.0s / 1,059,971 msg/s |
| Dekaf | 2026-07-29T17:59:44.2859648+00:00 | 2 | 14.0 MiB / 6.6 MiB | 390.5 MB/s | 1/1 | 3,545 | 91.1s / 1,081,027 msg/s |
| Dekaf | 2026-07-29T17:59:53.2881797+00:00 | 3 | 14.0 MiB / 11.9 MiB | 396.8 MB/s | 1/1 | 8,662 | 100.1s / 677,599 msg/s |
| Dekaf | 2026-07-29T18:00:02.290286+00:00 | 3 | 14.0 MiB / 6.2 MiB | 396.8 MB/s | 1/1 | 8,850 | 109.1s / 735,853 msg/s |
| Dekaf | 2026-07-29T18:00:11.2907299+00:00 | 3 | 14.0 MiB / 5.9 MiB | 396.8 MB/s | 1/1 | 9,074 | 118.1s / 718,545 msg/s |
| Dekaf | 2026-07-29T18:00:20.2966041+00:00 | 3 | 12.0 MiB / 2.8 MiB | 396.8 MB/s | 1/1 | 9,848 | 127.1s / 716,556 msg/s |
| Dekaf | 2026-07-29T18:00:30.2975916+00:00 | 1 | 12.0 MiB / 11.5 MiB | 404.7 MB/s | 1/2 | 35,717 | 137.1s / 699,723 msg/s |
| Dekaf | 2026-07-29T18:00:39.3054342+00:00 | 1 | 10.0 MiB / 2.0 MiB | 404.7 MB/s | 2/2 | 37,196 | 146.1s / 649,132 msg/s |
| Dekaf | 2026-07-29T18:00:48.3097913+00:00 | 1 | 10.0 MiB / 4.4 MiB | 404.7 MB/s | 2/2 | 38,542 | 155.1s / 752,978 msg/s |
| Dekaf | 2026-07-29T18:00:57.3199676+00:00 | 1 | 12.0 MiB / 11.5 MiB | 404.7 MB/s | 2/3 | 38,887 | 164.1s / 734,886 msg/s |
| Dekaf | 2026-07-29T18:01:06.3253271+00:00 | 2 | 12.0 MiB / 1.1 MiB | 390.5 MB/s | 2/3 | 5,749 | 173.1s / 691,357 msg/s |
| Dekaf | 2026-07-29T18:01:15.3304453+00:00 | 2 | 12.0 MiB / 3.1 MiB | 390.5 MB/s | 2/3 | 6,092 | 182.1s / 709,428 msg/s |
| Dekaf | 2026-07-29T18:01:24.3354277+00:00 | 2 | 12.0 MiB / 4.8 MiB | 390.5 MB/s | 2/3 | 6,274 | 191.1s / 781,123 msg/s |
| Dekaf | 2026-07-29T18:01:33.3405347+00:00 | 2 | 12.0 MiB / 4.7 MiB | 390.5 MB/s | 2/3 | 6,360 | 200.1s / 845,839 msg/s |
| Dekaf | 2026-07-29T18:01:42.3438725+00:00 | 3 | 10.0 MiB / 3.7 MiB | 396.8 MB/s | 3/2 | 15,430 | 209.1s / 706,707 msg/s |
| Dekaf | 2026-07-29T18:01:51.3501455+00:00 | 3 | 8.0 MiB / 7.4 MiB | 396.8 MB/s | 3/2 | 16,699 | 218.1s / 789,267 msg/s |
| Dekaf | 2026-07-29T18:02:00.3563064+00:00 | 3 | 10.0 MiB / 6.1 MiB | 396.8 MB/s | 3/3 | 17,794 | 227.1s / 768,453 msg/s |
| Dekaf | 2026-07-29T18:02:09.3613613+00:00 | 3 | 10.0 MiB / 5.8 MiB | 396.8 MB/s | 3/3 | 18,435 | 236.1s / 735,998 msg/s |
| Dekaf | 2026-07-29T18:02:19.3695541+00:00 | 1 | 12.0 MiB / 5.2 MiB | 404.7 MB/s | 2/5 | 54,826 | 246.1s / 809,056 msg/s |
| Dekaf | 2026-07-29T18:02:28.3746553+00:00 | 1 | 12.0 MiB / 6.2 MiB | 404.7 MB/s | 2/5 | 55,616 | 255.1s / 783,887 msg/s |
| Dekaf | 2026-07-29T18:02:37.3759282+00:00 | 1 | 12.0 MiB / 5.2 MiB | 404.7 MB/s | 2/5 | 56,930 | 264.2s / 823,901 msg/s |
| Dekaf | 2026-07-29T18:02:46.3828902+00:00 | 1 | 12.0 MiB / 9.4 MiB | 404.7 MB/s | 2/6 | 59,131 | 273.2s / 1,123,913 msg/s |
| Dekaf | 2026-07-29T18:02:55.3866729+00:00 | 2 | 12.0 MiB / 2.2 MiB | 390.5 MB/s | 2/5 | 8,707 | 282.2s / 1,049,246 msg/s |
| Dekaf | 2026-07-29T18:03:04.3914276+00:00 | 2 | 12.0 MiB / 3.7 MiB | 390.5 MB/s | 2/5 | 8,711 | 291.2s / 1,072,710 msg/s |
| Dekaf | 2026-07-29T18:03:13.396292+00:00 | 2 | 12.0 MiB / 5.6 MiB | 390.5 MB/s | 2/5 | 8,811 | 300.2s / 1,057,910 msg/s |
| Dekaf | 2026-07-29T18:03:22.4026176+00:00 | 2 | 12.0 MiB / 5.4 MiB | 390.5 MB/s | 2/5 | 8,815 | 309.2s / 1,058,079 msg/s |
| Dekaf | 2026-07-29T18:03:31.4074348+00:00 | 2 | 12.0 MiB / 3.0 MiB | 390.5 MB/s | 2/6 | 8,866 | 318.2s / 1,034,480 msg/s |
| Dekaf | 2026-07-29T18:03:40.4116642+00:00 | 3 | 8.0 MiB / 6.2 MiB | 396.8 MB/s | 4/4 | 29,849 | 327.2s / 1,048,846 msg/s |
| Dekaf | 2026-07-29T18:03:49.4146718+00:00 | 3 | 8.0 MiB / 5.1 MiB | 396.8 MB/s | 4/4 | 30,675 | 336.2s / 1,079,232 msg/s |
| Dekaf | 2026-07-29T18:03:58.4191865+00:00 | 3 | 9.0 MiB / 3.7 MiB | 396.8 MB/s | 4/4 | 32,041 | 345.2s / 1,011,573 msg/s |
| Dekaf | 2026-07-29T18:04:07.4250707+00:00 | 3 | 8.0 MiB / 5.9 MiB | 396.8 MB/s | 4/4 | 33,294 | 354.2s / 1,054,316 msg/s |
| Dekaf | 2026-07-29T18:04:17.4267886+00:00 | 1 | 8.0 MiB / 5.9 MiB | 404.7 MB/s | 4/7 | 94,543 | 364.2s / 1,008,804 msg/s |
| Dekaf | 2026-07-29T18:04:26.4287542+00:00 | 1 | 8.0 MiB / 7.9 MiB | 404.7 MB/s | 4/7 | 97,466 | 373.2s / 1,120,874 msg/s |
| Dekaf | 2026-07-29T18:04:35.4314352+00:00 | 1 | 8.0 MiB / 7.5 MiB | 404.7 MB/s | 4/7 | 100,015 | 382.2s / 1,050,445 msg/s |
| Dekaf | 2026-07-29T18:04:44.4314863+00:00 | 1 | 8.0 MiB / 8.0 MiB | 404.7 MB/s | 4/7 | 105,154 | 391.2s / 1,026,685 msg/s |
| Dekaf | 2026-07-29T18:04:53.4343601+00:00 | 2 | 10.0 MiB / 4.9 MiB | 390.5 MB/s | 2/7 | 10,373 | 400.2s / 930,909 msg/s |
| Dekaf | 2026-07-29T18:05:02.4369022+00:00 | 2 | 12.0 MiB / 4.3 MiB | 390.5 MB/s | 2/8 | 10,492 | 409.2s / 1,089,425 msg/s |
| Dekaf | 2026-07-29T18:05:11.4389554+00:00 | 2 | 12.0 MiB / 7.3 MiB | 390.5 MB/s | 2/8 | 10,558 | 418.2s / 910,417 msg/s |
| Dekaf | 2026-07-29T18:05:20.4402966+00:00 | 2 | 12.0 MiB / 8.1 MiB | 390.5 MB/s | 2/8 | 10,596 | 427.2s / 1,057,353 msg/s |
| Dekaf | 2026-07-29T18:05:29.4432288+00:00 | 3 | 8.0 MiB / 4.2 MiB | 396.8 MB/s | 4/5 | 45,326 | 436.2s / 981,746 msg/s |
| Dekaf | 2026-07-29T18:05:38.4456155+00:00 | 3 | 8.0 MiB / 7.9 MiB | 396.8 MB/s | 4/5 | 46,037 | 445.2s / 1,080,709 msg/s |
| Dekaf | 2026-07-29T18:05:47.4478587+00:00 | 3 | 8.0 MiB / 5.9 MiB | 396.8 MB/s | 4/5 | 48,407 | 454.2s / 1,014,751 msg/s |
| Dekaf | 2026-07-29T18:05:56.455006+00:00 | 3 | 8.0 MiB / 8.0 MiB | 396.8 MB/s | 4/5 | 50,050 | 463.2s / 1,025,820 msg/s |
| Dekaf | 2026-07-29T18:06:06.4554527+00:00 | 1 | 6.0 MiB / 6.0 MiB | 407.1 MB/s | 5/8 | 152,110 | 473.2s / 1,086,293 msg/s |
| Dekaf | 2026-07-29T18:06:15.4591273+00:00 | 1 | 7.0 MiB / 7.0 MiB | 407.1 MB/s | 5/8 | 156,155 | 482.2s / 1,100,960 msg/s |
| Dekaf | 2026-07-29T18:06:24.4596346+00:00 | 1 | 7.0 MiB / 4.8 MiB | 421.6 MB/s | 5/9 | 162,125 | 491.2s / 1,127,622 msg/s |
| Dekaf | 2026-07-29T18:06:33.4636562+00:00 | 1 | 7.0 MiB / 7.0 MiB | 421.6 MB/s | 5/9 | 167,626 | 500.2s / 1,051,249 msg/s |
| Dekaf | 2026-07-29T18:06:42.4664269+00:00 | 2 | 12.0 MiB / 6.7 MiB | 399.6 MB/s | 2/8 | 13,162 | 509.2s / 1,101,713 msg/s |
| Dekaf | 2026-07-29T18:06:51.4664435+00:00 | 2 | 12.0 MiB / 4.9 MiB | 399.6 MB/s | 2/8 | 13,711 | 518.2s / 990,131 msg/s |
| Dekaf | 2026-07-29T18:07:00.4692392+00:00 | 2 | 12.0 MiB / 2.3 MiB | 399.6 MB/s | 2/8 | 13,987 | 527.2s / 1,114,547 msg/s |
| Dekaf | 2026-07-29T18:07:09.4738007+00:00 | 2 | 12.0 MiB / 1.0 MiB | 399.6 MB/s | 2/8 | 14,501 | 536.2s / 1,124,896 msg/s |
| Dekaf | 2026-07-29T18:07:18.4797971+00:00 | 3 | 6.0 MiB / 6.0 MiB | 404.2 MB/s | 5/6 | 66,070 | 545.2s / 1,109,148 msg/s |
| Dekaf | 2026-07-29T18:07:27.4834166+00:00 | 3 | 6.0 MiB / 3.0 MiB | 404.2 MB/s | 5/6 | 67,239 | 554.2s / 1,114,306 msg/s |
| Dekaf | 2026-07-29T18:07:36.4846923+00:00 | 3 | 7.0 MiB / 7.0 MiB | 404.2 MB/s | 6/6 | 68,562 | 563.2s / 1,047,764 msg/s |
| Dekaf | 2026-07-29T18:07:45.48754+00:00 | 3 | 7.0 MiB / 2.4 MiB | 404.2 MB/s | 6/6 | 71,364 | 572.2s / 1,042,166 msg/s |
| Dekaf | 2026-07-29T18:07:55.495833+00:00 | 1 | 5.0 MiB / 3.8 MiB | 421.6 MB/s | 6/10 | 230,355 | 582.2s / 1,145,319 msg/s |
| Dekaf | 2026-07-29T18:08:04.497439+00:00 | 1 | 5.0 MiB / 3.5 MiB | 421.6 MB/s | 6/10 | 236,053 | 591.2s / 1,111,574 msg/s |
| Dekaf | 2026-07-29T18:08:13.4995159+00:00 | 1 | 6.0 MiB / 5.5 MiB | 421.6 MB/s | 6/11 | 240,289 | 600.2s / 1,012,819 msg/s |
| Dekaf | 2026-07-29T18:08:22.5030977+00:00 | 1 | 6.0 MiB / 2.9 MiB | 421.6 MB/s | 6/11 | 244,059 | 609.2s / 769,813 msg/s |
| Dekaf | 2026-07-29T18:08:31.5052056+00:00 | 1 | 6.0 MiB / 5.0 MiB | 421.6 MB/s | 6/11 | 247,220 | 618.2s / 793,115 msg/s |
| Dekaf | 2026-07-29T18:08:40.5076421+00:00 | 2 | 12.0 MiB / 4.2 MiB | 403.1 MB/s | 2/8 | 15,117 | 627.2s / 1,053,541 msg/s |
| Dekaf | 2026-07-29T18:08:49.5131002+00:00 | 2 | 12.0 MiB / 3.7 MiB | 403.1 MB/s | 2/8 | 15,117 | 636.2s / 1,076,241 msg/s |
| Dekaf | 2026-07-29T18:08:58.5137937+00:00 | 2 | 13.0 MiB / 4.2 MiB | 403.1 MB/s | 2/8 | 15,117 | 645.2s / 1,154,569 msg/s |
| Dekaf | 2026-07-29T18:09:07.5163612+00:00 | 2 | 12.0 MiB / 2.4 MiB | 403.1 MB/s | 2/9 | 15,200 | 654.2s / 1,108,580 msg/s |
| Dekaf | 2026-07-29T18:09:16.5202234+00:00 | 3 | 6.0 MiB / 6.0 MiB | 404.2 MB/s | 6/9 | 94,986 | 663.3s / 1,109,403 msg/s |
| Dekaf | 2026-07-29T18:09:25.5224214+00:00 | 3 | 6.0 MiB / 5.1 MiB | 404.2 MB/s | 6/9 | 96,435 | 672.3s / 1,052,564 msg/s |
| Dekaf | 2026-07-29T18:09:34.5253389+00:00 | 3 | 6.0 MiB / 6.0 MiB | 404.2 MB/s | 6/9 | 98,065 | 681.3s / 983,679 msg/s |
| Dekaf | 2026-07-29T18:09:43.5334655+00:00 | 3 | 6.0 MiB / 2.2 MiB | 404.2 MB/s | 6/9 | 100,259 | 690.3s / 774,718 msg/s |
| Dekaf | 2026-07-29T18:09:53.5408963+00:00 | 1 | 6.0 MiB / 5.3 MiB | 421.6 MB/s | 6/12 | 289,430 | 700.3s / 1,091,517 msg/s |
| Dekaf | 2026-07-29T18:10:02.5437329+00:00 | 1 | 6.0 MiB / 3.5 MiB | 421.6 MB/s | 6/12 | 296,069 | 709.3s / 646,536 msg/s |
| Dekaf | 2026-07-29T18:10:11.547788+00:00 | 1 | 6.0 MiB / 6.0 MiB | 421.6 MB/s | 6/12 | 300,492 | 718.3s / 1,051,417 msg/s |
| Dekaf | 2026-07-29T18:10:20.5493576+00:00 | 1 | 6.0 MiB / 6.0 MiB | 421.6 MB/s | 6/12 | 303,548 | 727.3s / 799,295 msg/s |
| Dekaf | 2026-07-29T18:10:29.5548755+00:00 | 2 | 10.0 MiB / 4.2 MiB | 403.1 MB/s | 2/10 | 17,084 | 736.3s / 885,819 msg/s |
| Dekaf | 2026-07-29T18:10:38.5582812+00:00 | 2 | 10.0 MiB / 5.2 MiB | 403.1 MB/s | 3/10 | 17,491 | 745.3s / 1,025,091 msg/s |
| Dekaf | 2026-07-29T18:10:47.5624294+00:00 | 2 | 8.0 MiB / 8.0 MiB | 403.1 MB/s | 3/10 | 18,156 | 754.3s / 748,060 msg/s |
| Dekaf | 2026-07-29T18:10:56.5673312+00:00 | 2 | 8.0 MiB / 3.9 MiB | 403.1 MB/s | 4/10 | 18,644 | 763.3s / 1,109,571 msg/s |
| Dekaf | 2026-07-29T18:11:05.5712939+00:00 | 3 | 6.0 MiB / 3.5 MiB | 404.2 MB/s | 6/9 | 120,972 | 772.3s / 756,636 msg/s |
| Dekaf | 2026-07-29T18:11:14.5756606+00:00 | 3 | 6.0 MiB / 5.4 MiB | 404.2 MB/s | 6/9 | 121,692 | 781.3s / 816,554 msg/s |
| Dekaf | 2026-07-29T18:11:23.579929+00:00 | 3 | 6.0 MiB / 3.6 MiB | 404.2 MB/s | 6/9 | 123,682 | 790.3s / 982,862 msg/s |
| Dekaf | 2026-07-29T18:11:32.5819038+00:00 | 3 | 6.0 MiB / 4.3 MiB | 404.2 MB/s | 6/9 | 125,787 | 799.3s / 934,126 msg/s |
| Dekaf | 2026-07-29T18:11:42.5820194+00:00 | 1 | 6.0 MiB / 4.2 MiB | 421.6 MB/s | 6/12 | 337,842 | 809.3s / 1,041,105 msg/s |
| Dekaf | 2026-07-29T18:11:51.5879543+00:00 | 1 | 6.0 MiB / 2.0 MiB | 421.6 MB/s | 6/12 | 341,014 | 818.3s / 1,014,440 msg/s |
| Dekaf | 2026-07-29T18:12:00.5916164+00:00 | 1 | 6.0 MiB / 6.0 MiB | 421.6 MB/s | 6/12 | 345,087 | 827.3s / 1,055,107 msg/s |
| Dekaf | 2026-07-29T18:12:09.5934428+00:00 | 1 | 6.0 MiB / 4.9 MiB | 421.6 MB/s | 6/12 | 349,571 | 836.3s / 1,082,233 msg/s |
| Dekaf | 2026-07-29T18:12:18.5961493+00:00 | 2 | 9.0 MiB / 5.6 MiB | 403.1 MB/s | 5/11 | 20,788 | 845.3s / 1,133,779 msg/s |
| Dekaf | 2026-07-29T18:12:27.6004782+00:00 | 2 | 9.0 MiB / 5.8 MiB | 403.1 MB/s | 5/11 | 20,833 | 854.3s / 1,136,417 msg/s |
| Dekaf | 2026-07-29T18:12:36.602486+00:00 | 2 | 9.0 MiB / 7.9 MiB | 403.1 MB/s | 5/11 | 21,056 | 863.3s / 1,158,265 msg/s |
| Dekaf | 2026-07-29T18:12:45.6046109+00:00 | 2 | 9.0 MiB / 3.1 MiB | 403.1 MB/s | 5/11 | 21,197 | 872.3s / 1,130,966 msg/s |
| Dekaf | 2026-07-29T18:12:54.6061127+00:00 | 3 | 6.0 MiB / 5.8 MiB | 404.2 MB/s | 6/9 | 149,419 | 881.3s / 913,403 msg/s |
| Dekaf | 2026-07-29T18:13:03.6120117+00:00 | 3 | 6.0 MiB / 4.2 MiB | 404.2 MB/s | 6/9 | 151,706 | 890.3s / 1,122,538 msg/s |
| Dekaf | 2026-07-29T18:13:12.6135527+00:00 | 3 | 6.0 MiB / 3.6 MiB | 404.2 MB/s | 6/9 | 153,414 | 899.3s / 1,118,219 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T17:58:43.5306711+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T17:58:43.5395297+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T17:58:43.5976996+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T17:58:58.5830719+00:00 | 3 | capacity | succeeded | 15,053ms | 14.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-29T17:58:58.5951629+00:00 | 2 | capacity | succeeded | 15,056ms | 14.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-29T17:58:58.6649408+00:00 | 1 | capacity | succeeded | 15,067ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T17:59:01.5906848+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-29T17:59:01.6044233+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T17:59:01.6764168+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-29T17:59:16.6572338+00:00 | 3 | capacity | failed | 15,066ms | 14.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-29T17:59:16.6580687+00:00 | 2 | capacity | failed | 15,053ms | 14.0 MiB / 6.5 MiB |
| Dekaf | 2026-07-29T17:59:16.7522933+00:00 | 1 | capacity | failed | 15,075ms | 14.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-29T17:59:46.760243+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T17:59:48.3632001+00:00 | 1 | capacity | failed | 1,507ms | 14.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T17:59:48.7684325+00:00 | 2 | capacity | failed | 2,008ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-29T18:00:16.8989639+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-29T18:00:18.5893018+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 8.5 MiB |
| Dekaf | 2026-07-29T18:00:18.9012235+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T18:00:32.0127071+00:00 | 3 | capacity | succeeded | 15,113ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:00:33.6651227+00:00 | 1 | capacity | succeeded | 15,075ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T18:00:34.0001544+00:00 | 2 | capacity | succeeded | 15,098ms | 12.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-29T18:00:35.0264871+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-29T18:00:36.6866196+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.4 MiB |
| Dekaf | 2026-07-29T18:00:37.0115356+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-29T18:00:50.0975832+00:00 | 3 | capacity | succeeded | 15,071ms | 10.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-29T18:00:51.7596582+00:00 | 1 | capacity | failed | 15,073ms | 12.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-29T18:00:52.0893324+00:00 | 2 | capacity | failed | 15,077ms | 12.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-29T18:00:53.111216+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-29T18:01:08.1767758+00:00 | 3 | capacity | failed | 15,065ms | 10.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-29T18:01:21.8999095+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-29T18:01:23.4064252+00:00 | 1 | capacity | failed | 1,506ms | 12.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-29T18:01:38.3023516+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-29T18:01:52.3221657+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-29T18:01:53.3592145+00:00 | 3 | capacity | failed | 15,056ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T18:01:53.5705326+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.0 MiB |
| Dekaf | 2026-07-29T18:02:07.3809389+00:00 | 2 | capacity | failed | 15,058ms | 12.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T18:02:08.6256379+00:00 | 1 | capacity | failed | 15,055ms | 12.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-29T18:02:23.5080758+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-29T18:02:38.570979+00:00 | 3 | capacity | succeeded | 15,062ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-29T18:02:38.7597571+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-29T18:02:40.266075+00:00 | 1 | capacity | failed | 1,506ms | 12.0 MiB / 1.3 MiB |
| Dekaf | 2026-07-29T18:02:41.581954+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-29T18:02:52.6160562+00:00 | 2 | capacity | failed | 15,058ms | 12.0 MiB / 6.0 MiB |
| Dekaf | 2026-07-29T18:02:56.635748+00:00 | 3 | capacity | failed | 15,053ms | 8.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T18:03:10.3757478+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:03:22.7130486+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T18:03:25.2232889+00:00 | 2 | capacity | failed | 2,510ms | 12.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-29T18:03:25.4327532+00:00 | 1 | capacity | succeeded | 15,056ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T18:03:28.4708857+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T18:03:43.524877+00:00 | 1 | capacity | succeeded | 15,054ms | 8.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-29T18:03:46.5300222+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.0 MiB |
| Dekaf | 2026-07-29T18:03:55.3224309+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-29T18:03:56.8570894+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-29T18:04:01.584852+00:00 | 1 | capacity | failed | 15,054ms | 8.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-29T18:04:10.3731711+00:00 | 2 | capacity | failed | 15,050ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T18:04:11.9090534+00:00 | 3 | capacity | failed | 15,051ms | 8.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T18:04:40.5039363+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T18:04:55.5581655+00:00 | 2 | capacity | failed | 15,054ms | 12.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T18:05:01.7910615+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-29T18:05:16.8418307+00:00 | 1 | capacity | failed | 15,050ms | 8.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:05:46.9851148+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.3 MiB |
| Dekaf | 2026-07-29T18:06:02.0399721+00:00 | 1 | capacity | succeeded | 15,055ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:06:12.3332846+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-29T18:06:20.1010436+00:00 | 1 | capacity | failed | 15,050ms | 7.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-29T18:06:27.3689478+00:00 | 3 | capacity | failed | 15,035ms | 8.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-29T18:06:50.1921632+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:06:57.4890883+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-29T18:07:05.247371+00:00 | 1 | capacity | succeeded | 15,055ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:07:08.2664853+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T18:07:12.5325449+00:00 | 3 | capacity | succeeded | 15,043ms | 7.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-29T18:07:15.5373702+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T18:07:23.3146872+00:00 | 1 | capacity | failed | 15,048ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T18:07:30.5731651+00:00 | 3 | capacity | succeeded | 15,035ms | 6.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-29T18:07:33.5840001+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T18:07:48.6225026+00:00 | 3 | capacity | failed | 15,038ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:07:53.4060394+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:08:08.4900492+00:00 | 1 | capacity | failed | 15,084ms | 6.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-29T18:08:18.7441936+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-29T18:08:20.2466292+00:00 | 3 | capacity | failed | 1,502ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:08:38.5851573+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T18:08:50.3595813+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:08:53.6253967+00:00 | 1 | capacity | failed | 15,040ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T18:08:56.3789088+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-29T18:08:59.8841501+00:00 | 2 | capacity | failed | 3,505ms | 12.0 MiB / 5.3 MiB |
| Dekaf | 2026-07-29T18:09:05.4086682+00:00 | 3 | capacity | failed | 15,049ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T18:09:30.0251977+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-29T18:09:45.0925004+00:00 | 2 | capacity | failed | 15,067ms | 12.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T18:10:30.2715925+00:00 | 2 | capacity | succeeded | 15,063ms | 10.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-29T18:10:33.286864+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-29T18:10:48.3411366+00:00 | 2 | capacity | succeeded | 15,054ms | 8.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-29T18:10:51.3585432+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T18:11:06.4074575+00:00 | 2 | capacity | succeeded | 15,048ms | 9.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T18:11:36.5617295+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-29T18:11:51.6120183+00:00 | 2 | capacity | failed | 15,050ms | 9.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-29T18:12:51.8074691+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-29T18:12:54.5386482+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:13:06.3101758+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-29T18:13:06.8564693+00:00 | 2 | capacity | failed | 15,045ms | 9.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-29T18:13:09.5888409+00:00 | 1 | capacity | succeeded | 15,050ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T18:13:12.5936876+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 3.9 MiB |
*4 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 73 |
| Dekaf | 1 | 0.002–0.004ms | 96 |
| Dekaf | 1 | 0.004–0.008ms | 287 |
| Dekaf | 1 | 0.008–0.016ms | 799 |
| Dekaf | 1 | 0.016–0.032ms | 1,840 |
| Dekaf | 1 | 0.032–0.064ms | 3,019 |
| Dekaf | 1 | 0.064–0.128ms | 4,622 |
| Dekaf | 1 | 0.128–0.256ms | 7,914 |
| Dekaf | 1 | 0.256–0.512ms | 15,670 |
| Dekaf | 1 | 0.512–1.024ms | 25,294 |
| Dekaf | 1 | 1.024–2.048ms | 20,542 |
| Dekaf | 1 | 2.048–4.096ms | 9,353 |
| Dekaf | 1 | 4.096–8.192ms | 4,139 |
| Dekaf | 1 | 8.192–16.384ms | 1,197 |
| Dekaf | 1 | 16.384–32.768ms | 439 |
| Dekaf | 1 | 32.768–65.536ms | 63 |
| Dekaf | 2 | 0.001–0.002ms | 1 |
| Dekaf | 2 | 0.002–0.004ms | 2 |
| Dekaf | 2 | 0.004–0.008ms | 21 |
| Dekaf | 2 | 0.008–0.016ms | 49 |
| Dekaf | 2 | 0.016–0.032ms | 120 |
| Dekaf | 2 | 0.032–0.064ms | 162 |
| Dekaf | 2 | 0.064–0.128ms | 214 |
| Dekaf | 2 | 0.128–0.256ms | 358 |
| Dekaf | 2 | 0.256–0.512ms | 719 |
| Dekaf | 2 | 0.512–1.024ms | 1,150 |
| Dekaf | 2 | 1.024–2.048ms | 1,151 |
| Dekaf | 2 | 2.048–4.096ms | 703 |
| Dekaf | 2 | 4.096–8.192ms | 356 |
| Dekaf | 2 | 8.192–16.384ms | 101 |
| Dekaf | 2 | 16.384–32.768ms | 38 |
| Dekaf | 2 | 32.768–65.536ms | 6 |
| Dekaf | 3 | 0.001–0.002ms | 41 |
| Dekaf | 3 | 0.002–0.004ms | 36 |
| Dekaf | 3 | 0.004–0.008ms | 139 |
| Dekaf | 3 | 0.008–0.016ms | 326 |
| Dekaf | 3 | 0.016–0.032ms | 826 |
| Dekaf | 3 | 0.032–0.064ms | 1,331 |
| Dekaf | 3 | 0.064–0.128ms | 2,012 |
| Dekaf | 3 | 0.128–0.256ms | 3,039 |
| Dekaf | 3 | 0.256–0.512ms | 5,946 |
| Dekaf | 3 | 0.512–1.024ms | 8,851 |
| Dekaf | 3 | 1.024–2.048ms | 7,726 |
| Dekaf | 3 | 2.048–4.096ms | 3,927 |
| Dekaf | 3 | 4.096–8.192ms | 1,952 |
| Dekaf | 3 | 8.192–16.384ms | 713 |
| Dekaf | 3 | 16.384–32.768ms | 345 |
| Dekaf | 3 | 32.768–65.536ms | 30 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 29,000 | 2026-07-29T17:58:13.3519472+00:00 | 121.7ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 30,000 | 2026-07-29T17:58:13.3534196+00:00 | 105.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 39,000 | 2026-07-29T17:58:13.3651303+00:00 | 128.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 40,000 | 2026-07-29T17:58:13.3666273+00:00 | 139.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 42,000 | 2026-07-29T17:58:13.3695741+00:00 | 106.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 53,000 | 2026-07-29T17:58:13.3865422+00:00 | 167.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 60,000 | 2026-07-29T17:58:13.4004394+00:00 | 154.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 61,000 | 2026-07-29T17:58:13.4014705+00:00 | 135.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 64,000 | 2026-07-29T17:58:13.404659+00:00 | 164.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 66,000 | 2026-07-29T17:58:13.407848+00:00 | 178.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 69,000 | 2026-07-29T17:58:13.4434405+00:00 | 130.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 71,000 | 2026-07-29T17:58:13.4590839+00:00 | 128.1ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 72,000 | 2026-07-29T17:58:13.463329+00:00 | 123.7ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 73,000 | 2026-07-29T17:58:13.4644331+00:00 | 123.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 74,000 | 2026-07-29T17:58:13.4652809+00:00 | 134.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 76,000 | 2026-07-29T17:58:13.4709105+00:00 | 129.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 81,000 | 2026-07-29T17:58:13.4840693+00:00 | 116.7ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 82,000 | 2026-07-29T17:58:13.4934335+00:00 | 107.3ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 90,000 | 2026-07-29T17:58:13.5092295+00:00 | 126.1ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 92,000 | 2026-07-29T17:58:13.5119716+00:00 | 125.5ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 93,000 | 2026-07-29T17:58:13.5129116+00:00 | 103.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 96,000 | 2026-07-29T17:58:13.518881+00:00 | 197.1ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 99,000 | 2026-07-29T17:58:13.5225141+00:00 | 206.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 100,000 | 2026-07-29T17:58:13.5546302+00:00 | 175.2ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 103,000 | 2026-07-29T17:58:13.5579696+00:00 | 209.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 110,000 | 2026-07-29T17:58:13.5656469+00:00 | 205.3ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 116,000 | 2026-07-29T17:58:13.5750702+00:00 | 263.8ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 117,000 | 2026-07-29T17:58:13.5764514+00:00 | 126.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 118,000 | 2026-07-29T17:58:13.5891376+00:00 | 127.7ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 119,000 | 2026-07-29T17:58:13.5909635+00:00 | 191.5ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 120,000 | 2026-07-29T17:58:13.5918796+00:00 | 202.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 121,000 | 2026-07-29T17:58:13.5949+00:00 | 273.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 122,000 | 2026-07-29T17:58:13.5963946+00:00 | 271.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 125,000 | 2026-07-29T17:58:13.6013375+00:00 | 120.1ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 130,000 | 2026-07-29T17:58:13.6075773+00:00 | 200.1ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 132,000 | 2026-07-29T17:58:13.6149816+00:00 | 287.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 135,000 | 2026-07-29T17:58:13.6387262+00:00 | 101.5ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 140,000 | 2026-07-29T17:58:13.7152231+00:00 | 121.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 141,000 | 2026-07-29T17:58:13.7160591+00:00 | 193.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 146,000 | 2026-07-29T17:58:13.7835934+00:00 | 160.1ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 152,000 | 2026-07-29T17:58:13.7913586+00:00 | 137.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 171,000 | 2026-07-29T17:58:13.8697756+00:00 | 134.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 176,000 | 2026-07-29T17:58:13.885369+00:00 | 137.8ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 181,000 | 2026-07-29T17:58:13.8913896+00:00 | 144.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 184,000 | 2026-07-29T17:58:13.8995535+00:00 | 140.2ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 186,000 | 2026-07-29T17:58:13.901814+00:00 | 138.0ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 190,000 | 2026-07-29T17:58:13.9136456+00:00 | 108.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 192,000 | 2026-07-29T17:58:13.9174808+00:00 | 161.7ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 193,000 | 2026-07-29T17:58:13.9285634+00:00 | 111.5ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 194,000 | 2026-07-29T17:58:13.9296323+00:00 | 127.3ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 200,000 | 2026-07-29T17:58:13.9453012+00:00 | 145.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 204,000 | 2026-07-29T17:58:13.9529503+00:00 | 140.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 206,000 | 2026-07-29T17:58:13.9559646+00:00 | 137.8ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 209,000 | 2026-07-29T17:58:13.9692661+00:00 | 148.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 219,000 | 2026-07-29T17:58:14.0113839+00:00 | 115.2ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 220,000 | 2026-07-29T17:58:14.0130833+00:00 | 113.5ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 231,000 | 2026-07-29T17:58:14.0366777+00:00 | 126.6ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 232,000 | 2026-07-29T17:58:14.0380779+00:00 | 125.2ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 234,000 | 2026-07-29T17:58:14.0454178+00:00 | 134.7ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 239,000 | 2026-07-29T17:58:14.0547353+00:00 | 133.5ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 240,000 | 2026-07-29T17:58:14.0596523+00:00 | 129.4ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 242,000 | 2026-07-29T17:58:14.0632101+00:00 | 116.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 246,000 | 2026-07-29T17:58:14.0895129+00:00 | 110.9ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 253,000 | 2026-07-29T17:58:14.0962175+00:00 | 164.2ms | GC pause | - | - | 1.0s / 303,859 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 260,000 | 2026-07-29T17:58:14.1275416+00:00 | 162.8ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 261,000 | 2026-07-29T17:58:14.1282296+00:00 | 152.8ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 262,000 | 2026-07-29T17:58:14.1290553+00:00 | 152.0ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 263,000 | 2026-07-29T17:58:14.1295714+00:00 | 153.3ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 264,000 | 2026-07-29T17:58:14.1305152+00:00 | 150.5ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 266,000 | 2026-07-29T17:58:14.1318273+00:00 | 149.2ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 269,000 | 2026-07-29T17:58:14.1408622+00:00 | 177.6ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 270,000 | 2026-07-29T17:58:14.1419503+00:00 | 176.5ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 272,000 | 2026-07-29T17:58:14.1546512+00:00 | 182.2ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 274,000 | 2026-07-29T17:58:14.1567542+00:00 | 180.6ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 280,000 | 2026-07-29T17:58:14.1719573+00:00 | 161.9ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 281,000 | 2026-07-29T17:58:14.1724759+00:00 | 164.9ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 291,000 | 2026-07-29T17:58:14.1946831+00:00 | 171.1ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 293,000 | 2026-07-29T17:58:14.2100375+00:00 | 165.5ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 296,000 | 2026-07-29T17:58:14.212942+00:00 | 169.7ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 298,000 | 2026-07-29T17:58:14.2173196+00:00 | 104.3ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 299,000 | 2026-07-29T17:58:14.2611529+00:00 | 126.5ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 302,000 | 2026-07-29T17:58:14.2632313+00:00 | 129.9ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 303,000 | 2026-07-29T17:58:14.2638729+00:00 | 134.5ms | GC pause | - | - | 2.0s / 457,108 msg/s | Gen2 +1 / pause +0.6ms |
| Dekaf | 306,000 | 2026-07-29T17:58:14.2841326+00:00 | 122.3ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 310,000 | 2026-07-29T17:58:14.2868481+00:00 | 133.4ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 312,000 | 2026-07-29T17:58:14.2926471+00:00 | 113.8ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 320,000 | 2026-07-29T17:58:14.3226923+00:00 | 132.6ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 322,000 | 2026-07-29T17:58:14.3376617+00:00 | 100.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 323,000 | 2026-07-29T17:58:14.338108+00:00 | 117.1ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 326,000 | 2026-07-29T17:58:14.3406012+00:00 | 101.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 329,000 | 2026-07-29T17:58:14.3422902+00:00 | 119.3ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 330,000 | 2026-07-29T17:58:14.3430551+00:00 | 112.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 339,000 | 2026-07-29T17:58:14.3816001+00:00 | 121.6ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 340,000 | 2026-07-29T17:58:14.3831518+00:00 | 120.0ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 342,000 | 2026-07-29T17:58:14.3861956+00:00 | 104.5ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 353,000 | 2026-07-29T17:58:14.4057336+00:00 | 157.5ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 359,000 | 2026-07-29T17:58:14.4228834+00:00 | 146.8ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 363,000 | 2026-07-29T17:58:14.43279+00:00 | 165.6ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 364,000 | 2026-07-29T17:58:14.4349142+00:00 | 127.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 379,000 | 2026-07-29T17:58:14.466936+00:00 | 137.4ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 383,000 | 2026-07-29T17:58:14.5024938+00:00 | 117.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 390,000 | 2026-07-29T17:58:14.5114138+00:00 | 117.9ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 391,000 | 2026-07-29T17:58:14.5120451+00:00 | 102.4ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 489,000 | 2026-07-29T17:58:14.7066882+00:00 | 141.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 490,000 | 2026-07-29T17:58:14.7071716+00:00 | 150.3ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 493,000 | 2026-07-29T17:58:14.715748+00:00 | 132.1ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 499,000 | 2026-07-29T17:58:14.7317501+00:00 | 124.4ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 501,000 | 2026-07-29T17:58:14.7330211+00:00 | 110.4ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 502,000 | 2026-07-29T17:58:14.7332942+00:00 | 110.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 504,000 | 2026-07-29T17:58:14.7362938+00:00 | 111.6ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 509,000 | 2026-07-29T17:58:14.7534463+00:00 | 109.1ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 512,000 | 2026-07-29T17:58:14.7600838+00:00 | 104.2ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 516,000 | 2026-07-29T17:58:14.7620748+00:00 | 106.4ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 520,000 | 2026-07-29T17:58:14.7659366+00:00 | 141.5ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 529,000 | 2026-07-29T17:58:14.7853573+00:00 | 123.6ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 549,000 | 2026-07-29T17:58:14.8726701+00:00 | 101.8ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 563,000 | 2026-07-29T17:58:14.8876757+00:00 | 107.8ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 570,000 | 2026-07-29T17:58:14.907862+00:00 | 114.7ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 579,000 | 2026-07-29T17:58:14.9282923+00:00 | 113.3ms | throughput collapse | - | - | 2.0s / 457,108 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 769,000 | 2026-07-29T17:58:15.2750779+00:00 | 106.8ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 783,000 | 2026-07-29T17:58:15.3012118+00:00 | 109.6ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 810,000 | 2026-07-29T17:58:15.3645315+00:00 | 168.9ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 813,000 | 2026-07-29T17:58:15.3808988+00:00 | 145.5ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 819,000 | 2026-07-29T17:58:15.3839159+00:00 | 140.5ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 823,000 | 2026-07-29T17:58:15.3941921+00:00 | 138.8ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 843,000 | 2026-07-29T17:58:15.448184+00:00 | 134.3ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 860,000 | 2026-07-29T17:58:15.5257046+00:00 | 103.0ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 869,000 | 2026-07-29T17:58:15.5373311+00:00 | 109.6ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 879,000 | 2026-07-29T17:58:15.5534943+00:00 | 110.0ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 880,000 | 2026-07-29T17:58:15.5541379+00:00 | 109.7ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,043,000 | 2026-07-29T17:58:15.8469012+00:00 | 148.0ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,049,000 | 2026-07-29T17:58:15.8631016+00:00 | 142.0ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,050,000 | 2026-07-29T17:58:15.8663205+00:00 | 131.9ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,053,000 | 2026-07-29T17:58:15.8690083+00:00 | 136.1ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,060,000 | 2026-07-29T17:58:15.8785316+00:00 | 152.4ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,063,000 | 2026-07-29T17:58:15.8850761+00:00 | 145.4ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,065,000 | 2026-07-29T17:58:15.8861989+00:00 | 124.1ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,067,000 | 2026-07-29T17:58:15.8871636+00:00 | 114.9ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,069,000 | 2026-07-29T17:58:15.8939272+00:00 | 151.6ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,074,000 | 2026-07-29T17:58:15.8965935+00:00 | 116.7ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,077,000 | 2026-07-29T17:58:15.9054454+00:00 | 120.9ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,079,000 | 2026-07-29T17:58:15.9097529+00:00 | 158.7ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,080,000 | 2026-07-29T17:58:15.9100432+00:00 | 143.7ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,082,000 | 2026-07-29T17:58:15.911002+00:00 | 103.7ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,083,000 | 2026-07-29T17:58:15.9191276+00:00 | 149.4ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,084,000 | 2026-07-29T17:58:15.9203597+00:00 | 110.6ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,085,000 | 2026-07-29T17:58:15.9206912+00:00 | 116.1ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,091,000 | 2026-07-29T17:58:15.9398855+00:00 | 101.9ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,109,000 | 2026-07-29T17:58:16.0313943+00:00 | 125.7ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,113,000 | 2026-07-29T17:58:16.0334455+00:00 | 123.6ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,130,000 | 2026-07-29T17:58:16.0694493+00:00 | 143.4ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,150,000 | 2026-07-29T17:58:16.1461052+00:00 | 103.0ms | throughput collapse | - | - | 3.0s / 447,388 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,170,000 | 2026-07-29T17:58:16.1930192+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 625,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,179,000 | 2026-07-29T17:58:16.2174567+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 625,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,603,000 | 2026-07-29T17:58:16.8903474+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 625,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,630,000 | 2026-07-29T17:58:16.9381207+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 625,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,901,000 | 2026-07-29T17:58:17.3576169+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,911,000 | 2026-07-29T17:58:17.3720769+00:00 | 146.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,920,000 | 2026-07-29T17:58:17.3808745+00:00 | 139.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,921,000 | 2026-07-29T17:58:17.3813798+00:00 | 162.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,924,000 | 2026-07-29T17:58:17.3830673+00:00 | 160.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,932,000 | 2026-07-29T17:58:17.3984372+00:00 | 154.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,936,000 | 2026-07-29T17:58:17.4021848+00:00 | 150.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,938,000 | 2026-07-29T17:58:17.4032726+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,942,000 | 2026-07-29T17:58:17.4057577+00:00 | 158.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,945,000 | 2026-07-29T17:58:17.4080021+00:00 | 117.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,946,000 | 2026-07-29T17:58:17.408602+00:00 | 168.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 644,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,593,000 | 2026-07-29T17:58:18.4402145+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 740,125 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,930,000 | 2026-07-29T17:58:18.887733+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 740,125 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,020,000 | 2026-07-29T17:58:20.3502374+00:00 | 117.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 712,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,033,000 | 2026-07-29T17:58:20.3799585+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 712,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,040,000 | 2026-07-29T17:58:20.3925452+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 712,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,049,000 | 2026-07-29T17:58:20.4065855+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 712,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,050,000 | 2026-07-29T17:58:20.4072323+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 712,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,053,000 | 2026-07-29T17:58:20.4085143+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 712,019 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,179,000 | 2026-07-29T17:58:21.9129311+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 748,981 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,189,000 | 2026-07-29T17:58:21.9203172+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 748,981 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,430,000 | 2026-07-29T17:58:22.2926317+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,450,000 | 2026-07-29T17:58:22.3245535+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,453,000 | 2026-07-29T17:58:22.3261385+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,731,000 | 2026-07-29T17:58:22.7797349+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,781,000 | 2026-07-29T17:58:22.8798718+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,784,000 | 2026-07-29T17:58:22.8817174+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,786,000 | 2026-07-29T17:58:22.8848636+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,801,000 | 2026-07-29T17:58:22.9035163+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,802,000 | 2026-07-29T17:58:22.903823+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 639,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,712,000 | 2026-07-29T17:58:25.4011007+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 732,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,024,000 | 2026-07-29T17:58:25.7775672+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 732,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,116,000 | 2026-07-29T17:58:25.9274257+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 732,447 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,354,000 | 2026-07-29T17:58:27.4129703+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 879,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,503,000 | 2026-07-29T17:58:29.8988187+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 808,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,509,000 | 2026-07-29T17:58:29.9030637+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 808,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,510,000 | 2026-07-29T17:58:29.9036315+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 808,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,529,000 | 2026-07-29T17:58:29.927381+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 808,314 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,467,000 | 2026-07-29T17:58:35.3836393+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 905,518 msg/s | Gen2 +0 / pause +2.0ms |
| Dekaf | 16,468,000 | 2026-07-29T17:58:35.3850436+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 905,518 msg/s | Gen2 +0 / pause +2.0ms |
| Dekaf | 16,485,000 | 2026-07-29T17:58:35.4013087+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 905,518 msg/s | Gen2 +0 / pause +2.0ms |
| Dekaf | 16,487,000 | 2026-07-29T17:58:35.4106554+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 905,518 msg/s | Gen2 +0 / pause +2.0ms |
| Dekaf | 17,430,000 | 2026-07-29T17:58:36.426563+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 882,761 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,443,000 | 2026-07-29T17:58:36.4337845+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 882,761 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,449,000 | 2026-07-29T17:58:36.4364692+00:00 | 114.8ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 882,761 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,611,000 | 2026-07-29T17:58:37.8661983+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 725,083 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,624,000 | 2026-07-29T17:58:37.8941049+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 725,083 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,631,000 | 2026-07-29T17:58:37.9092578+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 725,083 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,641,000 | 2026-07-29T17:58:37.9274762+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 725,083 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,646,000 | 2026-07-29T17:58:37.9364859+00:00 | 120.2ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 725,083 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,656,000 | 2026-07-29T17:58:37.9567456+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 725,083 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,588,000 | 2026-07-29T17:58:43.3849508+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 984,148 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,723,000 | 2026-07-29T17:58:55.3851136+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/succeeded, 1:capacity/succeeded | - | 43.0s / 1,047,578 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,031,000 | 2026-07-29T17:59:03.3709996+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,036,000 | 2026-07-29T17:59:03.3791281+00:00 | 109.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,405,000 | 2026-07-29T17:59:03.8963619+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,406,000 | 2026-07-29T17:59:03.896655+00:00 | 102.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,408,000 | 2026-07-29T17:59:03.8975464+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,418,000 | 2026-07-29T17:59:03.9115717+00:00 | 107.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,425,000 | 2026-07-29T17:59:03.9277563+00:00 | 104.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 51.0s / 669,063 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,719,000 | 2026-07-29T17:59:13.1853673+00:00 | 113.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 60.0s / 908,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,720,000 | 2026-07-29T17:59:13.1870505+00:00 | 113.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/failed | - | 60.0s / 908,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 90,564,000 | 2026-07-29T17:59:50.9192869+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 98.1s / 641,215 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,530,000 | 2026-07-29T17:59:52.3787208+00:00 | 127.8ms | broker/backlog (no scale or GC event) | - | - | 100.1s / 677,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,534,000 | 2026-07-29T17:59:52.3825164+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 100.1s / 677,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,540,000 | 2026-07-29T17:59:52.3886081+00:00 | 125.6ms | broker/backlog (no scale or GC event) | - | - | 100.1s / 677,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,541,000 | 2026-07-29T17:59:52.3895401+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 100.1s / 677,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 91,554,000 | 2026-07-29T17:59:52.4179541+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 100.1s / 677,599 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 92,563,000 | 2026-07-29T17:59:53.8713379+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,566,000 | 2026-07-29T17:59:53.8795647+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,569,000 | 2026-07-29T17:59:53.8827065+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,570,000 | 2026-07-29T17:59:53.8896617+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,571,000 | 2026-07-29T17:59:53.8909546+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,572,000 | 2026-07-29T17:59:53.8930011+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,573,000 | 2026-07-29T17:59:53.8982999+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,576,000 | 2026-07-29T17:59:53.9010237+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,581,000 | 2026-07-29T17:59:53.9070223+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,594,000 | 2026-07-29T17:59:53.9385703+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 92,596,000 | 2026-07-29T17:59:53.9393746+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 101.1s / 662,744 msg/s | Gen2 +0 / pause +1.4ms |
| Dekaf | 97,450,000 | 2026-07-29T18:00:00.8735274+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 108.1s / 703,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,453,000 | 2026-07-29T18:00:00.8805725+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 108.1s / 703,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,459,000 | 2026-07-29T18:00:00.8894679+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 108.1s / 703,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,463,000 | 2026-07-29T18:00:00.8977721+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 108.1s / 703,786 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 101,535,000 | 2026-07-29T18:00:06.3865757+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 114.1s / 713,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 101,557,000 | 2026-07-29T18:00:06.415891+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 114.1s / 713,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 114,369,000 | 2026-07-29T18:00:24.4112247+00:00 | 109.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded, 2:capacity/succeeded | - | 132.1s / 703,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 114,373,000 | 2026-07-29T18:00:24.4177874+00:00 | 109.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded, 2:capacity/succeeded | - | 132.1s / 703,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 114,383,000 | 2026-07-29T18:00:24.4249414+00:00 | 123.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded, 2:capacity/succeeded | - | 132.1s / 703,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 114,389,000 | 2026-07-29T18:00:24.4294672+00:00 | 119.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded, 2:capacity/succeeded | - | 132.1s / 703,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 219,069,000 | 2026-07-29T18:02:43.9043902+00:00 | 104.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 271.2s / 858,409 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 219,090,000 | 2026-07-29T18:02:43.9242271+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 271.2s / 858,409 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 219,093,000 | 2026-07-29T18:02:43.9360016+00:00 | 100.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 271.2s / 858,409 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 241,980,000 | 2026-07-29T18:03:05.9014504+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 293.2s / 681,288 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 241,999,000 | 2026-07-29T18:03:05.9216993+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 293.2s / 681,288 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 296,819,000 | 2026-07-29T18:03:59.9091611+00:00 | 104.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed, 3:capacity/failed | - | 347.2s / 873,501 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 387,109,000 | 2026-07-29T18:05:27.9089206+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 435.2s / 940,486 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 387,113,000 | 2026-07-29T18:05:27.9142255+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 435.2s / 940,486 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 434,495,000 | 2026-07-29T18:06:13.8817861+00:00 | 108.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 481.2s / 706,483 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 434,508,000 | 2026-07-29T18:06:13.9067965+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 481.2s / 706,483 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 434,518,000 | 2026-07-29T18:06:13.9179951+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed | - | 481.2s / 706,483 msg/s | Gen2 +0 / pause +0.4ms |
| Confluent | 125,122,000 | 2026-07-29T18:15:51.5106148+00:00 | 109.4ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,125,000 | 2026-07-29T18:15:51.5175779+00:00 | 117.3ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,126,000 | 2026-07-29T18:15:51.519574+00:00 | 115.4ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,134,000 | 2026-07-29T18:15:51.5293923+00:00 | 102.0ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,136,000 | 2026-07-29T18:15:51.5309444+00:00 | 126.1ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,139,000 | 2026-07-29T18:15:51.5558495+00:00 | 123.1ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,144,000 | 2026-07-29T18:15:51.5677347+00:00 | 100.9ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +278.1ms |
| Confluent | 125,152,000 | 2026-07-29T18:15:51.5978959+00:00 | 114.5ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,155,000 | 2026-07-29T18:15:51.6136784+00:00 | 108.7ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,156,000 | 2026-07-29T18:15:51.6152522+00:00 | 107.1ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,166,000 | 2026-07-29T18:15:51.6596709+00:00 | 107.0ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,172,000 | 2026-07-29T18:15:51.6801034+00:00 | 102.9ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,326,000 | 2026-07-29T18:15:52.2012144+00:00 | 112.7ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,335,000 | 2026-07-29T18:15:52.2266093+00:00 | 107.4ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,336,000 | 2026-07-29T18:15:52.2273001+00:00 | 106.8ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 125,349,000 | 2026-07-29T18:15:52.2464605+00:00 | 102.6ms | GC pause | - | - | 159.1s / 358,171 msg/s | Gen2 +0 / pause +161.1ms |
| Confluent | 140,584,000 | 2026-07-29T18:16:16.9525074+00:00 | 100.4ms | GC pause | - | - | 184.1s / 692,005 msg/s | Gen2 +0 / pause +78.3ms |
| Confluent | 150,450,000 | 2026-07-29T18:16:34.9620069+00:00 | 103.0ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,454,000 | 2026-07-29T18:16:34.9666997+00:00 | 113.7ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,768,000 | 2026-07-29T18:16:35.4398434+00:00 | 103.2ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,771,000 | 2026-07-29T18:16:35.4418887+00:00 | 102.1ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,790,000 | 2026-07-29T18:16:35.4569677+00:00 | 105.2ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,793,000 | 2026-07-29T18:16:35.4613765+00:00 | 101.1ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,810,000 | 2026-07-29T18:16:35.4861206+00:00 | 109.2ms | GC pause | - | - | 202.1s / 701,607 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 150,818,000 | 2026-07-29T18:16:35.5011363+00:00 | 105.5ms | GC pause | - | - | 203.1s / 691,396 msg/s | Gen2 +0 / pause +149.8ms |
| Confluent | 151,854,000 | 2026-07-29T18:16:36.9932454+00:00 | 122.7ms | GC pause | - | - | 204.1s / 701,551 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 151,857,000 | 2026-07-29T18:16:36.9991489+00:00 | 106.6ms | GC pause | - | - | 204.1s / 701,551 msg/s | Gen2 +0 / pause +65.6ms |
| Confluent | 266,530,000 | 2026-07-29T18:19:30.4071232+00:00 | 103.0ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,531,000 | 2026-07-29T18:19:30.409461+00:00 | 105.4ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,538,000 | 2026-07-29T18:19:30.4197343+00:00 | 108.4ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,541,000 | 2026-07-29T18:19:30.4239389+00:00 | 107.2ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,550,000 | 2026-07-29T18:19:30.437596+00:00 | 106.4ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,553,000 | 2026-07-29T18:19:30.4445376+00:00 | 113.5ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,561,000 | 2026-07-29T18:19:30.4533081+00:00 | 108.1ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,570,000 | 2026-07-29T18:19:30.4713665+00:00 | 131.4ms | GC pause | - | - | 377.3s / 680,705 msg/s | Gen2 +0 / pause +74.2ms |
| Confluent | 266,888,000 | 2026-07-29T18:19:30.9391189+00:00 | 119.9ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,904,000 | 2026-07-29T18:19:30.9525921+00:00 | 105.6ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,908,000 | 2026-07-29T18:19:30.9560152+00:00 | 119.8ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,911,000 | 2026-07-29T18:19:30.9600377+00:00 | 116.0ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,912,000 | 2026-07-29T18:19:30.9613578+00:00 | 107.2ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,924,000 | 2026-07-29T18:19:30.9791924+00:00 | 101.7ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,928,000 | 2026-07-29T18:19:30.9833246+00:00 | 122.9ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 266,931,000 | 2026-07-29T18:19:30.9876245+00:00 | 123.9ms | GC pause | - | - | 378.3s / 664,284 msg/s | Gen2 +0 / pause +64.1ms |
| Confluent | 274,842,000 | 2026-07-29T18:19:43.5371454+00:00 | 102.6ms | GC pause | - | - | 390.3s / 707,870 msg/s | Gen2 +0 / pause +62.2ms |
| Confluent | 274,852,000 | 2026-07-29T18:19:43.5482588+00:00 | 102.4ms | GC pause | - | - | 390.3s / 707,870 msg/s | Gen2 +0 / pause +62.2ms |
| Confluent | 276,171,000 | 2026-07-29T18:19:45.4487009+00:00 | 101.9ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,173,000 | 2026-07-29T18:19:45.453137+00:00 | 103.6ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,190,000 | 2026-07-29T18:19:45.4800086+00:00 | 105.3ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,231,000 | 2026-07-29T18:19:45.5264434+00:00 | 129.7ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,238,000 | 2026-07-29T18:19:45.537921+00:00 | 127.1ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,241,000 | 2026-07-29T18:19:45.5412518+00:00 | 124.7ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,261,000 | 2026-07-29T18:19:45.5747301+00:00 | 108.7ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,274,000 | 2026-07-29T18:19:45.5967822+00:00 | 111.3ms | GC pause | - | - | 392.3s / 695,519 msg/s | Gen2 +0 / pause +70.1ms |
| Confluent | 276,843,000 | 2026-07-29T18:19:46.4606901+00:00 | 107.1ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,853,000 | 2026-07-29T18:19:46.4681693+00:00 | 115.5ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,860,000 | 2026-07-29T18:19:46.4777803+00:00 | 140.3ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,870,000 | 2026-07-29T18:19:46.4896741+00:00 | 147.3ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,883,000 | 2026-07-29T18:19:46.5052316+00:00 | 144.1ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,890,000 | 2026-07-29T18:19:46.5135099+00:00 | 149.7ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,903,000 | 2026-07-29T18:19:46.5281797+00:00 | 151.6ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,920,000 | 2026-07-29T18:19:46.5436081+00:00 | 162.8ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,964,000 | 2026-07-29T18:19:46.5850625+00:00 | 108.8ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,970,000 | 2026-07-29T18:19:46.5993202+00:00 | 146.8ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,973,000 | 2026-07-29T18:19:46.6046861+00:00 | 142.3ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,982,000 | 2026-07-29T18:19:46.6231391+00:00 | 116.7ms | GC pause | - | - | 393.3s / 713,662 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 276,983,000 | 2026-07-29T18:19:46.6256389+00:00 | 134.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +158.1ms |
| Confluent | 276,990,000 | 2026-07-29T18:19:46.6440335+00:00 | 120.0ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +158.1ms |
| Confluent | 277,067,000 | 2026-07-29T18:19:46.7403326+00:00 | 119.1ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +158.1ms |
| Confluent | 277,071,000 | 2026-07-29T18:19:46.7476902+00:00 | 112.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +158.1ms |
| Confluent | 277,077,000 | 2026-07-29T18:19:46.7544692+00:00 | 108.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +158.1ms |
| Confluent | 277,144,000 | 2026-07-29T18:19:46.8545019+00:00 | 138.5ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,163,000 | 2026-07-29T18:19:46.8800107+00:00 | 123.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,177,000 | 2026-07-29T18:19:46.9070017+00:00 | 137.3ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,183,000 | 2026-07-29T18:19:46.9147535+00:00 | 115.6ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,184,000 | 2026-07-29T18:19:46.9163111+00:00 | 128.5ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,194,000 | 2026-07-29T18:19:46.9243339+00:00 | 150.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,205,000 | 2026-07-29T18:19:46.9356111+00:00 | 103.6ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,207,000 | 2026-07-29T18:19:46.9367428+00:00 | 141.6ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,211,000 | 2026-07-29T18:19:46.9393404+00:00 | 139.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,213,000 | 2026-07-29T18:19:46.9404629+00:00 | 165.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,214,000 | 2026-07-29T18:19:46.9411341+00:00 | 188.6ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,224,000 | 2026-07-29T18:19:46.9517624+00:00 | 200.9ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,227,000 | 2026-07-29T18:19:46.9565744+00:00 | 128.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,229,000 | 2026-07-29T18:19:46.9588631+00:00 | 115.9ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,231,000 | 2026-07-29T18:19:46.9617985+00:00 | 126.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,238,000 | 2026-07-29T18:19:46.9708432+00:00 | 121.4ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,239,000 | 2026-07-29T18:19:46.9722801+00:00 | 113.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,249,000 | 2026-07-29T18:19:46.9871905+00:00 | 106.5ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,257,000 | 2026-07-29T18:19:46.9970814+00:00 | 133.4ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,260,000 | 2026-07-29T18:19:47.0007868+00:00 | 152.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,264,000 | 2026-07-29T18:19:47.0053687+00:00 | 195.9ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,265,000 | 2026-07-29T18:19:47.0065972+00:00 | 122.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,266,000 | 2026-07-29T18:19:47.0080775+00:00 | 120.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,269,000 | 2026-07-29T18:19:47.0130234+00:00 | 122.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,270,000 | 2026-07-29T18:19:47.0146875+00:00 | 147.4ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,275,000 | 2026-07-29T18:19:47.0209953+00:00 | 115.1ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,298,000 | 2026-07-29T18:19:47.0639475+00:00 | 147.4ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,302,000 | 2026-07-29T18:19:47.0699291+00:00 | 101.0ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,312,000 | 2026-07-29T18:19:47.0839291+00:00 | 107.8ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,318,000 | 2026-07-29T18:19:47.0908554+00:00 | 140.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,320,000 | 2026-07-29T18:19:47.0931611+00:00 | 140.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,321,000 | 2026-07-29T18:19:47.0945126+00:00 | 137.2ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,330,000 | 2026-07-29T18:19:47.1081115+00:00 | 130.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,332,000 | 2026-07-29T18:19:47.1145415+00:00 | 100.9ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,338,000 | 2026-07-29T18:19:47.1219004+00:00 | 137.3ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,341,000 | 2026-07-29T18:19:47.1246984+00:00 | 137.8ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,350,000 | 2026-07-29T18:19:47.1406354+00:00 | 117.5ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,357,000 | 2026-07-29T18:19:47.1517928+00:00 | 116.5ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,364,000 | 2026-07-29T18:19:47.1672297+00:00 | 110.0ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,368,000 | 2026-07-29T18:19:47.1777486+00:00 | 114.5ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,551,000 | 2026-07-29T18:19:47.4619121+00:00 | 111.6ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,558,000 | 2026-07-29T18:19:47.4672726+00:00 | 111.6ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,561,000 | 2026-07-29T18:19:47.4689888+00:00 | 115.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,568,000 | 2026-07-29T18:19:47.4772155+00:00 | 112.7ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,577,000 | 2026-07-29T18:19:47.4877734+00:00 | 110.1ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 277,578,000 | 2026-07-29T18:19:47.4911602+00:00 | 112.9ms | GC pause | - | - | 394.3s / 622,867 msg/s | Gen2 +0 / pause +79.5ms |
| Confluent | 284,087,000 | 2026-07-29T18:19:57.9742033+00:00 | 101.3ms | GC pause | - | - | 405.3s / 776,950 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 289,724,000 | 2026-07-29T18:20:06.6625802+00:00 | 118.2ms | GC pause | - | - | 413.3s / 478,172 msg/s | Gen2 +0 / pause +203.7ms |
| Confluent | 289,734,000 | 2026-07-29T18:20:06.6875648+00:00 | 116.4ms | GC pause | - | - | 414.3s / 410,789 msg/s | Gen2 +0 / pause +421.5ms |
| Confluent | 289,788,000 | 2026-07-29T18:20:06.8046356+00:00 | 102.0ms | GC pause | - | - | 414.3s / 410,789 msg/s | Gen2 +0 / pause +217.8ms |
| Confluent | 290,217,000 | 2026-07-29T18:20:07.8913037+00:00 | 100.5ms | GC pause | - | - | 415.3s / 553,094 msg/s | Gen2 +0 / pause +93.0ms |
| Confluent | 391,574,000 | 2026-07-29T18:22:25.1967566+00:00 | 110.0ms | GC pause | - | - | 552.4s / 421,274 msg/s | Gen2 +0 / pause +243.8ms |
| Confluent | 393,152,000 | 2026-07-29T18:22:27.9455609+00:00 | 100.7ms | GC pause | - | - | 555.4s / 596,816 msg/s | Gen2 +0 / pause +132.6ms |
| Confluent | 393,165,000 | 2026-07-29T18:22:27.9659581+00:00 | 103.9ms | GC pause | - | - | 555.4s / 596,816 msg/s | Gen2 +0 / pause +132.6ms |
| Confluent | 393,174,000 | 2026-07-29T18:22:28.0017316+00:00 | 100.8ms | GC pause | - | - | 555.4s / 596,816 msg/s | Gen2 +0 / pause +132.6ms |
| Confluent | 452,846,000 | 2026-07-29T18:23:43.4836831+00:00 | 102.4ms | GC pause | - | - | 630.4s / 667,867 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 452,864,000 | 2026-07-29T18:23:43.4985795+00:00 | 119.9ms | GC pause | - | - | 630.4s / 667,867 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 452,876,000 | 2026-07-29T18:23:43.5183649+00:00 | 117.9ms | GC pause | - | - | 630.4s / 667,867 msg/s | Gen2 +0 / pause +140.9ms |
| Confluent | 457,490,000 | 2026-07-29T18:23:49.8998035+00:00 | 108.4ms | GC pause | - | - | 637.4s / 467,208 msg/s | Gen2 +0 / pause +402.2ms |
| Confluent | 463,567,000 | 2026-07-29T18:23:59.4516976+00:00 | 108.0ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,578,000 | 2026-07-29T18:23:59.4591671+00:00 | 110.7ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,584,000 | 2026-07-29T18:23:59.4656135+00:00 | 106.5ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,597,000 | 2026-07-29T18:23:59.4848428+00:00 | 105.2ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,613,000 | 2026-07-29T18:23:59.5030757+00:00 | 104.4ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,614,000 | 2026-07-29T18:23:59.5036391+00:00 | 110.4ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,617,000 | 2026-07-29T18:23:59.5066376+00:00 | 160.7ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,623,000 | 2026-07-29T18:23:59.515357+00:00 | 101.4ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,628,000 | 2026-07-29T18:23:59.5208211+00:00 | 150.4ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,644,000 | 2026-07-29T18:23:59.5423119+00:00 | 114.2ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,647,000 | 2026-07-29T18:23:59.5476341+00:00 | 155.8ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,648,000 | 2026-07-29T18:23:59.5484567+00:00 | 155.0ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,657,000 | 2026-07-29T18:23:59.5664821+00:00 | 149.6ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,661,000 | 2026-07-29T18:23:59.5712713+00:00 | 165.1ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,667,000 | 2026-07-29T18:23:59.5803671+00:00 | 173.3ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,671,000 | 2026-07-29T18:23:59.5870447+00:00 | 167.2ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,691,000 | 2026-07-29T18:23:59.6242258+00:00 | 141.4ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,707,000 | 2026-07-29T18:23:59.6390535+00:00 | 152.0ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,717,000 | 2026-07-29T18:23:59.6480092+00:00 | 151.7ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,728,000 | 2026-07-29T18:23:59.6594702+00:00 | 147.6ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,751,000 | 2026-07-29T18:23:59.6841557+00:00 | 134.6ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 463,758,000 | 2026-07-29T18:23:59.7068712+00:00 | 118.6ms | GC pause | - | - | 646.5s / 666,951 msg/s | Gen2 +0 / pause +88.2ms |
| Confluent | 465,501,000 | 2026-07-29T18:24:02.4638737+00:00 | 114.0ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,503,000 | 2026-07-29T18:24:02.4652127+00:00 | 108.7ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,504,000 | 2026-07-29T18:24:02.4663259+00:00 | 105.2ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,507,000 | 2026-07-29T18:24:02.4707269+00:00 | 128.6ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,517,000 | 2026-07-29T18:24:02.485575+00:00 | 147.3ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,528,000 | 2026-07-29T18:24:02.5084887+00:00 | 130.1ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,534,000 | 2026-07-29T18:24:02.5173575+00:00 | 115.9ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,547,000 | 2026-07-29T18:24:02.5399405+00:00 | 117.9ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,587,000 | 2026-07-29T18:24:02.5935+00:00 | 134.3ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,588,000 | 2026-07-29T18:24:02.5943366+00:00 | 133.5ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,608,000 | 2026-07-29T18:24:02.630729+00:00 | 115.3ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,621,000 | 2026-07-29T18:24:02.6479169+00:00 | 111.9ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,631,000 | 2026-07-29T18:24:02.6653614+00:00 | 108.2ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,637,000 | 2026-07-29T18:24:02.6766024+00:00 | 100.9ms | GC pause | - | - | 649.5s / 680,165 msg/s | Gen2 +0 / pause +70.0ms |
| Confluent | 465,807,000 | 2026-07-29T18:24:02.9464636+00:00 | 101.2ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,838,000 | 2026-07-29T18:24:02.9696138+00:00 | 138.3ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,842,000 | 2026-07-29T18:24:02.9726312+00:00 | 131.9ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,843,000 | 2026-07-29T18:24:02.9731412+00:00 | 102.2ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,851,000 | 2026-07-29T18:24:02.9832757+00:00 | 145.6ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,854,000 | 2026-07-29T18:24:02.9854407+00:00 | 105.6ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,861,000 | 2026-07-29T18:24:02.9926806+00:00 | 146.5ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,864,000 | 2026-07-29T18:24:02.9963446+00:00 | 131.7ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,868,000 | 2026-07-29T18:24:02.9987055+00:00 | 152.8ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,880,000 | 2026-07-29T18:24:03.0092135+00:00 | 117.1ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,894,000 | 2026-07-29T18:24:03.0305412+00:00 | 128.0ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,907,000 | 2026-07-29T18:24:03.0567014+00:00 | 142.4ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 465,937,000 | 2026-07-29T18:24:03.1016756+00:00 | 121.8ms | GC pause | - | - | 650.5s / 674,310 msg/s | Gen2 +0 / pause +75.3ms |
| Confluent | 466,558,000 | 2026-07-29T18:24:04.0255032+00:00 | 104.4ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 466,867,000 | 2026-07-29T18:24:04.5029821+00:00 | 105.7ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 466,888,000 | 2026-07-29T18:24:04.5323383+00:00 | 106.1ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 466,951,000 | 2026-07-29T18:24:04.6105335+00:00 | 106.2ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 466,963,000 | 2026-07-29T18:24:04.6281697+00:00 | 109.2ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 466,964,000 | 2026-07-29T18:24:04.6286983+00:00 | 109.0ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 466,998,000 | 2026-07-29T18:24:04.6768725+00:00 | 117.9ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 467,004,000 | 2026-07-29T18:24:04.6843945+00:00 | 111.2ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 467,010,000 | 2026-07-29T18:24:04.6923272+00:00 | 101.6ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 467,017,000 | 2026-07-29T18:24:04.6967254+00:00 | 139.5ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 467,051,000 | 2026-07-29T18:24:04.7303828+00:00 | 132.5ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 467,057,000 | 2026-07-29T18:24:04.7411032+00:00 | 128.4ms | GC pause | - | - | 651.5s / 712,581 msg/s | Gen2 +0 / pause +78.2ms |
| Confluent | 467,138,000 | 2026-07-29T18:24:04.8774844+00:00 | 104.8ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +168.2ms |
| Confluent | 467,178,000 | 2026-07-29T18:24:04.9362298+00:00 | 107.5ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,180,000 | 2026-07-29T18:24:04.9390369+00:00 | 122.4ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,183,000 | 2026-07-29T18:24:04.9429098+00:00 | 130.6ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,190,000 | 2026-07-29T18:24:04.9503276+00:00 | 135.9ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,201,000 | 2026-07-29T18:24:04.9582742+00:00 | 168.3ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,216,000 | 2026-07-29T18:24:04.9706985+00:00 | 115.9ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,217,000 | 2026-07-29T18:24:04.9713084+00:00 | 177.7ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,219,000 | 2026-07-29T18:24:04.9723207+00:00 | 119.1ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,224,000 | 2026-07-29T18:24:04.9769723+00:00 | 106.5ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,228,000 | 2026-07-29T18:24:04.9795864+00:00 | 171.9ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,239,000 | 2026-07-29T18:24:04.987754+00:00 | 125.8ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,250,000 | 2026-07-29T18:24:04.9951549+00:00 | 143.0ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,257,000 | 2026-07-29T18:24:05.0022491+00:00 | 239.0ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,258,000 | 2026-07-29T18:24:05.0039372+00:00 | 237.3ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,260,000 | 2026-07-29T18:24:05.0069752+00:00 | 153.9ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,266,000 | 2026-07-29T18:24:05.0318179+00:00 | 105.6ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,270,000 | 2026-07-29T18:24:05.043376+00:00 | 136.8ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,272,000 | 2026-07-29T18:24:05.0468785+00:00 | 116.7ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,273,000 | 2026-07-29T18:24:05.0489025+00:00 | 131.5ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,274,000 | 2026-07-29T18:24:05.0519061+00:00 | 123.4ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,283,000 | 2026-07-29T18:24:05.0748522+00:00 | 119.0ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,297,000 | 2026-07-29T18:24:05.1006837+00:00 | 169.2ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,301,000 | 2026-07-29T18:24:05.1119928+00:00 | 162.9ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,304,000 | 2026-07-29T18:24:05.1163171+00:00 | 117.2ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,308,000 | 2026-07-29T18:24:05.1228619+00:00 | 158.3ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,314,000 | 2026-07-29T18:24:05.129137+00:00 | 117.9ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,318,000 | 2026-07-29T18:24:05.1342027+00:00 | 150.8ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,328,000 | 2026-07-29T18:24:05.1480932+00:00 | 146.0ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 467,341,000 | 2026-07-29T18:24:05.1837531+00:00 | 114.3ms | GC pause | - | - | 652.5s / 572,205 msg/s | Gen2 +0 / pause +90.0ms |
| Confluent | 600,687,000 | 2026-07-29T18:26:51.4821087+00:00 | 104.4ms | GC pause | - | - | 818.6s / 481,109 msg/s | Gen2 +0 / pause +225.6ms |
| Confluent | 600,856,000 | 2026-07-29T18:26:51.8965474+00:00 | 110.8ms | GC pause | - | - | 818.6s / 481,109 msg/s | Gen2 +0 / pause +225.6ms |
| Confluent | 603,287,000 | 2026-07-29T18:26:55.5098766+00:00 | 102.1ms | GC pause | - | - | 822.6s / 496,079 msg/s | Gen2 +0 / pause +185.8ms |
| Confluent | 603,290,000 | 2026-07-29T18:26:55.5122224+00:00 | 101.5ms | GC pause | - | - | 822.6s / 496,079 msg/s | Gen2 +0 / pause +185.8ms |
| Confluent | 614,076,000 | 2026-07-29T18:27:09.400544+00:00 | 101.1ms | GC pause | - | - | 836.6s / 572,408 msg/s | Gen2 +0 / pause +203.9ms |
| Confluent | 637,043,000 | 2026-07-29T18:27:43.2972644+00:00 | 139.9ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,047,000 | 2026-07-29T18:27:43.3017894+00:00 | 139.2ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,063,000 | 2026-07-29T18:27:43.3221843+00:00 | 149.2ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,064,000 | 2026-07-29T18:27:43.3233463+00:00 | 119.9ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,068,000 | 2026-07-29T18:27:43.3276042+00:00 | 159.1ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,081,000 | 2026-07-29T18:27:43.3343086+00:00 | 173.4ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,083,000 | 2026-07-29T18:27:43.3354814+00:00 | 169.7ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,089,000 | 2026-07-29T18:27:43.3386649+00:00 | 116.3ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,091,000 | 2026-07-29T18:27:43.339633+00:00 | 185.8ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,092,000 | 2026-07-29T18:27:43.3401149+00:00 | 101.2ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,095,000 | 2026-07-29T18:27:43.3439362+00:00 | 121.3ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,099,000 | 2026-07-29T18:27:43.3471071+00:00 | 118.5ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,100,000 | 2026-07-29T18:27:43.3475761+00:00 | 178.4ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,107,000 | 2026-07-29T18:27:43.3526631+00:00 | 186.8ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,114,000 | 2026-07-29T18:27:43.3568021+00:00 | 171.9ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,119,000 | 2026-07-29T18:27:43.3593979+00:00 | 140.0ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,120,000 | 2026-07-29T18:27:43.3600708+00:00 | 179.0ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,123,000 | 2026-07-29T18:27:43.3628416+00:00 | 201.9ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,132,000 | 2026-07-29T18:27:43.3704527+00:00 | 141.1ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,136,000 | 2026-07-29T18:27:43.375275+00:00 | 131.9ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,139,000 | 2026-07-29T18:27:43.3842791+00:00 | 128.7ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,145,000 | 2026-07-29T18:27:43.3935512+00:00 | 132.8ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,148,000 | 2026-07-29T18:27:43.399954+00:00 | 168.0ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,163,000 | 2026-07-29T18:27:43.431778+00:00 | 153.6ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,165,000 | 2026-07-29T18:27:43.4358615+00:00 | 131.6ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,168,000 | 2026-07-29T18:27:43.4395863+00:00 | 149.9ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,173,000 | 2026-07-29T18:27:43.4469593+00:00 | 149.2ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,207,000 | 2026-07-29T18:27:43.5093894+00:00 | 119.6ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 637,211,000 | 2026-07-29T18:27:43.5165287+00:00 | 112.7ms | GC pause | - | - | 870.7s / 649,737 msg/s | Gen2 +0 / pause +122.4ms |
| Confluent | 645,785,000 | 2026-07-29T18:27:54.4599619+00:00 | 103.1ms | GC pause | - | - | 881.7s / 546,782 msg/s | Gen2 +0 / pause +198.3ms |
| Confluent | 645,786,000 | 2026-07-29T18:27:54.4604517+00:00 | 102.7ms | GC pause | - | - | 881.7s / 546,782 msg/s | Gen2 +0 / pause +198.3ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*1,269 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.53x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.35x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,643,208 | 1,636,214–1,650,232 | 0.94 | 1.13x |
| Confluent | 2 | 1,453,782 | 1,429,170–1,478,818 | 1.21 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 0.92 | 942.21 | 1,613,257 | 1,650,232 | +0.3% | -0.07% | 1538.52 | 1,613,257 | 0 | 1.49 |
| Dekaf (3conn) | 0.72 | 667.79 | 1,636,399 | 1,644,559 | -5.7% | -0.37% | 1560.59 | 1,636,399 | 0 | 1.18 |
| Dekaf (dekaf-first) | 0.97 | 992.31 | 1,609,506 | 1,636,214 | -3.5% | -0.21% | 1534.94 | 1,609,506 | 0 | 1.56 |
| Confluent (confluent-first) | 1.20 | - | 1,441,789 | 1,478,818 | +8.2% | +0.77% | 1375.00 | 1,441,789 | 0 | 1.73 |
| Confluent (dekaf-first) | 1.23 | - | 1,388,895 | 1,429,170 | +4.9% | +0.41% | 1324.55 | 1,388,895 | 0 | 1.71 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,419,035 | 1576.69 | 1017.11 KB |
| Dekaf | 1 | 1,410,942 | 1567.69 | 1020.57 KB |
| Dekaf (3conn) | 1 | 1,590,627 | 1767.35 | 920.40 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:01.2096626+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 624,233 msg/s |
| Dekaf | 2026-07-29T17:58:28.2175332+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1840.3 MB/s | 0/0 | 54,548 | 27.0s / 1,682,167 msg/s |
| Dekaf | 2026-07-29T17:58:56.2283311+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1840.3 MB/s | 0/1 | 107,689 | 55.0s / 1,428,935 msg/s |
| Dekaf | 2026-07-29T17:59:23.2335635+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1840.3 MB/s | 0/1 | 158,564 | 82.0s / 1,619,260 msg/s |
| Dekaf | 2026-07-29T17:59:50.2377789+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1849.0 MB/s | 0/1 | 220,795 | 109.0s / 1,671,437 msg/s |
| Dekaf | 2026-07-29T18:00:17.2433971+00:00 | 1 | 18.0 MiB / 17.5 MiB | 1849.0 MB/s | 1/1 | 277,302 | 136.0s / 1,678,586 msg/s |
| Dekaf | 2026-07-29T18:00:45.2527019+00:00 | 1 | 20.0 MiB / 19.9 MiB | 1849.0 MB/s | 1/1 | 335,412 | 164.0s / 1,661,873 msg/s |
| Dekaf | 2026-07-29T18:01:12.2675444+00:00 | 1 | 20.0 MiB / 19.9 MiB | 1859.9 MB/s | 2/1 | 385,513 | 191.1s / 1,749,990 msg/s |
| Dekaf | 2026-07-29T18:01:39.2817576+00:00 | 1 | 22.0 MiB / 22.0 MiB | 1859.9 MB/s | 3/1 | 437,247 | 218.1s / 1,722,413 msg/s |
| Dekaf | 2026-07-29T18:02:06.2921031+00:00 | 1 | 22.0 MiB / 21.2 MiB | 1859.9 MB/s | 3/1 | 489,213 | 245.1s / 1,671,540 msg/s |
| Dekaf | 2026-07-29T18:02:34.2986328+00:00 | 1 | 24.0 MiB / 24.0 MiB | 1859.9 MB/s | 4/1 | 544,212 | 273.1s / 1,547,981 msg/s |
| Dekaf | 2026-07-29T18:03:01.3046454+00:00 | 1 | 21.0 MiB / 20.5 MiB | 1859.9 MB/s | 4/1 | 579,438 | 300.1s / 1,546,390 msg/s |
| Dekaf | 2026-07-29T18:03:28.3128317+00:00 | 1 | 24.0 MiB / 23.4 MiB | 1859.9 MB/s | 4/2 | 611,014 | 327.1s / 1,545,808 msg/s |
| Dekaf | 2026-07-29T18:03:55.3245075+00:00 | 1 | 24.0 MiB / 23.4 MiB | 1859.9 MB/s | 4/2 | 650,965 | 354.1s / 1,478,314 msg/s |
| Dekaf | 2026-07-29T18:04:23.3318412+00:00 | 1 | 24.0 MiB / 20.9 MiB | 1859.9 MB/s | 4/3 | 685,204 | 382.1s / 1,510,936 msg/s |
| Dekaf | 2026-07-29T18:04:50.338493+00:00 | 1 | 24.0 MiB / 19.8 MiB | 1859.9 MB/s | 4/3 | 725,838 | 409.1s / 1,739,883 msg/s |
| Dekaf | 2026-07-29T18:05:17.3529974+00:00 | 1 | 24.0 MiB / 23.9 MiB | 1859.9 MB/s | 4/3 | 767,783 | 436.1s / 1,656,217 msg/s |
| Dekaf | 2026-07-29T18:05:44.3597616+00:00 | 1 | 24.0 MiB / 24.0 MiB | 1859.9 MB/s | 4/3 | 814,671 | 463.1s / 1,769,517 msg/s |
| Dekaf | 2026-07-29T18:06:12.3665717+00:00 | 1 | 24.0 MiB / 23.7 MiB | 1859.9 MB/s | 4/3 | 865,918 | 491.1s / 1,550,453 msg/s |
| Dekaf | 2026-07-29T18:06:39.3736672+00:00 | 1 | 24.0 MiB / 24.0 MiB | 1859.9 MB/s | 4/4 | 906,001 | 518.2s / 1,645,974 msg/s |
| Dekaf | 2026-07-29T18:07:06.3874443+00:00 | 1 | 24.0 MiB / 24.0 MiB | 1864.6 MB/s | 4/4 | 955,153 | 545.2s / 1,705,834 msg/s |
| Dekaf | 2026-07-29T18:07:34.3981278+00:00 | 1 | 18.0 MiB / 17.2 MiB | 1864.6 MB/s | 5/4 | 1,003,208 | 573.2s / 1,693,463 msg/s |
| Dekaf | 2026-07-29T18:08:01.413+00:00 | 1 | 21.0 MiB / 21.0 MiB | 1864.6 MB/s | 5/5 | 1,047,166 | 600.2s / 1,596,133 msg/s |
| Dekaf | 2026-07-29T18:08:28.4259351+00:00 | 1 | 21.0 MiB / 20.7 MiB | 1864.6 MB/s | 5/5 | 1,092,154 | 627.2s / 1,498,203 msg/s |
| Dekaf | 2026-07-29T18:08:55.4365308+00:00 | 1 | 21.0 MiB / 20.6 MiB | 1864.6 MB/s | 5/6 | 1,136,106 | 654.2s / 1,518,948 msg/s |
| Dekaf | 2026-07-29T18:09:23.4557094+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1864.6 MB/s | 5/6 | 1,178,621 | 682.2s / 1,665,617 msg/s |
| Dekaf | 2026-07-29T18:09:50.4697118+00:00 | 1 | 21.0 MiB / 20.9 MiB | 1864.6 MB/s | 5/7 | 1,225,299 | 709.2s / 1,680,872 msg/s |
| Dekaf | 2026-07-29T18:10:17.4854339+00:00 | 1 | 23.0 MiB / 21.9 MiB | 1873.7 MB/s | 5/7 | 1,268,701 | 736.2s / 1,749,425 msg/s |
| Dekaf | 2026-07-29T18:10:44.4968949+00:00 | 1 | 21.0 MiB / 21.0 MiB | 1873.7 MB/s | 5/8 | 1,309,601 | 763.2s / 1,722,385 msg/s |
| Dekaf | 2026-07-29T18:11:12.5093061+00:00 | 1 | 21.0 MiB / 20.9 MiB | 1873.7 MB/s | 5/8 | 1,359,323 | 791.3s / 1,665,499 msg/s |
| Dekaf | 2026-07-29T18:11:39.5189059+00:00 | 1 | 21.0 MiB / 21.0 MiB | 1873.7 MB/s | 5/8 | 1,403,566 | 818.3s / 1,548,331 msg/s |
| Dekaf | 2026-07-29T18:12:06.5337627+00:00 | 1 | 21.0 MiB / 21.0 MiB | 1873.7 MB/s | 5/8 | 1,449,313 | 845.3s / 1,469,108 msg/s |
| Dekaf | 2026-07-29T18:12:33.5447042+00:00 | 1 | 21.0 MiB / 21.0 MiB | 1873.7 MB/s | 5/8 | 1,491,072 | 872.3s / 1,463,271 msg/s |
| Dekaf | 2026-07-29T18:43:02.6373285+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 465,565 msg/s |
| Dekaf | 2026-07-29T18:43:29.6470406+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1776.0 MB/s | 0/0 | 56,325 | 27.0s / 1,699,755 msg/s |
| Dekaf | 2026-07-29T18:43:56.6631397+00:00 | 1 | 16.0 MiB / 15.5 MiB | 1814.9 MB/s | 0/1 | 121,236 | 54.0s / 1,686,726 msg/s |
| Dekaf | 2026-07-29T18:44:23.6716229+00:00 | 1 | 16.0 MiB / 15.2 MiB | 1814.9 MB/s | 0/1 | 174,301 | 81.0s / 1,704,292 msg/s |
| Dekaf | 2026-07-29T18:44:51.6814218+00:00 | 1 | 18.0 MiB / 17.7 MiB | 1814.9 MB/s | 0/1 | 225,148 | 109.0s / 1,521,133 msg/s |
| Dekaf | 2026-07-29T18:45:18.6933056+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1814.9 MB/s | 1/1 | 268,396 | 136.0s / 1,544,004 msg/s |
| Dekaf | 2026-07-29T18:45:45.7069942+00:00 | 1 | 20.0 MiB / 19.5 MiB | 1814.9 MB/s | 1/1 | 319,671 | 163.1s / 1,617,877 msg/s |
| Dekaf | 2026-07-29T18:46:13.7155703+00:00 | 1 | 18.0 MiB / 17.9 MiB | 1814.9 MB/s | 1/2 | 364,280 | 191.1s / 1,642,455 msg/s |
| Dekaf | 2026-07-29T18:46:40.7307924+00:00 | 1 | 18.0 MiB / 17.3 MiB | 1814.9 MB/s | 1/2 | 413,973 | 218.1s / 1,448,617 msg/s |
| Dekaf | 2026-07-29T18:47:07.7472458+00:00 | 1 | 18.0 MiB / 10.7 MiB | 1814.9 MB/s | 1/3 | 440,553 | 245.1s / 1,656,049 msg/s |
| Dekaf | 2026-07-29T18:47:34.7584929+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1848.1 MB/s | 1/3 | 500,164 | 272.1s / 1,673,686 msg/s |
| Dekaf | 2026-07-29T18:48:02.776644+00:00 | 1 | 18.0 MiB / 16.7 MiB | 1848.1 MB/s | 1/3 | 565,169 | 300.1s / 1,683,718 msg/s |
| Dekaf | 2026-07-29T18:48:29.7876726+00:00 | 1 | 18.0 MiB / 17.6 MiB | 1848.1 MB/s | 1/3 | 628,268 | 327.1s / 1,679,021 msg/s |
| Dekaf | 2026-07-29T18:48:56.8055322+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1848.1 MB/s | 1/3 | 692,815 | 354.1s / 1,710,257 msg/s |
| Dekaf | 2026-07-29T18:49:23.8252048+00:00 | 1 | 18.0 MiB / 17.1 MiB | 1848.1 MB/s | 1/4 | 756,067 | 381.1s / 1,687,157 msg/s |
| Dekaf | 2026-07-29T18:49:51.8492832+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1848.1 MB/s | 1/4 | 825,441 | 409.1s / 1,668,370 msg/s |
| Dekaf | 2026-07-29T18:50:18.8698842+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1848.1 MB/s | 2/4 | 891,225 | 436.1s / 1,698,794 msg/s |
| Dekaf | 2026-07-29T18:50:45.8840028+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1848.1 MB/s | 2/4 | 958,125 | 463.1s / 1,656,951 msg/s |
| Dekaf | 2026-07-29T18:51:12.8951589+00:00 | 1 | 13.0 MiB / 12.2 MiB | 1848.1 MB/s | 3/4 | 1,027,527 | 490.2s / 1,685,727 msg/s |
| Dekaf | 2026-07-29T18:51:40.9011967+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1848.1 MB/s | 4/4 | 1,100,718 | 518.2s / 1,686,953 msg/s |
| Dekaf | 2026-07-29T18:52:07.9124369+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1848.1 MB/s | 4/4 | 1,166,871 | 545.2s / 1,589,561 msg/s |
| Dekaf | 2026-07-29T18:52:34.921547+00:00 | 1 | 11.0 MiB / 10.4 MiB | 1848.1 MB/s | 4/5 | 1,228,931 | 572.2s / 1,389,609 msg/s |
| Dekaf | 2026-07-29T18:53:01.9305405+00:00 | 1 | 11.0 MiB / 10.6 MiB | 1848.1 MB/s | 4/5 | 1,296,732 | 599.2s / 1,684,412 msg/s |
| Dekaf | 2026-07-29T18:53:29.9348963+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1848.1 MB/s | 4/5 | 1,364,917 | 627.2s / 1,621,173 msg/s |
| Dekaf | 2026-07-29T18:53:56.9387582+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1848.1 MB/s | 4/6 | 1,424,056 | 654.2s / 1,558,683 msg/s |
| Dekaf | 2026-07-29T18:54:23.9430767+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1848.1 MB/s | 4/6 | 1,475,557 | 681.2s / 1,607,052 msg/s |
| Dekaf | 2026-07-29T18:54:50.9472828+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1848.1 MB/s | 4/6 | 1,535,949 | 708.2s / 1,663,600 msg/s |
| Dekaf | 2026-07-29T18:55:18.9510862+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1848.1 MB/s | 4/6 | 1,610,895 | 736.2s / 1,561,425 msg/s |
| Dekaf | 2026-07-29T18:55:45.9618758+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1848.1 MB/s | 4/6 | 1,668,002 | 763.2s / 1,309,222 msg/s |
| Dekaf | 2026-07-29T18:56:12.9723318+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1848.1 MB/s | 4/7 | 1,728,361 | 790.2s / 1,644,408 msg/s |
| Dekaf | 2026-07-29T18:56:40.9783616+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1848.1 MB/s | 4/7 | 1,804,315 | 818.2s / 1,667,365 msg/s |
| Dekaf | 2026-07-29T18:57:07.9841503+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1848.1 MB/s | 4/7 | 1,873,563 | 845.2s / 1,679,236 msg/s |
| Dekaf | 2026-07-29T18:57:34.9860184+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1848.1 MB/s | 4/7 | 1,931,258 | 872.2s / 1,617,269 msg/s |
| Dekaf | 2026-07-29T18:58:01.9895499+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1848.1 MB/s | 4/7 | 1,987,820 | 899.2s / 1,539,593 msg/s |
| Dekaf (3conn) | 2026-07-29T18:58:30.5795343+00:00 | 1 | 16.0 MiB / 13.4 MiB | 2096.4 MB/s | 0/0 | 994 | 27.0s / 1,675,653 msg/s |
| Dekaf (3conn) | 2026-07-29T18:58:57.5845482+00:00 | 1 | 14.0 MiB / 11.7 MiB | 2188.4 MB/s | 1/0 | 2,430 | 54.0s / 1,754,402 msg/s |
| Dekaf (3conn) | 2026-07-29T18:59:24.5905124+00:00 | 1 | 14.0 MiB / 2.0 MiB | 2247.3 MB/s | 1/0 | 4,079 | 81.0s / 1,750,447 msg/s |
| Dekaf (3conn) | 2026-07-29T18:59:51.5975303+00:00 | 1 | 14.0 MiB / 3.7 MiB | 2247.3 MB/s | 1/1 | 5,625 | 108.0s / 1,720,912 msg/s |
| Dekaf (3conn) | 2026-07-29T19:00:19.6057276+00:00 | 1 | 14.0 MiB / 4.8 MiB | 2247.3 MB/s | 1/1 | 7,305 | 136.1s / 1,726,261 msg/s |
| Dekaf (3conn) | 2026-07-29T19:00:46.6118832+00:00 | 1 | 15.0 MiB / 2.6 MiB | 2247.3 MB/s | 1/1 | 8,762 | 163.1s / 1,671,014 msg/s |
| Dekaf (3conn) | 2026-07-29T19:01:13.6244513+00:00 | 1 | 15.0 MiB / 6.1 MiB | 2247.3 MB/s | 2/1 | 9,847 | 190.1s / 2,014,186 msg/s |
| Dekaf (3conn) | 2026-07-29T19:01:40.6357697+00:00 | 1 | 15.0 MiB / 1.8 MiB | 2247.3 MB/s | 2/2 | 11,111 | 217.1s / 1,620,512 msg/s |
| Dekaf (3conn) | 2026-07-29T19:02:08.6455602+00:00 | 1 | 15.0 MiB / 5.9 MiB | 2247.3 MB/s | 2/2 | 12,600 | 245.1s / 1,619,502 msg/s |
| Dekaf (3conn) | 2026-07-29T19:02:35.6527515+00:00 | 1 | 13.0 MiB / 5.7 MiB | 2247.3 MB/s | 2/2 | 13,680 | 272.1s / 1,380,714 msg/s |
| Dekaf (3conn) | 2026-07-29T19:03:02.6549694+00:00 | 1 | 13.0 MiB / 3.2 MiB | 2247.3 MB/s | 3/2 | 15,157 | 299.1s / 1,523,028 msg/s |
| Dekaf (3conn) | 2026-07-29T19:03:29.6605593+00:00 | 1 | 13.0 MiB / 4.7 MiB | 2247.3 MB/s | 3/2 | 17,066 | 326.1s / 1,578,702 msg/s |
| Dekaf (3conn) | 2026-07-29T19:03:57.6715141+00:00 | 1 | 13.0 MiB / 7.0 MiB | 2247.3 MB/s | 3/3 | 19,013 | 354.1s / 1,594,175 msg/s |
| Dekaf (3conn) | 2026-07-29T19:04:24.6747822+00:00 | 1 | 13.0 MiB / 5.3 MiB | 2247.3 MB/s | 3/3 | 20,784 | 381.1s / 1,557,970 msg/s |
| Dekaf (3conn) | 2026-07-29T19:04:51.68054+00:00 | 1 | 14.0 MiB / 6.0 MiB | 2247.3 MB/s | 4/3 | 22,469 | 408.1s / 1,697,032 msg/s |
| Dekaf (3conn) | 2026-07-29T19:05:19.6875139+00:00 | 1 | 15.0 MiB / 3.3 MiB | 2247.3 MB/s | 4/3 | 24,020 | 436.2s / 1,683,093 msg/s |
| Dekaf (3conn) | 2026-07-29T19:05:46.6932848+00:00 | 1 | 14.0 MiB / 1.5 MiB | 2247.3 MB/s | 4/4 | 25,523 | 463.2s / 1,571,210 msg/s |
| Dekaf (3conn) | 2026-07-29T19:06:13.6987436+00:00 | 1 | 14.0 MiB / 3.2 MiB | 2247.3 MB/s | 4/4 | 27,152 | 490.2s / 1,830,248 msg/s |
| Dekaf (3conn) | 2026-07-29T19:06:40.7013022+00:00 | 1 | 14.0 MiB / 11.5 MiB | 2247.3 MB/s | 4/4 | 28,604 | 517.2s / 1,775,250 msg/s |
| Dekaf (3conn) | 2026-07-29T19:07:08.7074065+00:00 | 1 | 12.0 MiB / 6.0 MiB | 2247.3 MB/s | 5/4 | 30,901 | 545.2s / 1,647,814 msg/s |
| Dekaf (3conn) | 2026-07-29T19:07:35.7105834+00:00 | 1 | 12.0 MiB / 5.2 MiB | 2247.3 MB/s | 5/5 | 33,239 | 572.2s / 1,642,577 msg/s |
| Dekaf (3conn) | 2026-07-29T19:08:02.7127241+00:00 | 1 | 12.0 MiB / 2.2 MiB | 2247.3 MB/s | 5/5 | 35,661 | 599.2s / 1,769,828 msg/s |
| Dekaf (3conn) | 2026-07-29T19:08:29.7178046+00:00 | 1 | 12.0 MiB / 5.9 MiB | 2247.3 MB/s | 5/5 | 37,801 | 626.2s / 1,473,453 msg/s |
| Dekaf (3conn) | 2026-07-29T19:08:57.7316754+00:00 | 1 | 13.0 MiB / 5.6 MiB | 2247.3 MB/s | 6/5 | 39,731 | 654.2s / 1,362,058 msg/s |
| Dekaf (3conn) | 2026-07-29T19:09:24.7380226+00:00 | 1 | 13.0 MiB / 3.7 MiB | 2247.3 MB/s | 6/5 | 41,616 | 681.2s / 1,979,682 msg/s |
| Dekaf (3conn) | 2026-07-29T19:09:51.7453886+00:00 | 1 | 13.0 MiB / 11.9 MiB | 2247.3 MB/s | 6/6 | 43,385 | 708.2s / 1,488,106 msg/s |
| Dekaf (3conn) | 2026-07-29T19:10:18.7545397+00:00 | 1 | 13.0 MiB / 3.8 MiB | 2247.3 MB/s | 6/6 | 45,185 | 735.2s / 1,520,540 msg/s |
| Dekaf (3conn) | 2026-07-29T19:10:46.7683325+00:00 | 1 | 11.0 MiB / 1.7 MiB | 2247.3 MB/s | 6/6 | 47,203 | 763.3s / 1,366,434 msg/s |
| Dekaf (3conn) | 2026-07-29T19:11:13.7748084+00:00 | 1 | 13.0 MiB / 9.7 MiB | 2247.3 MB/s | 6/7 | 49,100 | 790.3s / 1,774,209 msg/s |
| Dekaf (3conn) | 2026-07-29T19:11:40.7885534+00:00 | 1 | 13.0 MiB / 5.7 MiB | 2247.3 MB/s | 6/7 | 51,814 | 817.3s / 1,691,999 msg/s |
| Dekaf (3conn) | 2026-07-29T19:12:07.7913933+00:00 | 1 | 13.0 MiB / 6.1 MiB | 2406.0 MB/s | 6/7 | 54,129 | 844.3s / 1,997,936 msg/s |
| Dekaf (3conn) | 2026-07-29T19:12:35.797986+00:00 | 1 | 13.0 MiB / 1.1 MiB | 2406.0 MB/s | 6/7 | 56,641 | 872.3s / 1,592,011 msg/s |
| Dekaf (3conn) | 2026-07-29T19:13:02.8105773+00:00 | 1 | 14.0 MiB / 5.2 MiB | 2406.0 MB/s | 6/7 | 58,853 | 899.3s / 1,523,077 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T17:58:31.3337122+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.9 MiB |
| Dekaf | 2026-07-29T17:58:46.343962+00:00 | 1 | capacity | failed | 15,010ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T17:59:46.3891202+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T18:00:01.3989574+00:00 | 1 | capacity | succeeded | 15,009ms | 18.0 MiB / 17.5 MiB |
| Dekaf | 2026-07-29T18:00:31.4177595+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T18:00:46.4265762+00:00 | 1 | capacity | succeeded | 15,008ms | 20.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-29T18:01:16.4687921+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 16.6 MiB |
| Dekaf | 2026-07-29T18:01:31.4796826+00:00 | 1 | capacity | succeeded | 15,011ms | 22.0 MiB / 20.9 MiB |
| Dekaf | 2026-07-29T18:02:01.4987713+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 21.1 MiB |
| Dekaf | 2026-07-29T18:02:16.5088003+00:00 | 1 | capacity | succeeded | 15,010ms | 24.0 MiB / 22.6 MiB |
| Dekaf | 2026-07-29T18:02:46.5294978+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 22.4 MiB |
| Dekaf | 2026-07-29T18:03:01.5400089+00:00 | 1 | capacity | failed | 15,010ms | 24.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-29T18:04:01.5871767+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 21.8 MiB |
| Dekaf | 2026-07-29T18:04:16.5983395+00:00 | 1 | capacity | failed | 15,011ms | 24.0 MiB / 18.4 MiB |
| Dekaf | 2026-07-29T18:06:16.689587+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 23.3 MiB |
| Dekaf | 2026-07-29T18:06:31.7011667+00:00 | 1 | capacity | failed | 15,011ms | 24.0 MiB / 20.1 MiB |
| Dekaf | 2026-07-29T18:07:01.7239581+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 20.6 MiB |
| Dekaf | 2026-07-29T18:07:16.737305+00:00 | 1 | capacity | succeeded | 15,013ms | 21.0 MiB / 20.1 MiB |
| Dekaf | 2026-07-29T18:07:19.7382309+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 19.5 MiB |
| Dekaf | 2026-07-29T18:07:34.7492539+00:00 | 1 | capacity | failed | 15,011ms | 21.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T18:08:34.7972616+00:00 | 1 | capacity | started | 0ms | 23.0 MiB / 20.1 MiB |
| Dekaf | 2026-07-29T18:08:49.8091695+00:00 | 1 | capacity | failed | 15,012ms | 21.0 MiB / 20.4 MiB |
| Dekaf | 2026-07-29T18:09:19.8335957+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 20.1 MiB |
| Dekaf | 2026-07-29T18:09:34.8439686+00:00 | 1 | capacity | failed | 15,010ms | 21.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-29T18:10:04.8658339+00:00 | 1 | capacity | started | 0ms | 23.0 MiB / 18.2 MiB |
| Dekaf | 2026-07-29T18:10:19.8764373+00:00 | 1 | capacity | failed | 15,010ms | 21.0 MiB / 22.1 MiB |
| Dekaf | 2026-07-29T18:43:32.731845+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-29T18:43:47.7434174+00:00 | 1 | capacity | failed | 15,011ms | 16.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-29T18:44:47.7890749+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.9 MiB |
| Dekaf | 2026-07-29T18:45:02.8009273+00:00 | 1 | capacity | succeeded | 15,011ms | 18.0 MiB / 14.4 MiB |
| Dekaf | 2026-07-29T18:45:32.8273529+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T18:45:47.8389175+00:00 | 1 | capacity | failed | 15,011ms | 18.0 MiB / 17.4 MiB |
| Dekaf | 2026-07-29T18:46:47.8859812+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 15.2 MiB |
| Dekaf | 2026-07-29T18:47:02.8975655+00:00 | 1 | capacity | failed | 15,011ms | 18.0 MiB / 13.5 MiB |
| Dekaf | 2026-07-29T18:49:03.0061625+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T18:49:18.0153648+00:00 | 1 | capacity | failed | 15,009ms | 18.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-29T18:49:48.0388489+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-29T18:50:03.0496608+00:00 | 1 | capacity | succeeded | 15,010ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T18:50:33.0706422+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-29T18:50:48.0836931+00:00 | 1 | capacity | succeeded | 15,013ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:51:18.106379+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:51:33.1170834+00:00 | 1 | capacity | succeeded | 15,010ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:52:03.1374472+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:52:18.1475457+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-29T18:53:18.1860738+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-29T18:53:33.1984127+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 11.3 MiB |
| Dekaf | 2026-07-29T18:55:33.2766404+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-29T18:55:48.2876866+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 8.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:58:33.6807014+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:58:48.6983113+00:00 | 1 | capacity | succeeded | 15,017ms | 14.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:59:18.7379211+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:59:33.7549478+00:00 | 1 | capacity | failed | 15,016ms | 14.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:00:33.8447419+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:00:48.8667286+00:00 | 1 | capacity | succeeded | 15,021ms | 15.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-29T19:01:18.9137852+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:01:33.9440476+00:00 | 1 | capacity | failed | 15,030ms | 15.0 MiB / 14.1 MiB |
| Dekaf (3conn) | 2026-07-29T19:02:34.0334644+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:02:49.0498922+00:00 | 1 | capacity | succeeded | 15,016ms | 13.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T19:03:19.0863559+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:03:34.1057291+00:00 | 1 | capacity | failed | 15,019ms | 13.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:04:34.1833874+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:04:49.1985681+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 9.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:05:19.235783+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-29T19:05:34.2510963+00:00 | 1 | capacity | failed | 15,015ms | 14.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:06:34.3186652+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:06:49.3355891+00:00 | 1 | capacity | succeeded | 15,016ms | 12.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-29T19:07:19.3749671+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-29T19:07:34.3946803+00:00 | 1 | capacity | failed | 15,019ms | 12.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-29T19:08:34.4794822+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:08:49.4985747+00:00 | 1 | capacity | succeeded | 15,019ms | 13.0 MiB / 9.3 MiB |
| Dekaf (3conn) | 2026-07-29T19:09:19.5484083+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-29T19:09:34.5673707+00:00 | 1 | capacity | failed | 15,018ms | 13.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-29T19:10:34.6515033+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T19:10:49.6746994+00:00 | 1 | capacity | failed | 15,023ms | 13.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-29T19:12:49.8122144+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.6 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 2,153 |
| Dekaf | 1 | 0.002–0.004ms | 2,618 |
| Dekaf | 1 | 0.004–0.008ms | 16,986 |
| Dekaf | 1 | 0.008–0.016ms | 99,842 |
| Dekaf | 1 | 0.016–0.032ms | 68,658 |
| Dekaf | 1 | 0.032–0.064ms | 66,813 |
| Dekaf | 1 | 0.064–0.128ms | 101,236 |
| Dekaf | 1 | 0.128–0.256ms | 291,653 |
| Dekaf | 1 | 0.256–0.512ms | 282,622 |
| Dekaf | 1 | 0.512–1.024ms | 45,663 |
| Dekaf | 1 | 1.024–2.048ms | 6,860 |
| Dekaf | 1 | 2.048–4.096ms | 3,590 |
| Dekaf | 1 | 4.096–8.192ms | 378 |
| Dekaf | 1 | 8.192–16.384ms | 3 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 34 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 27 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 96 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 207 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 708 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 2,271 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,764 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 3,139 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 3,941 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 3,367 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,371 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 332 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 31 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 2 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 2,255 |
| Dekaf | 1 | 0.002–0.004ms | 2,830 |
| Dekaf | 1 | 0.004–0.008ms | 11,072 |
| Dekaf | 1 | 0.008–0.016ms | 64,318 |
| Dekaf | 1 | 0.016–0.032ms | 66,223 |
| Dekaf | 1 | 0.032–0.064ms | 55,617 |
| Dekaf | 1 | 0.064–0.128ms | 100,093 |
| Dekaf | 1 | 0.128–0.256ms | 217,494 |
| Dekaf | 1 | 0.256–0.512ms | 200,174 |
| Dekaf | 1 | 0.512–1.024ms | 35,331 |
| Dekaf | 1 | 1.024–2.048ms | 4,747 |
| Dekaf | 1 | 2.048–4.096ms | 3,555 |
| Dekaf | 1 | 4.096–8.192ms | 349 |
| Dekaf | 1 | 8.192–16.384ms | 8 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 32.768–65.536ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 12,170,000 | 2026-07-29T17:58:09.9190295+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,577,375 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 12,180,000 | 2026-07-29T17:58:09.9238797+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,577,375 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 628,990,000 | 2026-07-29T18:04:30.9191356+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 390.1s / 1,496,616 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 629,000,000 | 2026-07-29T18:04:30.9256707+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 390.1s / 1,496,616 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 629,010,000 | 2026-07-29T18:04:30.9378352+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 390.1s / 1,496,616 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 10,438,000 | 2026-07-29T18:13:10.0295003+00:00 | 101.9ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 10,541,000 | 2026-07-29T18:13:10.1340295+00:00 | 107.7ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 10,597,000 | 2026-07-29T18:13:10.1904457+00:00 | 104.4ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 10,641,000 | 2026-07-29T18:13:10.2247419+00:00 | 114.9ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 10,657,000 | 2026-07-29T18:13:10.2384329+00:00 | 107.7ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 10,668,000 | 2026-07-29T18:13:10.2484342+00:00 | 111.4ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 10,737,000 | 2026-07-29T18:13:10.3268635+00:00 | 102.2ms | GC pause | - | - | 9.0s / 1,060,969 msg/s | Gen2 +0 / pause +97.8ms |
| Confluent | 11,432,000 | 2026-07-29T18:13:10.9688225+00:00 | 110.2ms | GC pause | - | - | 10.0s / 962,905 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 11,447,000 | 2026-07-29T18:13:10.9785644+00:00 | 126.8ms | GC pause | - | - | 10.0s / 962,905 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 11,457,000 | 2026-07-29T18:13:10.9885497+00:00 | 124.7ms | GC pause | - | - | 10.0s / 962,905 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 11,469,000 | 2026-07-29T18:13:10.9962696+00:00 | 109.1ms | GC pause | - | - | 10.0s / 962,905 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 11,471,000 | 2026-07-29T18:13:10.997272+00:00 | 139.1ms | GC pause | - | - | 10.0s / 962,905 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 11,497,000 | 2026-07-29T18:13:11.0371409+00:00 | 109.4ms | GC pause | - | - | 10.0s / 962,905 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 29,370,000 | 2026-07-29T18:13:28.0219004+00:00 | 118.1ms | GC pause | - | - | 27.0s / 972,247 msg/s | Gen2 +0 / pause +86.0ms |
| Confluent | 29,420,000 | 2026-07-29T18:13:28.1193177+00:00 | 112.6ms | GC pause | - | - | 27.0s / 972,247 msg/s | Gen2 +0 / pause +86.0ms |
| Confluent | 30,358,000 | 2026-07-29T18:13:29.1060581+00:00 | 107.8ms | GC pause | - | - | 28.0s / 961,873 msg/s | Gen2 +0 / pause +100.3ms |
| Confluent | 36,548,000 | 2026-07-29T18:13:34.8988878+00:00 | 101.1ms | GC pause | - | - | 34.0s / 1,114,387 msg/s | Gen2 +0 / pause +93.3ms |
| Confluent | 36,557,000 | 2026-07-29T18:13:34.9076022+00:00 | 105.2ms | GC pause | - | - | 34.0s / 1,114,387 msg/s | Gen2 +0 / pause +93.3ms |
| Confluent | 36,581,000 | 2026-07-29T18:13:34.9288265+00:00 | 103.9ms | GC pause | - | - | 34.0s / 1,114,387 msg/s | Gen2 +0 / pause +93.3ms |
| Confluent | 57,810,000 | 2026-07-29T18:13:54.6086287+00:00 | 104.3ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,907,000 | 2026-07-29T18:13:54.6721636+00:00 | 110.3ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,913,000 | 2026-07-29T18:13:54.6777553+00:00 | 112.5ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,947,000 | 2026-07-29T18:13:54.7056935+00:00 | 112.1ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,963,000 | 2026-07-29T18:13:54.7209927+00:00 | 117.6ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,968,000 | 2026-07-29T18:13:54.725933+00:00 | 108.9ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,970,000 | 2026-07-29T18:13:54.7271222+00:00 | 117.2ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 57,993,000 | 2026-07-29T18:13:54.7412324+00:00 | 124.4ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,111,000 | 2026-07-29T18:13:54.8248514+00:00 | 171.1ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,138,000 | 2026-07-29T18:13:54.857973+00:00 | 160.2ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,139,000 | 2026-07-29T18:13:54.8587709+00:00 | 131.0ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,148,000 | 2026-07-29T18:13:54.868167+00:00 | 166.5ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,197,000 | 2026-07-29T18:13:54.9216105+00:00 | 155.0ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,204,000 | 2026-07-29T18:13:54.927737+00:00 | 135.1ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,205,000 | 2026-07-29T18:13:54.9286676+00:00 | 117.7ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,222,000 | 2026-07-29T18:13:54.9464404+00:00 | 100.5ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,236,000 | 2026-07-29T18:13:54.9613654+00:00 | 109.6ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,264,000 | 2026-07-29T18:13:55.0002504+00:00 | 123.5ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,277,000 | 2026-07-29T18:13:55.0244635+00:00 | 114.2ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,327,000 | 2026-07-29T18:13:55.080834+00:00 | 112.9ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,350,000 | 2026-07-29T18:13:55.113249+00:00 | 100.4ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,543,000 | 2026-07-29T18:13:55.2790941+00:00 | 103.9ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,547,000 | 2026-07-29T18:13:55.281994+00:00 | 105.4ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 58,568,000 | 2026-07-29T18:13:55.3057683+00:00 | 104.2ms | GC pause | - | - | 54.0s / 1,071,848 msg/s | Gen2 +0 / pause +89.3ms |
| Confluent | 59,157,000 | 2026-07-29T18:13:55.8855046+00:00 | 108.2ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,208,000 | 2026-07-29T18:13:55.9296653+00:00 | 120.1ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,261,000 | 2026-07-29T18:13:55.970466+00:00 | 126.5ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,337,000 | 2026-07-29T18:13:56.0304814+00:00 | 143.7ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,358,000 | 2026-07-29T18:13:56.0619498+00:00 | 133.9ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,401,000 | 2026-07-29T18:13:56.1020605+00:00 | 132.9ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,467,000 | 2026-07-29T18:13:56.1609391+00:00 | 141.3ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,583,000 | 2026-07-29T18:13:56.2526906+00:00 | 109.5ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,613,000 | 2026-07-29T18:13:56.2762256+00:00 | 111.8ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,633,000 | 2026-07-29T18:13:56.2968359+00:00 | 110.8ms | GC pause | - | - | 55.0s / 1,127,295 msg/s | Gen2 +0 / pause +74.1ms |
| Confluent | 59,691,000 | 2026-07-29T18:13:56.3640543+00:00 | 167.4ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,718,000 | 2026-07-29T18:13:56.3822485+00:00 | 175.8ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,738,000 | 2026-07-29T18:13:56.393883+00:00 | 178.6ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,759,000 | 2026-07-29T18:13:56.4091168+00:00 | 104.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,761,000 | 2026-07-29T18:13:56.4101451+00:00 | 182.0ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,813,000 | 2026-07-29T18:13:56.4502732+00:00 | 132.1ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,816,000 | 2026-07-29T18:13:56.4527932+00:00 | 118.2ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,838,000 | 2026-07-29T18:13:56.4710235+00:00 | 206.9ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,847,000 | 2026-07-29T18:13:56.4773957+00:00 | 209.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,856,000 | 2026-07-29T18:13:56.4852335+00:00 | 118.8ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +143.9ms |
| Confluent | 59,878,000 | 2026-07-29T18:13:56.4994645+00:00 | 213.1ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 59,910,000 | 2026-07-29T18:13:56.5227672+00:00 | 162.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 59,924,000 | 2026-07-29T18:13:56.5333288+00:00 | 142.4ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 59,944,000 | 2026-07-29T18:13:56.5503214+00:00 | 144.5ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 59,945,000 | 2026-07-29T18:13:56.5513897+00:00 | 145.3ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 59,972,000 | 2026-07-29T18:13:56.5817549+00:00 | 103.4ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,028,000 | 2026-07-29T18:13:56.6302822+00:00 | 226.6ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,034,000 | 2026-07-29T18:13:56.6372715+00:00 | 134.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,039,000 | 2026-07-29T18:13:56.6414159+00:00 | 144.8ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,043,000 | 2026-07-29T18:13:56.6454515+00:00 | 167.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,074,000 | 2026-07-29T18:13:56.6689325+00:00 | 145.1ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,081,000 | 2026-07-29T18:13:56.6772686+00:00 | 239.0ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,103,000 | 2026-07-29T18:13:56.7018846+00:00 | 163.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,124,000 | 2026-07-29T18:13:56.7281934+00:00 | 126.9ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,178,000 | 2026-07-29T18:13:56.7905067+00:00 | 229.3ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,189,000 | 2026-07-29T18:13:56.8005419+00:00 | 127.3ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,225,000 | 2026-07-29T18:13:56.8272411+00:00 | 152.4ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,249,000 | 2026-07-29T18:13:56.8407014+00:00 | 170.2ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,278,000 | 2026-07-29T18:13:56.8569508+00:00 | 263.8ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,282,000 | 2026-07-29T18:13:56.8588301+00:00 | 130.9ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,314,000 | 2026-07-29T18:13:56.8891513+00:00 | 168.2ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,325,000 | 2026-07-29T18:13:56.908352+00:00 | 162.0ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,330,000 | 2026-07-29T18:13:56.9149691+00:00 | 179.8ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,338,000 | 2026-07-29T18:13:56.9282475+00:00 | 239.6ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,367,000 | 2026-07-29T18:13:56.9660853+00:00 | 225.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,369,000 | 2026-07-29T18:13:56.9681153+00:00 | 151.2ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,394,000 | 2026-07-29T18:13:56.9984671+00:00 | 137.5ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,435,000 | 2026-07-29T18:13:57.0288788+00:00 | 139.6ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,437,000 | 2026-07-29T18:13:57.032102+00:00 | 217.6ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,438,000 | 2026-07-29T18:13:57.0326503+00:00 | 219.6ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,460,000 | 2026-07-29T18:13:57.0459505+00:00 | 165.4ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,503,000 | 2026-07-29T18:13:57.0718441+00:00 | 178.3ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,510,000 | 2026-07-29T18:13:57.0814488+00:00 | 177.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,515,000 | 2026-07-29T18:13:57.0889439+00:00 | 151.2ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,569,000 | 2026-07-29T18:13:57.146915+00:00 | 130.1ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,643,000 | 2026-07-29T18:13:57.2171858+00:00 | 153.3ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,667,000 | 2026-07-29T18:13:57.2362489+00:00 | 225.1ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,707,000 | 2026-07-29T18:13:57.2798316+00:00 | 212.7ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,758,000 | 2026-07-29T18:13:57.3140963+00:00 | 226.8ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,781,000 | 2026-07-29T18:13:57.3349078+00:00 | 222.0ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,819,000 | 2026-07-29T18:13:57.3656288+00:00 | 126.1ms | GC pause | - | - | 56.0s / 1,092,113 msg/s | Gen2 +0 / pause +69.8ms |
| Confluent | 60,873,000 | 2026-07-29T18:13:57.4161937+00:00 | 157.4ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,875,000 | 2026-07-29T18:13:57.4181836+00:00 | 124.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,876,000 | 2026-07-29T18:13:57.4194587+00:00 | 123.4ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,908,000 | 2026-07-29T18:13:57.4485076+00:00 | 228.0ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,914,000 | 2026-07-29T18:13:57.4526208+00:00 | 117.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,933,000 | 2026-07-29T18:13:57.4691557+00:00 | 160.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,936,000 | 2026-07-29T18:13:57.472001+00:00 | 121.2ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,946,000 | 2026-07-29T18:13:57.4802353+00:00 | 125.1ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,948,000 | 2026-07-29T18:13:57.4820439+00:00 | 228.6ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,949,000 | 2026-07-29T18:13:57.4826172+00:00 | 122.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 60,953,000 | 2026-07-29T18:13:57.4863805+00:00 | 160.0ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +147.6ms |
| Confluent | 61,007,000 | 2026-07-29T18:13:57.5286106+00:00 | 229.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,016,000 | 2026-07-29T18:13:57.5338964+00:00 | 135.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,017,000 | 2026-07-29T18:13:57.534441+00:00 | 234.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,030,000 | 2026-07-29T18:13:57.546473+00:00 | 170.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,051,000 | 2026-07-29T18:13:57.5642023+00:00 | 240.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,093,000 | 2026-07-29T18:13:57.5997936+00:00 | 172.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,095,000 | 2026-07-29T18:13:57.6019898+00:00 | 131.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,121,000 | 2026-07-29T18:13:57.6277024+00:00 | 241.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,137,000 | 2026-07-29T18:13:57.6423442+00:00 | 243.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,154,000 | 2026-07-29T18:13:57.6599392+00:00 | 119.1ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,167,000 | 2026-07-29T18:13:57.6780075+00:00 | 247.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,168,000 | 2026-07-29T18:13:57.6786613+00:00 | 246.8ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,215,000 | 2026-07-29T18:13:57.7334955+00:00 | 110.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,219,000 | 2026-07-29T18:13:57.736995+00:00 | 114.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,233,000 | 2026-07-29T18:13:57.7487825+00:00 | 153.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,238,000 | 2026-07-29T18:13:57.7533319+00:00 | 254.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,243,000 | 2026-07-29T18:13:57.7584208+00:00 | 145.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,253,000 | 2026-07-29T18:13:57.767311+00:00 | 153.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,281,000 | 2026-07-29T18:13:57.790576+00:00 | 259.2ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,284,000 | 2026-07-29T18:13:57.7934526+00:00 | 107.6ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,291,000 | 2026-07-29T18:13:57.7987687+00:00 | 262.1ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,319,000 | 2026-07-29T18:13:57.8215729+00:00 | 134.1ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,344,000 | 2026-07-29T18:13:57.842475+00:00 | 127.4ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,409,000 | 2026-07-29T18:13:57.9156435+00:00 | 131.1ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,436,000 | 2026-07-29T18:13:57.9460193+00:00 | 128.6ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,493,000 | 2026-07-29T18:13:58.0096173+00:00 | 169.9ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,505,000 | 2026-07-29T18:13:58.0204827+00:00 | 119.0ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,553,000 | 2026-07-29T18:13:58.0617529+00:00 | 170.4ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,571,000 | 2026-07-29T18:13:58.0773677+00:00 | 244.4ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,581,000 | 2026-07-29T18:13:58.0857447+00:00 | 246.3ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,586,000 | 2026-07-29T18:13:58.0895088+00:00 | 128.8ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,615,000 | 2026-07-29T18:13:58.1099516+00:00 | 134.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,625,000 | 2026-07-29T18:13:58.1155875+00:00 | 139.8ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,630,000 | 2026-07-29T18:13:58.1182264+00:00 | 174.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,637,000 | 2026-07-29T18:13:58.1220366+00:00 | 263.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,660,000 | 2026-07-29T18:13:58.1358176+00:00 | 181.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,704,000 | 2026-07-29T18:13:58.166845+00:00 | 150.8ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,726,000 | 2026-07-29T18:13:58.2023562+00:00 | 142.1ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,739,000 | 2026-07-29T18:13:58.2137706+00:00 | 146.7ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,745,000 | 2026-07-29T18:13:58.219963+00:00 | 142.2ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,764,000 | 2026-07-29T18:13:58.2422911+00:00 | 130.6ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,769,000 | 2026-07-29T18:13:58.2477468+00:00 | 134.8ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,835,000 | 2026-07-29T18:13:58.3074663+00:00 | 149.6ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,859,000 | 2026-07-29T18:13:58.3348278+00:00 | 144.5ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,873,000 | 2026-07-29T18:13:58.3466324+00:00 | 183.8ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 61,884,000 | 2026-07-29T18:13:58.3544074+00:00 | 133.2ms | GC pause | - | - | 57.0s / 1,056,229 msg/s | Gen2 +0 / pause +77.8ms |
| Confluent | 61,891,000 | 2026-07-29T18:13:58.3586252+00:00 | 265.1ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 61,939,000 | 2026-07-29T18:13:58.4038163+00:00 | 145.0ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 61,968,000 | 2026-07-29T18:13:58.4289694+00:00 | 256.4ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 61,979,000 | 2026-07-29T18:13:58.4362287+00:00 | 139.8ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 61,996,000 | 2026-07-29T18:13:58.4639887+00:00 | 122.2ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +164.5ms |
| Confluent | 62,023,000 | 2026-07-29T18:13:58.4973567+00:00 | 153.9ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,058,000 | 2026-07-29T18:13:58.5542048+00:00 | 204.7ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,073,000 | 2026-07-29T18:13:58.5729101+00:00 | 112.0ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,088,000 | 2026-07-29T18:13:58.5863297+00:00 | 185.2ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,107,000 | 2026-07-29T18:13:58.6043324+00:00 | 182.8ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,187,000 | 2026-07-29T18:13:58.7106044+00:00 | 132.4ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,221,000 | 2026-07-29T18:13:58.7489934+00:00 | 118.5ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,378,000 | 2026-07-29T18:13:58.8882571+00:00 | 141.6ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,388,000 | 2026-07-29T18:13:58.8952635+00:00 | 139.8ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,478,000 | 2026-07-29T18:13:58.9719119+00:00 | 144.4ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,491,000 | 2026-07-29T18:13:58.981869+00:00 | 146.0ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,671,000 | 2026-07-29T18:13:59.1346854+00:00 | 150.8ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,718,000 | 2026-07-29T18:13:59.1769829+00:00 | 151.0ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,837,000 | 2026-07-29T18:13:59.2824631+00:00 | 127.5ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 62,857,000 | 2026-07-29T18:13:59.2987225+00:00 | 119.0ms | GC pause | - | - | 58.0s / 1,039,167 msg/s | Gen2 +0 / pause +86.7ms |
| Confluent | 64,761,000 | 2026-07-29T18:14:00.8658767+00:00 | 105.3ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 64,957,000 | 2026-07-29T18:14:01.0622312+00:00 | 105.3ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,021,000 | 2026-07-29T18:14:01.1171113+00:00 | 121.6ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,058,000 | 2026-07-29T18:14:01.1422195+00:00 | 140.7ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,075,000 | 2026-07-29T18:14:01.157393+00:00 | 109.4ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,076,000 | 2026-07-29T18:14:01.1590651+00:00 | 107.7ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,230,000 | 2026-07-29T18:14:01.307141+00:00 | 104.0ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,251,000 | 2026-07-29T18:14:01.3252755+00:00 | 136.2ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,275,000 | 2026-07-29T18:14:01.3439198+00:00 | 109.1ms | GC pause | - | - | 60.0s / 1,098,440 msg/s | Gen2 +0 / pause +76.1ms |
| Confluent | 65,307,000 | 2026-07-29T18:14:01.375343+00:00 | 142.6ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +153.0ms |
| Confluent | 65,578,000 | 2026-07-29T18:14:01.6429322+00:00 | 134.5ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 65,781,000 | 2026-07-29T18:14:01.8517377+00:00 | 113.8ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 65,818,000 | 2026-07-29T18:14:01.8896401+00:00 | 124.0ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 65,847,000 | 2026-07-29T18:14:01.9313688+00:00 | 104.9ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 65,991,000 | 2026-07-29T18:14:02.0504204+00:00 | 116.7ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 66,067,000 | 2026-07-29T18:14:02.1104693+00:00 | 132.6ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 66,117,000 | 2026-07-29T18:14:02.164545+00:00 | 120.6ms | GC pause | - | - | 61.0s / 1,050,214 msg/s | Gen2 +0 / pause +76.9ms |
| Confluent | 66,847,000 | 2026-07-29T18:14:02.8385742+00:00 | 109.1ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 66,878,000 | 2026-07-29T18:14:02.858675+00:00 | 122.8ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 66,927,000 | 2026-07-29T18:14:02.8969376+00:00 | 128.3ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,018,000 | 2026-07-29T18:14:02.9706635+00:00 | 136.0ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,081,000 | 2026-07-29T18:14:03.0125899+00:00 | 141.7ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,098,000 | 2026-07-29T18:14:03.027117+00:00 | 141.5ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,191,000 | 2026-07-29T18:14:03.0958478+00:00 | 156.5ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,237,000 | 2026-07-29T18:14:03.1363078+00:00 | 154.5ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,271,000 | 2026-07-29T18:14:03.1631441+00:00 | 152.2ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,391,000 | 2026-07-29T18:14:03.2752968+00:00 | 140.1ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,407,000 | 2026-07-29T18:14:03.2904151+00:00 | 136.7ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,418,000 | 2026-07-29T18:14:03.3019373+00:00 | 132.9ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,428,000 | 2026-07-29T18:14:03.3154531+00:00 | 123.6ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,431,000 | 2026-07-29T18:14:03.3185862+00:00 | 127.0ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 67,438,000 | 2026-07-29T18:14:03.3252447+00:00 | 123.1ms | GC pause | - | - | 62.0s / 1,128,358 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 68,337,000 | 2026-07-29T18:14:04.1276085+00:00 | 101.0ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,411,000 | 2026-07-29T18:14:04.180209+00:00 | 110.7ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,431,000 | 2026-07-29T18:14:04.19363+00:00 | 116.0ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,458,000 | 2026-07-29T18:14:04.2180509+00:00 | 111.6ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,571,000 | 2026-07-29T18:14:04.3127993+00:00 | 133.0ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,597,000 | 2026-07-29T18:14:04.3327501+00:00 | 140.3ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,611,000 | 2026-07-29T18:14:04.3433247+00:00 | 145.4ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,618,000 | 2026-07-29T18:14:04.3486023+00:00 | 145.1ms | GC pause | - | - | 63.0s / 1,133,096 msg/s | Gen2 +0 / pause +101.8ms |
| Confluent | 68,691,000 | 2026-07-29T18:14:04.4579694+00:00 | 103.3ms | GC pause | - | - | 64.0s / 1,148,807 msg/s | Gen2 +0 / pause +193.2ms |
| Confluent | 87,527,000 | 2026-07-29T18:14:22.1920351+00:00 | 101.3ms | GC pause | - | - | 81.1s / 1,149,532 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 87,537,000 | 2026-07-29T18:14:22.2018413+00:00 | 100.4ms | GC pause | - | - | 81.1s / 1,149,532 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 87,601,000 | 2026-07-29T18:14:22.250491+00:00 | 108.2ms | GC pause | - | - | 81.1s / 1,149,532 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 87,617,000 | 2026-07-29T18:14:22.2622734+00:00 | 112.3ms | GC pause | - | - | 81.1s / 1,149,532 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 87,741,000 | 2026-07-29T18:14:22.3599934+00:00 | 118.2ms | GC pause | - | - | 81.1s / 1,149,532 msg/s | Gen2 +0 / pause +85.9ms |
| Confluent | 142,841,000 | 2026-07-29T18:15:06.2966917+00:00 | 100.9ms | GC pause | - | - | 125.1s / 1,230,550 msg/s | Gen2 +0 / pause +75.1ms |
| Confluent | 143,061,000 | 2026-07-29T18:15:06.4655888+00:00 | 118.9ms | GC pause | - | - | 126.1s / 1,187,550 msg/s | Gen2 +0 / pause +175.8ms |
| Confluent | 143,098,000 | 2026-07-29T18:15:06.5105111+00:00 | 101.5ms | GC pause | - | - | 126.1s / 1,187,550 msg/s | Gen2 +0 / pause +175.8ms |
| Confluent | 143,517,000 | 2026-07-29T18:15:06.8498736+00:00 | 127.3ms | GC pause | - | - | 126.1s / 1,187,550 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 143,621,000 | 2026-07-29T18:15:06.9254927+00:00 | 130.2ms | GC pause | - | - | 126.1s / 1,187,550 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 143,758,000 | 2026-07-29T18:15:07.0389169+00:00 | 133.2ms | GC pause | - | - | 126.1s / 1,187,550 msg/s | Gen2 +0 / pause +100.7ms |
| Confluent | 617,547,000 | 2026-07-29T18:20:23.9882392+00:00 | 111.1ms | GC pause | - | - | 443.2s / 1,022,269 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 617,568,000 | 2026-07-29T18:20:24.0047442+00:00 | 111.0ms | GC pause | - | - | 443.2s / 1,022,269 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 617,594,000 | 2026-07-29T18:20:24.0225964+00:00 | 107.3ms | GC pause | - | - | 443.2s / 1,022,269 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 617,888,000 | 2026-07-29T18:20:24.326699+00:00 | 104.0ms | GC pause | - | - | 443.2s / 1,022,269 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 617,897,000 | 2026-07-29T18:20:24.3368528+00:00 | 107.2ms | GC pause | - | - | 443.2s / 1,022,269 msg/s | Gen2 +0 / pause +78.6ms |
| Confluent | 667,260,000 | 2026-07-29T18:21:03.3584564+00:00 | 101.4ms | GC pause | - | - | 482.3s / 1,085,285 msg/s | Gen2 +0 / pause +84.9ms |
| Confluent | 717,356,000 | 2026-07-29T18:21:44.1634849+00:00 | 100.8ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 717,391,000 | 2026-07-29T18:21:44.1897916+00:00 | 113.2ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 717,397,000 | 2026-07-29T18:21:44.1937537+00:00 | 119.5ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 717,404,000 | 2026-07-29T18:21:44.1976217+00:00 | 108.7ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 717,423,000 | 2026-07-29T18:21:44.2165935+00:00 | 102.3ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 717,488,000 | 2026-07-29T18:21:44.2835727+00:00 | 104.6ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 717,527,000 | 2026-07-29T18:21:44.3158134+00:00 | 108.8ms | GC pause | - | - | 523.3s / 1,141,221 msg/s | Gen2 +0 / pause +80.3ms |
| Confluent | 1,175,497,000 | 2026-07-29T18:27:03.1938482+00:00 | 100.2ms | GC pause | - | - | 842.5s / 1,287,638 msg/s | Gen2 +0 / pause +77.9ms |
| Confluent | 1,179,471,000 | 2026-07-29T18:27:06.1785607+00:00 | 100.5ms | GC pause | - | - | 845.5s / 1,280,818 msg/s | Gen2 +0 / pause +70.7ms |
| Confluent | 1,179,477,000 | 2026-07-29T18:27:06.181643+00:00 | 102.7ms | GC pause | - | - | 845.5s / 1,280,818 msg/s | Gen2 +0 / pause +70.7ms |
| Confluent | 1,179,511,000 | 2026-07-29T18:27:06.2041972+00:00 | 109.2ms | GC pause | - | - | 845.5s / 1,280,818 msg/s | Gen2 +0 / pause +70.7ms |
| Confluent | 1,179,521,000 | 2026-07-29T18:27:06.2098545+00:00 | 110.6ms | GC pause | - | - | 845.5s / 1,280,818 msg/s | Gen2 +0 / pause +70.7ms |
| Confluent | 1,180,458,000 | 2026-07-29T18:27:06.9585859+00:00 | 111.2ms | GC pause | - | - | 846.5s / 1,254,885 msg/s | Gen2 +0 / pause +92.3ms |
| Confluent | 1,180,497,000 | 2026-07-29T18:27:06.9900258+00:00 | 104.9ms | GC pause | - | - | 846.5s / 1,254,885 msg/s | Gen2 +0 / pause +92.3ms |
| Confluent | 1,180,501,000 | 2026-07-29T18:27:06.9920545+00:00 | 107.4ms | GC pause | - | - | 846.5s / 1,254,885 msg/s | Gen2 +0 / pause +92.3ms |
| Confluent | 1,180,617,000 | 2026-07-29T18:27:07.0872912+00:00 | 117.0ms | GC pause | - | - | 846.5s / 1,254,885 msg/s | Gen2 +0 / pause +92.3ms |
| Confluent | 1,180,668,000 | 2026-07-29T18:27:07.1413961+00:00 | 105.4ms | GC pause | - | - | 846.5s / 1,254,885 msg/s | Gen2 +0 / pause +92.3ms |
| Confluent | 131,908,000 | 2026-07-29T18:29:40.5482074+00:00 | 101.8ms | GC pause | - | - | 99.1s / 954,840 msg/s | Gen2 +0 / pause +185.4ms |
| Confluent | 132,047,000 | 2026-07-29T18:29:40.6796842+00:00 | 101.7ms | GC pause | - | - | 99.1s / 954,840 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 132,088,000 | 2026-07-29T18:29:40.7091345+00:00 | 134.1ms | GC pause | - | - | 99.1s / 954,840 msg/s | Gen2 +0 / pause +98.1ms |
| Confluent | 140,918,000 | 2026-07-29T18:29:48.6353792+00:00 | 102.5ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 140,958,000 | 2026-07-29T18:29:48.6887984+00:00 | 107.0ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,011,000 | 2026-07-29T18:29:48.7352829+00:00 | 107.6ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,017,000 | 2026-07-29T18:29:48.7447877+00:00 | 109.8ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,018,000 | 2026-07-29T18:29:48.74512+00:00 | 109.6ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,067,000 | 2026-07-29T18:29:48.8012991+00:00 | 149.5ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,068,000 | 2026-07-29T18:29:48.8022292+00:00 | 148.6ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,089,000 | 2026-07-29T18:29:48.8198205+00:00 | 106.9ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,107,000 | 2026-07-29T18:29:48.8417407+00:00 | 159.2ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,132,000 | 2026-07-29T18:29:48.8678433+00:00 | 121.2ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 141,141,000 | 2026-07-29T18:29:48.8897605+00:00 | 126.6ms | GC pause | - | - | 107.1s / 1,063,805 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 153,071,000 | 2026-07-29T18:29:58.9079476+00:00 | 102.8ms | GC pause | - | - | 117.1s / 1,207,103 msg/s | Gen2 +0 / pause +87.7ms |
| Confluent | 183,508,000 | 2026-07-29T18:30:22.1248348+00:00 | 108.5ms | GC pause | - | - | 140.1s / 1,179,846 msg/s | Gen2 +0 / pause +70.3ms |
| Confluent | 183,857,000 | 2026-07-29T18:30:22.4041964+00:00 | 103.3ms | GC pause | - | - | 140.1s / 1,179,846 msg/s | Gen2 +0 / pause +70.3ms |
| Confluent | 183,871,000 | 2026-07-29T18:30:22.4191002+00:00 | 111.1ms | GC pause | - | - | 140.1s / 1,179,846 msg/s | Gen2 +0 / pause +70.3ms |
| Confluent | 183,877,000 | 2026-07-29T18:30:22.4275832+00:00 | 106.3ms | GC pause | - | - | 140.1s / 1,179,846 msg/s | Gen2 +0 / pause +70.3ms |
| Confluent | 184,021,000 | 2026-07-29T18:30:22.5494001+00:00 | 104.3ms | GC pause | - | - | 141.1s / 1,155,550 msg/s | Gen2 +0 / pause +156.2ms |
| Confluent | 184,027,000 | 2026-07-29T18:30:22.5545817+00:00 | 102.0ms | GC pause | - | - | 141.1s / 1,155,550 msg/s | Gen2 +0 / pause +156.2ms |
| Confluent | 184,028,000 | 2026-07-29T18:30:22.5550798+00:00 | 101.6ms | GC pause | - | - | 141.1s / 1,155,550 msg/s | Gen2 +0 / pause +156.2ms |
| Confluent | 188,688,000 | 2026-07-29T18:30:26.5364626+00:00 | 104.3ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +183.8ms |
| Confluent | 188,737,000 | 2026-07-29T18:30:26.5716335+00:00 | 100.0ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +183.8ms |
| Confluent | 188,788,000 | 2026-07-29T18:30:26.6082022+00:00 | 112.6ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 188,861,000 | 2026-07-29T18:30:26.6639462+00:00 | 127.0ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 188,868,000 | 2026-07-29T18:30:26.6683319+00:00 | 131.0ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 188,938,000 | 2026-07-29T18:30:26.7250179+00:00 | 132.6ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 188,967,000 | 2026-07-29T18:30:26.7452034+00:00 | 138.6ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 188,987,000 | 2026-07-29T18:30:26.7585542+00:00 | 146.2ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 188,999,000 | 2026-07-29T18:30:26.7670214+00:00 | 112.1ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 189,011,000 | 2026-07-29T18:30:26.7908029+00:00 | 145.3ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 189,132,000 | 2026-07-29T18:30:26.9020553+00:00 | 118.9ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 189,134,000 | 2026-07-29T18:30:26.903579+00:00 | 112.0ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 189,138,000 | 2026-07-29T18:30:26.9091467+00:00 | 162.2ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 189,277,000 | 2026-07-29T18:30:27.0691091+00:00 | 112.2ms | GC pause | - | - | 145.1s / 1,100,546 msg/s | Gen2 +0 / pause +103.7ms |
| Confluent | 190,793,000 | 2026-07-29T18:30:28.3387154+00:00 | 101.4ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,807,000 | 2026-07-29T18:30:28.3499572+00:00 | 110.0ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,811,000 | 2026-07-29T18:30:28.3532293+00:00 | 106.9ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,817,000 | 2026-07-29T18:30:28.3567723+00:00 | 113.0ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,827,000 | 2026-07-29T18:30:28.3635887+00:00 | 112.5ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,843,000 | 2026-07-29T18:30:28.3760208+00:00 | 110.9ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,844,000 | 2026-07-29T18:30:28.3765056+00:00 | 103.8ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,849,000 | 2026-07-29T18:30:28.3797156+00:00 | 111.0ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,857,000 | 2026-07-29T18:30:28.3855894+00:00 | 119.7ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,868,000 | 2026-07-29T18:30:28.3929095+00:00 | 127.5ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,877,000 | 2026-07-29T18:30:28.3981759+00:00 | 145.6ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,891,000 | 2026-07-29T18:30:28.4134505+00:00 | 146.3ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,900,000 | 2026-07-29T18:30:28.4235507+00:00 | 142.0ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,913,000 | 2026-07-29T18:30:28.4432611+00:00 | 131.8ms | GC pause | - | - | 146.1s / 1,194,833 msg/s | Gen2 +0 / pause +109.5ms |
| Confluent | 190,916,000 | 2026-07-29T18:30:28.447992+00:00 | 137.9ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,925,000 | 2026-07-29T18:30:28.4592404+00:00 | 128.2ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,934,000 | 2026-07-29T18:30:28.4723669+00:00 | 117.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,953,000 | 2026-07-29T18:30:28.4832738+00:00 | 120.9ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,956,000 | 2026-07-29T18:30:28.4850012+00:00 | 120.8ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,958,000 | 2026-07-29T18:30:28.4859327+00:00 | 135.7ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,960,000 | 2026-07-29T18:30:28.4874467+00:00 | 124.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 190,992,000 | 2026-07-29T18:30:28.5106503+00:00 | 117.5ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 191,008,000 | 2026-07-29T18:30:28.5274361+00:00 | 131.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 191,011,000 | 2026-07-29T18:30:28.5340518+00:00 | 129.6ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 191,023,000 | 2026-07-29T18:30:28.5531982+00:00 | 103.0ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +190.6ms |
| Confluent | 191,038,000 | 2026-07-29T18:30:28.5760037+00:00 | 110.8ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,119,000 | 2026-07-29T18:30:28.6417916+00:00 | 102.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,138,000 | 2026-07-29T18:30:28.6566758+00:00 | 117.3ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,143,000 | 2026-07-29T18:30:28.6597806+00:00 | 102.3ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,159,000 | 2026-07-29T18:30:28.6702106+00:00 | 103.5ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,165,000 | 2026-07-29T18:30:28.6738971+00:00 | 102.8ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,170,000 | 2026-07-29T18:30:28.677313+00:00 | 103.5ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,200,000 | 2026-07-29T18:30:28.7001974+00:00 | 108.8ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,219,000 | 2026-07-29T18:30:28.7186924+00:00 | 100.8ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,220,000 | 2026-07-29T18:30:28.7199934+00:00 | 103.7ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,237,000 | 2026-07-29T18:30:28.7392682+00:00 | 118.0ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,238,000 | 2026-07-29T18:30:28.739901+00:00 | 117.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,281,000 | 2026-07-29T18:30:28.7818449+00:00 | 112.3ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,328,000 | 2026-07-29T18:30:28.8358428+00:00 | 101.9ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,337,000 | 2026-07-29T18:30:28.8460677+00:00 | 117.6ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,368,000 | 2026-07-29T18:30:28.8816492+00:00 | 100.3ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,378,000 | 2026-07-29T18:30:28.8903184+00:00 | 104.5ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,391,000 | 2026-07-29T18:30:28.8992687+00:00 | 109.5ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,398,000 | 2026-07-29T18:30:28.9035149+00:00 | 111.6ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,407,000 | 2026-07-29T18:30:28.9140136+00:00 | 103.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,437,000 | 2026-07-29T18:30:28.9376087+00:00 | 101.4ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,451,000 | 2026-07-29T18:30:28.9457253+00:00 | 102.9ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,458,000 | 2026-07-29T18:30:28.9507135+00:00 | 107.3ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,471,000 | 2026-07-29T18:30:28.9626163+00:00 | 103.3ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,967,000 | 2026-07-29T18:30:29.3939105+00:00 | 105.5ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 191,997,000 | 2026-07-29T18:30:29.4159797+00:00 | 110.7ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 192,017,000 | 2026-07-29T18:30:29.4299035+00:00 | 124.7ms | GC pause | - | - | 147.1s / 1,158,825 msg/s | Gen2 +0 / pause +81.1ms |
| Confluent | 192,117,000 | 2026-07-29T18:30:29.5073528+00:00 | 145.0ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,120,000 | 2026-07-29T18:30:29.5100086+00:00 | 106.8ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,141,000 | 2026-07-29T18:30:29.5262962+00:00 | 144.5ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,153,000 | 2026-07-29T18:30:29.5371033+00:00 | 102.8ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,157,000 | 2026-07-29T18:30:29.5403006+00:00 | 136.1ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,170,000 | 2026-07-29T18:30:29.5535021+00:00 | 104.3ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,171,000 | 2026-07-29T18:30:29.5541644+00:00 | 128.8ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,188,000 | 2026-07-29T18:30:29.5688841+00:00 | 126.0ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,193,000 | 2026-07-29T18:30:29.5732631+00:00 | 100.0ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +174.3ms |
| Confluent | 192,201,000 | 2026-07-29T18:30:29.5810056+00:00 | 128.3ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,221,000 | 2026-07-29T18:30:29.599932+00:00 | 120.6ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,227,000 | 2026-07-29T18:30:29.6053408+00:00 | 121.4ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,248,000 | 2026-07-29T18:30:29.6201342+00:00 | 128.3ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,251,000 | 2026-07-29T18:30:29.6223161+00:00 | 126.2ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,267,000 | 2026-07-29T18:30:29.6341133+00:00 | 122.2ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,298,000 | 2026-07-29T18:30:29.6592843+00:00 | 129.9ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,327,000 | 2026-07-29T18:30:29.6900842+00:00 | 115.9ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,358,000 | 2026-07-29T18:30:29.7118817+00:00 | 119.2ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,368,000 | 2026-07-29T18:30:29.7172467+00:00 | 123.5ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,391,000 | 2026-07-29T18:30:29.7368136+00:00 | 124.7ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,428,000 | 2026-07-29T18:30:29.7755828+00:00 | 111.3ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,528,000 | 2026-07-29T18:30:29.8638304+00:00 | 122.7ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,548,000 | 2026-07-29T18:30:29.8832033+00:00 | 115.9ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,568,000 | 2026-07-29T18:30:29.9081874+00:00 | 112.6ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,618,000 | 2026-07-29T18:30:29.9463891+00:00 | 106.8ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,631,000 | 2026-07-29T18:30:29.9556422+00:00 | 108.2ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,698,000 | 2026-07-29T18:30:30.0278346+00:00 | 101.3ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 192,707,000 | 2026-07-29T18:30:30.0361626+00:00 | 109.3ms | GC pause | - | - | 148.1s / 1,193,213 msg/s | Gen2 +0 / pause +93.2ms |
| Confluent | 341,213,000 | 2026-07-29T18:32:15.1514098+00:00 | 104.0ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,214,000 | 2026-07-29T18:32:15.1539395+00:00 | 101.6ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,271,000 | 2026-07-29T18:32:15.2089055+00:00 | 101.6ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,338,000 | 2026-07-29T18:32:15.254722+00:00 | 109.2ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,378,000 | 2026-07-29T18:32:15.2804929+00:00 | 107.9ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,420,000 | 2026-07-29T18:32:15.3066691+00:00 | 103.4ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,423,000 | 2026-07-29T18:32:15.3084955+00:00 | 102.4ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,468,000 | 2026-07-29T18:32:15.3396208+00:00 | 101.4ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 341,498,000 | 2026-07-29T18:32:15.360163+00:00 | 104.4ms | GC pause | - | - | 253.2s / 1,356,448 msg/s | Gen2 +0 / pause +73.2ms |
| Confluent | 347,709,000 | 2026-07-29T18:32:19.8672975+00:00 | 102.5ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,717,000 | 2026-07-29T18:32:19.8724044+00:00 | 121.2ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,731,000 | 2026-07-29T18:32:19.8873797+00:00 | 114.2ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,747,000 | 2026-07-29T18:32:19.8992252+00:00 | 117.1ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,787,000 | 2026-07-29T18:32:19.9300139+00:00 | 124.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,791,000 | 2026-07-29T18:32:19.9352794+00:00 | 124.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,798,000 | 2026-07-29T18:32:19.9502383+00:00 | 110.2ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,837,000 | 2026-07-29T18:32:19.9940894+00:00 | 100.4ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,888,000 | 2026-07-29T18:32:20.034758+00:00 | 105.1ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 347,977,000 | 2026-07-29T18:32:20.1030324+00:00 | 111.6ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,018,000 | 2026-07-29T18:32:20.1325076+00:00 | 116.4ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,057,000 | 2026-07-29T18:32:20.1604677+00:00 | 119.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,068,000 | 2026-07-29T18:32:20.1680247+00:00 | 122.7ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,077,000 | 2026-07-29T18:32:20.1734465+00:00 | 120.2ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,081,000 | 2026-07-29T18:32:20.1765952+00:00 | 121.7ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,111,000 | 2026-07-29T18:32:20.1978562+00:00 | 121.7ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,178,000 | 2026-07-29T18:32:20.2472352+00:00 | 133.4ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,190,000 | 2026-07-29T18:32:20.2548022+00:00 | 101.3ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,218,000 | 2026-07-29T18:32:20.2779305+00:00 | 133.5ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,220,000 | 2026-07-29T18:32:20.2791631+00:00 | 102.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,227,000 | 2026-07-29T18:32:20.2834503+00:00 | 139.3ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,238,000 | 2026-07-29T18:32:20.292759+00:00 | 136.3ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,243,000 | 2026-07-29T18:32:20.2964726+00:00 | 103.4ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,253,000 | 2026-07-29T18:32:20.3044881+00:00 | 104.5ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,268,000 | 2026-07-29T18:32:20.3148134+00:00 | 139.5ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,307,000 | 2026-07-29T18:32:20.3424095+00:00 | 137.3ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,320,000 | 2026-07-29T18:32:20.3520868+00:00 | 110.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,321,000 | 2026-07-29T18:32:20.3529848+00:00 | 140.4ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,340,000 | 2026-07-29T18:32:20.3733729+00:00 | 105.3ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,367,000 | 2026-07-29T18:32:20.3941182+00:00 | 134.8ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,383,000 | 2026-07-29T18:32:20.4054453+00:00 | 104.2ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,387,000 | 2026-07-29T18:32:20.4083539+00:00 | 132.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,423,000 | 2026-07-29T18:32:20.4358343+00:00 | 102.7ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,438,000 | 2026-07-29T18:32:20.4465438+00:00 | 133.8ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,441,000 | 2026-07-29T18:32:20.4487685+00:00 | 135.0ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,457,000 | 2026-07-29T18:32:20.4601443+00:00 | 133.7ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,483,000 | 2026-07-29T18:32:20.4803025+00:00 | 102.9ms | GC pause | - | - | 258.2s / 1,257,092 msg/s | Gen2 +0 / pause +54.0ms |
| Confluent | 348,517,000 | 2026-07-29T18:32:20.5038355+00:00 | 144.0ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +128.4ms |
| Confluent | 348,551,000 | 2026-07-29T18:32:20.5388686+00:00 | 142.5ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +128.4ms |
| Confluent | 348,577,000 | 2026-07-29T18:32:20.5598413+00:00 | 133.9ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +128.4ms |
| Confluent | 348,613,000 | 2026-07-29T18:32:20.5895524+00:00 | 102.9ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +128.4ms |
| Confluent | 348,648,000 | 2026-07-29T18:32:20.6182526+00:00 | 136.4ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +128.4ms |
| Confluent | 348,651,000 | 2026-07-29T18:32:20.6200944+00:00 | 134.7ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +128.4ms |
| Confluent | 348,691,000 | 2026-07-29T18:32:20.6554179+00:00 | 136.4ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 348,751,000 | 2026-07-29T18:32:20.7219082+00:00 | 115.6ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 348,891,000 | 2026-07-29T18:32:20.8629605+00:00 | 101.0ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 348,951,000 | 2026-07-29T18:32:20.9028067+00:00 | 108.5ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 348,961,000 | 2026-07-29T18:32:20.9098414+00:00 | 124.3ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,018,000 | 2026-07-29T18:32:20.9539646+00:00 | 137.0ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,031,000 | 2026-07-29T18:32:20.974561+00:00 | 120.0ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,061,000 | 2026-07-29T18:32:20.9988706+00:00 | 104.6ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,067,000 | 2026-07-29T18:32:21.0024601+00:00 | 101.4ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,088,000 | 2026-07-29T18:32:21.0158657+00:00 | 106.9ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,108,000 | 2026-07-29T18:32:21.0270719+00:00 | 110.6ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,357,000 | 2026-07-29T18:32:21.2474209+00:00 | 113.5ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,361,000 | 2026-07-29T18:32:21.250127+00:00 | 112.6ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,451,000 | 2026-07-29T18:32:21.3169513+00:00 | 113.2ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,477,000 | 2026-07-29T18:32:21.335062+00:00 | 119.7ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,481,000 | 2026-07-29T18:32:21.3372976+00:00 | 117.5ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,491,000 | 2026-07-29T18:32:21.3454307+00:00 | 115.4ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,558,000 | 2026-07-29T18:32:21.3982669+00:00 | 122.4ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,587,000 | 2026-07-29T18:32:21.4202115+00:00 | 123.4ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,668,000 | 2026-07-29T18:32:21.4932006+00:00 | 118.6ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 349,721,000 | 2026-07-29T18:32:21.5270977+00:00 | 107.8ms | GC pause | - | - | 259.2s / 1,183,101 msg/s | Gen2 +0 / pause +74.4ms |
| Confluent | 364,081,000 | 2026-07-29T18:32:31.4715365+00:00 | 105.1ms | GC pause | - | - | 269.2s / 1,547,093 msg/s | Gen2 +0 / pause +70.3ms |
| Confluent | 364,118,000 | 2026-07-29T18:32:31.502405+00:00 | 110.9ms | GC pause | - | - | 269.2s / 1,547,093 msg/s | Gen2 +0 / pause +70.3ms |
| Confluent | 364,230,000 | 2026-07-29T18:32:31.5802744+00:00 | 101.9ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,241,000 | 2026-07-29T18:32:31.5861096+00:00 | 113.2ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,250,000 | 2026-07-29T18:32:31.5932545+00:00 | 101.8ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,258,000 | 2026-07-29T18:32:31.5990198+00:00 | 112.3ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,280,000 | 2026-07-29T18:32:31.6128785+00:00 | 104.6ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,320,000 | 2026-07-29T18:32:31.6449889+00:00 | 103.4ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,323,000 | 2026-07-29T18:32:31.6468278+00:00 | 102.4ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +159.4ms |
| Confluent | 364,327,000 | 2026-07-29T18:32:31.6500213+00:00 | 106.1ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 364,341,000 | 2026-07-29T18:32:31.6578701+00:00 | 105.6ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 364,411,000 | 2026-07-29T18:32:31.7082542+00:00 | 109.4ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 364,421,000 | 2026-07-29T18:32:31.7158364+00:00 | 103.5ms | GC pause | - | - | 270.2s / 1,350,270 msg/s | Gen2 +0 / pause +89.1ms |
| Confluent | 369,168,000 | 2026-07-29T18:32:35.2068629+00:00 | 106.0ms | GC pause | - | - | 273.2s / 1,331,674 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 369,208,000 | 2026-07-29T18:32:35.2356792+00:00 | 102.4ms | GC pause | - | - | 273.2s / 1,331,674 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 369,237,000 | 2026-07-29T18:32:35.2555513+00:00 | 104.2ms | GC pause | - | - | 273.2s / 1,331,674 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 369,278,000 | 2026-07-29T18:32:35.2853093+00:00 | 111.5ms | GC pause | - | - | 273.2s / 1,331,674 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 369,287,000 | 2026-07-29T18:32:35.2922212+00:00 | 105.9ms | GC pause | - | - | 273.2s / 1,331,674 msg/s | Gen2 +0 / pause +75.8ms |
| Confluent | 369,628,000 | 2026-07-29T18:32:35.5635623+00:00 | 102.6ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 369,747,000 | 2026-07-29T18:32:35.6519689+00:00 | 107.9ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,767,000 | 2026-07-29T18:32:35.6670065+00:00 | 112.8ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,771,000 | 2026-07-29T18:32:35.6700157+00:00 | 112.7ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,838,000 | 2026-07-29T18:32:35.7199732+00:00 | 111.7ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,861,000 | 2026-07-29T18:32:35.7364415+00:00 | 114.6ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,877,000 | 2026-07-29T18:32:35.7484752+00:00 | 113.4ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,887,000 | 2026-07-29T18:32:35.7557098+00:00 | 114.5ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,958,000 | 2026-07-29T18:32:35.8135274+00:00 | 114.9ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 369,971,000 | 2026-07-29T18:32:35.8227897+00:00 | 115.8ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,011,000 | 2026-07-29T18:32:35.8532576+00:00 | 125.4ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,038,000 | 2026-07-29T18:32:35.8739742+00:00 | 127.5ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,041,000 | 2026-07-29T18:32:35.8760916+00:00 | 125.4ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,051,000 | 2026-07-29T18:32:35.8849079+00:00 | 125.8ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,101,000 | 2026-07-29T18:32:35.9509597+00:00 | 105.8ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,147,000 | 2026-07-29T18:32:35.9853637+00:00 | 109.9ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,161,000 | 2026-07-29T18:32:35.9961681+00:00 | 109.1ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,178,000 | 2026-07-29T18:32:36.0089596+00:00 | 105.8ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,201,000 | 2026-07-29T18:32:36.0237864+00:00 | 111.5ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,207,000 | 2026-07-29T18:32:36.0271975+00:00 | 112.1ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,257,000 | 2026-07-29T18:32:36.0678857+00:00 | 117.8ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,361,000 | 2026-07-29T18:32:36.1458266+00:00 | 128.3ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,371,000 | 2026-07-29T18:32:36.1547032+00:00 | 128.0ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,428,000 | 2026-07-29T18:32:36.195428+00:00 | 130.6ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,437,000 | 2026-07-29T18:32:36.2009678+00:00 | 130.9ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,541,000 | 2026-07-29T18:32:36.2843642+00:00 | 132.1ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,591,000 | 2026-07-29T18:32:36.3219888+00:00 | 138.4ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,608,000 | 2026-07-29T18:32:36.3345806+00:00 | 140.4ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,658,000 | 2026-07-29T18:32:36.3706894+00:00 | 148.9ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,667,000 | 2026-07-29T18:32:36.3765363+00:00 | 144.3ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,691,000 | 2026-07-29T18:32:36.3988863+00:00 | 140.6ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,701,000 | 2026-07-29T18:32:36.4063769+00:00 | 140.7ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,728,000 | 2026-07-29T18:32:36.4271263+00:00 | 143.5ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,748,000 | 2026-07-29T18:32:36.4440786+00:00 | 147.7ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,808,000 | 2026-07-29T18:32:36.4891468+00:00 | 148.3ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 370,818,000 | 2026-07-29T18:32:36.4966027+00:00 | 142.2ms | GC pause | - | - | 274.2s / 1,236,084 msg/s | Gen2 +0 / pause +56.4ms |
| Confluent | 1,111,888,000 | 2026-07-29T18:40:52.8913443+00:00 | 123.3ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +197.9ms |
| Confluent | 1,111,890,000 | 2026-07-29T18:40:52.8944843+00:00 | 106.3ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +197.9ms |
| Confluent | 1,111,902,000 | 2026-07-29T18:40:52.9061063+00:00 | 100.9ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +197.9ms |
| Confluent | 1,111,903,000 | 2026-07-29T18:40:52.9075141+00:00 | 107.7ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +197.9ms |
| Confluent | 1,111,957,000 | 2026-07-29T18:40:52.9510052+00:00 | 116.9ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +197.9ms |
| Confluent | 1,112,047,000 | 2026-07-29T18:40:53.031514+00:00 | 107.8ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,048,000 | 2026-07-29T18:40:53.0322287+00:00 | 107.1ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,051,000 | 2026-07-29T18:40:53.0379229+00:00 | 101.5ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,057,000 | 2026-07-29T18:40:53.0441973+00:00 | 100.2ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,168,000 | 2026-07-29T18:40:53.1201245+00:00 | 103.2ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,207,000 | 2026-07-29T18:40:53.1492884+00:00 | 101.6ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,278,000 | 2026-07-29T18:40:53.2056153+00:00 | 100.9ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |
| Confluent | 1,112,297,000 | 2026-07-29T18:40:53.2268331+00:00 | 103.3ms | GC pause | - | - | 771.5s / 1,175,975 msg/s | Gen2 +0 / pause +110.5ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*7,230 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.29x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.13x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.31 | 1299.03 | 1,030,894 | 1,068,208 | -8.3% | -1.05% | 983.14 | 1,030,894 | 0 | 1.35 |
| Dekaf | 1.35 | 1365.79 | 968,823 | 979,301 | +5.1% | +0.50% | 923.94 | 968,823 | 0 | 1.31 |
| Confluent | 2.72 | - | 610,602 | 597,948 | +5.8% | +0.30% | 582.32 | 610,602 | 0 | 1.66 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 290,240 | 322.48 | 1015.97 KB |
| Dekaf | 2 | 285,902 | 317.66 | 1005.01 KB |
| Dekaf | 3 | 284,383 | 315.97 | 1000.63 KB |
| Dekaf (3conn) | 1 | 306,734 | 340.81 | 985.95 KB |
| Dekaf (3conn) | 2 | 319,362 | 354.84 | 992.89 KB |
| Dekaf (3conn) | 3 | 306,624 | 340.69 | 987.51 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:15.9102145+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 264,839 msg/s |
| Dekaf | 2026-07-29T17:58:33.9206969+00:00 | 3 | 16.0 MiB / 16.0 MiB | 280.2 MB/s | 0/0 | 2,332 | 18.0s / 852,594 msg/s |
| Dekaf | 2026-07-29T17:58:52.9255706+00:00 | 1 | 16.0 MiB / 16.0 MiB | 336.2 MB/s | 0/0 | 16,413 | 37.0s / 927,077 msg/s |
| Dekaf | 2026-07-29T17:59:10.9349243+00:00 | 1 | 16.0 MiB / 15.3 MiB | 355.1 MB/s | 0/1 | 21,207 | 55.0s / 922,440 msg/s |
| Dekaf | 2026-07-29T17:59:28.9427259+00:00 | 2 | 16.0 MiB / 4.3 MiB | 347.4 MB/s | 0/1 | 3,469 | 73.0s / 954,900 msg/s |
| Dekaf | 2026-07-29T17:59:46.9487129+00:00 | 2 | 16.0 MiB / 8.5 MiB | 347.4 MB/s | 0/2 | 4,125 | 91.0s / 934,055 msg/s |
| Dekaf | 2026-07-29T18:00:04.9549561+00:00 | 3 | 14.0 MiB / 13.2 MiB | 339.4 MB/s | 0/2 | 8,726 | 109.1s / 941,042 msg/s |
| Dekaf | 2026-07-29T18:00:22.962559+00:00 | 3 | 16.0 MiB / 6.8 MiB | 339.4 MB/s | 0/3 | 10,788 | 127.1s / 952,285 msg/s |
| Dekaf | 2026-07-29T18:00:41.9685515+00:00 | 1 | 14.0 MiB / 12.9 MiB | 359.8 MB/s | 1/2 | 53,422 | 146.1s / 970,611 msg/s |
| Dekaf | 2026-07-29T18:00:59.9766642+00:00 | 1 | 12.0 MiB / 11.9 MiB | 359.8 MB/s | 2/2 | 59,720 | 164.1s / 943,764 msg/s |
| Dekaf | 2026-07-29T18:01:17.982224+00:00 | 2 | 12.0 MiB / 2.6 MiB | 353.5 MB/s | 1/3 | 8,179 | 182.1s / 942,401 msg/s |
| Dekaf | 2026-07-29T18:01:35.9869597+00:00 | 2 | 10.0 MiB / 9.6 MiB | 353.5 MB/s | 2/3 | 11,503 | 200.1s / 986,373 msg/s |
| Dekaf | 2026-07-29T18:01:53.9905872+00:00 | 3 | 16.0 MiB / 2.6 MiB | 340.8 MB/s | 0/3 | 11,327 | 218.1s / 964,547 msg/s |
| Dekaf | 2026-07-29T18:02:11.997403+00:00 | 3 | 16.0 MiB / 7.4 MiB | 350.4 MB/s | 0/3 | 11,378 | 236.1s / 1,024,988 msg/s |
| Dekaf | 2026-07-29T18:02:31.0077183+00:00 | 1 | 10.0 MiB / 9.0 MiB | 365.4 MB/s | 3/3 | 90,987 | 255.1s / 1,010,432 msg/s |
| Dekaf | 2026-07-29T18:02:49.0112698+00:00 | 1 | 8.0 MiB / 8.0 MiB | 365.4 MB/s | 4/3 | 96,652 | 273.1s / 933,831 msg/s |
| Dekaf | 2026-07-29T18:03:07.0184494+00:00 | 2 | 8.0 MiB / 2.6 MiB | 374.5 MB/s | 4/5 | 29,250 | 291.1s / 980,492 msg/s |
| Dekaf | 2026-07-29T18:03:25.026081+00:00 | 2 | 8.0 MiB / 3.8 MiB | 378.7 MB/s | 4/5 | 34,021 | 309.1s / 939,582 msg/s |
| Dekaf | 2026-07-29T18:03:43.0305713+00:00 | 3 | 16.0 MiB / 7.4 MiB | 367.6 MB/s | 0/3 | 11,800 | 327.1s / 1,059,064 msg/s |
| Dekaf | 2026-07-29T18:04:01.0383853+00:00 | 3 | 16.0 MiB / 3.6 MiB | 382.7 MB/s | 0/3 | 11,840 | 345.1s / 1,062,019 msg/s |
| Dekaf | 2026-07-29T18:04:20.0450928+00:00 | 1 | 8.0 MiB / 7.9 MiB | 394.4 MB/s | 4/5 | 133,771 | 364.1s / 869,304 msg/s |
| Dekaf | 2026-07-29T18:04:38.0538124+00:00 | 1 | 8.0 MiB / 8.0 MiB | 394.4 MB/s | 4/6 | 141,409 | 382.2s / 990,136 msg/s |
| Dekaf | 2026-07-29T18:04:56.0588091+00:00 | 2 | 7.0 MiB / 1.8 MiB | 393.4 MB/s | 5/6 | 58,817 | 400.2s / 910,340 msg/s |
| Dekaf | 2026-07-29T18:05:14.0637515+00:00 | 2 | 6.0 MiB / 5.1 MiB | 393.4 MB/s | 6/6 | 65,112 | 418.2s / 935,791 msg/s |
| Dekaf | 2026-07-29T18:05:32.0668751+00:00 | 3 | 12.0 MiB / 6.8 MiB | 384.9 MB/s | 2/4 | 16,519 | 436.2s / 995,786 msg/s |
| Dekaf | 2026-07-29T18:05:50.0726129+00:00 | 3 | 12.0 MiB / 6.2 MiB | 384.9 MB/s | 2/4 | 16,766 | 454.2s / 967,621 msg/s |
| Dekaf | 2026-07-29T18:06:09.0764167+00:00 | 1 | 7.0 MiB / 7.0 MiB | 394.4 MB/s | 5/7 | 172,086 | 473.2s / 997,216 msg/s |
| Dekaf | 2026-07-29T18:06:27.0795809+00:00 | 1 | 7.0 MiB / 3.2 MiB | 394.4 MB/s | 5/8 | 180,228 | 491.2s / 1,075,919 msg/s |
| Dekaf | 2026-07-29T18:06:45.0889919+00:00 | 2 | 8.0 MiB / 4.3 MiB | 393.4 MB/s | 8/6 | 85,008 | 509.2s / 980,615 msg/s |
| Dekaf | 2026-07-29T18:07:03.0909768+00:00 | 2 | 8.0 MiB / 3.2 MiB | 393.4 MB/s | 8/7 | 88,751 | 527.2s / 1,034,510 msg/s |
| Dekaf | 2026-07-29T18:07:21.0946543+00:00 | 3 | 10.0 MiB / 1.6 MiB | 384.9 MB/s | 3/6 | 21,228 | 545.2s / 1,008,478 msg/s |
| Dekaf | 2026-07-29T18:07:39.1018503+00:00 | 3 | 8.0 MiB / 3.5 MiB | 384.9 MB/s | 3/6 | 21,273 | 563.3s / 893,264 msg/s |
| Dekaf | 2026-07-29T18:07:58.1083989+00:00 | 1 | 6.0 MiB / 5.2 MiB | 394.4 MB/s | 6/10 | 239,789 | 582.3s / 904,479 msg/s |
| Dekaf | 2026-07-29T18:08:16.1167968+00:00 | 1 | 6.0 MiB / 6.0 MiB | 400.1 MB/s | 6/10 | 253,509 | 600.3s / 915,253 msg/s |
| Dekaf | 2026-07-29T18:08:34.1212861+00:00 | 2 | 8.0 MiB / 7.2 MiB | 393.4 MB/s | 8/8 | 101,892 | 618.3s / 1,112,719 msg/s |
| Dekaf | 2026-07-29T18:08:52.1255501+00:00 | 2 | 8.0 MiB / 3.5 MiB | 393.4 MB/s | 8/8 | 104,853 | 636.3s / 1,021,133 msg/s |
| Dekaf | 2026-07-29T18:09:10.1321695+00:00 | 3 | 8.0 MiB / 4.3 MiB | 389.2 MB/s | 4/8 | 23,738 | 654.3s / 1,006,820 msg/s |
| Dekaf | 2026-07-29T18:09:28.1405785+00:00 | 3 | 8.0 MiB / 2.6 MiB | 389.2 MB/s | 4/8 | 24,285 | 672.3s / 972,598 msg/s |
| Dekaf | 2026-07-29T18:09:47.1470676+00:00 | 1 | 6.0 MiB / 4.6 MiB | 402.6 MB/s | 8/11 | 321,977 | 691.3s / 994,478 msg/s |
| Dekaf | 2026-07-29T18:10:05.152028+00:00 | 1 | 6.0 MiB / 5.2 MiB | 402.6 MB/s | 8/11 | 331,585 | 709.3s / 934,231 msg/s |
| Dekaf | 2026-07-29T18:10:23.1585876+00:00 | 2 | 9.0 MiB / 5.4 MiB | 393.4 MB/s | 8/8 | 114,072 | 727.3s / 889,124 msg/s |
| Dekaf | 2026-07-29T18:10:41.1686221+00:00 | 2 | 8.0 MiB / 2.6 MiB | 393.4 MB/s | 8/9 | 116,625 | 745.3s / 1,019,675 msg/s |
| Dekaf | 2026-07-29T18:10:59.1734021+00:00 | 3 | 8.0 MiB / 3.2 MiB | 389.2 MB/s | 4/9 | 30,279 | 763.3s / 1,012,602 msg/s |
| Dekaf | 2026-07-29T18:11:17.1748542+00:00 | 3 | 8.0 MiB / 4.9 MiB | 389.2 MB/s | 4/9 | 31,140 | 781.4s / 990,676 msg/s |
| Dekaf | 2026-07-29T18:11:36.1816475+00:00 | 1 | 7.0 MiB / 4.9 MiB | 402.6 MB/s | 9/12 | 373,957 | 800.4s / 1,008,494 msg/s |
| Dekaf | 2026-07-29T18:11:54.1851643+00:00 | 1 | 6.0 MiB / 4.1 MiB | 402.6 MB/s | 9/12 | 382,346 | 818.4s / 1,029,049 msg/s |
| Dekaf | 2026-07-29T18:12:12.1877067+00:00 | 2 | 8.0 MiB / 3.5 MiB | 393.4 MB/s | 8/9 | 133,757 | 836.4s / 1,000,917 msg/s |
| Dekaf | 2026-07-29T18:12:30.1903941+00:00 | 2 | 8.0 MiB / 4.7 MiB | 393.4 MB/s | 8/9 | 134,887 | 854.4s / 984,696 msg/s |
| Dekaf | 2026-07-29T18:12:48.1944737+00:00 | 3 | 7.0 MiB / 3.2 MiB | 389.2 MB/s | 5/10 | 34,919 | 872.4s / 952,291 msg/s |
| Dekaf | 2026-07-29T18:13:06.2023768+00:00 | 3 | 8.0 MiB / 7.1 MiB | 389.2 MB/s | 5/10 | 35,707 | 890.4s / 985,510 msg/s |
| Dekaf (3conn) | 2026-07-29T18:28:38.3214563+00:00 | 3 | 16.0 MiB / 8.9 MiB | 352.8 MB/s | 0/0 | 1,389 | 9.0s / 862,773 msg/s |
| Dekaf (3conn) | 2026-07-29T18:28:56.3334804+00:00 | 3 | 16.0 MiB / 5.6 MiB | 401.4 MB/s | 0/0 | 2,161 | 27.0s / 1,208,166 msg/s |
| Dekaf (3conn) | 2026-07-29T18:29:15.3416022+00:00 | 1 | 16.0 MiB / 3.6 MiB | 428.7 MB/s | 0/1 | 3,705 | 46.0s / 995,060 msg/s |
| Dekaf (3conn) | 2026-07-29T18:29:33.3515658+00:00 | 1 | 16.0 MiB / 3.2 MiB | 428.7 MB/s | 0/1 | 4,389 | 64.0s / 1,011,238 msg/s |
| Dekaf (3conn) | 2026-07-29T18:29:51.3610285+00:00 | 2 | 16.0 MiB / 10.0 MiB | 441.4 MB/s | 0/1 | 13,799 | 82.1s / 1,117,748 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:09.375481+00:00 | 2 | 14.0 MiB / 3.4 MiB | 454.1 MB/s | 1/1 | 16,729 | 100.1s / 1,182,822 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:27.3897036+00:00 | 3 | 16.0 MiB / 1.8 MiB | 434.8 MB/s | 0/2 | 7,916 | 118.1s / 1,151,018 msg/s |
| Dekaf (3conn) | 2026-07-29T18:30:45.3949455+00:00 | 3 | 16.0 MiB / 7.4 MiB | 438.9 MB/s | 0/2 | 8,810 | 136.1s / 1,195,116 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:04.4032883+00:00 | 1 | 16.0 MiB / 8.9 MiB | 455.2 MB/s | 0/3 | 9,057 | 155.1s / 1,179,433 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:22.4146457+00:00 | 1 | 16.0 MiB / 15.4 MiB | 455.2 MB/s | 0/3 | 9,983 | 173.1s / 1,140,834 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:40.4354147+00:00 | 2 | 14.0 MiB / 5.9 MiB | 460.1 MB/s | 1/4 | 32,301 | 191.1s / 1,129,924 msg/s |
| Dekaf (3conn) | 2026-07-29T18:31:58.445467+00:00 | 2 | 14.0 MiB / 7.4 MiB | 460.1 MB/s | 1/4 | 35,189 | 209.1s / 1,061,806 msg/s |
| Dekaf (3conn) | 2026-07-29T18:32:16.4603351+00:00 | 3 | 14.0 MiB / 12.3 MiB | 438.9 MB/s | 0/4 | 14,529 | 227.1s / 1,204,963 msg/s |
| Dekaf (3conn) | 2026-07-29T18:32:34.4676556+00:00 | 3 | 12.0 MiB / 7.4 MiB | 438.9 MB/s | 1/4 | 17,332 | 245.1s / 1,230,387 msg/s |
| Dekaf (3conn) | 2026-07-29T18:32:53.4717289+00:00 | 1 | 16.0 MiB / 4.7 MiB | 455.2 MB/s | 0/4 | 12,232 | 264.2s / 1,173,719 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:11.4766279+00:00 | 1 | 16.0 MiB / 9.7 MiB | 455.2 MB/s | 0/4 | 12,448 | 282.2s / 1,073,284 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:29.4855863+00:00 | 2 | 10.0 MiB / 4.8 MiB | 467.4 MB/s | 3/6 | 51,507 | 300.2s / 1,266,294 msg/s |
| Dekaf (3conn) | 2026-07-29T18:33:47.491604+00:00 | 2 | 10.0 MiB / 8.6 MiB | 467.4 MB/s | 3/6 | 54,381 | 318.2s / 1,091,161 msg/s |
| Dekaf (3conn) | 2026-07-29T18:34:05.4987483+00:00 | 3 | 12.0 MiB / 10.6 MiB | 443.4 MB/s | 2/6 | 26,879 | 336.2s / 1,143,332 msg/s |
| Dekaf (3conn) | 2026-07-29T18:34:23.5109661+00:00 | 3 | 12.0 MiB / 12.0 MiB | 443.4 MB/s | 2/6 | 28,536 | 354.2s / 1,112,608 msg/s |
| Dekaf (3conn) | 2026-07-29T18:34:42.5162373+00:00 | 1 | 16.0 MiB / 11.7 MiB | 455.2 MB/s | 0/4 | 15,124 | 373.2s / 1,100,402 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:00.5375265+00:00 | 1 | 16.0 MiB / 0.9 MiB | 455.2 MB/s | 0/4 | 15,576 | 391.2s / 1,226,842 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:18.540419+00:00 | 2 | 8.0 MiB / 8.0 MiB | 467.4 MB/s | 4/8 | 75,353 | 409.2s / 1,066,552 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:36.5487259+00:00 | 2 | 8.0 MiB / 3.2 MiB | 467.4 MB/s | 4/8 | 80,239 | 427.2s / 918,581 msg/s |
| Dekaf (3conn) | 2026-07-29T18:35:54.5595294+00:00 | 3 | 12.0 MiB / 4.2 MiB | 443.4 MB/s | 2/8 | 35,792 | 445.2s / 962,620 msg/s |
| Dekaf (3conn) | 2026-07-29T18:36:12.570142+00:00 | 3 | 12.0 MiB / 8.1 MiB | 443.4 MB/s | 2/9 | 37,007 | 463.2s / 1,022,914 msg/s |
| Dekaf (3conn) | 2026-07-29T18:36:31.5796365+00:00 | 1 | 16.0 MiB / 7.7 MiB | 455.2 MB/s | 0/5 | 17,046 | 482.3s / 925,230 msg/s |
| Dekaf (3conn) | 2026-07-29T18:36:49.587101+00:00 | 1 | 16.0 MiB / 8.8 MiB | 455.2 MB/s | 0/6 | 17,347 | 500.3s / 937,180 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:07.5922121+00:00 | 2 | 8.0 MiB / 3.6 MiB | 467.4 MB/s | 4/10 | 101,879 | 518.3s / 955,871 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:25.5955513+00:00 | 2 | 7.0 MiB / 6.2 MiB | 467.4 MB/s | 4/10 | 106,652 | 536.3s / 940,283 msg/s |
| Dekaf (3conn) | 2026-07-29T18:37:43.6043799+00:00 | 3 | 12.0 MiB / 4.6 MiB | 443.4 MB/s | 2/9 | 40,344 | 554.3s / 772,625 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:01.6146223+00:00 | 3 | 12.0 MiB / 1.7 MiB | 443.4 MB/s | 2/9 | 40,437 | 572.3s / 651,494 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:20.6282675+00:00 | 1 | 16.0 MiB / 4.7 MiB | 455.2 MB/s | 0/8 | 18,073 | 591.3s / 679,386 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:38.6355448+00:00 | 1 | 16.0 MiB / 5.7 MiB | 455.2 MB/s | 0/8 | 18,100 | 609.4s / 796,455 msg/s |
| Dekaf (3conn) | 2026-07-29T18:38:56.6525559+00:00 | 2 | 8.0 MiB / 5.3 MiB | 467.4 MB/s | 4/12 | 118,246 | 627.4s / 1,030,974 msg/s |
| Dekaf (3conn) | 2026-07-29T18:39:14.6656124+00:00 | 2 | 8.0 MiB / 4.8 MiB | 467.4 MB/s | 4/12 | 120,729 | 645.4s / 713,037 msg/s |
| Dekaf (3conn) | 2026-07-29T18:39:32.6810519+00:00 | 3 | 12.0 MiB / 10.2 MiB | 443.4 MB/s | 2/9 | 42,763 | 663.4s / 1,002,927 msg/s |
| Dekaf (3conn) | 2026-07-29T18:39:50.6904254+00:00 | 3 | 12.0 MiB / 4.6 MiB | 443.4 MB/s | 2/9 | 44,045 | 681.4s / 1,135,988 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:09.6960013+00:00 | 1 | 16.0 MiB / 10.6 MiB | 455.2 MB/s | 0/8 | 19,639 | 700.4s / 852,437 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:27.7054738+00:00 | 1 | 16.0 MiB / 1.6 MiB | 455.2 MB/s | 0/8 | 19,644 | 718.4s / 825,811 msg/s |
| Dekaf (3conn) | 2026-07-29T18:40:45.7129927+00:00 | 2 | 8.0 MiB / 5.2 MiB | 467.4 MB/s | 4/12 | 141,379 | 736.4s / 1,196,002 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:03.7217346+00:00 | 2 | 8.0 MiB / 8.0 MiB | 467.4 MB/s | 4/12 | 147,604 | 754.4s / 985,430 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:21.725693+00:00 | 3 | 10.0 MiB / 5.1 MiB | 443.4 MB/s | 2/11 | 49,823 | 772.4s / 1,117,974 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:39.7350833+00:00 | 3 | 12.0 MiB / 3.9 MiB | 443.4 MB/s | 2/12 | 50,724 | 790.5s / 1,070,795 msg/s |
| Dekaf (3conn) | 2026-07-29T18:41:58.7459662+00:00 | 1 | 16.0 MiB / 6.9 MiB | 455.2 MB/s | 0/8 | 20,827 | 809.5s / 1,106,598 msg/s |
| Dekaf (3conn) | 2026-07-29T18:42:16.7498714+00:00 | 1 | 16.0 MiB / 2.2 MiB | 455.2 MB/s | 0/8 | 21,047 | 827.5s / 1,098,319 msg/s |
| Dekaf (3conn) | 2026-07-29T18:42:34.7566718+00:00 | 2 | 8.0 MiB / 1.9 MiB | 467.4 MB/s | 4/12 | 168,301 | 845.5s / 1,020,409 msg/s |
| Dekaf (3conn) | 2026-07-29T18:42:52.7674932+00:00 | 2 | 8.0 MiB / 1.9 MiB | 467.4 MB/s | 4/13 | 172,520 | 863.5s / 966,466 msg/s |
| Dekaf (3conn) | 2026-07-29T18:43:10.7781191+00:00 | 3 | 12.0 MiB / 5.5 MiB | 443.4 MB/s | 2/13 | 56,162 | 881.5s / 1,080,412 msg/s |
| Dekaf (3conn) | 2026-07-29T18:43:28.7875175+00:00 | 3 | 12.0 MiB / 5.4 MiB | 443.4 MB/s | 2/13 | 56,779 | 899.5s / 1,202,486 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-29T17:58:46.1724536+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T17:58:46.1878299+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-29T17:59:01.2347337+00:00 | 1 | capacity | failed | 15,063ms | 16.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-29T17:59:31.3324451+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-29T17:59:31.3561793+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-29T17:59:33.3568546+00:00 | 3 | capacity | failed | 2,000ms | 16.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-29T17:59:49.4062604+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-29T18:00:03.4683854+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-29T18:00:17.9851264+00:00 | 2 | capacity | failed | 15,054ms | 16.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T18:00:34.5497533+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-29T18:00:49.6499038+00:00 | 1 | capacity | succeeded | 15,100ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-29T18:01:03.1840948+00:00 | 2 | capacity | succeeded | 15,055ms | 14.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-29T18:01:07.7010727+00:00 | 1 | capacity | succeeded | 15,042ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-29T18:01:21.259344+00:00 | 2 | capacity | succeeded | 15,067ms | 12.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-29T18:01:25.7635454+00:00 | 1 | capacity | failed | 15,051ms | 10.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-29T18:01:42.3321217+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T18:02:00.392074+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-29T18:02:25.9687414+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 7.8 MiB |
| Dekaf | 2026-07-29T18:02:44.0270797+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.6 MiB |
| Dekaf | 2026-07-29T18:02:48.0408634+00:00 | 2 | capacity | failed | 2,504ms | 8.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-29T18:03:18.17671+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-29T18:03:33.2165494+00:00 | 2 | capacity | succeeded | 15,039ms | 7.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-29T18:03:44.2435328+00:00 | 1 | capacity | failed | 15,046ms | 8.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-29T18:04:14.3456185+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-29T18:04:29.394938+00:00 | 1 | capacity | failed | 15,049ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-29T18:04:37.4450763+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-29T18:04:52.5060474+00:00 | 3 | capacity | succeeded | 15,060ms | 12.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-29T18:04:59.5059021+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T18:05:07.0336249+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.5 MiB |
| Dekaf | 2026-07-29T18:05:14.574628+00:00 | 1 | capacity | failed | 15,068ms | 8.0 MiB / 6.0 MiB |
| Dekaf | 2026-07-29T18:05:40.7040826+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-29T18:05:52.2375587+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T18:05:58.7545603+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-29T18:06:02.7403726+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-29T18:06:13.8075451+00:00 | 3 | capacity | failed | 15,052ms | 10.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T18:06:37.3898036+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T18:06:46.3852489+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-29T18:06:52.4595295+00:00 | 2 | capacity | failed | 15,069ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:07:18.9905869+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:07:34.0392624+00:00 | 1 | capacity | succeeded | 15,048ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:07:41.6808278+00:00 | 3 | capacity | failed | 15,062ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-29T18:07:52.7269131+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-29T18:08:11.7927803+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T18:08:26.8612479+00:00 | 3 | capacity | succeeded | 15,068ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-29T18:08:37.2830567+00:00 | 1 | capacity | succeeded | 15,054ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-29T18:08:44.9221651+00:00 | 3 | capacity | failed | 15,045ms | 8.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-29T18:09:25.488476+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:09:45.1694514+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T18:10:08.2465946+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T18:10:23.3025283+00:00 | 2 | capacity | failed | 15,055ms | 8.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-29T18:10:55.8253179+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:11:40.974325+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-29T18:11:59.0346951+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-29T18:12:14.1127332+00:00 | 1 | capacity | succeeded | 15,078ms | 5.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-29T18:12:17.1220339+00:00 | 1 | capacity | started | 0ms | 4.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-29T18:12:32.1626968+00:00 | 1 | capacity | failed | 15,042ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-29T18:13:03.8769284+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:28:59.6168562+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 8.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:14.5934135+00:00 | 2 | capacity | failed | 15,049ms | 16.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:14.6791993+00:00 | 1 | capacity | failed | 15,062ms | 16.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:29:59.7775066+00:00 | 2 | capacity | succeeded | 15,064ms | 14.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:14.8869864+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:16.3926775+00:00 | 3 | capacity | failed | 1,505ms | 16.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:29.993378+00:00 | 1 | capacity | failed | 15,067ms | 16.0 MiB / 13.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:30:47.9321287+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:00.1156001+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:04.1343078+00:00 | 1 | capacity | failed | 4,018ms | 16.0 MiB / 13.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:31.7151444+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:34.2545531+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:31:49.3120754+00:00 | 1 | capacity | failed | 15,057ms | 16.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:05.2583538+00:00 | 2 | capacity | started | 0ms | 15.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:18.4026044+00:00 | 3 | capacity | succeeded | 15,050ms | 14.0 MiB / 1.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:36.4824199+00:00 | 3 | capacity | succeeded | 15,066ms | 12.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:39.5463928+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:32:54.617287+00:00 | 3 | capacity | failed | 15,070ms | 12.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:10.0597792+00:00 | 2 | capacity | succeeded | 15,109ms | 10.0 MiB / 8.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:28.127274+00:00 | 2 | capacity | failed | 15,055ms | 10.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:33:57.8267848+00:00 | 3 | capacity | failed | 3,003ms | 12.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:01.7343931+00:00 | 2 | capacity | failed | 3,510ms | 10.0 MiB / 8.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:31.8313354+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-29T18:34:46.8868585+00:00 | 2 | capacity | succeeded | 15,055ms | 8.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:35:04.946178+00:00 | 2 | capacity | failed | 15,045ms | 8.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:35:16.1618545+00:00 | 3 | capacity | failed | 3,017ms | 12.0 MiB / 11.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:35:50.2726839+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-29T18:36:02.8262961+00:00 | 1 | capacity | failed | 12,553ms | 16.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:36:07.7432054+00:00 | 2 | capacity | failed | 2,505ms | 8.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:36:37.840475+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:36:52.8974777+00:00 | 2 | capacity | failed | 15,057ms | 8.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:37:22.1911511+00:00 | 1 | capacity | failed | 4,027ms | 16.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:37:38.1631946+00:00 | 2 | capacity | failed | 15,069ms | 8.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:38:07.4168321+00:00 | 1 | capacity | failed | 15,091ms | 16.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:38:23.3995599+00:00 | 2 | capacity | failed | 15,088ms | 8.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:40:17.7218333+00:00 | 3 | capacity | failed | 15,073ms | 12.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-29T18:40:50.8618415+00:00 | 3 | capacity | failed | 3,010ms | 12.0 MiB / 11.3 MiB |
| Dekaf (3conn) | 2026-07-29T18:41:36.106816+00:00 | 3 | capacity | failed | 15,044ms | 12.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:08.6113445+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:23.6667608+00:00 | 1 | capacity | failed | 15,055ms | 16.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:42:39.5201866+00:00 | 2 | capacity | failed | 15,088ms | 8.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-29T18:43:08.8283692+00:00 | 1 | capacity | failed | 15,051ms | 16.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-29T18:43:24.6902638+00:00 | 2 | capacity | failed | 15,055ms | 8.0 MiB / 3.9 MiB |
*99 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 2 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 3 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 13 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 53 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 141 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 201 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 241 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 325 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 538 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 863 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 907 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 706 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 399 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 167 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 57 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 8 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 38 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 28 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 112 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 471 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 1,056 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 1,534 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 2,329 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 2,632 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 4,471 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 6,943 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 7,626 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 6,221 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 3,368 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 1,159 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 360 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 43 |
| Dekaf (3conn) | 2 | 65.536–131.072ms | 2 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 12 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 11 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 21 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 113 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 317 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 464 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 583 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 718 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 1,203 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 2,022 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 2,301 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 1,889 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 1,106 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 453 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 257 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 29 |
| Dekaf | 1 | 0.001–0.002ms | 80 |
| Dekaf | 1 | 0.002–0.004ms | 89 |
| Dekaf | 1 | 0.004–0.008ms | 321 |
| Dekaf | 1 | 0.008–0.016ms | 965 |
| Dekaf | 1 | 0.016–0.032ms | 2,440 |
| Dekaf | 1 | 0.032–0.064ms | 3,925 |
| Dekaf | 1 | 0.064–0.128ms | 5,631 |
| Dekaf | 1 | 0.128–0.256ms | 9,116 |
| Dekaf | 1 | 0.256–0.512ms | 17,528 |
| Dekaf | 1 | 0.512–1.024ms | 28,900 |
| Dekaf | 1 | 1.024–2.048ms | 24,975 |
| Dekaf | 1 | 2.048–4.096ms | 9,892 |
| Dekaf | 1 | 4.096–8.192ms | 3,717 |
| Dekaf | 1 | 8.192–16.384ms | 1,316 |
| Dekaf | 1 | 16.384–32.768ms | 577 |
| Dekaf | 1 | 32.768–65.536ms | 49 |
| Dekaf | 2 | 0.001–0.002ms | 36 |
| Dekaf | 2 | 0.002–0.004ms | 33 |
| Dekaf | 2 | 0.004–0.008ms | 110 |
| Dekaf | 2 | 0.008–0.016ms | 388 |
| Dekaf | 2 | 0.016–0.032ms | 1,067 |
| Dekaf | 2 | 0.032–0.064ms | 1,489 |
| Dekaf | 2 | 0.064–0.128ms | 2,083 |
| Dekaf | 2 | 0.128–0.256ms | 3,359 |
| Dekaf | 2 | 0.256–0.512ms | 6,090 |
| Dekaf | 2 | 0.512–1.024ms | 9,382 |
| Dekaf | 2 | 1.024–2.048ms | 7,862 |
| Dekaf | 2 | 2.048–4.096ms | 3,205 |
| Dekaf | 2 | 4.096–8.192ms | 1,296 |
| Dekaf | 2 | 8.192–16.384ms | 495 |
| Dekaf | 2 | 16.384–32.768ms | 243 |
| Dekaf | 2 | 32.768–65.536ms | 16 |
| Dekaf | 2 | 131.072–262.144ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 9 |
| Dekaf | 3 | 0.002–0.004ms | 10 |
| Dekaf | 3 | 0.004–0.008ms | 46 |
| Dekaf | 3 | 0.008–0.016ms | 111 |
| Dekaf | 3 | 0.016–0.032ms | 315 |
| Dekaf | 3 | 0.032–0.064ms | 410 |
| Dekaf | 3 | 0.064–0.128ms | 574 |
| Dekaf | 3 | 0.128–0.256ms | 887 |
| Dekaf | 3 | 0.256–0.512ms | 1,598 |
| Dekaf | 3 | 0.512–1.024ms | 2,372 |
| Dekaf | 3 | 1.024–2.048ms | 1,999 |
| Dekaf | 3 | 2.048–4.096ms | 918 |
| Dekaf | 3 | 4.096–8.192ms | 429 |
| Dekaf | 3 | 8.192–16.384ms | 135 |
| Dekaf | 3 | 16.384–32.768ms | 52 |
| Dekaf | 3 | 32.768–65.536ms | 3 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 43,000 | 2026-07-29T17:58:16.0825229+00:00 | 107.0ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 46,000 | 2026-07-29T17:58:16.0879078+00:00 | 112.8ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 51,000 | 2026-07-29T17:58:16.0996936+00:00 | 159.4ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 53,000 | 2026-07-29T17:58:16.1033406+00:00 | 103.9ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 66,000 | 2026-07-29T17:58:16.1257394+00:00 | 127.1ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 67,000 | 2026-07-29T17:58:16.1272591+00:00 | 250.5ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 71,000 | 2026-07-29T17:58:16.1323604+00:00 | 181.2ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 83,000 | 2026-07-29T17:58:16.1650409+00:00 | 112.8ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 84,000 | 2026-07-29T17:58:16.1754743+00:00 | 117.0ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 90,000 | 2026-07-29T17:58:16.1982887+00:00 | 261.8ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 91,000 | 2026-07-29T17:58:16.199883+00:00 | 196.2ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 104,000 | 2026-07-29T17:58:16.2477788+00:00 | 112.8ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 130,000 | 2026-07-29T17:58:16.3476765+00:00 | 208.9ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 138,000 | 2026-07-29T17:58:16.3779926+00:00 | 143.1ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 143,000 | 2026-07-29T17:58:16.3876306+00:00 | 142.1ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 147,000 | 2026-07-29T17:58:16.3998504+00:00 | 368.8ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 179,000 | 2026-07-29T17:58:16.5619383+00:00 | 108.0ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 184,000 | 2026-07-29T17:58:16.5786366+00:00 | 111.0ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 185,000 | 2026-07-29T17:58:16.5796491+00:00 | 104.6ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 190,000 | 2026-07-29T17:58:16.6049305+00:00 | 138.5ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 199,000 | 2026-07-29T17:58:16.634991+00:00 | 158.3ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 211,000 | 2026-07-29T17:58:16.693726+00:00 | 149.3ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 215,000 | 2026-07-29T17:58:16.7195143+00:00 | 119.8ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 229,000 | 2026-07-29T17:58:16.8144231+00:00 | 116.3ms | GC pause | - | - | 1.0s / 264,839 msg/s | Gen2 +1 / pause +0.7ms |
| Dekaf | 267,000 | 2026-07-29T17:58:16.9651059+00:00 | 281.9ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 286,000 | 2026-07-29T17:58:17.0086497+00:00 | 111.0ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 293,000 | 2026-07-29T17:58:17.0333763+00:00 | 163.7ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 294,000 | 2026-07-29T17:58:17.0339326+00:00 | 104.5ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 297,000 | 2026-07-29T17:58:17.0458981+00:00 | 285.4ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 298,000 | 2026-07-29T17:58:17.0478265+00:00 | 103.9ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 347,000 | 2026-07-29T17:58:17.2695041+00:00 | 279.2ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 373,000 | 2026-07-29T17:58:17.3731929+00:00 | 126.4ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 377,000 | 2026-07-29T17:58:17.4017341+00:00 | 248.5ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 383,000 | 2026-07-29T17:58:17.4148277+00:00 | 104.3ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 447,000 | 2026-07-29T17:58:17.6507273+00:00 | 185.9ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 459,000 | 2026-07-29T17:58:17.6773611+00:00 | 115.3ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 507,000 | 2026-07-29T17:58:17.8241824+00:00 | 127.6ms | throughput collapse | - | - | 2.0s / 315,253 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 737,000 | 2026-07-29T17:58:18.3556334+00:00 | 177.7ms | throughput collapse | - | - | 3.0s / 422,954 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 947,000 | 2026-07-29T17:58:18.8554295+00:00 | 185.9ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,019,000 | 2026-07-29T17:58:19.0415532+00:00 | 100.3ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,241,000 | 2026-07-29T17:58:19.4489563+00:00 | 143.0ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,252,000 | 2026-07-29T17:58:19.47113+00:00 | 140.9ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,281,000 | 2026-07-29T17:58:19.5216578+00:00 | 129.4ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,327,000 | 2026-07-29T17:58:19.6840638+00:00 | 129.6ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,377,000 | 2026-07-29T17:58:19.7849026+00:00 | 121.7ms | throughput collapse | - | - | 4.0s / 477,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,417,000 | 2026-07-29T17:58:19.8549265+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,447,000 | 2026-07-29T17:58:19.9018552+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,477,000 | 2026-07-29T17:58:19.9582249+00:00 | 150.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,487,000 | 2026-07-29T17:58:19.9750317+00:00 | 145.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,537,000 | 2026-07-29T17:58:20.0853804+00:00 | 123.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,687,000 | 2026-07-29T17:58:20.3429385+00:00 | 118.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,897,000 | 2026-07-29T17:58:20.7075328+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,907,000 | 2026-07-29T17:58:20.7222201+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 584,534 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,017,000 | 2026-07-29T17:58:20.8851516+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,087,000 | 2026-07-29T17:58:21.0007035+00:00 | 160.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,137,000 | 2026-07-29T17:58:21.1194039+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,267,000 | 2026-07-29T17:58:21.32026+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,287,000 | 2026-07-29T17:58:21.3568155+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,327,000 | 2026-07-29T17:58:21.4270237+00:00 | 159.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,367,000 | 2026-07-29T17:58:21.5180284+00:00 | 146.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,377,000 | 2026-07-29T17:58:21.536157+00:00 | 153.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,407,000 | 2026-07-29T17:58:21.6164837+00:00 | 137.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,447,000 | 2026-07-29T17:58:21.7003714+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,497,000 | 2026-07-29T17:58:21.7884997+00:00 | 125.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 535,810 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,567,000 | 2026-07-29T17:58:21.9111954+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,657,000 | 2026-07-29T17:58:22.0401986+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,817,000 | 2026-07-29T17:58:22.2794717+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,827,000 | 2026-07-29T17:58:22.2952311+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,957,000 | 2026-07-29T17:58:22.469706+00:00 | 168.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,975,000 | 2026-07-29T17:58:22.4897069+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,007,000 | 2026-07-29T17:58:22.6131468+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 665,794 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,297,000 | 2026-07-29T17:58:23.012238+00:00 | 135.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 695,137 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,317,000 | 2026-07-29T17:58:23.0461067+00:00 | 122.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 695,137 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,377,000 | 2026-07-29T17:58:23.1573744+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 695,137 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,437,000 | 2026-07-29T17:58:23.2458454+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 695,137 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,457,000 | 2026-07-29T17:58:23.2738531+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 695,137 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,637,000 | 2026-07-29T17:58:23.5160523+00:00 | 130.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 695,137 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,927,000 | 2026-07-29T17:58:23.9156896+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 632,877 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,957,000 | 2026-07-29T17:58:23.9535852+00:00 | 138.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 632,877 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,967,000 | 2026-07-29T17:58:23.9705604+00:00 | 150.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 632,877 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,007,000 | 2026-07-29T17:58:24.0366372+00:00 | 146.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 632,877 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,077,000 | 2026-07-29T17:58:24.1844879+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 632,877 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,017,000 | 2026-07-29T17:58:25.48824+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 726,749 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,057,000 | 2026-07-29T17:58:25.5382056+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 726,749 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,187,000 | 2026-07-29T17:58:25.7363146+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 726,749 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,437,000 | 2026-07-29T17:58:26.1510067+00:00 | 155.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 613,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,517,000 | 2026-07-29T17:58:26.3274061+00:00 | 140.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 613,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,547,000 | 2026-07-29T17:58:26.3959925+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 613,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,577,000 | 2026-07-29T17:58:26.4440579+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 613,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,637,000 | 2026-07-29T17:58:26.5374095+00:00 | 145.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 613,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,657,000 | 2026-07-29T17:58:26.6073744+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 613,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,967,000 | 2026-07-29T17:58:27.0007102+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,027,000 | 2026-07-29T17:58:27.109724+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,227,000 | 2026-07-29T17:58:27.3991686+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,267,000 | 2026-07-29T17:58:27.4475183+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,297,000 | 2026-07-29T17:58:27.5122174+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,370,000 | 2026-07-29T17:58:27.6528387+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,450,000 | 2026-07-29T17:58:27.7675773+00:00 | 121.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,460,000 | 2026-07-29T17:58:27.7842765+00:00 | 132.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,490,000 | 2026-07-29T17:58:27.8293356+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 626,449 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,530,000 | 2026-07-29T17:58:27.9114677+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,557,000 | 2026-07-29T17:58:27.9558759+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,560,000 | 2026-07-29T17:58:27.958499+00:00 | 137.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,610,000 | 2026-07-29T17:58:28.0413312+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,680,000 | 2026-07-29T17:58:28.1722589+00:00 | 125.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,720,000 | 2026-07-29T17:58:28.2475499+00:00 | 121.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,880,000 | 2026-07-29T17:58:28.5313088+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 665,460 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,627,000 | 2026-07-29T17:58:29.4914371+00:00 | 127.6ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 723,358 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,727,000 | 2026-07-29T17:58:29.657367+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 723,358 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,967,000 | 2026-07-29T17:58:29.9819475+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 675,029 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,977,000 | 2026-07-29T17:58:29.9932424+00:00 | 136.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 675,029 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,117,000 | 2026-07-29T17:58:30.213278+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 675,029 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,357,000 | 2026-07-29T17:58:30.5408694+00:00 | 140.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 675,029 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,097,000 | 2026-07-29T17:58:31.5313972+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 810,640 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,480,000 | 2026-07-29T17:58:32.0171887+00:00 | 131.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 726,338 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,840,000 | 2026-07-29T17:58:32.5113734+00:00 | 142.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 726,338 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,220,000 | 2026-07-29T17:58:33.0188784+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 852,594 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,230,000 | 2026-07-29T17:58:33.0280406+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 852,594 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,041,000 | 2026-07-29T17:58:33.9870462+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 762,819 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,051,000 | 2026-07-29T17:58:33.9970942+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 762,819 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,062,000 | 2026-07-29T17:58:34.0087716+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 762,819 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,071,000 | 2026-07-29T17:58:34.0209261+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 762,819 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,470,000 | 2026-07-29T17:58:34.533043+00:00 | 140.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 762,819 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,471,000 | 2026-07-29T17:58:34.5338443+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 762,819 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,850,000 | 2026-07-29T17:58:35.0299695+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 887,805 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,137,000 | 2026-07-29T17:58:36.5158716+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 847,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,577,000 | 2026-07-29T17:58:37.023572+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 868,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,992,000 | 2026-07-29T17:58:37.4897267+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 868,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,031,000 | 2026-07-29T17:58:37.5316264+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 868,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,047,000 | 2026-07-29T17:58:37.548776+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 868,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,431,000 | 2026-07-29T17:58:38.0139174+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 908,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,432,000 | 2026-07-29T17:58:38.0156193+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 908,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,451,000 | 2026-07-29T17:58:38.0369562+00:00 | 117.6ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 908,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,891,000 | 2026-07-29T17:58:38.5418977+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 908,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,331,000 | 2026-07-29T17:58:39.0010253+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 827,544 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,747,000 | 2026-07-29T17:58:39.4808129+00:00 | 121.1ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 827,544 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,057,000 | 2026-07-29T17:58:41.025996+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 922,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,462,000 | 2026-07-29T17:58:41.475165+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 922,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,511,000 | 2026-07-29T17:58:41.5376863+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 922,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,512,000 | 2026-07-29T17:58:41.5388683+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 922,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,522,000 | 2026-07-29T17:58:41.5531842+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 922,147 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,451,000 | 2026-07-29T17:58:42.520352+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 930,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,462,000 | 2026-07-29T17:58:42.530931+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 930,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,472,000 | 2026-07-29T17:58:42.540108+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 930,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,481,000 | 2026-07-29T17:58:42.5479072+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 930,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,482,000 | 2026-07-29T17:58:42.5484027+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 930,177 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,911,000 | 2026-07-29T17:58:43.0224852+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 866,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,912,000 | 2026-07-29T17:58:43.0231555+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 866,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,937,000 | 2026-07-29T17:58:43.0463208+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 866,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,337,000 | 2026-07-29T17:58:43.5060493+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 866,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,237,000 | 2026-07-29T17:58:44.5221216+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 892,316 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf | 20,277,000 | 2026-07-29T17:58:44.5654682+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 892,316 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf | 20,647,000 | 2026-07-29T17:58:44.9992035+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 927,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,147,000 | 2026-07-29T17:58:45.5392123+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 927,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,157,000 | 2026-07-29T17:58:45.5491164+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 927,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,597,000 | 2026-07-29T17:58:46.02929+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 937,648 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,617,000 | 2026-07-29T17:58:46.0460609+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 31.0s / 937,648 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,650,000 | 2026-07-29T17:58:50.5451512+00:00 | 119.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 35.0s / 953,211 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,057,000 | 2026-07-29T17:58:50.9712179+00:00 | 101.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 36.0s / 867,893 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,087,000 | 2026-07-29T17:58:51.0080246+00:00 | 111.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 36.0s / 867,893 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,097,000 | 2026-07-29T17:58:51.0196396+00:00 | 108.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 36.0s / 867,893 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,777,000 | 2026-07-29T17:58:54.9916246+00:00 | 110.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 40.0s / 944,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,807,000 | 2026-07-29T17:58:55.0192817+00:00 | 110.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 40.0s / 944,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 31,677,000 | 2026-07-29T17:58:56.9942286+00:00 | 114.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 42.0s / 949,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 31,717,000 | 2026-07-29T17:58:57.0337471+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 42.0s / 949,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 35,511,000 | 2026-07-29T17:59:01.0360843+00:00 | 104.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 46.0s / 952,407 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 39,380,000 | 2026-07-29T17:59:05.0433125+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 952,547 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 41,217,000 | 2026-07-29T17:59:07.0042533+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 906,495 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,117,000 | 2026-07-29T17:59:07.9978015+00:00 | 167.1ms | broker/backlog (no scale or GC event) | - | - | 53.0s / 890,174 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,137,000 | 2026-07-29T17:59:08.0247557+00:00 | 164.5ms | broker/backlog (no scale or GC event) | - | - | 53.0s / 890,174 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,140,000 | 2026-07-29T17:59:08.0282964+00:00 | 115.3ms | broker/backlog (no scale or GC event) | - | - | 53.0s / 890,174 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,000,000 | 2026-07-29T17:59:08.9915229+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 54.0s / 926,978 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,057,000 | 2026-07-29T17:59:09.0467737+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 54.0s / 926,978 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,887,000 | 2026-07-29T17:59:11.0267618+00:00 | 142.6ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 890,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,889,000 | 2026-07-29T17:59:11.0286093+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 890,304 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 45,777,000 | 2026-07-29T17:59:12.0298399+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 924,258 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 45,787,000 | 2026-07-29T17:59:12.0359671+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 924,258 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,197,000 | 2026-07-29T17:59:13.5434773+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 961,578 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 48,587,000 | 2026-07-29T17:59:15.0217074+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 60.0s / 960,340 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 49,562,000 | 2026-07-29T17:59:16.0308789+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 61.0s / 909,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 49,572,000 | 2026-07-29T17:59:16.038632+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 61.0s / 909,432 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 50,887,000 | 2026-07-29T17:59:17.4818215+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 62.0s / 923,213 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 50,897,000 | 2026-07-29T17:59:17.4947218+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 62.0s / 923,213 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 50,907,000 | 2026-07-29T17:59:17.5107176+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 62.0s / 923,213 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,690,000 | 2026-07-29T17:59:20.4956488+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 65.0s / 955,547 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 54,207,000 | 2026-07-29T17:59:21.0311266+00:00 | 118.9ms | broker/backlog (no scale or GC event) | - | - | 66.0s / 925,953 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 55,087,000 | 2026-07-29T17:59:21.9913409+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 949,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 55,567,000 | 2026-07-29T17:59:22.5078005+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 949,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 55,587,000 | 2026-07-29T17:59:22.5273231+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 949,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,572,000 | 2026-07-29T17:59:23.5308191+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 68.0s / 926,265 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 58,887,000 | 2026-07-29T17:59:25.9982806+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 920,367 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 58,920,000 | 2026-07-29T17:59:26.0361589+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 920,367 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 59,360,000 | 2026-07-29T17:59:26.5121145+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 920,367 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 60,310,000 | 2026-07-29T17:59:27.5277463+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 72.0s / 942,627 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 62,137,000 | 2026-07-29T17:59:29.488848+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 74.0s / 857,755 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 62,167,000 | 2026-07-29T17:59:29.5232064+00:00 | 113.0ms | broker/backlog (no scale or GC event) | - | - | 74.0s / 857,755 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 63,047,000 | 2026-07-29T17:59:30.5051371+00:00 | 121.2ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 899,625 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 63,067,000 | 2026-07-29T17:59:30.5284554+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 899,625 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 64,377,000 | 2026-07-29T17:59:31.9895267+00:00 | 102.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/succeeded | - | 77.0s / 950,735 msg/s | Gen2 +0 / pause +1.8ms |
| Dekaf | 68,737,000 | 2026-07-29T17:59:36.5445682+00:00 | 110.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 81.0s / 967,409 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 69,207,000 | 2026-07-29T17:59:37.0486194+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 82.0s / 960,574 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 69,647,000 | 2026-07-29T17:59:37.4991795+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 82.0s / 960,574 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 71,577,000 | 2026-07-29T17:59:39.5109949+00:00 | 104.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 84.0s / 970,037 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 73,007,000 | 2026-07-29T17:59:41.0115296+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 86.0s / 947,728 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 73,027,000 | 2026-07-29T17:59:41.0301883+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 86.0s / 947,728 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 73,047,000 | 2026-07-29T17:59:41.050907+00:00 | 108.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 86.0s / 947,728 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 73,977,000 | 2026-07-29T17:59:42.0289863+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 87.0s / 956,131 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 75,851,000 | 2026-07-29T17:59:44.0152328+00:00 | 111.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 89.0s / 900,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 76,357,000 | 2026-07-29T17:59:44.5267495+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 89.0s / 900,788 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 77,727,000 | 2026-07-29T17:59:46.0114959+00:00 | 121.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 91.0s / 934,055 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 78,691,000 | 2026-07-29T17:59:47.0417147+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 92.0s / 950,506 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 78,701,000 | 2026-07-29T17:59:47.0505737+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 92.0s / 950,506 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 80,077,000 | 2026-07-29T17:59:48.5306931+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 924,648 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 81,467,000 | 2026-07-29T17:59:50.0023759+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 95.1s / 899,290 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 82,360,000 | 2026-07-29T17:59:50.9948957+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 96.1s / 955,265 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 82,370,000 | 2026-07-29T17:59:51.0072885+00:00 | 113.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 96.1s / 955,265 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 82,380,000 | 2026-07-29T17:59:51.0163845+00:00 | 107.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 96.1s / 955,265 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 88,090,000 | 2026-07-29T17:59:57.0329768+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 102.1s / 943,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 88,100,000 | 2026-07-29T17:59:57.0429438+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 102.1s / 943,423 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 89,967,000 | 2026-07-29T17:59:59.0028103+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 104.1s / 958,829 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 89,977,000 | 2026-07-29T17:59:59.0146314+00:00 | 110.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 104.1s / 958,829 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 90,940,000 | 2026-07-29T18:00:00.0191934+00:00 | 148.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 105.1s / 812,035 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 100,307,000 | 2026-07-29T18:00:10.0251939+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 115.1s / 967,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 102,222,000 | 2026-07-29T18:00:12.0070862+00:00 | 107.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 117.1s / 951,383 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 102,252,000 | 2026-07-29T18:00:12.0342516+00:00 | 112.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 117.1s / 951,383 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 102,261,000 | 2026-07-29T18:00:12.041939+00:00 | 112.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 117.1s / 951,383 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 103,220,000 | 2026-07-29T18:00:13.0492877+00:00 | 104.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 118.1s / 980,550 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 103,690,000 | 2026-07-29T18:00:13.5340917+00:00 | 120.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 118.1s / 980,550 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 104,160,000 | 2026-07-29T18:00:14.0137528+00:00 | 106.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 119.1s / 911,923 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 107,937,000 | 2026-07-29T18:00:18.0522215+00:00 | 104.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 123.1s / 924,421 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 114,120,000 | 2026-07-29T18:00:24.5253106+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 129.1s / 970,282 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 118,602,000 | 2026-07-29T18:00:29.2058941+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 134.1s / 774,611 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 118,772,000 | 2026-07-29T18:00:29.4837752+00:00 | 157.6ms | broker/backlog (no scale or GC event) | - | - | 134.1s / 774,611 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 118,787,000 | 2026-07-29T18:00:29.5013552+00:00 | 135.0ms | broker/backlog (no scale or GC event) | - | - | 134.1s / 774,611 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 118,797,000 | 2026-07-29T18:00:29.5225291+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 134.1s / 774,611 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 132,627,000 | 2026-07-29T18:00:44.0330979+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 149.1s / 955,336 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 134,047,000 | 2026-07-29T18:00:45.5277002+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 150.1s / 808,156 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,892,000 | 2026-07-29T18:00:46.5260138+00:00 | 110.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 151.1s / 1,000,693 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 143,161,000 | 2026-07-29T18:00:55.0291631+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 160.1s / 930,745 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 143,181,000 | 2026-07-29T18:00:55.0488641+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 160.1s / 930,745 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 146,542,000 | 2026-07-29T18:00:58.548029+00:00 | 113.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 163.1s / 973,371 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 147,472,000 | 2026-07-29T18:00:59.5434866+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 164.1s / 943,764 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 161,318,000 | 2026-07-29T18:01:14.0256317+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 179.1s / 949,295 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 161,325,000 | 2026-07-29T18:01:14.0380264+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 179.1s / 949,295 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 161,331,000 | 2026-07-29T18:01:14.0474876+00:00 | 120.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 179.1s / 949,295 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 224,190,000 | 2026-07-29T18:02:18.5532233+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 243.1s / 1,033,893 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 331,427,000 | 2026-07-29T18:04:04.5341582+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 349.1s / 877,577 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 455,388,000 | 2026-07-29T18:06:10.5411026+00:00 | 206.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 475.2s / 823,725 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 455,392,000 | 2026-07-29T18:06:10.5450812+00:00 | 208.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 475.2s / 823,725 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 455,401,000 | 2026-07-29T18:06:10.5609113+00:00 | 211.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 475.2s / 823,725 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 455,406,000 | 2026-07-29T18:06:10.5770715+00:00 | 192.1ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 475.2s / 823,725 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 455,411,000 | 2026-07-29T18:06:10.5846492+00:00 | 189.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed | - | 475.2s / 823,725 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 462,971,000 | 2026-07-29T18:06:18.5299588+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 600,202 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 518,400,000 | 2026-07-29T18:07:15.0550761+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 539.2s / 943,968 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 531,492,000 | 2026-07-29T18:07:29.040292+00:00 | 113.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 553.2s / 844,563 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 533,177,000 | 2026-07-29T18:07:31.0491773+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 555.3s / 843,221 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 371,710,000 | 2026-07-29T18:23:54.084166+00:00 | 103.7ms | GC pause | - | - | 638.5s / 437,262 msg/s | Gen2 +0 / pause +170.9ms |
| Confluent | 371,711,000 | 2026-07-29T18:23:54.0928606+00:00 | 102.1ms | GC pause | - | - | 638.5s / 437,262 msg/s | Gen2 +0 / pause +170.9ms |
| Confluent | 443,137,000 | 2026-07-29T18:25:38.4284204+00:00 | 110.2ms | GC pause | - | - | 742.6s / 535,863 msg/s | Gen2 +0 / pause +138.2ms |
| Confluent | 443,138,000 | 2026-07-29T18:25:38.4292595+00:00 | 109.4ms | GC pause | - | - | 742.6s / 535,863 msg/s | Gen2 +0 / pause +138.2ms |
| Confluent | 443,141,000 | 2026-07-29T18:25:38.4314732+00:00 | 107.3ms | GC pause | - | - | 742.6s / 535,863 msg/s | Gen2 +0 / pause +138.2ms |
| Confluent | 443,144,000 | 2026-07-29T18:25:38.4392726+00:00 | 100.2ms | GC pause | - | - | 742.6s / 535,863 msg/s | Gen2 +0 / pause +138.2ms |
| Confluent | 443,849,000 | 2026-07-29T18:25:39.5615744+00:00 | 111.4ms | GC pause | - | - | 743.6s / 601,595 msg/s | Gen2 +0 / pause +167.9ms |
| Confluent | 443,850,000 | 2026-07-29T18:25:39.5621584+00:00 | 104.7ms | GC pause | - | - | 743.6s / 601,595 msg/s | Gen2 +0 / pause +167.9ms |
| Confluent | 443,853,000 | 2026-07-29T18:25:39.5733597+00:00 | 103.6ms | GC pause | - | - | 743.6s / 601,595 msg/s | Gen2 +0 / pause +167.9ms |
| Dekaf (3conn) | 38,000 | 2026-07-29T18:28:29.4407157+00:00 | 106.2ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 77,000 | 2026-07-29T18:28:29.5089273+00:00 | 183.0ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 97,000 | 2026-07-29T18:28:29.5822656+00:00 | 142.9ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 107,000 | 2026-07-29T18:28:29.6156873+00:00 | 123.7ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 147,000 | 2026-07-29T18:28:29.704576+00:00 | 123.7ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 197,000 | 2026-07-29T18:28:29.7967625+00:00 | 148.2ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 203,000 | 2026-07-29T18:28:29.8074003+00:00 | 138.0ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 212,000 | 2026-07-29T18:28:29.8241964+00:00 | 194.6ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 217,000 | 2026-07-29T18:28:29.8360969+00:00 | 152.7ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 222,000 | 2026-07-29T18:28:29.8423836+00:00 | 189.6ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 227,000 | 2026-07-29T18:28:29.8501276+00:00 | 168.5ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 230,000 | 2026-07-29T18:28:29.8542772+00:00 | 183.5ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 231,000 | 2026-07-29T18:28:29.8555095+00:00 | 180.2ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 234,000 | 2026-07-29T18:28:29.8643124+00:00 | 164.3ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 242,000 | 2026-07-29T18:28:29.9289925+00:00 | 131.5ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 244,000 | 2026-07-29T18:28:29.9311112+00:00 | 110.6ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 251,000 | 2026-07-29T18:28:29.9531848+00:00 | 125.2ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 252,000 | 2026-07-29T18:28:29.9542639+00:00 | 124.1ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 292,000 | 2026-07-29T18:28:30.0509175+00:00 | 103.8ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 300,000 | 2026-07-29T18:28:30.0646453+00:00 | 107.9ms | throughput collapse | - | - | 1.0s / 436,300 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 397,000 | 2026-07-29T18:28:30.2703813+00:00 | 156.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 457,000 | 2026-07-29T18:28:30.4054746+00:00 | 154.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 461,000 | 2026-07-29T18:28:30.4087138+00:00 | 130.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 477,000 | 2026-07-29T18:28:30.4562254+00:00 | 175.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 481,000 | 2026-07-29T18:28:30.4727445+00:00 | 124.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 493,000 | 2026-07-29T18:28:30.49274+00:00 | 117.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 502,000 | 2026-07-29T18:28:30.5085319+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 607,000 | 2026-07-29T18:28:30.6954587+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 770,000 | 2026-07-29T18:28:30.9032824+00:00 | 133.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 787,000 | 2026-07-29T18:28:30.922312+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 667,951 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,172,000 | 2026-07-29T18:28:31.4353697+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,192,000 | 2026-07-29T18:28:31.4594271+00:00 | 127.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,211,000 | 2026-07-29T18:28:31.4796757+00:00 | 136.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,222,000 | 2026-07-29T18:28:31.5153564+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,231,000 | 2026-07-29T18:28:31.5304347+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,232,000 | 2026-07-29T18:28:31.5355162+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,241,000 | 2026-07-29T18:28:31.5616917+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,520,000 | 2026-07-29T18:28:31.9164099+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 762,183 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,851,000 | 2026-07-29T18:28:32.340822+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 655,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,170,000 | 2026-07-29T18:28:32.8255007+00:00 | 118.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 655,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,190,000 | 2026-07-29T18:28:32.873971+00:00 | 142.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 655,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,200,000 | 2026-07-29T18:28:32.8906061+00:00 | 147.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 655,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,210,000 | 2026-07-29T18:28:32.9004528+00:00 | 153.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 655,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,352,000 | 2026-07-29T18:28:33.1261327+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 655,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,571,000 | 2026-07-29T18:28:33.4258796+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 903,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,572,000 | 2026-07-29T18:28:33.4262027+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 903,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,582,000 | 2026-07-29T18:28:33.43779+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 903,669 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,981,000 | 2026-07-29T18:28:34.9307164+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 881,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,982,000 | 2026-07-29T18:28:34.9314717+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 881,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,252,000 | 2026-07-29T18:28:35.2713823+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 978,321 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,892,000 | 2026-07-29T18:28:35.91895+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 978,321 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,911,000 | 2026-07-29T18:28:35.9404206+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 978,321 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,912,000 | 2026-07-29T18:28:35.9410379+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 978,321 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,381,000 | 2026-07-29T18:28:36.4551492+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 880,222 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,602,000 | 2026-07-29T18:28:37.8535766+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 862,773 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,612,000 | 2026-07-29T18:28:37.8649778+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 862,773 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,135,000 | 2026-07-29T18:28:38.4676689+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 906,671 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,327,000 | 2026-07-29T18:28:40.9458103+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 946,313 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,337,000 | 2026-07-29T18:28:40.9577021+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 946,313 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,804,000 | 2026-07-29T18:28:41.4531314+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 849,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,806,000 | 2026-07-29T18:28:41.4579293+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 849,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,810,000 | 2026-07-29T18:28:41.4615043+00:00 | 124.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 849,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,161,000 | 2026-07-29T18:28:41.9405873+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 849,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,172,000 | 2026-07-29T18:28:41.9485851+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 849,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,202,000 | 2026-07-29T18:28:41.9750679+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 849,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,081,000 | 2026-07-29T18:28:42.9255566+00:00 | 104.9ms | GC pause | - | - | 14.0s / 860,952 msg/s | Gen2 +1 / pause +4.2ms |
| Dekaf (3conn) | 11,082,000 | 2026-07-29T18:28:42.9266809+00:00 | 103.8ms | GC pause | - | - | 14.0s / 860,952 msg/s | Gen2 +1 / pause +4.2ms |
| Dekaf (3conn) | 11,452,000 | 2026-07-29T18:28:43.3994359+00:00 | 128.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,461,000 | 2026-07-29T18:28:43.4075193+00:00 | 145.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,472,000 | 2026-07-29T18:28:43.4220217+00:00 | 137.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,490,000 | 2026-07-29T18:28:43.4442585+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,502,000 | 2026-07-29T18:28:43.4634547+00:00 | 122.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,511,000 | 2026-07-29T18:28:43.4851513+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,731,000 | 2026-07-29T18:28:43.7474889+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,751,000 | 2026-07-29T18:28:43.7757878+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,752,000 | 2026-07-29T18:28:43.777577+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,917,000 | 2026-07-29T18:28:43.9802841+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,111,000 | 2026-07-29T18:28:44.2274348+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,122,000 | 2026-07-29T18:28:44.2349429+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,132,000 | 2026-07-29T18:28:44.2510542+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 774,733 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,141,000 | 2026-07-29T18:28:44.2623116+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,172,000 | 2026-07-29T18:28:44.3101905+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,222,000 | 2026-07-29T18:28:44.3949865+00:00 | 126.0ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,231,000 | 2026-07-29T18:28:44.4077017+00:00 | 125.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,232,000 | 2026-07-29T18:28:44.4079772+00:00 | 125.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,242,000 | 2026-07-29T18:28:44.4183764+00:00 | 138.2ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,262,000 | 2026-07-29T18:28:44.4524216+00:00 | 133.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,271,000 | 2026-07-29T18:28:44.4687267+00:00 | 130.7ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,302,000 | 2026-07-29T18:28:44.5403691+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,312,000 | 2026-07-29T18:28:44.5512734+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,591,000 | 2026-07-29T18:28:44.9306425+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 12,601,000 | 2026-07-29T18:28:44.9394638+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 758,708 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,217,000 | 2026-07-29T18:28:48.446292+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,218,000 | 2026-07-29T18:28:48.4484129+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,225,000 | 2026-07-29T18:28:48.4551245+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,227,000 | 2026-07-29T18:28:48.456535+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,228,000 | 2026-07-29T18:28:48.456827+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,247,000 | 2026-07-29T18:28:48.4715558+00:00 | 121.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,249,000 | 2026-07-29T18:28:48.4725027+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,257,000 | 2026-07-29T18:28:48.5315494+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 918,891 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,057,000 | 2026-07-29T18:28:50.4350708+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 919,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,087,000 | 2026-07-29T18:28:50.4685673+00:00 | 127.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 919,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,090,000 | 2026-07-29T18:28:50.46963+00:00 | 116.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 919,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,091,000 | 2026-07-29T18:28:50.4701159+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 919,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,095,000 | 2026-07-29T18:28:50.4795289+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 919,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,098,000 | 2026-07-29T18:28:50.482126+00:00 | 117.5ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 919,957 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,167,000 | 2026-07-29T18:28:52.4758499+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 1,054,702 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,231,000 | 2026-07-29T18:28:53.4764835+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,121,074 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 27,701,000 | 2026-07-29T18:28:58.9735015+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,081,416 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 55,002,000 | 2026-07-29T18:29:24.9254001+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 966,035 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 63,381,000 | 2026-07-29T18:29:32.4837585+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 64.0s / 1,011,238 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 64,920,000 | 2026-07-29T18:29:33.9581757+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 65.0s / 999,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 64,942,000 | 2026-07-29T18:29:33.9966934+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 65.0s / 999,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,330,000 | 2026-07-29T18:29:39.8855864+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 894,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,350,000 | 2026-07-29T18:29:39.9189882+00:00 | 134.5ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 894,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,360,000 | 2026-07-29T18:29:39.9349512+00:00 | 127.1ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 894,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,390,000 | 2026-07-29T18:29:39.9707569+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 894,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,530,000 | 2026-07-29T18:29:40.161306+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 71.0s / 894,754 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 71,690,000 | 2026-07-29T18:29:40.3679878+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 72.0s / 1,162,162 msg/s | Gen2 +0 / pause +2.9ms |
| Dekaf (3conn) | 71,700,000 | 2026-07-29T18:29:40.3814257+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 72.0s / 1,162,162 msg/s | Gen2 +0 / pause +2.9ms |
| Dekaf (3conn) | 83,387,000 | 2026-07-29T18:29:51.4882542+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 83.1s / 1,057,442 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 83,397,000 | 2026-07-29T18:29:51.4952874+00:00 | 107.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 83.1s / 1,057,442 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 83,897,000 | 2026-07-29T18:29:51.9529823+00:00 | 142.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 83.1s / 1,057,442 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 83,917,000 | 2026-07-29T18:29:51.9728255+00:00 | 149.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 83.1s / 1,057,442 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 86,517,000 | 2026-07-29T18:29:54.4506521+00:00 | 101.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 86.1s / 1,024,884 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,627,000 | 2026-07-29T18:29:56.4334271+00:00 | 132.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,631,000 | 2026-07-29T18:29:56.4349095+00:00 | 103.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,634,000 | 2026-07-29T18:29:56.4401645+00:00 | 125.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,636,000 | 2026-07-29T18:29:56.4410375+00:00 | 124.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,637,000 | 2026-07-29T18:29:56.4412787+00:00 | 146.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,639,000 | 2026-07-29T18:29:56.4454412+00:00 | 115.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,641,000 | 2026-07-29T18:29:56.4463343+00:00 | 115.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,642,000 | 2026-07-29T18:29:56.448203+00:00 | 113.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,643,000 | 2026-07-29T18:29:56.4488045+00:00 | 112.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,645,000 | 2026-07-29T18:29:56.4508859+00:00 | 121.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,647,000 | 2026-07-29T18:29:56.4534878+00:00 | 136.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,654,000 | 2026-07-29T18:29:56.456775+00:00 | 108.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,655,000 | 2026-07-29T18:29:56.4572669+00:00 | 131.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,656,000 | 2026-07-29T18:29:56.4575276+00:00 | 107.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,657,000 | 2026-07-29T18:29:56.4592907+00:00 | 140.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,659,000 | 2026-07-29T18:29:56.4617805+00:00 | 110.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,662,000 | 2026-07-29T18:29:56.4701983+00:00 | 112.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,665,000 | 2026-07-29T18:29:56.4778356+00:00 | 121.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,668,000 | 2026-07-29T18:29:56.4830383+00:00 | 116.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 88.1s / 988,985 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,199,000 | 2026-07-29T18:29:57.9393984+00:00 | 131.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 90,200,000 | 2026-07-29T18:29:57.9399619+00:00 | 155.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 90,205,000 | 2026-07-29T18:29:57.9514378+00:00 | 110.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 90,207,000 | 2026-07-29T18:29:57.9553311+00:00 | 111.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 90,210,000 | 2026-07-29T18:29:57.9570751+00:00 | 145.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 90,215,000 | 2026-07-29T18:29:57.9679337+00:00 | 100.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 90,216,000 | 2026-07-29T18:29:57.9689125+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 89.1s / 1,057,531 msg/s | Gen2 +0 / pause +7.5ms |
| Dekaf (3conn) | 94,080,000 | 2026-07-29T18:30:01.4272179+00:00 | 145.3ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,097,639 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 94,100,000 | 2026-07-29T18:30:01.456953+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,097,639 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 94,101,000 | 2026-07-29T18:30:01.4572029+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,097,639 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 94,103,000 | 2026-07-29T18:30:01.4641392+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,097,639 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 94,110,000 | 2026-07-29T18:30:01.4814666+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,097,639 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 100,467,000 | 2026-07-29T18:30:06.9449713+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 98.1s / 1,258,028 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 105,882,000 | 2026-07-29T18:30:11.4574582+00:00 | 132.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 103.1s / 1,141,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 105,887,000 | 2026-07-29T18:30:11.4677609+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 103.1s / 1,141,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 105,889,000 | 2026-07-29T18:30:11.4713132+00:00 | 118.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 103.1s / 1,141,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 105,893,000 | 2026-07-29T18:30:11.4744309+00:00 | 115.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 103.1s / 1,141,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 105,900,000 | 2026-07-29T18:30:11.4811114+00:00 | 111.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 103.1s / 1,141,056 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 111,580,000 | 2026-07-29T18:30:16.4706078+00:00 | 115.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 108.1s / 899,675 msg/s | Gen2 +0 / pause +2.2ms |
| Dekaf (3conn) | 111,590,000 | 2026-07-29T18:30:16.4900148+00:00 | 102.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed | - | 108.1s / 899,675 msg/s | Gen2 +0 / pause +2.2ms |
| Dekaf (3conn) | 158,582,000 | 2026-07-29T18:30:55.964445+00:00 | 113.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 158,592,000 | 2026-07-29T18:30:55.9692969+00:00 | 123.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 158,593,000 | 2026-07-29T18:30:55.969628+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 158,602,000 | 2026-07-29T18:30:55.9748002+00:00 | 122.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 158,603,000 | 2026-07-29T18:30:55.9750555+00:00 | 115.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 158,604,000 | 2026-07-29T18:30:55.9756616+00:00 | 114.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 158,610,000 | 2026-07-29T18:30:55.9836918+00:00 | 107.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 147.1s / 1,126,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 173,487,000 | 2026-07-29T18:31:09.4538327+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 161.1s / 1,097,919 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 192,371,000 | 2026-07-29T18:31:25.9391242+00:00 | 121.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 177.1s / 984,035 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 192,373,000 | 2026-07-29T18:31:25.9438709+00:00 | 115.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 177.1s / 984,035 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 192,377,000 | 2026-07-29T18:31:25.9500286+00:00 | 115.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 177.1s / 984,035 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 192,379,000 | 2026-07-29T18:31:25.9568555+00:00 | 116.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 177.1s / 984,035 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 192,382,000 | 2026-07-29T18:31:25.9626853+00:00 | 114.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 177.1s / 984,035 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 192,392,000 | 2026-07-29T18:31:25.9775629+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 177.1s / 984,035 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 194,107,000 | 2026-07-29T18:31:27.4609378+00:00 | 137.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 179.1s / 1,087,441 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 194,115,000 | 2026-07-29T18:31:27.4702866+00:00 | 125.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 179.1s / 1,087,441 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 205,391,000 | 2026-07-29T18:31:37.9473999+00:00 | 108.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 189.1s / 1,118,065 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 208,701,000 | 2026-07-29T18:31:40.9430169+00:00 | 107.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 192.1s / 1,059,269 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 208,720,000 | 2026-07-29T18:31:40.9740355+00:00 | 125.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 192.1s / 1,059,269 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 208,721,000 | 2026-07-29T18:31:40.9781326+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 192.1s / 1,059,269 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 208,730,000 | 2026-07-29T18:31:41.0134586+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 192.1s / 1,059,269 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 211,441,000 | 2026-07-29T18:31:43.4286967+00:00 | 120.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 195.1s / 1,052,637 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 211,442,000 | 2026-07-29T18:31:43.429275+00:00 | 119.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 195.1s / 1,052,637 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 211,453,000 | 2026-07-29T18:31:43.4474712+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 195.1s / 1,052,637 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 224,447,000 | 2026-07-29T18:31:55.3337806+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 206.1s / 1,003,068 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 224,514,000 | 2026-07-29T18:31:55.452264+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 207.1s / 1,028,577 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 224,530,000 | 2026-07-29T18:31:55.4647892+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 207.1s / 1,028,577 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 224,550,000 | 2026-07-29T18:31:55.497159+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 207.1s / 1,028,577 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 240,824,000 | 2026-07-29T18:32:09.4793492+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 221.1s / 1,015,472 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf (3conn) | 240,826,000 | 2026-07-29T18:32:09.4800601+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 221.1s / 1,015,472 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf (3conn) | 250,802,000 | 2026-07-29T18:32:17.9369815+00:00 | 111.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 229.1s / 1,046,079 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 250,811,000 | 2026-07-29T18:32:17.9537292+00:00 | 111.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 229.1s / 1,046,079 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 275,431,000 | 2026-07-29T18:32:38.476798+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 250.1s / 1,138,434 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 279,490,000 | 2026-07-29T18:32:41.9625759+00:00 | 101.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed | - | 253.1s / 939,457 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 281,100,000 | 2026-07-29T18:32:43.9280962+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed | - | 255.1s / 1,028,901 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 281,111,000 | 2026-07-29T18:32:43.9364471+00:00 | 114.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed | - | 255.1s / 1,028,901 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 281,122,000 | 2026-07-29T18:32:43.9556686+00:00 | 107.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed | - | 255.1s / 1,028,901 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 283,740,000 | 2026-07-29T18:32:46.4534286+00:00 | 127.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed | - | 258.1s / 1,165,818 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 287,174,000 | 2026-07-29T18:32:49.4497682+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/failed | - | 261.1s / 1,130,528 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 305,999,000 | 2026-07-29T18:33:05.4662849+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 277.2s / 1,068,288 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 306,005,000 | 2026-07-29T18:33:05.4687857+00:00 | 103.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 277.2s / 1,068,288 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 332,688,000 | 2026-07-29T18:33:27.9535261+00:00 | 115.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 299.2s / 884,983 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 332,708,000 | 2026-07-29T18:33:27.9723566+00:00 | 115.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 299.2s / 884,983 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 332,717,000 | 2026-07-29T18:33:27.9900173+00:00 | 120.9ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 299.2s / 884,983 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,544,000 | 2026-07-29T18:33:31.4368383+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,548,000 | 2026-07-29T18:33:31.4399844+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,554,000 | 2026-07-29T18:33:31.4461689+00:00 | 131.5ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,557,000 | 2026-07-29T18:33:31.4470667+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,559,000 | 2026-07-29T18:33:31.4538406+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,566,000 | 2026-07-29T18:33:31.4650078+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,572,000 | 2026-07-29T18:33:31.4719882+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 336,575,000 | 2026-07-29T18:33:31.475611+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 303.2s / 1,098,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 367,546,000 | 2026-07-29T18:33:57.4613757+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 329.2s / 1,081,645 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 397,487,000 | 2026-07-29T18:34:23.9592851+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 355.2s / 1,149,402 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 439,647,000 | 2026-07-29T18:35:01.4616652+00:00 | 104.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 392.2s / 1,103,858 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 439,657,000 | 2026-07-29T18:35:01.4717098+00:00 | 114.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 393.2s / 1,139,642 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 490,697,000 | 2026-07-29T18:35:48.4630626+00:00 | 109.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 439.2s / 883,403 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 491,505,000 | 2026-07-29T18:35:49.4598814+00:00 | 100.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 440.2s / 830,813 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 508,890,000 | 2026-07-29T18:36:07.4264333+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 458.2s / 794,095 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 509,836,000 | 2026-07-29T18:36:08.4642928+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 459.2s / 901,214 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 514,567,000 | 2026-07-29T18:36:13.4293268+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 464.2s / 976,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 514,577,000 | 2026-07-29T18:36:13.4374539+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 464.2s / 976,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 514,597,000 | 2026-07-29T18:36:13.4612601+00:00 | 129.5ms | broker/backlog (no scale or GC event) | - | - | 464.2s / 976,827 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 542,839,000 | 2026-07-29T18:36:41.9832509+00:00 | 129.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 2:capacity/failed | - | 493.3s / 827,009 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 571,633,000 | 2026-07-29T18:37:12.990425+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 524.3s / 852,006 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 572,410,000 | 2026-07-29T18:37:13.957815+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 525.3s / 771,815 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 630,525,000 | 2026-07-29T18:38:30.9692515+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 602.3s / 802,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 630,528,000 | 2026-07-29T18:38:30.9712105+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 602.3s / 802,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 653,077,000 | 2026-07-29T18:38:55.937229+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 627.4s / 1,030,974 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 653,087,000 | 2026-07-29T18:38:55.94692+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 627.4s / 1,030,974 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 653,088,000 | 2026-07-29T18:38:55.9471498+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 627.4s / 1,030,974 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 653,097,000 | 2026-07-29T18:38:55.9679172+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 627.4s / 1,030,974 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 653,098,000 | 2026-07-29T18:38:55.968176+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 627.4s / 1,030,974 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 668,006,000 | 2026-07-29T18:39:12.9662499+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 644.4s / 770,908 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 682,746,000 | 2026-07-29T18:39:32.4806746+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 663.4s / 1,002,927 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 717,017,000 | 2026-07-29T18:40:05.4375021+00:00 | 127.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 696.4s / 858,473 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 717,457,000 | 2026-07-29T18:40:05.9408321+00:00 | 106.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 697.4s / 848,262 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 717,477,000 | 2026-07-29T18:40:05.9616958+00:00 | 111.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 697.4s / 848,262 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 717,497,000 | 2026-07-29T18:40:05.9817755+00:00 | 116.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 697.4s / 848,262 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 727,069,000 | 2026-07-29T18:40:17.5484286+00:00 | 101.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 708.4s / 731,813 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 727,072,000 | 2026-07-29T18:40:17.5517872+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 708.4s / 731,813 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 727,080,000 | 2026-07-29T18:40:17.5614891+00:00 | 100.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed | - | 708.4s / 731,813 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 738,763,000 | 2026-07-29T18:40:32.4618218+00:00 | 140.9ms | broker/backlog (no scale or GC event) | - | - | 723.4s / 684,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,707,000 | 2026-07-29T18:40:37.9173469+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 729.4s / 1,044,727 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,717,000 | 2026-07-29T18:40:37.9236094+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 729.4s / 1,044,727 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,727,000 | 2026-07-29T18:40:37.9306317+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 729.4s / 1,044,727 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,747,000 | 2026-07-29T18:40:37.9583588+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 729.4s / 1,044,727 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,760,000 | 2026-07-29T18:40:37.981911+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 729.4s / 1,044,727 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 750,600,000 | 2026-07-29T18:40:42.9429116+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 817,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 764,367,000 | 2026-07-29T18:40:55.9239776+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 747.4s / 999,813 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 900,277,000 | 2026-07-29T18:43:02.9417012+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 874.5s / 1,047,378 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 900,297,000 | 2026-07-29T18:43:02.9588769+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 874.5s / 1,047,378 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*1,969 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 2.02x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.64x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.93 | 2994.82 | 1,306,335 | 2,195,288 | +26.0% | +290.96% | 159.46 | 1,306,335 | 0 | 1.21 |
| Confluent | 1.60 | - | 127,721 | 1,966,005 | -1.8% | +14.98% | 15.59 | 127,721 | 0 | 0.20 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 6,142 | 664.91 | 613.69 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:03.067603+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 997,347 msg/s |
| Dekaf | 2026-07-29T17:58:04.0713092+00:00 | 1 | 16.0 MiB / 1.9 MiB | 106.6 MB/s | 0/0 | 0 | 1.0s / 997,347 msg/s |
| Dekaf | 2026-07-29T17:58:05.0716293+00:00 | 1 | 16.0 MiB / 4.7 MiB | 323.4 MB/s | 0/0 | 0 | 2.0s / 2,133,446 msg/s |
| Dekaf | 2026-07-29T17:58:06.0732517+00:00 | 1 | 16.0 MiB / 3.6 MiB | 493.8 MB/s | 0/0 | 0 | 3.0s / 2,362,982 msg/s |
| Dekaf | 2026-07-29T17:58:07.0731761+00:00 | 1 | 16.0 MiB / 2.9 MiB | 493.8 MB/s | 0/0 | 0 | 4.0s / 2,120,219 msg/s |
| Dekaf | 2026-07-29T17:58:08.0745401+00:00 | 1 | 16.0 MiB / 1.9 MiB | 493.8 MB/s | 0/0 | 0 | 5.0s / 2,388,208 msg/s |
| Dekaf | 2026-07-29T17:58:09.0791454+00:00 | 1 | 16.0 MiB / 3.2 MiB | 493.8 MB/s | 0/0 | 0 | 6.0s / 2,195,288 msg/s |
| Dekaf | 2026-07-29T17:58:10.0786995+00:00 | 1 | 16.0 MiB / 1.5 MiB | 493.8 MB/s | 0/0 | 0 | 7.0s / 2,532,124 msg/s |
| Dekaf | 2026-07-29T17:58:11.0815296+00:00 | 1 | 16.0 MiB / 2.4 MiB | 528.4 MB/s | 0/0 | 0 | 8.0s / 2,249,470 msg/s |
| Dekaf | 2026-07-29T17:58:12.0820782+00:00 | 1 | 16.0 MiB / 1.9 MiB | 528.4 MB/s | 0/0 | 0 | 9.0s / 2,139,508 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.73x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.12x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 422.69 | 422.68 | 256 | 343 | +3.9% | +0.44% | 0.24 | 341 | 0 | 0.14 |
| Confluent | 288.86 | - | 124 | 167 | +2.9% | +0.35% | 0.12 | 165 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 101,868 | 113.15 | 1.16 KB |
| Dekaf | 2 | 102,413 | 113.75 | 1.16 KB |
| Dekaf | 3 | 102,725 | 114.10 | 1.16 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-29T17:58:15.2572252+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 154 msg/s |
| Dekaf | 2026-07-29T17:58:24.265207+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 9.0s / 271 msg/s |
| Dekaf | 2026-07-29T17:58:33.269897+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 18.0s / 269 msg/s |
| Dekaf | 2026-07-29T17:58:43.2737415+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 28.0s / 305 msg/s |
| Dekaf | 2026-07-29T17:58:52.2781347+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 37.0s / 313 msg/s |
| Dekaf | 2026-07-29T17:59:01.2872001+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 46.0s / 331 msg/s |
| Dekaf | 2026-07-29T17:59:10.2970819+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 55.0s / 339 msg/s |
| Dekaf | 2026-07-29T17:59:19.3185765+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 64.0s / 320 msg/s |
| Dekaf | 2026-07-29T17:59:28.3220386+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 73.0s / 345 msg/s |
| Dekaf | 2026-07-29T17:59:37.3223588+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 82.0s / 343 msg/s |
| Dekaf | 2026-07-29T17:59:46.3459263+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 348 msg/s |
| Dekaf | 2026-07-29T17:59:55.3814594+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 100.0s / 351 msg/s |
| Dekaf | 2026-07-29T18:00:04.3870106+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 109.0s / 299 msg/s |
| Dekaf | 2026-07-29T18:00:13.4063055+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 118.0s / 297 msg/s |
| Dekaf | 2026-07-29T18:00:22.4145903+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 354 msg/s |
| Dekaf | 2026-07-29T18:00:32.4280061+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 137.0s / 362 msg/s |
| Dekaf | 2026-07-29T18:00:41.4426132+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 146.0s / 362 msg/s |
| Dekaf | 2026-07-29T18:00:50.4671718+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 363 msg/s |
| Dekaf | 2026-07-29T18:00:59.4867947+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 351 msg/s |
| Dekaf | 2026-07-29T18:01:08.5014557+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 354 msg/s |
| Dekaf | 2026-07-29T18:01:17.506422+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 352 msg/s |
| Dekaf | 2026-07-29T18:01:26.5118579+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 369 msg/s |
| Dekaf | 2026-07-29T18:01:35.5180365+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 373 msg/s |
| Dekaf | 2026-07-29T18:01:44.5468886+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 363 msg/s |
| Dekaf | 2026-07-29T18:01:53.5671959+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 355 msg/s |
| Dekaf | 2026-07-29T18:02:02.5926138+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 347 msg/s |
| Dekaf | 2026-07-29T18:02:11.5977156+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 356 msg/s |
| Dekaf | 2026-07-29T18:02:21.6195784+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 246.0s / 339 msg/s |
| Dekaf | 2026-07-29T18:02:30.6240646+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 255.0s / 324 msg/s |
| Dekaf | 2026-07-29T18:02:39.6377113+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 264.0s / 332 msg/s |
| Dekaf | 2026-07-29T18:02:48.6586427+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 273.0s / 312 msg/s |
| Dekaf | 2026-07-29T18:02:57.6824305+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 315 msg/s |
| Dekaf | 2026-07-29T18:03:06.6999631+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 291.0s / 326 msg/s |
| Dekaf | 2026-07-29T18:03:15.7235619+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 300.0s / 332 msg/s |
| Dekaf | 2026-07-29T18:03:24.746718+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 309.0s / 331 msg/s |
| Dekaf | 2026-07-29T18:03:33.7636043+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 318.1s / 338 msg/s |
| Dekaf | 2026-07-29T18:03:42.7712488+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 327.1s / 340 msg/s |
| Dekaf | 2026-07-29T18:03:51.7738934+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.1s / 335 msg/s |
| Dekaf | 2026-07-29T18:04:00.8130009+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 345.1s / 328 msg/s |
| Dekaf | 2026-07-29T18:04:09.8176713+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 337 msg/s |
| Dekaf | 2026-07-29T18:04:19.8293067+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 364.1s / 349 msg/s |
| Dekaf | 2026-07-29T18:04:28.845949+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 373.1s / 354 msg/s |
| Dekaf | 2026-07-29T18:04:37.8720346+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 382.1s / 351 msg/s |
| Dekaf | 2026-07-29T18:04:46.8921858+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 392.1s / 337 msg/s |
| Dekaf | 2026-07-29T18:04:55.9068412+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 401.1s / 358 msg/s |
| Dekaf | 2026-07-29T18:05:04.9314851+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 410.1s / 329 msg/s |
| Dekaf | 2026-07-29T18:05:13.9559554+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 419.1s / 327 msg/s |
| Dekaf | 2026-07-29T18:05:22.9780843+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 428.1s / 333 msg/s |
| Dekaf | 2026-07-29T18:05:31.9913788+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 437.1s / 340 msg/s |
| Dekaf | 2026-07-29T18:05:40.9987325+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 446.1s / 336 msg/s |
| Dekaf | 2026-07-29T18:05:50.017162+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 455.1s / 330 msg/s |
| Dekaf | 2026-07-29T18:05:59.0271329+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 464.1s / 336 msg/s |
| Dekaf | 2026-07-29T18:06:09.0519464+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 474.1s / 342 msg/s |
| Dekaf | 2026-07-29T18:06:18.0574489+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 483.1s / 337 msg/s |
| Dekaf | 2026-07-29T18:06:27.0628426+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 492.1s / 341 msg/s |
| Dekaf | 2026-07-29T18:06:36.0673492+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 501.1s / 343 msg/s |
| Dekaf | 2026-07-29T18:06:45.0911202+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 510.1s / 348 msg/s |
| Dekaf | 2026-07-29T18:06:54.0943004+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 519.1s / 330 msg/s |
| Dekaf | 2026-07-29T18:07:03.0971107+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 528.1s / 335 msg/s |
| Dekaf | 2026-07-29T18:07:12.1120052+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 537.1s / 333 msg/s |
| Dekaf | 2026-07-29T18:07:21.1169925+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 546.1s / 331 msg/s |
| Dekaf | 2026-07-29T18:07:30.1226544+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 555.1s / 346 msg/s |
| Dekaf | 2026-07-29T18:07:39.1292855+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 564.1s / 340 msg/s |
| Dekaf | 2026-07-29T18:07:48.1379457+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 573.1s / 341 msg/s |
| Dekaf | 2026-07-29T18:07:58.1498289+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 583.1s / 330 msg/s |
| Dekaf | 2026-07-29T18:08:07.1552845+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 592.1s / 337 msg/s |
| Dekaf | 2026-07-29T18:08:16.162352+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 601.1s / 328 msg/s |
| Dekaf | 2026-07-29T18:08:25.1836962+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 610.1s / 348 msg/s |
| Dekaf | 2026-07-29T18:08:34.2008312+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 619.1s / 342 msg/s |
| Dekaf | 2026-07-29T18:08:43.2071361+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 628.1s / 320 msg/s |
| Dekaf | 2026-07-29T18:08:52.2218288+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 637.1s / 345 msg/s |
| Dekaf | 2026-07-29T18:09:01.2253474+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 646.1s / 345 msg/s |
| Dekaf | 2026-07-29T18:09:10.2330117+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 655.1s / 354 msg/s |
| Dekaf | 2026-07-29T18:09:19.249264+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 664.1s / 349 msg/s |
| Dekaf | 2026-07-29T18:09:28.2514561+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 353 msg/s |
| Dekaf | 2026-07-29T18:09:37.2554278+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 352 msg/s |
| Dekaf | 2026-07-29T18:09:46.2661152+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 359 msg/s |
| Dekaf | 2026-07-29T18:09:56.270277+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 701.1s / 351 msg/s |
| Dekaf | 2026-07-29T18:10:05.281523+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 710.1s / 361 msg/s |
| Dekaf | 2026-07-29T18:10:14.3001504+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 719.1s / 366 msg/s |
| Dekaf | 2026-07-29T18:10:23.3046716+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 728.1s / 366 msg/s |
| Dekaf | 2026-07-29T18:10:32.3179965+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 737.1s / 352 msg/s |
| Dekaf | 2026-07-29T18:10:41.3290935+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 746.1s / 343 msg/s |
| Dekaf | 2026-07-29T18:10:50.3396814+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 755.1s / 352 msg/s |
| Dekaf | 2026-07-29T18:10:59.3458644+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 764.1s / 358 msg/s |
| Dekaf | 2026-07-29T18:11:08.3566734+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 773.1s / 348 msg/s |
| Dekaf | 2026-07-29T18:11:17.3807855+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 782.1s / 350 msg/s |
| Dekaf | 2026-07-29T18:11:26.3865396+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 791.1s / 361 msg/s |
| Dekaf | 2026-07-29T18:11:35.4086976+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 350 msg/s |
| Dekaf | 2026-07-29T18:11:45.4292915+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 810.1s / 358 msg/s |
| Dekaf | 2026-07-29T18:11:54.4455025+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 819.1s / 361 msg/s |
| Dekaf | 2026-07-29T18:12:03.4722463+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 828.1s / 374 msg/s |
| Dekaf | 2026-07-29T18:12:12.4790693+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 837.1s / 361 msg/s |
| Dekaf | 2026-07-29T18:12:21.5026127+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 846.1s / 355 msg/s |
| Dekaf | 2026-07-29T18:12:30.5238457+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 855.1s / 334 msg/s |
| Dekaf | 2026-07-29T18:12:39.5339321+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 864.1s / 340 msg/s |
| Dekaf | 2026-07-29T18:12:48.5363139+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 873.1s / 344 msg/s |
| Dekaf | 2026-07-29T18:12:57.5394443+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 882.1s / 342 msg/s |
| Dekaf | 2026-07-29T18:13:06.542387+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 891.1s / 330 msg/s |
| Dekaf | 2026-07-29T18:13:15.54797+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 899.1s / 323 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 148,900 | 111,700 | 37,200 | 111,700 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 307,000 | 230,300 | 76,700 | 230,300 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.46x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.06x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.87 | - | 1,554,155 | 1,539,240 | -8.9% | -0.69% | 1482.16 | - | 0 | 1.35 |
| Confluent | 1.23 | - | 1,100,156 | 1,153,892 | +5.3% | +0.47% | 1049.19 | - | 0 | 1.35 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.41x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.33x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.79 | - | 1,743,054 | 1,731,961 | +4.9% | +0.42% | 1662.31 | - | 0 | 1.37 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.46 | - | 3,440,035 | 3,455,202 | -3.1% | -0.27% | 3280.67 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.42 | - | 3,640,013 | 3,701,971 | +8.6% | +0.44% | 3471.39 | - | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 58673 | 357 | 0 | 2250.04 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 231223 | 3 | 1 | 1152.87 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 223796 | 1 | 1 | 1229.33 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 146809 | 1 | 1 | 704.90 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 179766 | 1 | 1 | 1138.21 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 237738 | 3 | 1 | 1163.72 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 163894 | 1 | 1 | 791.20 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 322705 | 1 | 1 | 1557.19 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 312344 | 2 | 1 | 1500.07 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 137817 | 1 | 1 | 659.48 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 6115 | 1 | 1 | 14.36 GB | 779 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 80 | 1 | 1 | 177.73 MB | 1.22 KB |
| Dekaf | Consumer | 69302 | 10 | 1 | 2637.09 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 26002 | 3 | 2 | 2957.89 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 0 | 448.16 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 8 | 2 | 1 | 912.26 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 376 | 2 | 2 | 1.37 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 392 | 2 | 2 | 148.45 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 170 | 2 | 1 | 840.64 MB | 1 B |
| Dekaf | Producer (Acks All) | 336 | 2 | 2 | 135.73 MB | 0 B |
| Dekaf | Producer (Acks All) | 372 | 2 | 2 | 1.36 GB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 186 | 3 | 1 | 870.26 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 412 | 2 | 2 | 160.18 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 410 | 2 | 2 | 1.57 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 172 | 2 | 1 | 854.44 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 599 | 3 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 21 | 0 | 0 | 158.06 MB | 540 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 274 | 2 | 2 | 1.01 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 187 | 3 | 1 | 753.13 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 349 | 2 | 2 | 1.34 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 219 | 3 | 2 | 908.48 MB | 1 B |

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
