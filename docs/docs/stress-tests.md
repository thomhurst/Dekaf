---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-26 04:21 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,560,683 | 1,555,801–1,565,579 | 0.93 | 1.10x |
| Confluent | 2 | 1,421,503 | 1,384,403–1,459,597 | 1.26 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.93 | 957.84 | 1,548,657 | 1,565,579 | -6.7% | -0.61% | 1476.91 | 1,548,657 | 0 | 1.44 |
| Dekaf (confluent-first) | 0.92 | 942.57 | 1,543,399 | 1,555,801 | +2.0% | +0.11% | 1471.90 | 1,543,399 | 0 | 1.42 |
| Confluent (confluent-first) | 1.22 | - | 1,435,807 | 1,459,597 | +0.4% | -0.02% | 1369.29 | 1,435,807 | 0 | 1.75 |
| Dekaf (3conn) | 0.80 | 727.16 | 1,442,524 | 1,449,396 | +3.8% | +0.29% | 1375.70 | 1,442,524 | 0 | 1.15 |
| Confluent (dekaf-first) | 1.30 | - | 1,362,796 | 1,384,403 | +2.7% | +0.23% | 1299.66 | 1,362,796 | 0 | 1.77 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,358,772 | 1509.73 | 1016.22 KB |
| Dekaf | 1 | 1,357,481 | 1508.29 | 1020.66 KB |
| Dekaf (3conn) | 1 | 1,427,581 | 1586.19 | 904.02 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:11.1322137+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 441,371 msg/s |
| Dekaf | 2026-07-26T03:21:38.1451951+00:00 | 1 | 16.0 MiB / 15.0 MiB | 1634.5 MB/s | 0/0 | 31,873 | 27.0s / 1,573,071 msg/s |
| Dekaf | 2026-07-26T03:22:06.1568246+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1635.7 MB/s | 1/0 | 81,890 | 55.0s / 1,538,210 msg/s |
| Dekaf | 2026-07-26T03:22:33.1677839+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1700.5 MB/s | 1/0 | 138,949 | 82.0s / 1,541,924 msg/s |
| Dekaf | 2026-07-26T03:23:00.1753858+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1700.5 MB/s | 2/0 | 199,614 | 109.0s / 1,516,938 msg/s |
| Dekaf | 2026-07-26T03:23:27.1791096+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1700.5 MB/s | 2/1 | 251,153 | 136.1s / 1,529,381 msg/s |
| Dekaf | 2026-07-26T03:23:55.1885602+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1700.5 MB/s | 2/1 | 318,876 | 164.1s / 1,525,665 msg/s |
| Dekaf | 2026-07-26T03:24:22.1965874+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1700.5 MB/s | 2/1 | 382,210 | 191.1s / 1,569,596 msg/s |
| Dekaf | 2026-07-26T03:24:49.2072856+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1700.5 MB/s | 2/2 | 447,139 | 218.1s / 1,497,314 msg/s |
| Dekaf | 2026-07-26T03:25:16.2181223+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1723.6 MB/s | 2/2 | 520,649 | 245.1s / 1,550,966 msg/s |
| Dekaf | 2026-07-26T03:25:44.2259177+00:00 | 1 | 12.0 MiB / 10.1 MiB | 1724.3 MB/s | 2/2 | 596,543 | 273.1s / 1,468,205 msg/s |
| Dekaf | 2026-07-26T03:26:11.236074+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1724.3 MB/s | 2/2 | 670,424 | 300.1s / 1,606,287 msg/s |
| Dekaf | 2026-07-26T03:26:38.2493104+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1724.3 MB/s | 2/2 | 742,401 | 327.1s / 1,536,262 msg/s |
| Dekaf | 2026-07-26T03:27:05.2569445+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1724.3 MB/s | 2/3 | 805,379 | 354.1s / 1,614,688 msg/s |
| Dekaf | 2026-07-26T03:27:33.2669113+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1724.3 MB/s | 2/3 | 881,038 | 382.1s / 1,608,630 msg/s |
| Dekaf | 2026-07-26T03:28:00.2745703+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1732.4 MB/s | 2/3 | 951,438 | 409.1s / 1,585,397 msg/s |
| Dekaf | 2026-07-26T03:28:27.2777509+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1739.0 MB/s | 2/3 | 1,021,139 | 436.1s / 1,508,162 msg/s |
| Dekaf | 2026-07-26T03:28:54.285602+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1739.0 MB/s | 2/3 | 1,089,620 | 463.2s / 1,559,541 msg/s |
| Dekaf | 2026-07-26T03:29:22.2898766+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/3 | 1,158,790 | 491.2s / 1,574,829 msg/s |
| Dekaf | 2026-07-26T03:29:49.2971849+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/3 | 1,229,165 | 518.2s / 1,528,923 msg/s |
| Dekaf | 2026-07-26T03:30:16.3005216+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1739.0 MB/s | 2/3 | 1,296,773 | 545.2s / 1,544,885 msg/s |
| Dekaf | 2026-07-26T03:30:44.3090863+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/3 | 1,355,187 | 573.2s / 1,579,221 msg/s |
| Dekaf | 2026-07-26T03:31:11.3188497+00:00 | 1 | 10.0 MiB / 7.1 MiB | 1739.0 MB/s | 2/3 | 1,411,000 | 600.2s / 1,380,318 msg/s |
| Dekaf | 2026-07-26T03:31:38.3199952+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1739.0 MB/s | 2/4 | 1,481,693 | 627.2s / 1,605,013 msg/s |
| Dekaf | 2026-07-26T03:32:05.3336886+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/4 | 1,554,262 | 654.2s / 1,608,608 msg/s |
| Dekaf | 2026-07-26T03:32:33.3384062+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1739.0 MB/s | 2/4 | 1,629,914 | 682.2s / 1,545,900 msg/s |
| Dekaf | 2026-07-26T03:33:00.3445041+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1739.0 MB/s | 2/4 | 1,702,523 | 709.2s / 1,583,924 msg/s |
| Dekaf | 2026-07-26T03:33:27.3516364+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1739.0 MB/s | 2/4 | 1,774,195 | 736.2s / 1,556,510 msg/s |
| Dekaf | 2026-07-26T03:33:54.3637113+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/4 | 1,847,495 | 763.2s / 1,574,001 msg/s |
| Dekaf | 2026-07-26T03:34:22.3717582+00:00 | 1 | 12.0 MiB / 5.9 MiB | 1739.0 MB/s | 2/4 | 1,922,941 | 791.2s / 1,519,046 msg/s |
| Dekaf | 2026-07-26T03:34:49.3811497+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/4 | 1,995,154 | 818.2s / 1,577,039 msg/s |
| Dekaf | 2026-07-26T03:35:16.3873423+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1739.0 MB/s | 2/4 | 2,064,328 | 845.2s / 1,578,887 msg/s |
| Dekaf | 2026-07-26T03:35:43.3933583+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1739.0 MB/s | 2/5 | 2,136,735 | 872.3s / 1,530,117 msg/s |
| Dekaf | 2026-07-26T03:36:12.032961+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 426,107 msg/s |
| Dekaf | 2026-07-26T03:36:39.0427408+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1757.8 MB/s | 0/0 | 38,828 | 27.0s / 1,567,313 msg/s |
| Dekaf | 2026-07-26T03:37:06.0487655+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1757.8 MB/s | 1/0 | 95,197 | 54.0s / 1,529,398 msg/s |
| Dekaf | 2026-07-26T03:37:33.0590075+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1768.4 MB/s | 1/0 | 170,559 | 81.0s / 1,599,603 msg/s |
| Dekaf | 2026-07-26T03:38:01.0706387+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.5 MB/s | 1/1 | 252,578 | 109.0s / 1,601,874 msg/s |
| Dekaf | 2026-07-26T03:38:28.0754677+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1768.5 MB/s | 1/1 | 326,803 | 136.0s / 1,584,676 msg/s |
| Dekaf | 2026-07-26T03:38:55.0856975+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1768.5 MB/s | 1/1 | 402,706 | 163.0s / 1,586,823 msg/s |
| Dekaf | 2026-07-26T03:39:23.0947402+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.5 MB/s | 1/2 | 473,428 | 191.1s / 1,616,721 msg/s |
| Dekaf | 2026-07-26T03:39:50.1057561+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.5 MB/s | 1/2 | 548,125 | 218.1s / 1,624,695 msg/s |
| Dekaf | 2026-07-26T03:40:17.1128411+00:00 | 1 | 14.0 MiB / 12.4 MiB | 1768.5 MB/s | 1/2 | 620,677 | 245.1s / 1,597,408 msg/s |
| Dekaf | 2026-07-26T03:40:44.1247976+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.5 MB/s | 1/2 | 694,681 | 272.1s / 1,627,081 msg/s |
| Dekaf | 2026-07-26T03:41:12.1431526+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1768.5 MB/s | 1/2 | 769,083 | 300.1s / 1,590,618 msg/s |
| Dekaf | 2026-07-26T03:41:39.1517621+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1768.5 MB/s | 2/2 | 851,170 | 327.1s / 1,594,191 msg/s |
| Dekaf | 2026-07-26T03:42:06.1585055+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1768.5 MB/s | 2/3 | 927,400 | 354.1s / 1,592,882 msg/s |
| Dekaf | 2026-07-26T03:42:33.1669655+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1768.5 MB/s | 2/3 | 1,010,744 | 381.1s / 1,605,412 msg/s |
| Dekaf | 2026-07-26T03:43:01.1778573+00:00 | 1 | 13.0 MiB / 8.2 MiB | 1768.5 MB/s | 2/3 | 1,093,749 | 409.1s / 1,591,399 msg/s |
| Dekaf | 2026-07-26T03:43:28.185155+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/4 | 1,174,737 | 436.1s / 1,588,912 msg/s |
| Dekaf | 2026-07-26T03:43:55.1947977+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1768.5 MB/s | 2/4 | 1,258,621 | 463.1s / 1,556,584 msg/s |
| Dekaf | 2026-07-26T03:44:22.2069022+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1768.5 MB/s | 2/4 | 1,342,941 | 490.1s / 1,615,038 msg/s |
| Dekaf | 2026-07-26T03:44:50.2168475+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/4 | 1,422,800 | 518.2s / 1,596,920 msg/s |
| Dekaf | 2026-07-26T03:45:17.2275069+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1768.5 MB/s | 2/4 | 1,501,228 | 545.2s / 1,556,543 msg/s |
| Dekaf | 2026-07-26T03:45:44.2377619+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1768.5 MB/s | 2/5 | 1,578,881 | 572.2s / 1,525,089 msg/s |
| Dekaf | 2026-07-26T03:46:11.2456856+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/5 | 1,650,198 | 599.2s / 1,490,551 msg/s |
| Dekaf | 2026-07-26T03:46:39.2530537+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/5 | 1,720,126 | 627.2s / 1,505,405 msg/s |
| Dekaf | 2026-07-26T03:47:06.2583367+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1768.5 MB/s | 2/5 | 1,789,592 | 654.2s / 1,459,447 msg/s |
| Dekaf | 2026-07-26T03:47:33.2628523+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1768.5 MB/s | 2/5 | 1,855,853 | 681.2s / 1,471,820 msg/s |
| Dekaf | 2026-07-26T03:48:00.2730692+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/5 | 1,919,891 | 708.2s / 1,480,931 msg/s |
| Dekaf | 2026-07-26T03:48:28.2872415+00:00 | 1 | 12.0 MiB / 10.1 MiB | 1768.5 MB/s | 2/5 | 1,988,492 | 736.2s / 1,492,647 msg/s |
| Dekaf | 2026-07-26T03:48:55.2966915+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1768.5 MB/s | 2/5 | 2,055,048 | 763.2s / 1,497,594 msg/s |
| Dekaf | 2026-07-26T03:49:22.3035045+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/5 | 2,122,108 | 790.2s / 1,457,910 msg/s |
| Dekaf | 2026-07-26T03:49:50.3188328+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/6 | 2,192,707 | 818.3s / 1,494,301 msg/s |
| Dekaf | 2026-07-26T03:50:17.3268587+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1768.5 MB/s | 2/6 | 2,261,594 | 845.3s / 1,460,256 msg/s |
| Dekaf | 2026-07-26T03:50:44.3380528+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1768.5 MB/s | 2/6 | 2,328,422 | 872.3s / 1,484,763 msg/s |
| Dekaf | 2026-07-26T03:51:11.3450936+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1768.5 MB/s | 2/6 | 2,395,991 | 899.3s / 1,480,998 msg/s |
| Dekaf (3conn) | 2026-07-26T04:06:40.0982404+00:00 | 1 | 16.0 MiB / 6.2 MiB | 1598.2 MB/s | 0/0 | 1,482 | 27.0s / 1,362,541 msg/s |
| Dekaf (3conn) | 2026-07-26T04:07:07.1048136+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1598.2 MB/s | 1/0 | 2,639 | 54.0s / 1,399,293 msg/s |
| Dekaf (3conn) | 2026-07-26T04:07:34.1162366+00:00 | 1 | 14.0 MiB / 5.6 MiB | 1598.2 MB/s | 1/0 | 3,748 | 81.0s / 1,319,515 msg/s |
| Dekaf (3conn) | 2026-07-26T04:08:01.1225942+00:00 | 1 | 12.0 MiB / 4.5 MiB | 1598.2 MB/s | 2/0 | 5,834 | 108.0s / 1,518,772 msg/s |
| Dekaf (3conn) | 2026-07-26T04:08:29.1327409+00:00 | 1 | 12.0 MiB / 10.3 MiB | 1694.4 MB/s | 2/1 | 9,762 | 136.1s / 1,470,440 msg/s |
| Dekaf (3conn) | 2026-07-26T04:08:56.1457928+00:00 | 1 | 12.0 MiB / 4.8 MiB | 1694.4 MB/s | 2/1 | 14,086 | 163.1s / 1,514,377 msg/s |
| Dekaf (3conn) | 2026-07-26T04:09:23.1613272+00:00 | 1 | 12.0 MiB / 0.6 MiB | 1694.4 MB/s | 2/1 | 18,390 | 190.1s / 1,466,469 msg/s |
| Dekaf (3conn) | 2026-07-26T04:09:50.1687662+00:00 | 1 | 13.0 MiB / 5.7 MiB | 1694.4 MB/s | 3/1 | 21,767 | 217.1s / 1,371,560 msg/s |
| Dekaf (3conn) | 2026-07-26T04:10:18.1756311+00:00 | 1 | 13.0 MiB / 1.5 MiB | 1694.4 MB/s | 3/1 | 24,898 | 245.1s / 1,499,466 msg/s |
| Dekaf (3conn) | 2026-07-26T04:10:45.1918444+00:00 | 1 | 13.0 MiB / 6.6 MiB | 1764.7 MB/s | 3/2 | 28,785 | 272.1s / 1,548,014 msg/s |
| Dekaf (3conn) | 2026-07-26T04:11:12.1990698+00:00 | 1 | 13.0 MiB / 3.2 MiB | 1764.7 MB/s | 3/2 | 32,500 | 299.1s / 1,567,700 msg/s |
| Dekaf (3conn) | 2026-07-26T04:11:39.2185079+00:00 | 1 | 13.0 MiB / 2.6 MiB | 1781.9 MB/s | 3/2 | 37,349 | 326.1s / 1,603,640 msg/s |
| Dekaf (3conn) | 2026-07-26T04:12:07.235795+00:00 | 1 | 13.0 MiB / 5.6 MiB | 1781.9 MB/s | 3/3 | 42,258 | 354.2s / 1,517,826 msg/s |
| Dekaf (3conn) | 2026-07-26T04:12:34.247176+00:00 | 1 | 13.0 MiB / 2.2 MiB | 1781.9 MB/s | 3/3 | 45,495 | 381.2s / 1,343,965 msg/s |
| Dekaf (3conn) | 2026-07-26T04:13:01.2597303+00:00 | 1 | 13.0 MiB / 3.4 MiB | 1781.9 MB/s | 3/3 | 48,626 | 408.2s / 1,508,271 msg/s |
| Dekaf (3conn) | 2026-07-26T04:13:29.2783902+00:00 | 1 | 13.0 MiB / 8.3 MiB | 1781.9 MB/s | 3/3 | 51,589 | 436.2s / 1,418,141 msg/s |
| Dekaf (3conn) | 2026-07-26T04:13:56.2884365+00:00 | 1 | 14.0 MiB / 7.7 MiB | 1863.7 MB/s | 3/3 | 55,165 | 463.2s / 1,506,064 msg/s |
| Dekaf (3conn) | 2026-07-26T04:14:23.3051323+00:00 | 1 | 14.0 MiB / 3.1 MiB | 1863.7 MB/s | 4/3 | 58,422 | 490.2s / 1,424,440 msg/s |
| Dekaf (3conn) | 2026-07-26T04:14:50.3231236+00:00 | 1 | 14.0 MiB / 4.6 MiB | 1863.7 MB/s | 4/4 | 61,189 | 517.2s / 1,336,716 msg/s |
| Dekaf (3conn) | 2026-07-26T04:15:18.3451782+00:00 | 1 | 14.0 MiB / 4.2 MiB | 1863.7 MB/s | 4/4 | 63,631 | 545.2s / 1,400,385 msg/s |
| Dekaf (3conn) | 2026-07-26T04:15:45.3540321+00:00 | 1 | 12.0 MiB / 5.2 MiB | 1863.7 MB/s | 4/4 | 65,574 | 572.3s / 1,248,119 msg/s |
| Dekaf (3conn) | 2026-07-26T04:16:12.365762+00:00 | 1 | 12.0 MiB / 4.7 MiB | 1863.7 MB/s | 5/4 | 68,373 | 599.3s / 1,426,778 msg/s |
| Dekaf (3conn) | 2026-07-26T04:16:39.3812195+00:00 | 1 | 12.0 MiB / 5.7 MiB | 1863.7 MB/s | 5/4 | 74,039 | 626.3s / 1,465,078 msg/s |
| Dekaf (3conn) | 2026-07-26T04:17:07.389004+00:00 | 1 | 12.0 MiB / 3.3 MiB | 1863.7 MB/s | 5/5 | 79,610 | 654.3s / 1,477,937 msg/s |
| Dekaf (3conn) | 2026-07-26T04:17:34.4053223+00:00 | 1 | 12.0 MiB / 3.2 MiB | 1863.7 MB/s | 5/5 | 85,033 | 681.3s / 1,589,084 msg/s |
| Dekaf (3conn) | 2026-07-26T04:18:01.41605+00:00 | 1 | 13.0 MiB / 3.4 MiB | 1863.7 MB/s | 6/5 | 88,602 | 708.3s / 1,462,234 msg/s |
| Dekaf (3conn) | 2026-07-26T04:18:28.4248757+00:00 | 1 | 13.0 MiB / 3.9 MiB | 1863.7 MB/s | 6/5 | 91,357 | 735.3s / 1,341,601 msg/s |
| Dekaf (3conn) | 2026-07-26T04:18:56.430763+00:00 | 1 | 14.0 MiB / 6.1 MiB | 1863.7 MB/s | 7/5 | 94,166 | 763.3s / 1,437,384 msg/s |
| Dekaf (3conn) | 2026-07-26T04:19:23.4384046+00:00 | 1 | 14.0 MiB / 2.4 MiB | 1863.7 MB/s | 7/5 | 95,917 | 790.3s / 1,381,386 msg/s |
| Dekaf (3conn) | 2026-07-26T04:19:50.445773+00:00 | 1 | 14.0 MiB / 7.8 MiB | 1863.7 MB/s | 7/6 | 97,769 | 817.3s / 1,321,593 msg/s |
| Dekaf (3conn) | 2026-07-26T04:20:17.4537854+00:00 | 1 | 14.0 MiB / 5.9 MiB | 1863.7 MB/s | 7/6 | 100,039 | 844.4s / 1,563,533 msg/s |
| Dekaf (3conn) | 2026-07-26T04:20:45.4613031+00:00 | 1 | 12.0 MiB / 2.0 MiB | 1863.7 MB/s | 8/6 | 104,028 | 872.4s / 1,455,552 msg/s |
| Dekaf (3conn) | 2026-07-26T04:21:12.4737865+00:00 | 1 | 12.0 MiB / 2.5 MiB | 1863.7 MB/s | 8/6 | 109,490 | 899.4s / 1,588,055 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-26T03:21:41.2545771+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-26T03:21:56.2758336+00:00 | 1 | capacity | succeeded | 15,021ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-26T03:22:26.3002743+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-26T03:22:41.312804+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:23:11.3370834+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:23:26.3509506+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 9.4 MiB |
| Dekaf | 2026-07-26T03:24:26.3960373+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:24:41.4097646+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:26:41.4914538+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:26:56.511156+00:00 | 1 | capacity | failed | 15,019ms | 12.0 MiB / 7.7 MiB |
| Dekaf | 2026-07-26T03:30:56.7266822+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:31:11.7397783+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:35:11.9395045+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-26T03:35:26.9508497+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:36:42.131772+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.5 MiB |
| Dekaf | 2026-07-26T03:36:57.1487513+00:00 | 1 | capacity | succeeded | 15,017ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-26T03:37:27.177647+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-26T03:37:42.1870856+00:00 | 1 | capacity | failed | 15,009ms | 14.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-26T03:38:42.2652208+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:38:57.2779834+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 13.5 MiB |
| Dekaf | 2026-07-26T03:40:57.3812189+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:41:12.3961396+00:00 | 1 | capacity | succeeded | 15,014ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:41:42.4219143+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:41:57.4312127+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.4 MiB |
| Dekaf | 2026-07-26T03:42:57.4778908+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:43:12.4902642+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:45:12.5861322+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 7.3 MiB |
| Dekaf | 2026-07-26T03:45:27.6008555+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:49:27.8293274+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:49:42.843855+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 12.1 MiB |
| Dekaf (3conn) | 2026-07-26T04:06:43.2081067+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-26T04:06:58.2413357+00:00 | 1 | capacity | succeeded | 15,033ms | 14.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-26T04:07:28.2835227+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.0 MiB |
| Dekaf (3conn) | 2026-07-26T04:07:43.3049+00:00 | 1 | capacity | succeeded | 15,021ms | 12.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-26T04:08:13.3497547+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-26T04:08:28.3798465+00:00 | 1 | capacity | failed | 15,030ms | 12.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-26T04:09:28.4818774+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-26T04:09:43.5002151+00:00 | 1 | capacity | succeeded | 15,018ms | 13.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-26T04:10:13.5368021+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-26T04:10:28.5601556+00:00 | 1 | capacity | failed | 15,023ms | 13.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-26T04:11:28.6670338+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-26T04:11:43.6920517+00:00 | 1 | capacity | failed | 15,025ms | 13.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-26T04:13:43.8989334+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-07-26T04:13:58.9338461+00:00 | 1 | capacity | succeeded | 15,034ms | 14.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-26T04:14:28.9778817+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 8.9 MiB |
| Dekaf (3conn) | 2026-07-26T04:14:44.0043312+00:00 | 1 | capacity | failed | 15,026ms | 14.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:15:44.1028501+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-07-26T04:15:59.1243525+00:00 | 1 | capacity | succeeded | 15,021ms | 12.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:16:29.1780015+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-26T04:16:44.2027758+00:00 | 1 | capacity | failed | 15,024ms | 12.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-26T04:17:44.3061579+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 9.8 MiB |
| Dekaf (3conn) | 2026-07-26T04:17:59.3300792+00:00 | 1 | capacity | succeeded | 15,024ms | 13.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-26T04:18:29.3773479+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-26T04:18:44.3991656+00:00 | 1 | capacity | succeeded | 15,021ms | 14.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-26T04:19:14.45456+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-26T04:19:29.4758058+00:00 | 1 | capacity | failed | 15,021ms | 14.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-26T04:20:29.5697811+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 8.3 MiB |
| Dekaf (3conn) | 2026-07-26T04:20:44.5962134+00:00 | 1 | capacity | succeeded | 15,026ms | 12.0 MiB / 9.5 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 2,363 |
| Dekaf | 1 | 0.002–0.004ms | 2,972 |
| Dekaf | 1 | 0.004–0.008ms | 10,317 |
| Dekaf | 1 | 0.008–0.016ms | 34,475 |
| Dekaf | 1 | 0.016–0.032ms | 43,437 |
| Dekaf | 1 | 0.032–0.064ms | 52,689 |
| Dekaf | 1 | 0.064–0.128ms | 109,295 |
| Dekaf | 1 | 0.128–0.256ms | 299,106 |
| Dekaf | 1 | 0.256–0.512ms | 273,756 |
| Dekaf | 1 | 0.512–1.024ms | 45,985 |
| Dekaf | 1 | 1.024–2.048ms | 12,013 |
| Dekaf | 1 | 2.048–4.096ms | 3,561 |
| Dekaf | 1 | 4.096–8.192ms | 696 |
| Dekaf | 1 | 8.192–16.384ms | 42 |
| Dekaf | 1 | 16.384–32.768ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,810 |
| Dekaf | 1 | 0.002–0.004ms | 2,350 |
| Dekaf | 1 | 0.004–0.008ms | 9,725 |
| Dekaf | 1 | 0.008–0.016ms | 38,695 |
| Dekaf | 1 | 0.016–0.032ms | 43,563 |
| Dekaf | 1 | 0.032–0.064ms | 51,219 |
| Dekaf | 1 | 0.064–0.128ms | 93,787 |
| Dekaf | 1 | 0.128–0.256ms | 282,341 |
| Dekaf | 1 | 0.256–0.512ms | 336,225 |
| Dekaf | 1 | 0.512–1.024ms | 71,047 |
| Dekaf | 1 | 1.024–2.048ms | 14,959 |
| Dekaf | 1 | 2.048–4.096ms | 4,039 |
| Dekaf | 1 | 4.096–8.192ms | 822 |
| Dekaf | 1 | 8.192–16.384ms | 43 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 41 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 40 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 150 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 417 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 1,555 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 3,841 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 2,912 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 5,402 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 6,256 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 5,531 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 3,478 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 979 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 207 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 10 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 296,501,000 | 2026-07-26T03:09:37.6989744+00:00 | 103.2ms | GC pause | - | - | 207.1s / 1,095,299 msg/s | Gen2 +0 / pause +157.9ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.36x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.10x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.01 | 950.25 | 1,261,503 | 1,262,304 | +0.6% | +0.11% | 1203.06 | 1,261,503 | 0 | 1.27 |
| Dekaf | 1.04 | 970.36 | 1,197,210 | 1,201,132 | +0.6% | +0.02% | 1141.75 | 1,197,210 | 0 | 1.24 |
| Confluent | 1.59 | - | 957,234 | 954,793 | +1.9% | +0.22% | 912.89 | 957,234 | 0 | 1.53 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 377,134 | 419.03 | 932.42 KB |
| Dekaf | 2 | 390,663 | 434.06 | 932.75 KB |
| Dekaf | 3 | 384,664 | 427.39 | 923.08 KB |
| Dekaf (3conn) | 1 | 402,085 | 446.75 | 930.50 KB |
| Dekaf (3conn) | 2 | 394,164 | 437.95 | 940.03 KB |
| Dekaf (3conn) | 3 | 405,195 | 450.21 | 947.59 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:14.5412766+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 611,062 msg/s |
| Dekaf | 2026-07-26T03:21:32.5512172+00:00 | 3 | 16.0 MiB / 14.7 MiB | 421.8 MB/s | 0/0 | 776 | 18.0s / 1,174,285 msg/s |
| Dekaf | 2026-07-26T03:21:51.5601074+00:00 | 1 | 16.0 MiB / 11.8 MiB | 425.3 MB/s | 0/0 | 1,519 | 37.0s / 1,194,738 msg/s |
| Dekaf | 2026-07-26T03:22:09.5619761+00:00 | 1 | 16.0 MiB / 9.7 MiB | 428.7 MB/s | 0/1 | 2,383 | 55.0s / 1,225,306 msg/s |
| Dekaf | 2026-07-26T03:22:27.5773923+00:00 | 2 | 16.0 MiB / 2.1 MiB | 467.9 MB/s | 0/1 | 1,073 | 73.0s / 1,204,078 msg/s |
| Dekaf | 2026-07-26T03:22:45.5877538+00:00 | 2 | 16.0 MiB / 4.0 MiB | 467.9 MB/s | 0/1 | 1,633 | 91.1s / 1,149,561 msg/s |
| Dekaf | 2026-07-26T03:23:03.5924781+00:00 | 3 | 14.0 MiB / 13.3 MiB | 452.5 MB/s | 0/1 | 4,956 | 109.1s / 1,219,202 msg/s |
| Dekaf | 2026-07-26T03:23:21.6033468+00:00 | 3 | 14.0 MiB / 10.5 MiB | 461.1 MB/s | 1/1 | 6,333 | 127.1s / 1,234,819 msg/s |
| Dekaf | 2026-07-26T03:23:40.6113914+00:00 | 1 | 16.0 MiB / 8.3 MiB | 466.8 MB/s | 0/2 | 6,468 | 146.1s / 1,158,573 msg/s |
| Dekaf | 2026-07-26T03:23:58.6206832+00:00 | 1 | 14.0 MiB / 4.7 MiB | 466.8 MB/s | 0/2 | 7,452 | 164.1s / 1,209,860 msg/s |
| Dekaf | 2026-07-26T03:24:16.6317533+00:00 | 2 | 16.0 MiB / 12.6 MiB | 475.2 MB/s | 0/2 | 4,502 | 182.1s / 1,189,617 msg/s |
| Dekaf | 2026-07-26T03:24:34.6401489+00:00 | 2 | 16.0 MiB / 11.0 MiB | 475.7 MB/s | 0/2 | 4,921 | 200.1s / 1,216,713 msg/s |
| Dekaf | 2026-07-26T03:24:52.6626772+00:00 | 3 | 10.0 MiB / 7.5 MiB | 466.1 MB/s | 3/1 | 14,744 | 218.2s / 1,202,291 msg/s |
| Dekaf | 2026-07-26T03:25:10.6750891+00:00 | 3 | 8.0 MiB / 2.8 MiB | 466.1 MB/s | 4/1 | 16,816 | 236.2s / 1,261,656 msg/s |
| Dekaf | 2026-07-26T03:25:29.6935213+00:00 | 1 | 14.0 MiB / 2.7 MiB | 466.8 MB/s | 1/3 | 10,707 | 255.2s / 1,192,989 msg/s |
| Dekaf | 2026-07-26T03:25:47.7052821+00:00 | 1 | 15.0 MiB / 2.7 MiB | 466.8 MB/s | 1/3 | 11,689 | 273.2s / 1,131,081 msg/s |
| Dekaf | 2026-07-26T03:26:05.7173917+00:00 | 2 | 16.0 MiB / 0.9 MiB | 475.7 MB/s | 0/3 | 6,061 | 291.2s / 1,222,070 msg/s |
| Dekaf | 2026-07-26T03:26:23.7283231+00:00 | 2 | 16.0 MiB / 8.6 MiB | 475.7 MB/s | 0/3 | 6,766 | 309.2s / 1,246,475 msg/s |
| Dekaf | 2026-07-26T03:26:41.7419047+00:00 | 3 | 9.0 MiB / 3.0 MiB | 466.1 MB/s | 5/2 | 27,812 | 327.2s / 1,201,844 msg/s |
| Dekaf | 2026-07-26T03:26:59.7516178+00:00 | 3 | 9.0 MiB / 1.8 MiB | 466.1 MB/s | 5/2 | 29,915 | 345.2s / 1,154,835 msg/s |
| Dekaf | 2026-07-26T03:27:18.7670881+00:00 | 1 | 14.0 MiB / 3.8 MiB | 466.8 MB/s | 1/4 | 16,530 | 364.2s / 1,186,255 msg/s |
| Dekaf | 2026-07-26T03:27:36.7722856+00:00 | 1 | 14.0 MiB / 9.3 MiB | 466.8 MB/s | 1/4 | 17,197 | 382.3s / 1,253,180 msg/s |
| Dekaf | 2026-07-26T03:27:54.7911801+00:00 | 2 | 16.0 MiB / 0.8 MiB | 475.7 MB/s | 0/3 | 8,798 | 400.3s / 1,181,418 msg/s |
| Dekaf | 2026-07-26T03:28:12.8021002+00:00 | 2 | 16.0 MiB / 1.0 MiB | 475.7 MB/s | 0/3 | 9,279 | 418.3s / 1,225,328 msg/s |
| Dekaf | 2026-07-26T03:28:30.8120917+00:00 | 3 | 9.0 MiB / 2.3 MiB | 466.1 MB/s | 6/2 | 42,026 | 436.3s / 1,175,888 msg/s |
| Dekaf | 2026-07-26T03:28:48.8272417+00:00 | 3 | 8.0 MiB / 1.7 MiB | 466.1 MB/s | 6/3 | 43,827 | 454.3s / 1,307,182 msg/s |
| Dekaf | 2026-07-26T03:29:07.8442772+00:00 | 1 | 10.0 MiB / 1.6 MiB | 480.7 MB/s | 3/4 | 20,554 | 473.3s / 1,255,290 msg/s |
| Dekaf | 2026-07-26T03:29:25.8600897+00:00 | 1 | 10.0 MiB / 3.6 MiB | 480.7 MB/s | 3/4 | 21,642 | 491.3s / 1,183,869 msg/s |
| Dekaf | 2026-07-26T03:29:43.8731087+00:00 | 2 | 18.0 MiB / 2.0 MiB | 475.7 MB/s | 0/3 | 10,389 | 509.3s / 1,251,271 msg/s |
| Dekaf | 2026-07-26T03:30:01.8885942+00:00 | 2 | 18.0 MiB / 13.2 MiB | 475.7 MB/s | 1/3 | 10,541 | 527.3s / 1,145,269 msg/s |
| Dekaf | 2026-07-26T03:30:19.8985719+00:00 | 3 | 8.0 MiB / 1.1 MiB | 466.1 MB/s | 6/4 | 52,738 | 545.3s / 1,188,201 msg/s |
| Dekaf | 2026-07-26T03:30:37.910755+00:00 | 3 | 8.0 MiB / 0.4 MiB | 466.1 MB/s | 6/4 | 53,506 | 563.4s / 1,101,289 msg/s |
| Dekaf | 2026-07-26T03:30:56.9324969+00:00 | 1 | 8.0 MiB / 4.4 MiB | 480.7 MB/s | 4/5 | 27,721 | 582.4s / 1,214,449 msg/s |
| Dekaf | 2026-07-26T03:31:14.9380826+00:00 | 1 | 8.0 MiB / 2.6 MiB | 480.7 MB/s | 4/5 | 29,038 | 600.4s / 1,234,134 msg/s |
| Dekaf | 2026-07-26T03:31:32.9511018+00:00 | 2 | 15.0 MiB / 2.4 MiB | 475.7 MB/s | 1/4 | 11,349 | 618.4s / 1,170,276 msg/s |
| Dekaf | 2026-07-26T03:31:50.9639986+00:00 | 2 | 18.0 MiB / 3.5 MiB | 475.7 MB/s | 1/5 | 11,599 | 636.4s / 1,137,439 msg/s |
| Dekaf | 2026-07-26T03:32:08.9778148+00:00 | 3 | 8.0 MiB / 0.9 MiB | 466.1 MB/s | 6/5 | 60,154 | 654.4s / 1,181,536 msg/s |
| Dekaf | 2026-07-26T03:32:26.9927236+00:00 | 3 | 8.0 MiB / 2.2 MiB | 466.1 MB/s | 6/5 | 62,368 | 672.5s / 1,256,368 msg/s |
| Dekaf | 2026-07-26T03:32:45.99909+00:00 | 1 | 8.0 MiB / 2.7 MiB | 480.7 MB/s | 4/6 | 36,612 | 691.5s / 1,195,345 msg/s |
| Dekaf | 2026-07-26T03:33:04.0114167+00:00 | 1 | 8.0 MiB / 0.9 MiB | 480.7 MB/s | 4/6 | 38,421 | 709.5s / 1,214,633 msg/s |
| Dekaf | 2026-07-26T03:33:22.0188442+00:00 | 2 | 18.0 MiB / 2.9 MiB | 475.7 MB/s | 1/5 | 12,505 | 727.5s / 1,163,522 msg/s |
| Dekaf | 2026-07-26T03:33:40.0324089+00:00 | 2 | 18.0 MiB / 6.8 MiB | 475.7 MB/s | 1/5 | 12,688 | 745.5s / 1,147,697 msg/s |
| Dekaf | 2026-07-26T03:33:58.0396396+00:00 | 3 | 8.0 MiB / 7.6 MiB | 466.1 MB/s | 6/5 | 70,888 | 763.5s / 1,190,611 msg/s |
| Dekaf | 2026-07-26T03:34:16.0433896+00:00 | 3 | 8.0 MiB / 0.8 MiB | 466.1 MB/s | 6/5 | 72,689 | 781.5s / 1,216,900 msg/s |
| Dekaf | 2026-07-26T03:34:35.0512277+00:00 | 1 | 10.0 MiB / 8.3 MiB | 480.7 MB/s | 5/6 | 46,236 | 800.5s / 1,204,002 msg/s |
| Dekaf | 2026-07-26T03:34:53.0546171+00:00 | 1 | 10.0 MiB / 10.0 MiB | 480.7 MB/s | 6/6 | 47,573 | 818.6s / 1,207,194 msg/s |
| Dekaf | 2026-07-26T03:35:11.0594861+00:00 | 2 | 22.0 MiB / 5.6 MiB | 475.7 MB/s | 3/5 | 13,494 | 836.6s / 1,204,566 msg/s |
| Dekaf | 2026-07-26T03:35:29.0686583+00:00 | 2 | 22.0 MiB / 3.7 MiB | 475.7 MB/s | 3/5 | 13,506 | 854.6s / 1,141,437 msg/s |
| Dekaf | 2026-07-26T03:35:47.0719969+00:00 | 3 | 8.0 MiB / 4.9 MiB | 466.1 MB/s | 6/5 | 84,302 | 872.6s / 1,158,124 msg/s |
| Dekaf | 2026-07-26T03:36:05.0796738+00:00 | 3 | 8.0 MiB / 5.9 MiB | 466.1 MB/s | 6/5 | 86,844 | 890.6s / 1,217,074 msg/s |
| Dekaf (3conn) | 2026-07-26T03:36:37.3683469+00:00 | 3 | 16.0 MiB / 15.9 MiB | 494.8 MB/s | 0/0 | 591 | 9.0s / 1,275,452 msg/s |
| Dekaf (3conn) | 2026-07-26T03:36:55.3899783+00:00 | 3 | 16.0 MiB / 16.0 MiB | 494.8 MB/s | 0/0 | 1,451 | 27.0s / 1,206,456 msg/s |
| Dekaf (3conn) | 2026-07-26T03:37:14.4071268+00:00 | 1 | 16.0 MiB / 5.1 MiB | 489.2 MB/s | 0/1 | 1,504 | 46.0s / 1,149,959 msg/s |
| Dekaf (3conn) | 2026-07-26T03:37:32.4123126+00:00 | 1 | 16.0 MiB / 1.2 MiB | 489.2 MB/s | 0/1 | 1,704 | 64.0s / 1,204,905 msg/s |
| Dekaf (3conn) | 2026-07-26T03:37:50.4304926+00:00 | 2 | 16.0 MiB / 1.6 MiB | 484.5 MB/s | 0/1 | 3,014 | 82.1s / 1,285,429 msg/s |
| Dekaf (3conn) | 2026-07-26T03:38:08.4441239+00:00 | 2 | 16.0 MiB / 3.5 MiB | 484.5 MB/s | 0/1 | 3,432 | 100.1s / 1,175,990 msg/s |
| Dekaf (3conn) | 2026-07-26T03:38:26.4572694+00:00 | 3 | 18.0 MiB / 1.5 MiB | 516.8 MB/s | 0/1 | 5,700 | 118.1s / 1,256,932 msg/s |
| Dekaf (3conn) | 2026-07-26T03:38:44.4815185+00:00 | 3 | 16.0 MiB / 16.0 MiB | 520.8 MB/s | 0/2 | 6,834 | 136.1s / 1,292,555 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:03.4984075+00:00 | 1 | 18.0 MiB / 2.0 MiB | 505.9 MB/s | 1/1 | 3,777 | 155.1s / 1,314,201 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:21.5216432+00:00 | 1 | 20.0 MiB / 8.4 MiB | 505.9 MB/s | 2/1 | 4,046 | 173.2s / 1,372,570 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:39.5431166+00:00 | 2 | 20.0 MiB / 4.5 MiB | 511.7 MB/s | 2/1 | 7,191 | 191.2s / 1,239,361 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:57.5636367+00:00 | 2 | 22.0 MiB / 5.6 MiB | 511.7 MB/s | 2/1 | 7,805 | 209.2s / 1,383,783 msg/s |
| Dekaf (3conn) | 2026-07-26T03:40:15.6121841+00:00 | 3 | 16.0 MiB / 3.7 MiB | 544.8 MB/s | 0/2 | 15,626 | 227.2s / 1,298,805 msg/s |
| Dekaf (3conn) | 2026-07-26T03:40:33.6280869+00:00 | 3 | 14.0 MiB / 1.9 MiB | 557.0 MB/s | 0/2 | 16,915 | 245.2s / 1,258,841 msg/s |
| Dekaf (3conn) | 2026-07-26T03:40:52.6378103+00:00 | 1 | 17.0 MiB / 2.5 MiB | 513.6 MB/s | 3/2 | 6,058 | 264.2s / 1,244,829 msg/s |
| Dekaf (3conn) | 2026-07-26T03:41:10.6401838+00:00 | 1 | 17.0 MiB / 1.1 MiB | 513.6 MB/s | 3/2 | 6,457 | 282.2s / 1,212,902 msg/s |
| Dekaf (3conn) | 2026-07-26T03:41:28.6537381+00:00 | 2 | 27.0 MiB / 3.4 MiB | 511.7 MB/s | 4/1 | 9,224 | 300.3s / 1,240,977 msg/s |
| Dekaf (3conn) | 2026-07-26T03:41:46.6685695+00:00 | 2 | 24.0 MiB / 8.2 MiB | 511.7 MB/s | 4/2 | 9,468 | 318.3s / 1,305,149 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:04.6814945+00:00 | 3 | 12.0 MiB / 6.4 MiB | 557.0 MB/s | 2/2 | 25,310 | 336.3s / 1,279,979 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:22.6863942+00:00 | 3 | 12.0 MiB / 0.2 MiB | 557.0 MB/s | 2/3 | 26,979 | 354.3s / 1,222,503 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:41.701951+00:00 | 1 | 12.0 MiB / 10.5 MiB | 513.6 MB/s | 5/2 | 9,977 | 373.3s / 1,316,408 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:59.7153415+00:00 | 1 | 10.0 MiB / 1.4 MiB | 513.6 MB/s | 5/2 | 10,911 | 391.3s / 1,320,461 msg/s |
| Dekaf (3conn) | 2026-07-26T03:43:17.7307536+00:00 | 2 | 24.0 MiB / 1.9 MiB | 511.7 MB/s | 4/3 | 10,061 | 409.3s / 1,317,003 msg/s |
| Dekaf (3conn) | 2026-07-26T03:43:35.7432395+00:00 | 2 | 24.0 MiB / 1.0 MiB | 511.7 MB/s | 4/3 | 10,244 | 427.3s / 1,134,990 msg/s |
| Dekaf (3conn) | 2026-07-26T03:43:53.7598896+00:00 | 3 | 13.0 MiB / 2.0 MiB | 557.0 MB/s | 3/3 | 34,457 | 445.3s / 1,314,861 msg/s |
| Dekaf (3conn) | 2026-07-26T03:44:11.7731459+00:00 | 3 | 13.0 MiB / 1.4 MiB | 557.0 MB/s | 3/3 | 36,213 | 463.4s / 1,250,604 msg/s |
| Dekaf (3conn) | 2026-07-26T03:44:30.7994734+00:00 | 1 | 10.0 MiB / 1.6 MiB | 518.5 MB/s | 6/3 | 19,824 | 482.4s / 1,272,772 msg/s |
| Dekaf (3conn) | 2026-07-26T03:44:48.8186563+00:00 | 1 | 11.0 MiB / 5.0 MiB | 518.5 MB/s | 6/3 | 21,272 | 500.4s / 1,365,885 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:06.8366469+00:00 | 2 | 27.0 MiB / 1.9 MiB | 511.7 MB/s | 5/3 | 10,768 | 518.4s / 1,252,373 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:24.8426483+00:00 | 2 | 27.0 MiB / 6.6 MiB | 511.7 MB/s | 5/3 | 10,827 | 536.4s / 1,242,282 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:42.853089+00:00 | 3 | 8.0 MiB / 5.6 MiB | 557.0 MB/s | 5/3 | 48,370 | 554.4s / 1,266,096 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:00.8627653+00:00 | 3 | 8.0 MiB / 7.1 MiB | 557.0 MB/s | 6/3 | 51,658 | 572.4s / 1,307,992 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:19.8721727+00:00 | 1 | 13.0 MiB / 1.9 MiB | 518.5 MB/s | 8/3 | 24,981 | 591.5s / 1,332,837 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:37.8780388+00:00 | 1 | 13.0 MiB / 12.6 MiB | 518.5 MB/s | 9/3 | 25,615 | 609.5s / 1,322,770 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:55.8861769+00:00 | 2 | 30.0 MiB / 3.9 MiB | 511.7 MB/s | 6/4 | 11,096 | 627.5s / 1,258,981 msg/s |
| Dekaf (3conn) | 2026-07-26T03:47:13.9064218+00:00 | 2 | 30.0 MiB / 4.9 MiB | 511.7 MB/s | 6/4 | 11,206 | 645.5s / 1,208,208 msg/s |
| Dekaf (3conn) | 2026-07-26T03:47:31.9252389+00:00 | 3 | 10.0 MiB / 2.9 MiB | 557.0 MB/s | 8/3 | 66,985 | 663.5s / 1,292,105 msg/s |
| Dekaf (3conn) | 2026-07-26T03:47:49.9467683+00:00 | 3 | 11.0 MiB / 6.2 MiB | 557.0 MB/s | 8/3 | 69,853 | 681.5s / 1,241,851 msg/s |
| Dekaf (3conn) | 2026-07-26T03:48:08.9685344+00:00 | 1 | 15.0 MiB / 4.4 MiB | 522.6 MB/s | 11/3 | 29,002 | 700.5s / 1,327,994 msg/s |
| Dekaf (3conn) | 2026-07-26T03:48:26.9832898+00:00 | 1 | 15.0 MiB / 1.8 MiB | 522.6 MB/s | 11/3 | 29,472 | 718.5s / 1,217,170 msg/s |
| Dekaf (3conn) | 2026-07-26T03:48:45.0001632+00:00 | 2 | 30.0 MiB / 3.5 MiB | 511.7 MB/s | 6/5 | 11,657 | 736.5s / 1,249,015 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:03.0081013+00:00 | 2 | 30.0 MiB / 6.0 MiB | 517.0 MB/s | 6/5 | 11,724 | 754.6s / 1,323,578 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:21.0201553+00:00 | 3 | 9.0 MiB / 6.9 MiB | 557.0 MB/s | 9/4 | 83,000 | 772.6s / 1,229,361 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:39.0267427+00:00 | 3 | 11.0 MiB / 3.6 MiB | 557.0 MB/s | 9/5 | 85,463 | 790.6s / 1,330,265 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:58.0320277+00:00 | 1 | 11.0 MiB / 2.7 MiB | 522.6 MB/s | 13/3 | 34,173 | 809.6s / 1,259,880 msg/s |
| Dekaf (3conn) | 2026-07-26T03:50:16.0408997+00:00 | 1 | 9.0 MiB / 2.7 MiB | 522.6 MB/s | 13/3 | 36,368 | 827.6s / 1,318,377 msg/s |
| Dekaf (3conn) | 2026-07-26T03:50:34.0564902+00:00 | 2 | 22.0 MiB / 10.8 MiB | 517.0 MB/s | 7/5 | 11,877 | 845.7s / 1,292,031 msg/s |
| Dekaf (3conn) | 2026-07-26T03:50:52.0661133+00:00 | 2 | 22.0 MiB / 2.3 MiB | 517.0 MB/s | 8/5 | 11,950 | 863.7s / 1,250,242 msg/s |
| Dekaf (3conn) | 2026-07-26T03:51:10.0750982+00:00 | 3 | 11.0 MiB / 3.7 MiB | 557.0 MB/s | 9/5 | 95,222 | 881.7s / 1,272,037 msg/s |
| Dekaf (3conn) | 2026-07-26T03:51:28.0807598+00:00 | 3 | 11.0 MiB / 2.2 MiB | 557.0 MB/s | 9/5 | 96,996 | 899.7s / 1,235,975 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-26T03:21:44.7038597+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-26T03:21:44.7332305+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-26T03:21:59.7494329+00:00 | 3 | capacity | failed | 15,045ms | 16.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-26T03:21:59.7773695+00:00 | 2 | capacity | failed | 15,050ms | 16.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-26T03:22:59.9713218+00:00 | 2 | capacity | started | 0ms | 18.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-26T03:23:00.0182033+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 6.6 MiB |
| Dekaf | 2026-07-26T03:23:15.0310069+00:00 | 2 | capacity | failed | 15,059ms | 16.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-26T03:23:15.0435192+00:00 | 3 | capacity | succeeded | 15,066ms | 14.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-26T03:23:45.1998108+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-26T03:23:58.8775318+00:00 | 1 | capacity | succeeded | 15,073ms | 14.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-26T03:24:03.267758+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-26T03:24:28.9603489+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 0.7 MiB |
| Dekaf | 2026-07-26T03:24:44.0163238+00:00 | 1 | capacity | failed | 15,056ms | 14.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-26T03:25:03.5160429+00:00 | 3 | capacity | succeeded | 15,056ms | 8.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-26T03:25:15.5360148+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-26T03:25:33.690873+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-26T03:25:48.7621948+00:00 | 3 | capacity | succeeded | 15,071ms | 9.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:25:59.3318458+00:00 | 1 | capacity | failed | 15,046ms | 14.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-26T03:26:33.9306517+00:00 | 3 | capacity | failed | 15,055ms | 9.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-26T03:27:49.2491246+00:00 | 3 | capacity | succeeded | 15,068ms | 8.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-26T03:27:59.8133516+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-26T03:28:19.3567953+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-26T03:28:34.409045+00:00 | 3 | capacity | failed | 15,052ms | 8.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-26T03:29:00.096133+00:00 | 1 | capacity | succeeded | 15,102ms | 10.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-26T03:29:31.62711+00:00 | 2 | capacity | started | 0ms | 18.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-26T03:29:34.6341857+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-26T03:29:46.6854689+00:00 | 2 | capacity | succeeded | 15,058ms | 18.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-26T03:29:49.6999958+00:00 | 3 | capacity | failed | 15,065ms | 8.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-26T03:30:16.8223127+00:00 | 2 | capacity | started | 0ms | 20.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-26T03:30:31.8779376+00:00 | 2 | capacity | failed | 15,055ms | 18.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-26T03:31:30.7710278+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-26T03:31:45.8395007+00:00 | 1 | capacity | failed | 15,068ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-26T03:31:47.2104592+00:00 | 2 | capacity | failed | 15,041ms | 18.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-26T03:32:05.2131604+00:00 | 3 | capacity | failed | 15,043ms | 8.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-26T03:33:47.6945627+00:00 | 2 | capacity | started | 0ms | 20.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-26T03:34:01.3943919+00:00 | 1 | capacity | succeeded | 15,058ms | 9.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-26T03:34:31.5185147+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-26T03:34:32.8688112+00:00 | 2 | capacity | started | 0ms | 22.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-26T03:34:47.9373353+00:00 | 2 | capacity | succeeded | 15,068ms | 22.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-26T03:35:18.0820509+00:00 | 2 | capacity | started | 0ms | 19.0 MiB / 10.4 MiB |
| Dekaf | 2026-07-26T03:35:31.8098988+00:00 | 1 | capacity | succeeded | 15,044ms | 11.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-26T03:36:01.9387519+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-26T03:36:03.284262+00:00 | 2 | capacity | started | 0ms | 16.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:36:58.5655269+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:36:58.6096421+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:37:13.6193676+00:00 | 1 | capacity | failed | 15,054ms | 16.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:37:13.6814913+00:00 | 3 | capacity | failed | 15,071ms | 16.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:13.987098+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:14.0403332+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:29.0692859+00:00 | 2 | capacity | succeeded | 15,056ms | 18.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:29.1274839+00:00 | 3 | capacity | failed | 15,087ms | 16.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:59.2701184+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:14.4002347+00:00 | 1 | capacity | succeeded | 15,130ms | 20.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:44.5551491+00:00 | 2 | capacity | started | 0ms | 22.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:59.6323806+00:00 | 2 | capacity | succeeded | 15,077ms | 22.0 MiB / 14.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:59.6393895+00:00 | 1 | capacity | failed | 15,079ms | 20.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:29.7720171+00:00 | 1 | capacity | started | 0ms | 17.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:44.8867717+00:00 | 1 | capacity | succeeded | 15,114ms | 17.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:44.9007076+00:00 | 2 | capacity | succeeded | 15,132ms | 24.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:41:15.0820932+00:00 | 2 | capacity | started | 0ms | 27.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:41:15.0990535+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:41:30.1515116+00:00 | 1 | capacity | succeeded | 15,052ms | 14.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:41:30.2345331+00:00 | 3 | capacity | succeeded | 15,086ms | 12.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:42:00.2736526+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:42:15.3932615+00:00 | 1 | capacity | succeeded | 15,119ms | 12.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:42:15.4551561+00:00 | 3 | capacity | failed | 15,056ms | 12.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:42:45.5447945+00:00 | 2 | capacity | failed | 15,066ms | 24.0 MiB / 9.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:43:00.6106801+00:00 | 1 | capacity | succeeded | 15,064ms | 10.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:43:15.7241124+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:43:30.777736+00:00 | 3 | capacity | succeeded | 15,053ms | 13.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:43:45.8432137+00:00 | 1 | capacity | failed | 15,106ms | 10.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:16.0054678+00:00 | 3 | capacity | succeeded | 15,074ms | 11.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:46.1283897+00:00 | 2 | capacity | started | 0ms | 27.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:46.1553459+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:01.1822261+00:00 | 2 | capacity | succeeded | 15,053ms | 27.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:01.2122259+00:00 | 3 | capacity | succeeded | 15,056ms | 9.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:31.3684677+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:46.3892276+00:00 | 1 | capacity | succeeded | 15,127ms | 12.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:46.439549+00:00 | 3 | capacity | succeeded | 15,071ms | 8.0 MiB / 0.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:16.5325806+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:16.581163+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:31.6107966+00:00 | 1 | capacity | succeeded | 15,078ms | 13.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:31.6943556+00:00 | 2 | capacity | failed | 15,072ms | 30.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:01.7459827+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:16.7916112+00:00 | 1 | capacity | succeeded | 15,045ms | 14.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:32.0791839+00:00 | 2 | capacity | started | 0ms | 26.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:46.9850928+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:47.167894+00:00 | 2 | capacity | failed | 15,088ms | 30.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:02.0673686+00:00 | 1 | capacity | succeeded | 15,082ms | 15.0 MiB / 10.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:32.1914737+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:47.2677283+00:00 | 1 | capacity | succeeded | 15,076ms | 13.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:47.4349107+00:00 | 3 | capacity | failed | 15,147ms | 11.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:49:17.5917612+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:49:32.5079528+00:00 | 1 | capacity | succeeded | 15,063ms | 11.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:49:47.815286+00:00 | 2 | capacity | started | 0ms | 26.0 MiB / 9.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:02.9169399+00:00 | 2 | capacity | succeeded | 15,101ms | 26.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:17.7350869+00:00 | 1 | capacity | succeeded | 15,083ms | 9.0 MiB / 0.3 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:47.9156999+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:48.1435482+00:00 | 2 | capacity | succeeded | 15,083ms | 22.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:51:18.2900378+00:00 | 2 | capacity | started | 0ms | 19.0 MiB / 2.7 MiB |
*60 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 4 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 15 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 40 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 115 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 254 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 263 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 298 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 450 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 687 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 863 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 914 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 681 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 325 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 138 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 10 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 1 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 2 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 4 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 20 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 28 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 35 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 55 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 86 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 119 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 158 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 212 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 192 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 121 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 55 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 6 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 8 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 12 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 19 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 80 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 214 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 521 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 548 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 638 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 870 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 1,371 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 1,755 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 2,003 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 1,453 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 729 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 365 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 39 |
| Dekaf | 1 | 0.001–0.002ms | 4 |
| Dekaf | 1 | 0.002–0.004ms | 3 |
| Dekaf | 1 | 0.004–0.008ms | 10 |
| Dekaf | 1 | 0.008–0.016ms | 42 |
| Dekaf | 1 | 0.016–0.032ms | 133 |
| Dekaf | 1 | 0.032–0.064ms | 248 |
| Dekaf | 1 | 0.064–0.128ms | 236 |
| Dekaf | 1 | 0.128–0.256ms | 300 |
| Dekaf | 1 | 0.256–0.512ms | 502 |
| Dekaf | 1 | 0.512–1.024ms | 888 |
| Dekaf | 1 | 1.024–2.048ms | 1,134 |
| Dekaf | 1 | 2.048–4.096ms | 1,249 |
| Dekaf | 1 | 4.096–8.192ms | 874 |
| Dekaf | 1 | 8.192–16.384ms | 526 |
| Dekaf | 1 | 16.384–32.768ms | 237 |
| Dekaf | 1 | 32.768–65.536ms | 19 |
| Dekaf | 2 | 0.002–0.004ms | 3 |
| Dekaf | 2 | 0.008–0.016ms | 4 |
| Dekaf | 2 | 0.016–0.032ms | 9 |
| Dekaf | 2 | 0.032–0.064ms | 23 |
| Dekaf | 2 | 0.064–0.128ms | 42 |
| Dekaf | 2 | 0.128–0.256ms | 60 |
| Dekaf | 2 | 0.256–0.512ms | 82 |
| Dekaf | 2 | 0.512–1.024ms | 147 |
| Dekaf | 2 | 1.024–2.048ms | 208 |
| Dekaf | 2 | 2.048–4.096ms | 293 |
| Dekaf | 2 | 4.096–8.192ms | 308 |
| Dekaf | 2 | 8.192–16.384ms | 145 |
| Dekaf | 2 | 16.384–32.768ms | 32 |
| Dekaf | 2 | 32.768–65.536ms | 7 |
| Dekaf | 3 | 0.001–0.002ms | 5 |
| Dekaf | 3 | 0.002–0.004ms | 9 |
| Dekaf | 3 | 0.004–0.008ms | 22 |
| Dekaf | 3 | 0.008–0.016ms | 92 |
| Dekaf | 3 | 0.016–0.032ms | 232 |
| Dekaf | 3 | 0.032–0.064ms | 522 |
| Dekaf | 3 | 0.064–0.128ms | 493 |
| Dekaf | 3 | 0.128–0.256ms | 604 |
| Dekaf | 3 | 0.256–0.512ms | 991 |
| Dekaf | 3 | 0.512–1.024ms | 1,625 |
| Dekaf | 3 | 1.024–2.048ms | 2,125 |
| Dekaf | 3 | 2.048–4.096ms | 2,146 |
| Dekaf | 3 | 4.096–8.192ms | 1,510 |
| Dekaf | 3 | 8.192–16.384ms | 706 |
| Dekaf | 3 | 16.384–32.768ms | 276 |
| Dekaf | 3 | 32.768–65.536ms | 15 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 6,000 | 2026-07-26T03:06:14.4100657+00:00 | 101.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 19,000 | 2026-07-26T03:06:14.4405305+00:00 | 139.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 25,000 | 2026-07-26T03:06:14.449303+00:00 | 141.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 66,000 | 2026-07-26T03:06:14.5084313+00:00 | 217.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 69,000 | 2026-07-26T03:06:14.515738+00:00 | 211.7ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 70,000 | 2026-07-26T03:06:14.5168545+00:00 | 188.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 75,000 | 2026-07-26T03:06:14.5239954+00:00 | 209.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 80,000 | 2026-07-26T03:06:14.5355859+00:00 | 170.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 82,000 | 2026-07-26T03:06:14.539815+00:00 | 182.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 89,000 | 2026-07-26T03:06:14.5476913+00:00 | 189.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 90,000 | 2026-07-26T03:06:14.5495632+00:00 | 173.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 92,000 | 2026-07-26T03:06:14.5523185+00:00 | 183.7ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 102,000 | 2026-07-26T03:06:14.5692888+00:00 | 184.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 110,000 | 2026-07-26T03:06:14.5835189+00:00 | 178.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 115,000 | 2026-07-26T03:06:14.5916334+00:00 | 191.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 116,000 | 2026-07-26T03:06:14.5931811+00:00 | 190.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 122,000 | 2026-07-26T03:06:14.6029956+00:00 | 187.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 123,000 | 2026-07-26T03:06:14.6042987+00:00 | 186.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 126,000 | 2026-07-26T03:06:14.6086535+00:00 | 178.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 130,000 | 2026-07-26T03:06:14.6153784+00:00 | 179.7ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 135,000 | 2026-07-26T03:06:14.6222281+00:00 | 167.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 142,000 | 2026-07-26T03:06:14.63343+00:00 | 182.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 170,000 | 2026-07-26T03:06:14.7487171+00:00 | 148.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 173,000 | 2026-07-26T03:06:14.7553655+00:00 | 143.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 180,000 | 2026-07-26T03:06:14.766203+00:00 | 139.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 190,000 | 2026-07-26T03:06:14.7868208+00:00 | 140.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 192,000 | 2026-07-26T03:06:14.7924953+00:00 | 102.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 193,000 | 2026-07-26T03:06:14.7935966+00:00 | 133.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 203,000 | 2026-07-26T03:06:14.8064632+00:00 | 126.7ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 207,000 | 2026-07-26T03:06:14.8139377+00:00 | 138.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 211,000 | 2026-07-26T03:06:14.8195151+00:00 | 135.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 212,000 | 2026-07-26T03:06:14.8204004+00:00 | 106.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 218,000 | 2026-07-26T03:06:14.8286764+00:00 | 137.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 220,000 | 2026-07-26T03:06:14.8315271+00:00 | 103.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 223,000 | 2026-07-26T03:06:14.8345304+00:00 | 100.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 228,000 | 2026-07-26T03:06:14.8410063+00:00 | 127.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 231,000 | 2026-07-26T03:06:14.8449769+00:00 | 124.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 234,000 | 2026-07-26T03:06:14.8497595+00:00 | 157.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 237,000 | 2026-07-26T03:06:14.8529247+00:00 | 166.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 241,000 | 2026-07-26T03:06:14.8591458+00:00 | 160.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 251,000 | 2026-07-26T03:06:14.8746222+00:00 | 158.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 257,000 | 2026-07-26T03:06:14.8884612+00:00 | 171.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 258,000 | 2026-07-26T03:06:14.8901085+00:00 | 169.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 261,000 | 2026-07-26T03:06:14.8929135+00:00 | 167.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 264,000 | 2026-07-26T03:06:14.8984283+00:00 | 154.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 267,000 | 2026-07-26T03:06:14.9016872+00:00 | 173.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 268,000 | 2026-07-26T03:06:14.9027246+00:00 | 183.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 274,000 | 2026-07-26T03:06:14.9095509+00:00 | 226.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 277,000 | 2026-07-26T03:06:14.9136519+00:00 | 226.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 281,000 | 2026-07-26T03:06:14.9196117+00:00 | 244.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 288,000 | 2026-07-26T03:06:14.9300312+00:00 | 239.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 301,000 | 2026-07-26T03:06:14.9503349+00:00 | 225.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 306,000 | 2026-07-26T03:06:14.9573323+00:00 | 121.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 314,000 | 2026-07-26T03:06:14.9777158+00:00 | 180.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 326,000 | 2026-07-26T03:06:14.9914217+00:00 | 185.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 327,000 | 2026-07-26T03:06:14.9922123+00:00 | 291.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 328,000 | 2026-07-26T03:06:14.9931652+00:00 | 292.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 329,000 | 2026-07-26T03:06:14.9939256+00:00 | 183.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 335,000 | 2026-07-26T03:06:14.9988037+00:00 | 210.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 352,000 | 2026-07-26T03:06:15.0188416+00:00 | 115.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 353,000 | 2026-07-26T03:06:15.0210446+00:00 | 151.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 354,000 | 2026-07-26T03:06:15.0217039+00:00 | 239.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 357,000 | 2026-07-26T03:06:15.024843+00:00 | 269.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 358,000 | 2026-07-26T03:06:15.0255051+00:00 | 268.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 360,000 | 2026-07-26T03:06:15.027122+00:00 | 173.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 364,000 | 2026-07-26T03:06:15.0322129+00:00 | 241.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 365,000 | 2026-07-26T03:06:15.0355959+00:00 | 193.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 367,000 | 2026-07-26T03:06:15.0372757+00:00 | 264.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 368,000 | 2026-07-26T03:06:15.0382363+00:00 | 263.9ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 372,000 | 2026-07-26T03:06:15.0462032+00:00 | 190.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 375,000 | 2026-07-26T03:06:15.049115+00:00 | 187.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 377,000 | 2026-07-26T03:06:15.0514435+00:00 | 259.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 378,000 | 2026-07-26T03:06:15.0529906+00:00 | 257.7ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 379,000 | 2026-07-26T03:06:15.0541095+00:00 | 182.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 382,000 | 2026-07-26T03:06:15.0594729+00:00 | 184.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 387,000 | 2026-07-26T03:06:15.0762243+00:00 | 239.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 389,000 | 2026-07-26T03:06:15.0789982+00:00 | 160.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 390,000 | 2026-07-26T03:06:15.0802493+00:00 | 193.0ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 391,000 | 2026-07-26T03:06:15.080973+00:00 | 237.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 392,000 | 2026-07-26T03:06:15.0817828+00:00 | 175.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 393,000 | 2026-07-26T03:06:15.0836877+00:00 | 319.6ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 395,000 | 2026-07-26T03:06:15.0874434+00:00 | 152.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 396,000 | 2026-07-26T03:06:15.1326515+00:00 | 107.7ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 397,000 | 2026-07-26T03:06:15.1336798+00:00 | 189.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 402,000 | 2026-07-26T03:06:15.139524+00:00 | 126.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 407,000 | 2026-07-26T03:06:15.1464316+00:00 | 177.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 410,000 | 2026-07-26T03:06:15.1509646+00:00 | 294.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 414,000 | 2026-07-26T03:06:15.1564093+00:00 | 135.8ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 418,000 | 2026-07-26T03:06:15.1612324+00:00 | 164.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 422,000 | 2026-07-26T03:06:15.1663829+00:00 | 112.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 424,000 | 2026-07-26T03:06:15.1699661+00:00 | 123.3ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 428,000 | 2026-07-26T03:06:15.1769312+00:00 | 152.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 429,000 | 2026-07-26T03:06:15.1777605+00:00 | 132.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 430,000 | 2026-07-26T03:06:15.1787762+00:00 | 317.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 433,000 | 2026-07-26T03:06:15.1822697+00:00 | 318.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 437,000 | 2026-07-26T03:06:15.1910226+00:00 | 141.6ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 438,000 | 2026-07-26T03:06:15.1921061+00:00 | 140.5ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 439,000 | 2026-07-26T03:06:15.1935068+00:00 | 125.1ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 443,000 | 2026-07-26T03:06:15.20077+00:00 | 305.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 452,000 | 2026-07-26T03:06:15.2204425+00:00 | 160.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 453,000 | 2026-07-26T03:06:15.2216778+00:00 | 311.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 455,000 | 2026-07-26T03:06:15.2246358+00:00 | 100.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 457,000 | 2026-07-26T03:06:15.2261998+00:00 | 111.2ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 458,000 | 2026-07-26T03:06:15.2270162+00:00 | 110.4ms | GC pause | - | - | 1.0s / 559,807 msg/s | Gen2 +0 / pause +114.4ms |
| Confluent | 463,000 | 2026-07-26T03:06:15.2425361+00:00 | 291.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 470,000 | 2026-07-26T03:06:15.256137+00:00 | 278.8ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 472,000 | 2026-07-26T03:06:15.2589528+00:00 | 167.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 513,000 | 2026-07-26T03:06:15.3171044+00:00 | 275.3ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 520,000 | 2026-07-26T03:06:15.3354729+00:00 | 261.2ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 522,000 | 2026-07-26T03:06:15.3398677+00:00 | 210.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 523,000 | 2026-07-26T03:06:15.3406072+00:00 | 275.8ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 542,000 | 2026-07-26T03:06:15.3584163+00:00 | 239.5ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 543,000 | 2026-07-26T03:06:15.3590821+00:00 | 273.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 552,000 | 2026-07-26T03:06:15.3852744+00:00 | 220.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +264.5ms |
| Confluent | 563,000 | 2026-07-26T03:06:15.3994014+00:00 | 243.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 572,000 | 2026-07-26T03:06:15.4111078+00:00 | 217.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 583,000 | 2026-07-26T03:06:15.4244771+00:00 | 247.2ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 590,000 | 2026-07-26T03:06:15.4358958+00:00 | 238.0ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 592,000 | 2026-07-26T03:06:15.4393739+00:00 | 209.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 602,000 | 2026-07-26T03:06:15.4495938+00:00 | 200.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 630,000 | 2026-07-26T03:06:15.478903+00:00 | 272.5ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 633,000 | 2026-07-26T03:06:15.483925+00:00 | 286.5ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 640,000 | 2026-07-26T03:06:15.4934069+00:00 | 279.4ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 650,000 | 2026-07-26T03:06:15.507693+00:00 | 271.0ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 660,000 | 2026-07-26T03:06:15.5319811+00:00 | 259.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 662,000 | 2026-07-26T03:06:15.5509236+00:00 | 214.8ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 663,000 | 2026-07-26T03:06:15.551742+00:00 | 242.4ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 672,000 | 2026-07-26T03:06:15.5642181+00:00 | 209.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 680,000 | 2026-07-26T03:06:15.5741573+00:00 | 227.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 692,000 | 2026-07-26T03:06:15.5891168+00:00 | 202.0ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 700,000 | 2026-07-26T03:06:15.6006855+00:00 | 207.3ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 702,000 | 2026-07-26T03:06:15.6035574+00:00 | 189.3ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 710,000 | 2026-07-26T03:06:15.6140924+00:00 | 199.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 723,000 | 2026-07-26T03:06:15.6347786+00:00 | 186.8ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 733,000 | 2026-07-26T03:06:15.6517325+00:00 | 173.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 752,000 | 2026-07-26T03:06:15.6761637+00:00 | 135.3ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 760,000 | 2026-07-26T03:06:15.7221846+00:00 | 136.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 780,000 | 2026-07-26T03:06:15.7588619+00:00 | 122.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 783,000 | 2026-07-26T03:06:15.7625966+00:00 | 119.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 790,000 | 2026-07-26T03:06:15.7699943+00:00 | 115.9ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 818,000 | 2026-07-26T03:06:15.8193815+00:00 | 108.3ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 832,000 | 2026-07-26T03:06:15.8397949+00:00 | 102.3ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 840,000 | 2026-07-26T03:06:15.8468484+00:00 | 106.5ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 843,000 | 2026-07-26T03:06:15.8496284+00:00 | 104.0ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 844,000 | 2026-07-26T03:06:15.8508356+00:00 | 108.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 848,000 | 2026-07-26T03:06:15.8572974+00:00 | 105.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 851,000 | 2026-07-26T03:06:15.8632002+00:00 | 100.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 888,000 | 2026-07-26T03:06:15.9576743+00:00 | 128.0ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 891,000 | 2026-07-26T03:06:15.9643581+00:00 | 141.8ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 897,000 | 2026-07-26T03:06:15.9821649+00:00 | 124.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 898,000 | 2026-07-26T03:06:15.9867704+00:00 | 120.1ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 904,000 | 2026-07-26T03:06:16.0026891+00:00 | 110.0ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 947,000 | 2026-07-26T03:06:16.107839+00:00 | 104.7ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 951,000 | 2026-07-26T03:06:16.1114017+00:00 | 101.4ms | GC pause | - | - | 2.0s / 584,165 msg/s | Gen2 +0 / pause +150.1ms |
| Confluent | 1,870,000 | 2026-07-26T03:06:17.6006143+00:00 | 105.2ms | GC pause | - | - | 4.0s / 763,827 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 1,873,000 | 2026-07-26T03:06:17.6030158+00:00 | 102.9ms | GC pause | - | - | 4.0s / 763,827 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 2,462,000 | 2026-07-26T03:06:18.3687995+00:00 | 142.3ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,470,000 | 2026-07-26T03:06:18.3766464+00:00 | 132.7ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,492,000 | 2026-07-26T03:06:18.3950149+00:00 | 184.1ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,493,000 | 2026-07-26T03:06:18.3969327+00:00 | 161.1ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,500,000 | 2026-07-26T03:06:18.4050484+00:00 | 156.3ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,503,000 | 2026-07-26T03:06:18.4113023+00:00 | 159.9ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,512,000 | 2026-07-26T03:06:18.420304+00:00 | 194.4ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,513,000 | 2026-07-26T03:06:18.4212406+00:00 | 186.4ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,530,000 | 2026-07-26T03:06:18.4386747+00:00 | 176.6ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,533,000 | 2026-07-26T03:06:18.4406679+00:00 | 175.5ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +303.6ms |
| Confluent | 2,543,000 | 2026-07-26T03:06:18.4504882+00:00 | 169.3ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,550,000 | 2026-07-26T03:06:18.4574943+00:00 | 163.7ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,552,000 | 2026-07-26T03:06:18.4599703+00:00 | 216.3ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,553,000 | 2026-07-26T03:06:18.4612428+00:00 | 160.1ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,560,000 | 2026-07-26T03:06:18.4671549+00:00 | 154.9ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,562,000 | 2026-07-26T03:06:18.4699335+00:00 | 207.6ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,570,000 | 2026-07-26T03:06:18.4757041+00:00 | 169.6ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,580,000 | 2026-07-26T03:06:18.4842224+00:00 | 162.8ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,582,000 | 2026-07-26T03:06:18.4855288+00:00 | 245.1ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,583,000 | 2026-07-26T03:06:18.4861255+00:00 | 190.0ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,600,000 | 2026-07-26T03:06:18.5057714+00:00 | 172.5ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,602,000 | 2026-07-26T03:06:18.5115019+00:00 | 220.8ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,603,000 | 2026-07-26T03:06:18.5128303+00:00 | 166.4ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,613,000 | 2026-07-26T03:06:18.5261212+00:00 | 182.4ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,620,000 | 2026-07-26T03:06:18.5352258+00:00 | 174.2ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,630,000 | 2026-07-26T03:06:18.5492306+00:00 | 182.0ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,640,000 | 2026-07-26T03:06:18.5661365+00:00 | 166.5ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,643,000 | 2026-07-26T03:06:18.5683975+00:00 | 164.3ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,650,000 | 2026-07-26T03:06:18.5800245+00:00 | 174.1ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,652,000 | 2026-07-26T03:06:18.5835548+00:00 | 174.2ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,653,000 | 2026-07-26T03:06:18.5846765+00:00 | 169.6ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 2,673,000 | 2026-07-26T03:06:18.6498032+00:00 | 107.9ms | GC pause | - | - | 5.0s / 657,767 msg/s | Gen2 +0 / pause +196.7ms |
| Confluent | 4,347,000 | 2026-07-26T03:06:20.9973629+00:00 | 100.8ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,348,000 | 2026-07-26T03:06:20.99785+00:00 | 100.3ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,351,000 | 2026-07-26T03:06:21.0009635+00:00 | 106.6ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,364,000 | 2026-07-26T03:06:21.0121547+00:00 | 104.1ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,367,000 | 2026-07-26T03:06:21.014867+00:00 | 127.5ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,368,000 | 2026-07-26T03:06:21.0154321+00:00 | 126.9ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,371,000 | 2026-07-26T03:06:21.0203066+00:00 | 122.4ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,374,000 | 2026-07-26T03:06:21.0249864+00:00 | 113.2ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,378,000 | 2026-07-26T03:06:21.0279462+00:00 | 116.3ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,384,000 | 2026-07-26T03:06:21.0321232+00:00 | 116.2ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,394,000 | 2026-07-26T03:06:21.0388153+00:00 | 127.4ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,397,000 | 2026-07-26T03:06:21.0405838+00:00 | 136.5ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,401,000 | 2026-07-26T03:06:21.0433211+00:00 | 133.9ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,408,000 | 2026-07-26T03:06:21.0520615+00:00 | 127.9ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,411,000 | 2026-07-26T03:06:21.0635329+00:00 | 116.5ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,417,000 | 2026-07-26T03:06:21.0682716+00:00 | 116.5ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,418,000 | 2026-07-26T03:06:21.0688225+00:00 | 116.0ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,421,000 | 2026-07-26T03:06:21.0717956+00:00 | 115.6ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,424,000 | 2026-07-26T03:06:21.0748112+00:00 | 128.1ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,431,000 | 2026-07-26T03:06:21.0860242+00:00 | 139.9ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,434,000 | 2026-07-26T03:06:21.0888925+00:00 | 121.2ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,437,000 | 2026-07-26T03:06:21.0948636+00:00 | 132.3ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,438,000 | 2026-07-26T03:06:21.096786+00:00 | 130.4ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,441,000 | 2026-07-26T03:06:21.102855+00:00 | 124.5ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,447,000 | 2026-07-26T03:06:21.1120268+00:00 | 128.2ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,448,000 | 2026-07-26T03:06:21.112744+00:00 | 127.6ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,461,000 | 2026-07-26T03:06:21.1249806+00:00 | 121.4ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,467,000 | 2026-07-26T03:06:21.1333088+00:00 | 116.9ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,468,000 | 2026-07-26T03:06:21.1341093+00:00 | 116.2ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,471,000 | 2026-07-26T03:06:21.1367226+00:00 | 116.4ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 4,477,000 | 2026-07-26T03:06:21.1468309+00:00 | 106.9ms | GC pause | - | - | 7.1s / 723,605 msg/s | Gen2 +0 / pause +163.3ms |
| Confluent | 5,491,000 | 2026-07-26T03:06:22.5458433+00:00 | 102.9ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,548,000 | 2026-07-26T03:06:22.6271286+00:00 | 111.6ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,557,000 | 2026-07-26T03:06:22.6365087+00:00 | 125.6ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,558,000 | 2026-07-26T03:06:22.6377669+00:00 | 131.4ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,571,000 | 2026-07-26T03:06:22.6555577+00:00 | 160.3ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,574,000 | 2026-07-26T03:06:22.6596467+00:00 | 104.9ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,577,000 | 2026-07-26T03:06:22.6617842+00:00 | 161.0ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,578,000 | 2026-07-26T03:06:22.6641255+00:00 | 158.8ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,581,000 | 2026-07-26T03:06:22.6659753+00:00 | 160.7ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,584,000 | 2026-07-26T03:06:22.6689093+00:00 | 109.7ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,587,000 | 2026-07-26T03:06:22.6768924+00:00 | 150.8ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,588,000 | 2026-07-26T03:06:22.6775051+00:00 | 150.3ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,591,000 | 2026-07-26T03:06:22.6805109+00:00 | 147.4ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,597,000 | 2026-07-26T03:06:22.6857023+00:00 | 143.4ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,614,000 | 2026-07-26T03:06:22.7050013+00:00 | 115.7ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,624,000 | 2026-07-26T03:06:22.7133708+00:00 | 113.8ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,627,000 | 2026-07-26T03:06:22.7160621+00:00 | 135.2ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,631,000 | 2026-07-26T03:06:22.7198718+00:00 | 131.6ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,634,000 | 2026-07-26T03:06:22.7228105+00:00 | 105.5ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,641,000 | 2026-07-26T03:06:22.7329623+00:00 | 122.4ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,648,000 | 2026-07-26T03:06:22.7420366+00:00 | 114.1ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,651,000 | 2026-07-26T03:06:22.74478+00:00 | 117.8ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,658,000 | 2026-07-26T03:06:22.7509714+00:00 | 117.0ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,661,000 | 2026-07-26T03:06:22.7542173+00:00 | 114.6ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 5,678,000 | 2026-07-26T03:06:22.7697094+00:00 | 108.5ms | GC pause | - | - | 9.1s / 728,357 msg/s | Gen2 +0 / pause +132.0ms |
| Confluent | 382,437,000 | 2026-07-26T03:13:00.5605886+00:00 | 143.0ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,447,000 | 2026-07-26T03:13:00.573139+00:00 | 137.2ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,474,000 | 2026-07-26T03:13:00.6062745+00:00 | 122.3ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,487,000 | 2026-07-26T03:13:00.6261332+00:00 | 111.4ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,488,000 | 2026-07-26T03:13:00.6268519+00:00 | 110.7ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,497,000 | 2026-07-26T03:13:00.635872+00:00 | 106.0ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,511,000 | 2026-07-26T03:13:00.6456622+00:00 | 107.7ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,517,000 | 2026-07-26T03:13:00.6515726+00:00 | 102.1ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,521,000 | 2026-07-26T03:13:00.6545138+00:00 | 100.4ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,528,000 | 2026-07-26T03:13:00.6587639+00:00 | 103.0ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,531,000 | 2026-07-26T03:13:00.6608912+00:00 | 101.5ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Confluent | 382,537,000 | 2026-07-26T03:13:00.6650604+00:00 | 100.7ms | GC pause | - | - | 407.3s / 880,984 msg/s | Gen2 +0 / pause +182.2ms |
| Dekaf | 239,116,000 | 2026-07-26T03:24:35.0110315+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 201.1s / 1,190,365 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 484,947,000 | 2026-07-26T03:27:58.5172142+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 404.3s / 1,139,371 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 484,957,000 | 2026-07-26T03:27:58.521676+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 404.3s / 1,139,371 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 984,687,000 | 2026-07-26T03:34:56.503286+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 822.6s / 1,194,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 151,000 | 2026-07-26T03:36:28.6923211+00:00 | 104.0ms | throughput collapse | - | - | 1.0s / 584,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 152,000 | 2026-07-26T03:36:28.6936822+00:00 | 102.6ms | throughput collapse | - | - | 1.0s / 584,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 161,000 | 2026-07-26T03:36:28.7031222+00:00 | 107.3ms | throughput collapse | - | - | 1.0s / 584,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 162,000 | 2026-07-26T03:36:28.7043462+00:00 | 106.0ms | throughput collapse | - | - | 1.0s / 584,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 794,000 | 2026-07-26T03:36:29.766689+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 796,000 | 2026-07-26T03:36:29.7685195+00:00 | 116.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 804,000 | 2026-07-26T03:36:29.7743669+00:00 | 131.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 806,000 | 2026-07-26T03:36:29.7773769+00:00 | 128.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 814,000 | 2026-07-26T03:36:29.7839857+00:00 | 126.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 816,000 | 2026-07-26T03:36:29.7851301+00:00 | 125.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 824,000 | 2026-07-26T03:36:29.7906345+00:00 | 144.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 826,000 | 2026-07-26T03:36:29.7918872+00:00 | 143.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 834,000 | 2026-07-26T03:36:29.8200489+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 836,000 | 2026-07-26T03:36:29.8244233+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 844,000 | 2026-07-26T03:36:29.8310229+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 846,000 | 2026-07-26T03:36:29.8329607+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 742,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,085,000 | 2026-07-26T03:36:35.2886572+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,025,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,088,000 | 2026-07-26T03:36:35.2899067+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,025,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,095,000 | 2026-07-26T03:36:35.2953036+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,098,000 | 2026-07-26T03:36:35.2964883+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,105,000 | 2026-07-26T03:36:35.2997316+00:00 | 114.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,108,000 | 2026-07-26T03:36:35.3086952+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,115,000 | 2026-07-26T03:36:35.3118101+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,118,000 | 2026-07-26T03:36:35.3133229+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,125,000 | 2026-07-26T03:36:35.3167399+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,128,000 | 2026-07-26T03:36:35.3183452+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,138,000 | 2026-07-26T03:36:35.3431761+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,060,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,990,000 | 2026-07-26T03:36:42.8053493+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,118,066 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 249,797,000 | 2026-07-26T03:39:49.8156137+00:00 | 110.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 202.2s / 1,351,475 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 249,807,000 | 2026-07-26T03:39:49.8197788+00:00 | 117.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 202.2s / 1,351,475 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 249,813,000 | 2026-07-26T03:39:49.8222935+00:00 | 102.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 202.2s / 1,351,475 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 249,817,000 | 2026-07-26T03:39:49.8424638+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 202.2s / 1,351,475 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 267,250,000 | 2026-07-26T03:40:03.3166737+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 215.2s / 1,290,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 267,270,000 | 2026-07-26T03:40:03.3413284+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 215.2s / 1,290,520 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 270,915,000 | 2026-07-26T03:40:06.3079111+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 218.2s / 1,295,607 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 270,918,000 | 2026-07-26T03:40:06.3092174+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 218.2s / 1,295,607 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 351,514,000 | 2026-07-26T03:41:09.3263343+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 281.2s / 1,199,594 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 351,516,000 | 2026-07-26T03:41:09.326963+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 281.2s / 1,199,594 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,200,000 | 2026-07-26T03:43:33.8012589+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 426.3s / 1,413,484 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,210,000 | 2026-07-26T03:43:33.8058192+00:00 | 107.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 426.3s / 1,413,484 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,220,000 | 2026-07-26T03:43:33.8108223+00:00 | 104.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 426.3s / 1,413,484 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,230,000 | 2026-07-26T03:43:33.814537+00:00 | 106.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 426.3s / 1,413,484 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,240,000 | 2026-07-26T03:43:33.8198144+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 426.3s / 1,413,484 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 535,250,000 | 2026-07-26T03:43:33.8278163+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 426.3s / 1,413,484 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 860,954,000 | 2026-07-26T03:47:49.8073567+00:00 | 100.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/succeeded | - | 682.5s / 1,328,223 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 953,310,000 | 2026-07-26T03:49:02.8330656+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 754.6s / 1,323,578 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,626,000 | 2026-07-26T03:49:26.7757324+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,634,000 | 2026-07-26T03:49:26.7792291+00:00 | 101.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,636,000 | 2026-07-26T03:49:26.7802222+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,644,000 | 2026-07-26T03:49:26.7837194+00:00 | 125.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,646,000 | 2026-07-26T03:49:26.7935997+00:00 | 115.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,654,000 | 2026-07-26T03:49:26.8009775+00:00 | 108.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,656,000 | 2026-07-26T03:49:26.8018384+00:00 | 107.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,664,000 | 2026-07-26T03:49:26.8062095+00:00 | 110.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,666,000 | 2026-07-26T03:49:26.8073475+00:00 | 109.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,674,000 | 2026-07-26T03:49:26.8108285+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 982,676,000 | 2026-07-26T03:49:26.8116379+00:00 | 105.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded, 3:capacity/failed | - | 778.6s / 1,288,696 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,093,220,000 | 2026-07-26T03:50:54.8275497+00:00 | 100.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 866.7s / 1,205,088 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 1,121,994,000 | 2026-07-26T03:51:17.8054051+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 889.7s / 1,245,219 msg/s | Gen2 +0 / pause +0.8ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*353 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.54x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.26x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,578,713 | 1,572,432–1,585,018 | 0.92 | 1.16x |
| Confluent | 2 | 1,366,273 | 1,355,806–1,376,821 | 1.29 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 0.94 | 963.56 | 1,571,312 | 1,585,018 | -0.5% | -0.12% | 1498.52 | 1,571,312 | 0 | 1.47 |
| Dekaf (dekaf-first) | 0.90 | 921.28 | 1,556,782 | 1,572,432 | +2.3% | +0.20% | 1484.66 | 1,556,782 | 0 | 1.40 |
| Confluent (dekaf-first) | 1.32 | - | 1,363,127 | 1,376,821 | +4.1% | +0.29% | 1299.98 | 1,363,127 | 0 | 1.80 |
| Confluent (confluent-first) | 1.26 | - | 1,347,349 | 1,355,806 | -0.8% | -0.20% | 1284.93 | 1,347,349 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,366,658 | 1518.49 | 1019.12 KB |
| Dekaf | 1 | 1,377,138 | 1530.13 | 1020.81 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:10.7990707+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 637,491 msg/s |
| Dekaf | 2026-07-26T03:21:28.8039349+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1712.8 MB/s | 0/0 | 24,361 | 18.0s / 1,561,416 msg/s |
| Dekaf | 2026-07-26T03:21:46.8116506+00:00 | 1 | 16.0 MiB / 13.1 MiB | 1726.7 MB/s | 0/0 | 55,023 | 36.0s / 1,594,145 msg/s |
| Dekaf | 2026-07-26T03:22:05.8212475+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1726.7 MB/s | 0/1 | 88,340 | 55.0s / 1,607,447 msg/s |
| Dekaf | 2026-07-26T03:22:23.827775+00:00 | 1 | 16.0 MiB / 14.8 MiB | 1734.2 MB/s | 0/1 | 114,781 | 73.0s / 1,604,145 msg/s |
| Dekaf | 2026-07-26T03:22:41.8406257+00:00 | 1 | 16.0 MiB / 12.2 MiB | 1745.7 MB/s | 0/1 | 141,404 | 91.0s / 1,625,238 msg/s |
| Dekaf | 2026-07-26T03:22:59.8461683+00:00 | 1 | 18.0 MiB / 16.1 MiB | 1745.7 MB/s | 0/1 | 166,308 | 109.0s / 1,611,328 msg/s |
| Dekaf | 2026-07-26T03:23:17.8519107+00:00 | 1 | 18.0 MiB / 16.4 MiB | 1745.7 MB/s | 1/1 | 187,414 | 127.0s / 1,559,824 msg/s |
| Dekaf | 2026-07-26T03:23:35.8579465+00:00 | 1 | 18.0 MiB / 15.6 MiB | 1745.7 MB/s | 1/1 | 210,461 | 145.1s / 1,574,211 msg/s |
| Dekaf | 2026-07-26T03:23:54.865121+00:00 | 1 | 20.0 MiB / 18.3 MiB | 1745.7 MB/s | 1/1 | 232,440 | 164.1s / 1,567,705 msg/s |
| Dekaf | 2026-07-26T03:24:12.8730593+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1745.7 MB/s | 1/2 | 255,653 | 182.1s / 1,576,137 msg/s |
| Dekaf | 2026-07-26T03:24:30.8845708+00:00 | 1 | 18.0 MiB / 16.8 MiB | 1745.7 MB/s | 1/2 | 278,797 | 200.1s / 1,563,780 msg/s |
| Dekaf | 2026-07-26T03:24:48.8888795+00:00 | 1 | 18.0 MiB / 14.6 MiB | 1745.7 MB/s | 1/2 | 302,296 | 218.1s / 1,616,459 msg/s |
| Dekaf | 2026-07-26T03:25:06.8990237+00:00 | 1 | 18.0 MiB / 17.3 MiB | 1745.7 MB/s | 1/2 | 325,960 | 236.1s / 1,580,964 msg/s |
| Dekaf | 2026-07-26T03:25:24.9185006+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1745.7 MB/s | 1/3 | 350,309 | 254.1s / 1,565,217 msg/s |
| Dekaf | 2026-07-26T03:25:43.922746+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1745.7 MB/s | 1/3 | 373,222 | 273.1s / 1,604,896 msg/s |
| Dekaf | 2026-07-26T03:26:01.9313117+00:00 | 1 | 18.0 MiB / 13.7 MiB | 1745.7 MB/s | 1/3 | 394,226 | 291.1s / 1,601,572 msg/s |
| Dekaf | 2026-07-26T03:26:19.9407834+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1745.7 MB/s | 1/3 | 415,963 | 309.1s / 1,565,266 msg/s |
| Dekaf | 2026-07-26T03:26:37.9486438+00:00 | 1 | 18.0 MiB / 15.1 MiB | 1745.7 MB/s | 1/3 | 438,922 | 327.2s / 1,619,625 msg/s |
| Dekaf | 2026-07-26T03:26:55.9606806+00:00 | 1 | 18.0 MiB / 12.8 MiB | 1745.7 MB/s | 1/3 | 460,400 | 345.2s / 1,591,662 msg/s |
| Dekaf | 2026-07-26T03:27:13.9721348+00:00 | 1 | 20.0 MiB / 20.0 MiB | 1745.7 MB/s | 1/3 | 480,652 | 363.2s / 1,573,429 msg/s |
| Dekaf | 2026-07-26T03:27:32.9778492+00:00 | 1 | 18.0 MiB / 16.7 MiB | 1745.7 MB/s | 1/4 | 505,405 | 382.2s / 1,590,625 msg/s |
| Dekaf | 2026-07-26T03:27:50.9891108+00:00 | 1 | 18.0 MiB / 16.1 MiB | 1745.7 MB/s | 1/4 | 528,645 | 400.2s / 1,617,330 msg/s |
| Dekaf | 2026-07-26T03:28:08.9986841+00:00 | 1 | 18.0 MiB / 16.9 MiB | 1745.7 MB/s | 1/4 | 552,460 | 418.2s / 1,624,979 msg/s |
| Dekaf | 2026-07-26T03:28:27.0105886+00:00 | 1 | 18.0 MiB / 16.9 MiB | 1747.6 MB/s | 1/4 | 575,237 | 436.2s / 1,588,839 msg/s |
| Dekaf | 2026-07-26T03:28:45.0130445+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1753.3 MB/s | 1/4 | 597,270 | 454.2s / 1,574,957 msg/s |
| Dekaf | 2026-07-26T03:29:03.0178485+00:00 | 1 | 18.0 MiB / 15.9 MiB | 1753.3 MB/s | 1/4 | 620,602 | 472.2s / 1,572,125 msg/s |
| Dekaf | 2026-07-26T03:29:22.0299414+00:00 | 1 | 18.0 MiB / 15.9 MiB | 1753.3 MB/s | 1/4 | 641,240 | 491.2s / 1,580,843 msg/s |
| Dekaf | 2026-07-26T03:29:40.0367714+00:00 | 1 | 18.0 MiB / 17.7 MiB | 1753.3 MB/s | 1/4 | 662,729 | 509.2s / 1,585,163 msg/s |
| Dekaf | 2026-07-26T03:29:58.0432131+00:00 | 1 | 18.0 MiB / 17.9 MiB | 1753.3 MB/s | 1/4 | 683,226 | 527.3s / 1,502,759 msg/s |
| Dekaf | 2026-07-26T03:30:16.0527373+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1753.3 MB/s | 1/4 | 703,374 | 545.3s / 1,503,891 msg/s |
| Dekaf | 2026-07-26T03:30:34.0665825+00:00 | 1 | 18.0 MiB / 2.7 MiB | 1753.3 MB/s | 1/4 | 720,377 | 563.3s / 1,290,275 msg/s |
| Dekaf | 2026-07-26T03:30:52.072153+00:00 | 1 | 18.0 MiB / 12.8 MiB | 1753.3 MB/s | 1/4 | 733,387 | 581.3s / 1,536,796 msg/s |
| Dekaf | 2026-07-26T03:31:11.0793055+00:00 | 1 | 18.0 MiB / 15.6 MiB | 1753.3 MB/s | 1/4 | 753,671 | 600.3s / 1,529,349 msg/s |
| Dekaf | 2026-07-26T03:31:29.088032+00:00 | 1 | 15.0 MiB / 13.7 MiB | 1753.3 MB/s | 1/4 | 774,992 | 618.3s / 1,581,687 msg/s |
| Dekaf | 2026-07-26T03:31:47.0942748+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1753.3 MB/s | 2/4 | 804,784 | 636.3s / 1,573,769 msg/s |
| Dekaf | 2026-07-26T03:32:05.1026767+00:00 | 1 | 15.0 MiB / 13.1 MiB | 1753.3 MB/s | 2/4 | 841,856 | 654.3s / 1,609,328 msg/s |
| Dekaf | 2026-07-26T03:32:23.1086272+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.3 MB/s | 2/4 | 876,297 | 672.3s / 1,576,027 msg/s |
| Dekaf | 2026-07-26T03:32:42.112783+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1753.3 MB/s | 3/4 | 927,103 | 691.3s / 1,585,097 msg/s |
| Dekaf | 2026-07-26T03:33:00.1198372+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1753.3 MB/s | 3/4 | 976,638 | 709.3s / 1,573,110 msg/s |
| Dekaf | 2026-07-26T03:33:18.1234089+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1753.3 MB/s | 3/5 | 1,023,301 | 727.3s / 1,527,000 msg/s |
| Dekaf | 2026-07-26T03:33:36.1291978+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.3 MB/s | 3/5 | 1,071,135 | 745.3s / 1,571,844 msg/s |
| Dekaf | 2026-07-26T03:33:54.1355541+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.3 MB/s | 3/5 | 1,112,949 | 763.3s / 1,570,775 msg/s |
| Dekaf | 2026-07-26T03:34:12.1371399+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1753.3 MB/s | 3/5 | 1,158,662 | 781.4s / 1,580,105 msg/s |
| Dekaf | 2026-07-26T03:34:31.1410597+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.3 MB/s | 3/6 | 1,209,589 | 800.4s / 1,500,764 msg/s |
| Dekaf | 2026-07-26T03:34:49.1467968+00:00 | 1 | 13.0 MiB / 11.1 MiB | 1753.3 MB/s | 3/6 | 1,260,496 | 818.4s / 1,599,779 msg/s |
| Dekaf | 2026-07-26T03:35:07.1533678+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1753.3 MB/s | 3/6 | 1,304,824 | 836.4s / 1,547,859 msg/s |
| Dekaf | 2026-07-26T03:35:25.1615681+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1753.3 MB/s | 3/6 | 1,350,945 | 854.4s / 1,599,093 msg/s |
| Dekaf | 2026-07-26T03:35:43.1655475+00:00 | 1 | 13.0 MiB / 2.4 MiB | 1753.3 MB/s | 3/6 | 1,399,097 | 872.4s / 1,421,654 msg/s |
| Dekaf | 2026-07-26T03:36:01.1716223+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.3 MB/s | 3/6 | 1,442,668 | 890.4s / 1,594,736 msg/s |
| Dekaf | 2026-07-26T03:36:20.6335047+00:00 | 1 | 16.0 MiB / 14.1 MiB | 1707.4 MB/s | 0/0 | 9,301 | 9.0s / 1,630,613 msg/s |
| Dekaf | 2026-07-26T03:36:38.6416972+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1707.4 MB/s | 0/0 | 34,628 | 27.0s / 1,550,340 msg/s |
| Dekaf | 2026-07-26T03:36:56.6445671+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1707.4 MB/s | 0/0 | 67,512 | 45.0s / 1,518,029 msg/s |
| Dekaf | 2026-07-26T03:37:14.6528428+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1707.4 MB/s | 1/0 | 109,656 | 63.0s / 1,447,892 msg/s |
| Dekaf | 2026-07-26T03:37:32.6583679+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1707.4 MB/s | 1/0 | 159,011 | 81.0s / 1,559,964 msg/s |
| Dekaf | 2026-07-26T03:37:50.6648739+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1728.9 MB/s | 2/0 | 212,257 | 99.0s / 1,555,424 msg/s |
| Dekaf | 2026-07-26T03:38:09.6686407+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1730.8 MB/s | 2/0 | 269,363 | 118.0s / 1,398,617 msg/s |
| Dekaf | 2026-07-26T03:38:27.6711342+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1730.8 MB/s | 2/1 | 312,133 | 136.0s / 1,510,616 msg/s |
| Dekaf | 2026-07-26T03:38:45.6756994+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1730.8 MB/s | 2/1 | 366,185 | 154.0s / 1,548,218 msg/s |
| Dekaf | 2026-07-26T03:39:03.6831554+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1730.8 MB/s | 2/1 | 420,117 | 172.0s / 1,553,107 msg/s |
| Dekaf | 2026-07-26T03:39:21.6865997+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1730.8 MB/s | 2/1 | 473,608 | 190.0s / 1,574,621 msg/s |
| Dekaf | 2026-07-26T03:39:39.6897244+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1730.8 MB/s | 2/1 | 527,488 | 208.1s / 1,520,389 msg/s |
| Dekaf | 2026-07-26T03:39:58.6949796+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1735.4 MB/s | 2/2 | 581,490 | 227.1s / 1,585,007 msg/s |
| Dekaf | 2026-07-26T03:40:16.7027321+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1744.9 MB/s | 2/2 | 634,290 | 245.1s / 1,543,764 msg/s |
| Dekaf | 2026-07-26T03:40:34.710822+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1744.9 MB/s | 2/2 | 686,836 | 263.1s / 1,519,410 msg/s |
| Dekaf | 2026-07-26T03:40:52.7188908+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/2 | 741,357 | 281.1s / 1,588,433 msg/s |
| Dekaf | 2026-07-26T03:41:10.7235673+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1744.9 MB/s | 2/2 | 798,840 | 299.1s / 1,562,161 msg/s |
| Dekaf | 2026-07-26T03:41:29.7286962+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/2 | 858,474 | 318.1s / 1,599,721 msg/s |
| Dekaf | 2026-07-26T03:41:47.7306311+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1744.9 MB/s | 2/2 | 910,267 | 336.1s / 1,592,032 msg/s |
| Dekaf | 2026-07-26T03:42:05.7320266+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1744.9 MB/s | 2/3 | 962,473 | 354.1s / 1,607,764 msg/s |
| Dekaf | 2026-07-26T03:42:23.736564+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,018,008 | 372.1s / 1,624,918 msg/s |
| Dekaf | 2026-07-26T03:42:41.7417483+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,072,284 | 390.1s / 1,576,734 msg/s |
| Dekaf | 2026-07-26T03:42:59.7458776+00:00 | 1 | 12.0 MiB / 3.8 MiB | 1744.9 MB/s | 2/3 | 1,125,303 | 408.1s / 1,583,381 msg/s |
| Dekaf | 2026-07-26T03:43:18.7495236+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1744.9 MB/s | 2/3 | 1,181,314 | 427.1s / 1,580,887 msg/s |
| Dekaf | 2026-07-26T03:43:36.7567549+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,235,686 | 445.1s / 1,556,995 msg/s |
| Dekaf | 2026-07-26T03:43:54.7634868+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,290,600 | 463.1s / 1,581,898 msg/s |
| Dekaf | 2026-07-26T03:44:12.7676101+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,345,094 | 481.1s / 1,560,223 msg/s |
| Dekaf | 2026-07-26T03:44:30.7766012+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,401,575 | 499.1s / 1,574,033 msg/s |
| Dekaf | 2026-07-26T03:44:48.7836054+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,454,271 | 517.1s / 1,597,709 msg/s |
| Dekaf | 2026-07-26T03:45:07.7904913+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,511,334 | 536.1s / 1,578,947 msg/s |
| Dekaf | 2026-07-26T03:45:25.7937322+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 2/3 | 1,565,390 | 554.2s / 1,597,212 msg/s |
| Dekaf | 2026-07-26T03:45:43.8007434+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1744.9 MB/s | 2/3 | 1,613,547 | 572.2s / 1,568,617 msg/s |
| Dekaf | 2026-07-26T03:46:01.8044421+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1744.9 MB/s | 2/3 | 1,669,254 | 590.2s / 1,559,034 msg/s |
| Dekaf | 2026-07-26T03:46:19.8099373+00:00 | 1 | 13.0 MiB / 12.5 MiB | 1744.9 MB/s | 3/3 | 1,719,611 | 608.2s / 1,573,822 msg/s |
| Dekaf | 2026-07-26T03:46:37.8163589+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1744.9 MB/s | 3/3 | 1,770,609 | 626.2s / 1,560,658 msg/s |
| Dekaf | 2026-07-26T03:46:56.8236767+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1744.9 MB/s | 3/3 | 1,824,530 | 645.2s / 1,583,756 msg/s |
| Dekaf | 2026-07-26T03:47:14.8333712+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1744.9 MB/s | 3/4 | 1,874,670 | 663.2s / 1,561,549 msg/s |
| Dekaf | 2026-07-26T03:47:32.838275+00:00 | 1 | 13.0 MiB / 12.3 MiB | 1744.9 MB/s | 3/4 | 1,923,104 | 681.2s / 1,557,790 msg/s |
| Dekaf | 2026-07-26T03:47:50.8435917+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1744.9 MB/s | 3/4 | 1,972,762 | 699.2s / 1,557,150 msg/s |
| Dekaf | 2026-07-26T03:48:08.850496+00:00 | 1 | 11.0 MiB / 10.6 MiB | 1744.9 MB/s | 3/4 | 2,027,252 | 717.2s / 1,580,789 msg/s |
| Dekaf | 2026-07-26T03:48:26.85523+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1744.9 MB/s | 4/4 | 2,081,649 | 735.2s / 1,515,727 msg/s |
| Dekaf | 2026-07-26T03:48:45.8570361+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1744.9 MB/s | 4/4 | 2,132,958 | 754.2s / 1,345,087 msg/s |
| Dekaf | 2026-07-26T03:49:03.8627027+00:00 | 1 | 11.0 MiB / 8.7 MiB | 1744.9 MB/s | 4/5 | 2,182,520 | 772.2s / 1,597,063 msg/s |
| Dekaf | 2026-07-26T03:49:21.8699119+00:00 | 1 | 11.0 MiB / 10.6 MiB | 1744.9 MB/s | 4/5 | 2,238,344 | 790.2s / 1,563,183 msg/s |
| Dekaf | 2026-07-26T03:49:39.8792565+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1744.9 MB/s | 4/5 | 2,293,794 | 808.2s / 1,552,890 msg/s |
| Dekaf | 2026-07-26T03:49:57.8856605+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1744.9 MB/s | 4/5 | 2,350,766 | 826.2s / 1,583,910 msg/s |
| Dekaf | 2026-07-26T03:50:15.8917336+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1744.9 MB/s | 5/5 | 2,407,211 | 844.2s / 1,575,394 msg/s |
| Dekaf | 2026-07-26T03:50:34.8960883+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1747.8 MB/s | 5/5 | 2,467,891 | 863.2s / 1,574,230 msg/s |
| Dekaf | 2026-07-26T03:50:52.9063436+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1747.8 MB/s | 5/5 | 2,522,756 | 881.2s / 1,552,007 msg/s |
| Dekaf | 2026-07-26T03:51:10.9140107+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1747.8 MB/s | 6/5 | 2,574,653 | 899.2s / 1,590,948 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-26T03:21:40.9020908+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.2 MiB |
| Dekaf | 2026-07-26T03:21:55.9187997+00:00 | 1 | capacity | failed | 15,016ms | 16.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-26T03:22:55.9816339+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-26T03:23:11.0057929+00:00 | 1 | capacity | succeeded | 15,024ms | 18.0 MiB / 16.4 MiB |
| Dekaf | 2026-07-26T03:23:41.0508981+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-26T03:23:56.0715825+00:00 | 1 | capacity | failed | 15,020ms | 18.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-26T03:24:56.1549372+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 18.0 MiB |
| Dekaf | 2026-07-26T03:25:11.173813+00:00 | 1 | capacity | failed | 15,019ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-26T03:27:11.3471647+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-26T03:27:26.3652809+00:00 | 1 | capacity | failed | 15,018ms | 18.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-26T03:31:26.725747+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 17.6 MiB |
| Dekaf | 2026-07-26T03:31:41.7482347+00:00 | 1 | capacity | succeeded | 15,022ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-26T03:32:11.7748906+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.3 MiB |
| Dekaf | 2026-07-26T03:32:26.7910193+00:00 | 1 | capacity | succeeded | 15,016ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:32:56.8134132+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:33:11.8249196+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-26T03:34:11.874606+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:34:26.8854829+00:00 | 1 | capacity | failed | 15,010ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:36:41.7298967+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.4 MiB |
| Dekaf | 2026-07-26T03:36:56.7449727+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-26T03:37:26.7760564+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:37:41.79145+00:00 | 1 | capacity | succeeded | 15,015ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:38:11.814442+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:38:26.8279287+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 9.9 MiB |
| Dekaf | 2026-07-26T03:39:26.8795655+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 7.5 MiB |
| Dekaf | 2026-07-26T03:39:41.892423+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-26T03:41:42.0040201+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:41:57.0196325+00:00 | 1 | capacity | failed | 15,015ms | 12.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-26T03:45:57.2124364+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:46:12.2254023+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:46:42.252079+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:46:57.2653056+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 13.8 MiB |
| Dekaf | 2026-07-26T03:47:57.3165961+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-26T03:48:12.3278469+00:00 | 1 | capacity | succeeded | 15,011ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:48:42.3557994+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:48:57.3697533+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-26T03:49:57.4165577+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:50:12.4311183+00:00 | 1 | capacity | succeeded | 15,014ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:50:42.4534825+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.4 MiB |
| Dekaf | 2026-07-26T03:50:57.4728681+00:00 | 1 | capacity | succeeded | 15,019ms | 13.0 MiB / 11.1 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,618 |
| Dekaf | 1 | 0.002–0.004ms | 2,126 |
| Dekaf | 1 | 0.004–0.008ms | 8,060 |
| Dekaf | 1 | 0.008–0.016ms | 33,795 |
| Dekaf | 1 | 0.016–0.032ms | 39,635 |
| Dekaf | 1 | 0.032–0.064ms | 44,995 |
| Dekaf | 1 | 0.064–0.128ms | 92,505 |
| Dekaf | 1 | 0.128–0.256ms | 272,836 |
| Dekaf | 1 | 0.256–0.512ms | 389,580 |
| Dekaf | 1 | 0.512–1.024ms | 84,486 |
| Dekaf | 1 | 1.024–2.048ms | 16,170 |
| Dekaf | 1 | 2.048–4.096ms | 4,369 |
| Dekaf | 1 | 4.096–8.192ms | 881 |
| Dekaf | 1 | 8.192–16.384ms | 48 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,079 |
| Dekaf | 1 | 0.002–0.004ms | 1,288 |
| Dekaf | 1 | 0.004–0.008ms | 4,439 |
| Dekaf | 1 | 0.008–0.016ms | 20,299 |
| Dekaf | 1 | 0.016–0.032ms | 27,031 |
| Dekaf | 1 | 0.032–0.064ms | 26,089 |
| Dekaf | 1 | 0.064–0.128ms | 46,915 |
| Dekaf | 1 | 0.128–0.256ms | 123,160 |
| Dekaf | 1 | 0.256–0.512ms | 155,618 |
| Dekaf | 1 | 0.512–1.024ms | 93,111 |
| Dekaf | 1 | 1.024–2.048ms | 31,141 |
| Dekaf | 1 | 2.048–4.096ms | 4,050 |
| Dekaf | 1 | 4.096–8.192ms | 1,083 |
| Dekaf | 1 | 8.192–16.384ms | 145 |
| Dekaf | 1 | 16.384–32.768ms | 2 |

## Delivery Latency Outliers - Producer (Acks All)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 572,978,000 | 2026-07-26T03:58:17.3917432+00:00 | 110.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 572,987,000 | 2026-07-26T03:58:17.3980223+00:00 | 106.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 572,997,000 | 2026-07-26T03:58:17.4038874+00:00 | 106.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 572,998,000 | 2026-07-26T03:58:17.404461+00:00 | 105.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,001,000 | 2026-07-26T03:58:17.4064646+00:00 | 108.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,007,000 | 2026-07-26T03:58:17.4105044+00:00 | 108.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,011,000 | 2026-07-26T03:58:17.4132243+00:00 | 112.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,017,000 | 2026-07-26T03:58:17.4171692+00:00 | 112.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,028,000 | 2026-07-26T03:58:17.4244482+00:00 | 110.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,031,000 | 2026-07-26T03:58:17.4261935+00:00 | 112.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,033,000 | 2026-07-26T03:58:17.4270975+00:00 | 100.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,037,000 | 2026-07-26T03:58:17.4291308+00:00 | 113.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,040,000 | 2026-07-26T03:58:17.43063+00:00 | 101.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,041,000 | 2026-07-26T03:58:17.4310664+00:00 | 116.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,043,000 | 2026-07-26T03:58:17.4319732+00:00 | 100.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,047,000 | 2026-07-26T03:58:17.4361037+00:00 | 121.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,051,000 | 2026-07-26T03:58:17.4384405+00:00 | 119.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,057,000 | 2026-07-26T03:58:17.44227+00:00 | 125.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,058,000 | 2026-07-26T03:58:17.4428577+00:00 | 124.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,060,000 | 2026-07-26T03:58:17.4442454+00:00 | 102.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,063,000 | 2026-07-26T03:58:17.4457549+00:00 | 108.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,068,000 | 2026-07-26T03:58:17.4488903+00:00 | 123.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,070,000 | 2026-07-26T03:58:17.4497946+00:00 | 107.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,073,000 | 2026-07-26T03:58:17.4515274+00:00 | 105.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,075,000 | 2026-07-26T03:58:17.4524068+00:00 | 102.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,077,000 | 2026-07-26T03:58:17.4534812+00:00 | 128.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,079,000 | 2026-07-26T03:58:17.4543637+00:00 | 102.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,080,000 | 2026-07-26T03:58:17.4548042+00:00 | 105.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,081,000 | 2026-07-26T03:58:17.455463+00:00 | 130.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,083,000 | 2026-07-26T03:58:17.4563688+00:00 | 108.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,084,000 | 2026-07-26T03:58:17.4567991+00:00 | 103.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,085,000 | 2026-07-26T03:58:17.4572314+00:00 | 103.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,086,000 | 2026-07-26T03:58:17.4578746+00:00 | 102.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,088,000 | 2026-07-26T03:58:17.461374+00:00 | 129.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,089,000 | 2026-07-26T03:58:17.4621083+00:00 | 103.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,090,000 | 2026-07-26T03:58:17.4625626+00:00 | 107.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,091,000 | 2026-07-26T03:58:17.4629934+00:00 | 128.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,094,000 | 2026-07-26T03:58:17.4651199+00:00 | 106.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,095,000 | 2026-07-26T03:58:17.4655645+00:00 | 106.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,096,000 | 2026-07-26T03:58:17.4660197+00:00 | 105.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,097,000 | 2026-07-26T03:58:17.4664608+00:00 | 130.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,098,000 | 2026-07-26T03:58:17.4672982+00:00 | 133.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,099,000 | 2026-07-26T03:58:17.4677499+00:00 | 104.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,102,000 | 2026-07-26T03:58:17.4700768+00:00 | 103.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,103,000 | 2026-07-26T03:58:17.4708172+00:00 | 108.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,105,000 | 2026-07-26T03:58:17.4718976+00:00 | 104.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,107,000 | 2026-07-26T03:58:17.4731071+00:00 | 141.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,108,000 | 2026-07-26T03:58:17.473628+00:00 | 141.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,109,000 | 2026-07-26T03:58:17.4741256+00:00 | 106.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,111,000 | 2026-07-26T03:58:17.4910485+00:00 | 124.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,117,000 | 2026-07-26T03:58:17.4977704+00:00 | 118.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,118,000 | 2026-07-26T03:58:17.4982795+00:00 | 118.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,121,000 | 2026-07-26T03:58:17.5008697+00:00 | 118.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,127,000 | 2026-07-26T03:58:17.5050558+00:00 | 119.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,128,000 | 2026-07-26T03:58:17.5055892+00:00 | 119.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,131,000 | 2026-07-26T03:58:17.5074531+00:00 | 117.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,137,000 | 2026-07-26T03:58:17.5106554+00:00 | 121.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,138,000 | 2026-07-26T03:58:17.5112587+00:00 | 125.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,140,000 | 2026-07-26T03:58:17.51221+00:00 | 103.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,141,000 | 2026-07-26T03:58:17.512675+00:00 | 124.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,146,000 | 2026-07-26T03:58:17.5152799+00:00 | 100.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,148,000 | 2026-07-26T03:58:17.5164114+00:00 | 126.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,150,000 | 2026-07-26T03:58:17.5174845+00:00 | 101.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,151,000 | 2026-07-26T03:58:17.5181259+00:00 | 127.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,158,000 | 2026-07-26T03:58:17.5223115+00:00 | 128.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,163,000 | 2026-07-26T03:58:17.5248203+00:00 | 108.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,165,000 | 2026-07-26T03:58:17.5259398+00:00 | 103.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,167,000 | 2026-07-26T03:58:17.5268692+00:00 | 133.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,168,000 | 2026-07-26T03:58:17.5273224+00:00 | 132.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,169,000 | 2026-07-26T03:58:17.5279746+00:00 | 101.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,171,000 | 2026-07-26T03:58:17.528957+00:00 | 131.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,172,000 | 2026-07-26T03:58:17.5294305+00:00 | 103.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,175,000 | 2026-07-26T03:58:17.5310551+00:00 | 104.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,177,000 | 2026-07-26T03:58:17.5322245+00:00 | 138.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,180,000 | 2026-07-26T03:58:17.5336504+00:00 | 109.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,183,000 | 2026-07-26T03:58:17.5351888+00:00 | 112.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,184,000 | 2026-07-26T03:58:17.5356678+00:00 | 107.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,185,000 | 2026-07-26T03:58:17.5361485+00:00 | 108.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,186,000 | 2026-07-26T03:58:17.5367764+00:00 | 108.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,187,000 | 2026-07-26T03:58:17.5372563+00:00 | 135.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,188,000 | 2026-07-26T03:58:17.5377284+00:00 | 135.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,189,000 | 2026-07-26T03:58:17.5382122+00:00 | 111.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,190,000 | 2026-07-26T03:58:17.5387698+00:00 | 114.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,191,000 | 2026-07-26T03:58:17.5393847+00:00 | 135.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,192,000 | 2026-07-26T03:58:17.5398513+00:00 | 107.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,194,000 | 2026-07-26T03:58:17.540759+00:00 | 117.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,195,000 | 2026-07-26T03:58:17.5414158+00:00 | 116.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,196,000 | 2026-07-26T03:58:17.5418754+00:00 | 116.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,198,000 | 2026-07-26T03:58:17.5429346+00:00 | 139.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,199,000 | 2026-07-26T03:58:17.5433929+00:00 | 114.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,201,000 | 2026-07-26T03:58:17.5449892+00:00 | 138.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,202,000 | 2026-07-26T03:58:17.5455746+00:00 | 112.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,203,000 | 2026-07-26T03:58:17.546129+00:00 | 116.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,204,000 | 2026-07-26T03:58:17.5470126+00:00 | 111.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,205,000 | 2026-07-26T03:58:17.5478835+00:00 | 112.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,209,000 | 2026-07-26T03:58:17.5500225+00:00 | 120.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,211,000 | 2026-07-26T03:58:17.5512301+00:00 | 140.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,213,000 | 2026-07-26T03:58:17.5523385+00:00 | 119.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,214,000 | 2026-07-26T03:58:17.5528797+00:00 | 118.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,215,000 | 2026-07-26T03:58:17.5535818+00:00 | 117.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,216,000 | 2026-07-26T03:58:17.5545992+00:00 | 116.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,220,000 | 2026-07-26T03:58:17.5582408+00:00 | 116.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,221,000 | 2026-07-26T03:58:17.5588258+00:00 | 141.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,222,000 | 2026-07-26T03:58:17.5595159+00:00 | 112.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,223,000 | 2026-07-26T03:58:17.5603897+00:00 | 118.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,225,000 | 2026-07-26T03:58:17.561609+00:00 | 113.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,226,000 | 2026-07-26T03:58:17.5621277+00:00 | 112.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,227,000 | 2026-07-26T03:58:17.562936+00:00 | 140.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,232,000 | 2026-07-26T03:58:17.5662338+00:00 | 112.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,234,000 | 2026-07-26T03:58:17.567256+00:00 | 111.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,235,000 | 2026-07-26T03:58:17.5681093+00:00 | 111.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,236,000 | 2026-07-26T03:58:17.5688239+00:00 | 110.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,239,000 | 2026-07-26T03:58:17.5705375+00:00 | 113.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,240,000 | 2026-07-26T03:58:17.5710603+00:00 | 127.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,242,000 | 2026-07-26T03:58:17.5725977+00:00 | 118.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,243,000 | 2026-07-26T03:58:17.5732492+00:00 | 124.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,244,000 | 2026-07-26T03:58:17.5737629+00:00 | 117.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,245,000 | 2026-07-26T03:58:17.574367+00:00 | 117.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,246,000 | 2026-07-26T03:58:17.5748472+00:00 | 116.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,247,000 | 2026-07-26T03:58:17.5753207+00:00 | 145.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,249,000 | 2026-07-26T03:58:17.5765184+00:00 | 121.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,251,000 | 2026-07-26T03:58:17.5776046+00:00 | 143.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,252,000 | 2026-07-26T03:58:17.5782843+00:00 | 120.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,254,000 | 2026-07-26T03:58:17.5792128+00:00 | 120.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,255,000 | 2026-07-26T03:58:17.5797414+00:00 | 119.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,256,000 | 2026-07-26T03:58:17.5803536+00:00 | 119.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,257,000 | 2026-07-26T03:58:17.580927+00:00 | 142.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,259,000 | 2026-07-26T03:58:17.5818103+00:00 | 117.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,260,000 | 2026-07-26T03:58:17.5826496+00:00 | 121.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,261,000 | 2026-07-26T03:58:17.5831003+00:00 | 144.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,262,000 | 2026-07-26T03:58:17.5835332+00:00 | 120.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,263,000 | 2026-07-26T03:58:17.5839822+00:00 | 131.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,264,000 | 2026-07-26T03:58:17.5844149+00:00 | 118.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,267,000 | 2026-07-26T03:58:17.5859309+00:00 | 152.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,269,000 | 2026-07-26T03:58:17.5871958+00:00 | 118.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,270,000 | 2026-07-26T03:58:17.5876369+00:00 | 128.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,272,000 | 2026-07-26T03:58:17.5885965+00:00 | 127.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,273,000 | 2026-07-26T03:58:17.5890542+00:00 | 126.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,274,000 | 2026-07-26T03:58:17.5896891+00:00 | 125.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,276,000 | 2026-07-26T03:58:17.590566+00:00 | 124.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,277,000 | 2026-07-26T03:58:17.5911963+00:00 | 151.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,278,000 | 2026-07-26T03:58:17.5918465+00:00 | 151.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,281,000 | 2026-07-26T03:58:17.5932984+00:00 | 150.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,284,000 | 2026-07-26T03:58:17.5949414+00:00 | 121.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,285,000 | 2026-07-26T03:58:17.5954332+00:00 | 125.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,286,000 | 2026-07-26T03:58:17.5959807+00:00 | 124.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,291,000 | 2026-07-26T03:58:17.5986289+00:00 | 156.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,294,000 | 2026-07-26T03:58:17.600262+00:00 | 121.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,296,000 | 2026-07-26T03:58:17.6013761+00:00 | 125.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,297,000 | 2026-07-26T03:58:17.6021014+00:00 | 153.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,298,000 | 2026-07-26T03:58:17.6026458+00:00 | 153.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,299,000 | 2026-07-26T03:58:17.6031954+00:00 | 123.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,301,000 | 2026-07-26T03:58:17.6046639+00:00 | 153.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,304,000 | 2026-07-26T03:58:17.6066895+00:00 | 131.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,305,000 | 2026-07-26T03:58:17.6072336+00:00 | 130.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,309,000 | 2026-07-26T03:58:17.6104449+00:00 | 128.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,310,000 | 2026-07-26T03:58:17.6109896+00:00 | 132.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,313,000 | 2026-07-26T03:58:17.6176959+00:00 | 125.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,314,000 | 2026-07-26T03:58:17.61829+00:00 | 124.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,315,000 | 2026-07-26T03:58:17.6194847+00:00 | 123.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,316,000 | 2026-07-26T03:58:17.6202101+00:00 | 122.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,317,000 | 2026-07-26T03:58:17.620786+00:00 | 151.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,319,000 | 2026-07-26T03:58:17.6223269+00:00 | 120.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,320,000 | 2026-07-26T03:58:17.6228371+00:00 | 131.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,323,000 | 2026-07-26T03:58:17.6249589+00:00 | 130.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,325,000 | 2026-07-26T03:58:17.6259836+00:00 | 118.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,328,000 | 2026-07-26T03:58:17.6281437+00:00 | 151.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,331,000 | 2026-07-26T03:58:17.6300502+00:00 | 149.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,333,000 | 2026-07-26T03:58:17.6311076+00:00 | 125.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,334,000 | 2026-07-26T03:58:17.6316293+00:00 | 123.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,336,000 | 2026-07-26T03:58:17.633286+00:00 | 122.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,337,000 | 2026-07-26T03:58:17.6338095+00:00 | 151.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,340,000 | 2026-07-26T03:58:17.6357006+00:00 | 129.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,342,000 | 2026-07-26T03:58:17.6369654+00:00 | 126.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,343,000 | 2026-07-26T03:58:17.6377396+00:00 | 127.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,345,000 | 2026-07-26T03:58:17.6388025+00:00 | 125.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,346,000 | 2026-07-26T03:58:17.639405+00:00 | 125.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,347,000 | 2026-07-26T03:58:17.6401705+00:00 | 153.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,348,000 | 2026-07-26T03:58:17.6406671+00:00 | 153.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,349,000 | 2026-07-26T03:58:17.6413311+00:00 | 123.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,351,000 | 2026-07-26T03:58:17.642537+00:00 | 154.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,354,000 | 2026-07-26T03:58:17.6445171+00:00 | 121.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,358,000 | 2026-07-26T03:58:17.6470371+00:00 | 151.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,359,000 | 2026-07-26T03:58:17.647682+00:00 | 124.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,360,000 | 2026-07-26T03:58:17.6484988+00:00 | 130.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,361,000 | 2026-07-26T03:58:17.6489849+00:00 | 149.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,362,000 | 2026-07-26T03:58:17.6494705+00:00 | 129.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,363,000 | 2026-07-26T03:58:17.6500034+00:00 | 132.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,364,000 | 2026-07-26T03:58:17.6508516+00:00 | 123.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,369,000 | 2026-07-26T03:58:17.6539653+00:00 | 125.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,371,000 | 2026-07-26T03:58:17.6549272+00:00 | 151.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,372,000 | 2026-07-26T03:58:17.6554054+00:00 | 127.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,373,000 | 2026-07-26T03:58:17.658958+00:00 | 127.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,375,000 | 2026-07-26T03:58:17.6605659+00:00 | 122.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,376,000 | 2026-07-26T03:58:17.6611584+00:00 | 122.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,377,000 | 2026-07-26T03:58:17.6623531+00:00 | 147.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,378,000 | 2026-07-26T03:58:17.6629188+00:00 | 147.1ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,379,000 | 2026-07-26T03:58:17.6634765+00:00 | 123.4ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,380,000 | 2026-07-26T03:58:17.6640373+00:00 | 129.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,382,000 | 2026-07-26T03:58:17.665367+00:00 | 128.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,383,000 | 2026-07-26T03:58:17.6658637+00:00 | 128.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,384,000 | 2026-07-26T03:58:17.6663622+00:00 | 120.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,387,000 | 2026-07-26T03:58:17.6679873+00:00 | 149.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,389,000 | 2026-07-26T03:58:17.6689519+00:00 | 125.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,390,000 | 2026-07-26T03:58:17.6694282+00:00 | 127.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,394,000 | 2026-07-26T03:58:17.6758886+00:00 | 118.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,395,000 | 2026-07-26T03:58:17.676413+00:00 | 118.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,398,000 | 2026-07-26T03:58:17.677852+00:00 | 147.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,399,000 | 2026-07-26T03:58:17.6791408+00:00 | 118.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,400,000 | 2026-07-26T03:58:17.6802181+00:00 | 126.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,401,000 | 2026-07-26T03:58:17.6808452+00:00 | 144.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,403,000 | 2026-07-26T03:58:17.6847664+00:00 | 121.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,404,000 | 2026-07-26T03:58:17.6853577+00:00 | 115.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,405,000 | 2026-07-26T03:58:17.6859357+00:00 | 115.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,406,000 | 2026-07-26T03:58:17.6865216+00:00 | 115.3ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,407,000 | 2026-07-26T03:58:17.6921536+00:00 | 136.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,408,000 | 2026-07-26T03:58:17.6927923+00:00 | 135.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,410,000 | 2026-07-26T03:58:17.7003425+00:00 | 110.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,411,000 | 2026-07-26T03:58:17.7010449+00:00 | 132.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,412,000 | 2026-07-26T03:58:17.7017406+00:00 | 105.8ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,413,000 | 2026-07-26T03:58:17.702426+00:00 | 108.9ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,414,000 | 2026-07-26T03:58:17.7039265+00:00 | 104.7ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,416,000 | 2026-07-26T03:58:17.7060655+00:00 | 103.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,421,000 | 2026-07-26T03:58:17.7169362+00:00 | 124.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,427,000 | 2026-07-26T03:58:17.7236044+00:00 | 120.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,428,000 | 2026-07-26T03:58:17.7261446+00:00 | 118.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,431,000 | 2026-07-26T03:58:17.7285711+00:00 | 115.6ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,438,000 | 2026-07-26T03:58:17.7410758+00:00 | 111.2ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,441,000 | 2026-07-26T03:58:17.7453986+00:00 | 107.0ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 573,451,000 | 2026-07-26T03:58:17.7593693+00:00 | 100.5ms | GC pause | - | - | 426.3s / 1,358,040 msg/s | Gen2 +0 / pause +102.6ms |
| Confluent | 604,308,000 | 2026-07-26T03:58:40.4183835+00:00 | 104.4ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,317,000 | 2026-07-26T03:58:40.4231186+00:00 | 101.0ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,321,000 | 2026-07-26T03:58:40.4270597+00:00 | 102.6ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,327,000 | 2026-07-26T03:58:40.4311034+00:00 | 102.1ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,328,000 | 2026-07-26T03:58:40.431786+00:00 | 101.4ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,337,000 | 2026-07-26T03:58:40.4374314+00:00 | 100.4ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,341,000 | 2026-07-26T03:58:40.4399615+00:00 | 100.9ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,358,000 | 2026-07-26T03:58:40.4507573+00:00 | 101.0ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,371,000 | 2026-07-26T03:58:40.4608592+00:00 | 100.4ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,381,000 | 2026-07-26T03:58:40.4661658+00:00 | 101.5ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 604,387,000 | 2026-07-26T03:58:40.4708065+00:00 | 101.8ms | GC pause | - | - | 449.3s / 1,392,775 msg/s | Gen2 +0 / pause +98.2ms |
| Confluent | 979,937,000 | 2026-07-26T04:03:14.0680546+00:00 | 100.9ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 979,981,000 | 2026-07-26T04:03:14.0894852+00:00 | 102.5ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 979,991,000 | 2026-07-26T04:03:14.0949285+00:00 | 100.5ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 980,001,000 | 2026-07-26T04:03:14.1032857+00:00 | 111.1ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 980,008,000 | 2026-07-26T04:03:14.1167276+00:00 | 100.9ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 980,057,000 | 2026-07-26T04:03:14.156124+00:00 | 104.7ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 980,058,000 | 2026-07-26T04:03:14.1568631+00:00 | 103.9ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 980,091,000 | 2026-07-26T04:03:14.1850091+00:00 | 101.9ms | GC pause | - | - | 722.5s / 1,437,348 msg/s | Gen2 +0 / pause +100.6ms |
| Confluent | 997,111,000 | 2026-07-26T04:03:26.4995013+00:00 | 103.1ms | GC pause | - | - | 735.5s / 1,429,386 msg/s | Gen2 +0 / pause +50.0ms |
| Confluent | 997,117,000 | 2026-07-26T04:03:26.5031786+00:00 | 100.7ms | GC pause | - | - | 735.5s / 1,429,386 msg/s | Gen2 +0 / pause +50.0ms |
| Confluent | 1,220,821,000 | 2026-07-26T04:06:07.4136718+00:00 | 104.3ms | GC pause | - | - | 896.6s / 1,371,407 msg/s | Gen2 +0 / pause +192.6ms |
| Confluent | 1,220,827,000 | 2026-07-26T04:06:07.4181326+00:00 | 101.2ms | GC pause | - | - | 896.6s / 1,371,407 msg/s | Gen2 +0 / pause +192.6ms |
| Confluent | 1,220,828,000 | 2026-07-26T04:06:07.4186937+00:00 | 100.6ms | GC pause | - | - | 896.6s / 1,371,407 msg/s | Gen2 +0 / pause +192.6ms |
| Confluent | 1,220,917,000 | 2026-07-26T04:06:07.4843506+00:00 | 102.0ms | GC pause | - | - | 896.6s / 1,371,407 msg/s | Gen2 +0 / pause +91.7ms |
| Confluent | 1,220,918,000 | 2026-07-26T04:06:07.4847965+00:00 | 101.6ms | GC pause | - | - | 896.6s / 1,371,407 msg/s | Gen2 +0 / pause +91.7ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*156 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.40x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.16x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.14 | 1154.02 | 1,081,316 | 1,091,613 | -1.9% | -0.15% | 1031.22 | 1,081,316 | 0 | 1.24 |
| Confluent | 1.71 | - | 894,055 | 897,263 | +1.5% | +0.20% | 852.64 | 894,055 | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 320,523 | 356.13 | 1000.55 KB |
| Dekaf | 2 | 319,027 | 354.47 | 995.54 KB |
| Dekaf | 3 | 324,768 | 360.85 | 1013.39 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:17.9090778+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 769,975 msg/s |
| Dekaf | 2026-07-26T03:21:26.9114976+00:00 | 3 | 16.0 MiB / 16.0 MiB | 399.2 MB/s | 0/0 | 1,824 | 9.0s / 1,095,958 msg/s |
| Dekaf | 2026-07-26T03:21:35.9193566+00:00 | 3 | 16.0 MiB / 11.2 MiB | 424.2 MB/s | 0/0 | 6,007 | 18.0s / 1,047,109 msg/s |
| Dekaf | 2026-07-26T03:21:45.9257267+00:00 | 1 | 16.0 MiB / 11.5 MiB | 408.2 MB/s | 0/0 | 4,167 | 28.0s / 1,068,410 msg/s |
| Dekaf | 2026-07-26T03:21:54.930364+00:00 | 1 | 16.0 MiB / 1.5 MiB | 408.2 MB/s | 0/0 | 4,631 | 37.0s / 1,000,001 msg/s |
| Dekaf | 2026-07-26T03:22:03.9303864+00:00 | 1 | 14.0 MiB / 6.6 MiB | 408.2 MB/s | 1/0 | 4,810 | 46.0s / 1,047,065 msg/s |
| Dekaf | 2026-07-26T03:22:12.9370588+00:00 | 1 | 14.0 MiB / 3.4 MiB | 408.2 MB/s | 1/0 | 5,548 | 55.0s / 1,103,638 msg/s |
| Dekaf | 2026-07-26T03:22:21.9441893+00:00 | 2 | 12.0 MiB / 11.7 MiB | 393.9 MB/s | 2/0 | 1,667 | 64.1s / 1,074,094 msg/s |
| Dekaf | 2026-07-26T03:22:30.9495537+00:00 | 2 | 12.0 MiB / 7.0 MiB | 407.6 MB/s | 2/0 | 2,382 | 73.1s / 1,111,685 msg/s |
| Dekaf | 2026-07-26T03:22:39.9521843+00:00 | 2 | 10.0 MiB / 5.3 MiB | 407.6 MB/s | 3/0 | 3,214 | 82.1s / 1,077,673 msg/s |
| Dekaf | 2026-07-26T03:22:48.957026+00:00 | 2 | 10.0 MiB / 6.5 MiB | 407.6 MB/s | 3/0 | 5,529 | 91.1s / 1,105,261 msg/s |
| Dekaf | 2026-07-26T03:22:57.9605553+00:00 | 3 | 10.0 MiB / 8.8 MiB | 428.8 MB/s | 3/1 | 53,961 | 100.1s / 1,109,402 msg/s |
| Dekaf | 2026-07-26T03:23:06.9645791+00:00 | 3 | 10.0 MiB / 10.0 MiB | 428.8 MB/s | 3/1 | 60,381 | 109.1s / 1,099,693 msg/s |
| Dekaf | 2026-07-26T03:23:15.9702259+00:00 | 3 | 10.0 MiB / 9.8 MiB | 428.8 MB/s | 3/1 | 66,005 | 118.1s / 1,063,516 msg/s |
| Dekaf | 2026-07-26T03:23:24.9743785+00:00 | 3 | 10.0 MiB / 10.0 MiB | 428.8 MB/s | 3/1 | 70,454 | 127.1s / 1,115,635 msg/s |
| Dekaf | 2026-07-26T03:23:34.9845093+00:00 | 1 | 10.0 MiB / 8.7 MiB | 408.2 MB/s | 3/1 | 18,876 | 137.1s / 1,143,058 msg/s |
| Dekaf | 2026-07-26T03:23:43.9887715+00:00 | 1 | 10.0 MiB / 4.3 MiB | 408.2 MB/s | 3/1 | 19,793 | 146.1s / 1,063,409 msg/s |
| Dekaf | 2026-07-26T03:23:52.9963152+00:00 | 1 | 10.0 MiB / 6.7 MiB | 408.2 MB/s | 3/1 | 21,044 | 155.1s / 1,057,442 msg/s |
| Dekaf | 2026-07-26T03:24:01.9986542+00:00 | 1 | 10.0 MiB / 8.4 MiB | 408.2 MB/s | 3/1 | 22,306 | 164.1s / 1,126,754 msg/s |
| Dekaf | 2026-07-26T03:24:11.0022027+00:00 | 2 | 10.0 MiB / 3.6 MiB | 407.6 MB/s | 3/2 | 13,036 | 173.1s / 1,128,245 msg/s |
| Dekaf | 2026-07-26T03:24:20.0072466+00:00 | 2 | 10.0 MiB / 5.6 MiB | 407.6 MB/s | 3/3 | 13,892 | 182.1s / 1,116,422 msg/s |
| Dekaf | 2026-07-26T03:24:29.0122369+00:00 | 2 | 10.0 MiB / 6.1 MiB | 407.6 MB/s | 3/3 | 14,377 | 191.1s / 1,151,868 msg/s |
| Dekaf | 2026-07-26T03:24:38.0174937+00:00 | 2 | 10.0 MiB / 3.5 MiB | 407.6 MB/s | 3/3 | 14,846 | 200.1s / 1,030,367 msg/s |
| Dekaf | 2026-07-26T03:24:47.0190847+00:00 | 3 | 7.0 MiB / 6.7 MiB | 428.8 MB/s | 5/2 | 121,158 | 209.1s / 1,086,324 msg/s |
| Dekaf | 2026-07-26T03:24:56.0203175+00:00 | 3 | 7.0 MiB / 6.2 MiB | 428.8 MB/s | 5/2 | 127,009 | 218.1s / 1,076,278 msg/s |
| Dekaf | 2026-07-26T03:25:05.0216763+00:00 | 3 | 6.0 MiB / 5.5 MiB | 428.8 MB/s | 6/2 | 132,951 | 227.1s / 1,149,693 msg/s |
| Dekaf | 2026-07-26T03:25:14.0260437+00:00 | 3 | 6.0 MiB / 5.5 MiB | 428.8 MB/s | 6/2 | 140,179 | 236.1s / 1,055,915 msg/s |
| Dekaf | 2026-07-26T03:25:24.0288017+00:00 | 1 | 9.0 MiB / 3.5 MiB | 408.2 MB/s | 5/2 | 39,884 | 246.1s / 1,081,413 msg/s |
| Dekaf | 2026-07-26T03:25:33.0299463+00:00 | 1 | 7.0 MiB / 6.6 MiB | 408.2 MB/s | 5/2 | 40,406 | 255.1s / 1,073,827 msg/s |
| Dekaf | 2026-07-26T03:25:42.0306197+00:00 | 1 | 9.0 MiB / 3.3 MiB | 408.2 MB/s | 5/2 | 41,153 | 264.2s / 1,045,876 msg/s |
| Dekaf | 2026-07-26T03:25:51.0316722+00:00 | 1 | 9.0 MiB / 4.0 MiB | 408.2 MB/s | 5/3 | 42,264 | 273.2s / 1,124,849 msg/s |
| Dekaf | 2026-07-26T03:26:00.0333085+00:00 | 2 | 8.0 MiB / 3.7 MiB | 407.6 MB/s | 4/5 | 21,129 | 282.2s / 1,123,407 msg/s |
| Dekaf | 2026-07-26T03:26:09.0356957+00:00 | 2 | 8.0 MiB / 3.6 MiB | 407.6 MB/s | 4/5 | 21,688 | 291.2s / 1,127,340 msg/s |
| Dekaf | 2026-07-26T03:26:18.0414233+00:00 | 2 | 8.0 MiB / 5.7 MiB | 407.6 MB/s | 4/5 | 22,119 | 300.2s / 1,096,126 msg/s |
| Dekaf | 2026-07-26T03:26:27.0425191+00:00 | 2 | 8.0 MiB / 3.9 MiB | 407.6 MB/s | 4/5 | 22,564 | 309.2s / 1,117,250 msg/s |
| Dekaf | 2026-07-26T03:26:36.0473267+00:00 | 2 | 8.0 MiB / 4.8 MiB | 407.6 MB/s | 4/6 | 23,290 | 318.2s / 1,041,554 msg/s |
| Dekaf | 2026-07-26T03:26:45.053055+00:00 | 3 | 5.0 MiB / 1.8 MiB | 428.8 MB/s | 7/3 | 210,126 | 327.2s / 1,071,321 msg/s |
| Dekaf | 2026-07-26T03:26:54.0582331+00:00 | 3 | 6.0 MiB / 5.4 MiB | 428.8 MB/s | 7/3 | 216,673 | 336.2s / 1,133,312 msg/s |
| Dekaf | 2026-07-26T03:27:03.0678302+00:00 | 3 | 6.0 MiB / 3.8 MiB | 428.8 MB/s | 8/3 | 221,711 | 345.2s / 1,058,403 msg/s |
| Dekaf | 2026-07-26T03:27:12.0703113+00:00 | 3 | 6.0 MiB / 4.0 MiB | 428.8 MB/s | 8/3 | 227,502 | 354.2s / 952,836 msg/s |
| Dekaf | 2026-07-26T03:27:22.0704678+00:00 | 1 | 7.0 MiB / 6.9 MiB | 408.2 MB/s | 6/4 | 60,095 | 364.2s / 1,053,657 msg/s |
| Dekaf | 2026-07-26T03:27:31.0727384+00:00 | 1 | 7.0 MiB / 3.4 MiB | 408.2 MB/s | 6/4 | 62,334 | 373.2s / 1,124,024 msg/s |
| Dekaf | 2026-07-26T03:27:40.0792666+00:00 | 1 | 7.0 MiB / 4.7 MiB | 408.2 MB/s | 6/4 | 65,508 | 382.2s / 1,062,066 msg/s |
| Dekaf | 2026-07-26T03:27:49.0792973+00:00 | 1 | 7.0 MiB / 3.4 MiB | 408.2 MB/s | 6/4 | 67,960 | 391.2s / 1,031,725 msg/s |
| Dekaf | 2026-07-26T03:27:58.0814084+00:00 | 2 | 9.0 MiB / 3.1 MiB | 407.6 MB/s | 5/6 | 27,585 | 400.2s / 1,100,633 msg/s |
| Dekaf | 2026-07-26T03:28:07.0834357+00:00 | 2 | 9.0 MiB / 6.2 MiB | 407.6 MB/s | 5/7 | 28,461 | 409.2s / 1,089,783 msg/s |
| Dekaf | 2026-07-26T03:28:16.0863875+00:00 | 2 | 9.0 MiB / 2.1 MiB | 407.6 MB/s | 5/7 | 28,868 | 418.2s / 1,073,832 msg/s |
| Dekaf | 2026-07-26T03:28:25.0920655+00:00 | 2 | 9.0 MiB / 6.7 MiB | 407.6 MB/s | 5/7 | 29,003 | 427.2s / 1,098,960 msg/s |
| Dekaf | 2026-07-26T03:28:34.0946008+00:00 | 3 | 6.0 MiB / 5.9 MiB | 428.8 MB/s | 8/4 | 274,602 | 436.2s / 1,099,806 msg/s |
| Dekaf | 2026-07-26T03:28:43.1011008+00:00 | 3 | 7.0 MiB / 6.0 MiB | 428.8 MB/s | 8/4 | 279,685 | 445.2s / 1,014,190 msg/s |
| Dekaf | 2026-07-26T03:28:52.1033099+00:00 | 3 | 7.0 MiB / 6.7 MiB | 428.8 MB/s | 8/4 | 284,494 | 454.2s / 1,114,721 msg/s |
| Dekaf | 2026-07-26T03:29:01.1123257+00:00 | 3 | 6.0 MiB / 5.4 MiB | 428.8 MB/s | 8/5 | 289,922 | 463.2s / 1,100,384 msg/s |
| Dekaf | 2026-07-26T03:29:11.1186029+00:00 | 1 | 7.0 MiB / 6.3 MiB | 408.2 MB/s | 8/5 | 97,693 | 473.2s / 1,095,744 msg/s |
| Dekaf | 2026-07-26T03:29:20.1208046+00:00 | 1 | 7.0 MiB / 4.9 MiB | 408.2 MB/s | 8/5 | 100,351 | 482.2s / 1,128,325 msg/s |
| Dekaf | 2026-07-26T03:29:29.1241819+00:00 | 1 | 7.0 MiB / 5.9 MiB | 408.2 MB/s | 8/5 | 102,716 | 491.3s / 1,091,992 msg/s |
| Dekaf | 2026-07-26T03:29:38.1256237+00:00 | 1 | 7.0 MiB / 4.7 MiB | 408.2 MB/s | 8/5 | 105,336 | 500.3s / 1,137,019 msg/s |
| Dekaf | 2026-07-26T03:29:47.1265051+00:00 | 2 | 7.0 MiB / 4.1 MiB | 412.6 MB/s | 6/8 | 36,370 | 509.3s / 1,030,823 msg/s |
| Dekaf | 2026-07-26T03:29:56.1296822+00:00 | 2 | 7.0 MiB / 1.0 MiB | 412.6 MB/s | 6/8 | 37,650 | 518.3s / 999,004 msg/s |
| Dekaf | 2026-07-26T03:30:05.1319557+00:00 | 2 | 7.0 MiB / 3.0 MiB | 412.6 MB/s | 6/8 | 38,644 | 527.3s / 1,079,116 msg/s |
| Dekaf | 2026-07-26T03:30:14.1324196+00:00 | 2 | 7.0 MiB / 5.7 MiB | 412.6 MB/s | 6/8 | 39,912 | 536.3s / 1,132,913 msg/s |
| Dekaf | 2026-07-26T03:30:23.136836+00:00 | 3 | 6.0 MiB / 5.8 MiB | 428.8 MB/s | 9/5 | 330,791 | 545.3s / 1,141,873 msg/s |
| Dekaf | 2026-07-26T03:30:32.1414468+00:00 | 3 | 5.0 MiB / 3.2 MiB | 428.8 MB/s | 10/5 | 336,436 | 554.3s / 1,116,077 msg/s |
| Dekaf | 2026-07-26T03:30:41.1433012+00:00 | 3 | 5.0 MiB / 4.7 MiB | 428.8 MB/s | 10/5 | 341,668 | 563.3s / 1,094,132 msg/s |
| Dekaf | 2026-07-26T03:30:50.1461755+00:00 | 3 | 4.0 MiB / 4.0 MiB | 428.8 MB/s | 11/5 | 348,764 | 572.3s / 1,085,864 msg/s |
| Dekaf | 2026-07-26T03:31:00.1464219+00:00 | 1 | 8.0 MiB / 5.1 MiB | 408.2 MB/s | 11/5 | 123,534 | 582.3s / 1,122,272 msg/s |
| Dekaf | 2026-07-26T03:31:09.1477285+00:00 | 1 | 8.0 MiB / 5.6 MiB | 408.2 MB/s | 11/5 | 124,618 | 591.3s / 1,066,615 msg/s |
| Dekaf | 2026-07-26T03:31:18.1478002+00:00 | 1 | 8.0 MiB / 4.3 MiB | 408.2 MB/s | 11/5 | 125,712 | 600.3s / 1,102,038 msg/s |
| Dekaf | 2026-07-26T03:31:27.1488191+00:00 | 1 | 8.0 MiB / 4.1 MiB | 408.2 MB/s | 11/5 | 126,650 | 609.3s / 1,110,633 msg/s |
| Dekaf | 2026-07-26T03:31:36.1533218+00:00 | 1 | 8.0 MiB / 3.4 MiB | 408.2 MB/s | 11/5 | 128,294 | 618.3s / 1,112,800 msg/s |
| Dekaf | 2026-07-26T03:31:45.1580531+00:00 | 2 | 7.0 MiB / 4.4 MiB | 412.6 MB/s | 6/10 | 52,066 | 627.3s / 1,095,320 msg/s |
| Dekaf | 2026-07-26T03:31:54.1600195+00:00 | 2 | 7.0 MiB / 3.5 MiB | 412.6 MB/s | 6/11 | 52,504 | 636.3s / 1,084,065 msg/s |
| Dekaf | 2026-07-26T03:32:03.1603096+00:00 | 2 | 7.0 MiB / 4.6 MiB | 412.6 MB/s | 6/11 | 54,064 | 645.3s / 1,060,707 msg/s |
| Dekaf | 2026-07-26T03:32:12.1615097+00:00 | 2 | 7.0 MiB / 4.1 MiB | 412.6 MB/s | 6/11 | 55,468 | 654.3s / 1,091,703 msg/s |
| Dekaf | 2026-07-26T03:32:21.1624941+00:00 | 3 | 5.0 MiB / 2.6 MiB | 428.8 MB/s | 11/7 | 410,715 | 663.3s / 1,084,873 msg/s |
| Dekaf | 2026-07-26T03:32:30.171314+00:00 | 3 | 5.0 MiB / 1.1 MiB | 428.8 MB/s | 11/7 | 416,852 | 672.3s / 958,759 msg/s |
| Dekaf | 2026-07-26T03:32:39.1786917+00:00 | 3 | 5.0 MiB / 4.4 MiB | 428.8 MB/s | 11/7 | 423,630 | 681.3s / 1,057,018 msg/s |
| Dekaf | 2026-07-26T03:32:48.1821865+00:00 | 3 | 5.0 MiB / 1.5 MiB | 428.8 MB/s | 11/7 | 429,909 | 690.3s / 1,115,092 msg/s |
| Dekaf | 2026-07-26T03:32:58.1837465+00:00 | 1 | 8.0 MiB / 3.2 MiB | 408.2 MB/s | 11/7 | 140,498 | 700.3s / 1,124,031 msg/s |
| Dekaf | 2026-07-26T03:33:07.1874687+00:00 | 1 | 8.0 MiB / 5.2 MiB | 408.2 MB/s | 11/7 | 141,618 | 709.4s / 1,118,103 msg/s |
| Dekaf | 2026-07-26T03:33:16.1920731+00:00 | 1 | 8.0 MiB / 5.6 MiB | 408.2 MB/s | 11/7 | 141,949 | 718.4s / 1,142,241 msg/s |
| Dekaf | 2026-07-26T03:33:25.1950708+00:00 | 1 | 8.0 MiB / 7.6 MiB | 408.2 MB/s | 11/7 | 143,231 | 727.4s / 1,063,273 msg/s |
| Dekaf | 2026-07-26T03:33:34.1987849+00:00 | 2 | 7.0 MiB / 3.7 MiB | 412.6 MB/s | 6/11 | 67,227 | 736.4s / 1,100,860 msg/s |
| Dekaf | 2026-07-26T03:33:43.1995013+00:00 | 2 | 7.0 MiB / 3.0 MiB | 412.6 MB/s | 6/11 | 68,546 | 745.4s / 1,093,658 msg/s |
| Dekaf | 2026-07-26T03:33:52.209361+00:00 | 2 | 7.0 MiB / 4.4 MiB | 412.6 MB/s | 6/11 | 70,689 | 754.4s / 1,061,117 msg/s |
| Dekaf | 2026-07-26T03:34:01.2115614+00:00 | 2 | 7.0 MiB / 3.2 MiB | 412.6 MB/s | 6/11 | 71,859 | 763.4s / 1,054,924 msg/s |
| Dekaf | 2026-07-26T03:34:10.215418+00:00 | 3 | 5.0 MiB / 2.8 MiB | 428.8 MB/s | 11/7 | 489,225 | 772.4s / 1,085,754 msg/s |
| Dekaf | 2026-07-26T03:34:19.2178973+00:00 | 3 | 6.0 MiB / 4.9 MiB | 428.8 MB/s | 11/7 | 495,487 | 781.4s / 932,995 msg/s |
| Dekaf | 2026-07-26T03:34:28.2205621+00:00 | 3 | 5.0 MiB / 3.6 MiB | 428.8 MB/s | 11/7 | 501,720 | 790.4s / 1,020,502 msg/s |
| Dekaf | 2026-07-26T03:34:37.2249348+00:00 | 3 | 6.0 MiB / 3.0 MiB | 428.8 MB/s | 12/7 | 506,012 | 799.4s / 1,111,018 msg/s |
| Dekaf | 2026-07-26T03:34:47.2289558+00:00 | 1 | 8.0 MiB / 4.9 MiB | 408.2 MB/s | 11/7 | 155,149 | 809.4s / 1,076,126 msg/s |
| Dekaf | 2026-07-26T03:34:56.233577+00:00 | 1 | 8.0 MiB / 6.7 MiB | 408.2 MB/s | 11/7 | 156,669 | 818.4s / 965,862 msg/s |
| Dekaf | 2026-07-26T03:35:05.2350404+00:00 | 1 | 8.0 MiB / 1.1 MiB | 408.2 MB/s | 11/7 | 158,252 | 827.4s / 1,032,612 msg/s |
| Dekaf | 2026-07-26T03:35:14.2366223+00:00 | 1 | 8.0 MiB / 3.1 MiB | 408.2 MB/s | 11/8 | 159,196 | 836.4s / 1,039,621 msg/s |
| Dekaf | 2026-07-26T03:35:23.24146+00:00 | 2 | 7.0 MiB / 7.0 MiB | 412.6 MB/s | 6/11 | 85,307 | 845.4s / 1,137,087 msg/s |
| Dekaf | 2026-07-26T03:35:32.2443956+00:00 | 2 | 7.0 MiB / 5.8 MiB | 412.6 MB/s | 6/11 | 87,211 | 854.4s / 929,169 msg/s |
| Dekaf | 2026-07-26T03:35:41.253062+00:00 | 2 | 7.0 MiB / 2.4 MiB | 412.6 MB/s | 6/11 | 89,078 | 863.4s / 1,055,693 msg/s |
| Dekaf | 2026-07-26T03:35:50.258082+00:00 | 2 | 7.0 MiB / 4.0 MiB | 412.6 MB/s | 6/11 | 90,142 | 872.4s / 1,125,835 msg/s |
| Dekaf | 2026-07-26T03:35:59.2631956+00:00 | 3 | 6.0 MiB / 4.2 MiB | 428.8 MB/s | 13/7 | 553,916 | 881.4s / 895,307 msg/s |
| Dekaf | 2026-07-26T03:36:08.2651396+00:00 | 3 | 5.0 MiB / 5.0 MiB | 428.8 MB/s | 14/7 | 559,143 | 890.4s / 938,954 msg/s |
| Dekaf | 2026-07-26T03:36:17.2675762+00:00 | 3 | 5.0 MiB / 5.0 MiB | 428.8 MB/s | 14/7 | 564,632 | 899.4s / 913,471 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-26T03:21:48.0460811+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-26T03:21:48.0493522+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-26T03:21:48.0986552+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:22:03.1085538+00:00 | 1 | capacity | succeeded | 15,059ms | 14.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-26T03:22:03.1659397+00:00 | 3 | capacity | succeeded | 15,067ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:22:06.1076815+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-26T03:22:06.1194508+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:22:06.1711941+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-26T03:22:21.161138+00:00 | 2 | capacity | succeeded | 15,053ms | 12.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-26T03:22:21.2361134+00:00 | 3 | capacity | succeeded | 15,065ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:22:24.1700545+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-26T03:22:24.1967747+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-26T03:22:24.2616464+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-26T03:22:39.2106245+00:00 | 2 | capacity | succeeded | 15,040ms | 10.0 MiB / 7.0 MiB |
| Dekaf | 2026-07-26T03:22:39.303507+00:00 | 1 | capacity | succeeded | 15,106ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:22:42.2241795+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-26T03:22:42.3132031+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-26T03:22:42.346532+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-26T03:22:57.311534+00:00 | 2 | capacity | failed | 15,087ms | 10.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-26T03:22:57.3561847+00:00 | 1 | capacity | failed | 15,042ms | 10.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-26T03:22:57.3984025+00:00 | 3 | capacity | failed | 15,052ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-26T03:23:27.4784613+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-26T03:23:30.4285341+00:00 | 2 | capacity | failed | 3,011ms | 10.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-26T03:23:42.5171885+00:00 | 3 | capacity | succeeded | 15,038ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-26T03:23:45.533182+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-26T03:23:57.5400623+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 8.2 MiB |
| Dekaf | 2026-07-26T03:24:00.5167498+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-26T03:24:12.5884534+00:00 | 1 | capacity | succeeded | 15,047ms | 8.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-26T03:24:15.550641+00:00 | 2 | capacity | failed | 15,033ms | 10.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:24:15.6011044+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-26T03:24:17.1059741+00:00 | 1 | capacity | failed | 1,504ms | 8.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-26T03:24:30.6717447+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-26T03:24:45.650103+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 8.5 MiB |
| Dekaf | 2026-07-26T03:24:47.2083391+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-26T03:24:48.7282327+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:25:00.6921679+00:00 | 2 | capacity | succeeded | 15,042ms | 8.0 MiB / 0.8 MiB |
| Dekaf | 2026-07-26T03:25:02.26286+00:00 | 1 | capacity | succeeded | 15,054ms | 9.0 MiB / 0.0 MiB |
| Dekaf | 2026-07-26T03:25:03.7019809+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-26T03:25:06.7161223+00:00 | 2 | capacity | failed | 3,014ms | 8.0 MiB / 7.6 MiB |
| Dekaf | 2026-07-26T03:25:06.8208263+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:25:21.8881107+00:00 | 3 | capacity | succeeded | 15,067ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:25:24.8958734+00:00 | 3 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:25:32.375292+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-26T03:25:36.854778+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-26T03:25:47.4258153+00:00 | 1 | capacity | failed | 15,050ms | 9.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-26T03:25:51.8956932+00:00 | 2 | capacity | failed | 15,040ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:26:17.5189527+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-26T03:26:21.9805965+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-26T03:26:32.5782922+00:00 | 1 | capacity | succeeded | 15,059ms | 7.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-26T03:26:34.52726+00:00 | 2 | capacity | failed | 12,546ms | 8.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-26T03:26:40.1314287+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-26T03:26:50.6276033+00:00 | 1 | capacity | failed | 15,039ms | 7.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-26T03:26:55.1754423+00:00 | 3 | capacity | succeeded | 15,044ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:27:04.6496585+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-26T03:27:19.6972096+00:00 | 2 | capacity | succeeded | 15,047ms | 9.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-26T03:27:25.3204647+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:27:49.8244675+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-26T03:27:50.85783+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.8 MiB |
| Dekaf | 2026-07-26T03:28:04.8779406+00:00 | 2 | capacity | failed | 15,053ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-26T03:28:05.9017103+00:00 | 1 | capacity | failed | 15,043ms | 7.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-26T03:28:35.980786+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-26T03:28:40.5623907+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-26T03:28:54.0333875+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-26T03:28:55.6149194+00:00 | 3 | capacity | failed | 15,052ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:29:05.0669902+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:29:09.0762801+00:00 | 1 | capacity | succeeded | 15,042ms | 7.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-26T03:29:20.1101786+00:00 | 2 | capacity | succeeded | 15,043ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:29:25.6904271+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:29:38.1656934+00:00 | 2 | capacity | failed | 15,043ms | 7.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-26T03:29:39.1676345+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:29:40.7361962+00:00 | 3 | capacity | succeeded | 15,045ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:29:54.2162472+00:00 | 1 | capacity | succeeded | 15,048ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:29:57.2276814+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:30:10.8799741+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-26T03:30:12.3112758+00:00 | 1 | capacity | succeeded | 15,083ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:30:23.3574357+00:00 | 2 | capacity | failed | 15,074ms | 7.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:30:25.9243617+00:00 | 3 | capacity | succeeded | 15,044ms | 6.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-26T03:30:28.9320099+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-26T03:30:42.4195229+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:30:46.9798145+00:00 | 3 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:30:53.4423152+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-26T03:30:57.4683214+00:00 | 1 | capacity | succeeded | 15,048ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-26T03:31:02.0250768+00:00 | 3 | capacity | failed | 15,045ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-26T03:31:08.4861427+00:00 | 2 | capacity | failed | 15,043ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:31:27.5655168+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-26T03:31:42.6213177+00:00 | 1 | capacity | failed | 15,055ms | 8.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-26T03:31:53.6282556+00:00 | 2 | capacity | failed | 15,039ms | 7.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-26T03:32:02.1906967+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-26T03:32:17.233914+00:00 | 3 | capacity | failed | 15,043ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-26T03:32:42.854589+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-26T03:32:57.9048235+00:00 | 1 | capacity | failed | 15,050ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-26T03:34:32.6851872+00:00 | 3 | capacity | succeeded | 15,047ms | 6.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-26T03:34:58.3710606+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-26T03:35:02.8210444+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-26T03:35:13.4133348+00:00 | 1 | capacity | failed | 15,042ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-07-26T03:35:17.8698438+00:00 | 3 | capacity | succeeded | 15,048ms | 7.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-26T03:35:47.992862+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:36:03.0579256+00:00 | 3 | capacity | succeeded | 15,065ms | 6.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-26T03:36:06.0643279+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-26T03:36:09.5339193+00:00 | 2 | capacity | failed | 15,042ms | 7.0 MiB / 3.4 MiB |
*17 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 43 |
| Dekaf | 1 | 0.002–0.004ms | 60 |
| Dekaf | 1 | 0.004–0.008ms | 202 |
| Dekaf | 1 | 0.008–0.016ms | 540 |
| Dekaf | 1 | 0.016–0.032ms | 1,055 |
| Dekaf | 1 | 0.032–0.064ms | 1,714 |
| Dekaf | 1 | 0.064–0.128ms | 2,272 |
| Dekaf | 1 | 0.128–0.256ms | 3,718 |
| Dekaf | 1 | 0.256–0.512ms | 7,098 |
| Dekaf | 1 | 0.512–1.024ms | 9,652 |
| Dekaf | 1 | 1.024–2.048ms | 8,617 |
| Dekaf | 1 | 2.048–4.096ms | 4,180 |
| Dekaf | 1 | 4.096–8.192ms | 1,507 |
| Dekaf | 1 | 8.192–16.384ms | 341 |
| Dekaf | 1 | 16.384–32.768ms | 56 |
| Dekaf | 1 | 32.768–65.536ms | 2 |
| Dekaf | 2 | 0.001–0.002ms | 16 |
| Dekaf | 2 | 0.002–0.004ms | 31 |
| Dekaf | 2 | 0.004–0.008ms | 127 |
| Dekaf | 2 | 0.008–0.016ms | 348 |
| Dekaf | 2 | 0.016–0.032ms | 640 |
| Dekaf | 2 | 0.032–0.064ms | 976 |
| Dekaf | 2 | 0.064–0.128ms | 1,223 |
| Dekaf | 2 | 0.128–0.256ms | 2,057 |
| Dekaf | 2 | 0.256–0.512ms | 3,875 |
| Dekaf | 2 | 0.512–1.024ms | 5,243 |
| Dekaf | 2 | 1.024–2.048ms | 4,778 |
| Dekaf | 2 | 2.048–4.096ms | 2,607 |
| Dekaf | 2 | 4.096–8.192ms | 938 |
| Dekaf | 2 | 8.192–16.384ms | 260 |
| Dekaf | 2 | 16.384–32.768ms | 41 |
| Dekaf | 2 | 32.768–65.536ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 109 |
| Dekaf | 3 | 0.002–0.004ms | 138 |
| Dekaf | 3 | 0.004–0.008ms | 562 |
| Dekaf | 3 | 0.008–0.016ms | 1,525 |
| Dekaf | 3 | 0.016–0.032ms | 3,052 |
| Dekaf | 3 | 0.032–0.064ms | 4,998 |
| Dekaf | 3 | 0.064–0.128ms | 6,773 |
| Dekaf | 3 | 0.128–0.256ms | 11,299 |
| Dekaf | 3 | 0.256–0.512ms | 22,910 |
| Dekaf | 3 | 0.512–1.024ms | 31,721 |
| Dekaf | 3 | 1.024–2.048ms | 27,475 |
| Dekaf | 3 | 2.048–4.096ms | 14,304 |
| Dekaf | 3 | 4.096–8.192ms | 5,593 |
| Dekaf | 3 | 8.192–16.384ms | 1,457 |
| Dekaf | 3 | 16.384–32.768ms | 307 |
| Dekaf | 3 | 32.768–65.536ms | 12 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 25,000 | 2026-07-26T03:06:17.7987764+00:00 | 141.6ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 42,000 | 2026-07-26T03:06:17.840973+00:00 | 184.9ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 56,000 | 2026-07-26T03:06:17.8775062+00:00 | 156.9ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 108,000 | 2026-07-26T03:06:18.0441039+00:00 | 584.4ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 111,000 | 2026-07-26T03:06:18.0505944+00:00 | 578.1ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 113,000 | 2026-07-26T03:06:18.0585355+00:00 | 254.9ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 139,000 | 2026-07-26T03:06:18.1339383+00:00 | 169.8ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 167,000 | 2026-07-26T03:06:18.2248081+00:00 | 783.6ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 168,000 | 2026-07-26T03:06:18.22609+00:00 | 782.3ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 170,000 | 2026-07-26T03:06:18.2289387+00:00 | 409.1ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 196,000 | 2026-07-26T03:06:18.2670336+00:00 | 361.3ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 200,000 | 2026-07-26T03:06:18.273211+00:00 | 405.1ms | GC pause | - | - | 1.0s / 508,247 msg/s | Gen2 +0 / pause +101.7ms |
| Confluent | 241,000 | 2026-07-26T03:06:18.3353204+00:00 | 885.6ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 268,000 | 2026-07-26T03:06:18.3774455+00:00 | 874.4ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 386,000 | 2026-07-26T03:06:18.5453029+00:00 | 821.3ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 404,000 | 2026-07-26T03:06:18.5724025+00:00 | 1.0s | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 432,000 | 2026-07-26T03:06:18.6084099+00:00 | 866.1ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 434,000 | 2026-07-26T03:06:18.6107916+00:00 | 1.1s | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 458,000 | 2026-07-26T03:06:18.6435864+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +286.4ms |
| Confluent | 467,000 | 2026-07-26T03:06:18.6552131+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +286.4ms |
| Confluent | 484,000 | 2026-07-26T03:06:18.6751904+00:00 | 1.1s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +286.4ms |
| Confluent | 525,000 | 2026-07-26T03:06:18.7309307+00:00 | 786.4ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +208.9ms |
| Confluent | 533,000 | 2026-07-26T03:06:18.7886677+00:00 | 645.2ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +107.2ms |
| Confluent | 574,000 | 2026-07-26T03:06:18.8464858+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 575,000 | 2026-07-26T03:06:18.8474437+00:00 | 757.7ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +107.2ms |
| Confluent | 589,000 | 2026-07-26T03:06:18.863891+00:00 | 756.2ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +107.2ms |
| Confluent | 599,000 | 2026-07-26T03:06:18.8762587+00:00 | 758.3ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +107.2ms |
| Confluent | 629,000 | 2026-07-26T03:06:18.934216+00:00 | 736.2ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +107.2ms |
| Confluent | 633,000 | 2026-07-26T03:06:18.938336+00:00 | 744.4ms | GC pause | - | - | 2.0s / 558,363 msg/s | Gen2 +0 / pause +107.2ms |
| Confluent | 647,000 | 2026-07-26T03:06:18.9541529+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 709,000 | 2026-07-26T03:06:19.1224632+00:00 | 803.1ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 725,000 | 2026-07-26T03:06:19.1611535+00:00 | 800.7ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 736,000 | 2026-07-26T03:06:19.1689833+00:00 | 803.9ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 775,000 | 2026-07-26T03:06:19.2270765+00:00 | 823.3ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 779,000 | 2026-07-26T03:06:19.2317353+00:00 | 829.0ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 813,000 | 2026-07-26T03:06:19.2724334+00:00 | 809.7ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 819,000 | 2026-07-26T03:06:19.2789435+00:00 | 834.5ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 822,000 | 2026-07-26T03:06:19.3324265+00:00 | 998.7ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 830,000 | 2026-07-26T03:06:19.3410929+00:00 | 786.5ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 878,000 | 2026-07-26T03:06:19.4122657+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 887,000 | 2026-07-26T03:06:19.4222743+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 909,000 | 2026-07-26T03:06:19.447827+00:00 | 857.1ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 921,000 | 2026-07-26T03:06:19.4670992+00:00 | 1.2s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 932,000 | 2026-07-26T03:06:19.4779005+00:00 | 1.0s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 939,000 | 2026-07-26T03:06:19.4855858+00:00 | 852.6ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 1,012,000 | 2026-07-26T03:06:19.5749118+00:00 | 1.1s | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 1,051,000 | 2026-07-26T03:06:19.6515736+00:00 | 1.1s | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +403.6ms |
| Confluent | 1,056,000 | 2026-07-26T03:06:19.6656114+00:00 | 865.9ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +184.6ms |
| Confluent | 1,102,000 | 2026-07-26T03:06:19.8323045+00:00 | 981.4ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,134,000 | 2026-07-26T03:06:19.8961682+00:00 | 1.4s | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,148,000 | 2026-07-26T03:06:19.926196+00:00 | 1.0s | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,153,000 | 2026-07-26T03:06:19.9361114+00:00 | 749.8ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 1,171,000 | 2026-07-26T03:06:19.9742042+00:00 | 1.0s | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,189,000 | 2026-07-26T03:06:20.0162567+00:00 | 724.8ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 1,233,000 | 2026-07-26T03:06:20.1158675+00:00 | 644.5ms | GC pause | - | - | 3.0s / 456,540 msg/s | Gen2 +0 / pause +77.4ms |
| Confluent | 1,271,000 | 2026-07-26T03:06:20.2033475+00:00 | 929.0ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,285,000 | 2026-07-26T03:06:20.2734306+00:00 | 590.9ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,319,000 | 2026-07-26T03:06:20.324154+00:00 | 575.3ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,382,000 | 2026-07-26T03:06:20.4389382+00:00 | 805.1ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,389,000 | 2026-07-26T03:06:20.4775196+00:00 | 486.2ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,391,000 | 2026-07-26T03:06:20.4809846+00:00 | 979.2ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,411,000 | 2026-07-26T03:06:20.5254147+00:00 | 945.6ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,435,000 | 2026-07-26T03:06:20.5712572+00:00 | 429.9ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,437,000 | 2026-07-26T03:06:20.5750547+00:00 | 1.0s | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,471,000 | 2026-07-26T03:06:20.6299913+00:00 | 988.3ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,476,000 | 2026-07-26T03:06:20.6435087+00:00 | 411.5ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +296.4ms |
| Confluent | 1,552,000 | 2026-07-26T03:06:20.79028+00:00 | 635.7ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +219.0ms |
| Confluent | 1,610,000 | 2026-07-26T03:06:20.8574733+00:00 | 386.1ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +219.0ms |
| Confluent | 1,667,000 | 2026-07-26T03:06:21.0661931+00:00 | 983.4ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,670,000 | 2026-07-26T03:06:21.067867+00:00 | 245.5ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +219.0ms |
| Confluent | 1,688,000 | 2026-07-26T03:06:21.097554+00:00 | 968.3ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,704,000 | 2026-07-26T03:06:21.1477615+00:00 | 1.1s | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,707,000 | 2026-07-26T03:06:21.1816932+00:00 | 890.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,713,000 | 2026-07-26T03:06:21.201596+00:00 | 206.6ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +219.0ms |
| Confluent | 1,719,000 | 2026-07-26T03:06:21.2305713+00:00 | 148.2ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +219.0ms |
| Confluent | 1,721,000 | 2026-07-26T03:06:21.2318524+00:00 | 850.6ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,779,000 | 2026-07-26T03:06:21.4253418+00:00 | 123.7ms | GC pause | - | - | 4.0s / 500,540 msg/s | Gen2 +0 / pause +219.0ms |
| Confluent | 1,804,000 | 2026-07-26T03:06:21.4779896+00:00 | 1.0s | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,814,000 | 2026-07-26T03:06:21.4936477+00:00 | 1.0s | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,827,000 | 2026-07-26T03:06:21.5105096+00:00 | 661.2ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,872,000 | 2026-07-26T03:06:21.5947695+00:00 | 368.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,917,000 | 2026-07-26T03:06:21.6848803+00:00 | 657.5ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,937,000 | 2026-07-26T03:06:21.6991276+00:00 | 660.9ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,965,000 | 2026-07-26T03:06:21.7182725+00:00 | 145.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,973,000 | 2026-07-26T03:06:21.724237+00:00 | 125.6ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 1,988,000 | 2026-07-26T03:06:21.7353833+00:00 | 822.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 2,008,000 | 2026-07-26T03:06:21.7511672+00:00 | 845.6ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 2,016,000 | 2026-07-26T03:06:21.757316+00:00 | 149.1ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 2,029,000 | 2026-07-26T03:06:21.7666478+00:00 | 144.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +403.9ms |
| Confluent | 2,044,000 | 2026-07-26T03:06:21.7813223+00:00 | 1.1s | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,065,000 | 2026-07-26T03:06:21.7940645+00:00 | 181.4ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,083,000 | 2026-07-26T03:06:21.8064769+00:00 | 167.0ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,090,000 | 2026-07-26T03:06:21.8183652+00:00 | 156.1ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,126,000 | 2026-07-26T03:06:21.8906529+00:00 | 171.9ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,133,000 | 2026-07-26T03:06:21.9106977+00:00 | 138.1ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,167,000 | 2026-07-26T03:06:22.024164+00:00 | 743.5ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,219,000 | 2026-07-26T03:06:22.1041382+00:00 | 116.2ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,256,000 | 2026-07-26T03:06:22.143687+00:00 | 131.4ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,268,000 | 2026-07-26T03:06:22.1565596+00:00 | 708.3ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,289,000 | 2026-07-26T03:06:22.1868705+00:00 | 170.0ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,309,000 | 2026-07-26T03:06:22.2177895+00:00 | 189.2ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,313,000 | 2026-07-26T03:06:22.2220043+00:00 | 266.0ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,319,000 | 2026-07-26T03:06:22.2346956+00:00 | 189.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,347,000 | 2026-07-26T03:06:22.2687131+00:00 | 671.6ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,351,000 | 2026-07-26T03:06:22.2769674+00:00 | 663.5ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,393,000 | 2026-07-26T03:06:22.3530345+00:00 | 171.7ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,414,000 | 2026-07-26T03:06:22.4158486+00:00 | 878.9ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,417,000 | 2026-07-26T03:06:22.4289198+00:00 | 594.7ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,442,000 | 2026-07-26T03:06:22.5155545+00:00 | 110.1ms | GC pause | - | - | 5.0s / 504,238 msg/s | Gen2 +0 / pause +185.0ms |
| Confluent | 2,458,000 | 2026-07-26T03:06:22.5708201+00:00 | 459.1ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,488,000 | 2026-07-26T03:06:22.6328315+00:00 | 408.8ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,501,000 | 2026-07-26T03:06:22.6509125+00:00 | 399.9ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,511,000 | 2026-07-26T03:06:22.6640636+00:00 | 410.7ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,528,000 | 2026-07-26T03:06:22.6994729+00:00 | 403.3ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +360.9ms |
| Confluent | 2,648,000 | 2026-07-26T03:06:22.9424073+00:00 | 275.6ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +175.9ms |
| Confluent | 2,707,000 | 2026-07-26T03:06:23.0643554+00:00 | 271.1ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +175.9ms |
| Confluent | 2,738,000 | 2026-07-26T03:06:23.1090879+00:00 | 255.2ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +175.9ms |
| Confluent | 2,787,000 | 2026-07-26T03:06:23.2086335+00:00 | 201.8ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +175.9ms |
| Confluent | 2,841,000 | 2026-07-26T03:06:23.2840912+00:00 | 165.4ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +175.9ms |
| Confluent | 2,901,000 | 2026-07-26T03:06:23.4201887+00:00 | 117.0ms | GC pause | - | - | 6.0s / 562,633 msg/s | Gen2 +0 / pause +175.9ms |
| Confluent | 3,452,000 | 2026-07-26T03:06:24.2477853+00:00 | 157.3ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,487,000 | 2026-07-26T03:06:24.2686789+00:00 | 233.2ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,496,000 | 2026-07-26T03:06:24.2767581+00:00 | 238.8ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,501,000 | 2026-07-26T03:06:24.2838005+00:00 | 231.7ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,519,000 | 2026-07-26T03:06:24.3040254+00:00 | 242.4ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,535,000 | 2026-07-26T03:06:24.328739+00:00 | 220.6ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,549,000 | 2026-07-26T03:06:24.3517007+00:00 | 209.6ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,559,000 | 2026-07-26T03:06:24.3620171+00:00 | 209.5ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,564,000 | 2026-07-26T03:06:24.3687228+00:00 | 189.9ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,573,000 | 2026-07-26T03:06:24.389925+00:00 | 216.7ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,579,000 | 2026-07-26T03:06:24.3942597+00:00 | 199.1ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,580,000 | 2026-07-26T03:06:24.3979863+00:00 | 211.7ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,588,000 | 2026-07-26T03:06:24.4127677+00:00 | 191.2ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,597,000 | 2026-07-26T03:06:24.4204506+00:00 | 194.2ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 3,716,000 | 2026-07-26T03:06:24.6616644+00:00 | 116.8ms | GC pause | - | - | 7.0s / 684,418 msg/s | Gen2 +0 / pause +134.1ms |
| Confluent | 4,159,000 | 2026-07-26T03:06:25.2556486+00:00 | 116.4ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,169,000 | 2026-07-26T03:06:25.2606809+00:00 | 266.7ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,172,000 | 2026-07-26T03:06:25.2621724+00:00 | 186.6ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,190,000 | 2026-07-26T03:06:25.2751249+00:00 | 114.5ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,196,000 | 2026-07-26T03:06:25.2796022+00:00 | 255.2ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,201,000 | 2026-07-26T03:06:25.2829218+00:00 | 267.3ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,214,000 | 2026-07-26T03:06:25.2931677+00:00 | 236.4ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,250,000 | 2026-07-26T03:06:25.3244038+00:00 | 219.2ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,263,000 | 2026-07-26T03:06:25.3367486+00:00 | 209.1ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,288,000 | 2026-07-26T03:06:25.3866071+00:00 | 247.2ms | GC pause | - | - | 8.0s / 654,981 msg/s | Gen2 +0 / pause +191.9ms |
| Confluent | 4,478,000 | 2026-07-26T03:06:25.8221206+00:00 | 114.6ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,498,000 | 2026-07-26T03:06:25.843934+00:00 | 126.2ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,511,000 | 2026-07-26T03:06:25.8625079+00:00 | 135.3ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,538,000 | 2026-07-26T03:06:25.8915241+00:00 | 130.7ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,564,000 | 2026-07-26T03:06:25.9127264+00:00 | 101.1ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,794,000 | 2026-07-26T03:06:26.244215+00:00 | 195.7ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,801,000 | 2026-07-26T03:06:26.2509617+00:00 | 140.4ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,808,000 | 2026-07-26T03:06:26.256896+00:00 | 150.7ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,825,000 | 2026-07-26T03:06:26.2747047+00:00 | 104.8ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 4,877,000 | 2026-07-26T03:06:26.4008537+00:00 | 107.1ms | GC pause | - | - | 9.0s / 687,524 msg/s | Gen2 +0 / pause +156.9ms |
| Confluent | 5,132,000 | 2026-07-26T03:06:26.7723315+00:00 | 107.4ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +249.8ms |
| Confluent | 5,149,000 | 2026-07-26T03:06:26.7886113+00:00 | 128.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,150,000 | 2026-07-26T03:06:26.7894255+00:00 | 122.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,159,000 | 2026-07-26T03:06:26.79534+00:00 | 141.9ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,165,000 | 2026-07-26T03:06:26.7988836+00:00 | 163.6ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,191,000 | 2026-07-26T03:06:26.8193916+00:00 | 116.1ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,236,000 | 2026-07-26T03:06:26.8470079+00:00 | 196.7ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,237,000 | 2026-07-26T03:06:26.8474757+00:00 | 130.9ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,238,000 | 2026-07-26T03:06:26.8481667+00:00 | 130.3ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,262,000 | 2026-07-26T03:06:26.8629316+00:00 | 330.4ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,281,000 | 2026-07-26T03:06:26.8789823+00:00 | 150.7ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,292,000 | 2026-07-26T03:06:26.8895381+00:00 | 375.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,298,000 | 2026-07-26T03:06:26.8942082+00:00 | 149.9ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,305,000 | 2026-07-26T03:06:26.9011146+00:00 | 222.9ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,322,000 | 2026-07-26T03:06:26.9229495+00:00 | 548.3ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,342,000 | 2026-07-26T03:06:26.9419531+00:00 | 555.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,345,000 | 2026-07-26T03:06:26.9437781+00:00 | 210.9ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,390,000 | 2026-07-26T03:06:27.0232419+00:00 | 126.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,402,000 | 2026-07-26T03:06:27.0453581+00:00 | 496.2ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,405,000 | 2026-07-26T03:06:27.0477204+00:00 | 165.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,434,000 | 2026-07-26T03:06:27.0901489+00:00 | 416.6ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,441,000 | 2026-07-26T03:06:27.1000709+00:00 | 247.3ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,445,000 | 2026-07-26T03:06:27.1042944+00:00 | 179.6ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,447,000 | 2026-07-26T03:06:27.1086051+00:00 | 240.7ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,470,000 | 2026-07-26T03:06:27.1353559+00:00 | 145.7ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,506,000 | 2026-07-26T03:06:27.1878431+00:00 | 158.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,508,000 | 2026-07-26T03:06:27.1896004+00:00 | 214.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,514,000 | 2026-07-26T03:06:27.1967117+00:00 | 455.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,531,000 | 2026-07-26T03:06:27.2169397+00:00 | 201.3ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,547,000 | 2026-07-26T03:06:27.2433436+00:00 | 207.9ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,550,000 | 2026-07-26T03:06:27.2458304+00:00 | 204.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,557,000 | 2026-07-26T03:06:27.250808+00:00 | 208.3ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,561,000 | 2026-07-26T03:06:27.2554573+00:00 | 203.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,578,000 | 2026-07-26T03:06:27.2794132+00:00 | 201.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,592,000 | 2026-07-26T03:06:27.2954075+00:00 | 489.3ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,598,000 | 2026-07-26T03:06:27.303312+00:00 | 233.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,618,000 | 2026-07-26T03:06:27.3310425+00:00 | 219.5ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,626,000 | 2026-07-26T03:06:27.3400605+00:00 | 248.2ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,629,000 | 2026-07-26T03:06:27.3435284+00:00 | 260.8ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,631,000 | 2026-07-26T03:06:27.3457932+00:00 | 273.2ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,632,000 | 2026-07-26T03:06:27.3476099+00:00 | 495.4ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,672,000 | 2026-07-26T03:06:27.397597+00:00 | 510.6ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,686,000 | 2026-07-26T03:06:27.4158453+00:00 | 310.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,714,000 | 2026-07-26T03:06:27.4720836+00:00 | 479.3ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,720,000 | 2026-07-26T03:06:27.4866917+00:00 | 164.0ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,729,000 | 2026-07-26T03:06:27.4990399+00:00 | 282.7ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,730,000 | 2026-07-26T03:06:27.4997701+00:00 | 153.5ms | GC pause | - | - | 10.0s / 793,948 msg/s | Gen2 +0 / pause +92.9ms |
| Confluent | 5,749,000 | 2026-07-26T03:06:27.5331724+00:00 | 262.7ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,794,000 | 2026-07-26T03:06:27.586441+00:00 | 418.4ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,807,000 | 2026-07-26T03:06:27.6021225+00:00 | 401.1ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,809,000 | 2026-07-26T03:06:27.6063203+00:00 | 273.9ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,833,000 | 2026-07-26T03:06:27.634629+00:00 | 159.8ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,838,000 | 2026-07-26T03:06:27.648262+00:00 | 390.5ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,854,000 | 2026-07-26T03:06:27.6724523+00:00 | 424.8ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,859,000 | 2026-07-26T03:06:27.6767753+00:00 | 236.9ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,868,000 | 2026-07-26T03:06:27.6826981+00:00 | 377.9ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,903,000 | 2026-07-26T03:06:27.7271369+00:00 | 140.6ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,905,000 | 2026-07-26T03:06:27.7291447+00:00 | 244.6ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +242.3ms |
| Confluent | 5,954,000 | 2026-07-26T03:06:27.8533949+00:00 | 296.5ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +149.5ms |
| Confluent | 5,965,000 | 2026-07-26T03:06:27.89835+00:00 | 138.9ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +149.5ms |
| Confluent | 5,978,000 | 2026-07-26T03:06:27.9300999+00:00 | 212.6ms | GC pause | - | - | 11.0s / 560,367 msg/s | Gen2 +0 / pause +149.5ms |
| Confluent | 6,565,000 | 2026-07-26T03:06:28.8476265+00:00 | 116.6ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,598,000 | 2026-07-26T03:06:28.8689899+00:00 | 203.2ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,620,000 | 2026-07-26T03:06:28.8842519+00:00 | 237.1ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,627,000 | 2026-07-26T03:06:28.8922947+00:00 | 218.4ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,665,000 | 2026-07-26T03:06:28.9300585+00:00 | 239.7ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,708,000 | 2026-07-26T03:06:28.9690281+00:00 | 355.2ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,709,000 | 2026-07-26T03:06:28.9696253+00:00 | 322.6ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,718,000 | 2026-07-26T03:06:28.9858416+00:00 | 339.7ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,738,000 | 2026-07-26T03:06:29.0253843+00:00 | 311.3ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,747,000 | 2026-07-26T03:06:29.035045+00:00 | 336.4ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,762,000 | 2026-07-26T03:06:29.0652063+00:00 | 117.3ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,771,000 | 2026-07-26T03:06:29.0827037+00:00 | 329.8ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,801,000 | 2026-07-26T03:06:29.1257476+00:00 | 343.0ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,822,000 | 2026-07-26T03:06:29.1407302+00:00 | 264.8ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,828,000 | 2026-07-26T03:06:29.1440123+00:00 | 423.2ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,833,000 | 2026-07-26T03:06:29.1466756+00:00 | 312.8ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,850,000 | 2026-07-26T03:06:29.1584961+00:00 | 308.7ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,871,000 | 2026-07-26T03:06:29.1788567+00:00 | 418.3ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,910,000 | 2026-07-26T03:06:29.2285425+00:00 | 329.1ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,917,000 | 2026-07-26T03:06:29.2471915+00:00 | 370.4ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,939,000 | 2026-07-26T03:06:29.2959422+00:00 | 288.2ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,969,000 | 2026-07-26T03:06:29.4485363+00:00 | 147.6ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 6,979,000 | 2026-07-26T03:06:29.4780109+00:00 | 123.1ms | GC pause | - | - | 12.0s / 709,200 msg/s | Gen2 +0 / pause +170.8ms |
| Confluent | 7,079,000 | 2026-07-26T03:06:29.6879076+00:00 | 121.9ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,081,000 | 2026-07-26T03:06:29.6891388+00:00 | 260.2ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,105,000 | 2026-07-26T03:06:29.7079457+00:00 | 167.2ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,167,000 | 2026-07-26T03:06:29.7550275+00:00 | 271.8ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,191,000 | 2026-07-26T03:06:29.7716878+00:00 | 293.7ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,192,000 | 2026-07-26T03:06:29.7721851+00:00 | 203.3ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,194,000 | 2026-07-26T03:06:29.7733363+00:00 | 227.4ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +324.2ms |
| Confluent | 7,206,000 | 2026-07-26T03:06:29.7852772+00:00 | 201.2ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,217,000 | 2026-07-26T03:06:29.7925967+00:00 | 328.0ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,236,000 | 2026-07-26T03:06:29.8239003+00:00 | 181.3ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,257,000 | 2026-07-26T03:06:29.8359578+00:00 | 298.5ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,289,000 | 2026-07-26T03:06:29.8637665+00:00 | 235.8ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,291,000 | 2026-07-26T03:06:29.8649204+00:00 | 333.1ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,324,000 | 2026-07-26T03:06:29.9800701+00:00 | 224.7ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,329,000 | 2026-07-26T03:06:29.9926596+00:00 | 118.4ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 7,364,000 | 2026-07-26T03:06:30.0431663+00:00 | 186.7ms | GC pause | - | - | 13.0s / 646,552 msg/s | Gen2 +0 / pause +153.4ms |
| Confluent | 487,739,000 | 2026-07-26T03:15:25.7987634+00:00 | 106.9ms | GC pause | - | - | 548.4s / 942,862 msg/s | Gen2 +0 / pause +100.5ms |
| Dekaf | 18,177,000 | 2026-07-26T03:21:35.2367325+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,047,109 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,539,000 | 2026-07-26T03:21:43.248383+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,033,507 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 37,113,000 | 2026-07-26T03:21:53.2344841+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 36.0s / 989,987 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf | 39,639,000 | 2026-07-26T03:21:55.7290365+00:00 | 101.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 38.0s / 1,033,517 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 60,947,000 | 2026-07-26T03:22:15.7264169+00:00 | 106.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 58.0s / 967,304 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 60,949,000 | 2026-07-26T03:22:15.728177+00:00 | 105.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 58.0s / 967,304 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 60,953,000 | 2026-07-26T03:22:15.7319589+00:00 | 106.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 58.0s / 967,304 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 60,957,000 | 2026-07-26T03:22:15.7346113+00:00 | 111.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 58.0s / 967,304 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 60,959,000 | 2026-07-26T03:22:15.7353253+00:00 | 108.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 58.0s / 967,304 msg/s | Gen2 +0 / pause +0.5ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*4,527 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.49x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.22x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,568,087 | 1,561,354–1,574,849 | 0.97 | 1.09x |
| Confluent | 2 | 1,440,132 | 1,429,525–1,450,817 | 1.24 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 0.99 | 1007.76 | 1,542,332 | 1,574,849 | +0.5% | -0.15% | 1470.88 | 1,542,332 | 0 | 1.52 |
| Dekaf (dekaf-first) | 0.95 | 977.17 | 1,546,129 | 1,561,354 | +1.3% | +0.11% | 1474.50 | 1,546,129 | 0 | 1.47 |
| Confluent (confluent-first) | 1.23 | - | 1,435,476 | 1,450,817 | +3.2% | +0.27% | 1368.98 | 1,435,476 | 0 | 1.76 |
| Dekaf (3conn) | 0.82 | 748.69 | 1,450,581 | 1,446,242 | -4.4% | -0.22% | 1383.38 | 1,450,581 | 0 | 1.19 |
| Confluent (dekaf-first) | 1.24 | - | 1,420,379 | 1,429,525 | -2.6% | -0.23% | 1354.58 | 1,420,379 | 0 | 1.77 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,357,650 | 1508.48 | 1016.37 KB |
| Dekaf | 1 | 1,354,899 | 1505.42 | 1020.93 KB |
| Dekaf (3conn) | 1 | 1,427,610 | 1586.22 | 909.05 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:11.9687227+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 672,019 msg/s |
| Dekaf | 2026-07-26T03:21:38.975578+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1734.9 MB/s | 0/0 | 44,146 | 27.0s / 1,545,862 msg/s |
| Dekaf | 2026-07-26T03:22:06.9891469+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1734.9 MB/s | 1/0 | 92,549 | 55.0s / 1,565,105 msg/s |
| Dekaf | 2026-07-26T03:22:34.0089918+00:00 | 1 | 14.0 MiB / 12.2 MiB | 1734.9 MB/s | 1/0 | 146,760 | 82.0s / 1,569,113 msg/s |
| Dekaf | 2026-07-26T03:23:01.0222364+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1734.9 MB/s | 2/0 | 198,906 | 109.0s / 1,569,506 msg/s |
| Dekaf | 2026-07-26T03:23:28.0261001+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1734.9 MB/s | 2/1 | 246,500 | 136.1s / 1,563,601 msg/s |
| Dekaf | 2026-07-26T03:23:56.0369037+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1734.9 MB/s | 2/1 | 303,579 | 164.1s / 1,562,289 msg/s |
| Dekaf | 2026-07-26T03:24:23.0430515+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1734.9 MB/s | 2/1 | 362,209 | 191.1s / 1,551,734 msg/s |
| Dekaf | 2026-07-26T03:24:50.0512954+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1734.9 MB/s | 3/1 | 420,229 | 218.1s / 1,574,093 msg/s |
| Dekaf | 2026-07-26T03:25:17.0602169+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1734.9 MB/s | 3/1 | 478,573 | 245.1s / 1,587,219 msg/s |
| Dekaf | 2026-07-26T03:25:45.0679997+00:00 | 1 | 14.0 MiB / 12.8 MiB | 1734.9 MB/s | 4/1 | 539,558 | 273.1s / 1,607,808 msg/s |
| Dekaf | 2026-07-26T03:26:12.0844526+00:00 | 1 | 15.0 MiB / 14.1 MiB | 1734.9 MB/s | 4/1 | 587,320 | 300.1s / 1,568,185 msg/s |
| Dekaf | 2026-07-26T03:26:39.0934465+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1734.9 MB/s | 5/1 | 634,109 | 327.1s / 1,574,809 msg/s |
| Dekaf | 2026-07-26T03:27:06.1060316+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1734.9 MB/s | 6/1 | 683,900 | 354.1s / 1,582,652 msg/s |
| Dekaf | 2026-07-26T03:27:34.116003+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1734.9 MB/s | 6/1 | 741,548 | 382.1s / 1,575,416 msg/s |
| Dekaf | 2026-07-26T03:28:01.1309978+00:00 | 1 | 18.0 MiB / 17.3 MiB | 1734.9 MB/s | 7/1 | 789,812 | 409.1s / 1,623,939 msg/s |
| Dekaf | 2026-07-26T03:28:28.1509633+00:00 | 1 | 20.0 MiB / 19.2 MiB | 1748.4 MB/s | 8/1 | 836,643 | 436.2s / 1,616,164 msg/s |
| Dekaf | 2026-07-26T03:28:55.1621185+00:00 | 1 | 20.0 MiB / 16.4 MiB | 1748.4 MB/s | 8/1 | 876,971 | 463.2s / 1,600,729 msg/s |
| Dekaf | 2026-07-26T03:29:23.1693627+00:00 | 1 | 20.0 MiB / 20.0 MiB | 1748.4 MB/s | 8/2 | 920,468 | 491.2s / 1,594,962 msg/s |
| Dekaf | 2026-07-26T03:29:50.1813611+00:00 | 1 | 20.0 MiB / 17.9 MiB | 1748.4 MB/s | 8/2 | 959,817 | 518.2s / 1,490,257 msg/s |
| Dekaf | 2026-07-26T03:30:17.1881261+00:00 | 1 | 17.0 MiB / 17.0 MiB | 1748.4 MB/s | 9/2 | 1,005,053 | 545.2s / 1,549,450 msg/s |
| Dekaf | 2026-07-26T03:30:45.197436+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1748.4 MB/s | 10/2 | 1,035,280 | 573.2s / 1,223,290 msg/s |
| Dekaf | 2026-07-26T03:31:12.2000889+00:00 | 1 | 14.0 MiB / 10.7 MiB | 1748.4 MB/s | 10/2 | 1,041,435 | 600.2s / 1,070,821 msg/s |
| Dekaf | 2026-07-26T03:31:39.2090224+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1748.4 MB/s | 10/3 | 1,082,624 | 627.2s / 1,591,387 msg/s |
| Dekaf | 2026-07-26T03:32:06.2151158+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1748.4 MB/s | 10/3 | 1,132,908 | 654.2s / 1,527,614 msg/s |
| Dekaf | 2026-07-26T03:32:34.2263039+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1748.4 MB/s | 10/3 | 1,185,295 | 682.2s / 1,565,957 msg/s |
| Dekaf | 2026-07-26T03:33:01.2360881+00:00 | 1 | 14.0 MiB / 12.7 MiB | 1748.4 MB/s | 10/4 | 1,236,800 | 709.2s / 1,577,643 msg/s |
| Dekaf | 2026-07-26T03:33:28.2439941+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1748.4 MB/s | 10/4 | 1,285,573 | 736.2s / 1,573,516 msg/s |
| Dekaf | 2026-07-26T03:33:55.2553265+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1748.4 MB/s | 10/4 | 1,338,343 | 763.3s / 1,582,153 msg/s |
| Dekaf | 2026-07-26T03:34:23.2639756+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1748.4 MB/s | 10/4 | 1,394,016 | 791.3s / 1,640,363 msg/s |
| Dekaf | 2026-07-26T03:34:50.2724947+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1748.4 MB/s | 10/4 | 1,448,207 | 818.3s / 1,620,559 msg/s |
| Dekaf | 2026-07-26T03:35:17.2750979+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1748.4 MB/s | 11/4 | 1,506,925 | 845.3s / 1,578,793 msg/s |
| Dekaf | 2026-07-26T03:35:44.2771412+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1748.4 MB/s | 11/5 | 1,562,471 | 872.3s / 1,428,306 msg/s |
| Dekaf | 2026-07-26T03:36:12.8774396+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 449,803 msg/s |
| Dekaf | 2026-07-26T03:36:39.8820761+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1758.3 MB/s | 0/0 | 47,896 | 27.0s / 1,535,068 msg/s |
| Dekaf | 2026-07-26T03:37:06.890347+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1759.7 MB/s | 1/0 | 102,291 | 54.0s / 1,467,889 msg/s |
| Dekaf | 2026-07-26T03:37:33.9046302+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1759.7 MB/s | 1/0 | 169,619 | 81.0s / 1,618,118 msg/s |
| Dekaf | 2026-07-26T03:38:01.9141456+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/0 | 240,857 | 109.0s / 1,521,208 msg/s |
| Dekaf | 2026-07-26T03:38:28.9214046+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/1 | 311,644 | 136.1s / 1,587,093 msg/s |
| Dekaf | 2026-07-26T03:38:55.929496+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1759.7 MB/s | 2/1 | 379,461 | 163.1s / 1,587,281 msg/s |
| Dekaf | 2026-07-26T03:39:23.9374467+00:00 | 1 | 12.0 MiB / 10.8 MiB | 1759.7 MB/s | 2/1 | 445,616 | 191.1s / 1,554,522 msg/s |
| Dekaf | 2026-07-26T03:39:50.946869+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1759.7 MB/s | 2/2 | 513,770 | 218.1s / 1,581,420 msg/s |
| Dekaf | 2026-07-26T03:40:17.9540329+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/2 | 581,042 | 245.1s / 1,538,913 msg/s |
| Dekaf | 2026-07-26T03:40:44.9655724+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/3 | 649,180 | 272.1s / 1,561,222 msg/s |
| Dekaf | 2026-07-26T03:41:12.9775882+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/3 | 716,337 | 300.1s / 1,547,806 msg/s |
| Dekaf | 2026-07-26T03:41:39.9830112+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1759.7 MB/s | 2/3 | 781,709 | 327.1s / 1,564,699 msg/s |
| Dekaf | 2026-07-26T03:42:06.9960094+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/3 | 846,125 | 354.1s / 1,549,581 msg/s |
| Dekaf | 2026-07-26T03:42:33.9992012+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1759.7 MB/s | 2/3 | 913,294 | 381.1s / 1,469,507 msg/s |
| Dekaf | 2026-07-26T03:43:02.0081326+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1759.7 MB/s | 2/3 | 972,178 | 409.1s / 1,482,388 msg/s |
| Dekaf | 2026-07-26T03:43:29.0170206+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1759.7 MB/s | 2/3 | 1,031,695 | 436.1s / 1,487,877 msg/s |
| Dekaf | 2026-07-26T03:43:56.0279125+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1759.7 MB/s | 2/3 | 1,089,957 | 463.2s / 1,467,939 msg/s |
| Dekaf | 2026-07-26T03:44:23.0407587+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1759.7 MB/s | 2/3 | 1,146,758 | 490.2s / 1,451,090 msg/s |
| Dekaf | 2026-07-26T03:44:51.0528854+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1759.7 MB/s | 3/3 | 1,204,631 | 518.2s / 1,483,902 msg/s |
| Dekaf | 2026-07-26T03:45:18.0649971+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1759.7 MB/s | 3/3 | 1,261,391 | 545.2s / 1,513,793 msg/s |
| Dekaf | 2026-07-26T03:45:45.0764538+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1759.7 MB/s | 4/3 | 1,318,127 | 572.2s / 1,521,493 msg/s |
| Dekaf | 2026-07-26T03:46:12.0940652+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1759.7 MB/s | 4/3 | 1,378,248 | 599.2s / 1,549,791 msg/s |
| Dekaf | 2026-07-26T03:46:40.1141873+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1759.7 MB/s | 5/3 | 1,439,855 | 627.2s / 1,493,536 msg/s |
| Dekaf | 2026-07-26T03:47:07.1312811+00:00 | 1 | 15.0 MiB / 13.5 MiB | 1759.7 MB/s | 5/4 | 1,494,656 | 654.2s / 1,591,462 msg/s |
| Dekaf | 2026-07-26T03:47:34.1490114+00:00 | 1 | 15.0 MiB / 14.1 MiB | 1759.7 MB/s | 5/4 | 1,544,728 | 681.2s / 1,048,832 msg/s |
| Dekaf | 2026-07-26T03:48:01.1590047+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1759.7 MB/s | 5/4 | 1,603,379 | 708.2s / 1,650,626 msg/s |
| Dekaf | 2026-07-26T03:48:29.1733697+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1773.0 MB/s | 6/4 | 1,668,748 | 736.2s / 1,607,562 msg/s |
| Dekaf | 2026-07-26T03:48:56.1815258+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1773.0 MB/s | 6/4 | 1,735,336 | 763.2s / 1,622,224 msg/s |
| Dekaf | 2026-07-26T03:49:23.1913201+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1773.0 MB/s | 7/4 | 1,808,949 | 790.2s / 1,586,967 msg/s |
| Dekaf | 2026-07-26T03:49:51.1961366+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1773.0 MB/s | 7/5 | 1,877,267 | 818.2s / 1,574,473 msg/s |
| Dekaf | 2026-07-26T03:50:18.2073051+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1773.0 MB/s | 7/5 | 1,950,394 | 845.3s / 1,576,903 msg/s |
| Dekaf | 2026-07-26T03:50:45.2112386+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1773.0 MB/s | 7/5 | 2,025,003 | 872.3s / 1,595,884 msg/s |
| Dekaf | 2026-07-26T03:51:12.2230241+00:00 | 1 | 11.0 MiB / 8.4 MiB | 1773.0 MB/s | 7/6 | 2,095,625 | 899.3s / 1,618,121 msg/s |
| Dekaf (3conn) | 2026-07-26T04:06:40.9738332+00:00 | 1 | 16.0 MiB / 1.9 MiB | 1793.8 MB/s | 0/0 | 2,131 | 27.0s / 1,525,766 msg/s |
| Dekaf (3conn) | 2026-07-26T04:07:07.9850112+00:00 | 1 | 16.0 MiB / 4.4 MiB | 1865.2 MB/s | 0/1 | 4,450 | 54.0s / 1,492,835 msg/s |
| Dekaf (3conn) | 2026-07-26T04:07:34.9944984+00:00 | 1 | 16.0 MiB / 3.3 MiB | 1865.2 MB/s | 0/1 | 6,264 | 81.1s / 1,497,021 msg/s |
| Dekaf (3conn) | 2026-07-26T04:08:02.0034535+00:00 | 1 | 18.0 MiB / 4.0 MiB | 1865.2 MB/s | 0/1 | 7,991 | 108.1s / 1,507,661 msg/s |
| Dekaf (3conn) | 2026-07-26T04:08:30.0130165+00:00 | 1 | 18.0 MiB / 5.7 MiB | 1865.2 MB/s | 1/1 | 9,469 | 136.1s / 1,517,651 msg/s |
| Dekaf (3conn) | 2026-07-26T04:08:57.029207+00:00 | 1 | 20.0 MiB / 5.9 MiB | 1865.2 MB/s | 1/1 | 10,650 | 163.1s / 1,670,133 msg/s |
| Dekaf (3conn) | 2026-07-26T04:09:24.0368276+00:00 | 1 | 20.0 MiB / 2.6 MiB | 1865.2 MB/s | 2/1 | 11,441 | 190.1s / 1,467,077 msg/s |
| Dekaf (3conn) | 2026-07-26T04:09:51.0419974+00:00 | 1 | 22.0 MiB / 12.2 MiB | 1865.2 MB/s | 3/1 | 12,024 | 217.1s / 1,416,445 msg/s |
| Dekaf (3conn) | 2026-07-26T04:10:19.0500818+00:00 | 1 | 22.0 MiB / 10.8 MiB | 1865.2 MB/s | 3/1 | 12,578 | 245.2s / 1,500,145 msg/s |
| Dekaf (3conn) | 2026-07-26T04:10:46.0593313+00:00 | 1 | 24.0 MiB / 2.8 MiB | 1865.2 MB/s | 4/1 | 12,990 | 272.2s / 1,407,718 msg/s |
| Dekaf (3conn) | 2026-07-26T04:11:13.0666336+00:00 | 1 | 27.0 MiB / 5.0 MiB | 1865.2 MB/s | 4/1 | 13,292 | 299.2s / 1,259,546 msg/s |
| Dekaf (3conn) | 2026-07-26T04:11:40.0874176+00:00 | 1 | 27.0 MiB / 0.9 MiB | 1865.2 MB/s | 5/1 | 13,406 | 326.2s / 1,368,755 msg/s |
| Dekaf (3conn) | 2026-07-26T04:12:08.1065498+00:00 | 1 | 30.0 MiB / 19.8 MiB | 1865.2 MB/s | 6/1 | 13,543 | 354.2s / 1,350,699 msg/s |
| Dekaf (3conn) | 2026-07-26T04:12:35.133013+00:00 | 1 | 30.0 MiB / 11.6 MiB | 1865.2 MB/s | 6/1 | 13,698 | 381.2s / 1,466,299 msg/s |
| Dekaf (3conn) | 2026-07-26T04:13:02.1479349+00:00 | 1 | 30.0 MiB / 2.6 MiB | 1865.2 MB/s | 6/2 | 13,837 | 408.2s / 1,556,898 msg/s |
| Dekaf (3conn) | 2026-07-26T04:13:30.1682126+00:00 | 1 | 30.0 MiB / 3.4 MiB | 1865.2 MB/s | 6/2 | 14,011 | 436.3s / 1,357,802 msg/s |
| Dekaf (3conn) | 2026-07-26T04:13:57.1900288+00:00 | 1 | 26.0 MiB / 6.7 MiB | 1865.2 MB/s | 6/2 | 14,172 | 463.3s / 1,336,161 msg/s |
| Dekaf (3conn) | 2026-07-26T04:14:24.2082635+00:00 | 1 | 26.0 MiB / 9.4 MiB | 1865.2 MB/s | 7/2 | 14,551 | 490.3s / 1,464,633 msg/s |
| Dekaf (3conn) | 2026-07-26T04:14:51.2230217+00:00 | 1 | 26.0 MiB / 8.4 MiB | 1865.2 MB/s | 7/3 | 15,006 | 517.3s / 1,383,028 msg/s |
| Dekaf (3conn) | 2026-07-26T04:15:19.2370139+00:00 | 1 | 26.0 MiB / 4.2 MiB | 1865.2 MB/s | 7/3 | 15,199 | 545.3s / 1,357,990 msg/s |
| Dekaf (3conn) | 2026-07-26T04:15:46.2470268+00:00 | 1 | 29.0 MiB / 4.8 MiB | 1865.2 MB/s | 7/3 | 15,510 | 572.3s / 1,324,152 msg/s |
| Dekaf (3conn) | 2026-07-26T04:16:13.2564886+00:00 | 1 | 26.0 MiB / 4.9 MiB | 1865.2 MB/s | 7/4 | 15,778 | 599.3s / 1,524,343 msg/s |
| Dekaf (3conn) | 2026-07-26T04:16:40.2700157+00:00 | 1 | 26.0 MiB / 5.7 MiB | 1865.2 MB/s | 7/4 | 16,063 | 626.4s / 1,470,071 msg/s |
| Dekaf (3conn) | 2026-07-26T04:17:08.2860064+00:00 | 1 | 26.0 MiB / 17.2 MiB | 1865.2 MB/s | 7/4 | 16,509 | 654.4s / 1,459,800 msg/s |
| Dekaf (3conn) | 2026-07-26T04:17:35.3001185+00:00 | 1 | 26.0 MiB / 5.0 MiB | 1865.2 MB/s | 7/4 | 16,908 | 681.4s / 1,407,653 msg/s |
| Dekaf (3conn) | 2026-07-26T04:18:02.3083918+00:00 | 1 | 22.0 MiB / 17.4 MiB | 1865.2 MB/s | 7/4 | 17,115 | 708.4s / 1,340,330 msg/s |
| Dekaf (3conn) | 2026-07-26T04:18:29.324036+00:00 | 1 | 26.0 MiB / 3.6 MiB | 1865.2 MB/s | 7/5 | 17,270 | 735.4s / 1,246,687 msg/s |
| Dekaf (3conn) | 2026-07-26T04:18:57.3370194+00:00 | 1 | 26.0 MiB / 4.9 MiB | 1865.2 MB/s | 7/5 | 17,473 | 763.4s / 1,328,180 msg/s |
| Dekaf (3conn) | 2026-07-26T04:19:24.3489895+00:00 | 1 | 26.0 MiB / 3.6 MiB | 1865.2 MB/s | 7/5 | 17,726 | 790.4s / 1,471,088 msg/s |
| Dekaf (3conn) | 2026-07-26T04:19:51.3560003+00:00 | 1 | 26.0 MiB / 11.6 MiB | 1865.2 MB/s | 7/5 | 17,911 | 817.5s / 1,439,517 msg/s |
| Dekaf (3conn) | 2026-07-26T04:20:18.3716317+00:00 | 1 | 26.0 MiB / 14.7 MiB | 1865.2 MB/s | 7/5 | 18,302 | 844.5s / 1,548,240 msg/s |
| Dekaf (3conn) | 2026-07-26T04:20:46.3875916+00:00 | 1 | 26.0 MiB / 7.3 MiB | 1913.5 MB/s | 7/5 | 18,948 | 872.5s / 1,654,363 msg/s |
| Dekaf (3conn) | 2026-07-26T04:21:13.3950346+00:00 | 1 | 26.0 MiB / 6.4 MiB | 1913.5 MB/s | 7/5 | 19,474 | 899.5s / 1,652,866 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-26T03:21:42.0612909+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 8.3 MiB |
| Dekaf | 2026-07-26T03:21:57.0751266+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-26T03:22:27.1205453+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:22:42.1344552+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:23:12.1587326+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:23:27.1721544+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-26T03:24:27.2226094+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:24:42.2335356+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 11.8 MiB |
| Dekaf | 2026-07-26T03:25:12.2537579+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-26T03:25:27.2649203+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:25:57.2860579+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:26:12.3006166+00:00 | 1 | capacity | succeeded | 15,014ms | 15.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-26T03:26:42.3198157+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-26T03:26:57.3293895+00:00 | 1 | capacity | succeeded | 15,009ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-26T03:27:27.3542852+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.4 MiB |
| Dekaf | 2026-07-26T03:27:42.3643205+00:00 | 1 | capacity | succeeded | 15,010ms | 18.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-26T03:28:12.3882318+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-26T03:28:27.3992894+00:00 | 1 | capacity | succeeded | 15,011ms | 20.0 MiB / 18.5 MiB |
| Dekaf | 2026-07-26T03:28:57.4291923+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-26T03:29:12.4381693+00:00 | 1 | capacity | failed | 15,008ms | 20.0 MiB / 20.7 MiB |
| Dekaf | 2026-07-26T03:29:42.4637407+00:00 | 1 | capacity | started | 0ms | 17.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-26T03:29:57.4768363+00:00 | 1 | capacity | succeeded | 15,013ms | 17.0 MiB / 16.1 MiB |
| Dekaf | 2026-07-26T03:30:27.5001461+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 16.6 MiB |
| Dekaf | 2026-07-26T03:30:42.5088596+00:00 | 1 | capacity | succeeded | 15,008ms | 14.0 MiB / 12.9 MiB |
| Dekaf | 2026-07-26T03:31:12.5482663+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:31:27.5626109+00:00 | 1 | capacity | failed | 15,014ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:32:27.6244165+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:32:42.6379668+00:00 | 1 | capacity | failed | 15,013ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-26T03:34:42.7284949+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:34:57.7376614+00:00 | 1 | capacity | succeeded | 15,009ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:35:27.7609775+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.4 MiB |
| Dekaf | 2026-07-26T03:35:42.7712152+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:36:42.9713617+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-26T03:36:57.9809183+00:00 | 1 | capacity | succeeded | 15,009ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-26T03:37:28.0000856+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:37:43.011768+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 10.4 MiB |
| Dekaf | 2026-07-26T03:38:13.029549+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:38:28.0402103+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-26T03:39:28.0901229+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:39:43.1165052+00:00 | 1 | capacity | failed | 15,026ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-26T03:40:13.1425593+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:40:28.1543831+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:44:28.3298483+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-26T03:44:43.3424211+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:45:13.3645179+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:45:28.3759103+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-26T03:45:58.3995244+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:46:13.4103713+00:00 | 1 | capacity | succeeded | 15,010ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-26T03:46:43.4331342+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-26T03:46:58.4457041+00:00 | 1 | capacity | failed | 15,012ms | 15.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-26T03:47:58.4980617+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-26T03:48:13.5095366+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-26T03:48:43.5313548+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-26T03:48:58.5407887+00:00 | 1 | capacity | succeeded | 15,010ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:49:28.561141+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.8 MiB |
| Dekaf | 2026-07-26T03:49:43.5741372+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-26T03:50:43.6417087+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-26T03:50:58.6509036+00:00 | 1 | capacity | failed | 15,009ms | 11.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-26T04:06:44.0891908+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 9.7 MiB |
| Dekaf (3conn) | 2026-07-26T04:06:59.1143102+00:00 | 1 | capacity | failed | 15,025ms | 16.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-07-26T04:07:59.2240199+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 10.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:08:14.2461124+00:00 | 1 | capacity | succeeded | 15,021ms | 18.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-26T04:08:44.304009+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-26T04:08:59.3399989+00:00 | 1 | capacity | succeeded | 15,035ms | 20.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-26T04:09:29.3918192+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 4.5 MiB |
| Dekaf (3conn) | 2026-07-26T04:09:44.418124+00:00 | 1 | capacity | succeeded | 15,026ms | 22.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:10:14.458568+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-26T04:10:29.4767547+00:00 | 1 | capacity | succeeded | 15,018ms | 24.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-26T04:10:59.5243053+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-26T04:11:14.5499451+00:00 | 1 | capacity | succeeded | 15,025ms | 27.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:11:44.627943+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-26T04:11:59.6518702+00:00 | 1 | capacity | succeeded | 15,024ms | 30.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:12:29.6980537+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-26T04:12:44.726296+00:00 | 1 | capacity | failed | 15,028ms | 30.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-26T04:13:44.8146008+00:00 | 1 | capacity | started | 0ms | 26.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-26T04:13:59.8421589+00:00 | 1 | capacity | succeeded | 15,027ms | 26.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-26T04:14:29.8965016+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 0.8 MiB |
| Dekaf (3conn) | 2026-07-26T04:14:44.9263445+00:00 | 1 | capacity | failed | 15,029ms | 26.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-26T04:15:45.1878312+00:00 | 1 | capacity | started | 0ms | 29.0 MiB / 11.3 MiB |
| Dekaf (3conn) | 2026-07-26T04:16:00.2160428+00:00 | 1 | capacity | failed | 15,028ms | 26.0 MiB / 11.0 MiB |
| Dekaf (3conn) | 2026-07-26T04:18:00.4868966+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 9.7 MiB |
| Dekaf (3conn) | 2026-07-26T04:18:15.5057225+00:00 | 1 | capacity | failed | 15,018ms | 26.0 MiB / 5.3 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 8 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 11 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 34 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 93 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 265 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 533 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 663 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 1,028 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 1,265 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 1,081 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 714 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 360 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 88 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 7 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 8 |
| Dekaf | 1 | 0.001–0.002ms | 2,618 |
| Dekaf | 1 | 0.002–0.004ms | 3,089 |
| Dekaf | 1 | 0.004–0.008ms | 10,254 |
| Dekaf | 1 | 0.008–0.016ms | 50,101 |
| Dekaf | 1 | 0.016–0.032ms | 65,318 |
| Dekaf | 1 | 0.032–0.064ms | 57,268 |
| Dekaf | 1 | 0.064–0.128ms | 117,818 |
| Dekaf | 1 | 0.128–0.256ms | 246,406 |
| Dekaf | 1 | 0.256–0.512ms | 208,203 |
| Dekaf | 1 | 0.512–1.024ms | 32,659 |
| Dekaf | 1 | 1.024–2.048ms | 4,835 |
| Dekaf | 1 | 2.048–4.096ms | 3,457 |
| Dekaf | 1 | 4.096–8.192ms | 703 |
| Dekaf | 1 | 8.192–16.384ms | 34 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 2,118 |
| Dekaf | 1 | 0.002–0.004ms | 2,488 |
| Dekaf | 1 | 0.004–0.008ms | 9,317 |
| Dekaf | 1 | 0.008–0.016ms | 51,629 |
| Dekaf | 1 | 0.016–0.032ms | 63,213 |
| Dekaf | 1 | 0.032–0.064ms | 60,193 |
| Dekaf | 1 | 0.064–0.128ms | 116,590 |
| Dekaf | 1 | 0.128–0.256ms | 295,070 |
| Dekaf | 1 | 0.256–0.512ms | 335,885 |
| Dekaf | 1 | 0.512–1.024ms | 58,325 |
| Dekaf | 1 | 1.024–2.048ms | 6,219 |
| Dekaf | 1 | 2.048–4.096ms | 3,931 |
| Dekaf | 1 | 4.096–8.192ms | 746 |
| Dekaf | 1 | 8.192–16.384ms | 44 |
| Dekaf | 1 | 16.384–32.768ms | 1 |
| Dekaf | 1 | 32.768–65.536ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 10,741,000 | 2026-07-26T03:06:21.1369653+00:00 | 105.3ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,742,000 | 2026-07-26T03:06:21.1374897+00:00 | 107.7ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,743,000 | 2026-07-26T03:06:21.1383232+00:00 | 107.1ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,744,000 | 2026-07-26T03:06:21.1397098+00:00 | 105.9ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,745,000 | 2026-07-26T03:06:21.1404833+00:00 | 101.7ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,746,000 | 2026-07-26T03:06:21.1406433+00:00 | 101.6ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,747,000 | 2026-07-26T03:06:21.1411542+00:00 | 104.6ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 10,748,000 | 2026-07-26T03:06:21.1415711+00:00 | 104.3ms | GC pause | - | - | 10.0s / 1,200,224 msg/s | Gen2 +0 / pause +169.7ms |
| Confluent | 314,795,000 | 2026-07-26T03:09:57.1687646+00:00 | 112.6ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,796,000 | 2026-07-26T03:09:57.1693427+00:00 | 112.2ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,797,000 | 2026-07-26T03:09:57.1699655+00:00 | 112.1ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,798,000 | 2026-07-26T03:09:57.1707088+00:00 | 111.5ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,799,000 | 2026-07-26T03:09:57.1712797+00:00 | 110.6ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,800,000 | 2026-07-26T03:09:57.1718321+00:00 | 112.3ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,801,000 | 2026-07-26T03:09:57.1726294+00:00 | 112.1ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,802,000 | 2026-07-26T03:09:57.1784571+00:00 | 109.4ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,803,000 | 2026-07-26T03:09:57.179147+00:00 | 110.6ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,804,000 | 2026-07-26T03:09:57.1796651+00:00 | 110.9ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,805,000 | 2026-07-26T03:09:57.1801866+00:00 | 105.0ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,806,000 | 2026-07-26T03:09:57.1816711+00:00 | 103.6ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,807,000 | 2026-07-26T03:09:57.1821939+00:00 | 103.9ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,808,000 | 2026-07-26T03:09:57.1827084+00:00 | 108.5ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 314,809,000 | 2026-07-26T03:09:57.1832278+00:00 | 107.8ms | GC pause | - | - | 226.1s / 982,717 msg/s | Gen2 +0 / pause +165.5ms |
| Confluent | 923,968,000 | 2026-07-26T03:16:56.0861481+00:00 | 142.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,971,000 | 2026-07-26T03:16:56.0882045+00:00 | 140.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,972,000 | 2026-07-26T03:16:56.0886962+00:00 | 108.0ms | GC pause | - | - | 644.4s / 1,310,908 msg/s | Gen2 +0 / pause +134.6ms |
| Confluent | 923,973,000 | 2026-07-26T03:16:56.0892112+00:00 | 114.9ms | GC pause | - | - | 644.4s / 1,310,908 msg/s | Gen2 +0 / pause +134.6ms |
| Confluent | 923,974,000 | 2026-07-26T03:16:56.0905681+00:00 | 137.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,975,000 | 2026-07-26T03:16:56.0938395+00:00 | 135.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,976,000 | 2026-07-26T03:16:56.0943012+00:00 | 135.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,977,000 | 2026-07-26T03:16:56.0947541+00:00 | 142.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,978,000 | 2026-07-26T03:16:56.0970706+00:00 | 140.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,979,000 | 2026-07-26T03:16:56.098358+00:00 | 135.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,980,000 | 2026-07-26T03:16:56.0991157+00:00 | 133.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,981,000 | 2026-07-26T03:16:56.0998007+00:00 | 151.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,982,000 | 2026-07-26T03:16:56.1004211+00:00 | 132.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,983,000 | 2026-07-26T03:16:56.1008526+00:00 | 144.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,984,000 | 2026-07-26T03:16:56.1012757+00:00 | 145.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,986,000 | 2026-07-26T03:16:56.1021397+00:00 | 148.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,987,000 | 2026-07-26T03:16:56.10271+00:00 | 162.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,988,000 | 2026-07-26T03:16:56.1031281+00:00 | 162.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,989,000 | 2026-07-26T03:16:56.1036185+00:00 | 157.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,990,000 | 2026-07-26T03:16:56.104053+00:00 | 156.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,985,000 | 2026-07-26T03:16:56.1047286+00:00 | 146.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,991,000 | 2026-07-26T03:16:56.113589+00:00 | 151.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,992,000 | 2026-07-26T03:16:56.1140259+00:00 | 146.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,993,000 | 2026-07-26T03:16:56.114446+00:00 | 146.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,994,000 | 2026-07-26T03:16:56.1149582+00:00 | 145.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,995,000 | 2026-07-26T03:16:56.1227544+00:00 | 147.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,996,000 | 2026-07-26T03:16:56.1245051+00:00 | 146.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,997,000 | 2026-07-26T03:16:56.1250115+00:00 | 161.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,998,000 | 2026-07-26T03:16:56.1254508+00:00 | 160.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 923,999,000 | 2026-07-26T03:16:56.1258978+00:00 | 144.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,000,000 | 2026-07-26T03:16:56.1517206+00:00 | 118.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,001,000 | 2026-07-26T03:16:56.1537333+00:00 | 132.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,002,000 | 2026-07-26T03:16:56.154299+00:00 | 125.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,003,000 | 2026-07-26T03:16:56.1608998+00:00 | 119.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,004,000 | 2026-07-26T03:16:56.1614865+00:00 | 119.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,005,000 | 2026-07-26T03:16:56.1642136+00:00 | 116.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,006,000 | 2026-07-26T03:16:56.1647131+00:00 | 116.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,007,000 | 2026-07-26T03:16:56.1652111+00:00 | 140.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,008,000 | 2026-07-26T03:16:56.1705285+00:00 | 135.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,009,000 | 2026-07-26T03:16:56.1710825+00:00 | 129.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,010,000 | 2026-07-26T03:16:56.1715317+00:00 | 123.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,011,000 | 2026-07-26T03:16:56.174041+00:00 | 149.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,012,000 | 2026-07-26T03:16:56.1766032+00:00 | 112.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,013,000 | 2026-07-26T03:16:56.1770652+00:00 | 131.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,014,000 | 2026-07-26T03:16:56.1775098+00:00 | 140.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,015,000 | 2026-07-26T03:16:56.1779648+00:00 | 140.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,016,000 | 2026-07-26T03:16:56.1784105+00:00 | 139.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,017,000 | 2026-07-26T03:16:56.1826977+00:00 | 151.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,018,000 | 2026-07-26T03:16:56.1831463+00:00 | 151.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,019,000 | 2026-07-26T03:16:56.1835619+00:00 | 144.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,020,000 | 2026-07-26T03:16:56.1840244+00:00 | 143.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,021,000 | 2026-07-26T03:16:56.1872305+00:00 | 152.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,022,000 | 2026-07-26T03:16:56.1878445+00:00 | 139.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,023,000 | 2026-07-26T03:16:56.1896651+00:00 | 137.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,024,000 | 2026-07-26T03:16:56.1902262+00:00 | 138.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,025,000 | 2026-07-26T03:16:56.191677+00:00 | 145.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,026,000 | 2026-07-26T03:16:56.1922045+00:00 | 144.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,027,000 | 2026-07-26T03:16:56.1927232+00:00 | 157.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,028,000 | 2026-07-26T03:16:56.1939495+00:00 | 155.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,029,000 | 2026-07-26T03:16:56.1948058+00:00 | 142.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,030,000 | 2026-07-26T03:16:56.2033159+00:00 | 132.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,031,000 | 2026-07-26T03:16:56.2037545+00:00 | 146.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,032,000 | 2026-07-26T03:16:56.2045872+00:00 | 139.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,033,000 | 2026-07-26T03:16:56.2051009+00:00 | 139.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,034,000 | 2026-07-26T03:16:56.2092738+00:00 | 136.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,035,000 | 2026-07-26T03:16:56.2099635+00:00 | 139.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,036,000 | 2026-07-26T03:16:56.2104895+00:00 | 139.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,037,000 | 2026-07-26T03:16:56.2109041+00:00 | 148.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +276.2ms |
| Confluent | 924,038,000 | 2026-07-26T03:16:56.2279551+00:00 | 139.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,039,000 | 2026-07-26T03:16:56.2298323+00:00 | 125.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,040,000 | 2026-07-26T03:16:56.230605+00:00 | 123.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,041,000 | 2026-07-26T03:16:56.2311833+00:00 | 136.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,042,000 | 2026-07-26T03:16:56.233776+00:00 | 120.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,043,000 | 2026-07-26T03:16:56.2343358+00:00 | 127.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,044,000 | 2026-07-26T03:16:56.2348816+00:00 | 126.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,045,000 | 2026-07-26T03:16:56.2353896+00:00 | 129.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,046,000 | 2026-07-26T03:16:56.2358701+00:00 | 128.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,047,000 | 2026-07-26T03:16:56.2392897+00:00 | 154.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,048,000 | 2026-07-26T03:16:56.239887+00:00 | 154.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,049,000 | 2026-07-26T03:16:56.2407609+00:00 | 133.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,050,000 | 2026-07-26T03:16:56.2421115+00:00 | 125.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,051,000 | 2026-07-26T03:16:56.2438039+00:00 | 152.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,052,000 | 2026-07-26T03:16:56.2447968+00:00 | 130.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,053,000 | 2026-07-26T03:16:56.2477811+00:00 | 120.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,054,000 | 2026-07-26T03:16:56.2484215+00:00 | 126.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,055,000 | 2026-07-26T03:16:56.2517631+00:00 | 143.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,056,000 | 2026-07-26T03:16:56.2525821+00:00 | 142.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,057,000 | 2026-07-26T03:16:56.2531367+00:00 | 154.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,058,000 | 2026-07-26T03:16:56.2539147+00:00 | 153.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,059,000 | 2026-07-26T03:16:56.255132+00:00 | 140.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,060,000 | 2026-07-26T03:16:56.2646112+00:00 | 130.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,061,000 | 2026-07-26T03:16:56.2652988+00:00 | 148.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,062,000 | 2026-07-26T03:16:56.2660306+00:00 | 130.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,063,000 | 2026-07-26T03:16:56.266577+00:00 | 129.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,064,000 | 2026-07-26T03:16:56.2708762+00:00 | 125.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,065,000 | 2026-07-26T03:16:56.271868+00:00 | 130.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,066,000 | 2026-07-26T03:16:56.2724482+00:00 | 130.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,067,000 | 2026-07-26T03:16:56.2732001+00:00 | 151.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,068,000 | 2026-07-26T03:16:56.2780043+00:00 | 146.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,069,000 | 2026-07-26T03:16:56.2791289+00:00 | 133.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,070,000 | 2026-07-26T03:16:56.2796506+00:00 | 123.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,071,000 | 2026-07-26T03:16:56.2812223+00:00 | 143.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,072,000 | 2026-07-26T03:16:56.2830466+00:00 | 125.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,073,000 | 2026-07-26T03:16:56.2835871+00:00 | 128.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,074,000 | 2026-07-26T03:16:56.2841084+00:00 | 132.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,075,000 | 2026-07-26T03:16:56.284631+00:00 | 139.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,076,000 | 2026-07-26T03:16:56.2860939+00:00 | 137.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,077,000 | 2026-07-26T03:16:56.2882529+00:00 | 141.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,078,000 | 2026-07-26T03:16:56.289485+00:00 | 144.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,079,000 | 2026-07-26T03:16:56.2900457+00:00 | 139.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,080,000 | 2026-07-26T03:16:56.2939679+00:00 | 129.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,081,000 | 2026-07-26T03:16:56.2955347+00:00 | 138.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,082,000 | 2026-07-26T03:16:56.2965185+00:00 | 130.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,083,000 | 2026-07-26T03:16:56.2976233+00:00 | 126.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,084,000 | 2026-07-26T03:16:56.2995501+00:00 | 125.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,085,000 | 2026-07-26T03:16:56.3007336+00:00 | 132.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,086,000 | 2026-07-26T03:16:56.3040718+00:00 | 129.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,087,000 | 2026-07-26T03:16:56.3048131+00:00 | 133.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,088,000 | 2026-07-26T03:16:56.3058848+00:00 | 132.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,089,000 | 2026-07-26T03:16:56.3064493+00:00 | 127.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,090,000 | 2026-07-26T03:16:56.3105098+00:00 | 123.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,091,000 | 2026-07-26T03:16:56.3110537+00:00 | 128.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,092,000 | 2026-07-26T03:16:56.3120195+00:00 | 125.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,093,000 | 2026-07-26T03:16:56.3131392+00:00 | 120.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,094,000 | 2026-07-26T03:16:56.3184038+00:00 | 115.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,095,000 | 2026-07-26T03:16:56.319073+00:00 | 115.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,096,000 | 2026-07-26T03:16:56.3197073+00:00 | 115.2ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,097,000 | 2026-07-26T03:16:56.3226734+00:00 | 122.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,098,000 | 2026-07-26T03:16:56.3239553+00:00 | 121.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,099,000 | 2026-07-26T03:16:56.3265366+00:00 | 112.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,100,000 | 2026-07-26T03:16:56.3287165+00:00 | 109.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,101,000 | 2026-07-26T03:16:56.3292883+00:00 | 117.4ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,102,000 | 2026-07-26T03:16:56.3298227+00:00 | 110.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,103,000 | 2026-07-26T03:16:56.3357646+00:00 | 105.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,104,000 | 2026-07-26T03:16:56.3363817+00:00 | 105.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,105,000 | 2026-07-26T03:16:56.337133+00:00 | 108.0ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,106,000 | 2026-07-26T03:16:56.3376744+00:00 | 107.5ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,107,000 | 2026-07-26T03:16:56.3388442+00:00 | 111.7ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,108,000 | 2026-07-26T03:16:56.3396272+00:00 | 110.9ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,109,000 | 2026-07-26T03:16:56.340485+00:00 | 105.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,110,000 | 2026-07-26T03:16:56.3413029+00:00 | 104.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,111,000 | 2026-07-26T03:16:56.3442792+00:00 | 106.3ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,112,000 | 2026-07-26T03:16:56.3451364+00:00 | 102.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,113,000 | 2026-07-26T03:16:56.3458194+00:00 | 102.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,114,000 | 2026-07-26T03:16:56.3463437+00:00 | 102.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,115,000 | 2026-07-26T03:16:56.3468268+00:00 | 103.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,117,000 | 2026-07-26T03:16:56.3510288+00:00 | 102.6ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,118,000 | 2026-07-26T03:16:56.3525199+00:00 | 104.8ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Confluent | 924,121,000 | 2026-07-26T03:16:56.3572923+00:00 | 100.1ms | GC pause | - | - | 645.4s / 1,047,603 msg/s | Gen2 +0 / pause +141.6ms |
| Dekaf | 756,460,000 | 2026-07-26T03:29:14.2064655+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 1,421,936 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 756,470,000 | 2026-07-26T03:29:14.2143208+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 1,421,936 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 756,480,000 | 2026-07-26T03:29:14.2219596+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 1,421,936 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 756,490,000 | 2026-07-26T03:29:14.2276039+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 1,421,936 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 756,500,000 | 2026-07-26T03:29:14.2359145+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 1,421,936 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 756,510,000 | 2026-07-26T03:29:14.2443366+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 483.2s / 1,421,936 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 744,065,000 | 2026-07-26T04:14:42.6558023+00:00 | 219.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,068,000 | 2026-07-26T04:14:42.6568829+00:00 | 218.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,067,000 | 2026-07-26T04:14:42.6571518+00:00 | 217.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,075,000 | 2026-07-26T04:14:42.6602572+00:00 | 219.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,077,000 | 2026-07-26T04:14:42.6616411+00:00 | 214.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,078,000 | 2026-07-26T04:14:42.6624099+00:00 | 220.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,080,000 | 2026-07-26T04:14:42.6631208+00:00 | 222.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,082,000 | 2026-07-26T04:14:42.6647056+00:00 | 219.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,085,000 | 2026-07-26T04:14:42.6671889+00:00 | 215.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,087,000 | 2026-07-26T04:14:42.667927+00:00 | 209.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,088,000 | 2026-07-26T04:14:42.6682068+00:00 | 214.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,090,000 | 2026-07-26T04:14:42.6701541+00:00 | 215.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,091,000 | 2026-07-26T04:14:42.6708679+00:00 | 213.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,092,000 | 2026-07-26T04:14:42.6711466+00:00 | 212.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,095,000 | 2026-07-26T04:14:42.6731456+00:00 | 210.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,097,000 | 2026-07-26T04:14:42.6752532+00:00 | 204.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,098,000 | 2026-07-26T04:14:42.6758176+00:00 | 209.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,100,000 | 2026-07-26T04:14:42.6968221+00:00 | 190.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 744,101,000 | 2026-07-26T04:14:42.7041852+00:00 | 181.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 509.3s / 1,164,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,837,000 | 2026-07-26T04:15:08.4085517+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,838,000 | 2026-07-26T04:15:08.4088003+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,840,000 | 2026-07-26T04:15:08.411686+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,841,000 | 2026-07-26T04:15:08.4140979+00:00 | 222.6ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,842,000 | 2026-07-26T04:15:08.4143366+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,843,000 | 2026-07-26T04:15:08.4145874+00:00 | 214.4ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,844,000 | 2026-07-26T04:15:08.4148338+00:00 | 214.2ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,845,000 | 2026-07-26T04:15:08.415088+00:00 | 221.6ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,846,000 | 2026-07-26T04:15:08.4156195+00:00 | 218.8ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,847,000 | 2026-07-26T04:15:08.41633+00:00 | 214.2ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,848,000 | 2026-07-26T04:15:08.4165968+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,849,000 | 2026-07-26T04:15:08.4168581+00:00 | 213.7ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,850,000 | 2026-07-26T04:15:08.4171286+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,851,000 | 2026-07-26T04:15:08.4173935+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,852,000 | 2026-07-26T04:15:08.4180918+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,853,000 | 2026-07-26T04:15:08.4190294+00:00 | 211.5ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,854,000 | 2026-07-26T04:15:08.4192803+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,855,000 | 2026-07-26T04:15:08.4195425+00:00 | 217.2ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,856,000 | 2026-07-26T04:15:08.419791+00:00 | 214.7ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,857,000 | 2026-07-26T04:15:08.4200483+00:00 | 214.4ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,858,000 | 2026-07-26T04:15:08.4204447+00:00 | 216.3ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,859,000 | 2026-07-26T04:15:08.4212847+00:00 | 209.3ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,860,000 | 2026-07-26T04:15:08.4217005+00:00 | 215.0ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,861,000 | 2026-07-26T04:15:08.4219687+00:00 | 214.7ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,862,000 | 2026-07-26T04:15:08.4222456+00:00 | 214.4ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 779,863,000 | 2026-07-26T04:15:08.4225171+00:00 | 208.0ms | broker/backlog (no scale or GC event) | - | - | 535.3s / 1,111,860 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,645,000 | 2026-07-26T04:15:15.6598427+00:00 | 222.0ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,647,000 | 2026-07-26T04:15:15.6606137+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,648,000 | 2026-07-26T04:15:15.6610568+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,653,000 | 2026-07-26T04:15:15.6637938+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,654,000 | 2026-07-26T04:15:15.6663721+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,655,000 | 2026-07-26T04:15:15.6677056+00:00 | 223.5ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,656,000 | 2026-07-26T04:15:15.6679635+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,657,000 | 2026-07-26T04:15:15.6682405+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,658,000 | 2026-07-26T04:15:15.668653+00:00 | 222.6ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,659,000 | 2026-07-26T04:15:15.6689156+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,660,000 | 2026-07-26T04:15:15.6697648+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,661,000 | 2026-07-26T04:15:15.6701921+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,662,000 | 2026-07-26T04:15:15.6704616+00:00 | 220.8ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,663,000 | 2026-07-26T04:15:15.6707458+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,664,000 | 2026-07-26T04:15:15.6711844+00:00 | 214.5ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,665,000 | 2026-07-26T04:15:15.6714545+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,666,000 | 2026-07-26T04:15:15.6723265+00:00 | 213.4ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,667,000 | 2026-07-26T04:15:15.6725955+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,668,000 | 2026-07-26T04:15:15.6730054+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,669,000 | 2026-07-26T04:15:15.6732485+00:00 | 212.9ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,670,000 | 2026-07-26T04:15:15.6735136+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,671,000 | 2026-07-26T04:15:15.6739011+00:00 | 217.3ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,672,000 | 2026-07-26T04:15:15.6747261+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,673,000 | 2026-07-26T04:15:15.6749971+00:00 | 211.2ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,674,000 | 2026-07-26T04:15:15.6754319+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 789,675,000 | 2026-07-26T04:15:15.6757038+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 542.3s / 1,041,549 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 847,078,000 | 2026-07-26T04:15:57.402282+00:00 | 223.4ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,085,000 | 2026-07-26T04:15:57.405008+00:00 | 222.0ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,087,000 | 2026-07-26T04:15:57.4062044+00:00 | 220.1ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,088,000 | 2026-07-26T04:15:57.4064716+00:00 | 220.5ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,092,000 | 2026-07-26T04:15:57.4085882+00:00 | 229.5ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,095,000 | 2026-07-26T04:15:57.4099524+00:00 | 223.5ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,097,000 | 2026-07-26T04:15:57.410971+00:00 | 217.7ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,098,000 | 2026-07-26T04:15:57.4113857+00:00 | 224.0ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,100,000 | 2026-07-26T04:15:57.41202+00:00 | 229.5ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,101,000 | 2026-07-26T04:15:57.4122501+00:00 | 226.6ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,102,000 | 2026-07-26T04:15:57.412678+00:00 | 226.2ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,105,000 | 2026-07-26T04:15:57.4152927+00:00 | 220.1ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,107,000 | 2026-07-26T04:15:57.4158032+00:00 | 213.9ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,108,000 | 2026-07-26T04:15:57.4160584+00:00 | 219.4ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,110,000 | 2026-07-26T04:15:57.4182951+00:00 | 223.9ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,111,000 | 2026-07-26T04:15:57.4186855+00:00 | 222.2ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,112,000 | 2026-07-26T04:15:57.4189457+00:00 | 221.9ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,115,000 | 2026-07-26T04:15:57.4202472+00:00 | 216.6ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,117,000 | 2026-07-26T04:15:57.4214782+00:00 | 209.4ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,118,000 | 2026-07-26T04:15:57.4217354+00:00 | 215.1ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,120,000 | 2026-07-26T04:15:57.4222558+00:00 | 223.5ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,121,000 | 2026-07-26T04:15:57.4228408+00:00 | 218.6ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,122,000 | 2026-07-26T04:15:57.4235123+00:00 | 218.0ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,125,000 | 2026-07-26T04:15:57.4252069+00:00 | 213.7ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,127,000 | 2026-07-26T04:15:57.4283619+00:00 | 204.3ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 847,128,000 | 2026-07-26T04:15:57.428633+00:00 | 210.2ms | GC pause | 1:capacity/failed | - | 584.3s / 1,193,336 msg/s | Gen2 +1 / pause +0.8ms |
| Dekaf (3conn) | 993,277,000 | 2026-07-26T04:17:37.1964058+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,278,000 | 2026-07-26T04:17:37.1968566+00:00 | 221.8ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,279,000 | 2026-07-26T04:17:37.1982635+00:00 | 220.8ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,280,000 | 2026-07-26T04:17:37.1995958+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,281,000 | 2026-07-26T04:17:37.2020018+00:00 | 226.7ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,282,000 | 2026-07-26T04:17:37.2033667+00:00 | 225.4ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,283,000 | 2026-07-26T04:17:37.2036322+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,284,000 | 2026-07-26T04:17:37.2038944+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,285,000 | 2026-07-26T04:17:37.2043224+00:00 | 224.1ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,286,000 | 2026-07-26T04:17:37.205059+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,287,000 | 2026-07-26T04:17:37.2059441+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,288,000 | 2026-07-26T04:17:37.206214+00:00 | 222.2ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,289,000 | 2026-07-26T04:17:37.2064795+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,290,000 | 2026-07-26T04:17:37.2067345+00:00 | 222.8ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,291,000 | 2026-07-26T04:17:37.2071522+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,292,000 | 2026-07-26T04:17:37.207697+00:00 | 221.9ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,293,000 | 2026-07-26T04:17:37.2081173+00:00 | 219.9ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,294,000 | 2026-07-26T04:17:37.2086857+00:00 | 214.1ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,295,000 | 2026-07-26T04:17:37.2089543+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,296,000 | 2026-07-26T04:17:37.2092318+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,297,000 | 2026-07-26T04:17:37.2096447+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,298,000 | 2026-07-26T04:17:37.2100749+00:00 | 218.5ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,299,000 | 2026-07-26T04:17:37.2106329+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,300,000 | 2026-07-26T04:17:37.2112053+00:00 | 227.3ms | broker/backlog (no scale or GC event) | - | - | 684.4s / 1,190,049 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,301,000 | 2026-07-26T04:17:37.2114588+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 993,302,000 | 2026-07-26T04:17:37.2117111+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 683.4s / 1,014,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,585,000 | 2026-07-26T04:18:04.1742433+00:00 | 219.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,587,000 | 2026-07-26T04:18:04.1753378+00:00 | 218.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,588,000 | 2026-07-26T04:18:04.1757412+00:00 | 220.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,595,000 | 2026-07-26T04:18:04.1826864+00:00 | 217.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,596,000 | 2026-07-26T04:18:04.1840293+00:00 | 212.6ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,597,000 | 2026-07-26T04:18:04.184288+00:00 | 216.2ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,598,000 | 2026-07-26T04:18:04.1848704+00:00 | 215.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,599,000 | 2026-07-26T04:18:04.1861989+00:00 | 210.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,600,000 | 2026-07-26T04:18:04.1865957+00:00 | 214.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,601,000 | 2026-07-26T04:18:04.1868546+00:00 | 213.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,602,000 | 2026-07-26T04:18:04.1872614+00:00 | 213.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,603,000 | 2026-07-26T04:18:04.1875118+00:00 | 213.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,604,000 | 2026-07-26T04:18:04.1879156+00:00 | 208.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,605,000 | 2026-07-26T04:18:04.188324+00:00 | 212.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,606,000 | 2026-07-26T04:18:04.1890515+00:00 | 211.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,607,000 | 2026-07-26T04:18:04.1893168+00:00 | 211.3ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,608,000 | 2026-07-26T04:18:04.1897514+00:00 | 210.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,609,000 | 2026-07-26T04:18:04.1900215+00:00 | 210.5ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,610,000 | 2026-07-26T04:18:04.190433+00:00 | 212.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,611,000 | 2026-07-26T04:18:04.1908374+00:00 | 209.8ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,612,000 | 2026-07-26T04:18:04.1915893+00:00 | 212.9ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 710.4s / 1,092,896 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,925,000 | 2026-07-26T04:18:34.1644447+00:00 | 224.6ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,927,000 | 2026-07-26T04:18:34.1661388+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,928,000 | 2026-07-26T04:18:34.1674888+00:00 | 227.0ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,929,000 | 2026-07-26T04:18:34.1677678+00:00 | 221.8ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,930,000 | 2026-07-26T04:18:34.1682003+00:00 | 226.3ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,931,000 | 2026-07-26T04:18:34.1684735+00:00 | 226.0ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,932,000 | 2026-07-26T04:18:34.1687383+00:00 | 225.8ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,933,000 | 2026-07-26T04:18:34.1690095+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,934,000 | 2026-07-26T04:18:34.1710075+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,935,000 | 2026-07-26T04:18:34.1712745+00:00 | 223.2ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,936,000 | 2026-07-26T04:18:34.1717063+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,937,000 | 2026-07-26T04:18:34.1719665+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,938,000 | 2026-07-26T04:18:34.1722278+00:00 | 222.3ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,939,000 | 2026-07-26T04:18:34.1724804+00:00 | 217.1ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,940,000 | 2026-07-26T04:18:34.1732091+00:00 | 223.3ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,941,000 | 2026-07-26T04:18:34.1738571+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,942,000 | 2026-07-26T04:18:34.1743077+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,943,000 | 2026-07-26T04:18:34.1745792+00:00 | 216.0ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,944,000 | 2026-07-26T04:18:34.1748455+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,945,000 | 2026-07-26T04:18:34.1751135+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,946,000 | 2026-07-26T04:18:34.175692+00:00 | 213.9ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,947,000 | 2026-07-26T04:18:34.17645+00:00 | 218.0ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,948,000 | 2026-07-26T04:18:34.1768741+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,949,000 | 2026-07-26T04:18:34.1771299+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,950,000 | 2026-07-26T04:18:34.1773838+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,951,000 | 2026-07-26T04:18:34.1776468+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,066,952,000 | 2026-07-26T04:18:34.1781986+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 740.4s / 1,073,488 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,171,235,000 | 2026-07-26T04:19:49.3497588+00:00 | 222.9ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,237,000 | 2026-07-26T04:19:49.3507848+00:00 | 218.3ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,238,000 | 2026-07-26T04:19:49.3512396+00:00 | 219.1ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,244,000 | 2026-07-26T04:19:49.3566508+00:00 | 215.6ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,245,000 | 2026-07-26T04:19:49.3584088+00:00 | 223.6ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,246,000 | 2026-07-26T04:19:49.3587586+00:00 | 214.6ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,247,000 | 2026-07-26T04:19:49.3591142+00:00 | 214.2ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,248,000 | 2026-07-26T04:19:49.3594594+00:00 | 222.5ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,249,000 | 2026-07-26T04:19:49.3602848+00:00 | 213.1ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,250,000 | 2026-07-26T04:19:49.3606317+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,251,000 | 2026-07-26T04:19:49.3615234+00:00 | 220.4ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,252,000 | 2026-07-26T04:19:49.3618718+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,253,000 | 2026-07-26T04:19:49.3622233+00:00 | 211.1ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,254,000 | 2026-07-26T04:19:49.3625725+00:00 | 210.8ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,255,000 | 2026-07-26T04:19:49.3632639+00:00 | 218.7ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,256,000 | 2026-07-26T04:19:49.3637584+00:00 | 209.6ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,257,000 | 2026-07-26T04:19:49.3642516+00:00 | 210.8ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,258,000 | 2026-07-26T04:19:49.3649236+00:00 | 217.0ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,259,000 | 2026-07-26T04:19:49.3652421+00:00 | 208.1ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,260,000 | 2026-07-26T04:19:49.3655586+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,261,000 | 2026-07-26T04:19:49.3662401+00:00 | 215.8ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,262,000 | 2026-07-26T04:19:49.3667501+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,263,000 | 2026-07-26T04:19:49.367242+00:00 | 207.0ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,264,000 | 2026-07-26T04:19:49.3678897+00:00 | 214.1ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,171,265,000 | 2026-07-26T04:19:49.3682073+00:00 | 213.8ms | broker/backlog (no scale or GC event) | - | - | 816.5s / 1,323,379 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,181,777,000 | 2026-07-26T04:19:56.8821397+00:00 | 218.0ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,778,000 | 2026-07-26T04:19:56.8835243+00:00 | 220.2ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,782,000 | 2026-07-26T04:19:56.8860126+00:00 | 231.9ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,783,000 | 2026-07-26T04:19:56.8864435+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,784,000 | 2026-07-26T04:19:56.886878+00:00 | 218.8ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,785,000 | 2026-07-26T04:19:56.8872041+00:00 | 229.8ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,786,000 | 2026-07-26T04:19:56.8875435+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,787,000 | 2026-07-26T04:19:56.8878481+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,788,000 | 2026-07-26T04:19:56.888575+00:00 | 228.4ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,789,000 | 2026-07-26T04:19:56.8900897+00:00 | 223.0ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,790,000 | 2026-07-26T04:19:56.8905138+00:00 | 228.3ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,791,000 | 2026-07-26T04:19:56.890795+00:00 | 227.2ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,792,000 | 2026-07-26T04:19:56.8910822+00:00 | 227.0ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,793,000 | 2026-07-26T04:19:56.8913664+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,794,000 | 2026-07-26T04:19:56.8920731+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,795,000 | 2026-07-26T04:19:56.8926286+00:00 | 225.1ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,796,000 | 2026-07-26T04:19:56.8930454+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,797,000 | 2026-07-26T04:19:56.8933144+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,798,000 | 2026-07-26T04:19:56.8935791+00:00 | 224.2ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,799,000 | 2026-07-26T04:19:56.8938512+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,800,000 | 2026-07-26T04:19:56.8946845+00:00 | 226.3ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,801,000 | 2026-07-26T04:19:56.8951599+00:00 | 224.6ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,802,000 | 2026-07-26T04:19:56.8957273+00:00 | 224.0ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,803,000 | 2026-07-26T04:19:56.8960253+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,804,000 | 2026-07-26T04:19:56.8963177+00:00 | 220.7ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,805,000 | 2026-07-26T04:19:56.8966023+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,181,806,000 | 2026-07-26T04:19:56.8973021+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 823.5s / 1,285,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,485,000 | 2026-07-26T04:21:04.5112351+00:00 | 225.5ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,486,000 | 2026-07-26T04:21:04.5115608+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,487,000 | 2026-07-26T04:21:04.5120202+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,488,000 | 2026-07-26T04:21:04.5123337+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,489,000 | 2026-07-26T04:21:04.512773+00:00 | 218.5ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,490,000 | 2026-07-26T04:21:04.5141619+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,491,000 | 2026-07-26T04:21:04.5156778+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,492,000 | 2026-07-26T04:21:04.5161324+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,493,000 | 2026-07-26T04:21:04.5165668+00:00 | 215.7ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,494,000 | 2026-07-26T04:21:04.5168806+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,495,000 | 2026-07-26T04:21:04.5173379+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,496,000 | 2026-07-26T04:21:04.5177787+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,497,000 | 2026-07-26T04:21:04.5182313+00:00 | 214.5ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,498,000 | 2026-07-26T04:21:04.518787+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,499,000 | 2026-07-26T04:21:04.5192208+00:00 | 213.1ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,500,000 | 2026-07-26T04:21:04.5195212+00:00 | 221.1ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,501,000 | 2026-07-26T04:21:04.5199381+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,502,000 | 2026-07-26T04:21:04.5203795+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,503,000 | 2026-07-26T04:21:04.5208041+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,504,000 | 2026-07-26T04:21:04.521372+00:00 | 210.9ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,505,000 | 2026-07-26T04:21:04.5217829+00:00 | 218.9ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,506,000 | 2026-07-26T04:21:04.5220591+00:00 | 210.2ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,507,000 | 2026-07-26T04:21:04.5226604+00:00 | 213.5ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,508,000 | 2026-07-26T04:21:04.523206+00:00 | 217.4ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,290,509,000 | 2026-07-26T04:21:04.5235968+00:00 | 212.5ms | broker/backlog (no scale or GC event) | - | - | 891.5s / 1,286,382 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.28x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.09x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.09 | 1103.93 | 1,165,649 | 1,174,334 | +1.4% | +0.18% | 1111.65 | 1,165,649 | 0 | 1.27 |
| Dekaf | 1.21 | 1230.80 | 1,060,832 | 1,065,748 | +0.4% | +0.01% | 1011.69 | 1,060,832 | 0 | 1.29 |
| Confluent | 1.85 | - | 846,415 | 852,275 | +4.1% | +0.41% | 807.20 | 846,415 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 310,573 | 345.08 | 1009.07 KB |
| Dekaf | 2 | 318,137 | 353.48 | 1014.91 KB |
| Dekaf | 3 | 311,477 | 346.08 | 1004.32 KB |
| Dekaf (3conn) | 1 | 341,702 | 379.66 | 1000.74 KB |
| Dekaf (3conn) | 2 | 356,010 | 395.56 | 1007.10 KB |
| Dekaf (3conn) | 3 | 341,216 | 379.12 | 1003.39 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:14.3216853+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 746,450 msg/s |
| Dekaf | 2026-07-26T03:21:32.3310982+00:00 | 3 | 16.0 MiB / 3.9 MiB | 381.8 MB/s | 0/0 | 1,233 | 18.0s / 1,058,910 msg/s |
| Dekaf | 2026-07-26T03:21:51.3382898+00:00 | 1 | 16.0 MiB / 12.4 MiB | 409.1 MB/s | 0/0 | 7,608 | 37.0s / 1,131,375 msg/s |
| Dekaf | 2026-07-26T03:22:09.346893+00:00 | 1 | 16.0 MiB / 4.2 MiB | 409.1 MB/s | 0/1 | 9,820 | 55.0s / 1,114,999 msg/s |
| Dekaf | 2026-07-26T03:22:27.3572606+00:00 | 2 | 16.0 MiB / 10.1 MiB | 424.3 MB/s | 0/1 | 30,791 | 73.0s / 1,049,360 msg/s |
| Dekaf | 2026-07-26T03:22:45.3679401+00:00 | 2 | 16.0 MiB / 14.5 MiB | 424.3 MB/s | 0/2 | 36,959 | 91.1s / 1,049,770 msg/s |
| Dekaf | 2026-07-26T03:23:03.3708387+00:00 | 3 | 16.0 MiB / 6.3 MiB | 401.2 MB/s | 0/2 | 6,394 | 109.1s / 1,134,763 msg/s |
| Dekaf | 2026-07-26T03:23:21.3807902+00:00 | 3 | 16.0 MiB / 7.9 MiB | 416.1 MB/s | 0/3 | 6,767 | 127.1s / 1,060,803 msg/s |
| Dekaf | 2026-07-26T03:23:40.3967684+00:00 | 1 | 16.0 MiB / 5.6 MiB | 409.1 MB/s | 0/3 | 21,378 | 146.1s / 997,532 msg/s |
| Dekaf | 2026-07-26T03:23:58.4103389+00:00 | 1 | 14.0 MiB / 11.2 MiB | 409.1 MB/s | 0/3 | 23,930 | 164.1s / 1,055,576 msg/s |
| Dekaf | 2026-07-26T03:24:16.4189248+00:00 | 2 | 16.0 MiB / 15.4 MiB | 424.3 MB/s | 0/2 | 78,500 | 182.1s / 1,053,132 msg/s |
| Dekaf | 2026-07-26T03:24:34.4269252+00:00 | 2 | 16.0 MiB / 16.0 MiB | 424.3 MB/s | 0/2 | 85,366 | 200.1s / 1,025,398 msg/s |
| Dekaf | 2026-07-26T03:24:52.4326088+00:00 | 3 | 8.0 MiB / 6.2 MiB | 416.1 MB/s | 3/3 | 16,098 | 218.1s / 976,507 msg/s |
| Dekaf | 2026-07-26T03:25:10.4400815+00:00 | 3 | 10.0 MiB / 4.0 MiB | 416.1 MB/s | 3/4 | 19,092 | 236.2s / 1,067,876 msg/s |
| Dekaf | 2026-07-26T03:25:29.4506186+00:00 | 1 | 8.0 MiB / 8.0 MiB | 409.1 MB/s | 3/4 | 44,648 | 255.2s / 1,062,037 msg/s |
| Dekaf | 2026-07-26T03:25:47.4593199+00:00 | 1 | 9.0 MiB / 7.4 MiB | 409.1 MB/s | 4/4 | 47,317 | 273.2s / 1,080,994 msg/s |
| Dekaf | 2026-07-26T03:26:05.4640408+00:00 | 2 | 8.0 MiB / 3.8 MiB | 424.3 MB/s | 4/2 | 121,734 | 291.2s / 1,044,963 msg/s |
| Dekaf | 2026-07-26T03:26:23.4665697+00:00 | 2 | 8.0 MiB / 3.6 MiB | 424.3 MB/s | 4/3 | 130,504 | 309.2s / 1,058,523 msg/s |
| Dekaf | 2026-07-26T03:26:41.4747864+00:00 | 3 | 10.0 MiB / 7.6 MiB | 416.1 MB/s | 3/5 | 28,439 | 327.2s / 1,110,081 msg/s |
| Dekaf | 2026-07-26T03:26:59.4804402+00:00 | 3 | 10.0 MiB / 7.6 MiB | 416.1 MB/s | 3/5 | 29,619 | 345.2s / 1,069,665 msg/s |
| Dekaf | 2026-07-26T03:27:18.4859978+00:00 | 1 | 8.0 MiB / 7.1 MiB | 409.1 MB/s | 4/6 | 71,037 | 364.2s / 1,092,277 msg/s |
| Dekaf | 2026-07-26T03:27:36.4925457+00:00 | 1 | 8.0 MiB / 2.7 MiB | 409.1 MB/s | 4/6 | 75,895 | 382.2s / 1,097,938 msg/s |
| Dekaf | 2026-07-26T03:27:54.4969329+00:00 | 2 | 8.0 MiB / 3.6 MiB | 424.3 MB/s | 4/4 | 167,240 | 400.2s / 1,094,069 msg/s |
| Dekaf | 2026-07-26T03:28:12.5017122+00:00 | 2 | 8.0 MiB / 2.2 MiB | 424.3 MB/s | 4/4 | 172,275 | 418.2s / 1,095,090 msg/s |
| Dekaf | 2026-07-26T03:28:30.5026889+00:00 | 3 | 8.0 MiB / 7.1 MiB | 416.1 MB/s | 4/6 | 41,762 | 436.2s / 1,106,407 msg/s |
| Dekaf | 2026-07-26T03:28:48.50555+00:00 | 3 | 8.0 MiB / 7.1 MiB | 416.1 MB/s | 4/6 | 45,553 | 454.2s / 1,056,338 msg/s |
| Dekaf | 2026-07-26T03:29:07.5086273+00:00 | 1 | 8.0 MiB / 5.6 MiB | 409.1 MB/s | 5/7 | 102,295 | 473.2s / 1,064,647 msg/s |
| Dekaf | 2026-07-26T03:29:25.5156096+00:00 | 1 | 7.0 MiB / 4.6 MiB | 409.1 MB/s | 5/8 | 107,549 | 491.3s / 1,114,396 msg/s |
| Dekaf | 2026-07-26T03:29:43.5166235+00:00 | 2 | 7.0 MiB / 5.4 MiB | 424.3 MB/s | 5/5 | 200,040 | 509.3s / 1,105,623 msg/s |
| Dekaf | 2026-07-26T03:30:01.5246805+00:00 | 2 | 6.0 MiB / 4.2 MiB | 424.3 MB/s | 5/5 | 205,724 | 527.3s / 1,033,704 msg/s |
| Dekaf | 2026-07-26T03:30:19.5346437+00:00 | 3 | 7.0 MiB / 6.7 MiB | 416.1 MB/s | 5/8 | 60,947 | 545.3s / 1,025,338 msg/s |
| Dekaf | 2026-07-26T03:30:37.5397288+00:00 | 3 | 7.0 MiB / 7.0 MiB | 416.1 MB/s | 5/8 | 62,512 | 563.3s / 1,033,423 msg/s |
| Dekaf | 2026-07-26T03:30:56.5472377+00:00 | 1 | 7.0 MiB / 3.4 MiB | 409.1 MB/s | 5/10 | 124,640 | 582.3s / 1,094,947 msg/s |
| Dekaf | 2026-07-26T03:31:14.5508812+00:00 | 1 | 7.0 MiB / 5.8 MiB | 409.1 MB/s | 5/10 | 128,353 | 600.3s / 1,084,236 msg/s |
| Dekaf | 2026-07-26T03:31:32.553096+00:00 | 2 | 6.0 MiB / 5.4 MiB | 424.3 MB/s | 5/7 | 230,302 | 618.3s / 1,056,462 msg/s |
| Dekaf | 2026-07-26T03:31:50.5591614+00:00 | 2 | 7.0 MiB / 6.6 MiB | 424.3 MB/s | 5/8 | 235,776 | 636.3s / 1,023,787 msg/s |
| Dekaf | 2026-07-26T03:32:08.5670053+00:00 | 3 | 7.0 MiB / 2.9 MiB | 416.1 MB/s | 5/8 | 76,912 | 654.3s / 1,028,690 msg/s |
| Dekaf | 2026-07-26T03:32:26.57312+00:00 | 3 | 7.0 MiB / 6.9 MiB | 416.1 MB/s | 5/8 | 80,242 | 672.3s / 1,051,060 msg/s |
| Dekaf | 2026-07-26T03:32:45.5766155+00:00 | 1 | 7.0 MiB / 2.7 MiB | 409.1 MB/s | 5/11 | 150,816 | 691.3s / 1,074,007 msg/s |
| Dekaf | 2026-07-26T03:33:03.5838616+00:00 | 1 | 7.0 MiB / 4.6 MiB | 409.1 MB/s | 5/11 | 155,133 | 709.3s / 1,063,584 msg/s |
| Dekaf | 2026-07-26T03:33:21.5884515+00:00 | 2 | 7.0 MiB / 4.4 MiB | 424.3 MB/s | 5/8 | 265,123 | 727.3s / 1,086,648 msg/s |
| Dekaf | 2026-07-26T03:33:39.6027347+00:00 | 2 | 7.0 MiB / 6.9 MiB | 424.3 MB/s | 5/8 | 272,253 | 745.4s / 1,050,897 msg/s |
| Dekaf | 2026-07-26T03:33:57.6129536+00:00 | 3 | 7.0 MiB / 6.7 MiB | 416.1 MB/s | 5/9 | 96,708 | 763.4s / 1,096,408 msg/s |
| Dekaf | 2026-07-26T03:34:15.6140017+00:00 | 3 | 7.0 MiB / 6.2 MiB | 416.1 MB/s | 5/9 | 100,598 | 781.4s / 1,114,141 msg/s |
| Dekaf | 2026-07-26T03:34:34.6226418+00:00 | 1 | 7.0 MiB / 6.2 MiB | 409.1 MB/s | 5/11 | 178,771 | 800.4s / 1,078,822 msg/s |
| Dekaf | 2026-07-26T03:34:52.6301582+00:00 | 1 | 7.0 MiB / 5.2 MiB | 409.1 MB/s | 5/11 | 184,214 | 818.4s / 1,104,078 msg/s |
| Dekaf | 2026-07-26T03:35:10.6347619+00:00 | 2 | 7.0 MiB / 6.4 MiB | 424.3 MB/s | 5/8 | 304,485 | 836.4s / 1,018,793 msg/s |
| Dekaf | 2026-07-26T03:35:28.6415589+00:00 | 2 | 7.0 MiB / 6.3 MiB | 424.3 MB/s | 5/8 | 311,652 | 854.4s / 1,066,536 msg/s |
| Dekaf | 2026-07-26T03:35:46.6421465+00:00 | 3 | 7.0 MiB / 6.4 MiB | 416.1 MB/s | 5/9 | 122,588 | 872.4s / 1,065,829 msg/s |
| Dekaf | 2026-07-26T03:36:04.6461613+00:00 | 3 | 7.0 MiB / 4.1 MiB | 416.1 MB/s | 5/9 | 126,439 | 890.4s / 1,098,732 msg/s |
| Dekaf (3conn) | 2026-07-26T03:36:35.9064758+00:00 | 3 | 16.0 MiB / 4.1 MiB | 322.8 MB/s | 0/0 | 1,205 | 9.0s / 800,128 msg/s |
| Dekaf (3conn) | 2026-07-26T03:36:53.9163345+00:00 | 3 | 16.0 MiB / 11.7 MiB | 414.6 MB/s | 0/0 | 2,495 | 27.0s / 1,173,872 msg/s |
| Dekaf (3conn) | 2026-07-26T03:37:12.9241005+00:00 | 1 | 16.0 MiB / 2.1 MiB | 419.9 MB/s | 0/1 | 7,684 | 46.0s / 1,174,545 msg/s |
| Dekaf (3conn) | 2026-07-26T03:37:30.9380879+00:00 | 1 | 16.0 MiB / 5.4 MiB | 428.6 MB/s | 0/1 | 9,008 | 64.1s / 1,192,326 msg/s |
| Dekaf (3conn) | 2026-07-26T03:37:48.9483513+00:00 | 2 | 16.0 MiB / 5.2 MiB | 474.0 MB/s | 0/1 | 18,047 | 82.1s / 1,154,997 msg/s |
| Dekaf (3conn) | 2026-07-26T03:38:06.9613888+00:00 | 2 | 14.0 MiB / 13.4 MiB | 474.0 MB/s | 1/1 | 22,164 | 100.1s / 1,181,763 msg/s |
| Dekaf (3conn) | 2026-07-26T03:38:24.9736705+00:00 | 3 | 14.0 MiB / 10.0 MiB | 439.2 MB/s | 1/2 | 13,117 | 118.1s / 1,200,976 msg/s |
| Dekaf (3conn) | 2026-07-26T03:38:42.9849077+00:00 | 3 | 14.0 MiB / 2.0 MiB | 439.2 MB/s | 1/2 | 15,733 | 136.1s / 1,182,118 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:01.9878151+00:00 | 1 | 14.0 MiB / 14.0 MiB | 441.7 MB/s | 1/3 | 20,409 | 155.1s / 1,120,282 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:19.9956457+00:00 | 1 | 12.0 MiB / 11.2 MiB | 441.7 MB/s | 1/3 | 22,912 | 173.1s / 1,182,419 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:38.0076254+00:00 | 2 | 14.0 MiB / 3.7 MiB | 474.0 MB/s | 1/3 | 41,131 | 191.1s / 1,167,760 msg/s |
| Dekaf (3conn) | 2026-07-26T03:39:56.0250095+00:00 | 2 | 12.0 MiB / 8.1 MiB | 474.0 MB/s | 2/3 | 46,892 | 209.1s / 1,259,930 msg/s |
| Dekaf (3conn) | 2026-07-26T03:40:14.0369863+00:00 | 3 | 12.0 MiB / 2.3 MiB | 455.2 MB/s | 1/4 | 28,980 | 227.2s / 1,264,467 msg/s |
| Dekaf (3conn) | 2026-07-26T03:40:32.0437361+00:00 | 3 | 10.0 MiB / 2.2 MiB | 455.2 MB/s | 2/4 | 32,102 | 245.2s / 1,153,673 msg/s |
| Dekaf (3conn) | 2026-07-26T03:40:51.0520896+00:00 | 1 | 14.0 MiB / 4.7 MiB | 443.7 MB/s | 1/5 | 30,950 | 264.2s / 1,193,976 msg/s |
| Dekaf (3conn) | 2026-07-26T03:41:09.0541767+00:00 | 1 | 14.0 MiB / 9.2 MiB | 443.7 MB/s | 1/5 | 32,050 | 282.2s / 1,209,284 msg/s |
| Dekaf (3conn) | 2026-07-26T03:41:27.0696623+00:00 | 2 | 12.0 MiB / 7.9 MiB | 474.0 MB/s | 2/5 | 65,128 | 300.2s / 1,179,730 msg/s |
| Dekaf (3conn) | 2026-07-26T03:41:45.0816346+00:00 | 2 | 12.0 MiB / 11.3 MiB | 474.0 MB/s | 2/5 | 68,554 | 318.2s / 1,082,908 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:03.0876857+00:00 | 3 | 6.0 MiB / 2.7 MiB | 455.2 MB/s | 6/4 | 60,002 | 336.3s / 1,145,251 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:21.0906264+00:00 | 3 | 5.0 MiB / 3.9 MiB | 455.2 MB/s | 7/4 | 69,290 | 354.3s / 1,121,298 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:40.0964489+00:00 | 1 | 14.0 MiB / 7.5 MiB | 443.7 MB/s | 1/5 | 35,810 | 373.3s / 1,123,936 msg/s |
| Dekaf (3conn) | 2026-07-26T03:42:58.1031878+00:00 | 1 | 14.0 MiB / 11.2 MiB | 443.7 MB/s | 1/5 | 36,363 | 391.3s / 1,193,926 msg/s |
| Dekaf (3conn) | 2026-07-26T03:43:16.1136798+00:00 | 2 | 10.0 MiB / 5.4 MiB | 474.0 MB/s | 3/5 | 80,999 | 409.3s / 1,207,448 msg/s |
| Dekaf (3conn) | 2026-07-26T03:43:34.1201766+00:00 | 2 | 10.0 MiB / 3.5 MiB | 474.0 MB/s | 3/6 | 84,203 | 427.3s / 1,224,129 msg/s |
| Dekaf (3conn) | 2026-07-26T03:43:52.1243892+00:00 | 3 | 6.0 MiB / 4.4 MiB | 455.2 MB/s | 7/6 | 112,769 | 445.3s / 1,235,334 msg/s |
| Dekaf (3conn) | 2026-07-26T03:44:10.1288552+00:00 | 3 | 6.0 MiB / 5.4 MiB | 455.2 MB/s | 7/6 | 121,158 | 463.3s / 1,146,926 msg/s |
| Dekaf (3conn) | 2026-07-26T03:44:29.1414315+00:00 | 1 | 12.0 MiB / 3.8 MiB | 443.7 MB/s | 2/5 | 38,395 | 482.3s / 1,080,400 msg/s |
| Dekaf (3conn) | 2026-07-26T03:44:47.1434122+00:00 | 1 | 12.0 MiB / 4.6 MiB | 443.7 MB/s | 2/6 | 39,290 | 500.3s / 1,179,004 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:05.154492+00:00 | 2 | 8.0 MiB / 4.3 MiB | 474.0 MB/s | 4/7 | 104,216 | 518.3s / 1,142,380 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:23.1606156+00:00 | 2 | 8.0 MiB / 5.5 MiB | 474.0 MB/s | 4/7 | 108,898 | 536.4s / 1,189,238 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:41.1684592+00:00 | 3 | 7.0 MiB / 4.8 MiB | 455.2 MB/s | 8/7 | 153,645 | 554.4s / 1,150,703 msg/s |
| Dekaf (3conn) | 2026-07-26T03:45:59.1756734+00:00 | 3 | 7.0 MiB / 2.6 MiB | 455.2 MB/s | 8/7 | 159,977 | 572.4s / 1,121,999 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:18.1855633+00:00 | 1 | 12.0 MiB / 8.9 MiB | 443.7 MB/s | 2/7 | 42,744 | 591.4s / 1,141,327 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:36.1932179+00:00 | 1 | 12.0 MiB / 3.1 MiB | 443.7 MB/s | 2/7 | 43,678 | 609.4s / 1,157,875 msg/s |
| Dekaf (3conn) | 2026-07-26T03:46:54.201606+00:00 | 2 | 9.0 MiB / 6.2 MiB | 474.0 MB/s | 4/8 | 131,741 | 627.4s / 1,047,086 msg/s |
| Dekaf (3conn) | 2026-07-26T03:47:12.2133731+00:00 | 2 | 9.0 MiB / 4.9 MiB | 474.0 MB/s | 5/8 | 134,945 | 645.4s / 1,205,302 msg/s |
| Dekaf (3conn) | 2026-07-26T03:47:30.2298191+00:00 | 3 | 6.0 MiB / 5.8 MiB | 455.2 MB/s | 9/8 | 199,180 | 663.4s / 1,131,777 msg/s |
| Dekaf (3conn) | 2026-07-26T03:47:48.2358593+00:00 | 3 | 6.0 MiB / 5.6 MiB | 455.2 MB/s | 9/9 | 206,573 | 681.4s / 1,151,961 msg/s |
| Dekaf (3conn) | 2026-07-26T03:48:07.2476865+00:00 | 1 | 12.0 MiB / 3.2 MiB | 443.7 MB/s | 2/8 | 47,628 | 700.4s / 1,205,074 msg/s |
| Dekaf (3conn) | 2026-07-26T03:48:25.2551896+00:00 | 1 | 10.0 MiB / 5.1 MiB | 443.7 MB/s | 3/8 | 49,596 | 718.4s / 1,145,029 msg/s |
| Dekaf (3conn) | 2026-07-26T03:48:43.2640945+00:00 | 2 | 8.0 MiB / 5.8 MiB | 474.0 MB/s | 5/9 | 151,610 | 736.4s / 1,113,141 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:01.2738042+00:00 | 2 | 7.0 MiB / 7.0 MiB | 474.0 MB/s | 6/9 | 155,770 | 754.4s / 1,154,826 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:19.285372+00:00 | 3 | 7.0 MiB / 7.0 MiB | 455.2 MB/s | 10/10 | 239,782 | 772.4s / 1,146,018 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:37.2974006+00:00 | 3 | 7.0 MiB / 7.0 MiB | 455.2 MB/s | 10/10 | 245,836 | 790.5s / 1,161,246 msg/s |
| Dekaf (3conn) | 2026-07-26T03:49:56.3039025+00:00 | 1 | 11.0 MiB / 7.7 MiB | 443.7 MB/s | 3/9 | 57,132 | 809.5s / 1,228,296 msg/s |
| Dekaf (3conn) | 2026-07-26T03:50:14.3068631+00:00 | 1 | 11.0 MiB / 5.4 MiB | 443.7 MB/s | 4/9 | 58,657 | 827.5s / 1,198,313 msg/s |
| Dekaf (3conn) | 2026-07-26T03:50:32.310646+00:00 | 2 | 7.0 MiB / 6.4 MiB | 474.0 MB/s | 6/11 | 180,192 | 845.5s / 1,148,656 msg/s |
| Dekaf (3conn) | 2026-07-26T03:50:50.3186485+00:00 | 2 | 8.0 MiB / 1.9 MiB | 474.0 MB/s | 7/11 | 185,557 | 863.5s / 1,121,750 msg/s |
| Dekaf (3conn) | 2026-07-26T03:51:08.3257648+00:00 | 3 | 8.0 MiB / 8.0 MiB | 455.2 MB/s | 11/11 | 267,828 | 881.5s / 1,224,992 msg/s |
| Dekaf (3conn) | 2026-07-26T03:51:26.3329559+00:00 | 3 | 8.0 MiB / 4.7 MiB | 455.2 MB/s | 11/11 | 270,621 | 899.5s / 1,262,899 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-26T03:21:44.4407454+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-26T03:21:44.5078035+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 14.9 MiB |
| Dekaf | 2026-07-26T03:21:59.5480583+00:00 | 1 | capacity | failed | 15,051ms | 16.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-26T03:22:29.6191812+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 14.7 MiB |
| Dekaf | 2026-07-26T03:22:29.6548617+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-26T03:22:44.6979906+00:00 | 1 | capacity | failed | 15,043ms | 16.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-26T03:23:14.7625624+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-26T03:23:16.2647772+00:00 | 3 | capacity | failed | 1,502ms | 16.0 MiB / 9.6 MiB |
| Dekaf | 2026-07-26T03:23:46.3617522+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 7.4 MiB |
| Dekaf | 2026-07-26T03:24:01.4112999+00:00 | 3 | capacity | succeeded | 15,049ms | 14.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-26T03:24:04.4275306+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-26T03:24:19.5146576+00:00 | 3 | capacity | succeeded | 15,087ms | 12.0 MiB / 9.5 MiB |
| Dekaf | 2026-07-26T03:24:22.5227796+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-26T03:24:37.5715897+00:00 | 3 | capacity | succeeded | 15,048ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:24:40.588696+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 8.4 MiB |
| Dekaf | 2026-07-26T03:24:45.1627148+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-26T03:24:55.6833272+00:00 | 1 | capacity | failed | 15,044ms | 10.0 MiB / 6.3 MiB |
| Dekaf | 2026-07-26T03:25:18.2575328+00:00 | 2 | capacity | succeeded | 15,043ms | 12.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-26T03:25:25.797745+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-26T03:25:39.3242201+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-26T03:25:43.8578891+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-26T03:25:55.8383525+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-26T03:25:57.8427895+00:00 | 3 | capacity | failed | 2,004ms | 10.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-26T03:26:12.4469227+00:00 | 2 | capacity | failed | 15,069ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:26:57.6167175+00:00 | 2 | capacity | failed | 15,052ms | 8.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-26T03:27:00.6487294+00:00 | 1 | capacity | failed | 1,505ms | 8.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-26T03:27:45.8021312+00:00 | 1 | capacity | succeeded | 15,046ms | 7.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-26T03:27:58.2601331+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.0 MiB |
| Dekaf | 2026-07-26T03:28:13.3101936+00:00 | 3 | capacity | succeeded | 15,050ms | 8.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-26T03:28:28.8511419+00:00 | 3 | capacity | failed | 12,532ms | 8.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-26T03:28:58.9873586+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-26T03:29:13.0404844+00:00 | 2 | capacity | succeeded | 15,046ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:29:16.0484474+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-26T03:29:19.142385+00:00 | 1 | capacity | failed | 15,049ms | 7.0 MiB / 6.9 MiB |
| Dekaf | 2026-07-26T03:29:32.0921689+00:00 | 3 | capacity | failed | 15,050ms | 7.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-26T03:30:01.2167221+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:30:04.2984015+00:00 | 1 | capacity | failed | 15,060ms | 7.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-26T03:30:17.2253288+00:00 | 3 | capacity | failed | 15,043ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:30:46.3674061+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:31:01.4805751+00:00 | 2 | capacity | failed | 15,113ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-26T03:31:31.575242+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-26T03:31:46.6247994+00:00 | 2 | capacity | failed | 15,050ms | 7.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-26T03:32:32.6981571+00:00 | 3 | capacity | failed | 15,050ms | 7.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-26T03:35:47.3751209+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.8 MiB |
| Dekaf | 2026-07-26T03:36:02.4809201+00:00 | 2 | capacity | failed | 15,105ms | 7.0 MiB / 7.0 MiB |
| Dekaf (3conn) | 2026-07-26T03:36:57.0905716+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:37:12.1211922+00:00 | 2 | capacity | failed | 15,071ms | 16.0 MiB / 10.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:37:12.158155+00:00 | 3 | capacity | failed | 15,067ms | 16.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:37:42.3163179+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:37:57.3483229+00:00 | 2 | capacity | succeeded | 15,055ms | 14.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:00.3630614+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:00.4077468+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:15.4440654+00:00 | 1 | capacity | failed | 15,058ms | 14.0 MiB / 9.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:45.5351645+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 10.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:45.6082523+00:00 | 3 | capacity | started | 0ms | 15.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:38:47.1229176+00:00 | 3 | capacity | failed | 1,514ms | 14.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:17.1837481+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 7.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:30.6912122+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:32.3373225+00:00 | 3 | capacity | failed | 15,101ms | 14.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:39:48.8368687+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:02.4514306+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:04.3712075+00:00 | 1 | capacity | failed | 2,003ms | 14.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:20.5374757+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:35.5901665+00:00 | 3 | capacity | succeeded | 15,052ms | 10.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:49.0560799+00:00 | 2 | capacity | failed | 15,056ms | 12.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:40:56.6532195+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 6.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:41:41.8648161+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-26T03:41:59.9613454+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:42:18.038686+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-26T03:42:49.5000226+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:43:07.554342+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:43:33.3635904+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:05.4207729+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:20.465949+00:00 | 1 | capacity | succeeded | 15,045ms | 12.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:23.4738186+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:37.8928255+00:00 | 2 | capacity | succeeded | 15,035ms | 8.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:44:40.9097149+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:03.7153966+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:18.8162266+00:00 | 3 | capacity | failed | 15,100ms | 7.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:45:48.9097258+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:03.9609016+00:00 | 3 | capacity | succeeded | 15,051ms | 6.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:11.2203602+00:00 | 2 | capacity | failed | 15,049ms | 8.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-26T03:46:41.3741613+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:23.2979013+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:37.3172725+00:00 | 3 | capacity | failed | 15,102ms | 6.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:47:41.6171577+00:00 | 2 | capacity | failed | 15,070ms | 9.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:08.4453681+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 6.3 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:23.4970304+00:00 | 1 | capacity | succeeded | 15,051ms | 10.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:41.5516192+00:00 | 1 | capacity | failed | 15,051ms | 10.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:52.5842354+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:48:59.9201905+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-07-26T03:49:14.9741424+00:00 | 2 | capacity | failed | 15,053ms | 8.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:49:45.0827422+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:00.1727709+00:00 | 2 | capacity | failed | 15,090ms | 8.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:22.948649+00:00 | 3 | capacity | succeeded | 15,058ms | 8.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:30.3205192+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:45.0665417+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:50:48.3923146+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-26T03:51:00.1161367+00:00 | 1 | capacity | failed | 15,049ms | 9.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-07-26T03:51:08.1159876+00:00 | 3 | capacity | failed | 15,046ms | 8.0 MiB / 3.6 MiB |
*102 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 8 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 5 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 46 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 135 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 390 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 533 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 616 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 821 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 1,414 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 2,424 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 2,948 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 2,486 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 1,042 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 333 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 93 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 5 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 33 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 30 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 131 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 467 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 1,194 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 1,665 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 2,114 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 2,594 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 4,809 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 7,522 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 9,040 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 6,936 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 2,819 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 776 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 279 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 13 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 36 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 45 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 173 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 626 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 1,490 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 2,272 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 2,775 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 3,186 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 6,021 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 9,274 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 11,679 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 9,677 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 3,998 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 1,088 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 377 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 25 |
| Dekaf (3conn) | 3 | 65.536–131.072ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 45 |
| Dekaf | 1 | 0.002–0.004ms | 51 |
| Dekaf | 1 | 0.004–0.008ms | 199 |
| Dekaf | 1 | 0.008–0.016ms | 706 |
| Dekaf | 1 | 0.016–0.032ms | 1,653 |
| Dekaf | 1 | 0.032–0.064ms | 2,329 |
| Dekaf | 1 | 0.064–0.128ms | 2,951 |
| Dekaf | 1 | 0.128–0.256ms | 4,725 |
| Dekaf | 1 | 0.256–0.512ms | 8,853 |
| Dekaf | 1 | 0.512–1.024ms | 11,916 |
| Dekaf | 1 | 1.024–2.048ms | 10,693 |
| Dekaf | 1 | 2.048–4.096ms | 5,492 |
| Dekaf | 1 | 4.096–8.192ms | 2,115 |
| Dekaf | 1 | 8.192–16.384ms | 798 |
| Dekaf | 1 | 16.384–32.768ms | 282 |
| Dekaf | 1 | 32.768–65.536ms | 20 |
| Dekaf | 2 | 0.001–0.002ms | 69 |
| Dekaf | 2 | 0.002–0.004ms | 78 |
| Dekaf | 2 | 0.004–0.008ms | 298 |
| Dekaf | 2 | 0.008–0.016ms | 1,084 |
| Dekaf | 2 | 0.016–0.032ms | 2,703 |
| Dekaf | 2 | 0.032–0.064ms | 3,847 |
| Dekaf | 2 | 0.064–0.128ms | 4,813 |
| Dekaf | 2 | 0.128–0.256ms | 7,836 |
| Dekaf | 2 | 0.256–0.512ms | 14,820 |
| Dekaf | 2 | 0.512–1.024ms | 20,828 |
| Dekaf | 2 | 1.024–2.048ms | 17,569 |
| Dekaf | 2 | 2.048–4.096ms | 8,656 |
| Dekaf | 2 | 4.096–8.192ms | 2,845 |
| Dekaf | 2 | 8.192–16.384ms | 730 |
| Dekaf | 2 | 16.384–32.768ms | 218 |
| Dekaf | 2 | 32.768–65.536ms | 14 |
| Dekaf | 3 | 0.001–0.002ms | 30 |
| Dekaf | 3 | 0.002–0.004ms | 46 |
| Dekaf | 3 | 0.004–0.008ms | 156 |
| Dekaf | 3 | 0.008–0.016ms | 498 |
| Dekaf | 3 | 0.016–0.032ms | 1,118 |
| Dekaf | 3 | 0.032–0.064ms | 1,552 |
| Dekaf | 3 | 0.064–0.128ms | 2,115 |
| Dekaf | 3 | 0.128–0.256ms | 3,261 |
| Dekaf | 3 | 0.256–0.512ms | 5,655 |
| Dekaf | 3 | 0.512–1.024ms | 7,772 |
| Dekaf | 3 | 1.024–2.048ms | 6,671 |
| Dekaf | 3 | 2.048–4.096ms | 3,228 |
| Dekaf | 3 | 4.096–8.192ms | 1,257 |
| Dekaf | 3 | 8.192–16.384ms | 404 |
| Dekaf | 3 | 16.384–32.768ms | 129 |
| Dekaf | 3 | 32.768–65.536ms | 9 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 16,000 | 2026-07-26T03:06:14.2482315+00:00 | 120.9ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 17,000 | 2026-07-26T03:06:14.2530891+00:00 | 100.9ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 19,000 | 2026-07-26T03:06:14.2622405+00:00 | 107.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 21,000 | 2026-07-26T03:06:14.2670509+00:00 | 112.8ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 25,000 | 2026-07-26T03:06:14.2813191+00:00 | 143.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 26,000 | 2026-07-26T03:06:14.282288+00:00 | 142.8ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 27,000 | 2026-07-26T03:06:14.2832316+00:00 | 114.4ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 28,000 | 2026-07-26T03:06:14.2875411+00:00 | 110.1ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 29,000 | 2026-07-26T03:06:14.2884895+00:00 | 153.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 31,000 | 2026-07-26T03:06:14.2943006+00:00 | 103.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 33,000 | 2026-07-26T03:06:14.2998179+00:00 | 111.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 35,000 | 2026-07-26T03:06:14.3079972+00:00 | 157.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 36,000 | 2026-07-26T03:06:14.3089977+00:00 | 156.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 37,000 | 2026-07-26T03:06:14.31154+00:00 | 113.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 38,000 | 2026-07-26T03:06:14.3130907+00:00 | 136.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 39,000 | 2026-07-26T03:06:14.3174019+00:00 | 170.8ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 40,000 | 2026-07-26T03:06:14.318799+00:00 | 129.4ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 41,000 | 2026-07-26T03:06:14.3266563+00:00 | 123.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 42,000 | 2026-07-26T03:06:14.3290987+00:00 | 112.8ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 43,000 | 2026-07-26T03:06:14.3332977+00:00 | 115.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 45,000 | 2026-07-26T03:06:14.3395396+00:00 | 179.9ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 46,000 | 2026-07-26T03:06:14.340511+00:00 | 179.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 47,000 | 2026-07-26T03:06:14.3420669+00:00 | 120.8ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 48,000 | 2026-07-26T03:06:14.3433966+00:00 | 119.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 49,000 | 2026-07-26T03:06:14.3479626+00:00 | 171.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 50,000 | 2026-07-26T03:06:14.3495284+00:00 | 127.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 51,000 | 2026-07-26T03:06:14.3547008+00:00 | 132.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 52,000 | 2026-07-26T03:06:14.3559941+00:00 | 120.9ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 53,000 | 2026-07-26T03:06:14.3571551+00:00 | 161.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 55,000 | 2026-07-26T03:06:14.3679048+00:00 | 157.4ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 56,000 | 2026-07-26T03:06:14.3688465+00:00 | 156.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 57,000 | 2026-07-26T03:06:14.3737627+00:00 | 151.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 58,000 | 2026-07-26T03:06:14.3747436+00:00 | 150.8ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 59,000 | 2026-07-26T03:06:14.3759591+00:00 | 222.3ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 60,000 | 2026-07-26T03:06:14.3806073+00:00 | 148.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 61,000 | 2026-07-26T03:06:14.3815751+00:00 | 217.1ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 62,000 | 2026-07-26T03:06:14.3831439+00:00 | 136.3ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 63,000 | 2026-07-26T03:06:14.3846606+00:00 | 228.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 64,000 | 2026-07-26T03:06:14.3903731+00:00 | 129.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 65,000 | 2026-07-26T03:06:14.3913471+00:00 | 220.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 66,000 | 2026-07-26T03:06:14.3955398+00:00 | 216.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 67,000 | 2026-07-26T03:06:14.3967402+00:00 | 248.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 68,000 | 2026-07-26T03:06:14.4009987+00:00 | 244.3ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 69,000 | 2026-07-26T03:06:14.4021951+00:00 | 210.3ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 70,000 | 2026-07-26T03:06:14.4114838+00:00 | 234.1ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 71,000 | 2026-07-26T03:06:14.4127802+00:00 | 232.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 72,000 | 2026-07-26T03:06:14.4137516+00:00 | 197.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 73,000 | 2026-07-26T03:06:14.4152488+00:00 | 230.4ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 74,000 | 2026-07-26T03:06:14.4197599+00:00 | 178.9ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 75,000 | 2026-07-26T03:06:14.4261403+00:00 | 218.9ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 76,000 | 2026-07-26T03:06:14.4270914+00:00 | 218.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 77,000 | 2026-07-26T03:06:14.4285049+00:00 | 219.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 78,000 | 2026-07-26T03:06:14.4319545+00:00 | 215.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 79,000 | 2026-07-26T03:06:14.4446563+00:00 | 200.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 80,000 | 2026-07-26T03:06:14.445608+00:00 | 224.5ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 81,000 | 2026-07-26T03:06:14.4464797+00:00 | 216.2ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 82,000 | 2026-07-26T03:06:14.448111+00:00 | 197.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 83,000 | 2026-07-26T03:06:14.4548084+00:00 | 215.4ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 84,000 | 2026-07-26T03:06:14.4980763+00:00 | 146.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 85,000 | 2026-07-26T03:06:14.4991618+00:00 | 186.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 86,000 | 2026-07-26T03:06:14.5157793+00:00 | 170.0ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 87,000 | 2026-07-26T03:06:14.5169846+00:00 | 159.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 88,000 | 2026-07-26T03:06:14.5180253+00:00 | 158.6ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 89,000 | 2026-07-26T03:06:14.5305028+00:00 | 177.3ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 99,000 | 2026-07-26T03:06:14.6875394+00:00 | 122.7ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 129,000 | 2026-07-26T03:06:14.9897031+00:00 | 103.1ms | GC pause | - | - | 1.0s / 157,019 msg/s | Gen2 +0 / pause +115.9ms |
| Confluent | 213,000 | 2026-07-26T03:06:15.3982082+00:00 | 104.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 223,000 | 2026-07-26T03:06:15.4150794+00:00 | 105.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 227,000 | 2026-07-26T03:06:15.425642+00:00 | 103.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 228,000 | 2026-07-26T03:06:15.4263543+00:00 | 102.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 230,000 | 2026-07-26T03:06:15.429087+00:00 | 110.8ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 233,000 | 2026-07-26T03:06:15.4348405+00:00 | 105.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 240,000 | 2026-07-26T03:06:15.4475801+00:00 | 150.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 241,000 | 2026-07-26T03:06:15.4483069+00:00 | 122.5ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 243,000 | 2026-07-26T03:06:15.4550447+00:00 | 143.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 247,000 | 2026-07-26T03:06:15.4584784+00:00 | 155.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 248,000 | 2026-07-26T03:06:15.4642817+00:00 | 149.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 250,000 | 2026-07-26T03:06:15.4660547+00:00 | 154.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 251,000 | 2026-07-26T03:06:15.4703882+00:00 | 158.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 253,000 | 2026-07-26T03:06:15.4718331+00:00 | 160.8ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 257,000 | 2026-07-26T03:06:15.4788881+00:00 | 158.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 258,000 | 2026-07-26T03:06:15.479573+00:00 | 157.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 260,000 | 2026-07-26T03:06:15.4843092+00:00 | 156.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 261,000 | 2026-07-26T03:06:15.4849452+00:00 | 152.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 263,000 | 2026-07-26T03:06:15.4919178+00:00 | 149.2ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 265,000 | 2026-07-26T03:06:15.4939667+00:00 | 108.6ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 266,000 | 2026-07-26T03:06:15.4946233+00:00 | 107.9ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 267,000 | 2026-07-26T03:06:15.4973086+00:00 | 208.6ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 268,000 | 2026-07-26T03:06:15.4994899+00:00 | 215.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 269,000 | 2026-07-26T03:06:15.5007636+00:00 | 101.9ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 270,000 | 2026-07-26T03:06:15.5040352+00:00 | 211.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 271,000 | 2026-07-26T03:06:15.5077864+00:00 | 207.2ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 273,000 | 2026-07-26T03:06:15.5091657+00:00 | 206.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 275,000 | 2026-07-26T03:06:15.515919+00:00 | 116.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 276,000 | 2026-07-26T03:06:15.5166649+00:00 | 190.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 277,000 | 2026-07-26T03:06:15.5173007+00:00 | 198.2ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 278,000 | 2026-07-26T03:06:15.5180041+00:00 | 197.5ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 279,000 | 2026-07-26T03:06:15.5251139+00:00 | 189.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 280,000 | 2026-07-26T03:06:15.5257332+00:00 | 193.9ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 281,000 | 2026-07-26T03:06:15.5264709+00:00 | 203.6ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 283,000 | 2026-07-26T03:06:15.5411793+00:00 | 194.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 285,000 | 2026-07-26T03:06:15.5425422+00:00 | 184.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 286,000 | 2026-07-26T03:06:15.5432174+00:00 | 184.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 287,000 | 2026-07-26T03:06:15.5473664+00:00 | 191.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 288,000 | 2026-07-26T03:06:15.5480782+00:00 | 190.5ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 289,000 | 2026-07-26T03:06:15.5493281+00:00 | 205.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 290,000 | 2026-07-26T03:06:15.5505701+00:00 | 204.2ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 291,000 | 2026-07-26T03:06:15.5549856+00:00 | 217.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 293,000 | 2026-07-26T03:06:15.5563496+00:00 | 216.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 295,000 | 2026-07-26T03:06:15.5637704+00:00 | 208.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 296,000 | 2026-07-26T03:06:15.5762461+00:00 | 195.5ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 297,000 | 2026-07-26T03:06:15.5799034+00:00 | 193.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 298,000 | 2026-07-26T03:06:15.5806319+00:00 | 192.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 299,000 | 2026-07-26T03:06:15.5816764+00:00 | 190.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 300,000 | 2026-07-26T03:06:15.5823498+00:00 | 197.8ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 301,000 | 2026-07-26T03:06:15.5830285+00:00 | 190.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 303,000 | 2026-07-26T03:06:15.5882941+00:00 | 192.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 305,000 | 2026-07-26T03:06:15.5904623+00:00 | 182.2ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 306,000 | 2026-07-26T03:06:15.5948064+00:00 | 184.6ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 307,000 | 2026-07-26T03:06:15.5963887+00:00 | 184.9ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 308,000 | 2026-07-26T03:06:15.5970382+00:00 | 197.2ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 309,000 | 2026-07-26T03:06:15.5977543+00:00 | 181.9ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 310,000 | 2026-07-26T03:06:15.6147224+00:00 | 175.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 311,000 | 2026-07-26T03:06:15.6156259+00:00 | 178.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 313,000 | 2026-07-26T03:06:15.6170178+00:00 | 186.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 315,000 | 2026-07-26T03:06:15.6218129+00:00 | 170.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 316,000 | 2026-07-26T03:06:15.6225354+00:00 | 169.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 317,000 | 2026-07-26T03:06:15.6232335+00:00 | 181.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 318,000 | 2026-07-26T03:06:15.6243539+00:00 | 180.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 319,000 | 2026-07-26T03:06:15.6298125+00:00 | 173.5ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 320,000 | 2026-07-26T03:06:15.6306246+00:00 | 188.4ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 321,000 | 2026-07-26T03:06:15.6313623+00:00 | 188.5ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 323,000 | 2026-07-26T03:06:15.6359372+00:00 | 185.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 325,000 | 2026-07-26T03:06:15.6390649+00:00 | 168.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 326,000 | 2026-07-26T03:06:15.639728+00:00 | 168.1ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 327,000 | 2026-07-26T03:06:15.6420029+00:00 | 183.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 328,000 | 2026-07-26T03:06:15.6427022+00:00 | 182.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 329,000 | 2026-07-26T03:06:15.7050498+00:00 | 103.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 330,000 | 2026-07-26T03:06:15.7178071+00:00 | 108.3ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 331,000 | 2026-07-26T03:06:15.7186561+00:00 | 116.7ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 333,000 | 2026-07-26T03:06:15.7332938+00:00 | 102.0ms | GC pause | - | - | 2.0s / 293,580 msg/s | Gen2 +0 / pause +183.1ms |
| Confluent | 578,000 | 2026-07-26T03:06:16.422264+00:00 | 114.4ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 581,000 | 2026-07-26T03:06:16.4247216+00:00 | 112.0ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 583,000 | 2026-07-26T03:06:16.4276116+00:00 | 117.3ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 587,000 | 2026-07-26T03:06:16.4400937+00:00 | 110.1ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 588,000 | 2026-07-26T03:06:16.4460385+00:00 | 104.2ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 590,000 | 2026-07-26T03:06:16.4472755+00:00 | 117.0ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 591,000 | 2026-07-26T03:06:16.4518559+00:00 | 115.8ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 593,000 | 2026-07-26T03:06:16.453706+00:00 | 110.7ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 594,000 | 2026-07-26T03:06:16.4585124+00:00 | 105.6ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 597,000 | 2026-07-26T03:06:16.4669798+00:00 | 105.7ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 598,000 | 2026-07-26T03:06:16.4684348+00:00 | 104.3ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 601,000 | 2026-07-26T03:06:16.4705275+00:00 | 111.8ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Confluent | 602,000 | 2026-07-26T03:06:16.4712234+00:00 | 102.3ms | GC pause | - | - | 3.0s / 484,728 msg/s | Gen2 +0 / pause +152.7ms |
| Dekaf | 856,000 | 2026-07-26T03:21:15.4459059+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 1,041,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 884,000 | 2026-07-26T03:21:15.4839034+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 1,041,704 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,840,000 | 2026-07-26T03:21:16.3822152+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,090,075 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,870,000 | 2026-07-26T03:21:16.4116427+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,090,075 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,517,000 | 2026-07-26T03:21:17.9211966+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 1,068,297 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,547,000 | 2026-07-26T03:21:18.9159653+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 996,367 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,967,000 | 2026-07-26T03:21:19.362111+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,009,144 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 4,997,000 | 2026-07-26T03:21:19.3962506+00:00 | 126.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 1,009,144 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 5,964,000 | 2026-07-26T03:21:20.3513703+00:00 | 151.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,996,000 | 2026-07-26T03:21:20.3930127+00:00 | 151.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,006,000 | 2026-07-26T03:21:20.4081693+00:00 | 140.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,474,000 | 2026-07-26T03:21:20.9127828+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,476,000 | 2026-07-26T03:21:20.9135829+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,484,000 | 2026-07-26T03:21:20.9197757+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,486,000 | 2026-07-26T03:21:20.9217178+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,496,000 | 2026-07-26T03:21:20.93303+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,506,000 | 2026-07-26T03:21:20.946063+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 962,384 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,987,000 | 2026-07-26T03:21:22.4124376+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,038,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,520,000 | 2026-07-26T03:21:22.9224967+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 1,038,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,040,000 | 2026-07-26T03:21:24.3968021+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 1,016,015 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,080,000 | 2026-07-26T03:21:24.4407644+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 1,016,015 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,597,000 | 2026-07-26T03:21:25.8881373+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 1,016,577 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,647,000 | 2026-07-26T03:21:25.929359+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 1,016,577 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,067,000 | 2026-07-26T03:21:26.3861596+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,059,062 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 12,077,000 | 2026-07-26T03:21:26.3948653+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,059,062 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 12,657,000 | 2026-07-26T03:21:26.9325021+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,059,062 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 13,127,000 | 2026-07-26T03:21:27.3806635+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,020,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,167,000 | 2026-07-26T03:21:27.4190189+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,020,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,627,000 | 2026-07-26T03:21:27.8911241+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,020,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,687,000 | 2026-07-26T03:21:27.9416889+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 1,020,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,467,000 | 2026-07-26T03:21:32.4182058+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,047,870 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 19,007,000 | 2026-07-26T03:21:32.9336622+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,047,870 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 19,527,000 | 2026-07-26T03:21:33.432913+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,074,940 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,560,000 | 2026-07-26T03:21:34.3984504+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 923,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,570,000 | 2026-07-26T03:21:34.4144236+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 923,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,987,000 | 2026-07-26T03:21:34.879855+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 923,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,997,000 | 2026-07-26T03:21:34.8875062+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 923,136 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,507,000 | 2026-07-26T03:21:35.4155016+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 937,318 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,527,000 | 2026-07-26T03:21:35.4293089+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 937,318 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,040,000 | 2026-07-26T03:21:35.9854174+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 937,318 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,050,000 | 2026-07-26T03:21:35.9938569+00:00 | 139.1ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 937,318 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,430,000 | 2026-07-26T03:21:36.4102344+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 985,561 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 22,880,000 | 2026-07-26T03:21:36.911929+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 985,561 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,400,000 | 2026-07-26T03:21:37.4008529+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 999,717 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,420,000 | 2026-07-26T03:21:37.4191052+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 999,717 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,927,000 | 2026-07-26T03:21:37.9128431+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 999,717 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,397,000 | 2026-07-26T03:21:38.3911083+00:00 | 106.6ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,044,172 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 24,417,000 | 2026-07-26T03:21:38.4074032+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,044,172 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 24,427,000 | 2026-07-26T03:21:38.4157937+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,044,172 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 24,457,000 | 2026-07-26T03:21:38.4518414+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,044,172 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 25,467,000 | 2026-07-26T03:21:39.4182215+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,086,779 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 26,560,000 | 2026-07-26T03:21:40.4167107+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,027,223 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,540,000 | 2026-07-26T03:21:41.3831518+00:00 | 138.4ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,011,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,550,000 | 2026-07-26T03:21:41.3991846+00:00 | 137.5ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,011,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,560,000 | 2026-07-26T03:21:41.412566+00:00 | 136.5ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,011,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 27,567,000 | 2026-07-26T03:21:41.4227065+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,011,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 32,767,000 | 2026-07-26T03:21:46.3959861+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 33.0s / 1,055,066 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 37,717,000 | 2026-07-26T03:21:50.9202052+00:00 | 101.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 37.0s / 1,131,375 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 38,777,000 | 2026-07-26T03:21:51.9079461+00:00 | 104.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 38.0s / 1,066,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 38,787,000 | 2026-07-26T03:21:51.9156731+00:00 | 107.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 38.0s / 1,066,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 38,797,000 | 2026-07-26T03:21:51.9209153+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 38.0s / 1,066,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 38,807,000 | 2026-07-26T03:21:51.9277462+00:00 | 113.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 38.0s / 1,066,811 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,097,000 | 2026-07-26T03:21:54.8729241+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 41.0s / 1,107,110 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,107,000 | 2026-07-26T03:21:54.8839479+00:00 | 105.1ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 41.0s / 1,107,110 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,137,000 | 2026-07-26T03:21:54.9094453+00:00 | 117.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 41.0s / 1,107,110 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,147,000 | 2026-07-26T03:21:54.9180355+00:00 | 119.6ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 41.0s / 1,107,110 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,157,000 | 2026-07-26T03:21:54.9363722+00:00 | 108.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 41.0s / 1,107,110 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,256,000 | 2026-07-26T03:21:55.9200717+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 42.0s / 1,091,424 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,827,000 | 2026-07-26T03:21:57.4208648+00:00 | 102.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 44.0s / 1,002,485 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 48,010,000 | 2026-07-26T03:22:00.4348018+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,052,997 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 48,510,000 | 2026-07-26T03:22:00.9154484+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,052,997 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 48,520,000 | 2026-07-26T03:22:00.9363677+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,052,997 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 50,617,000 | 2026-07-26T03:22:02.9116437+00:00 | 111.0ms | broker/backlog (no scale or GC event) | - | - | 49.0s / 1,060,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 50,627,000 | 2026-07-26T03:22:02.9220209+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 49.0s / 1,060,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 50,637,000 | 2026-07-26T03:22:02.928849+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 49.0s / 1,060,832 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 51,147,000 | 2026-07-26T03:22:03.430968+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,051,404 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 51,157,000 | 2026-07-26T03:22:03.4363942+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,051,404 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 51,167,000 | 2026-07-26T03:22:03.4427658+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,051,404 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,837,000 | 2026-07-26T03:22:05.9058919+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,085,578 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,847,000 | 2026-07-26T03:22:05.9142714+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,085,578 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 53,857,000 | 2026-07-26T03:22:05.9214393+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,085,578 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 54,387,000 | 2026-07-26T03:22:06.423175+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 53.0s / 1,080,059 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 57,677,000 | 2026-07-26T03:22:09.4301101+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 1,072,042 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 58,757,000 | 2026-07-26T03:22:10.4366769+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,070,105 msg/s | Gen2 +0 / pause +2.9ms |
| Dekaf | 59,817,000 | 2026-07-26T03:22:11.4310324+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,026,350 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 60,314,000 | 2026-07-26T03:22:11.9303998+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,026,350 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 61,326,000 | 2026-07-26T03:22:12.8862337+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 59.0s / 1,078,215 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 62,437,000 | 2026-07-26T03:22:13.9152169+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 60.0s / 1,051,288 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 65,120,000 | 2026-07-26T03:22:16.3940614+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 63.0s / 1,087,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 65,130,000 | 2026-07-26T03:22:16.4024122+00:00 | 113.0ms | broker/backlog (no scale or GC event) | - | - | 63.0s / 1,087,541 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 66,227,000 | 2026-07-26T03:22:17.4094424+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 64.0s / 1,066,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 66,247,000 | 2026-07-26T03:22:17.4263017+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 64.0s / 1,066,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 66,257,000 | 2026-07-26T03:22:17.4387746+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 64.0s / 1,066,021 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 67,267,000 | 2026-07-26T03:22:18.3879014+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 65.0s / 1,086,081 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 67,317,000 | 2026-07-26T03:22:18.4408332+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 65.0s / 1,086,081 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 68,880,000 | 2026-07-26T03:22:19.8951642+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 66.0s / 1,050,452 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 68,910,000 | 2026-07-26T03:22:19.9240878+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 66.0s / 1,050,452 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 69,430,000 | 2026-07-26T03:22:20.4077882+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 1,058,269 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 69,450,000 | 2026-07-26T03:22:20.4216227+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 1,058,269 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 71,054,000 | 2026-07-26T03:22:21.9330523+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 68.0s / 1,092,702 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 71,560,000 | 2026-07-26T03:22:22.3928932+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 69.0s / 1,048,291 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 71,566,000 | 2026-07-26T03:22:22.4031823+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 69.0s / 1,048,291 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 71,604,000 | 2026-07-26T03:22:22.4481545+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 69.0s / 1,048,291 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 72,107,000 | 2026-07-26T03:22:22.9318684+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 69.0s / 1,048,291 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 73,177,000 | 2026-07-26T03:22:23.9179497+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 70.0s / 1,079,095 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 77,977,000 | 2026-07-26T03:22:28.3982067+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,070,809 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 78,007,000 | 2026-07-26T03:22:28.4212425+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,070,809 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 81,680,000 | 2026-07-26T03:22:31.9290523+00:00 | 117.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 78.0s / 1,064,167 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 83,204,000 | 2026-07-26T03:22:33.4049813+00:00 | 104.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 80.0s / 1,009,342 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 87,897,000 | 2026-07-26T03:22:37.8744431+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 84.1s / 1,071,468 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 88,457,000 | 2026-07-26T03:22:38.3891914+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 85.1s / 1,070,076 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 88,507,000 | 2026-07-26T03:22:38.4254863+00:00 | 118.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/failed | - | 85.1s / 1,070,076 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 95,256,000 | 2026-07-26T03:22:44.9459054+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 91.1s / 1,049,770 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 96,277,000 | 2026-07-26T03:22:45.9295571+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 92.1s / 1,025,925 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 96,747,000 | 2026-07-26T03:22:46.3914108+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,022,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 96,767,000 | 2026-07-26T03:22:46.4078061+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,022,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 97,777,000 | 2026-07-26T03:22:47.3989785+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 94.1s / 1,016,138 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 97,787,000 | 2026-07-26T03:22:47.4058046+00:00 | 115.3ms | broker/backlog (no scale or GC event) | - | - | 94.1s / 1,016,138 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 97,817,000 | 2026-07-26T03:22:47.4444266+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 94.1s / 1,016,138 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 98,807,000 | 2026-07-26T03:22:48.4088873+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 1,044,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 99,367,000 | 2026-07-26T03:22:48.9398564+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 1,044,989 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 100,397,000 | 2026-07-26T03:22:49.908231+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 96.1s / 1,048,650 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 100,407,000 | 2026-07-26T03:22:49.9176169+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 96.1s / 1,048,650 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 100,410,000 | 2026-07-26T03:22:49.9220497+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 96.1s / 1,048,650 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 101,440,000 | 2026-07-26T03:22:50.8960892+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 97.1s / 1,054,226 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 101,460,000 | 2026-07-26T03:22:50.9155223+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 97.1s / 1,054,226 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 101,470,000 | 2026-07-26T03:22:50.9216918+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 97.1s / 1,054,226 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 101,944,000 | 2026-07-26T03:22:51.4008807+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 98.1s / 1,040,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 102,477,000 | 2026-07-26T03:22:51.9136776+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 98.1s / 1,040,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 103,007,000 | 2026-07-26T03:22:52.4215743+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 99.1s / 1,074,362 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 103,567,000 | 2026-07-26T03:22:52.9234059+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 99.1s / 1,074,362 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 103,587,000 | 2026-07-26T03:22:52.9484767+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 99.1s / 1,074,362 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 106,827,000 | 2026-07-26T03:22:55.9137323+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 102.1s / 1,090,208 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 107,927,000 | 2026-07-26T03:22:56.9349+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 103.1s / 1,089,857 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 108,437,000 | 2026-07-26T03:22:57.4189574+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 104.1s / 1,003,182 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 108,897,000 | 2026-07-26T03:22:57.8869857+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 104.1s / 1,003,182 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 109,980,000 | 2026-07-26T03:22:58.9112386+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 105.1s / 1,046,486 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 111,017,000 | 2026-07-26T03:22:59.8987785+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 106.1s / 1,056,057 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 111,557,000 | 2026-07-26T03:23:00.4271721+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 107.1s / 1,062,751 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 113,187,000 | 2026-07-26T03:23:01.9239381+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 108.1s / 1,101,505 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 113,197,000 | 2026-07-26T03:23:01.9310311+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 108.1s / 1,101,505 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 115,950,000 | 2026-07-26T03:23:04.4151258+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 111.1s / 1,072,050 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 117,007,000 | 2026-07-26T03:23:05.4042095+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 112.1s / 1,068,426 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 118,050,000 | 2026-07-26T03:23:06.3698714+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 113.1s / 1,058,052 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 118,097,000 | 2026-07-26T03:23:06.4200283+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 113.1s / 1,058,052 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 120,796,000 | 2026-07-26T03:23:08.8944917+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 115.1s / 1,068,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 120,797,000 | 2026-07-26T03:23:08.8960042+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 115.1s / 1,068,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 120,807,000 | 2026-07-26T03:23:08.9061767+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 115.1s / 1,068,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 120,816,000 | 2026-07-26T03:23:08.9136895+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 115.1s / 1,068,911 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 126,277,000 | 2026-07-26T03:23:13.9091772+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 120.1s / 1,122,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 126,317,000 | 2026-07-26T03:23:13.9447182+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 120.1s / 1,122,411 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 126,847,000 | 2026-07-26T03:23:14.4338037+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 121.1s / 1,087,328 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 134,897,000 | 2026-07-26T03:23:21.8796896+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 1,061,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 134,937,000 | 2026-07-26T03:23:21.903931+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 128.1s / 1,061,047 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 137,607,000 | 2026-07-26T03:23:24.4074774+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 131.1s / 1,109,890 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 138,717,000 | 2026-07-26T03:23:25.4096926+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 132.1s / 1,092,155 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 139,817,000 | 2026-07-26T03:23:26.4194447+00:00 | 115.8ms | broker/backlog (no scale or GC event) | - | - | 133.1s / 1,054,143 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 142,467,000 | 2026-07-26T03:23:28.9233003+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 135.1s / 1,081,645 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 142,497,000 | 2026-07-26T03:23:28.940188+00:00 | 114.2ms | broker/backlog (no scale or GC event) | - | - | 135.1s / 1,081,645 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 144,657,000 | 2026-07-26T03:23:30.9097535+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 137.1s / 1,103,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 146,227,000 | 2026-07-26T03:23:32.3987971+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 139.1s / 1,101,839 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 146,247,000 | 2026-07-26T03:23:32.418186+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 139.1s / 1,101,839 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 146,257,000 | 2026-07-26T03:23:32.42763+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 139.1s / 1,101,839 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 146,817,000 | 2026-07-26T03:23:32.9394251+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 139.1s / 1,101,839 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 147,306,000 | 2026-07-26T03:23:33.3601369+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 140.1s / 918,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 147,354,000 | 2026-07-26T03:23:33.402095+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 140.1s / 918,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 147,356,000 | 2026-07-26T03:23:33.4031621+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 140.1s / 918,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 147,777,000 | 2026-07-26T03:23:33.8926807+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 140.1s / 918,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 147,797,000 | 2026-07-26T03:23:33.9151475+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 140.1s / 918,826 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 148,277,000 | 2026-07-26T03:23:34.4230834+00:00 | 107.9ms | broker/backlog (no scale or GC event) | - | - | 141.1s / 943,033 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 148,750,000 | 2026-07-26T03:23:34.9194299+00:00 | 113.0ms | broker/backlog (no scale or GC event) | - | - | 141.1s / 943,033 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 148,764,000 | 2026-07-26T03:23:34.9381079+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 141.1s / 943,033 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 149,230,000 | 2026-07-26T03:23:35.4380421+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 142.1s / 1,016,927 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf | 149,260,000 | 2026-07-26T03:23:35.4792098+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 142.1s / 1,016,927 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 150,206,000 | 2026-07-26T03:23:36.3923137+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 143.1s / 1,026,737 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 150,234,000 | 2026-07-26T03:23:36.4177184+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 143.1s / 1,026,737 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 150,246,000 | 2026-07-26T03:23:36.4301488+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 143.1s / 1,026,737 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 152,744,000 | 2026-07-26T03:23:38.8758679+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 145.1s / 1,056,070 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 152,776,000 | 2026-07-26T03:23:38.8982159+00:00 | 134.2ms | broker/backlog (no scale or GC event) | - | - | 145.1s / 1,056,070 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 152,790,000 | 2026-07-26T03:23:38.9099185+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 145.1s / 1,056,070 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 152,794,000 | 2026-07-26T03:23:38.9156582+00:00 | 135.8ms | broker/backlog (no scale or GC event) | - | - | 145.1s / 1,056,070 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 153,304,000 | 2026-07-26T03:23:39.4143396+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 153,314,000 | 2026-07-26T03:23:39.4194444+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 153,316,000 | 2026-07-26T03:23:39.421859+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 153,326,000 | 2026-07-26T03:23:39.4407268+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +1.2ms |
| Dekaf | 153,774,000 | 2026-07-26T03:23:39.8789831+00:00 | 117.8ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 153,776,000 | 2026-07-26T03:23:39.8810749+00:00 | 115.7ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 153,784,000 | 2026-07-26T03:23:39.8857459+00:00 | 125.3ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 153,834,000 | 2026-07-26T03:23:39.9545724+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 146.1s / 997,532 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 155,417,000 | 2026-07-26T03:23:41.4116192+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 148.1s / 1,041,824 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 155,427,000 | 2026-07-26T03:23:41.4207989+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 148.1s / 1,041,824 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 155,437,000 | 2026-07-26T03:23:41.4388778+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 148.1s / 1,041,824 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 156,457,000 | 2026-07-26T03:23:42.4157735+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 149.1s / 1,078,301 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 156,477,000 | 2026-07-26T03:23:42.4330671+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 149.1s / 1,078,301 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 157,507,000 | 2026-07-26T03:23:43.3922761+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 150.1s / 1,011,253 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 157,547,000 | 2026-07-26T03:23:43.4206097+00:00 | 140.1ms | broker/backlog (no scale or GC event) | - | - | 150.1s / 1,011,253 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 157,567,000 | 2026-07-26T03:23:43.4899653+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 150.1s / 1,011,253 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 159,120,000 | 2026-07-26T03:23:44.9362834+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 151.1s / 1,046,823 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 159,587,000 | 2026-07-26T03:23:45.3929892+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 152.1s / 1,112,963 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 159,597,000 | 2026-07-26T03:23:45.401901+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 152.1s / 1,112,963 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 159,607,000 | 2026-07-26T03:23:45.4110287+00:00 | 112.7ms | broker/backlog (no scale or GC event) | - | - | 152.1s / 1,112,963 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 161,207,000 | 2026-07-26T03:23:46.9031915+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 153.1s / 1,064,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 161,217,000 | 2026-07-26T03:23:46.9093973+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 153.1s / 1,064,973 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 161,797,000 | 2026-07-26T03:23:47.4439296+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 154.1s / 1,074,291 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 162,307,000 | 2026-07-26T03:23:47.9262304+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 154.1s / 1,074,291 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 162,317,000 | 2026-07-26T03:23:47.9335596+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 154.1s / 1,074,291 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 164,417,000 | 2026-07-26T03:23:49.8748652+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 156.1s / 1,034,246 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 164,437,000 | 2026-07-26T03:23:49.8925246+00:00 | 105.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 156.1s / 1,034,246 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 164,457,000 | 2026-07-26T03:23:49.9109157+00:00 | 104.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 156.1s / 1,034,246 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 167,120,000 | 2026-07-26T03:23:52.4033074+00:00 | 103.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 159.1s / 1,111,454 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 167,130,000 | 2026-07-26T03:23:52.4123154+00:00 | 108.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 159.1s / 1,111,454 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 168,240,000 | 2026-07-26T03:23:53.4224883+00:00 | 100.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 160.1s / 1,109,284 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 170,987,000 | 2026-07-26T03:23:55.9090898+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 162.1s / 1,111,424 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 171,007,000 | 2026-07-26T03:23:55.9280706+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 162.1s / 1,111,424 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 176,947,000 | 2026-07-26T03:24:01.4393724+00:00 | 101.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 168.1s / 1,050,034 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 186,407,000 | 2026-07-26T03:24:10.4042811+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 177.1s / 1,015,797 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 186,957,000 | 2026-07-26T03:24:10.9368369+00:00 | 112.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 177.1s / 1,015,797 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 187,427,000 | 2026-07-26T03:24:11.4098146+00:00 | 116.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 178.1s / 969,031 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 187,447,000 | 2026-07-26T03:24:11.4254028+00:00 | 116.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 178.1s / 969,031 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 188,867,000 | 2026-07-26T03:24:12.8884031+00:00 | 109.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 179.1s / 968,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 188,877,000 | 2026-07-26T03:24:12.8958427+00:00 | 108.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 179.1s / 968,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 188,907,000 | 2026-07-26T03:24:12.931068+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 179.1s / 968,260 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 189,857,000 | 2026-07-26T03:24:13.8613459+00:00 | 112.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 180.1s / 988,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 189,877,000 | 2026-07-26T03:24:13.8843783+00:00 | 114.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 180.1s / 988,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 189,887,000 | 2026-07-26T03:24:13.8939488+00:00 | 117.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 180.1s / 988,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 189,897,000 | 2026-07-26T03:24:13.9053566+00:00 | 113.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 180.1s / 988,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 189,917,000 | 2026-07-26T03:24:13.9266805+00:00 | 113.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 180.1s / 988,969 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 190,347,000 | 2026-07-26T03:24:14.394066+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 181.1s / 1,086,905 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 190,357,000 | 2026-07-26T03:24:14.4050358+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 181.1s / 1,086,905 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 192,027,000 | 2026-07-26T03:24:15.9211345+00:00 | 107.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 182.1s / 1,053,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 192,487,000 | 2026-07-26T03:24:16.3977104+00:00 | 108.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 183.1s / 1,059,881 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 192,497,000 | 2026-07-26T03:24:16.4042974+00:00 | 114.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 183.1s / 1,059,881 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 192,527,000 | 2026-07-26T03:24:16.4372214+00:00 | 113.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 183.1s / 1,059,881 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 193,037,000 | 2026-07-26T03:24:16.9337559+00:00 | 102.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 183.1s / 1,059,881 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf | 195,747,000 | 2026-07-26T03:24:19.4067393+00:00 | 103.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 186.1s / 1,017,212 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 195,777,000 | 2026-07-26T03:24:19.424869+00:00 | 110.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 186.1s / 1,017,212 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 203,697,000 | 2026-07-26T03:24:26.941465+00:00 | 106.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 193.1s / 1,002,466 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 204,647,000 | 2026-07-26T03:24:27.8811012+00:00 | 110.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 194.1s / 975,858 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 204,687,000 | 2026-07-26T03:24:27.9156978+00:00 | 107.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 194.1s / 975,858 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 204,697,000 | 2026-07-26T03:24:27.9230575+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 194.1s / 975,858 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 206,127,000 | 2026-07-26T03:24:29.3502455+00:00 | 102.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 195.1s / 1,018,398 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 206,167,000 | 2026-07-26T03:24:29.3965212+00:00 | 117.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 196.1s / 1,013,983 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 207,197,000 | 2026-07-26T03:24:30.4303997+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 197.1s / 1,002,625 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 207,677,000 | 2026-07-26T03:24:30.9162716+00:00 | 103.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 197.1s / 1,002,625 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 211,767,000 | 2026-07-26T03:24:34.9177611+00:00 | 109.8ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 201.1s / 1,099,569 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 211,777,000 | 2026-07-26T03:24:34.9273884+00:00 | 113.2ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 201.1s / 1,099,569 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 211,787,000 | 2026-07-26T03:24:34.9392354+00:00 | 106.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 201.1s / 1,099,569 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 213,937,000 | 2026-07-26T03:24:36.9127305+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/succeeded | - | 203.1s / 1,049,889 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 231,477,000 | 2026-07-26T03:24:53.3828704+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/succeeded | - | 220.1s / 1,042,381 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 231,497,000 | 2026-07-26T03:24:53.3988957+00:00 | 106.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/succeeded | - | 220.1s / 1,042,381 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 231,527,000 | 2026-07-26T03:24:53.4325913+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/succeeded | - | 220.1s / 1,042,381 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 231,537,000 | 2026-07-26T03:24:53.4440103+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 1:capacity/failed, 2:capacity/succeeded | - | 220.1s / 1,042,381 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf | 236,827,000 | 2026-07-26T03:24:58.3899407+00:00 | 107.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 225.1s / 1,031,163 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 237,367,000 | 2026-07-26T03:24:58.9135649+00:00 | 109.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 225.1s / 1,031,163 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 249,597,000 | 2026-07-26T03:25:10.4208078+00:00 | 121.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 237.2s / 1,105,465 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 250,697,000 | 2026-07-26T03:25:11.4198619+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 238.2s / 1,081,882 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 40,000 | 2026-07-26T03:36:27.0342197+00:00 | 118.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 46,000 | 2026-07-26T03:36:27.0475731+00:00 | 104.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 50,000 | 2026-07-26T03:36:27.0550971+00:00 | 132.8ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 56,000 | 2026-07-26T03:36:27.0687928+00:00 | 127.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 64,000 | 2026-07-26T03:36:27.0879275+00:00 | 124.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 66,000 | 2026-07-26T03:36:27.0932597+00:00 | 118.8ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 70,000 | 2026-07-26T03:36:27.1007173+00:00 | 132.6ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 84,000 | 2026-07-26T03:36:27.1353411+00:00 | 101.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 130,000 | 2026-07-26T03:36:27.2446784+00:00 | 116.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 134,000 | 2026-07-26T03:36:27.2505285+00:00 | 105.7ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 160,000 | 2026-07-26T03:36:27.3198281+00:00 | 134.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 176,000 | 2026-07-26T03:36:27.3391607+00:00 | 103.2ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 177,000 | 2026-07-26T03:36:27.3401759+00:00 | 135.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 178,000 | 2026-07-26T03:36:27.3416061+00:00 | 130.5ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 180,000 | 2026-07-26T03:36:27.3591415+00:00 | 129.6ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 187,000 | 2026-07-26T03:36:27.366911+00:00 | 123.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 195,000 | 2026-07-26T03:36:27.3938461+00:00 | 101.7ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 198,000 | 2026-07-26T03:36:27.4041665+00:00 | 104.8ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 208,000 | 2026-07-26T03:36:27.4190268+00:00 | 106.0ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 220,000 | 2026-07-26T03:36:27.45451+00:00 | 112.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 227,000 | 2026-07-26T03:36:27.4738258+00:00 | 117.4ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 230,000 | 2026-07-26T03:36:27.4766586+00:00 | 146.4ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 237,000 | 2026-07-26T03:36:27.4930198+00:00 | 107.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 240,000 | 2026-07-26T03:36:27.4974833+00:00 | 163.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 244,000 | 2026-07-26T03:36:27.504614+00:00 | 136.5ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 246,000 | 2026-07-26T03:36:27.5107831+00:00 | 130.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 250,000 | 2026-07-26T03:36:27.5245846+00:00 | 150.0ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 254,000 | 2026-07-26T03:36:27.5305097+00:00 | 126.7ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 260,000 | 2026-07-26T03:36:27.5460136+00:00 | 160.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 264,000 | 2026-07-26T03:36:27.5600765+00:00 | 126.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 270,000 | 2026-07-26T03:36:27.5729267+00:00 | 181.0ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 274,000 | 2026-07-26T03:36:27.5772658+00:00 | 122.6ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 280,000 | 2026-07-26T03:36:27.6123408+00:00 | 178.5ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 290,000 | 2026-07-26T03:36:27.6448087+00:00 | 181.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 300,000 | 2026-07-26T03:36:27.6705529+00:00 | 177.3ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 304,000 | 2026-07-26T03:36:27.6783494+00:00 | 119.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 306,000 | 2026-07-26T03:36:27.6815286+00:00 | 115.9ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 310,000 | 2026-07-26T03:36:27.6933421+00:00 | 165.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 316,000 | 2026-07-26T03:36:27.7061032+00:00 | 129.1ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 320,000 | 2026-07-26T03:36:27.7188595+00:00 | 160.4ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 324,000 | 2026-07-26T03:36:27.7298924+00:00 | 112.0ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 326,000 | 2026-07-26T03:36:27.7324001+00:00 | 109.5ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 330,000 | 2026-07-26T03:36:27.7422807+00:00 | 152.8ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 334,000 | 2026-07-26T03:36:27.7557289+00:00 | 100.7ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 340,000 | 2026-07-26T03:36:27.7923478+00:00 | 111.8ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 360,000 | 2026-07-26T03:36:27.8418279+00:00 | 105.5ms | GC pause | - | - | 1.0s / 426,889 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 380,000 | 2026-07-26T03:36:27.8736143+00:00 | 101.6ms | GC pause | - | - | 2.0s / 643,756 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 390,000 | 2026-07-26T03:36:27.8874937+00:00 | 113.6ms | GC pause | - | - | 2.0s / 643,756 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 407,000 | 2026-07-26T03:36:27.916062+00:00 | 148.9ms | GC pause | - | - | 2.0s / 643,756 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 417,000 | 2026-07-26T03:36:27.9361652+00:00 | 141.5ms | GC pause | - | - | 2.0s / 643,756 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 420,000 | 2026-07-26T03:36:27.9390625+00:00 | 114.1ms | GC pause | - | - | 2.0s / 643,756 msg/s | Gen2 +1 / pause +0.4ms |
| Dekaf (3conn) | 428,000 | 2026-07-26T03:36:27.9483413+00:00 | 128.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 430,000 | 2026-07-26T03:36:27.9521546+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 440,000 | 2026-07-26T03:36:27.9703488+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 445,000 | 2026-07-26T03:36:27.9753135+00:00 | 114.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 447,000 | 2026-07-26T03:36:27.9791361+00:00 | 144.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 448,000 | 2026-07-26T03:36:27.9819634+00:00 | 114.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 450,000 | 2026-07-26T03:36:27.9832169+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 457,000 | 2026-07-26T03:36:28.0138732+00:00 | 121.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 547,000 | 2026-07-26T03:36:28.1933722+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 637,000 | 2026-07-26T03:36:28.3400385+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 901,000 | 2026-07-26T03:36:28.7102167+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 921,000 | 2026-07-26T03:36:28.7280881+00:00 | 116.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 929,000 | 2026-07-26T03:36:28.733463+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 931,000 | 2026-07-26T03:36:28.7382124+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 932,000 | 2026-07-26T03:36:28.7388806+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 941,000 | 2026-07-26T03:36:28.7523211+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 643,756 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,751,000 | 2026-07-26T03:36:29.7847105+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 774,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,752,000 | 2026-07-26T03:36:29.7850837+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 774,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,763,000 | 2026-07-26T03:36:29.7919292+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 774,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,769,000 | 2026-07-26T03:36:29.7997304+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 774,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,782,000 | 2026-07-26T03:36:29.8390034+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 774,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,872,000 | 2026-07-26T03:36:29.9772122+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,881,000 | 2026-07-26T03:36:29.990326+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,930,000 | 2026-07-26T03:36:30.0714655+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,940,000 | 2026-07-26T03:36:30.0888867+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,960,000 | 2026-07-26T03:36:30.1206384+00:00 | 122.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,980,000 | 2026-07-26T03:36:30.1784871+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,111,000 | 2026-07-26T03:36:30.3503012+00:00 | 126.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,122,000 | 2026-07-26T03:36:30.3577447+00:00 | 135.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,131,000 | 2026-07-26T03:36:30.3717521+00:00 | 126.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,142,000 | 2026-07-26T03:36:30.405643+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,361,000 | 2026-07-26T03:36:30.6914745+00:00 | 116.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,372,000 | 2026-07-26T03:36:30.7061389+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,381,000 | 2026-07-26T03:36:30.7165336+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,391,000 | 2026-07-26T03:36:30.7227795+00:00 | 161.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,392,000 | 2026-07-26T03:36:30.7277271+00:00 | 156.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,397,000 | 2026-07-26T03:36:30.7308597+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,407,000 | 2026-07-26T03:36:30.7448214+00:00 | 118.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,413,000 | 2026-07-26T03:36:30.7642285+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,414,000 | 2026-07-26T03:36:30.7645051+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,419,000 | 2026-07-26T03:36:30.7865555+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,421,000 | 2026-07-26T03:36:30.7900956+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 670,748 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,597,000 | 2026-07-26T03:36:31.0381167+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,601,000 | 2026-07-26T03:36:31.0431525+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,602,000 | 2026-07-26T03:36:31.0498491+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,611,000 | 2026-07-26T03:36:31.0553039+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,621,000 | 2026-07-26T03:36:31.0682455+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,627,000 | 2026-07-26T03:36:31.0750367+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,631,000 | 2026-07-26T03:36:31.0791405+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,711,000 | 2026-07-26T03:36:31.205977+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,722,000 | 2026-07-26T03:36:31.2199598+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,187,000 | 2026-07-26T03:36:31.6974858+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 878,662 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,627,000 | 2026-07-26T03:36:32.1912269+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 891,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,657,000 | 2026-07-26T03:36:32.2110618+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 891,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,100,000 | 2026-07-26T03:36:32.7099569+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 891,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,120,000 | 2026-07-26T03:36:32.7260979+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 891,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,140,000 | 2026-07-26T03:36:32.7561641+00:00 | 112.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 891,154 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,542,000 | 2026-07-26T03:36:33.227679+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 816,039 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,901,000 | 2026-07-26T03:36:33.677054+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 816,039 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,902,000 | 2026-07-26T03:36:33.6778496+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 816,039 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,912,000 | 2026-07-26T03:36:33.6867639+00:00 | 130.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 816,039 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,921,000 | 2026-07-26T03:36:33.6966475+00:00 | 129.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 816,039 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,931,000 | 2026-07-26T03:36:33.7102721+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 816,039 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,087,000 | 2026-07-26T03:36:33.9294351+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,311,000 | 2026-07-26T03:36:34.1877171+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,312,000 | 2026-07-26T03:36:34.1883036+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,331,000 | 2026-07-26T03:36:34.2081681+00:00 | 128.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,341,000 | 2026-07-26T03:36:34.2172203+00:00 | 126.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,342,000 | 2026-07-26T03:36:34.2206617+00:00 | 128.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,732,000 | 2026-07-26T03:36:34.6841805+00:00 | 119.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,741,000 | 2026-07-26T03:36:34.6907276+00:00 | 141.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,742,000 | 2026-07-26T03:36:34.6918315+00:00 | 140.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,751,000 | 2026-07-26T03:36:34.7022306+00:00 | 160.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,752,000 | 2026-07-26T03:36:34.7025893+00:00 | 159.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,754,000 | 2026-07-26T03:36:34.7059391+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,756,000 | 2026-07-26T03:36:34.7079635+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,761,000 | 2026-07-26T03:36:34.7134393+00:00 | 159.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,762,000 | 2026-07-26T03:36:34.7158927+00:00 | 163.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,766,000 | 2026-07-26T03:36:34.7213043+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,769,000 | 2026-07-26T03:36:34.7287611+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,771,000 | 2026-07-26T03:36:34.7296535+00:00 | 157.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,779,000 | 2026-07-26T03:36:34.7380757+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,780,000 | 2026-07-26T03:36:34.7536645+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,781,000 | 2026-07-26T03:36:34.7543066+00:00 | 143.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,782,000 | 2026-07-26T03:36:34.7549234+00:00 | 142.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,784,000 | 2026-07-26T03:36:34.7556243+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,792,000 | 2026-07-26T03:36:34.7969852+00:00 | 125.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,801,000 | 2026-07-26T03:36:34.8062985+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 787,257 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,077,000 | 2026-07-26T03:36:35.1814991+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,087,000 | 2026-07-26T03:36:35.194899+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,097,000 | 2026-07-26T03:36:35.2046116+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,107,000 | 2026-07-26T03:36:35.2194773+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,297,000 | 2026-07-26T03:36:35.4258804+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,327,000 | 2026-07-26T03:36:35.4603504+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,477,000 | 2026-07-26T03:36:35.6589666+00:00 | 142.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,487,000 | 2026-07-26T03:36:35.6654431+00:00 | 149.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,508,000 | 2026-07-26T03:36:35.687045+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,510,000 | 2026-07-26T03:36:35.6893122+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,517,000 | 2026-07-26T03:36:35.6953025+00:00 | 145.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,518,000 | 2026-07-26T03:36:35.6955927+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,520,000 | 2026-07-26T03:36:35.6978309+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,537,000 | 2026-07-26T03:36:35.7349242+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 800,128 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,531,000 | 2026-07-26T03:36:36.7260494+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 998,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,532,000 | 2026-07-26T03:36:36.7266131+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 998,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,537,000 | 2026-07-26T03:36:36.7476922+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 998,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,541,000 | 2026-07-26T03:36:36.7508225+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 998,011 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,937,000 | 2026-07-26T03:36:37.1818973+00:00 | 133.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,957,000 | 2026-07-26T03:36:37.2030243+00:00 | 143.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,967,000 | 2026-07-26T03:36:37.2225815+00:00 | 129.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,252,000 | 2026-07-26T03:36:37.57644+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,261,000 | 2026-07-26T03:36:37.5853109+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,262,000 | 2026-07-26T03:36:37.5918022+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,281,000 | 2026-07-26T03:36:37.6118852+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,341,000 | 2026-07-26T03:36:37.7128813+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,342,000 | 2026-07-26T03:36:37.7133685+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,362,000 | 2026-07-26T03:36:37.7320611+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,371,000 | 2026-07-26T03:36:37.7398428+00:00 | 121.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,382,000 | 2026-07-26T03:36:37.7547259+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 800,530 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,451,000 | 2026-07-26T03:36:37.8833516+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,461,000 | 2026-07-26T03:36:37.8900298+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,471,000 | 2026-07-26T03:36:37.9009307+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,482,000 | 2026-07-26T03:36:37.9127306+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,700,000 | 2026-07-26T03:36:38.1795231+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,740,000 | 2026-07-26T03:36:38.2189729+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,750,000 | 2026-07-26T03:36:38.2310452+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,227,000 | 2026-07-26T03:36:38.6958069+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,237,000 | 2026-07-26T03:36:38.7012826+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,247,000 | 2026-07-26T03:36:38.707794+00:00 | 123.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,257,000 | 2026-07-26T03:36:38.7168972+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,267,000 | 2026-07-26T03:36:38.7294053+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 984,597 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,131,000 | 2026-07-26T03:36:39.6417431+00:00 | 115.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 916,273 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,142,000 | 2026-07-26T03:36:39.6557743+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 916,273 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,152,000 | 2026-07-26T03:36:39.6660235+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 916,273 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,162,000 | 2026-07-26T03:36:39.6772216+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 916,273 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,172,000 | 2026-07-26T03:36:39.691205+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 916,273 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 10,182,000 | 2026-07-26T03:36:39.707285+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 916,273 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 11,827,000 | 2026-07-26T03:36:41.2031875+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,060,173 msg/s | Gen2 +0 / pause +6.0ms |
| Dekaf (3conn) | 11,837,000 | 2026-07-26T03:36:41.2091533+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,060,173 msg/s | Gen2 +0 / pause +6.0ms |
| Dekaf (3conn) | 11,847,000 | 2026-07-26T03:36:41.2152673+00:00 | 113.8ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,060,173 msg/s | Gen2 +0 / pause +6.0ms |
| Dekaf (3conn) | 11,867,000 | 2026-07-26T03:36:41.2393687+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,060,173 msg/s | Gen2 +0 / pause +6.0ms |
| Dekaf (3conn) | 12,400,000 | 2026-07-26T03:36:41.7389644+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 1,060,173 msg/s | Gen2 +0 / pause +6.0ms |
| Dekaf (3conn) | 15,237,000 | 2026-07-26T03:36:44.2180399+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,129,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,257,000 | 2026-07-26T03:36:44.2387871+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 18.0s / 1,129,941 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,771,000 | 2026-07-26T03:36:45.7086221+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 992,749 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,772,000 | 2026-07-26T03:36:45.7093763+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 992,749 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 16,782,000 | 2026-07-26T03:36:45.7169948+00:00 | 110.8ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 992,749 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,842,000 | 2026-07-26T03:36:49.2267815+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,116,208 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,851,000 | 2026-07-26T03:36:49.2409479+00:00 | 104.7ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,116,208 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,962,000 | 2026-07-26T03:36:50.2389466+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 1,076,651 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,972,000 | 2026-07-26T03:36:50.2433641+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 1,076,651 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 33,927,000 | 2026-07-26T03:37:00.7467876+00:00 | 105.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 34.0s / 999,299 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,931,000 | 2026-07-26T03:37:07.6940715+00:00 | 109.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 41.0s / 1,129,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,962,000 | 2026-07-26T03:37:07.7324348+00:00 | 112.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 41.0s / 1,129,844 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,582,000 | 2026-07-26T03:37:16.1888197+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,592,000 | 2026-07-26T03:37:16.1946863+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,601,000 | 2026-07-26T03:37:16.2002407+00:00 | 117.8ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,602,000 | 2026-07-26T03:37:16.2011236+00:00 | 117.0ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,611,000 | 2026-07-26T03:37:16.2065878+00:00 | 117.9ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,612,000 | 2026-07-26T03:37:16.207335+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 51,632,000 | 2026-07-26T03:37:16.237405+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,115,979 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,482,000 | 2026-07-26T03:37:18.6888348+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,491,000 | 2026-07-26T03:37:18.6939541+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,502,000 | 2026-07-26T03:37:18.7011921+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,511,000 | 2026-07-26T03:37:18.7116479+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,512,000 | 2026-07-26T03:37:18.7123588+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,531,000 | 2026-07-26T03:37:18.7300091+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,532,000 | 2026-07-26T03:37:18.7308805+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,541,000 | 2026-07-26T03:37:18.7413124+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 54,542,000 | 2026-07-26T03:37:18.7418326+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,153,132 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 56,792,000 | 2026-07-26T03:37:20.6898462+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 54.0s / 1,137,208 msg/s | Gen2 +0 / pause +1.1ms |
| Dekaf (3conn) | 57,397,000 | 2026-07-26T03:37:21.1967663+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 55.0s / 1,182,998 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,731,000 | 2026-07-26T03:37:23.2138407+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 57.1s / 1,158,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,761,000 | 2026-07-26T03:37:23.2380003+00:00 | 112.0ms | broker/backlog (no scale or GC event) | - | - | 57.1s / 1,158,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 59,762,000 | 2026-07-26T03:37:23.2404042+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 57.1s / 1,158,457 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 63,290,000 | 2026-07-26T03:37:26.2161446+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 60.1s / 1,164,743 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 63,820,000 | 2026-07-26T03:37:26.6928476+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 60.1s / 1,164,743 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 63,840,000 | 2026-07-26T03:37:26.7122438+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 60.1s / 1,164,743 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 63,860,000 | 2026-07-26T03:37:26.7348657+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 60.1s / 1,164,743 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 67,950,000 | 2026-07-26T03:37:30.2456415+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 64.1s / 1,192,326 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 87,410,000 | 2026-07-26T03:37:46.6907072+00:00 | 100.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 80.1s / 1,171,195 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 90,931,000 | 2026-07-26T03:37:49.7386673+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 83.1s / 1,156,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 90,941,000 | 2026-07-26T03:37:49.7472355+00:00 | 106.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 83.1s / 1,156,323 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 91,432,000 | 2026-07-26T03:37:50.2084618+00:00 | 111.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 84.1s / 1,154,656 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 91,451,000 | 2026-07-26T03:37:50.2251137+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 84.1s / 1,154,656 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 91,452,000 | 2026-07-26T03:37:50.2263329+00:00 | 111.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 84.1s / 1,154,656 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 91,462,000 | 2026-07-26T03:37:50.2333091+00:00 | 106.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 84.1s / 1,154,656 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 93,220,000 | 2026-07-26T03:37:51.7533149+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 85.1s / 1,149,824 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 93,230,000 | 2026-07-26T03:37:51.7598197+00:00 | 100.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 85.1s / 1,149,824 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 96,097,000 | 2026-07-26T03:37:54.2108511+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 88.1s / 1,179,673 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 96,107,000 | 2026-07-26T03:37:54.2171894+00:00 | 106.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 88.1s / 1,179,673 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 96,117,000 | 2026-07-26T03:37:54.2274265+00:00 | 107.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 88.1s / 1,179,673 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 100,967,000 | 2026-07-26T03:37:58.2035388+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 92.1s / 1,040,784 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 100,977,000 | 2026-07-26T03:37:58.2124332+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 92.1s / 1,040,784 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 100,987,000 | 2026-07-26T03:37:58.2193396+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 92.1s / 1,040,784 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 107,291,000 | 2026-07-26T03:38:03.7419276+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 97.1s / 1,160,003 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 107,292,000 | 2026-07-26T03:38:03.7424817+00:00 | 103.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 97.1s / 1,160,003 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 139,999,000 | 2026-07-26T03:38:31.2243034+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 1,063,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 140,003,000 | 2026-07-26T03:38:31.2272143+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 1,063,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 140,545,000 | 2026-07-26T03:38:31.72012+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 1,063,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 140,548,000 | 2026-07-26T03:38:31.7225644+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 1,063,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 140,555,000 | 2026-07-26T03:38:31.7261557+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 1,063,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 140,558,000 | 2026-07-26T03:38:31.7275524+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 125.1s / 1,063,683 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 220,117,000 | 2026-07-26T03:39:39.6931607+00:00 | 100.0ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded | - | 193.1s / 1,142,865 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 967,014,000 | 2026-07-26T03:50:17.7322787+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 831.5s / 1,063,516 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 967,024,000 | 2026-07-26T03:50:17.748855+00:00 | 103.9ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 831.5s / 1,063,516 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*746 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.53x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.25x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.93 | 4969.96 | 1,340,178 | 2,530,610 | +48.5% | +488.96% | 163.60 | 1,340,178 | 0 | 1.25 |
| Confluent | 1.83 | - | 130,110 | 1,572,314 | +15.7% | +107.57% | 15.88 | 130,110 | 0 | 0.24 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 3,712 | 442.72 | 1015.41 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:08:41.851577+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 899,710 msg/s |
| Dekaf | 2026-07-26T03:08:46.8284323+00:00 | 1 | 16.0 MiB / 4.5 MiB | 548.6 MB/s | 0/0 | 2 | 5.0s / 2,571,233 msg/s |

## Producer Admission Block Durations - Producer → Consumer Round-Trip Steady State

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 8.192–16.384ms | 1 |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.97x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.61x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 466.65 | 466.64 | 265 | 354 | -0.0% | +0.01% | 0.25 | 353 | 0 | 0.16 |
| Confluent | 303.63 | - | 129 | 172 | +1.7% | +0.17% | 0.12 | 171 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 106,004 | 117.74 | 1.16 KB |
| Dekaf | 2 | 106,076 | 117.82 | 1.16 KB |
| Dekaf | 3 | 105,726 | 117.43 | 1.16 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-26T03:21:19.2585999+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 290 msg/s |
| Dekaf | 2026-07-26T03:21:28.2609068+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 9.0s / 356 msg/s |
| Dekaf | 2026-07-26T03:21:37.2617339+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 18.0s / 349 msg/s |
| Dekaf | 2026-07-26T03:21:47.2726554+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 28.0s / 361 msg/s |
| Dekaf | 2026-07-26T03:21:56.3270217+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 37.0s / 342 msg/s |
| Dekaf | 2026-07-26T03:22:05.3346007+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 46.0s / 340 msg/s |
| Dekaf | 2026-07-26T03:22:14.3436829+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 55.0s / 355 msg/s |
| Dekaf | 2026-07-26T03:22:23.3699627+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 64.0s / 349 msg/s |
| Dekaf | 2026-07-26T03:22:32.377125+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 73.0s / 356 msg/s |
| Dekaf | 2026-07-26T03:22:41.3884086+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 82.0s / 359 msg/s |
| Dekaf | 2026-07-26T03:22:50.4098964+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 363 msg/s |
| Dekaf | 2026-07-26T03:22:59.4263783+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 100.0s / 363 msg/s |
| Dekaf | 2026-07-26T03:23:08.4316105+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 109.0s / 362 msg/s |
| Dekaf | 2026-07-26T03:23:17.4340182+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 118.0s / 355 msg/s |
| Dekaf | 2026-07-26T03:23:26.4392797+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 351 msg/s |
| Dekaf | 2026-07-26T03:23:36.4435719+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 137.0s / 365 msg/s |
| Dekaf | 2026-07-26T03:23:45.4468045+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 146.0s / 354 msg/s |
| Dekaf | 2026-07-26T03:23:54.4547002+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 355 msg/s |
| Dekaf | 2026-07-26T03:24:03.4733344+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 359 msg/s |
| Dekaf | 2026-07-26T03:24:12.4753226+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 358 msg/s |
| Dekaf | 2026-07-26T03:24:21.4764832+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 354 msg/s |
| Dekaf | 2026-07-26T03:24:30.478869+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 351 msg/s |
| Dekaf | 2026-07-26T03:24:39.4946352+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 341 msg/s |
| Dekaf | 2026-07-26T03:24:48.4991307+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 347 msg/s |
| Dekaf | 2026-07-26T03:24:57.5113011+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 352 msg/s |
| Dekaf | 2026-07-26T03:25:06.515699+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 351 msg/s |
| Dekaf | 2026-07-26T03:25:15.535509+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 355 msg/s |
| Dekaf | 2026-07-26T03:25:25.5591599+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 246.0s / 349 msg/s |
| Dekaf | 2026-07-26T03:25:34.5649914+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 255.0s / 346 msg/s |
| Dekaf | 2026-07-26T03:25:43.5736836+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 264.0s / 349 msg/s |
| Dekaf | 2026-07-26T03:25:52.5916048+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 273.0s / 354 msg/s |
| Dekaf | 2026-07-26T03:26:01.6056008+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 356 msg/s |
| Dekaf | 2026-07-26T03:26:10.6259236+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 291.0s / 349 msg/s |
| Dekaf | 2026-07-26T03:26:19.6308905+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 300.0s / 352 msg/s |
| Dekaf | 2026-07-26T03:26:28.638408+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 309.0s / 353 msg/s |
| Dekaf | 2026-07-26T03:26:37.6404134+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 318.1s / 345 msg/s |
| Dekaf | 2026-07-26T03:26:46.6474894+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 327.1s / 356 msg/s |
| Dekaf | 2026-07-26T03:26:55.6527492+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.1s / 345 msg/s |
| Dekaf | 2026-07-26T03:27:04.6563358+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 345.1s / 354 msg/s |
| Dekaf | 2026-07-26T03:27:13.6615985+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 341 msg/s |
| Dekaf | 2026-07-26T03:27:23.6741828+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 364.1s / 344 msg/s |
| Dekaf | 2026-07-26T03:27:32.6795475+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 373.1s / 342 msg/s |
| Dekaf | 2026-07-26T03:27:41.7023944+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 382.1s / 340 msg/s |
| Dekaf | 2026-07-26T03:27:50.7064213+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 391.1s / 348 msg/s |
| Dekaf | 2026-07-26T03:27:59.729977+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 400.1s / 360 msg/s |
| Dekaf | 2026-07-26T03:28:08.7369734+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 409.1s / 362 msg/s |
| Dekaf | 2026-07-26T03:28:17.7412184+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 418.1s / 344 msg/s |
| Dekaf | 2026-07-26T03:28:26.7459212+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 427.1s / 358 msg/s |
| Dekaf | 2026-07-26T03:28:35.7560658+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 436.1s / 360 msg/s |
| Dekaf | 2026-07-26T03:28:44.7570856+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 445.1s / 365 msg/s |
| Dekaf | 2026-07-26T03:28:53.7646785+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 454.1s / 368 msg/s |
| Dekaf | 2026-07-26T03:29:02.7769091+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 463.1s / 353 msg/s |
| Dekaf | 2026-07-26T03:29:12.783997+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 365 msg/s |
| Dekaf | 2026-07-26T03:29:21.800853+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 358 msg/s |
| Dekaf | 2026-07-26T03:29:30.8039675+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 357 msg/s |
| Dekaf | 2026-07-26T03:29:39.8116637+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 364 msg/s |
| Dekaf | 2026-07-26T03:29:48.8169709+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 509.1s / 356 msg/s |
| Dekaf | 2026-07-26T03:29:57.8243582+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 518.1s / 348 msg/s |
| Dekaf | 2026-07-26T03:30:06.8323368+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 527.1s / 341 msg/s |
| Dekaf | 2026-07-26T03:30:15.8557908+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 536.1s / 346 msg/s |
| Dekaf | 2026-07-26T03:30:24.8627041+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 545.1s / 352 msg/s |
| Dekaf | 2026-07-26T03:30:33.8739644+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 555.1s / 360 msg/s |
| Dekaf | 2026-07-26T03:30:42.8922518+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 564.1s / 359 msg/s |
| Dekaf | 2026-07-26T03:30:51.9010636+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 573.1s / 363 msg/s |
| Dekaf | 2026-07-26T03:31:01.9217057+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 583.1s / 364 msg/s |
| Dekaf | 2026-07-26T03:31:10.9259937+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 592.1s / 361 msg/s |
| Dekaf | 2026-07-26T03:31:19.9448875+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 601.1s / 353 msg/s |
| Dekaf | 2026-07-26T03:31:28.9472941+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 610.1s / 356 msg/s |
| Dekaf | 2026-07-26T03:31:37.9532133+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 619.1s / 358 msg/s |
| Dekaf | 2026-07-26T03:31:46.9594775+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 628.1s / 360 msg/s |
| Dekaf | 2026-07-26T03:31:55.9656998+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 637.1s / 361 msg/s |
| Dekaf | 2026-07-26T03:32:04.9759991+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 646.1s / 353 msg/s |
| Dekaf | 2026-07-26T03:32:13.989159+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 655.1s / 355 msg/s |
| Dekaf | 2026-07-26T03:32:23.007397+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 664.1s / 363 msg/s |
| Dekaf | 2026-07-26T03:32:32.0336696+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 354 msg/s |
| Dekaf | 2026-07-26T03:32:41.0382562+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 348 msg/s |
| Dekaf | 2026-07-26T03:32:50.0454092+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 348 msg/s |
| Dekaf | 2026-07-26T03:33:00.0487847+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 701.1s / 348 msg/s |
| Dekaf | 2026-07-26T03:33:09.0519824+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 710.1s / 357 msg/s |
| Dekaf | 2026-07-26T03:33:18.0574912+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 719.1s / 356 msg/s |
| Dekaf | 2026-07-26T03:33:27.0812294+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 728.1s / 361 msg/s |
| Dekaf | 2026-07-26T03:33:36.0903217+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 737.1s / 361 msg/s |
| Dekaf | 2026-07-26T03:33:45.0966799+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 746.1s / 364 msg/s |
| Dekaf | 2026-07-26T03:33:54.1043772+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 755.1s / 356 msg/s |
| Dekaf | 2026-07-26T03:34:03.1113347+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 764.1s / 353 msg/s |
| Dekaf | 2026-07-26T03:34:12.1382377+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 773.1s / 356 msg/s |
| Dekaf | 2026-07-26T03:34:21.1400502+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 782.1s / 350 msg/s |
| Dekaf | 2026-07-26T03:34:30.155345+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 791.1s / 358 msg/s |
| Dekaf | 2026-07-26T03:34:39.1610434+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 351 msg/s |
| Dekaf | 2026-07-26T03:34:49.162761+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 810.1s / 358 msg/s |
| Dekaf | 2026-07-26T03:34:58.1714807+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 819.1s / 362 msg/s |
| Dekaf | 2026-07-26T03:35:07.1844771+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 828.1s / 359 msg/s |
| Dekaf | 2026-07-26T03:35:16.1947507+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 837.1s / 342 msg/s |
| Dekaf | 2026-07-26T03:35:25.2536878+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 846.1s / 351 msg/s |
| Dekaf | 2026-07-26T03:35:34.2724539+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 855.1s / 355 msg/s |
| Dekaf | 2026-07-26T03:35:43.2809121+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 864.1s / 343 msg/s |
| Dekaf | 2026-07-26T03:35:52.292942+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 873.1s / 351 msg/s |
| Dekaf | 2026-07-26T03:36:01.3133248+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 882.1s / 359 msg/s |
| Dekaf | 2026-07-26T03:36:10.3163873+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 891.1s / 335 msg/s |
| Dekaf | 2026-07-26T03:36:19.3179642+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 899.1s / 346 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 154,300 | 115,800 | 38,500 | 115,800 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 317,800 | 238,400 | 79,400 | 238,400 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.54x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.06x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.76 | - | 1,711,124 | 1,713,190 | -5.9% | -0.52% | 1631.86 | - | 0 | 1.30 |
| Confluent | 1.02 | - | 1,345,032 | 1,407,561 | -3.6% | -0.35% | 1282.72 | - | 0 | 1.37 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.34x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.22x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.74 | - | 1,847,507 | 1,862,474 | +12.3% | +1.11% | 1761.92 | - | 0 | 1.37 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.43 | - | 3,635,600 | 3,666,473 | -0.9% | -0.12% | 3467.18 | - | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.37 | - | 4,149,602 | 4,083,544 | +1.9% | +0.19% | 3957.37 | - | 0 | 1.53 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 71710 | 0 | 0 | 2750.84 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 322161 | 1 | 1 | 1550.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 309857 | 12 | 1 | 1471.91 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 213249 | 0 | 0 | 1033.87 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 304451 | 18 | 1 | 1472.31 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 276178 | 1 | 1 | 1455.13 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 196788 | 0 | 0 | 965.65 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 314808 | 1 | 1 | 1550.38 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 323662 | 25 | 1 | 1534.13 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 194103 | 0 | 0 | 914.17 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 8486 | 4 | 4 | 21.41 GB | 1.13 KB |
| Confluent | Producer (Transactional EOS), 3 Brokers | 97 | 2 | 1 | 254.34 MB | 1.69 KB |
| Dekaf | Consumer | 76339 | 23 | 2 | 2903.39 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 82353 | 4 | 1 | 3135.12 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 0 | 491.52 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 16 | 1 | 0 | 1.00 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 422 | 2 | 2 | 146.88 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 433 | 2 | 2 | 1.61 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 264 | 5 | 2 | 121.03 MB | 0 B |
| Dekaf | Producer (Acks All) | 421 | 3 | 2 | 1.60 GB | 1 B |
| Dekaf | Producer (Acks All) | 464 | 2 | 2 | 131.56 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 257 | 3 | 2 | 132.49 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 407 | 2 | 2 | 164.97 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 409 | 2 | 2 | 1.54 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 221 | 3 | 2 | 109.08 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 2159 | 3 | 2 | 5.63 GB | 306 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 43 | 1 | 1 | 110.73 MB | 365 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 324 | 2 | 2 | 1.20 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 274 | 4 | 2 | 1.06 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 309 | 3 | 3 | 1.22 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 245 | 3 | 2 | 994.83 MB | 1 B |

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
