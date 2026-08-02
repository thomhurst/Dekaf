---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-02 04:22 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,572,592 | 1,557,667–1,587,660 | 0.94 | 1.10x |
| Confluent | 2 | 1,431,097 | 1,413,396–1,449,018 | 1.26 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.71 | 679.98 | 1,879,709 | 1,891,295 | +25.4% | +2.08% | 1792.63 | 1,879,709 | 0 | 1.33 |
| Dekaf (dekaf-first) | 0.94 | 962.75 | 1,575,370 | 1,587,660 | -0.7% | -0.02% | 1502.39 | 1,575,370 | 0 | 1.48 |
| Dekaf (confluent-first) | 0.94 | 960.47 | 1,545,980 | 1,557,667 | +2.8% | +0.27% | 1474.36 | 1,545,980 | 0 | 1.45 |
| Confluent (dekaf-first) | 1.25 | - | 1,427,878 | 1,449,018 | +2.4% | +0.32% | 1361.73 | 1,427,878 | 0 | 1.79 |
| Confluent (confluent-first) | 1.28 | - | 1,377,623 | 1,413,396 | -2.5% | -0.03% | 1313.80 | 1,377,623 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,381,512 | 1534.99 | 1020.20 KB |
| Dekaf | 1 | 1,357,816 | 1508.67 | 1018.64 KB |
| Dekaf (3conn) | 1 | 1,760,712 | 1956.33 | 955.12 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:20.5485371+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 547,638 msg/s |
| Dekaf | 2026-08-02T03:06:47.5553276+00:00 | 1 | 16.0 MiB / 13.1 MiB | 1765.4 MB/s | 0/0 | 34,875 | 27.0s / 1,644,196 msg/s |
| Dekaf | 2026-08-02T03:07:15.5651015+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1782.2 MB/s | 0/1 | 82,303 | 55.0s / 1,658,880 msg/s |
| Dekaf | 2026-08-02T03:07:42.5721848+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1782.2 MB/s | 0/1 | 119,589 | 82.0s / 1,611,364 msg/s |
| Dekaf | 2026-08-02T03:08:09.5809586+00:00 | 1 | 18.0 MiB / 16.2 MiB | 1793.4 MB/s | 0/1 | 154,268 | 109.0s / 1,615,554 msg/s |
| Dekaf | 2026-08-02T03:08:36.5920099+00:00 | 1 | 18.0 MiB / 14.9 MiB | 1793.4 MB/s | 1/1 | 190,722 | 136.1s / 1,647,348 msg/s |
| Dekaf | 2026-08-02T03:09:04.6096137+00:00 | 1 | 20.0 MiB / 18.1 MiB | 1795.3 MB/s | 1/1 | 223,204 | 164.1s / 1,691,806 msg/s |
| Dekaf | 2026-08-02T03:09:31.6272453+00:00 | 1 | 20.0 MiB / 16.1 MiB | 1795.3 MB/s | 2/1 | 256,558 | 191.1s / 1,619,713 msg/s |
| Dekaf | 2026-08-02T03:09:58.6412847+00:00 | 1 | 20.0 MiB / 19.9 MiB | 1795.3 MB/s | 2/2 | 288,438 | 218.1s / 1,500,753 msg/s |
| Dekaf | 2026-08-02T03:10:25.6545293+00:00 | 1 | 20.0 MiB / 18.2 MiB | 1795.3 MB/s | 2/2 | 314,268 | 245.1s / 1,560,412 msg/s |
| Dekaf | 2026-08-02T03:10:53.6647592+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1795.3 MB/s | 3/2 | 351,412 | 273.1s / 1,544,588 msg/s |
| Dekaf | 2026-08-02T03:11:20.670463+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1795.3 MB/s | 4/2 | 397,278 | 300.1s / 1,505,533 msg/s |
| Dekaf | 2026-08-02T03:11:47.6825432+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1795.3 MB/s | 4/3 | 442,321 | 327.1s / 1,558,237 msg/s |
| Dekaf | 2026-08-02T03:12:14.6895113+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1795.3 MB/s | 4/3 | 487,402 | 354.2s / 1,528,064 msg/s |
| Dekaf | 2026-08-02T03:12:42.6955879+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1795.3 MB/s | 4/3 | 535,724 | 382.2s / 1,554,263 msg/s |
| Dekaf | 2026-08-02T03:13:09.707978+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1795.3 MB/s | 5/3 | 576,564 | 409.2s / 1,535,984 msg/s |
| Dekaf | 2026-08-02T03:13:36.7164996+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1795.3 MB/s | 5/3 | 611,553 | 436.2s / 1,554,664 msg/s |
| Dekaf | 2026-08-02T03:14:03.7283135+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1795.3 MB/s | 5/4 | 661,091 | 463.2s / 1,623,588 msg/s |
| Dekaf | 2026-08-02T03:14:31.7416366+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1795.3 MB/s | 5/4 | 719,959 | 491.2s / 1,638,304 msg/s |
| Dekaf | 2026-08-02T03:14:58.7492671+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1795.3 MB/s | 6/4 | 781,820 | 518.2s / 1,425,609 msg/s |
| Dekaf | 2026-08-02T03:15:25.7529573+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1795.3 MB/s | 6/4 | 848,743 | 545.2s / 1,297,589 msg/s |
| Dekaf | 2026-08-02T03:15:53.7676942+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1795.3 MB/s | 6/5 | 920,524 | 573.2s / 1,662,571 msg/s |
| Dekaf | 2026-08-02T03:16:20.7733269+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1795.3 MB/s | 6/5 | 994,913 | 600.2s / 1,628,805 msg/s |
| Dekaf | 2026-08-02T03:16:47.7824636+00:00 | 1 | 13.0 MiB / 11.1 MiB | 1795.3 MB/s | 6/5 | 1,065,220 | 627.2s / 1,652,471 msg/s |
| Dekaf | 2026-08-02T03:17:14.7952495+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1795.3 MB/s | 7/5 | 1,139,331 | 654.2s / 1,643,925 msg/s |
| Dekaf | 2026-08-02T03:17:42.8066107+00:00 | 1 | 15.0 MiB / 14.3 MiB | 1795.3 MB/s | 8/5 | 1,205,509 | 682.3s / 1,650,298 msg/s |
| Dekaf | 2026-08-02T03:18:09.8197398+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1795.3 MB/s | 8/5 | 1,262,226 | 709.3s / 1,625,728 msg/s |
| Dekaf | 2026-08-02T03:18:36.8319424+00:00 | 1 | 15.0 MiB / 13.9 MiB | 1795.3 MB/s | 8/6 | 1,305,326 | 736.3s / 1,531,517 msg/s |
| Dekaf | 2026-08-02T03:19:03.8405948+00:00 | 1 | 15.0 MiB / 13.0 MiB | 1795.3 MB/s | 8/6 | 1,350,214 | 763.3s / 1,526,406 msg/s |
| Dekaf | 2026-08-02T03:19:31.8514609+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1795.3 MB/s | 8/6 | 1,400,146 | 791.3s / 1,555,676 msg/s |
| Dekaf | 2026-08-02T03:19:58.8598635+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1795.3 MB/s | 8/7 | 1,447,800 | 818.3s / 1,576,935 msg/s |
| Dekaf | 2026-08-02T03:20:25.8669436+00:00 | 1 | 15.0 MiB / 14.6 MiB | 1795.3 MB/s | 8/7 | 1,493,620 | 845.3s / 1,570,484 msg/s |
| Dekaf | 2026-08-02T03:20:52.8755934+00:00 | 1 | 15.0 MiB / 14.0 MiB | 1795.3 MB/s | 8/7 | 1,538,134 | 872.3s / 1,584,735 msg/s |
| Dekaf | 2026-08-02T03:51:21.830174+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 671,490 msg/s |
| Dekaf | 2026-08-02T03:51:48.8381554+00:00 | 1 | 16.0 MiB / 14.7 MiB | 1640.3 MB/s | 0/0 | 35,366 | 27.0s / 1,524,344 msg/s |
| Dekaf | 2026-08-02T03:52:15.8521651+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1640.3 MB/s | 1/0 | 87,868 | 54.0s / 1,550,795 msg/s |
| Dekaf | 2026-08-02T03:52:42.8692422+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1665.4 MB/s | 1/0 | 147,047 | 81.0s / 1,559,572 msg/s |
| Dekaf | 2026-08-02T03:53:10.8780182+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1665.4 MB/s | 2/0 | 207,175 | 109.0s / 1,555,783 msg/s |
| Dekaf | 2026-08-02T03:53:37.8853597+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1665.4 MB/s | 2/1 | 263,567 | 136.1s / 1,535,531 msg/s |
| Dekaf | 2026-08-02T03:54:04.8931702+00:00 | 1 | 12.0 MiB / 9.1 MiB | 1665.4 MB/s | 2/1 | 326,164 | 163.1s / 1,558,261 msg/s |
| Dekaf | 2026-08-02T03:54:32.8979589+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1665.4 MB/s | 2/1 | 390,341 | 191.1s / 1,550,540 msg/s |
| Dekaf | 2026-08-02T03:54:59.9070649+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1665.4 MB/s | 3/1 | 455,693 | 218.1s / 1,481,566 msg/s |
| Dekaf | 2026-08-02T03:55:26.9178894+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1669.4 MB/s | 3/1 | 518,124 | 245.1s / 1,539,052 msg/s |
| Dekaf | 2026-08-02T03:55:53.9312392+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1679.2 MB/s | 4/1 | 578,533 | 272.1s / 1,550,310 msg/s |
| Dekaf | 2026-08-02T03:56:21.9459626+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1679.2 MB/s | 4/1 | 635,786 | 300.1s / 1,462,603 msg/s |
| Dekaf | 2026-08-02T03:56:48.9527879+00:00 | 1 | 14.0 MiB / 8.6 MiB | 1704.6 MB/s | 4/2 | 696,490 | 327.1s / 1,577,865 msg/s |
| Dekaf | 2026-08-02T03:57:15.9625005+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1730.2 MB/s | 4/2 | 758,807 | 354.1s / 1,594,077 msg/s |
| Dekaf | 2026-08-02T03:57:42.9736884+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1738.3 MB/s | 5/2 | 824,750 | 381.1s / 1,560,633 msg/s |
| Dekaf | 2026-08-02T03:58:10.9821021+00:00 | 1 | 10.0 MiB / 9.6 MiB | 1758.4 MB/s | 5/2 | 895,843 | 409.2s / 1,290,371 msg/s |
| Dekaf | 2026-08-02T03:58:37.9919151+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1758.4 MB/s | 5/3 | 966,732 | 436.2s / 1,592,678 msg/s |
| Dekaf | 2026-08-02T03:59:04.9994069+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1758.4 MB/s | 5/3 | 1,041,845 | 463.2s / 1,600,360 msg/s |
| Dekaf | 2026-08-02T03:59:32.0091115+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1758.4 MB/s | 5/3 | 1,119,784 | 490.2s / 1,547,746 msg/s |
| Dekaf | 2026-08-02T04:00:00.0169362+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1758.4 MB/s | 6/3 | 1,195,413 | 518.2s / 1,557,667 msg/s |
| Dekaf | 2026-08-02T04:00:27.0283728+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1758.4 MB/s | 7/3 | 1,267,522 | 545.2s / 1,571,715 msg/s |
| Dekaf | 2026-08-02T04:00:54.0392716+00:00 | 1 | 15.0 MiB / 5.6 MiB | 1758.4 MB/s | 7/3 | 1,333,924 | 572.2s / 1,600,969 msg/s |
| Dekaf | 2026-08-02T04:01:21.0515905+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1758.4 MB/s | 7/4 | 1,397,024 | 599.2s / 1,491,881 msg/s |
| Dekaf | 2026-08-02T04:01:49.0618206+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1758.4 MB/s | 7/4 | 1,466,737 | 627.2s / 1,610,082 msg/s |
| Dekaf | 2026-08-02T04:02:16.0682806+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1758.4 MB/s | 7/4 | 1,535,202 | 654.2s / 1,592,347 msg/s |
| Dekaf | 2026-08-02T04:02:43.0750744+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1758.4 MB/s | 7/5 | 1,605,729 | 681.2s / 1,607,342 msg/s |
| Dekaf | 2026-08-02T04:03:10.0819471+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1758.4 MB/s | 7/5 | 1,677,721 | 708.2s / 1,564,441 msg/s |
| Dekaf | 2026-08-02T04:03:38.0870607+00:00 | 1 | 14.0 MiB / 12.7 MiB | 1758.4 MB/s | 7/5 | 1,749,524 | 736.2s / 1,561,836 msg/s |
| Dekaf | 2026-08-02T04:04:05.0942057+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1758.4 MB/s | 7/5 | 1,822,932 | 763.2s / 1,455,818 msg/s |
| Dekaf | 2026-08-02T04:04:32.1033545+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1758.4 MB/s | 7/5 | 1,894,544 | 790.3s / 1,616,161 msg/s |
| Dekaf | 2026-08-02T04:05:00.1231609+00:00 | 1 | 15.0 MiB / 14.5 MiB | 1758.4 MB/s | 8/5 | 1,954,127 | 818.3s / 1,597,893 msg/s |
| Dekaf | 2026-08-02T04:05:27.1374537+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1758.4 MB/s | 9/5 | 2,010,089 | 845.3s / 1,508,217 msg/s |
| Dekaf | 2026-08-02T04:05:54.1544564+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1758.4 MB/s | 9/5 | 2,057,969 | 872.3s / 1,588,299 msg/s |
| Dekaf | 2026-08-02T04:06:21.1675854+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1758.4 MB/s | 9/6 | 2,103,141 | 899.3s / 1,564,386 msg/s |
| Dekaf (3conn) | 2026-08-02T04:06:49.6887901+00:00 | 1 | 16.0 MiB / 4.0 MiB | 2017.0 MB/s | 0/0 | 1,356 | 27.0s / 1,395,140 msg/s |
| Dekaf (3conn) | 2026-08-02T04:07:16.6930411+00:00 | 1 | 14.0 MiB / 5.6 MiB | 2017.0 MB/s | 1/0 | 2,769 | 54.0s / 1,358,293 msg/s |
| Dekaf (3conn) | 2026-08-02T04:07:43.698903+00:00 | 1 | 14.0 MiB / 10.2 MiB | 2017.0 MB/s | 1/0 | 6,266 | 81.0s / 1,921,765 msg/s |
| Dekaf (3conn) | 2026-08-02T04:08:10.7089556+00:00 | 1 | 12.0 MiB / 7.3 MiB | 2168.2 MB/s | 2/0 | 12,445 | 108.1s / 1,958,114 msg/s |
| Dekaf (3conn) | 2026-08-02T04:08:38.7210752+00:00 | 1 | 12.0 MiB / 2.4 MiB | 2802.0 MB/s | 2/1 | 20,551 | 136.1s / 1,873,737 msg/s |
| Dekaf (3conn) | 2026-08-02T04:09:05.7382581+00:00 | 1 | 12.0 MiB / 4.2 MiB | 2802.0 MB/s | 2/1 | 27,536 | 163.1s / 1,786,794 msg/s |
| Dekaf (3conn) | 2026-08-02T04:09:32.7449655+00:00 | 1 | 12.0 MiB / 8.7 MiB | 2802.0 MB/s | 2/1 | 33,065 | 190.1s / 2,008,988 msg/s |
| Dekaf (3conn) | 2026-08-02T04:09:59.7511806+00:00 | 1 | 12.0 MiB / 3.1 MiB | 2802.0 MB/s | 2/2 | 37,748 | 217.1s / 1,316,863 msg/s |
| Dekaf (3conn) | 2026-08-02T04:10:27.7600207+00:00 | 1 | 12.0 MiB / 8.7 MiB | 2802.0 MB/s | 2/2 | 42,738 | 245.1s / 1,684,200 msg/s |
| Dekaf (3conn) | 2026-08-02T04:10:54.7674184+00:00 | 1 | 12.0 MiB / 2.6 MiB | 2802.0 MB/s | 2/2 | 47,953 | 272.1s / 2,077,432 msg/s |
| Dekaf (3conn) | 2026-08-02T04:11:21.7819006+00:00 | 1 | 12.0 MiB / 3.2 MiB | 2802.0 MB/s | 2/2 | 54,636 | 299.1s / 1,607,223 msg/s |
| Dekaf (3conn) | 2026-08-02T04:11:48.7949383+00:00 | 1 | 12.0 MiB / 9.9 MiB | 2802.0 MB/s | 2/2 | 61,953 | 326.1s / 2,008,066 msg/s |
| Dekaf (3conn) | 2026-08-02T04:12:16.8120458+00:00 | 1 | 12.0 MiB / 3.9 MiB | 2802.0 MB/s | 2/3 | 70,568 | 354.2s / 1,971,277 msg/s |
| Dekaf (3conn) | 2026-08-02T04:12:43.8242748+00:00 | 1 | 12.0 MiB / 2.9 MiB | 2802.0 MB/s | 2/3 | 76,699 | 381.2s / 1,957,922 msg/s |
| Dekaf (3conn) | 2026-08-02T04:13:10.8439544+00:00 | 1 | 12.0 MiB / 3.9 MiB | 2802.0 MB/s | 2/3 | 82,733 | 408.2s / 1,731,884 msg/s |
| Dekaf (3conn) | 2026-08-02T04:13:38.8632897+00:00 | 1 | 12.0 MiB / 6.9 MiB | 2802.0 MB/s | 2/3 | 88,855 | 436.2s / 1,993,259 msg/s |
| Dekaf (3conn) | 2026-08-02T04:14:05.8764449+00:00 | 1 | 12.0 MiB / 11.6 MiB | 2802.0 MB/s | 2/3 | 94,373 | 463.2s / 1,700,664 msg/s |
| Dekaf (3conn) | 2026-08-02T04:14:32.8854853+00:00 | 1 | 12.0 MiB / 11.3 MiB | 2802.0 MB/s | 2/3 | 99,873 | 490.2s / 1,490,851 msg/s |
| Dekaf (3conn) | 2026-08-02T04:14:59.8911407+00:00 | 1 | 12.0 MiB / 6.2 MiB | 2802.0 MB/s | 2/3 | 103,826 | 517.2s / 1,729,548 msg/s |
| Dekaf (3conn) | 2026-08-02T04:15:27.8991062+00:00 | 1 | 12.0 MiB / 5.1 MiB | 2802.0 MB/s | 2/3 | 108,762 | 545.2s / 1,430,461 msg/s |
| Dekaf (3conn) | 2026-08-02T04:15:54.9117656+00:00 | 1 | 12.0 MiB / 6.7 MiB | 2802.0 MB/s | 2/3 | 114,295 | 572.2s / 1,703,604 msg/s |
| Dekaf (3conn) | 2026-08-02T04:16:21.9269637+00:00 | 1 | 13.0 MiB / 9.3 MiB | 2802.0 MB/s | 2/3 | 120,453 | 599.2s / 1,618,752 msg/s |
| Dekaf (3conn) | 2026-08-02T04:16:48.9381654+00:00 | 1 | 13.0 MiB / 12.0 MiB | 2802.0 MB/s | 3/3 | 124,599 | 626.3s / 2,005,220 msg/s |
| Dekaf (3conn) | 2026-08-02T04:17:16.9488056+00:00 | 1 | 13.0 MiB / 6.8 MiB | 2802.0 MB/s | 3/4 | 130,564 | 654.3s / 2,310,128 msg/s |
| Dekaf (3conn) | 2026-08-02T04:17:43.9572717+00:00 | 1 | 13.0 MiB / 13.0 MiB | 2826.7 MB/s | 3/4 | 137,911 | 681.3s / 2,252,728 msg/s |
| Dekaf (3conn) | 2026-08-02T04:18:10.9692125+00:00 | 1 | 11.0 MiB / 3.2 MiB | 2826.7 MB/s | 3/4 | 145,126 | 708.3s / 2,154,370 msg/s |
| Dekaf (3conn) | 2026-08-02T04:18:37.9749389+00:00 | 1 | 13.0 MiB / 12.7 MiB | 2826.7 MB/s | 3/5 | 152,408 | 735.3s / 1,724,063 msg/s |
| Dekaf (3conn) | 2026-08-02T04:19:05.9810619+00:00 | 1 | 13.0 MiB / 5.1 MiB | 2826.7 MB/s | 3/5 | 158,254 | 763.3s / 1,938,264 msg/s |
| Dekaf (3conn) | 2026-08-02T04:19:32.991936+00:00 | 1 | 13.0 MiB / 8.1 MiB | 2826.7 MB/s | 3/5 | 164,839 | 790.3s / 2,358,534 msg/s |
| Dekaf (3conn) | 2026-08-02T04:19:59.9961206+00:00 | 1 | 13.0 MiB / 13.0 MiB | 2826.7 MB/s | 3/5 | 171,661 | 817.3s / 2,247,868 msg/s |
| Dekaf (3conn) | 2026-08-02T04:20:27.0068069+00:00 | 1 | 14.0 MiB / 1.6 MiB | 2826.7 MB/s | 3/5 | 178,039 | 844.3s / 2,165,737 msg/s |
| Dekaf (3conn) | 2026-08-02T04:20:55.0165149+00:00 | 1 | 13.0 MiB / 1.8 MiB | 2826.7 MB/s | 3/6 | 185,125 | 872.4s / 2,242,305 msg/s |
| Dekaf (3conn) | 2026-08-02T04:21:22.0319662+00:00 | 1 | 13.0 MiB / 4.4 MiB | 2826.7 MB/s | 3/6 | 191,565 | 899.4s / 1,945,481 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-02T03:06:50.6485776+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.9 MiB |
| Dekaf | 2026-08-02T03:07:05.6608996+00:00 | 1 | capacity | failed | 15,012ms | 16.0 MiB / 13.9 MiB |
| Dekaf | 2026-08-02T03:08:05.7265795+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.6 MiB |
| Dekaf | 2026-08-02T03:08:20.7485357+00:00 | 1 | capacity | succeeded | 15,021ms | 18.0 MiB / 17.0 MiB |
| Dekaf | 2026-08-02T03:08:50.7909675+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T03:09:05.8070811+00:00 | 1 | capacity | succeeded | 15,016ms | 20.0 MiB / 18.1 MiB |
| Dekaf | 2026-08-02T03:09:35.8663293+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 16.1 MiB |
| Dekaf | 2026-08-02T03:09:50.9048603+00:00 | 1 | capacity | failed | 15,038ms | 20.0 MiB / 18.8 MiB |
| Dekaf | 2026-08-02T03:10:20.9678452+00:00 | 1 | capacity | started | 0ms | 17.0 MiB / 16.0 MiB |
| Dekaf | 2026-08-02T03:10:35.9901857+00:00 | 1 | capacity | succeeded | 15,022ms | 17.0 MiB / 12.4 MiB |
| Dekaf | 2026-08-02T03:10:38.9935307+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.4 MiB |
| Dekaf | 2026-08-02T03:10:54.0153349+00:00 | 1 | capacity | succeeded | 15,021ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:11:24.0447203+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:11:39.0571589+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:12:39.1108492+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 11.2 MiB |
| Dekaf | 2026-08-02T03:12:54.1272125+00:00 | 1 | capacity | succeeded | 15,016ms | 15.0 MiB / 11.8 MiB |
| Dekaf | 2026-08-02T03:13:24.154496+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.6 MiB |
| Dekaf | 2026-08-02T03:13:39.1691552+00:00 | 1 | capacity | failed | 15,014ms | 15.0 MiB / 14.5 MiB |
| Dekaf | 2026-08-02T03:14:39.2165847+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T03:14:54.2285445+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 10.7 MiB |
| Dekaf | 2026-08-02T03:15:24.2587374+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 8.7 MiB |
| Dekaf | 2026-08-02T03:15:39.2718956+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 8.6 MiB |
| Dekaf | 2026-08-02T03:16:39.319479+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:16:54.3333968+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:17:24.3615395+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.7 MiB |
| Dekaf | 2026-08-02T03:17:39.3900806+00:00 | 1 | capacity | succeeded | 15,028ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:18:09.4181257+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T03:18:24.4367861+00:00 | 1 | capacity | failed | 15,018ms | 15.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T03:19:24.4931718+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.6 MiB |
| Dekaf | 2026-08-02T03:19:39.5092104+00:00 | 1 | capacity | failed | 15,016ms | 15.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:51:51.9380458+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.5 MiB |
| Dekaf | 2026-08-02T03:52:06.9510242+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 14.0 MiB |
| Dekaf | 2026-08-02T03:52:36.9798428+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:52:51.992587+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:53:22.0194091+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.7 MiB |
| Dekaf | 2026-08-02T03:53:37.0341126+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 9.7 MiB |
| Dekaf | 2026-08-02T03:54:37.0913213+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.3 MiB |
| Dekaf | 2026-08-02T03:54:52.1036008+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 11.9 MiB |
| Dekaf | 2026-08-02T03:55:22.1315266+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 8.0 MiB |
| Dekaf | 2026-08-02T03:55:37.1478052+00:00 | 1 | capacity | succeeded | 15,016ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:56:07.1730159+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:56:22.1901889+00:00 | 1 | capacity | failed | 15,017ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T03:57:22.2458226+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:57:37.2649047+00:00 | 1 | capacity | succeeded | 15,019ms | 12.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:58:07.2855596+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:58:22.3008572+00:00 | 1 | capacity | failed | 15,015ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:59:22.3458806+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:59:37.3732354+00:00 | 1 | capacity | succeeded | 15,027ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T04:00:07.4050495+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T04:00:22.4168247+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 10.9 MiB |
| Dekaf | 2026-08-02T04:00:52.4401972+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T04:01:07.4555249+00:00 | 1 | capacity | failed | 15,015ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:02:07.5056616+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T04:02:22.5183195+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 10.9 MiB |
| Dekaf | 2026-08-02T04:04:22.6056112+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.6 MiB |
| Dekaf | 2026-08-02T04:04:37.6174973+00:00 | 1 | capacity | succeeded | 15,012ms | 15.0 MiB / 14.0 MiB |
| Dekaf | 2026-08-02T04:05:07.6425701+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.2 MiB |
| Dekaf | 2026-08-02T04:05:22.6575495+00:00 | 1 | capacity | succeeded | 15,014ms | 16.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T04:05:52.6889363+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:06:07.7072991+00:00 | 1 | capacity | failed | 15,018ms | 16.0 MiB / 16.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:06:52.7973809+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-08-02T04:07:07.8206709+00:00 | 1 | capacity | succeeded | 15,023ms | 14.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-08-02T04:07:37.8780975+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:07:52.905453+00:00 | 1 | capacity | succeeded | 15,027ms | 12.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-08-02T04:08:22.9517323+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 7.6 MiB |
| Dekaf (3conn) | 2026-08-02T04:08:37.9688714+00:00 | 1 | capacity | failed | 15,016ms | 12.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-08-02T04:09:38.052988+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-08-02T04:09:53.0717544+00:00 | 1 | capacity | failed | 15,018ms | 12.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-08-02T04:11:53.2371805+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-08-02T04:12:08.2600259+00:00 | 1 | capacity | failed | 15,022ms | 12.0 MiB / 7.0 MiB |
| Dekaf (3conn) | 2026-08-02T04:16:08.5931913+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 7.5 MiB |
| Dekaf (3conn) | 2026-08-02T04:16:23.6109668+00:00 | 1 | capacity | succeeded | 15,017ms | 13.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-08-02T04:16:53.6458679+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-08-02T04:17:08.6644756+00:00 | 1 | capacity | failed | 15,018ms | 13.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:18:08.7351521+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 9.8 MiB |
| Dekaf (3conn) | 2026-08-02T04:18:23.7515103+00:00 | 1 | capacity | failed | 15,016ms | 13.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-08-02T04:20:23.9440076+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:20:38.9719812+00:00 | 1 | capacity | failed | 15,027ms | 13.0 MiB / 3.9 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 114 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 110 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 308 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 782 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 2,786 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 6,170 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 5,974 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 10,775 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 11,751 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 9,541 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 4,929 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,262 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 251 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 21 |
| Dekaf | 1 | 0.001–0.002ms | 1,713 |
| Dekaf | 1 | 0.002–0.004ms | 2,088 |
| Dekaf | 1 | 0.004–0.008ms | 6,625 |
| Dekaf | 1 | 0.008–0.016ms | 35,080 |
| Dekaf | 1 | 0.016–0.032ms | 41,158 |
| Dekaf | 1 | 0.032–0.064ms | 38,553 |
| Dekaf | 1 | 0.064–0.128ms | 71,673 |
| Dekaf | 1 | 0.128–0.256ms | 163,200 |
| Dekaf | 1 | 0.256–0.512ms | 155,346 |
| Dekaf | 1 | 0.512–1.024ms | 76,183 |
| Dekaf | 1 | 1.024–2.048ms | 23,015 |
| Dekaf | 1 | 2.048–4.096ms | 3,876 |
| Dekaf | 1 | 4.096–8.192ms | 1,081 |
| Dekaf | 1 | 8.192–16.384ms | 107 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,923 |
| Dekaf | 1 | 0.002–0.004ms | 2,488 |
| Dekaf | 1 | 0.004–0.008ms | 8,442 |
| Dekaf | 1 | 0.008–0.016ms | 40,806 |
| Dekaf | 1 | 0.016–0.032ms | 44,559 |
| Dekaf | 1 | 0.032–0.064ms | 45,497 |
| Dekaf | 1 | 0.064–0.128ms | 89,633 |
| Dekaf | 1 | 0.128–0.256ms | 242,058 |
| Dekaf | 1 | 0.256–0.512ms | 263,714 |
| Dekaf | 1 | 0.512–1.024ms | 72,273 |
| Dekaf | 1 | 1.024–2.048ms | 20,136 |
| Dekaf | 1 | 2.048–4.096ms | 3,621 |
| Dekaf | 1 | 4.096–8.192ms | 834 |
| Dekaf | 1 | 8.192–16.384ms | 56 |
| Dekaf | 1 | 16.384–32.768ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 389,342,000 | 2026-08-02T03:10:26.3443932+00:00 | 117.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,344,000 | 2026-08-02T03:10:26.3474661+00:00 | 114.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,345,000 | 2026-08-02T03:10:26.3476755+00:00 | 114.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,346,000 | 2026-08-02T03:10:26.3486448+00:00 | 113.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,347,000 | 2026-08-02T03:10:26.3494225+00:00 | 115.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,348,000 | 2026-08-02T03:10:26.3504284+00:00 | 111.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,349,000 | 2026-08-02T03:10:26.351115+00:00 | 114.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,350,000 | 2026-08-02T03:10:26.3516533+00:00 | 113.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,351,000 | 2026-08-02T03:10:26.3622275+00:00 | 105.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 389,352,000 | 2026-08-02T03:10:26.362706+00:00 | 105.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 246.1s / 1,038,467 msg/s | Gen2 +0 / pause +0.8ms |
| Confluent | 339,747,000 | 2026-08-02T03:25:22.4585829+00:00 | 102.6ms | GC pause | - | - | 242.2s / 1,371,469 msg/s | Gen2 +0 / pause +83.4ms |
| Confluent | 339,748,000 | 2026-08-02T03:25:22.4593906+00:00 | 101.9ms | GC pause | - | - | 242.2s / 1,371,469 msg/s | Gen2 +0 / pause +83.4ms |
| Confluent | 339,758,000 | 2026-08-02T03:25:22.4663343+00:00 | 106.8ms | GC pause | - | - | 242.2s / 1,371,469 msg/s | Gen2 +0 / pause +83.4ms |
| Confluent | 339,761,000 | 2026-08-02T03:25:22.4683133+00:00 | 104.9ms | GC pause | - | - | 242.2s / 1,371,469 msg/s | Gen2 +0 / pause +83.4ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.35x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.10x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.02 | 980.57 | 1,252,815 | 1,254,227 | -1.2% | -0.08% | 1194.78 | 1,252,815 | 0 | 1.28 |
| Dekaf | 1.02 | 976.32 | 1,211,824 | 1,219,113 | +5.5% | +0.50% | 1155.69 | 1,211,824 | 0 | 1.24 |
| Confluent | 1.67 | - | 903,951 | 907,196 | -0.1% | +0.09% | 862.08 | 903,951 | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 372,704 | 414.11 | 948.32 KB |
| Dekaf | 2 | 381,484 | 423.86 | 942.13 KB |
| Dekaf | 3 | 387,461 | 430.50 | 958.38 KB |
| Dekaf (3conn) | 1 | 390,188 | 433.54 | 946.80 KB |
| Dekaf (3conn) | 2 | 400,771 | 445.30 | 957.88 KB |
| Dekaf (3conn) | 3 | 387,124 | 430.13 | 949.37 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:31.4287911+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 435,704 msg/s |
| Dekaf | 2026-08-02T03:06:49.4375047+00:00 | 3 | 16.0 MiB / 6.0 MiB | 387.6 MB/s | 0/0 | 3,434 | 18.1s / 972,500 msg/s |
| Dekaf | 2026-08-02T03:07:08.4446617+00:00 | 1 | 16.0 MiB / 11.9 MiB | 426.2 MB/s | 0/0 | 3,454 | 37.1s / 1,205,903 msg/s |
| Dekaf | 2026-08-02T03:07:26.4535827+00:00 | 1 | 14.0 MiB / 2.4 MiB | 448.6 MB/s | 1/0 | 4,527 | 55.1s / 1,174,162 msg/s |
| Dekaf | 2026-08-02T03:07:44.4680143+00:00 | 2 | 12.0 MiB / 2.6 MiB | 439.1 MB/s | 2/0 | 5,667 | 73.1s / 1,143,216 msg/s |
| Dekaf | 2026-08-02T03:08:02.4796974+00:00 | 2 | 12.0 MiB / 8.6 MiB | 439.1 MB/s | 2/0 | 6,675 | 91.1s / 1,167,704 msg/s |
| Dekaf | 2026-08-02T03:08:20.5007349+00:00 | 3 | 10.0 MiB / 9.7 MiB | 458.1 MB/s | 3/0 | 14,261 | 109.1s / 1,175,981 msg/s |
| Dekaf | 2026-08-02T03:08:38.5219843+00:00 | 3 | 10.0 MiB / 1.6 MiB | 459.8 MB/s | 3/1 | 16,243 | 127.2s / 1,182,792 msg/s |
| Dekaf | 2026-08-02T03:08:57.5289909+00:00 | 1 | 10.0 MiB / 2.9 MiB | 448.6 MB/s | 3/1 | 11,229 | 146.2s / 1,239,186 msg/s |
| Dekaf | 2026-08-02T03:09:15.5355607+00:00 | 1 | 10.0 MiB / 3.0 MiB | 448.6 MB/s | 3/1 | 12,258 | 164.2s / 1,226,093 msg/s |
| Dekaf | 2026-08-02T03:09:33.5449781+00:00 | 2 | 10.0 MiB / 2.6 MiB | 453.1 MB/s | 2/1 | 10,803 | 182.2s / 1,189,991 msg/s |
| Dekaf | 2026-08-02T03:09:51.5515743+00:00 | 2 | 10.0 MiB / 3.2 MiB | 453.1 MB/s | 3/1 | 11,697 | 200.2s / 1,178,532 msg/s |
| Dekaf | 2026-08-02T03:10:09.5661297+00:00 | 3 | 10.0 MiB / 1.9 MiB | 468.2 MB/s | 3/2 | 27,310 | 218.2s / 1,122,831 msg/s |
| Dekaf | 2026-08-02T03:10:27.5709027+00:00 | 3 | 10.0 MiB / 4.8 MiB | 468.2 MB/s | 3/2 | 28,657 | 236.3s / 1,122,636 msg/s |
| Dekaf | 2026-08-02T03:10:46.5808886+00:00 | 1 | 8.0 MiB / 3.4 MiB | 448.6 MB/s | 4/2 | 18,573 | 255.3s / 1,280,782 msg/s |
| Dekaf | 2026-08-02T03:11:04.5898104+00:00 | 1 | 8.0 MiB / 0.6 MiB | 448.6 MB/s | 4/2 | 19,949 | 273.3s / 1,242,523 msg/s |
| Dekaf | 2026-08-02T03:11:22.6031303+00:00 | 2 | 11.0 MiB / 2.4 MiB | 456.8 MB/s | 4/2 | 16,786 | 291.3s / 1,253,326 msg/s |
| Dekaf | 2026-08-02T03:11:40.6145255+00:00 | 2 | 11.0 MiB / 4.7 MiB | 456.8 MB/s | 4/2 | 17,838 | 309.3s / 1,268,619 msg/s |
| Dekaf | 2026-08-02T03:11:58.6330296+00:00 | 3 | 8.0 MiB / 7.7 MiB | 468.2 MB/s | 4/2 | 38,355 | 327.3s / 1,199,927 msg/s |
| Dekaf | 2026-08-02T03:12:16.6429625+00:00 | 3 | 8.0 MiB / 7.9 MiB | 468.2 MB/s | 4/2 | 42,753 | 345.3s / 1,202,000 msg/s |
| Dekaf | 2026-08-02T03:12:35.6622996+00:00 | 1 | 10.0 MiB / 1.5 MiB | 448.6 MB/s | 6/2 | 27,405 | 364.3s / 1,228,404 msg/s |
| Dekaf | 2026-08-02T03:12:53.6832473+00:00 | 1 | 8.0 MiB / 7.5 MiB | 461.7 MB/s | 6/2 | 29,087 | 382.3s / 1,240,674 msg/s |
| Dekaf | 2026-08-02T03:13:11.697592+00:00 | 2 | 10.0 MiB / 3.9 MiB | 456.8 MB/s | 6/2 | 24,478 | 400.3s / 1,234,221 msg/s |
| Dekaf | 2026-08-02T03:13:29.7054764+00:00 | 2 | 8.0 MiB / 2.8 MiB | 456.8 MB/s | 7/2 | 26,802 | 418.4s / 1,264,961 msg/s |
| Dekaf | 2026-08-02T03:13:47.7158457+00:00 | 3 | 8.0 MiB / 4.2 MiB | 479.7 MB/s | 4/4 | 62,016 | 436.4s / 1,211,858 msg/s |
| Dekaf | 2026-08-02T03:14:05.7277645+00:00 | 3 | 8.0 MiB / 5.1 MiB | 479.7 MB/s | 4/4 | 65,951 | 454.4s / 1,248,786 msg/s |
| Dekaf | 2026-08-02T03:14:24.7338708+00:00 | 1 | 8.0 MiB / 1.4 MiB | 461.7 MB/s | 7/3 | 38,774 | 473.4s / 1,269,310 msg/s |
| Dekaf | 2026-08-02T03:14:42.7525924+00:00 | 1 | 9.0 MiB / 6.4 MiB | 461.7 MB/s | 7/3 | 40,645 | 491.4s / 1,258,461 msg/s |
| Dekaf | 2026-08-02T03:15:00.7615716+00:00 | 2 | 8.0 MiB / 4.6 MiB | 463.8 MB/s | 7/3 | 39,795 | 509.4s / 1,283,802 msg/s |
| Dekaf | 2026-08-02T03:15:18.7673606+00:00 | 2 | 9.0 MiB / 2.8 MiB | 467.8 MB/s | 7/3 | 41,873 | 527.4s / 1,198,245 msg/s |
| Dekaf | 2026-08-02T03:15:36.7786108+00:00 | 3 | 9.0 MiB / 8.7 MiB | 482.4 MB/s | 5/4 | 84,923 | 545.5s / 1,196,784 msg/s |
| Dekaf | 2026-08-02T03:15:54.7930944+00:00 | 3 | 9.0 MiB / 5.7 MiB | 482.4 MB/s | 5/4 | 87,808 | 563.5s / 1,195,538 msg/s |
| Dekaf | 2026-08-02T03:16:13.8038619+00:00 | 1 | 9.0 MiB / 2.7 MiB | 461.7 MB/s | 8/4 | 52,550 | 582.5s / 1,208,639 msg/s |
| Dekaf | 2026-08-02T03:16:31.8208341+00:00 | 1 | 9.0 MiB / 3.1 MiB | 461.7 MB/s | 8/4 | 54,831 | 600.5s / 1,219,984 msg/s |
| Dekaf | 2026-08-02T03:16:49.8411224+00:00 | 2 | 10.0 MiB / 10.0 MiB | 467.8 MB/s | 8/4 | 54,189 | 618.5s / 1,186,639 msg/s |
| Dekaf | 2026-08-02T03:17:07.849251+00:00 | 2 | 10.0 MiB / 6.6 MiB | 467.8 MB/s | 9/4 | 56,377 | 636.5s / 1,286,817 msg/s |
| Dekaf | 2026-08-02T03:17:25.8605817+00:00 | 3 | 9.0 MiB / 9.0 MiB | 485.7 MB/s | 5/6 | 104,924 | 654.5s / 1,237,411 msg/s |
| Dekaf | 2026-08-02T03:17:43.8666883+00:00 | 3 | 9.0 MiB / 7.4 MiB | 485.7 MB/s | 5/6 | 108,796 | 672.5s / 1,220,643 msg/s |
| Dekaf | 2026-08-02T03:18:02.8858276+00:00 | 1 | 9.0 MiB / 0.4 MiB | 461.7 MB/s | 8/6 | 67,379 | 691.6s / 1,202,939 msg/s |
| Dekaf | 2026-08-02T03:18:20.8937334+00:00 | 1 | 9.0 MiB / 0.2 MiB | 461.7 MB/s | 8/6 | 69,962 | 709.6s / 1,226,089 msg/s |
| Dekaf | 2026-08-02T03:18:38.9002277+00:00 | 2 | 10.0 MiB / 1.5 MiB | 467.8 MB/s | 9/6 | 67,285 | 727.6s / 1,244,123 msg/s |
| Dekaf | 2026-08-02T03:18:56.9093894+00:00 | 2 | 10.0 MiB / 4.6 MiB | 467.8 MB/s | 9/6 | 69,288 | 745.6s / 1,204,956 msg/s |
| Dekaf | 2026-08-02T03:19:14.9235318+00:00 | 3 | 10.0 MiB / 2.8 MiB | 485.7 MB/s | 6/6 | 124,879 | 763.6s / 1,201,272 msg/s |
| Dekaf | 2026-08-02T03:19:32.9324738+00:00 | 3 | 10.0 MiB / 4.5 MiB | 485.7 MB/s | 6/6 | 127,238 | 781.6s / 1,196,550 msg/s |
| Dekaf | 2026-08-02T03:19:51.9412912+00:00 | 1 | 9.0 MiB / 4.6 MiB | 461.7 MB/s | 8/6 | 81,898 | 800.7s / 1,254,562 msg/s |
| Dekaf | 2026-08-02T03:20:09.9476855+00:00 | 1 | 9.0 MiB / 5.4 MiB | 461.7 MB/s | 8/6 | 83,613 | 818.7s / 1,291,907 msg/s |
| Dekaf | 2026-08-02T03:20:27.9606099+00:00 | 2 | 10.0 MiB / 3.2 MiB | 472.8 MB/s | 9/6 | 78,194 | 836.7s / 1,258,890 msg/s |
| Dekaf | 2026-08-02T03:20:45.9744098+00:00 | 2 | 11.0 MiB / 8.6 MiB | 472.8 MB/s | 10/6 | 79,187 | 854.7s / 1,268,811 msg/s |
| Dekaf | 2026-08-02T03:21:03.9850621+00:00 | 3 | 10.0 MiB / 7.1 MiB | 485.7 MB/s | 6/7 | 141,881 | 872.7s / 1,229,208 msg/s |
| Dekaf | 2026-08-02T03:21:21.9947458+00:00 | 3 | 10.0 MiB / 4.0 MiB | 485.7 MB/s | 6/8 | 144,299 | 890.7s / 1,167,786 msg/s |
| Dekaf (3conn) | 2026-08-02T03:36:53.8540904+00:00 | 3 | 16.0 MiB / 4.6 MiB | 443.0 MB/s | 0/0 | 753 | 9.0s / 1,250,043 msg/s |
| Dekaf (3conn) | 2026-08-02T03:37:11.8858284+00:00 | 3 | 16.0 MiB / 7.3 MiB | 530.4 MB/s | 0/0 | 1,930 | 27.0s / 1,142,700 msg/s |
| Dekaf (3conn) | 2026-08-02T03:37:30.9081159+00:00 | 1 | 16.0 MiB / 2.1 MiB | 521.1 MB/s | 0/1 | 3,568 | 46.0s / 1,200,007 msg/s |
| Dekaf (3conn) | 2026-08-02T03:37:48.918132+00:00 | 1 | 16.0 MiB / 3.9 MiB | 521.1 MB/s | 0/1 | 4,634 | 64.1s / 1,262,854 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:06.9264621+00:00 | 2 | 16.0 MiB / 11.7 MiB | 555.2 MB/s | 0/1 | 5,644 | 82.1s / 1,256,099 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:24.9512047+00:00 | 2 | 16.0 MiB / 1.4 MiB | 555.2 MB/s | 0/2 | 7,242 | 100.1s / 1,152,936 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:42.9781473+00:00 | 3 | 16.0 MiB / 13.5 MiB | 530.4 MB/s | 0/2 | 6,868 | 118.1s / 1,344,598 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:00.9855848+00:00 | 3 | 14.0 MiB / 8.2 MiB | 530.4 MB/s | 1/2 | 8,068 | 136.1s / 1,271,252 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:20.0050794+00:00 | 1 | 16.0 MiB / 13.1 MiB | 521.1 MB/s | 0/2 | 9,384 | 155.2s / 1,314,120 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:38.0113121+00:00 | 1 | 16.0 MiB / 14.4 MiB | 521.7 MB/s | 0/2 | 10,378 | 173.2s / 1,258,141 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:56.0241274+00:00 | 2 | 16.0 MiB / 1.6 MiB | 555.2 MB/s | 0/2 | 12,010 | 191.2s / 1,202,747 msg/s |
| Dekaf (3conn) | 2026-08-02T03:40:14.0395653+00:00 | 2 | 16.0 MiB / 4.6 MiB | 555.2 MB/s | 0/2 | 12,692 | 209.2s / 1,140,037 msg/s |
| Dekaf (3conn) | 2026-08-02T03:40:32.0515572+00:00 | 3 | 12.0 MiB / 5.3 MiB | 530.4 MB/s | 2/3 | 17,945 | 227.2s / 1,244,230 msg/s |
| Dekaf (3conn) | 2026-08-02T03:40:50.068287+00:00 | 3 | 12.0 MiB / 2.2 MiB | 530.4 MB/s | 2/3 | 20,171 | 245.2s / 1,293,563 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:09.0902516+00:00 | 1 | 16.0 MiB / 4.8 MiB | 527.0 MB/s | 0/3 | 13,126 | 264.2s / 1,263,608 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:27.1043075+00:00 | 1 | 16.0 MiB / 1.7 MiB | 527.0 MB/s | 0/3 | 14,371 | 282.3s / 1,318,691 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:45.1112993+00:00 | 2 | 16.0 MiB / 8.2 MiB | 555.2 MB/s | 0/3 | 15,830 | 300.3s / 1,396,050 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:03.1206301+00:00 | 2 | 16.0 MiB / 6.4 MiB | 555.2 MB/s | 0/3 | 16,446 | 318.3s / 1,350,665 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:21.125296+00:00 | 3 | 10.0 MiB / 2.4 MiB | 530.4 MB/s | 3/3 | 32,229 | 336.3s / 1,184,735 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:39.1452463+00:00 | 3 | 10.0 MiB / 8.7 MiB | 530.4 MB/s | 3/4 | 35,685 | 354.3s / 1,245,870 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:58.152823+00:00 | 1 | 16.0 MiB / 0.5 MiB | 527.0 MB/s | 0/3 | 17,980 | 373.3s / 1,300,583 msg/s |
| Dekaf (3conn) | 2026-08-02T03:43:16.1814417+00:00 | 1 | 16.0 MiB / 9.9 MiB | 527.0 MB/s | 0/3 | 18,656 | 391.4s / 1,214,708 msg/s |
| Dekaf (3conn) | 2026-08-02T03:43:34.2066591+00:00 | 2 | 16.0 MiB / 5.9 MiB | 555.2 MB/s | 0/3 | 19,594 | 409.4s / 1,333,664 msg/s |
| Dekaf (3conn) | 2026-08-02T03:43:52.2195647+00:00 | 2 | 16.0 MiB / 2.6 MiB | 555.2 MB/s | 0/3 | 20,689 | 427.4s / 1,229,156 msg/s |
| Dekaf (3conn) | 2026-08-02T03:44:10.2353971+00:00 | 3 | 11.0 MiB / 11.0 MiB | 530.4 MB/s | 4/4 | 51,491 | 445.4s / 1,261,716 msg/s |
| Dekaf (3conn) | 2026-08-02T03:44:28.2446742+00:00 | 3 | 11.0 MiB / 7.1 MiB | 530.4 MB/s | 4/4 | 53,923 | 463.5s / 1,360,720 msg/s |
| Dekaf (3conn) | 2026-08-02T03:44:47.2601033+00:00 | 1 | 16.0 MiB / 7.6 MiB | 537.5 MB/s | 0/3 | 21,753 | 482.5s / 1,206,206 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:05.2762041+00:00 | 1 | 18.0 MiB / 3.1 MiB | 537.5 MB/s | 0/3 | 22,453 | 500.5s / 1,197,899 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:23.2953613+00:00 | 2 | 14.0 MiB / 3.2 MiB | 556.9 MB/s | 1/3 | 25,345 | 518.5s / 1,303,249 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:41.3144307+00:00 | 2 | 12.0 MiB / 4.2 MiB | 556.9 MB/s | 2/3 | 27,005 | 536.5s / 1,256,690 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:59.3273192+00:00 | 3 | 12.0 MiB / 3.1 MiB | 530.4 MB/s | 5/5 | 63,035 | 554.5s / 1,400,303 msg/s |
| Dekaf (3conn) | 2026-08-02T03:46:17.3342444+00:00 | 3 | 12.0 MiB / 4.4 MiB | 530.4 MB/s | 5/5 | 65,160 | 572.5s / 1,238,483 msg/s |
| Dekaf (3conn) | 2026-08-02T03:46:36.3498862+00:00 | 1 | 16.0 MiB / 3.3 MiB | 537.5 MB/s | 0/4 | 24,964 | 591.6s / 1,264,510 msg/s |
| Dekaf (3conn) | 2026-08-02T03:46:54.3591874+00:00 | 1 | 16.0 MiB / 1.8 MiB | 537.5 MB/s | 0/4 | 25,262 | 609.6s / 1,264,689 msg/s |
| Dekaf (3conn) | 2026-08-02T03:47:12.3686799+00:00 | 2 | 8.0 MiB / 6.9 MiB | 556.9 MB/s | 4/3 | 42,091 | 627.6s / 1,257,619 msg/s |
| Dekaf (3conn) | 2026-08-02T03:47:30.3856668+00:00 | 2 | 8.0 MiB / 6.3 MiB | 556.9 MB/s | 4/4 | 46,579 | 645.6s / 1,239,586 msg/s |
| Dekaf (3conn) | 2026-08-02T03:47:48.3988745+00:00 | 3 | 12.0 MiB / 1.5 MiB | 530.4 MB/s | 5/6 | 71,285 | 663.6s / 1,294,856 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:06.4157852+00:00 | 3 | 12.0 MiB / 1.4 MiB | 530.4 MB/s | 5/6 | 72,227 | 681.6s / 1,328,169 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:25.4285588+00:00 | 1 | 16.0 MiB / 5.9 MiB | 537.5 MB/s | 0/4 | 27,282 | 700.6s / 1,272,496 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:43.4360611+00:00 | 1 | 16.0 MiB / 5.9 MiB | 537.5 MB/s | 0/4 | 27,728 | 718.6s / 1,205,546 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:01.4476118+00:00 | 2 | 8.0 MiB / 2.2 MiB | 556.9 MB/s | 4/5 | 68,374 | 736.6s / 1,220,195 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:19.4586308+00:00 | 2 | 8.0 MiB / 1.9 MiB | 556.9 MB/s | 4/5 | 71,721 | 754.7s / 1,296,207 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:37.4675563+00:00 | 3 | 12.0 MiB / 9.6 MiB | 530.4 MB/s | 5/7 | 78,112 | 772.7s / 1,201,242 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:55.4831378+00:00 | 3 | 12.0 MiB / 5.1 MiB | 533.6 MB/s | 5/7 | 79,373 | 790.7s / 1,202,655 msg/s |
| Dekaf (3conn) | 2026-08-02T03:50:14.4997051+00:00 | 1 | 16.0 MiB / 6.9 MiB | 537.5 MB/s | 0/5 | 29,013 | 809.7s / 1,269,524 msg/s |
| Dekaf (3conn) | 2026-08-02T03:50:32.5076239+00:00 | 1 | 16.0 MiB / 9.4 MiB | 537.5 MB/s | 0/5 | 29,229 | 827.7s / 1,185,773 msg/s |
| Dekaf (3conn) | 2026-08-02T03:50:50.5156928+00:00 | 2 | 9.0 MiB / 9.0 MiB | 556.9 MB/s | 4/5 | 85,583 | 845.7s / 1,252,459 msg/s |
| Dekaf (3conn) | 2026-08-02T03:51:08.5278693+00:00 | 2 | 8.0 MiB / 1.7 MiB | 556.9 MB/s | 4/6 | 88,253 | 863.8s / 1,223,048 msg/s |
| Dekaf (3conn) | 2026-08-02T03:51:26.5357436+00:00 | 3 | 12.0 MiB / 2.1 MiB | 533.6 MB/s | 5/7 | 83,241 | 881.8s / 1,174,601 msg/s |
| Dekaf (3conn) | 2026-08-02T03:51:44.548577+00:00 | 3 | 12.0 MiB / 0.7 MiB | 533.6 MB/s | 5/7 | 84,017 | 899.8s / 1,281,444 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-02T03:07:01.7630617+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.4 MiB |
| Dekaf | 2026-08-02T03:07:01.7905367+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:07:16.865499+00:00 | 2 | capacity | succeeded | 15,102ms | 14.0 MiB / 1.1 MiB |
| Dekaf | 2026-08-02T03:07:16.8763056+00:00 | 1 | capacity | succeeded | 15,082ms | 14.0 MiB / 1.2 MiB |
| Dekaf | 2026-08-02T03:07:19.8696678+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 0.7 MiB |
| Dekaf | 2026-08-02T03:07:19.8886712+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.3 MiB |
| Dekaf | 2026-08-02T03:07:34.9575822+00:00 | 2 | capacity | succeeded | 15,087ms | 12.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-02T03:07:34.9597058+00:00 | 3 | capacity | succeeded | 15,054ms | 12.0 MiB / 2.4 MiB |
| Dekaf | 2026-08-02T03:07:37.9760098+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 1.0 MiB |
| Dekaf | 2026-08-02T03:07:37.997378+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:07:53.0407203+00:00 | 3 | capacity | succeeded | 15,064ms | 10.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-02T03:08:05.1282651+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 0.6 MiB |
| Dekaf | 2026-08-02T03:08:20.2177396+00:00 | 2 | capacity | failed | 15,088ms | 12.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-02T03:08:23.2312345+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.8 MiB |
| Dekaf | 2026-08-02T03:08:38.2734676+00:00 | 3 | capacity | failed | 15,068ms | 10.0 MiB / 2.8 MiB |
| Dekaf | 2026-08-02T03:09:08.3890041+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 1.6 MiB |
| Dekaf | 2026-08-02T03:09:20.4608666+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 6.3 MiB |
| Dekaf | 2026-08-02T03:09:35.5168051+00:00 | 2 | capacity | succeeded | 15,055ms | 10.0 MiB / 1.7 MiB |
| Dekaf | 2026-08-02T03:09:38.5645535+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:09:53.6959483+00:00 | 1 | capacity | failed | 15,131ms | 10.0 MiB / 8.1 MiB |
| Dekaf | 2026-08-02T03:10:20.7340829+00:00 | 2 | capacity | failed | 15,094ms | 10.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-02T03:10:23.8386649+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-02T03:10:50.8476081+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 0.6 MiB |
| Dekaf | 2026-08-02T03:11:05.899661+00:00 | 2 | capacity | succeeded | 15,052ms | 11.0 MiB / 0.6 MiB |
| Dekaf | 2026-08-02T03:11:24.006996+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-02T03:11:24.1022305+00:00 | 1 | capacity | succeeded | 15,065ms | 9.0 MiB / 5.9 MiB |
| Dekaf | 2026-08-02T03:11:39.0621845+00:00 | 3 | capacity | succeeded | 15,055ms | 8.0 MiB / 2.0 MiB |
| Dekaf | 2026-08-02T03:11:51.1071476+00:00 | 2 | capacity | succeeded | 15,066ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-02T03:11:54.2678615+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 0.8 MiB |
| Dekaf | 2026-08-02T03:12:09.3065513+00:00 | 1 | capacity | succeeded | 15,038ms | 10.0 MiB / 1.7 MiB |
| Dekaf | 2026-08-02T03:12:21.2620789+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:12:36.3236849+00:00 | 2 | capacity | succeeded | 15,061ms | 10.0 MiB / 0.9 MiB |
| Dekaf | 2026-08-02T03:12:39.4408008+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.1 MiB |
| Dekaf | 2026-08-02T03:12:54.5185713+00:00 | 1 | capacity | succeeded | 15,077ms | 8.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-02T03:13:06.457174+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-08-02T03:13:21.5237003+00:00 | 2 | capacity | succeeded | 15,066ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:13:24.6617885+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.0 MiB |
| Dekaf | 2026-08-02T03:13:39.7399016+00:00 | 1 | capacity | failed | 15,078ms | 8.0 MiB / 2.1 MiB |
| Dekaf | 2026-08-02T03:14:06.7407381+00:00 | 2 | capacity | failed | 15,075ms | 8.0 MiB / 1.2 MiB |
| Dekaf | 2026-08-02T03:14:39.9948808+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.6 MiB |
| Dekaf | 2026-08-02T03:15:06.993841+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.0 MiB |
| Dekaf | 2026-08-02T03:15:10.0061102+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 4.8 MiB |
| Dekaf | 2026-08-02T03:15:25.0625303+00:00 | 3 | capacity | succeeded | 15,056ms | 9.0 MiB / 2.1 MiB |
| Dekaf | 2026-08-02T03:15:25.1946043+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.4 MiB |
| Dekaf | 2026-08-02T03:15:52.2067809+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-08-02T03:15:55.2682738+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.7 MiB |
| Dekaf | 2026-08-02T03:16:07.2786454+00:00 | 2 | capacity | failed | 15,071ms | 9.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:16:37.3994671+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:16:40.4271319+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.4 MiB |
| Dekaf | 2026-08-02T03:16:52.4752639+00:00 | 2 | capacity | succeeded | 15,075ms | 10.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:16:55.5087229+00:00 | 3 | capacity | failed | 15,081ms | 9.0 MiB / 1.2 MiB |
| Dekaf | 2026-08-02T03:17:22.5908134+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-02T03:17:25.7683595+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:17:40.8290607+00:00 | 1 | capacity | failed | 15,060ms | 9.0 MiB / 1.2 MiB |
| Dekaf | 2026-08-02T03:18:07.7950455+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.2 MiB |
| Dekaf | 2026-08-02T03:18:22.84839+00:00 | 2 | capacity | failed | 15,053ms | 10.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-02T03:19:11.1403396+00:00 | 3 | capacity | succeeded | 15,073ms | 10.0 MiB / 8.3 MiB |
| Dekaf | 2026-08-02T03:19:41.2898818+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-02T03:20:23.3835649+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 4.0 MiB |
| Dekaf | 2026-08-02T03:20:38.462849+00:00 | 2 | capacity | succeeded | 15,079ms | 11.0 MiB / 1.5 MiB |
| Dekaf | 2026-08-02T03:21:08.5691564+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:21:11.7436987+00:00 | 3 | capacity | failed | 15,049ms | 10.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:15.0537735+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:15.113175+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:15.1229695+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:30.202772+00:00 | 2 | capacity | failed | 15,089ms | 16.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:30.2454528+00:00 | 3 | capacity | failed | 15,122ms | 16.0 MiB / 5.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:00.4154848+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 1.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:15.4031406+00:00 | 2 | capacity | failed | 15,049ms | 16.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:30.4899822+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 0.0 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:45.618556+00:00 | 1 | capacity | failed | 15,128ms | 16.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:00.7924168+00:00 | 3 | capacity | succeeded | 15,071ms | 14.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:31.021558+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:46.0873442+00:00 | 3 | capacity | succeeded | 15,066ms | 12.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:40:16.2866041+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:40:31.234975+00:00 | 2 | capacity | failed | 15,070ms | 16.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:40:46.2578255+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:01.348692+00:00 | 1 | capacity | failed | 15,090ms | 16.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:46.788367+00:00 | 3 | capacity | succeeded | 15,063ms | 10.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:42:16.9036345+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:43:32.2830765+00:00 | 3 | capacity | started | 0ms | 11.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:43:47.3626285+00:00 | 3 | capacity | succeeded | 15,079ms | 11.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:44:17.5695239+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 3.0 MiB |
| Dekaf (3conn) | 2026-08-02T03:44:32.7250482+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:44:47.7942466+00:00 | 2 | capacity | succeeded | 15,069ms | 14.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:02.81816+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:17.7964132+00:00 | 1 | capacity | failed | 15,107ms | 16.0 MiB / 12.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:17.9713374+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 10.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:33.0931788+00:00 | 2 | capacity | succeeded | 15,121ms | 12.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:46:18.2711081+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 0.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:46:18.3996752+00:00 | 2 | capacity | succeeded | 15,117ms | 10.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:46:21.415057+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:46:36.511093+00:00 | 2 | capacity | succeeded | 15,096ms | 8.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:47:06.6806097+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-08-02T03:48:22.1069377+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:48:34.1505691+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:48:49.2200252+00:00 | 3 | capacity | failed | 15,069ms | 12.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:49:19.2389757+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:50:37.8024301+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-08-02T03:50:52.8514351+00:00 | 2 | capacity | failed | 15,049ms | 8.0 MiB / 0.9 MiB |
*44 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 3 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 3 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 5 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 22 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 83 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 105 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 116 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 169 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 218 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 383 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 510 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 581 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 509 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 287 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 125 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 19 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 12 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 5 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 30 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 85 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 281 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 509 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 575 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 669 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 965 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 1,506 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 1,968 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 2,176 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,485 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 616 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 188 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 34 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 5 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 2 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 14 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 66 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 202 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 394 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 460 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 521 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 739 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 1,184 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 1,562 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 1,663 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 1,321 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 594 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 298 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 40 |
| Dekaf | 1 | 0.001–0.002ms | 8 |
| Dekaf | 1 | 0.002–0.004ms | 8 |
| Dekaf | 1 | 0.004–0.008ms | 35 |
| Dekaf | 1 | 0.008–0.016ms | 107 |
| Dekaf | 1 | 0.016–0.032ms | 327 |
| Dekaf | 1 | 0.032–0.064ms | 649 |
| Dekaf | 1 | 0.064–0.128ms | 515 |
| Dekaf | 1 | 0.128–0.256ms | 707 |
| Dekaf | 1 | 0.256–0.512ms | 1,146 |
| Dekaf | 1 | 0.512–1.024ms | 1,970 |
| Dekaf | 1 | 1.024–2.048ms | 2,506 |
| Dekaf | 1 | 2.048–4.096ms | 2,547 |
| Dekaf | 1 | 4.096–8.192ms | 1,511 |
| Dekaf | 1 | 8.192–16.384ms | 570 |
| Dekaf | 1 | 16.384–32.768ms | 91 |
| Dekaf | 1 | 32.768–65.536ms | 7 |
| Dekaf | 2 | 0.001–0.002ms | 6 |
| Dekaf | 2 | 0.002–0.004ms | 12 |
| Dekaf | 2 | 0.004–0.008ms | 35 |
| Dekaf | 2 | 0.008–0.016ms | 97 |
| Dekaf | 2 | 0.016–0.032ms | 277 |
| Dekaf | 2 | 0.032–0.064ms | 533 |
| Dekaf | 2 | 0.064–0.128ms | 418 |
| Dekaf | 2 | 0.128–0.256ms | 643 |
| Dekaf | 2 | 0.256–0.512ms | 1,142 |
| Dekaf | 2 | 0.512–1.024ms | 1,780 |
| Dekaf | 2 | 1.024–2.048ms | 2,381 |
| Dekaf | 2 | 2.048–4.096ms | 2,270 |
| Dekaf | 2 | 4.096–8.192ms | 1,408 |
| Dekaf | 2 | 8.192–16.384ms | 428 |
| Dekaf | 2 | 16.384–32.768ms | 87 |
| Dekaf | 2 | 32.768–65.536ms | 8 |
| Dekaf | 2 | 65.536–131.072ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 10 |
| Dekaf | 3 | 0.002–0.004ms | 16 |
| Dekaf | 3 | 0.004–0.008ms | 46 |
| Dekaf | 3 | 0.008–0.016ms | 185 |
| Dekaf | 3 | 0.016–0.032ms | 520 |
| Dekaf | 3 | 0.032–0.064ms | 906 |
| Dekaf | 3 | 0.064–0.128ms | 859 |
| Dekaf | 3 | 0.128–0.256ms | 1,094 |
| Dekaf | 3 | 0.256–0.512ms | 1,938 |
| Dekaf | 3 | 0.512–1.024ms | 3,139 |
| Dekaf | 3 | 1.024–2.048ms | 4,055 |
| Dekaf | 3 | 2.048–4.096ms | 3,952 |
| Dekaf | 3 | 4.096–8.192ms | 2,350 |
| Dekaf | 3 | 8.192–16.384ms | 941 |
| Dekaf | 3 | 16.384–32.768ms | 211 |
| Dekaf | 3 | 32.768–65.536ms | 9 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 101,000 | 2026-08-02T03:06:31.6735604+00:00 | 123.5ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 102,000 | 2026-08-02T03:06:31.6744562+00:00 | 122.6ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 105,000 | 2026-08-02T03:06:31.6795059+00:00 | 102.4ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 108,000 | 2026-08-02T03:06:31.6820615+00:00 | 100.0ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 111,000 | 2026-08-02T03:06:31.6855589+00:00 | 149.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 112,000 | 2026-08-02T03:06:31.6870411+00:00 | 149.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 115,000 | 2026-08-02T03:06:31.6897274+00:00 | 146.5ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 118,000 | 2026-08-02T03:06:31.6934194+00:00 | 154.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 121,000 | 2026-08-02T03:06:31.6965188+00:00 | 218.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 122,000 | 2026-08-02T03:06:31.6975706+00:00 | 217.1ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 125,000 | 2026-08-02T03:06:31.7012606+00:00 | 146.6ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 126,000 | 2026-08-02T03:06:31.7021966+00:00 | 105.1ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 128,000 | 2026-08-02T03:06:31.7039002+00:00 | 143.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 131,000 | 2026-08-02T03:06:31.7077316+00:00 | 223.7ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 132,000 | 2026-08-02T03:06:31.7086551+00:00 | 222.8ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 134,000 | 2026-08-02T03:06:31.7105178+00:00 | 103.7ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 135,000 | 2026-08-02T03:06:31.7116778+00:00 | 201.6ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 136,000 | 2026-08-02T03:06:31.7137389+00:00 | 100.5ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 137,000 | 2026-08-02T03:06:31.7147888+00:00 | 100.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 138,000 | 2026-08-02T03:06:31.715848+00:00 | 197.4ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 141,000 | 2026-08-02T03:06:31.7194213+00:00 | 212.0ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 142,000 | 2026-08-02T03:06:31.7211553+00:00 | 221.6ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 145,000 | 2026-08-02T03:06:31.773803+00:00 | 140.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 148,000 | 2026-08-02T03:06:31.7781345+00:00 | 164.7ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 151,000 | 2026-08-02T03:06:31.783101+00:00 | 159.7ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 152,000 | 2026-08-02T03:06:31.7843908+00:00 | 158.4ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 155,000 | 2026-08-02T03:06:31.7997704+00:00 | 145.1ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 158,000 | 2026-08-02T03:06:31.8367035+00:00 | 108.1ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 161,000 | 2026-08-02T03:06:31.8412471+00:00 | 109.8ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 162,000 | 2026-08-02T03:06:31.8421745+00:00 | 108.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 165,000 | 2026-08-02T03:06:31.8453906+00:00 | 112.1ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 168,000 | 2026-08-02T03:06:31.8513164+00:00 | 128.7ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 171,000 | 2026-08-02T03:06:31.8553876+00:00 | 122.7ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 172,000 | 2026-08-02T03:06:31.8575966+00:00 | 123.1ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 254,000 | 2026-08-02T03:06:32.0681788+00:00 | 117.4ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 256,000 | 2026-08-02T03:06:32.070185+00:00 | 161.8ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 257,000 | 2026-08-02T03:06:32.0715388+00:00 | 168.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 264,000 | 2026-08-02T03:06:32.0815542+00:00 | 161.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 266,000 | 2026-08-02T03:06:32.0856566+00:00 | 157.8ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 267,000 | 2026-08-02T03:06:32.0865065+00:00 | 156.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 274,000 | 2026-08-02T03:06:32.0955034+00:00 | 147.9ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 276,000 | 2026-08-02T03:06:32.0981982+00:00 | 147.8ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 277,000 | 2026-08-02T03:06:32.0994414+00:00 | 150.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 284,000 | 2026-08-02T03:06:32.1093187+00:00 | 136.6ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 286,000 | 2026-08-02T03:06:32.1116752+00:00 | 134.3ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 287,000 | 2026-08-02T03:06:32.1130851+00:00 | 140.3ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 294,000 | 2026-08-02T03:06:32.1283093+00:00 | 128.8ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 296,000 | 2026-08-02T03:06:32.1349308+00:00 | 122.2ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 297,000 | 2026-08-02T03:06:32.1366836+00:00 | 120.4ms | GC pause | - | - | 1.0s / 435,704 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 429,000 | 2026-08-02T03:06:32.4331618+00:00 | 141.3ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 430,000 | 2026-08-02T03:06:32.4344259+00:00 | 130.9ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 433,000 | 2026-08-02T03:06:32.4399105+00:00 | 134.6ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 439,000 | 2026-08-02T03:06:32.45332+00:00 | 135.8ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 440,000 | 2026-08-02T03:06:32.4651232+00:00 | 124.2ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 443,000 | 2026-08-02T03:06:32.4716993+00:00 | 117.6ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 447,000 | 2026-08-02T03:06:32.4823086+00:00 | 115.7ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 449,000 | 2026-08-02T03:06:32.4860751+00:00 | 105.2ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 450,000 | 2026-08-02T03:06:32.4875496+00:00 | 108.7ms | GC pause | - | - | 2.0s / 640,351 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf | 453,000 | 2026-08-02T03:06:32.49473+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 454,000 | 2026-08-02T03:06:32.4958715+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 456,000 | 2026-08-02T03:06:32.4984698+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 457,000 | 2026-08-02T03:06:32.4996227+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 459,000 | 2026-08-02T03:06:32.5030201+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 464,000 | 2026-08-02T03:06:32.5128879+00:00 | 128.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 466,000 | 2026-08-02T03:06:32.5221012+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 467,000 | 2026-08-02T03:06:32.523514+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 527,000 | 2026-08-02T03:06:32.7062898+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 780,000 | 2026-08-02T03:06:33.0824851+00:00 | 129.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 640,351 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,571,000 | 2026-08-02T03:06:34.0878001+00:00 | 146.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,572,000 | 2026-08-02T03:06:34.0915155+00:00 | 143.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,575,000 | 2026-08-02T03:06:34.0927948+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,578,000 | 2026-08-02T03:06:34.0941333+00:00 | 125.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,581,000 | 2026-08-02T03:06:34.0983735+00:00 | 142.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,582,000 | 2026-08-02T03:06:34.0986413+00:00 | 142.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,585,000 | 2026-08-02T03:06:34.1000388+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,588,000 | 2026-08-02T03:06:34.1041771+00:00 | 119.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,591,000 | 2026-08-02T03:06:34.1056903+00:00 | 140.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,592,000 | 2026-08-02T03:06:34.105918+00:00 | 140.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,595,000 | 2026-08-02T03:06:34.1102771+00:00 | 131.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,598,000 | 2026-08-02T03:06:34.1118133+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,601,000 | 2026-08-02T03:06:34.1137657+00:00 | 142.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,602,000 | 2026-08-02T03:06:34.1142361+00:00 | 142.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,605,000 | 2026-08-02T03:06:34.1211214+00:00 | 130.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,608,000 | 2026-08-02T03:06:34.1327023+00:00 | 132.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,611,000 | 2026-08-02T03:06:34.1339129+00:00 | 129.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,612,000 | 2026-08-02T03:06:34.1341945+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 826,914 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,916,000 | 2026-08-02T03:06:34.4873529+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,927,000 | 2026-08-02T03:06:34.5054674+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,934,000 | 2026-08-02T03:06:34.5099705+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,936,000 | 2026-08-02T03:06:34.511186+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,937,000 | 2026-08-02T03:06:34.511626+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,954,000 | 2026-08-02T03:06:34.5319392+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,956,000 | 2026-08-02T03:06:34.5416176+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,957,000 | 2026-08-02T03:06:34.5419209+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,510,000 | 2026-08-02T03:06:35.0785352+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,509,000 | 2026-08-02T03:06:35.0791318+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,513,000 | 2026-08-02T03:06:35.080133+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,523,000 | 2026-08-02T03:06:35.0994383+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 917,508 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,380,000 | 2026-08-02T03:06:37.102629+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 996,413 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,419,000 | 2026-08-02T03:06:39.0789109+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,003,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,423,000 | 2026-08-02T03:06:39.0813684+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,003,614 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,820,000 | 2026-08-02T03:06:40.5592266+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 997,654 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,823,000 | 2026-08-02T03:06:40.561031+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 997,654 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,919,000 | 2026-08-02T03:06:42.6341374+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,920,000 | 2026-08-02T03:06:42.634488+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,923,000 | 2026-08-02T03:06:42.6361037+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,929,000 | 2026-08-02T03:06:42.6390501+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,930,000 | 2026-08-02T03:06:42.6395697+00:00 | 157.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,933,000 | 2026-08-02T03:06:42.64112+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,939,000 | 2026-08-02T03:06:42.6437819+00:00 | 156.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,940,000 | 2026-08-02T03:06:42.6562934+00:00 | 144.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,943,000 | 2026-08-02T03:06:42.6641087+00:00 | 137.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,949,000 | 2026-08-02T03:06:42.6812309+00:00 | 124.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,950,000 | 2026-08-02T03:06:42.6815806+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,953,000 | 2026-08-02T03:06:42.6837989+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,959,000 | 2026-08-02T03:06:42.7019427+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,960,000 | 2026-08-02T03:06:42.7025642+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,963,000 | 2026-08-02T03:06:42.7094866+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,237,000 | 2026-08-02T03:06:43.0612881+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,247,000 | 2026-08-02T03:06:43.0687606+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 835,713 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,397,000 | 2026-08-02T03:06:47.1137705+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 16.1s / 1,023,820 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,359,000 | 2026-08-02T03:06:48.0666757+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 17.1s / 956,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,360,000 | 2026-08-02T03:06:48.0673875+00:00 | 105.4ms | broker/backlog (no scale or GC event) | - | - | 17.1s / 956,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,363,000 | 2026-08-02T03:06:48.0688693+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 17.1s / 956,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,369,000 | 2026-08-02T03:06:48.0740232+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 17.1s / 956,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,373,000 | 2026-08-02T03:06:48.0782503+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 17.1s / 956,765 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,740,000 | 2026-08-02T03:06:48.4589821+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 18.1s / 972,500 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,377,000 | 2026-08-02T03:06:51.0157987+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,384,000 | 2026-08-02T03:06:51.0216258+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,386,000 | 2026-08-02T03:06:51.0285167+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,387,000 | 2026-08-02T03:06:51.030384+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,394,000 | 2026-08-02T03:06:51.0343732+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,396,000 | 2026-08-02T03:06:51.0364103+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,397,000 | 2026-08-02T03:06:51.0368019+00:00 | 114.1ms | broker/backlog (no scale or GC event) | - | - | 20.1s / 1,021,197 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 200,000 | 2026-08-02T03:36:45.2473587+00:00 | 100.8ms | throughput collapse | - | - | 1.0s / 565,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 213,000 | 2026-08-02T03:36:45.2634772+00:00 | 111.3ms | throughput collapse | - | - | 1.0s / 565,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 219,000 | 2026-08-02T03:36:45.2742058+00:00 | 108.9ms | throughput collapse | - | - | 1.0s / 565,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 223,000 | 2026-08-02T03:36:45.2789004+00:00 | 104.2ms | throughput collapse | - | - | 1.0s / 565,111 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,121,000 | 2026-08-02T03:36:46.4523481+00:00 | 116.9ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,122,000 | 2026-08-02T03:36:46.4534661+00:00 | 115.8ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,131,000 | 2026-08-02T03:36:46.4632637+00:00 | 108.4ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,132,000 | 2026-08-02T03:36:46.4637188+00:00 | 107.9ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,141,000 | 2026-08-02T03:36:46.4702158+00:00 | 113.5ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,142,000 | 2026-08-02T03:36:46.4710977+00:00 | 112.6ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,151,000 | 2026-08-02T03:36:46.4789347+00:00 | 121.5ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 1,152,000 | 2026-08-02T03:36:46.4793538+00:00 | 121.1ms | GC pause | - | - | 2.0s / 896,089 msg/s | Gen2 +1 / pause +1.2ms |
| Dekaf (3conn) | 2,093,000 | 2026-08-02T03:36:47.4438946+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,144,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,109,000 | 2026-08-02T03:36:47.4518691+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,144,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,123,000 | 2026-08-02T03:36:47.4604517+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,144,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,139,000 | 2026-08-02T03:36:47.4724047+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,144,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,143,000 | 2026-08-02T03:36:47.4744098+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,144,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,149,000 | 2026-08-02T03:36:47.4930177+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,144,369 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,821,000 | 2026-08-02T03:36:48.8973133+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,100,497 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,822,000 | 2026-08-02T03:36:48.89757+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,100,497 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,831,000 | 2026-08-02T03:36:48.9017699+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,100,497 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,832,000 | 2026-08-02T03:36:48.9023412+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 1,100,497 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,123,000 | 2026-08-02T03:36:50.9550026+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,100,126 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,129,000 | 2026-08-02T03:36:50.9591009+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 1,100,126 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,204,000 | 2026-08-02T03:36:51.9533917+00:00 | 168.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,206,000 | 2026-08-02T03:36:51.9546797+00:00 | 185.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,214,000 | 2026-08-02T03:36:51.9582051+00:00 | 188.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,216,000 | 2026-08-02T03:36:51.9594153+00:00 | 187.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,224,000 | 2026-08-02T03:36:51.9637155+00:00 | 205.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,226,000 | 2026-08-02T03:36:51.9645182+00:00 | 204.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,229,000 | 2026-08-02T03:36:51.9656008+00:00 | 126.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,231,000 | 2026-08-02T03:36:51.9732436+00:00 | 130.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,232,000 | 2026-08-02T03:36:51.9756317+00:00 | 128.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,233,000 | 2026-08-02T03:36:51.9761351+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,234,000 | 2026-08-02T03:36:51.9765988+00:00 | 194.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,236,000 | 2026-08-02T03:36:51.9773243+00:00 | 193.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,239,000 | 2026-08-02T03:36:51.978761+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,241,000 | 2026-08-02T03:36:51.9832538+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,242,000 | 2026-08-02T03:36:51.9837007+00:00 | 120.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,244,000 | 2026-08-02T03:36:52.0083304+00:00 | 168.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,246,000 | 2026-08-02T03:36:52.0091831+00:00 | 167.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,251,000 | 2026-08-02T03:36:52.011767+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,252,000 | 2026-08-02T03:36:52.0121305+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,254,000 | 2026-08-02T03:36:52.0132655+00:00 | 165.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,256,000 | 2026-08-02T03:36:52.0142741+00:00 | 164.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,264,000 | 2026-08-02T03:36:52.0637947+00:00 | 121.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,068,966 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,574,000 | 2026-08-02T03:37:04.428227+00:00 | 124.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,576,000 | 2026-08-02T03:37:04.4290577+00:00 | 123.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,584,000 | 2026-08-02T03:37:04.4356131+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,586,000 | 2026-08-02T03:37:04.4379495+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,594,000 | 2026-08-02T03:37:04.4434124+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,596,000 | 2026-08-02T03:37:04.4443043+00:00 | 115.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,604,000 | 2026-08-02T03:37:04.4515358+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,606,000 | 2026-08-02T03:37:04.4526749+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,614,000 | 2026-08-02T03:37:04.4582105+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,616,000 | 2026-08-02T03:37:04.4591117+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,624,000 | 2026-08-02T03:37:04.4624559+00:00 | 107.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,626,000 | 2026-08-02T03:37:04.4632923+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 22,634,000 | 2026-08-02T03:37:04.4721501+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 1,150,073 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 106,763,000 | 2026-08-02T03:38:11.4679284+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 87.1s / 1,290,747 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 106,769,000 | 2026-08-02T03:38:11.4701079+00:00 | 103.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 87.1s / 1,290,747 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 106,773,000 | 2026-08-02T03:38:11.4712925+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 87.1s / 1,290,747 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 106,779,000 | 2026-08-02T03:38:11.473523+00:00 | 104.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed | - | 87.1s / 1,290,747 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 402,890,000 | 2026-08-02T03:42:04.938764+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 320.3s / 1,228,592 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 402,920,000 | 2026-08-02T03:42:04.967617+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 320.3s / 1,228,592 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 519,329,000 | 2026-08-02T03:43:38.9489496+00:00 | 102.6ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 414.4s / 1,172,577 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 519,333,000 | 2026-08-02T03:43:38.9508217+00:00 | 104.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 414.4s / 1,172,577 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 519,339,000 | 2026-08-02T03:43:38.9535198+00:00 | 101.7ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded | - | 414.4s / 1,172,577 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 620,431,000 | 2026-08-02T03:44:58.0261207+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,432,000 | 2026-08-02T03:44:58.0266497+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,441,000 | 2026-08-02T03:44:58.0336138+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,442,000 | 2026-08-02T03:44:58.0340402+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,444,000 | 2026-08-02T03:44:58.034604+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,446,000 | 2026-08-02T03:44:58.0357419+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,452,000 | 2026-08-02T03:44:58.0401711+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,451,000 | 2026-08-02T03:44:58.0407061+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,454,000 | 2026-08-02T03:44:58.0410031+00:00 | 108.6ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,456,000 | 2026-08-02T03:44:58.0414832+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,461,000 | 2026-08-02T03:44:58.0434583+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,462,000 | 2026-08-02T03:44:58.0437313+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,464,000 | 2026-08-02T03:44:58.0446208+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,466,000 | 2026-08-02T03:44:58.0474874+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 620,471,000 | 2026-08-02T03:44:58.0501865+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 493.5s / 1,235,207 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 691,371,000 | 2026-08-02T03:45:53.4657385+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 549.5s / 1,178,479 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 691,372,000 | 2026-08-02T03:45:53.4661133+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 549.5s / 1,178,479 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 699,106,000 | 2026-08-02T03:45:59.4477463+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 555.5s / 1,109,310 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 820,155,000 | 2026-08-02T03:47:36.9658479+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 652.6s / 1,213,509 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 886,914,000 | 2026-08-02T03:48:30.4028434+00:00 | 123.6ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 706.6s / 1,076,694 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 886,916,000 | 2026-08-02T03:48:30.4040378+00:00 | 123.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 706.6s / 1,076,694 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 886,924,000 | 2026-08-02T03:48:30.4104073+00:00 | 118.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 706.6s / 1,076,694 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 886,926,000 | 2026-08-02T03:48:30.4109798+00:00 | 118.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 706.6s / 1,076,694 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.63x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.34x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,552,416 | 1,520,213–1,585,302 | 0.92 | 1.05x |
| Confluent | 2 | 1,485,309 | 1,479,092–1,491,552 | 1.22 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.88 | 895.49 | 1,558,255 | 1,585,302 | -2.7% | -0.33% | 1486.07 | 1,558,255 | 0 | 1.37 |
| Dekaf (confluent-first) | 0.95 | 972.30 | 1,510,663 | 1,520,213 | -3.1% | -0.31% | 1440.68 | 1,510,663 | 0 | 1.43 |
| Confluent (confluent-first) | 1.23 | - | 1,476,771 | 1,491,552 | +0.5% | +0.00% | 1408.36 | 1,476,771 | 0 | 1.81 |
| Confluent (dekaf-first) | 1.22 | - | 1,457,791 | 1,479,092 | -2.0% | -0.16% | 1390.26 | 1,457,791 | 0 | 1.78 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,328,225 | 1475.78 | 1017.55 KB |
| Dekaf | 1 | 1,380,027 | 1533.34 | 1010.20 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:20.6947868+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 821,586 msg/s |
| Dekaf | 2026-08-02T03:06:38.6988388+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1671.7 MB/s | 0/0 | 22,140 | 18.0s / 1,601,296 msg/s |
| Dekaf | 2026-08-02T03:06:56.7071134+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1727.8 MB/s | 0/0 | 50,714 | 36.0s / 1,635,145 msg/s |
| Dekaf | 2026-08-02T03:07:15.7123818+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1732.2 MB/s | 1/0 | 90,825 | 55.0s / 1,586,071 msg/s |
| Dekaf | 2026-08-02T03:07:33.7203789+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1737.1 MB/s | 1/0 | 132,035 | 73.0s / 1,547,926 msg/s |
| Dekaf | 2026-08-02T03:07:51.7272293+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1744.4 MB/s | 1/1 | 174,272 | 91.0s / 1,637,070 msg/s |
| Dekaf | 2026-08-02T03:08:09.7310387+00:00 | 1 | 14.0 MiB / 4.1 MiB | 1744.4 MB/s | 1/1 | 213,171 | 109.0s / 1,389,813 msg/s |
| Dekaf | 2026-08-02T03:08:27.7340017+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1744.4 MB/s | 1/1 | 255,793 | 127.0s / 1,611,015 msg/s |
| Dekaf | 2026-08-02T03:08:45.7400669+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1744.4 MB/s | 1/1 | 299,784 | 145.1s / 1,565,650 msg/s |
| Dekaf | 2026-08-02T03:09:04.7448125+00:00 | 1 | 15.0 MiB / 14.6 MiB | 1744.4 MB/s | 1/1 | 341,173 | 164.1s / 1,624,376 msg/s |
| Dekaf | 2026-08-02T03:09:22.7589024+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1744.4 MB/s | 1/2 | 385,372 | 182.1s / 1,633,652 msg/s |
| Dekaf | 2026-08-02T03:09:40.766535+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1744.4 MB/s | 1/2 | 422,985 | 200.1s / 1,535,369 msg/s |
| Dekaf | 2026-08-02T03:09:58.7670062+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1744.4 MB/s | 1/2 | 461,921 | 218.1s / 1,555,046 msg/s |
| Dekaf | 2026-08-02T03:10:16.7740481+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1744.4 MB/s | 1/2 | 503,091 | 236.1s / 1,612,632 msg/s |
| Dekaf | 2026-08-02T03:10:34.7761082+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1758.6 MB/s | 1/2 | 543,728 | 254.1s / 1,602,428 msg/s |
| Dekaf | 2026-08-02T03:10:53.7821186+00:00 | 1 | 14.0 MiB / 12.2 MiB | 1769.0 MB/s | 1/2 | 592,347 | 273.1s / 1,604,234 msg/s |
| Dekaf | 2026-08-02T03:11:11.7899138+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1770.3 MB/s | 1/2 | 638,869 | 291.1s / 1,499,690 msg/s |
| Dekaf | 2026-08-02T03:11:29.7945128+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.3 MB/s | 2/2 | 689,004 | 309.1s / 1,641,703 msg/s |
| Dekaf | 2026-08-02T03:11:47.7993196+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1770.3 MB/s | 2/2 | 737,097 | 327.1s / 1,570,217 msg/s |
| Dekaf | 2026-08-02T03:12:05.8043087+00:00 | 1 | 10.0 MiB / 1.1 MiB | 1770.3 MB/s | 2/2 | 781,504 | 345.1s / 1,224,765 msg/s |
| Dekaf | 2026-08-02T03:12:23.8108273+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.3 MB/s | 2/3 | 831,674 | 363.1s / 1,615,322 msg/s |
| Dekaf | 2026-08-02T03:12:42.8102568+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.3 MB/s | 2/3 | 887,068 | 382.1s / 1,635,044 msg/s |
| Dekaf | 2026-08-02T03:13:00.8180087+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.3 MB/s | 2/3 | 940,774 | 400.1s / 1,620,062 msg/s |
| Dekaf | 2026-08-02T03:13:18.8251807+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1770.3 MB/s | 2/3 | 991,826 | 418.1s / 1,645,462 msg/s |
| Dekaf | 2026-08-02T03:13:36.8315286+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1770.3 MB/s | 3/3 | 1,040,213 | 436.1s / 1,571,328 msg/s |
| Dekaf | 2026-08-02T03:13:54.8388356+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1770.3 MB/s | 3/3 | 1,087,591 | 454.1s / 1,606,957 msg/s |
| Dekaf | 2026-08-02T03:14:12.8422294+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1770.3 MB/s | 3/4 | 1,138,400 | 472.1s / 1,598,074 msg/s |
| Dekaf | 2026-08-02T03:14:31.8453326+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1770.3 MB/s | 3/4 | 1,188,903 | 491.1s / 1,593,951 msg/s |
| Dekaf | 2026-08-02T03:14:49.8503899+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1770.3 MB/s | 3/4 | 1,236,650 | 509.1s / 1,604,128 msg/s |
| Dekaf | 2026-08-02T03:15:07.8530815+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1770.3 MB/s | 3/4 | 1,280,068 | 527.1s / 1,562,280 msg/s |
| Dekaf | 2026-08-02T03:15:25.8566672+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1770.3 MB/s | 4/4 | 1,324,947 | 545.2s / 1,523,387 msg/s |
| Dekaf | 2026-08-02T03:15:43.8609923+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/4 | 1,372,117 | 563.2s / 1,459,946 msg/s |
| Dekaf | 2026-08-02T03:16:01.8640108+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1770.3 MB/s | 4/4 | 1,400,239 | 581.2s / 1,516,009 msg/s |
| Dekaf | 2026-08-02T03:16:20.8705933+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1770.3 MB/s | 4/5 | 1,441,949 | 600.2s / 1,546,609 msg/s |
| Dekaf | 2026-08-02T03:16:38.8720714+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/5 | 1,491,296 | 618.2s / 1,495,782 msg/s |
| Dekaf | 2026-08-02T03:16:56.8782712+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/5 | 1,541,164 | 636.2s / 1,555,788 msg/s |
| Dekaf | 2026-08-02T03:17:14.8830335+00:00 | 1 | 11.0 MiB / 9.7 MiB | 1770.3 MB/s | 4/5 | 1,593,468 | 654.2s / 1,605,442 msg/s |
| Dekaf | 2026-08-02T03:17:32.893608+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/6 | 1,641,747 | 672.2s / 1,560,006 msg/s |
| Dekaf | 2026-08-02T03:17:51.8972843+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/6 | 1,693,984 | 691.2s / 1,591,992 msg/s |
| Dekaf | 2026-08-02T03:18:09.8975137+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1770.3 MB/s | 4/6 | 1,739,509 | 709.2s / 1,601,184 msg/s |
| Dekaf | 2026-08-02T03:18:27.8983943+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/6 | 1,784,457 | 727.2s / 1,557,064 msg/s |
| Dekaf | 2026-08-02T03:18:45.900531+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/6 | 1,833,513 | 745.2s / 1,529,058 msg/s |
| Dekaf | 2026-08-02T03:19:03.9076842+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/6 | 1,882,380 | 763.2s / 1,524,250 msg/s |
| Dekaf | 2026-08-02T03:19:21.9170172+00:00 | 1 | 9.0 MiB / 4.5 MiB | 1770.3 MB/s | 4/6 | 1,924,556 | 781.2s / 1,400,124 msg/s |
| Dekaf | 2026-08-02T03:19:40.9221602+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1770.3 MB/s | 4/7 | 1,956,485 | 800.2s / 1,528,449 msg/s |
| Dekaf | 2026-08-02T03:19:58.9263249+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1770.3 MB/s | 4/7 | 2,003,744 | 818.2s / 1,528,737 msg/s |
| Dekaf | 2026-08-02T03:20:16.9322658+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/7 | 2,047,199 | 836.2s / 1,557,660 msg/s |
| Dekaf | 2026-08-02T03:20:34.9375503+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/7 | 2,090,972 | 854.2s / 1,517,921 msg/s |
| Dekaf | 2026-08-02T03:20:52.940171+00:00 | 1 | 11.0 MiB / 6.1 MiB | 1770.3 MB/s | 4/7 | 2,129,570 | 872.2s / 1,556,269 msg/s |
| Dekaf | 2026-08-02T03:21:10.9540834+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.3 MB/s | 4/7 | 2,175,391 | 890.2s / 1,562,097 msg/s |
| Dekaf | 2026-08-02T03:51:30.9346158+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1679.4 MB/s | 0/0 | 13,014 | 9.0s / 1,542,071 msg/s |
| Dekaf | 2026-08-02T03:51:48.9402426+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1679.4 MB/s | 0/0 | 39,905 | 27.0s / 1,489,552 msg/s |
| Dekaf | 2026-08-02T03:52:06.9510635+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1714.5 MB/s | 0/0 | 74,250 | 45.0s / 1,556,378 msg/s |
| Dekaf | 2026-08-02T03:52:24.9547961+00:00 | 1 | 14.0 MiB / 12.2 MiB | 1714.5 MB/s | 1/0 | 119,017 | 63.0s / 1,532,109 msg/s |
| Dekaf | 2026-08-02T03:52:42.9615079+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1714.5 MB/s | 1/0 | 164,824 | 81.0s / 1,541,653 msg/s |
| Dekaf | 2026-08-02T03:53:00.9675714+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1714.5 MB/s | 2/0 | 214,185 | 99.0s / 1,548,045 msg/s |
| Dekaf | 2026-08-02T03:53:19.975904+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1714.5 MB/s | 2/0 | 267,396 | 118.0s / 1,571,912 msg/s |
| Dekaf | 2026-08-02T03:53:37.9779984+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/1 | 314,636 | 136.0s / 1,539,754 msg/s |
| Dekaf | 2026-08-02T03:53:55.9853887+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/1 | 367,427 | 154.0s / 1,578,792 msg/s |
| Dekaf | 2026-08-02T03:54:13.9880949+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1714.5 MB/s | 2/1 | 419,781 | 172.0s / 1,571,608 msg/s |
| Dekaf | 2026-08-02T03:54:31.9976642+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/1 | 471,995 | 190.0s / 1,491,184 msg/s |
| Dekaf | 2026-08-02T03:54:50.0071256+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1714.5 MB/s | 2/1 | 524,666 | 208.0s / 1,542,202 msg/s |
| Dekaf | 2026-08-02T03:55:09.0125705+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/2 | 575,278 | 227.0s / 1,568,740 msg/s |
| Dekaf | 2026-08-02T03:55:27.0131571+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1714.5 MB/s | 2/2 | 627,275 | 245.1s / 1,572,717 msg/s |
| Dekaf | 2026-08-02T03:55:45.0188345+00:00 | 1 | 12.0 MiB / 10.1 MiB | 1714.5 MB/s | 2/2 | 677,787 | 263.1s / 1,422,960 msg/s |
| Dekaf | 2026-08-02T03:56:03.0210527+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/2 | 729,257 | 281.1s / 1,559,656 msg/s |
| Dekaf | 2026-08-02T03:56:21.0307307+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/2 | 775,697 | 299.1s / 1,537,951 msg/s |
| Dekaf | 2026-08-02T03:56:40.034636+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1714.5 MB/s | 2/2 | 824,958 | 318.1s / 1,552,148 msg/s |
| Dekaf | 2026-08-02T03:56:58.0402119+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/2 | 869,459 | 336.1s / 1,580,206 msg/s |
| Dekaf | 2026-08-02T03:57:16.0467478+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1714.5 MB/s | 2/3 | 919,337 | 354.1s / 1,554,672 msg/s |
| Dekaf | 2026-08-02T03:57:34.0542554+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/3 | 966,896 | 372.1s / 1,565,957 msg/s |
| Dekaf | 2026-08-02T03:57:52.066112+00:00 | 1 | 12.0 MiB / 8.9 MiB | 1714.5 MB/s | 2/3 | 1,016,723 | 390.1s / 1,545,782 msg/s |
| Dekaf | 2026-08-02T03:58:10.0712735+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/3 | 1,064,355 | 408.1s / 1,518,849 msg/s |
| Dekaf | 2026-08-02T03:58:29.0760019+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1714.5 MB/s | 2/3 | 1,111,410 | 427.1s / 1,427,274 msg/s |
| Dekaf | 2026-08-02T03:58:47.0780059+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/3 | 1,156,046 | 445.1s / 1,428,609 msg/s |
| Dekaf | 2026-08-02T03:59:05.0851926+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/3 | 1,199,590 | 463.1s / 1,492,210 msg/s |
| Dekaf | 2026-08-02T03:59:23.0891962+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1714.5 MB/s | 2/3 | 1,243,301 | 481.1s / 1,518,962 msg/s |
| Dekaf | 2026-08-02T03:59:41.0961847+00:00 | 1 | 12.0 MiB / 10.1 MiB | 1714.5 MB/s | 2/3 | 1,287,737 | 499.1s / 1,514,487 msg/s |
| Dekaf | 2026-08-02T03:59:59.1042963+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/3 | 1,331,998 | 517.1s / 1,507,733 msg/s |
| Dekaf | 2026-08-02T04:00:18.1090186+00:00 | 1 | 12.0 MiB / 10.5 MiB | 1714.5 MB/s | 2/3 | 1,371,184 | 536.1s / 1,425,761 msg/s |
| Dekaf | 2026-08-02T04:00:36.1120407+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1714.5 MB/s | 2/3 | 1,413,828 | 554.1s / 1,517,904 msg/s |
| Dekaf | 2026-08-02T04:00:54.115219+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1714.5 MB/s | 2/3 | 1,459,331 | 572.1s / 1,476,348 msg/s |
| Dekaf | 2026-08-02T04:01:12.1189418+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1714.5 MB/s | 2/3 | 1,500,022 | 590.1s / 1,404,038 msg/s |
| Dekaf | 2026-08-02T04:01:30.1234517+00:00 | 1 | 13.0 MiB / 11.9 MiB | 1714.5 MB/s | 3/3 | 1,539,113 | 608.2s / 1,549,245 msg/s |
| Dekaf | 2026-08-02T04:01:48.1343223+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1714.5 MB/s | 3/3 | 1,583,136 | 626.2s / 1,568,957 msg/s |
| Dekaf | 2026-08-02T04:02:07.1408835+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1714.5 MB/s | 3/3 | 1,630,540 | 645.2s / 1,549,807 msg/s |
| Dekaf | 2026-08-02T04:02:25.1484004+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1714.5 MB/s | 3/4 | 1,676,184 | 663.2s / 1,475,899 msg/s |
| Dekaf | 2026-08-02T04:02:43.1536807+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1714.5 MB/s | 3/4 | 1,721,807 | 681.2s / 1,513,338 msg/s |
| Dekaf | 2026-08-02T04:03:01.1604689+00:00 | 1 | 13.0 MiB / 12.3 MiB | 1714.5 MB/s | 3/4 | 1,770,892 | 699.2s / 1,531,879 msg/s |
| Dekaf | 2026-08-02T04:03:19.1715498+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1714.5 MB/s | 3/4 | 1,816,786 | 717.2s / 1,522,637 msg/s |
| Dekaf | 2026-08-02T04:03:37.1782467+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1714.5 MB/s | 4/4 | 1,863,407 | 735.2s / 1,547,251 msg/s |
| Dekaf | 2026-08-02T04:03:56.1850207+00:00 | 1 | 15.0 MiB / 14.4 MiB | 1714.5 MB/s | 4/4 | 1,907,789 | 754.2s / 1,495,775 msg/s |
| Dekaf | 2026-08-02T04:04:14.2005939+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1714.5 MB/s | 5/4 | 1,948,194 | 772.2s / 1,449,623 msg/s |
| Dekaf | 2026-08-02T04:04:32.2058562+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1714.5 MB/s | 5/4 | 1,986,378 | 790.2s / 1,405,865 msg/s |
| Dekaf | 2026-08-02T04:04:50.2099973+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1714.5 MB/s | 5/4 | 2,026,200 | 808.2s / 1,467,680 msg/s |
| Dekaf | 2026-08-02T04:05:08.2180293+00:00 | 1 | 16.0 MiB / 15.6 MiB | 1714.5 MB/s | 6/4 | 2,056,153 | 826.2s / 1,461,987 msg/s |
| Dekaf | 2026-08-02T04:05:26.2230232+00:00 | 1 | 18.0 MiB / 17.7 MiB | 1714.5 MB/s | 6/4 | 2,085,805 | 844.2s / 1,435,594 msg/s |
| Dekaf | 2026-08-02T04:05:45.2290358+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1714.5 MB/s | 6/5 | 2,116,445 | 863.2s / 1,470,236 msg/s |
| Dekaf | 2026-08-02T04:06:03.2425004+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1714.5 MB/s | 6/5 | 2,149,070 | 881.2s / 1,458,244 msg/s |
| Dekaf | 2026-08-02T04:06:21.2515613+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1714.5 MB/s | 6/5 | 2,179,181 | 899.3s / 1,431,708 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-02T03:06:50.8011964+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-08-02T03:07:05.8162299+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 11.7 MiB |
| Dekaf | 2026-08-02T03:07:35.8412716+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:07:50.8594703+00:00 | 1 | capacity | failed | 15,018ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:08:50.9118275+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:09:05.9242502+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 15.0 MiB |
| Dekaf | 2026-08-02T03:11:06.0210168+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:11:21.0311889+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:11:51.0774126+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:12:06.0922392+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 9.5 MiB |
| Dekaf | 2026-08-02T03:13:06.1391175+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:13:21.1508486+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:13:51.1742858+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.0 MiB |
| Dekaf | 2026-08-02T03:14:06.1861676+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 13.6 MiB |
| Dekaf | 2026-08-02T03:15:06.2458484+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.0 MiB |
| Dekaf | 2026-08-02T03:15:21.2608674+00:00 | 1 | capacity | succeeded | 15,015ms | 11.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:15:51.2852366+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:16:06.2989506+00:00 | 1 | capacity | failed | 15,013ms | 11.0 MiB / 6.9 MiB |
| Dekaf | 2026-08-02T03:17:06.3502398+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-02T03:17:21.362659+00:00 | 1 | capacity | failed | 15,011ms | 11.0 MiB / 8.7 MiB |
| Dekaf | 2026-08-02T03:19:21.4670514+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 9.8 MiB |
| Dekaf | 2026-08-02T03:19:36.4816672+00:00 | 1 | capacity | failed | 15,014ms | 11.0 MiB / 6.2 MiB |
| Dekaf | 2026-08-02T03:51:52.0318853+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T03:52:07.0528032+00:00 | 1 | capacity | succeeded | 15,021ms | 14.0 MiB / 13.4 MiB |
| Dekaf | 2026-08-02T03:52:37.0953586+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.2 MiB |
| Dekaf | 2026-08-02T03:52:52.109024+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:53:22.1320582+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:53:37.1438364+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 9.7 MiB |
| Dekaf | 2026-08-02T03:54:37.1928305+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:54:52.203716+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:56:52.3034839+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.0 MiB |
| Dekaf | 2026-08-02T03:57:07.3166592+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 10.0 MiB |
| Dekaf | 2026-08-02T04:01:07.527435+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 9.2 MiB |
| Dekaf | 2026-08-02T04:01:22.5417479+00:00 | 1 | capacity | succeeded | 15,014ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T04:01:52.5861399+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T04:02:07.6008587+00:00 | 1 | capacity | failed | 15,014ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-02T04:03:07.6547227+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.9 MiB |
| Dekaf | 2026-08-02T04:03:22.6673938+00:00 | 1 | capacity | succeeded | 15,012ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T04:03:52.7016412+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T04:04:07.7185819+00:00 | 1 | capacity | succeeded | 15,016ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:04:37.7475263+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:04:52.7650394+00:00 | 1 | capacity | succeeded | 15,017ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:05:22.8024658+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 12.7 MiB |
| Dekaf | 2026-08-02T04:05:37.8328793+00:00 | 1 | capacity | failed | 15,030ms | 16.0 MiB / 17.1 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,878 |
| Dekaf | 1 | 0.002–0.004ms | 2,341 |
| Dekaf | 1 | 0.004–0.008ms | 8,388 |
| Dekaf | 1 | 0.008–0.016ms | 39,374 |
| Dekaf | 1 | 0.016–0.032ms | 41,847 |
| Dekaf | 1 | 0.032–0.064ms | 46,950 |
| Dekaf | 1 | 0.064–0.128ms | 87,850 |
| Dekaf | 1 | 0.128–0.256ms | 247,084 |
| Dekaf | 1 | 0.256–0.512ms | 286,087 |
| Dekaf | 1 | 0.512–1.024ms | 73,417 |
| Dekaf | 1 | 1.024–2.048ms | 19,695 |
| Dekaf | 1 | 2.048–4.096ms | 3,903 |
| Dekaf | 1 | 4.096–8.192ms | 846 |
| Dekaf | 1 | 8.192–16.384ms | 71 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 0.001–0.002ms | 2,073 |
| Dekaf | 1 | 0.002–0.004ms | 2,646 |
| Dekaf | 1 | 0.004–0.008ms | 10,700 |
| Dekaf | 1 | 0.008–0.016ms | 33,661 |
| Dekaf | 1 | 0.016–0.032ms | 40,527 |
| Dekaf | 1 | 0.032–0.064ms | 51,261 |
| Dekaf | 1 | 0.064–0.128ms | 106,028 |
| Dekaf | 1 | 0.128–0.256ms | 284,724 |
| Dekaf | 1 | 0.256–0.512ms | 281,338 |
| Dekaf | 1 | 0.512–1.024ms | 53,668 |
| Dekaf | 1 | 1.024–2.048ms | 13,841 |
| Dekaf | 1 | 2.048–4.096ms | 3,709 |
| Dekaf | 1 | 4.096–8.192ms | 794 |
| Dekaf | 1 | 8.192–16.384ms | 47 |
| Dekaf | 1 | 32.768–65.536ms | 1 |

:::tip
**Dekaf uses 1.34x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.05x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.16 | 1170.14 | 1,055,978 | 1,063,018 | -0.3% | +0.01% | 1007.06 | 1,055,978 | 0 | 1.23 |
| Confluent | 1.81 | - | 844,744 | 847,968 | -0.6% | -0.00% | 805.61 | 844,744 | 0 | 1.53 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 310,206 | 344.67 | 992.85 KB |
| Dekaf | 2 | 313,782 | 348.64 | 998.10 KB |
| Dekaf | 3 | 319,786 | 355.31 | 1011.85 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:25.2979473+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 288,535 msg/s |
| Dekaf | 2026-08-02T03:06:34.2996241+00:00 | 3 | 16.0 MiB / 15.1 MiB | 306.9 MB/s | 0/0 | 4,109 | 9.0s / 711,668 msg/s |
| Dekaf | 2026-08-02T03:06:43.3091983+00:00 | 3 | 16.0 MiB / 13.8 MiB | 377.2 MB/s | 0/0 | 9,341 | 18.0s / 1,004,634 msg/s |
| Dekaf | 2026-08-02T03:06:53.3151676+00:00 | 1 | 16.0 MiB / 7.0 MiB | 370.2 MB/s | 0/0 | 2,083 | 28.0s / 1,090,004 msg/s |
| Dekaf | 2026-08-02T03:07:02.3161626+00:00 | 1 | 16.0 MiB / 1.5 MiB | 385.8 MB/s | 0/0 | 2,304 | 37.0s / 1,041,243 msg/s |
| Dekaf | 2026-08-02T03:07:11.3186459+00:00 | 1 | 14.0 MiB / 5.0 MiB | 393.5 MB/s | 1/0 | 2,740 | 46.0s / 1,097,911 msg/s |
| Dekaf | 2026-08-02T03:07:20.328276+00:00 | 1 | 14.0 MiB / 3.6 MiB | 393.5 MB/s | 1/0 | 2,879 | 55.0s / 1,084,600 msg/s |
| Dekaf | 2026-08-02T03:07:29.3316104+00:00 | 2 | 12.0 MiB / 5.7 MiB | 408.3 MB/s | 2/0 | 7,364 | 64.0s / 1,042,066 msg/s |
| Dekaf | 2026-08-02T03:07:38.3371807+00:00 | 2 | 12.0 MiB / 2.9 MiB | 408.3 MB/s | 2/0 | 8,605 | 73.0s / 1,072,245 msg/s |
| Dekaf | 2026-08-02T03:07:47.3403539+00:00 | 2 | 12.0 MiB / 10.1 MiB | 408.3 MB/s | 2/1 | 9,200 | 82.0s / 1,061,469 msg/s |
| Dekaf | 2026-08-02T03:07:56.3468535+00:00 | 2 | 12.0 MiB / 5.4 MiB | 408.3 MB/s | 2/1 | 10,627 | 91.1s / 1,081,894 msg/s |
| Dekaf | 2026-08-02T03:08:05.3488378+00:00 | 3 | 12.0 MiB / 11.8 MiB | 419.2 MB/s | 2/1 | 42,567 | 100.1s / 1,119,320 msg/s |
| Dekaf | 2026-08-02T03:08:14.3505398+00:00 | 3 | 12.0 MiB / 8.3 MiB | 430.1 MB/s | 2/1 | 46,344 | 109.1s / 1,111,001 msg/s |
| Dekaf | 2026-08-02T03:08:23.3563214+00:00 | 3 | 12.0 MiB / 8.4 MiB | 430.1 MB/s | 2/1 | 51,409 | 118.1s / 1,108,483 msg/s |
| Dekaf | 2026-08-02T03:08:32.3601774+00:00 | 3 | 12.0 MiB / 11.7 MiB | 432.7 MB/s | 2/2 | 54,225 | 127.1s / 1,089,842 msg/s |
| Dekaf | 2026-08-02T03:08:42.3690051+00:00 | 1 | 12.0 MiB / 4.2 MiB | 402.0 MB/s | 2/2 | 8,418 | 137.1s / 1,083,085 msg/s |
| Dekaf | 2026-08-02T03:08:51.3724246+00:00 | 1 | 12.0 MiB / 8.8 MiB | 402.0 MB/s | 2/2 | 9,805 | 146.1s / 1,107,113 msg/s |
| Dekaf | 2026-08-02T03:09:00.3770398+00:00 | 1 | 12.0 MiB / 10.1 MiB | 402.0 MB/s | 2/2 | 10,231 | 155.1s / 1,097,697 msg/s |
| Dekaf | 2026-08-02T03:09:09.3801855+00:00 | 1 | 12.0 MiB / 4.6 MiB | 402.0 MB/s | 2/2 | 11,340 | 164.1s / 1,107,687 msg/s |
| Dekaf | 2026-08-02T03:09:18.3842827+00:00 | 2 | 12.0 MiB / 7.6 MiB | 408.3 MB/s | 2/3 | 23,171 | 173.1s / 1,112,404 msg/s |
| Dekaf | 2026-08-02T03:09:27.3864796+00:00 | 2 | 12.0 MiB / 10.4 MiB | 408.3 MB/s | 2/3 | 24,269 | 182.1s / 1,047,161 msg/s |
| Dekaf | 2026-08-02T03:09:36.3902451+00:00 | 2 | 10.0 MiB / 7.0 MiB | 408.3 MB/s | 2/3 | 25,407 | 191.1s / 1,116,314 msg/s |
| Dekaf | 2026-08-02T03:09:45.3921799+00:00 | 2 | 10.0 MiB / 6.5 MiB | 408.3 MB/s | 2/3 | 26,216 | 200.1s / 1,084,417 msg/s |
| Dekaf | 2026-08-02T03:09:54.3942973+00:00 | 3 | 8.0 MiB / 5.1 MiB | 432.8 MB/s | 4/3 | 94,680 | 209.1s / 1,053,251 msg/s |
| Dekaf | 2026-08-02T03:10:03.3990412+00:00 | 3 | 8.0 MiB / 4.6 MiB | 432.8 MB/s | 4/3 | 98,710 | 218.1s / 1,113,171 msg/s |
| Dekaf | 2026-08-02T03:10:12.4041419+00:00 | 3 | 8.0 MiB / 7.9 MiB | 432.8 MB/s | 4/3 | 102,741 | 227.1s / 1,130,489 msg/s |
| Dekaf | 2026-08-02T03:10:21.410261+00:00 | 3 | 8.0 MiB / 7.6 MiB | 432.8 MB/s | 4/3 | 106,632 | 236.1s / 988,734 msg/s |
| Dekaf | 2026-08-02T03:10:31.4169527+00:00 | 1 | 12.0 MiB / 8.1 MiB | 404.2 MB/s | 2/2 | 15,336 | 246.1s / 1,068,605 msg/s |
| Dekaf | 2026-08-02T03:10:40.4231662+00:00 | 1 | 12.0 MiB / 5.4 MiB | 404.2 MB/s | 2/2 | 15,421 | 255.1s / 1,103,878 msg/s |
| Dekaf | 2026-08-02T03:10:49.4250889+00:00 | 1 | 10.0 MiB / 1.9 MiB | 404.2 MB/s | 3/2 | 15,592 | 264.1s / 1,134,109 msg/s |
| Dekaf | 2026-08-02T03:10:58.4271723+00:00 | 1 | 10.0 MiB / 7.1 MiB | 406.0 MB/s | 3/2 | 15,834 | 273.2s / 1,109,915 msg/s |
| Dekaf | 2026-08-02T03:11:07.428482+00:00 | 2 | 8.0 MiB / 5.1 MiB | 408.4 MB/s | 3/4 | 33,729 | 282.2s / 1,097,369 msg/s |
| Dekaf | 2026-08-02T03:11:16.431499+00:00 | 2 | 10.0 MiB / 3.1 MiB | 408.4 MB/s | 3/4 | 34,571 | 291.2s / 1,137,746 msg/s |
| Dekaf | 2026-08-02T03:11:25.4371928+00:00 | 2 | 7.0 MiB / 2.4 MiB | 408.4 MB/s | 4/4 | 35,755 | 300.2s / 1,123,259 msg/s |
| Dekaf | 2026-08-02T03:11:34.4400006+00:00 | 2 | 8.0 MiB / 3.1 MiB | 408.4 MB/s | 4/4 | 37,999 | 309.2s / 1,100,086 msg/s |
| Dekaf | 2026-08-02T03:11:43.4439758+00:00 | 2 | 8.0 MiB / 7.2 MiB | 408.4 MB/s | 4/5 | 40,134 | 318.2s / 1,100,702 msg/s |
| Dekaf | 2026-08-02T03:11:52.4475156+00:00 | 3 | 7.0 MiB / 4.9 MiB | 432.8 MB/s | 5/5 | 150,398 | 327.2s / 1,132,600 msg/s |
| Dekaf | 2026-08-02T03:12:01.4488608+00:00 | 3 | 6.0 MiB / 5.9 MiB | 432.8 MB/s | 5/5 | 155,383 | 336.2s / 1,099,834 msg/s |
| Dekaf | 2026-08-02T03:12:10.4517811+00:00 | 3 | 6.0 MiB / 4.2 MiB | 432.8 MB/s | 5/5 | 160,574 | 345.2s / 1,013,207 msg/s |
| Dekaf | 2026-08-02T03:12:19.4514213+00:00 | 3 | 5.0 MiB / 4.7 MiB | 432.8 MB/s | 6/5 | 165,872 | 354.2s / 1,072,131 msg/s |
| Dekaf | 2026-08-02T03:12:29.4593393+00:00 | 1 | 8.0 MiB / 5.6 MiB | 406.0 MB/s | 4/3 | 23,638 | 364.2s / 1,027,008 msg/s |
| Dekaf | 2026-08-02T03:12:38.4631923+00:00 | 1 | 8.0 MiB / 2.6 MiB | 406.0 MB/s | 4/3 | 24,160 | 373.2s / 995,594 msg/s |
| Dekaf | 2026-08-02T03:12:47.4641651+00:00 | 1 | 8.0 MiB / 3.6 MiB | 406.0 MB/s | 4/3 | 24,559 | 382.2s / 1,034,820 msg/s |
| Dekaf | 2026-08-02T03:12:56.4697733+00:00 | 1 | 8.0 MiB / 4.6 MiB | 406.0 MB/s | 4/3 | 25,196 | 391.2s / 1,093,187 msg/s |
| Dekaf | 2026-08-02T03:13:05.4727517+00:00 | 2 | 9.0 MiB / 7.1 MiB | 408.4 MB/s | 5/5 | 51,745 | 400.2s / 987,623 msg/s |
| Dekaf | 2026-08-02T03:13:14.47706+00:00 | 2 | 6.0 MiB / 2.9 MiB | 408.4 MB/s | 6/5 | 53,232 | 409.2s / 1,019,797 msg/s |
| Dekaf | 2026-08-02T03:13:23.4824653+00:00 | 2 | 7.0 MiB / 3.2 MiB | 408.4 MB/s | 6/5 | 55,113 | 418.2s / 1,087,763 msg/s |
| Dekaf | 2026-08-02T03:13:32.4901838+00:00 | 2 | 5.0 MiB / 3.6 MiB | 408.4 MB/s | 7/5 | 57,169 | 427.2s / 1,028,295 msg/s |
| Dekaf | 2026-08-02T03:13:41.4932828+00:00 | 3 | 5.0 MiB / 4.6 MiB | 432.8 MB/s | 7/6 | 226,506 | 436.2s / 1,090,851 msg/s |
| Dekaf | 2026-08-02T03:13:50.4983793+00:00 | 3 | 5.0 MiB / 3.8 MiB | 432.8 MB/s | 7/6 | 231,913 | 445.2s / 1,053,504 msg/s |
| Dekaf | 2026-08-02T03:13:59.5023559+00:00 | 3 | 5.0 MiB / 3.7 MiB | 432.8 MB/s | 7/6 | 237,049 | 454.3s / 1,121,997 msg/s |
| Dekaf | 2026-08-02T03:14:08.5031341+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/7 | 243,635 | 463.3s / 1,081,356 msg/s |
| Dekaf | 2026-08-02T03:14:18.5057127+00:00 | 1 | 8.0 MiB / 7.6 MiB | 406.0 MB/s | 4/4 | 31,574 | 473.3s / 1,006,623 msg/s |
| Dekaf | 2026-08-02T03:14:27.5101733+00:00 | 1 | 8.0 MiB / 2.3 MiB | 406.0 MB/s | 4/4 | 32,037 | 482.3s / 1,038,113 msg/s |
| Dekaf | 2026-08-02T03:14:36.5162005+00:00 | 1 | 8.0 MiB / 3.9 MiB | 406.0 MB/s | 4/4 | 32,860 | 491.3s / 1,086,759 msg/s |
| Dekaf | 2026-08-02T03:14:45.5172939+00:00 | 1 | 8.0 MiB / 5.0 MiB | 406.0 MB/s | 4/4 | 33,853 | 500.3s / 979,791 msg/s |
| Dekaf | 2026-08-02T03:14:54.518316+00:00 | 2 | 6.0 MiB / 6.0 MiB | 408.4 MB/s | 7/6 | 81,976 | 509.3s / 1,047,987 msg/s |
| Dekaf | 2026-08-02T03:15:03.5204666+00:00 | 2 | 6.0 MiB / 3.2 MiB | 408.4 MB/s | 7/7 | 85,405 | 518.3s / 1,053,960 msg/s |
| Dekaf | 2026-08-02T03:15:12.5204001+00:00 | 2 | 6.0 MiB / 2.5 MiB | 408.4 MB/s | 7/7 | 87,475 | 527.3s / 1,008,591 msg/s |
| Dekaf | 2026-08-02T03:15:21.52149+00:00 | 2 | 6.0 MiB / 4.3 MiB | 408.4 MB/s | 7/7 | 88,731 | 536.3s / 1,058,631 msg/s |
| Dekaf | 2026-08-02T03:15:30.523187+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/7 | 309,786 | 545.3s / 1,107,836 msg/s |
| Dekaf | 2026-08-02T03:15:39.5255187+00:00 | 3 | 5.0 MiB / 2.9 MiB | 432.8 MB/s | 7/7 | 317,623 | 554.3s / 1,075,907 msg/s |
| Dekaf | 2026-08-02T03:15:48.534549+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/7 | 324,141 | 563.3s / 926,573 msg/s |
| Dekaf | 2026-08-02T03:15:57.5346238+00:00 | 3 | 5.0 MiB / 3.8 MiB | 432.8 MB/s | 7/7 | 330,347 | 572.3s / 1,055,356 msg/s |
| Dekaf | 2026-08-02T03:16:07.5369645+00:00 | 1 | 8.0 MiB / 3.4 MiB | 406.0 MB/s | 4/6 | 38,921 | 582.3s / 907,984 msg/s |
| Dekaf | 2026-08-02T03:16:16.5411398+00:00 | 1 | 8.0 MiB / 3.3 MiB | 406.0 MB/s | 4/6 | 39,056 | 591.3s / 1,053,273 msg/s |
| Dekaf | 2026-08-02T03:16:25.5403747+00:00 | 1 | 8.0 MiB / 5.9 MiB | 406.0 MB/s | 4/6 | 39,950 | 600.3s / 1,019,532 msg/s |
| Dekaf | 2026-08-02T03:16:34.5440587+00:00 | 1 | 8.0 MiB / 5.2 MiB | 406.0 MB/s | 4/6 | 40,749 | 609.3s / 1,038,643 msg/s |
| Dekaf | 2026-08-02T03:16:43.547277+00:00 | 1 | 8.0 MiB / 2.5 MiB | 406.0 MB/s | 4/6 | 41,697 | 618.3s / 1,046,401 msg/s |
| Dekaf | 2026-08-02T03:16:52.5479968+00:00 | 2 | 6.0 MiB / 4.3 MiB | 408.4 MB/s | 7/7 | 117,781 | 627.3s / 1,057,673 msg/s |
| Dekaf | 2026-08-02T03:17:01.555987+00:00 | 2 | 6.0 MiB / 5.5 MiB | 408.4 MB/s | 7/7 | 120,123 | 636.3s / 1,087,228 msg/s |
| Dekaf | 2026-08-02T03:17:10.5607735+00:00 | 2 | 6.0 MiB / 4.6 MiB | 408.4 MB/s | 7/7 | 122,938 | 645.3s / 1,051,289 msg/s |
| Dekaf | 2026-08-02T03:17:19.5611596+00:00 | 2 | 7.0 MiB / 2.4 MiB | 408.4 MB/s | 8/7 | 125,321 | 654.3s / 1,087,179 msg/s |
| Dekaf | 2026-08-02T03:17:28.5630447+00:00 | 3 | 5.0 MiB / 4.2 MiB | 432.8 MB/s | 7/8 | 402,310 | 663.3s / 1,068,845 msg/s |
| Dekaf | 2026-08-02T03:17:37.5700831+00:00 | 3 | 5.0 MiB / 4.2 MiB | 432.8 MB/s | 7/8 | 409,227 | 672.4s / 1,043,536 msg/s |
| Dekaf | 2026-08-02T03:17:46.5713555+00:00 | 3 | 5.0 MiB / 4.6 MiB | 432.8 MB/s | 7/8 | 417,189 | 681.4s / 1,049,840 msg/s |
| Dekaf | 2026-08-02T03:17:55.5747334+00:00 | 3 | 5.0 MiB / 4.9 MiB | 432.8 MB/s | 7/8 | 424,331 | 690.4s / 1,075,248 msg/s |
| Dekaf | 2026-08-02T03:18:05.5835336+00:00 | 1 | 8.0 MiB / 5.8 MiB | 406.0 MB/s | 4/6 | 46,390 | 700.4s / 1,006,959 msg/s |
| Dekaf | 2026-08-02T03:18:14.5832662+00:00 | 1 | 8.0 MiB / 3.1 MiB | 406.0 MB/s | 4/6 | 46,732 | 709.4s / 1,108,713 msg/s |
| Dekaf | 2026-08-02T03:18:23.5879731+00:00 | 1 | 8.0 MiB / 5.2 MiB | 406.0 MB/s | 4/6 | 47,459 | 718.4s / 1,063,073 msg/s |
| Dekaf | 2026-08-02T03:18:32.5899028+00:00 | 1 | 8.0 MiB / 3.0 MiB | 406.0 MB/s | 4/6 | 48,481 | 727.4s / 1,032,867 msg/s |
| Dekaf | 2026-08-02T03:18:41.5902272+00:00 | 2 | 6.0 MiB / 4.9 MiB | 408.4 MB/s | 9/8 | 148,537 | 736.4s / 979,884 msg/s |
| Dekaf | 2026-08-02T03:18:50.5919065+00:00 | 2 | 6.0 MiB / 6.0 MiB | 408.4 MB/s | 9/8 | 151,440 | 745.4s / 983,754 msg/s |
| Dekaf | 2026-08-02T03:18:59.5988028+00:00 | 2 | 6.0 MiB / 4.1 MiB | 408.4 MB/s | 9/8 | 153,513 | 754.4s / 1,106,266 msg/s |
| Dekaf | 2026-08-02T03:19:08.5994169+00:00 | 2 | 6.0 MiB / 4.4 MiB | 408.4 MB/s | 9/9 | 155,851 | 763.4s / 1,049,521 msg/s |
| Dekaf | 2026-08-02T03:19:17.6020859+00:00 | 3 | 5.0 MiB / 2.4 MiB | 432.8 MB/s | 7/8 | 485,751 | 772.4s / 1,010,401 msg/s |
| Dekaf | 2026-08-02T03:19:26.6041946+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/8 | 492,349 | 781.4s / 1,011,241 msg/s |
| Dekaf | 2026-08-02T03:19:35.6064905+00:00 | 3 | 5.0 MiB / 2.7 MiB | 432.8 MB/s | 7/8 | 498,733 | 790.4s / 1,076,921 msg/s |
| Dekaf | 2026-08-02T03:19:44.6075468+00:00 | 3 | 5.0 MiB / 2.8 MiB | 432.8 MB/s | 7/8 | 505,690 | 799.4s / 1,091,539 msg/s |
| Dekaf | 2026-08-02T03:19:54.6101623+00:00 | 1 | 8.0 MiB / 4.9 MiB | 406.0 MB/s | 4/6 | 54,002 | 809.4s / 1,116,312 msg/s |
| Dekaf | 2026-08-02T03:20:03.6153964+00:00 | 1 | 8.0 MiB / 4.2 MiB | 406.0 MB/s | 4/6 | 54,612 | 818.4s / 1,087,973 msg/s |
| Dekaf | 2026-08-02T03:20:12.6176119+00:00 | 1 | 8.0 MiB / 8.0 MiB | 406.0 MB/s | 4/6 | 55,019 | 827.4s / 1,002,240 msg/s |
| Dekaf | 2026-08-02T03:20:21.6225244+00:00 | 1 | 9.0 MiB / 8.0 MiB | 406.0 MB/s | 4/6 | 55,782 | 836.4s / 1,072,991 msg/s |
| Dekaf | 2026-08-02T03:20:30.6242436+00:00 | 2 | 6.0 MiB / 4.8 MiB | 408.4 MB/s | 9/9 | 176,658 | 845.4s / 1,081,666 msg/s |
| Dekaf | 2026-08-02T03:20:39.6294819+00:00 | 2 | 6.0 MiB / 4.1 MiB | 408.4 MB/s | 9/9 | 177,913 | 854.4s / 1,103,286 msg/s |
| Dekaf | 2026-08-02T03:20:48.6332851+00:00 | 2 | 6.0 MiB / 3.9 MiB | 408.4 MB/s | 9/9 | 180,363 | 863.4s / 1,048,946 msg/s |
| Dekaf | 2026-08-02T03:20:57.6399052+00:00 | 2 | 6.0 MiB / 4.6 MiB | 408.4 MB/s | 9/9 | 182,942 | 872.4s / 1,078,603 msg/s |
| Dekaf | 2026-08-02T03:21:06.6403692+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/9 | 569,105 | 881.4s / 1,056,186 msg/s |
| Dekaf | 2026-08-02T03:21:15.6423929+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/9 | 575,911 | 890.4s / 1,041,767 msg/s |
| Dekaf | 2026-08-02T03:21:24.6523018+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 7/9 | 582,953 | 899.4s / 1,042,996 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-02T03:06:55.4540374+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 11.6 MiB |
| Dekaf | 2026-08-02T03:06:55.5174317+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:06:55.580707+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 5.0 MiB |
| Dekaf | 2026-08-02T03:07:10.5018572+00:00 | 2 | capacity | succeeded | 15,047ms | 14.0 MiB / 13.3 MiB |
| Dekaf | 2026-08-02T03:07:10.5743421+00:00 | 1 | capacity | succeeded | 15,056ms | 14.0 MiB / 10.5 MiB |
| Dekaf | 2026-08-02T03:07:10.6788467+00:00 | 3 | capacity | succeeded | 15,098ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:07:13.5134256+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 5.9 MiB |
| Dekaf | 2026-08-02T03:07:13.5876768+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.3 MiB |
| Dekaf | 2026-08-02T03:07:13.7173167+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:07:28.5564302+00:00 | 2 | capacity | succeeded | 15,043ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-02T03:07:28.6302895+00:00 | 1 | capacity | succeeded | 15,042ms | 12.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:07:28.7793805+00:00 | 3 | capacity | succeeded | 15,062ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:07:31.5700572+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.6 MiB |
| Dekaf | 2026-08-02T03:07:31.6395631+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 2.8 MiB |
| Dekaf | 2026-08-02T03:07:31.7963611+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:07:46.6158838+00:00 | 2 | capacity | failed | 15,045ms | 12.0 MiB / 7.0 MiB |
| Dekaf | 2026-08-02T03:07:46.737628+00:00 | 1 | capacity | failed | 15,098ms | 12.0 MiB / 7.7 MiB |
| Dekaf | 2026-08-02T03:07:46.850641+00:00 | 3 | capacity | failed | 15,054ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:08:16.7503571+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-02T03:08:16.8372842+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-02T03:08:16.9677469+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:08:31.797719+00:00 | 2 | capacity | failed | 15,047ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:08:31.8947685+00:00 | 1 | capacity | failed | 15,057ms | 12.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-02T03:08:32.0162264+00:00 | 3 | capacity | failed | 15,048ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:09:01.8989724+00:00 | 2 | capacity | started | 0ms | 13.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-02T03:09:02.1189998+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-02T03:09:03.9022641+00:00 | 2 | capacity | failed | 2,003ms | 12.0 MiB / 8.9 MiB |
| Dekaf | 2026-08-02T03:09:17.2162533+00:00 | 3 | capacity | succeeded | 15,097ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:09:20.2277992+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:09:33.9919456+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 7.2 MiB |
| Dekaf | 2026-08-02T03:09:35.269676+00:00 | 3 | capacity | succeeded | 15,041ms | 8.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:09:38.2787164+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.6 MiB |
| Dekaf | 2026-08-02T03:09:49.035316+00:00 | 2 | capacity | succeeded | 15,043ms | 10.0 MiB / 8.6 MiB |
| Dekaf | 2026-08-02T03:09:52.0421681+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:09:53.3202445+00:00 | 3 | capacity | failed | 15,041ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-02T03:10:07.0851973+00:00 | 2 | capacity | failed | 15,043ms | 10.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-02T03:10:23.4021678+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.2 MiB |
| Dekaf | 2026-08-02T03:10:32.3145529+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.0 MiB |
| Dekaf | 2026-08-02T03:10:38.4362257+00:00 | 3 | capacity | succeeded | 15,034ms | 7.0 MiB / 5.3 MiB |
| Dekaf | 2026-08-02T03:10:41.4444556+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 5.7 MiB |
| Dekaf | 2026-08-02T03:10:47.3719862+00:00 | 1 | capacity | succeeded | 15,057ms | 10.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-02T03:10:50.3830433+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 6.9 MiB |
| Dekaf | 2026-08-02T03:10:56.4999214+00:00 | 3 | capacity | failed | 15,055ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:11:05.4214661+00:00 | 1 | capacity | succeeded | 15,038ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:11:07.3115457+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.4 MiB |
| Dekaf | 2026-08-02T03:11:22.3580504+00:00 | 2 | capacity | succeeded | 15,046ms | 8.0 MiB / 1.9 MiB |
| Dekaf | 2026-08-02T03:11:25.3662183+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-02T03:11:26.5897623+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-02T03:11:29.092447+00:00 | 3 | capacity | failed | 2,502ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:11:35.5235719+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:11:40.4179603+00:00 | 2 | capacity | failed | 15,051ms | 8.0 MiB / 5.8 MiB |
| Dekaf | 2026-08-02T03:11:50.5803883+00:00 | 1 | capacity | failed | 15,056ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-02T03:11:59.2075316+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-02T03:12:10.5257163+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-02T03:12:14.2489996+00:00 | 3 | capacity | succeeded | 15,041ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:12:17.2589721+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-02T03:12:25.568327+00:00 | 2 | capacity | succeeded | 15,042ms | 9.0 MiB / 6.5 MiB |
| Dekaf | 2026-08-02T03:12:32.3084913+00:00 | 3 | capacity | succeeded | 15,049ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:12:35.3176532+00:00 | 3 | capacity | started | 0ms | 4.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:12:50.3619347+00:00 | 3 | capacity | failed | 15,044ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:12:50.8088061+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:12:55.6920556+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 8.2 MiB |
| Dekaf | 2026-08-02T03:13:05.8511952+00:00 | 1 | capacity | failed | 15,042ms | 8.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-02T03:13:10.756737+00:00 | 2 | capacity | succeeded | 15,064ms | 7.0 MiB / 5.4 MiB |
| Dekaf | 2026-08-02T03:13:13.7702007+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:13:28.8212276+00:00 | 2 | capacity | succeeded | 15,049ms | 6.0 MiB / 5.3 MiB |
| Dekaf | 2026-08-02T03:13:31.8243481+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:13:46.9021526+00:00 | 2 | capacity | failed | 15,077ms | 6.0 MiB / 1.8 MiB |
| Dekaf | 2026-08-02T03:13:50.5555891+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:14:05.6095785+00:00 | 3 | capacity | failed | 15,053ms | 5.0 MiB / 4.4 MiB |
| Dekaf | 2026-08-02T03:14:47.0925591+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 4.7 MiB |
| Dekaf | 2026-08-02T03:15:02.145159+00:00 | 2 | capacity | failed | 15,052ms | 6.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-02T03:15:06.3172438+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.4 MiB |
| Dekaf | 2026-08-02T03:15:21.3779572+00:00 | 1 | capacity | failed | 15,060ms | 8.0 MiB / 5.7 MiB |
| Dekaf | 2026-08-02T03:15:51.4631015+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-02T03:16:06.0440033+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 2.9 MiB |
| Dekaf | 2026-08-02T03:16:06.5290772+00:00 | 1 | capacity | failed | 15,065ms | 8.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:16:21.1037163+00:00 | 3 | capacity | failed | 15,059ms | 5.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-02T03:17:02.5785557+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.3 MiB |
| Dekaf | 2026-08-02T03:17:17.6217427+00:00 | 2 | capacity | succeeded | 15,043ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-02T03:17:47.7655322+00:00 | 2 | capacity | started | 0ms | 6.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-02T03:18:02.8081409+00:00 | 2 | capacity | succeeded | 15,042ms | 6.0 MiB / 3.3 MiB |
| Dekaf | 2026-08-02T03:18:05.8171202+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:18:20.8772401+00:00 | 2 | capacity | failed | 15,060ms | 6.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-02T03:18:50.9682457+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:19:06.0247056+00:00 | 2 | capacity | failed | 15,056ms | 6.0 MiB / 3.5 MiB |
| Dekaf | 2026-08-02T03:20:07.3784408+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.6 MiB |
| Dekaf | 2026-08-02T03:20:21.9165568+00:00 | 3 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:20:22.4274521+00:00 | 1 | capacity | failed | 15,048ms | 8.0 MiB / 3.7 MiB |
| Dekaf | 2026-08-02T03:20:36.9709877+00:00 | 3 | capacity | failed | 15,054ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:21:06.4373527+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 1.0 MiB |
| Dekaf | 2026-08-02T03:21:21.4764739+00:00 | 2 | capacity | failed | 15,039ms | 6.0 MiB / 2.2 MiB |

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 8 |
| Dekaf | 1 | 0.002–0.004ms | 21 |
| Dekaf | 1 | 0.004–0.008ms | 89 |
| Dekaf | 1 | 0.008–0.016ms | 218 |
| Dekaf | 1 | 0.016–0.032ms | 385 |
| Dekaf | 1 | 0.032–0.064ms | 598 |
| Dekaf | 1 | 0.064–0.128ms | 760 |
| Dekaf | 1 | 0.128–0.256ms | 1,255 |
| Dekaf | 1 | 0.256–0.512ms | 2,338 |
| Dekaf | 1 | 0.512–1.024ms | 3,331 |
| Dekaf | 1 | 1.024–2.048ms | 3,121 |
| Dekaf | 1 | 2.048–4.096ms | 1,697 |
| Dekaf | 1 | 4.096–8.192ms | 624 |
| Dekaf | 1 | 8.192–16.384ms | 172 |
| Dekaf | 1 | 16.384–32.768ms | 36 |
| Dekaf | 1 | 32.768–65.536ms | 2 |
| Dekaf | 2 | 0.001–0.002ms | 49 |
| Dekaf | 2 | 0.002–0.004ms | 53 |
| Dekaf | 2 | 0.004–0.008ms | 250 |
| Dekaf | 2 | 0.008–0.016ms | 611 |
| Dekaf | 2 | 0.016–0.032ms | 1,143 |
| Dekaf | 2 | 0.032–0.064ms | 1,813 |
| Dekaf | 2 | 0.064–0.128ms | 2,121 |
| Dekaf | 2 | 0.128–0.256ms | 3,426 |
| Dekaf | 2 | 0.256–0.512ms | 7,156 |
| Dekaf | 2 | 0.512–1.024ms | 10,692 |
| Dekaf | 2 | 1.024–2.048ms | 10,105 |
| Dekaf | 2 | 2.048–4.096ms | 5,402 |
| Dekaf | 2 | 4.096–8.192ms | 1,852 |
| Dekaf | 2 | 8.192–16.384ms | 567 |
| Dekaf | 2 | 16.384–32.768ms | 151 |
| Dekaf | 2 | 32.768–65.536ms | 12 |
| Dekaf | 3 | 0.001–0.002ms | 103 |
| Dekaf | 3 | 0.002–0.004ms | 162 |
| Dekaf | 3 | 0.004–0.008ms | 565 |
| Dekaf | 3 | 0.008–0.016ms | 1,420 |
| Dekaf | 3 | 0.016–0.032ms | 2,850 |
| Dekaf | 3 | 0.032–0.064ms | 4,814 |
| Dekaf | 3 | 0.064–0.128ms | 6,172 |
| Dekaf | 3 | 0.128–0.256ms | 10,625 |
| Dekaf | 3 | 0.256–0.512ms | 21,663 |
| Dekaf | 3 | 0.512–1.024ms | 34,001 |
| Dekaf | 3 | 1.024–2.048ms | 30,916 |
| Dekaf | 3 | 2.048–4.096ms | 15,330 |
| Dekaf | 3 | 4.096–8.192ms | 5,375 |
| Dekaf | 3 | 8.192–16.384ms | 1,623 |
| Dekaf | 3 | 16.384–32.768ms | 412 |
| Dekaf | 3 | 32.768–65.536ms | 28 |
| Dekaf | 3 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 40,000 | 2026-08-02T03:06:25.4285376+00:00 | 103.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 46,000 | 2026-08-02T03:06:25.4363882+00:00 | 111.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 49,000 | 2026-08-02T03:06:25.4397562+00:00 | 127.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 50,000 | 2026-08-02T03:06:25.4409619+00:00 | 125.9ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 53,000 | 2026-08-02T03:06:25.4446695+00:00 | 139.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 54,000 | 2026-08-02T03:06:25.4460026+00:00 | 123.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 55,000 | 2026-08-02T03:06:25.4469884+00:00 | 100.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 56,000 | 2026-08-02T03:06:25.4481831+00:00 | 121.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 57,000 | 2026-08-02T03:06:25.4534291+00:00 | 120.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 58,000 | 2026-08-02T03:06:25.4549717+00:00 | 114.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 63,000 | 2026-08-02T03:06:25.4649711+00:00 | 158.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 64,000 | 2026-08-02T03:06:25.4710547+00:00 | 118.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 66,000 | 2026-08-02T03:06:25.4789722+00:00 | 122.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 67,000 | 2026-08-02T03:06:25.4799814+00:00 | 127.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 68,000 | 2026-08-02T03:06:25.4812211+00:00 | 108.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 69,000 | 2026-08-02T03:06:25.482263+00:00 | 158.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 72,000 | 2026-08-02T03:06:25.4865039+00:00 | 109.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 74,000 | 2026-08-02T03:06:25.4994964+00:00 | 112.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 75,000 | 2026-08-02T03:06:25.5007481+00:00 | 101.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 85,000 | 2026-08-02T03:06:25.5424007+00:00 | 170.6ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 87,000 | 2026-08-02T03:06:25.5443991+00:00 | 155.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 89,000 | 2026-08-02T03:06:25.5508658+00:00 | 195.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 92,000 | 2026-08-02T03:06:25.5549353+00:00 | 158.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 93,000 | 2026-08-02T03:06:25.5560807+00:00 | 213.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 95,000 | 2026-08-02T03:06:25.5686071+00:00 | 159.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 96,000 | 2026-08-02T03:06:25.5701079+00:00 | 153.3ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 98,000 | 2026-08-02T03:06:25.5726847+00:00 | 197.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 102,000 | 2026-08-02T03:06:25.5880061+00:00 | 181.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 106,000 | 2026-08-02T03:06:25.5989946+00:00 | 172.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 107,000 | 2026-08-02T03:06:25.5999382+00:00 | 165.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 108,000 | 2026-08-02T03:06:25.6010421+00:00 | 168.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 115,000 | 2026-08-02T03:06:25.6277282+00:00 | 202.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 117,000 | 2026-08-02T03:06:25.632444+00:00 | 161.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 118,000 | 2026-08-02T03:06:25.6415652+00:00 | 188.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 119,000 | 2026-08-02T03:06:25.6429551+00:00 | 172.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 121,000 | 2026-08-02T03:06:25.6475191+00:00 | 182.6ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 128,000 | 2026-08-02T03:06:25.7232008+00:00 | 124.4ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 131,000 | 2026-08-02T03:06:25.7367583+00:00 | 135.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 133,000 | 2026-08-02T03:06:25.7390517+00:00 | 103.3ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 138,000 | 2026-08-02T03:06:25.74923+00:00 | 122.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 140,000 | 2026-08-02T03:06:25.7517484+00:00 | 111.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 142,000 | 2026-08-02T03:06:25.7697226+00:00 | 157.9ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 145,000 | 2026-08-02T03:06:25.7777239+00:00 | 134.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 152,000 | 2026-08-02T03:06:25.7938769+00:00 | 187.9ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 154,000 | 2026-08-02T03:06:25.7959514+00:00 | 114.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 156,000 | 2026-08-02T03:06:25.7994723+00:00 | 146.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 162,000 | 2026-08-02T03:06:25.8094021+00:00 | 196.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 163,000 | 2026-08-02T03:06:25.8199797+00:00 | 130.4ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 164,000 | 2026-08-02T03:06:25.8208814+00:00 | 132.4ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 167,000 | 2026-08-02T03:06:25.8313033+00:00 | 137.9ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 170,000 | 2026-08-02T03:06:25.834346+00:00 | 127.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 171,000 | 2026-08-02T03:06:25.835721+00:00 | 221.3ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 173,000 | 2026-08-02T03:06:25.837879+00:00 | 135.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 174,000 | 2026-08-02T03:06:25.8387273+00:00 | 136.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 177,000 | 2026-08-02T03:06:25.8492567+00:00 | 150.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 179,000 | 2026-08-02T03:06:25.8511299+00:00 | 155.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 180,000 | 2026-08-02T03:06:25.8521984+00:00 | 166.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 183,000 | 2026-08-02T03:06:25.8738228+00:00 | 160.4ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 184,000 | 2026-08-02T03:06:25.8749098+00:00 | 149.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 185,000 | 2026-08-02T03:06:25.8757605+00:00 | 215.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 186,000 | 2026-08-02T03:06:25.8767579+00:00 | 147.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 188,000 | 2026-08-02T03:06:25.9129392+00:00 | 195.9ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 189,000 | 2026-08-02T03:06:25.9140076+00:00 | 152.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 190,000 | 2026-08-02T03:06:25.9150172+00:00 | 159.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 191,000 | 2026-08-02T03:06:25.9158769+00:00 | 225.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 194,000 | 2026-08-02T03:06:25.9280277+00:00 | 124.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 195,000 | 2026-08-02T03:06:25.9292731+00:00 | 211.9ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 196,000 | 2026-08-02T03:06:25.9303029+00:00 | 135.4ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 197,000 | 2026-08-02T03:06:25.9311568+00:00 | 141.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 198,000 | 2026-08-02T03:06:25.9320088+00:00 | 209.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 199,000 | 2026-08-02T03:06:25.9333745+00:00 | 183.6ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 203,000 | 2026-08-02T03:06:26.0045955+00:00 | 112.4ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 209,000 | 2026-08-02T03:06:26.0135712+00:00 | 121.1ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 220,000 | 2026-08-02T03:06:26.068341+00:00 | 147.5ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 221,000 | 2026-08-02T03:06:26.0696811+00:00 | 142.3ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 222,000 | 2026-08-02T03:06:26.0708149+00:00 | 141.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 223,000 | 2026-08-02T03:06:26.0721381+00:00 | 143.8ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 228,000 | 2026-08-02T03:06:26.0961258+00:00 | 126.7ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 235,000 | 2026-08-02T03:06:26.1164233+00:00 | 122.0ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 238,000 | 2026-08-02T03:06:26.1210782+00:00 | 123.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 243,000 | 2026-08-02T03:06:26.1456678+00:00 | 200.4ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 248,000 | 2026-08-02T03:06:26.1519998+00:00 | 166.2ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 249,000 | 2026-08-02T03:06:26.1532731+00:00 | 224.5ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 250,000 | 2026-08-02T03:06:26.1598159+00:00 | 218.2ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 251,000 | 2026-08-02T03:06:26.1610226+00:00 | 169.3ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 260,000 | 2026-08-02T03:06:26.1909648+00:00 | 225.2ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 261,000 | 2026-08-02T03:06:26.1921118+00:00 | 164.2ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 262,000 | 2026-08-02T03:06:26.1934616+00:00 | 185.9ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 267,000 | 2026-08-02T03:06:26.2195042+00:00 | 112.3ms | GC pause | - | - | 1.0s / 288,535 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 268,000 | 2026-08-02T03:06:26.2207803+00:00 | 163.0ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 269,000 | 2026-08-02T03:06:26.2220485+00:00 | 194.1ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 271,000 | 2026-08-02T03:06:26.2266792+00:00 | 162.4ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 273,000 | 2026-08-02T03:06:26.2290793+00:00 | 210.7ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 274,000 | 2026-08-02T03:06:26.2305255+00:00 | 109.8ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 275,000 | 2026-08-02T03:06:26.2316622+00:00 | 165.5ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 279,000 | 2026-08-02T03:06:26.2400306+00:00 | 210.4ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 280,000 | 2026-08-02T03:06:26.2412927+00:00 | 209.2ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 282,000 | 2026-08-02T03:06:26.2438547+00:00 | 178.6ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 284,000 | 2026-08-02T03:06:26.2473313+00:00 | 120.2ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 286,000 | 2026-08-02T03:06:26.2501139+00:00 | 117.4ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 287,000 | 2026-08-02T03:06:26.2515037+00:00 | 122.2ms | GC pause | - | - | 2.0s / 473,596 msg/s | Gen2 +1 / pause +7.1ms |
| Dekaf | 293,000 | 2026-08-02T03:06:26.3516524+00:00 | 125.4ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 300,000 | 2026-08-02T03:06:26.385352+00:00 | 107.9ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 303,000 | 2026-08-02T03:06:26.3909171+00:00 | 113.6ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 331,000 | 2026-08-02T03:06:26.4644411+00:00 | 116.2ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 332,000 | 2026-08-02T03:06:26.4656514+00:00 | 115.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 333,000 | 2026-08-02T03:06:26.4672695+00:00 | 112.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 335,000 | 2026-08-02T03:06:26.4697615+00:00 | 115.9ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 339,000 | 2026-08-02T03:06:26.4800428+00:00 | 119.1ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 340,000 | 2026-08-02T03:06:26.4813418+00:00 | 132.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 341,000 | 2026-08-02T03:06:26.4826499+00:00 | 115.4ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 342,000 | 2026-08-02T03:06:26.4839005+00:00 | 114.1ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 343,000 | 2026-08-02T03:06:26.4930776+00:00 | 120.6ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 348,000 | 2026-08-02T03:06:26.4996572+00:00 | 112.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 349,000 | 2026-08-02T03:06:26.5022681+00:00 | 122.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 350,000 | 2026-08-02T03:06:26.5038018+00:00 | 122.2ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 351,000 | 2026-08-02T03:06:26.5065065+00:00 | 100.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 353,000 | 2026-08-02T03:06:26.5103587+00:00 | 114.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 358,000 | 2026-08-02T03:06:26.5167187+00:00 | 109.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 359,000 | 2026-08-02T03:06:26.5180254+00:00 | 110.6ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 360,000 | 2026-08-02T03:06:26.519299+00:00 | 118.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 363,000 | 2026-08-02T03:06:26.5242617+00:00 | 118.8ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 369,000 | 2026-08-02T03:06:26.5442187+00:00 | 108.7ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 370,000 | 2026-08-02T03:06:26.5457413+00:00 | 115.2ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 413,000 | 2026-08-02T03:06:26.6485351+00:00 | 156.4ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 419,000 | 2026-08-02T03:06:26.6599191+00:00 | 149.9ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 420,000 | 2026-08-02T03:06:26.6604264+00:00 | 147.1ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 421,000 | 2026-08-02T03:06:26.6667101+00:00 | 129.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 422,000 | 2026-08-02T03:06:26.6672366+00:00 | 128.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 423,000 | 2026-08-02T03:06:26.6679539+00:00 | 141.9ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 429,000 | 2026-08-02T03:06:26.7027011+00:00 | 126.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 435,000 | 2026-08-02T03:06:26.7067071+00:00 | 100.8ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 438,000 | 2026-08-02T03:06:26.7188504+00:00 | 106.8ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 439,000 | 2026-08-02T03:06:26.7194481+00:00 | 125.7ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 443,000 | 2026-08-02T03:06:26.7252805+00:00 | 140.4ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 445,000 | 2026-08-02T03:06:26.7262298+00:00 | 113.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 448,000 | 2026-08-02T03:06:26.7289608+00:00 | 110.7ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 449,000 | 2026-08-02T03:06:26.7351236+00:00 | 137.2ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 451,000 | 2026-08-02T03:06:26.7363434+00:00 | 112.2ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 452,000 | 2026-08-02T03:06:26.7368725+00:00 | 111.7ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 453,000 | 2026-08-02T03:06:26.737567+00:00 | 134.8ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 493,000 | 2026-08-02T03:06:26.8488541+00:00 | 105.4ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 499,000 | 2026-08-02T03:06:26.8692064+00:00 | 109.1ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 503,000 | 2026-08-02T03:06:26.8755299+00:00 | 104.1ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 702,000 | 2026-08-02T03:06:27.2080235+00:00 | 123.0ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 711,000 | 2026-08-02T03:06:27.2162599+00:00 | 113.5ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 712,000 | 2026-08-02T03:06:27.2170601+00:00 | 112.7ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 715,000 | 2026-08-02T03:06:27.2197665+00:00 | 112.7ms | throughput collapse | - | - | 2.0s / 473,596 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 722,000 | 2026-08-02T03:06:27.2302954+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 731,000 | 2026-08-02T03:06:27.24282+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 741,000 | 2026-08-02T03:06:27.2607144+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 745,000 | 2026-08-02T03:06:27.2648714+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 748,000 | 2026-08-02T03:06:27.2674503+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 882,000 | 2026-08-02T03:06:27.5007606+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 885,000 | 2026-08-02T03:06:27.5066452+00:00 | 120.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 888,000 | 2026-08-02T03:06:27.5171382+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 891,000 | 2026-08-02T03:06:27.5184142+00:00 | 110.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 892,000 | 2026-08-02T03:06:27.51883+00:00 | 110.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 898,000 | 2026-08-02T03:06:27.5341067+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 901,000 | 2026-08-02T03:06:27.5358967+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 902,000 | 2026-08-02T03:06:27.5363723+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 908,000 | 2026-08-02T03:06:27.5397358+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 912,000 | 2026-08-02T03:06:27.5494118+00:00 | 122.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 915,000 | 2026-08-02T03:06:27.5516282+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 918,000 | 2026-08-02T03:06:27.5533045+00:00 | 141.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 921,000 | 2026-08-02T03:06:27.5565096+00:00 | 140.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 922,000 | 2026-08-02T03:06:27.5568019+00:00 | 139.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 925,000 | 2026-08-02T03:06:27.5581768+00:00 | 138.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 929,000 | 2026-08-02T03:06:27.5678292+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 930,000 | 2026-08-02T03:06:27.5683109+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 931,000 | 2026-08-02T03:06:27.5688678+00:00 | 143.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 932,000 | 2026-08-02T03:06:27.569615+00:00 | 142.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,211,000 | 2026-08-02T03:06:28.0285923+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,212,000 | 2026-08-02T03:06:28.0294223+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,219,000 | 2026-08-02T03:06:28.0378817+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,220,000 | 2026-08-02T03:06:28.038878+00:00 | 103.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,221,000 | 2026-08-02T03:06:28.0396176+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,229,000 | 2026-08-02T03:06:28.0666721+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,238,000 | 2026-08-02T03:06:28.0822299+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,242,000 | 2026-08-02T03:06:28.0841545+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,243,000 | 2026-08-02T03:06:28.0844484+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,249,000 | 2026-08-02T03:06:28.1040861+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,255,000 | 2026-08-02T03:06:28.1130971+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,260,000 | 2026-08-02T03:06:28.1293601+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,262,000 | 2026-08-02T03:06:28.1302802+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,263,000 | 2026-08-02T03:06:28.1309669+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,278,000 | 2026-08-02T03:06:28.151818+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 615,744 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,337,000 | 2026-08-02T03:06:28.2638015+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 658,946 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,344,000 | 2026-08-02T03:06:28.2747006+00:00 | 112.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 658,946 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,356,000 | 2026-08-02T03:06:28.2842316+00:00 | 123.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 658,946 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,367,000 | 2026-08-02T03:06:28.314208+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 658,946 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,374,000 | 2026-08-02T03:06:28.3245359+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 658,946 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,376,000 | 2026-08-02T03:06:28.3259553+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 658,946 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,219,000 | 2026-08-02T03:06:29.5824675+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,223,000 | 2026-08-02T03:06:29.5859851+00:00 | 118.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,230,000 | 2026-08-02T03:06:29.5915472+00:00 | 128.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,239,000 | 2026-08-02T03:06:29.6015847+00:00 | 124.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,240,000 | 2026-08-02T03:06:29.6023979+00:00 | 131.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,250,000 | 2026-08-02T03:06:29.6151362+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,253,000 | 2026-08-02T03:06:29.6166266+00:00 | 123.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,260,000 | 2026-08-02T03:06:29.6358456+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,263,000 | 2026-08-02T03:06:29.6405063+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,313,000 | 2026-08-02T03:06:29.76094+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 686,168 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,360,000 | 2026-08-02T03:06:31.0923775+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 804,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,363,000 | 2026-08-02T03:06:31.0940978+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 804,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,379,000 | 2026-08-02T03:06:31.1210575+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 804,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,380,000 | 2026-08-02T03:06:31.1219264+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 804,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,393,000 | 2026-08-02T03:06:31.1391306+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 804,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,773,000 | 2026-08-02T03:06:31.6140045+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 810,613 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,779,000 | 2026-08-02T03:06:31.6169753+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 810,613 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,780,000 | 2026-08-02T03:06:31.6176031+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 810,613 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,783,000 | 2026-08-02T03:06:31.6203169+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 810,613 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,793,000 | 2026-08-02T03:06:31.6322094+00:00 | 100.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 810,613 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,457,000 | 2026-08-02T03:06:32.4784424+00:00 | 118.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,464,000 | 2026-08-02T03:06:32.4861961+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,466,000 | 2026-08-02T03:06:32.4908437+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,467,000 | 2026-08-02T03:06:32.4911644+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,476,000 | 2026-08-02T03:06:32.4980615+00:00 | 124.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,477,000 | 2026-08-02T03:06:32.5008326+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,484,000 | 2026-08-02T03:06:32.5064367+00:00 | 127.5ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,494,000 | 2026-08-02T03:06:32.5419209+00:00 | 122.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,497,000 | 2026-08-02T03:06:32.5429898+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,504,000 | 2026-08-02T03:06:32.559255+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,026,000 | 2026-08-02T03:06:33.1763985+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 788,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,360,000 | 2026-08-02T03:06:33.5809343+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,399,000 | 2026-08-02T03:06:33.6655983+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,400,000 | 2026-08-02T03:06:33.6663053+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,586,000 | 2026-08-02T03:06:33.9300317+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,587,000 | 2026-08-02T03:06:33.9402658+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,607,000 | 2026-08-02T03:06:33.9683199+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,617,000 | 2026-08-02T03:06:33.9812941+00:00 | 113.5ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,700,000 | 2026-08-02T03:06:34.1207439+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,709,000 | 2026-08-02T03:06:34.1361576+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,710,000 | 2026-08-02T03:06:34.1365871+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,713,000 | 2026-08-02T03:06:34.1442987+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,723,000 | 2026-08-02T03:06:34.1498097+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 711,668 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,360,000 | 2026-08-02T03:06:36.1320665+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 858,214 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,370,000 | 2026-08-02T03:06:36.1382419+00:00 | 109.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 858,214 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,380,000 | 2026-08-02T03:06:36.1497111+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 858,214 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,769,000 | 2026-08-02T03:06:36.6117854+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,773,000 | 2026-08-02T03:06:36.6156875+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,779,000 | 2026-08-02T03:06:36.6201041+00:00 | 113.3ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,783,000 | 2026-08-02T03:06:36.6219487+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,793,000 | 2026-08-02T03:06:36.6483245+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,799,000 | 2026-08-02T03:06:36.651683+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,800,000 | 2026-08-02T03:06:36.6520947+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 785,980 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,850,000 | 2026-08-02T03:06:40.1177594+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 978,280 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,868,000 | 2026-08-02T03:06:40.1313126+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 15.0s / 978,280 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,359,000 | 2026-08-02T03:06:43.6113259+00:00 | 108.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,016,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,370,000 | 2026-08-02T03:06:43.6234625+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,016,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,383,000 | 2026-08-02T03:06:43.6377144+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,016,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,389,000 | 2026-08-02T03:06:43.6443845+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,016,296 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 15,903,000 | 2026-08-02T03:06:45.1446745+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 985,137 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf | 16,833,000 | 2026-08-02T03:06:46.1457148+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 925,906 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 31,681,000 | 2026-08-02T03:07:00.1476169+00:00 | 100.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded, 3:capacity/succeeded | - | 35.0s / 1,067,619 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 125,477,000 | 2026-08-02T03:08:26.6152982+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/failed, 3:capacity/failed | - | 122.1s / 1,064,885 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 34,615,000 | 2026-08-02T03:22:06.7830541+00:00 | 101.7ms | GC pause | - | - | 42.0s / 836,218 msg/s | Gen2 +0 / pause +101.2ms |
| Confluent | 283,687,000 | 2026-08-02T03:26:57.6830589+00:00 | 105.1ms | GC pause | - | - | 333.2s / 748,549 msg/s | Gen2 +0 / pause +161.8ms |
| Confluent | 283,688,000 | 2026-08-02T03:26:57.6836276+00:00 | 104.6ms | GC pause | - | - | 333.2s / 748,549 msg/s | Gen2 +0 / pause +161.8ms |
| Confluent | 283,691,000 | 2026-08-02T03:26:57.6853036+00:00 | 103.2ms | GC pause | - | - | 333.2s / 748,549 msg/s | Gen2 +0 / pause +161.8ms |
| Confluent | 499,032,000 | 2026-08-02T03:31:18.6438209+00:00 | 100.3ms | GC pause | - | - | 593.4s / 858,149 msg/s | Gen2 +0 / pause +102.9ms |
| Confluent | 576,704,000 | 2026-08-02T03:32:49.1811833+00:00 | 100.3ms | GC pause | - | - | 684.5s / 813,953 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 576,714,000 | 2026-08-02T03:32:49.188407+00:00 | 101.8ms | GC pause | - | - | 684.5s / 813,953 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 576,734,000 | 2026-08-02T03:32:49.2083212+00:00 | 103.6ms | GC pause | - | - | 684.5s / 813,953 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 576,744,000 | 2026-08-02T03:32:49.2223566+00:00 | 101.7ms | GC pause | - | - | 684.5s / 813,953 msg/s | Gen2 +0 / pause +74.5ms |
| Confluent | 613,262,000 | 2026-08-02T03:33:32.1977407+00:00 | 108.6ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,272,000 | 2026-08-02T03:33:32.2077301+00:00 | 100.9ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,285,000 | 2026-08-02T03:33:32.2221596+00:00 | 106.8ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,286,000 | 2026-08-02T03:33:32.2232282+00:00 | 106.0ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,289,000 | 2026-08-02T03:33:32.2258411+00:00 | 103.5ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,292,000 | 2026-08-02T03:33:32.2290225+00:00 | 102.1ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,304,000 | 2026-08-02T03:33:32.2439276+00:00 | 106.3ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,314,000 | 2026-08-02T03:33:32.2538672+00:00 | 106.1ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,324,000 | 2026-08-02T03:33:32.2661813+00:00 | 105.8ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 613,334,000 | 2026-08-02T03:33:32.2750603+00:00 | 104.5ms | GC pause | - | - | 727.5s / 862,581 msg/s | Gen2 +0 / pause +106.9ms |
| Confluent | 646,658,000 | 2026-08-02T03:34:12.2175501+00:00 | 107.0ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,661,000 | 2026-08-02T03:34:12.2193164+00:00 | 105.4ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,667,000 | 2026-08-02T03:34:12.2262811+00:00 | 101.4ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,668,000 | 2026-08-02T03:34:12.2268373+00:00 | 100.9ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,671,000 | 2026-08-02T03:34:12.2296754+00:00 | 110.7ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,677,000 | 2026-08-02T03:34:12.2348756+00:00 | 107.3ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,678,000 | 2026-08-02T03:34:12.2361891+00:00 | 106.1ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,681,000 | 2026-08-02T03:34:12.2383173+00:00 | 113.1ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,687,000 | 2026-08-02T03:34:12.2480128+00:00 | 107.9ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,688,000 | 2026-08-02T03:34:12.2486847+00:00 | 107.2ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,691,000 | 2026-08-02T03:34:12.2513782+00:00 | 104.7ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,697,000 | 2026-08-02T03:34:12.2564096+00:00 | 103.6ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,698,000 | 2026-08-02T03:34:12.2573219+00:00 | 106.4ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 646,701,000 | 2026-08-02T03:34:12.2607885+00:00 | 103.0ms | GC pause | - | - | 767.6s / 793,026 msg/s | Gen2 +0 / pause +105.3ms |
| Confluent | 705,517,000 | 2026-08-02T03:35:21.6609903+00:00 | 103.1ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,518,000 | 2026-08-02T03:35:21.6615521+00:00 | 102.6ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,521,000 | 2026-08-02T03:35:21.6654594+00:00 | 105.3ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,527,000 | 2026-08-02T03:35:21.6696559+00:00 | 118.5ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,528,000 | 2026-08-02T03:35:21.6702227+00:00 | 118.0ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,531,000 | 2026-08-02T03:35:21.6728365+00:00 | 119.3ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,537,000 | 2026-08-02T03:35:21.6766267+00:00 | 118.7ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,538,000 | 2026-08-02T03:35:21.6773441+00:00 | 118.1ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,541,000 | 2026-08-02T03:35:21.6790428+00:00 | 116.5ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,547,000 | 2026-08-02T03:35:21.6849608+00:00 | 111.6ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,548,000 | 2026-08-02T03:35:21.6875775+00:00 | 109.8ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,551,000 | 2026-08-02T03:35:21.6902401+00:00 | 107.4ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,557,000 | 2026-08-02T03:35:21.6965927+00:00 | 109.6ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,558,000 | 2026-08-02T03:35:21.6976252+00:00 | 108.6ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,561,000 | 2026-08-02T03:35:21.7036299+00:00 | 105.2ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,567,000 | 2026-08-02T03:35:21.7080902+00:00 | 101.1ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,568,000 | 2026-08-02T03:35:21.7086192+00:00 | 100.6ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 705,571,000 | 2026-08-02T03:35:21.7100723+00:00 | 100.8ms | GC pause | - | - | 836.6s / 827,246 msg/s | Gen2 +0 / pause +132.2ms |
| Confluent | 733,506,000 | 2026-08-02T03:35:53.6638902+00:00 | 104.5ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,517,000 | 2026-08-02T03:35:53.6757956+00:00 | 107.0ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,518,000 | 2026-08-02T03:35:53.6762398+00:00 | 106.6ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,521,000 | 2026-08-02T03:35:53.6782351+00:00 | 108.0ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,527,000 | 2026-08-02T03:35:53.6830435+00:00 | 103.5ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,528,000 | 2026-08-02T03:35:53.6843831+00:00 | 102.3ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,535,000 | 2026-08-02T03:35:53.6961288+00:00 | 103.9ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,536,000 | 2026-08-02T03:35:53.6976375+00:00 | 109.5ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 733,539,000 | 2026-08-02T03:35:53.7014818+00:00 | 105.8ms | GC pause | - | - | 868.6s / 846,819 msg/s | Gen2 +0 / pause +164.7ms |
| Confluent | 757,578,000 | 2026-08-02T03:36:22.2258737+00:00 | 100.7ms | GC pause | - | - | 897.6s / 865,491 msg/s | Gen2 +0 / pause +112.3ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*203 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.56x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.25x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,611,347 | 1,589,425–1,633,572 | 0.93 | 1.15x |
| Confluent | 2 | 1,401,920 | 1,401,686–1,402,153 | 1.28 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.71 | 684.83 | 1,863,891 | 1,868,327 | +2.6% | +0.33% | 1777.55 | 1,863,891 | 0 | 1.32 |
| Dekaf (dekaf-first) | 0.89 | 909.98 | 1,616,656 | 1,633,572 | -0.9% | -0.06% | 1541.76 | 1,616,656 | 0 | 1.44 |
| Dekaf (confluent-first) | 0.97 | 997.40 | 1,576,502 | 1,589,425 | -1.9% | -0.16% | 1503.47 | 1,576,502 | 0 | 1.53 |
| Confluent (dekaf-first) | 1.28 | - | 1,379,740 | 1,402,153 | -0.4% | +0.06% | 1315.82 | 1,379,740 | 0 | 1.77 |
| Confluent (confluent-first) | 1.28 | - | 1,391,153 | 1,401,686 | -2.2% | -0.25% | 1326.71 | 1,391,153 | 0 | 1.79 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,421,175 | 1579.06 | 1017.72 KB |
| Dekaf | 1 | 1,381,405 | 1534.87 | 1021.02 KB |
| Dekaf (3conn) | 1 | 1,739,830 | 1933.13 | 958.45 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:19.7358489+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 637,322 msg/s |
| Dekaf | 2026-08-02T03:06:46.7418467+00:00 | 1 | 16.0 MiB / 8.4 MiB | 1756.6 MB/s | 0/0 | 52,479 | 27.0s / 1,635,825 msg/s |
| Dekaf | 2026-08-02T03:07:14.7544495+00:00 | 1 | 14.0 MiB / 13.5 MiB | 1762.7 MB/s | 1/0 | 110,845 | 55.0s / 1,622,631 msg/s |
| Dekaf | 2026-08-02T03:07:41.7616022+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1767.5 MB/s | 1/0 | 166,470 | 82.0s / 1,432,232 msg/s |
| Dekaf | 2026-08-02T03:08:08.7687911+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1780.7 MB/s | 2/0 | 222,448 | 109.0s / 1,584,699 msg/s |
| Dekaf | 2026-08-02T03:08:35.7739683+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1804.1 MB/s | 2/1 | 284,997 | 136.0s / 1,691,554 msg/s |
| Dekaf | 2026-08-02T03:09:03.7938484+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1806.2 MB/s | 2/1 | 352,367 | 164.1s / 1,657,783 msg/s |
| Dekaf | 2026-08-02T03:09:30.8025603+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1806.2 MB/s | 2/1 | 417,101 | 191.1s / 1,639,939 msg/s |
| Dekaf | 2026-08-02T03:09:57.8131501+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1806.2 MB/s | 3/1 | 480,226 | 218.1s / 1,680,737 msg/s |
| Dekaf | 2026-08-02T03:10:24.8199618+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1806.2 MB/s | 3/1 | 538,138 | 245.1s / 1,660,037 msg/s |
| Dekaf | 2026-08-02T03:10:52.8242715+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1806.2 MB/s | 3/2 | 603,527 | 273.1s / 1,666,105 msg/s |
| Dekaf | 2026-08-02T03:11:19.8352336+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1806.2 MB/s | 3/2 | 662,976 | 300.1s / 1,622,660 msg/s |
| Dekaf | 2026-08-02T03:11:46.8412708+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1806.2 MB/s | 3/2 | 724,321 | 327.1s / 1,672,849 msg/s |
| Dekaf | 2026-08-02T03:12:13.8513958+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1806.2 MB/s | 4/2 | 790,499 | 354.1s / 1,663,942 msg/s |
| Dekaf | 2026-08-02T03:12:41.8578148+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1806.2 MB/s | 4/3 | 847,178 | 382.1s / 1,626,951 msg/s |
| Dekaf | 2026-08-02T03:13:08.8636909+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1806.2 MB/s | 4/3 | 911,839 | 409.1s / 1,598,380 msg/s |
| Dekaf | 2026-08-02T03:13:35.8716743+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1806.2 MB/s | 4/3 | 977,043 | 436.1s / 1,593,145 msg/s |
| Dekaf | 2026-08-02T03:14:02.878113+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1806.2 MB/s | 5/3 | 1,040,927 | 463.1s / 1,625,661 msg/s |
| Dekaf | 2026-08-02T03:14:30.8854538+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1806.2 MB/s | 5/3 | 1,108,498 | 491.2s / 1,682,766 msg/s |
| Dekaf | 2026-08-02T03:14:57.8988203+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1806.2 MB/s | 5/4 | 1,172,727 | 518.2s / 1,656,280 msg/s |
| Dekaf | 2026-08-02T03:15:24.9110859+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1806.2 MB/s | 5/4 | 1,232,814 | 545.2s / 1,613,805 msg/s |
| Dekaf | 2026-08-02T03:15:52.9221536+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1806.2 MB/s | 5/5 | 1,294,870 | 573.2s / 1,637,896 msg/s |
| Dekaf | 2026-08-02T03:16:19.9277651+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1806.2 MB/s | 5/5 | 1,359,790 | 600.2s / 1,626,483 msg/s |
| Dekaf | 2026-08-02T03:16:46.9329749+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1806.2 MB/s | 5/5 | 1,425,765 | 627.2s / 1,601,379 msg/s |
| Dekaf | 2026-08-02T03:17:13.9479273+00:00 | 1 | 12.0 MiB / 2.1 MiB | 1806.2 MB/s | 5/5 | 1,489,041 | 654.2s / 1,583,906 msg/s |
| Dekaf | 2026-08-02T03:17:41.9540503+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1806.2 MB/s | 5/5 | 1,558,001 | 682.2s / 1,595,763 msg/s |
| Dekaf | 2026-08-02T03:18:08.9606492+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1806.2 MB/s | 6/5 | 1,620,782 | 709.2s / 1,620,641 msg/s |
| Dekaf | 2026-08-02T03:18:35.9718404+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1806.2 MB/s | 6/5 | 1,682,029 | 736.2s / 1,618,467 msg/s |
| Dekaf | 2026-08-02T03:19:02.9793351+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1806.2 MB/s | 6/6 | 1,745,873 | 763.2s / 1,609,041 msg/s |
| Dekaf | 2026-08-02T03:19:30.9861212+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1806.2 MB/s | 6/6 | 1,807,061 | 791.2s / 1,577,164 msg/s |
| Dekaf | 2026-08-02T03:19:57.9958015+00:00 | 1 | 13.0 MiB / 8.7 MiB | 1806.2 MB/s | 6/6 | 1,866,323 | 818.2s / 1,631,999 msg/s |
| Dekaf | 2026-08-02T03:20:25.0047304+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1806.2 MB/s | 7/6 | 1,935,022 | 845.2s / 1,671,175 msg/s |
| Dekaf | 2026-08-02T03:20:52.0152214+00:00 | 1 | 11.0 MiB / 10.8 MiB | 1806.2 MB/s | 7/7 | 1,988,306 | 872.3s / 1,629,916 msg/s |
| Dekaf | 2026-08-02T03:51:21.6918712+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 489,483 msg/s |
| Dekaf | 2026-08-02T03:51:48.699971+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1727.6 MB/s | 0/0 | 51,701 | 27.0s / 1,613,420 msg/s |
| Dekaf | 2026-08-02T03:52:15.7108346+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/0 | 104,746 | 54.0s / 1,604,467 msg/s |
| Dekaf | 2026-08-02T03:52:42.728078+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/0 | 164,716 | 81.0s / 1,635,600 msg/s |
| Dekaf | 2026-08-02T03:53:10.7368744+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1768.1 MB/s | 1/1 | 223,459 | 109.0s / 1,583,976 msg/s |
| Dekaf | 2026-08-02T03:53:37.7501531+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1768.1 MB/s | 1/1 | 281,489 | 136.0s / 1,617,799 msg/s |
| Dekaf | 2026-08-02T03:54:04.7573062+00:00 | 1 | 15.0 MiB / 12.2 MiB | 1768.1 MB/s | 1/1 | 340,151 | 163.1s / 1,620,313 msg/s |
| Dekaf | 2026-08-02T03:54:32.7697468+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/2 | 396,274 | 191.1s / 1,623,845 msg/s |
| Dekaf | 2026-08-02T03:54:59.7870101+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1768.1 MB/s | 1/2 | 452,829 | 218.1s / 1,620,647 msg/s |
| Dekaf | 2026-08-02T03:55:26.7968181+00:00 | 1 | 14.0 MiB / 12.4 MiB | 1768.1 MB/s | 1/2 | 512,312 | 245.1s / 1,611,100 msg/s |
| Dekaf | 2026-08-02T03:55:53.8074599+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1768.1 MB/s | 1/2 | 569,199 | 272.1s / 1,631,723 msg/s |
| Dekaf | 2026-08-02T03:56:21.8207605+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1768.1 MB/s | 1/2 | 627,456 | 300.1s / 1,581,338 msg/s |
| Dekaf | 2026-08-02T03:56:48.8261429+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1768.1 MB/s | 1/3 | 685,685 | 327.1s / 1,620,172 msg/s |
| Dekaf | 2026-08-02T03:57:15.829766+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1768.1 MB/s | 1/3 | 745,383 | 354.1s / 1,627,102 msg/s |
| Dekaf | 2026-08-02T03:57:42.8388446+00:00 | 1 | 14.0 MiB / 12.7 MiB | 1768.1 MB/s | 1/3 | 803,774 | 381.1s / 1,619,368 msg/s |
| Dekaf | 2026-08-02T03:58:10.8497519+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/3 | 867,578 | 409.1s / 1,610,260 msg/s |
| Dekaf | 2026-08-02T03:58:37.8593195+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/3 | 924,128 | 436.1s / 1,550,147 msg/s |
| Dekaf | 2026-08-02T03:59:04.8647463+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/3 | 980,905 | 463.1s / 1,523,268 msg/s |
| Dekaf | 2026-08-02T03:59:31.8723746+00:00 | 1 | 14.0 MiB / 13.8 MiB | 1768.1 MB/s | 1/3 | 1,036,450 | 490.1s / 1,589,662 msg/s |
| Dekaf | 2026-08-02T03:59:59.8798133+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1768.1 MB/s | 1/3 | 1,090,658 | 518.2s / 1,575,382 msg/s |
| Dekaf | 2026-08-02T04:00:26.891523+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 1/3 | 1,150,831 | 545.2s / 1,603,524 msg/s |
| Dekaf | 2026-08-02T04:00:53.9079558+00:00 | 1 | 15.0 MiB / 14.4 MiB | 1768.1 MB/s | 2/3 | 1,204,270 | 572.2s / 1,573,995 msg/s |
| Dekaf | 2026-08-02T04:01:20.9147549+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1768.1 MB/s | 2/3 | 1,261,552 | 599.2s / 1,595,941 msg/s |
| Dekaf | 2026-08-02T04:01:48.9222155+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1768.1 MB/s | 3/3 | 1,322,232 | 627.2s / 1,588,626 msg/s |
| Dekaf | 2026-08-02T04:02:15.9279988+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1768.1 MB/s | 4/3 | 1,374,032 | 654.2s / 1,576,446 msg/s |
| Dekaf | 2026-08-02T04:02:42.9377683+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1768.1 MB/s | 4/3 | 1,430,241 | 681.2s / 1,582,331 msg/s |
| Dekaf | 2026-08-02T04:03:09.9520358+00:00 | 1 | 15.0 MiB / 14.3 MiB | 1768.1 MB/s | 5/3 | 1,485,863 | 708.2s / 1,581,924 msg/s |
| Dekaf | 2026-08-02T04:03:37.9646085+00:00 | 1 | 15.0 MiB / 13.7 MiB | 1768.1 MB/s | 5/4 | 1,541,331 | 736.2s / 1,571,567 msg/s |
| Dekaf | 2026-08-02T04:04:04.9720968+00:00 | 1 | 15.0 MiB / 14.2 MiB | 1768.1 MB/s | 5/4 | 1,588,231 | 763.2s / 1,573,005 msg/s |
| Dekaf | 2026-08-02T04:04:31.9809065+00:00 | 1 | 15.0 MiB / 8.9 MiB | 1768.1 MB/s | 5/4 | 1,637,723 | 790.2s / 1,528,996 msg/s |
| Dekaf | 2026-08-02T04:04:59.9896922+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1768.1 MB/s | 6/4 | 1,688,624 | 818.3s / 1,578,009 msg/s |
| Dekaf | 2026-08-02T04:05:27.0002173+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1768.1 MB/s | 6/4 | 1,733,719 | 845.3s / 1,513,897 msg/s |
| Dekaf | 2026-08-02T04:05:54.0148131+00:00 | 1 | 16.0 MiB / 13.3 MiB | 1768.1 MB/s | 6/5 | 1,782,849 | 872.3s / 1,531,232 msg/s |
| Dekaf | 2026-08-02T04:06:21.0264729+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1768.1 MB/s | 6/5 | 1,832,743 | 899.3s / 1,607,925 msg/s |
| Dekaf (3conn) | 2026-08-02T04:06:49.544935+00:00 | 1 | 16.0 MiB / 16.0 MiB | 2128.4 MB/s | 0/0 | 1,931 | 27.0s / 1,510,159 msg/s |
| Dekaf (3conn) | 2026-08-02T04:07:16.5568133+00:00 | 1 | 14.0 MiB / 6.8 MiB | 2326.8 MB/s | 1/0 | 5,588 | 54.0s / 2,057,246 msg/s |
| Dekaf (3conn) | 2026-08-02T04:07:43.5662219+00:00 | 1 | 14.0 MiB / 7.7 MiB | 2667.8 MB/s | 1/0 | 11,106 | 81.0s / 1,533,285 msg/s |
| Dekaf (3conn) | 2026-08-02T04:08:10.5792385+00:00 | 1 | 12.0 MiB / 3.2 MiB | 2667.8 MB/s | 2/0 | 18,808 | 108.1s / 1,709,746 msg/s |
| Dekaf (3conn) | 2026-08-02T04:08:38.594306+00:00 | 1 | 12.0 MiB / 11.9 MiB | 2667.8 MB/s | 2/1 | 26,569 | 136.1s / 1,772,314 msg/s |
| Dekaf (3conn) | 2026-08-02T04:09:05.6013682+00:00 | 1 | 12.0 MiB / 6.7 MiB | 2667.8 MB/s | 2/1 | 33,940 | 163.1s / 1,925,416 msg/s |
| Dekaf (3conn) | 2026-08-02T04:09:32.6122522+00:00 | 1 | 12.0 MiB / 5.1 MiB | 2667.8 MB/s | 2/1 | 42,336 | 190.1s / 1,762,516 msg/s |
| Dekaf (3conn) | 2026-08-02T04:09:59.6235108+00:00 | 1 | 12.0 MiB / 8.1 MiB | 2667.8 MB/s | 2/2 | 50,200 | 217.1s / 2,148,366 msg/s |
| Dekaf (3conn) | 2026-08-02T04:10:27.6322541+00:00 | 1 | 12.0 MiB / 7.4 MiB | 2667.8 MB/s | 2/2 | 58,123 | 245.1s / 1,664,190 msg/s |
| Dekaf (3conn) | 2026-08-02T04:10:54.6438265+00:00 | 1 | 12.0 MiB / 6.6 MiB | 2667.8 MB/s | 2/2 | 64,655 | 272.1s / 1,628,578 msg/s |
| Dekaf (3conn) | 2026-08-02T04:11:21.6618039+00:00 | 1 | 12.0 MiB / 3.0 MiB | 2667.8 MB/s | 2/2 | 73,334 | 299.2s / 2,018,174 msg/s |
| Dekaf (3conn) | 2026-08-02T04:11:48.674986+00:00 | 1 | 12.0 MiB / 5.0 MiB | 2667.8 MB/s | 2/2 | 81,383 | 326.2s / 1,677,768 msg/s |
| Dekaf (3conn) | 2026-08-02T04:12:16.6818815+00:00 | 1 | 12.0 MiB / 8.1 MiB | 2667.8 MB/s | 2/3 | 89,433 | 354.2s / 2,047,655 msg/s |
| Dekaf (3conn) | 2026-08-02T04:12:43.7008503+00:00 | 1 | 12.0 MiB / 7.8 MiB | 2667.8 MB/s | 2/3 | 97,627 | 381.2s / 1,819,352 msg/s |
| Dekaf (3conn) | 2026-08-02T04:13:10.7140607+00:00 | 1 | 12.0 MiB / 7.3 MiB | 2667.8 MB/s | 2/3 | 106,150 | 408.2s / 2,162,253 msg/s |
| Dekaf (3conn) | 2026-08-02T04:13:38.7202034+00:00 | 1 | 12.0 MiB / 3.8 MiB | 2695.5 MB/s | 2/3 | 114,064 | 436.2s / 2,127,667 msg/s |
| Dekaf (3conn) | 2026-08-02T04:14:05.7291264+00:00 | 1 | 12.0 MiB / 6.9 MiB | 2695.5 MB/s | 2/3 | 121,155 | 463.2s / 1,838,171 msg/s |
| Dekaf (3conn) | 2026-08-02T04:14:32.7409856+00:00 | 1 | 12.0 MiB / 7.8 MiB | 2695.5 MB/s | 2/3 | 128,259 | 490.3s / 1,690,763 msg/s |
| Dekaf (3conn) | 2026-08-02T04:14:59.7551523+00:00 | 1 | 12.0 MiB / 9.0 MiB | 2695.5 MB/s | 2/3 | 135,750 | 517.3s / 2,279,264 msg/s |
| Dekaf (3conn) | 2026-08-02T04:15:27.7721752+00:00 | 1 | 12.0 MiB / 12.0 MiB | 2695.5 MB/s | 2/3 | 143,318 | 545.3s / 1,863,474 msg/s |
| Dekaf (3conn) | 2026-08-02T04:15:54.7830042+00:00 | 1 | 12.0 MiB / 3.7 MiB | 2695.5 MB/s | 2/3 | 151,025 | 572.3s / 1,915,769 msg/s |
| Dekaf (3conn) | 2026-08-02T04:16:21.7916064+00:00 | 1 | 13.0 MiB / 1.8 MiB | 2695.5 MB/s | 2/3 | 157,623 | 599.3s / 1,696,742 msg/s |
| Dekaf (3conn) | 2026-08-02T04:16:48.7952992+00:00 | 1 | 12.0 MiB / 5.1 MiB | 2695.5 MB/s | 2/4 | 164,567 | 626.3s / 1,825,904 msg/s |
| Dekaf (3conn) | 2026-08-02T04:17:16.8015233+00:00 | 1 | 12.0 MiB / 6.7 MiB | 2695.5 MB/s | 2/4 | 170,155 | 654.3s / 2,010,577 msg/s |
| Dekaf (3conn) | 2026-08-02T04:17:43.8038468+00:00 | 1 | 12.0 MiB / 2.0 MiB | 2695.5 MB/s | 2/4 | 175,690 | 681.4s / 1,820,940 msg/s |
| Dekaf (3conn) | 2026-08-02T04:18:10.812452+00:00 | 1 | 12.0 MiB / 3.3 MiB | 2695.5 MB/s | 2/4 | 180,633 | 708.4s / 1,720,527 msg/s |
| Dekaf (3conn) | 2026-08-02T04:18:37.8179551+00:00 | 1 | 12.0 MiB / 3.5 MiB | 2695.5 MB/s | 2/4 | 186,148 | 735.4s / 1,659,892 msg/s |
| Dekaf (3conn) | 2026-08-02T04:19:05.8314313+00:00 | 1 | 12.0 MiB / 6.4 MiB | 2695.5 MB/s | 2/4 | 193,819 | 763.4s / 1,850,590 msg/s |
| Dekaf (3conn) | 2026-08-02T04:19:32.8453725+00:00 | 1 | 12.0 MiB / 12.0 MiB | 2695.5 MB/s | 2/4 | 202,474 | 790.4s / 1,904,401 msg/s |
| Dekaf (3conn) | 2026-08-02T04:19:59.8546838+00:00 | 1 | 12.0 MiB / 1.3 MiB | 2695.5 MB/s | 2/4 | 210,138 | 817.4s / 1,739,040 msg/s |
| Dekaf (3conn) | 2026-08-02T04:20:26.8595876+00:00 | 1 | 10.0 MiB / 5.9 MiB | 2695.5 MB/s | 2/4 | 217,555 | 844.4s / 1,668,599 msg/s |
| Dekaf (3conn) | 2026-08-02T04:20:54.8708117+00:00 | 1 | 12.0 MiB / 3.7 MiB | 2695.5 MB/s | 2/5 | 225,663 | 872.4s / 1,887,239 msg/s |
| Dekaf (3conn) | 2026-08-02T04:21:21.8788098+00:00 | 1 | 12.0 MiB / 12.0 MiB | 2695.5 MB/s | 2/5 | 233,588 | 899.4s / 1,877,343 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-02T03:06:49.8330413+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.2 MiB |
| Dekaf | 2026-08-02T03:07:04.8461305+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-08-02T03:07:34.866173+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:07:49.8800944+00:00 | 1 | capacity | succeeded | 15,014ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:08:19.903337+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 9.7 MiB |
| Dekaf | 2026-08-02T03:08:34.9147306+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 9.7 MiB |
| Dekaf | 2026-08-02T03:09:34.984113+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:09:49.9947952+00:00 | 1 | capacity | succeeded | 15,010ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:10:20.0159533+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:10:35.0269951+00:00 | 1 | capacity | failed | 15,011ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:11:35.0658455+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:11:50.0732539+00:00 | 1 | capacity | succeeded | 15,008ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-02T03:12:20.0925676+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-08-02T03:12:35.1033555+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 8.6 MiB |
| Dekaf | 2026-08-02T03:13:35.1532696+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:13:50.1651174+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:14:20.1879339+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:14:35.1975399+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 11.9 MiB |
| Dekaf | 2026-08-02T03:15:35.2412086+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.4 MiB |
| Dekaf | 2026-08-02T03:15:50.256856+00:00 | 1 | capacity | failed | 15,015ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:17:50.3377215+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.6 MiB |
| Dekaf | 2026-08-02T03:18:05.3514321+00:00 | 1 | capacity | succeeded | 15,014ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:18:35.3736267+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T03:18:50.3836479+00:00 | 1 | capacity | failed | 15,010ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:19:50.4221232+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 11.2 MiB |
| Dekaf | 2026-08-02T03:20:05.4482757+00:00 | 1 | capacity | succeeded | 15,026ms | 11.0 MiB / 10.0 MiB |
| Dekaf | 2026-08-02T03:20:35.48321+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 11.0 MiB |
| Dekaf | 2026-08-02T03:20:50.4967683+00:00 | 1 | capacity | failed | 15,014ms | 11.0 MiB / 7.7 MiB |
| Dekaf | 2026-08-02T03:51:51.7839955+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T03:52:06.797439+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-08-02T03:52:36.8212614+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:52:51.8312517+00:00 | 1 | capacity | failed | 15,009ms | 14.0 MiB / 12.0 MiB |
| Dekaf | 2026-08-02T03:53:51.8781303+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-08-02T03:54:06.890881+00:00 | 1 | capacity | failed | 15,012ms | 14.0 MiB / 14.2 MiB |
| Dekaf | 2026-08-02T03:56:06.9978307+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 9.6 MiB |
| Dekaf | 2026-08-02T03:56:22.0089293+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T04:00:22.2042467+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T04:00:37.2185248+00:00 | 1 | capacity | succeeded | 15,014ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:01:07.2491152+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.6 MiB |
| Dekaf | 2026-08-02T04:01:22.2593869+00:00 | 1 | capacity | succeeded | 15,010ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T04:01:52.2829606+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T04:02:07.2949802+00:00 | 1 | capacity | succeeded | 15,012ms | 18.0 MiB / 17.1 MiB |
| Dekaf | 2026-08-02T04:02:37.3191967+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 17.1 MiB |
| Dekaf | 2026-08-02T04:02:52.3315061+00:00 | 1 | capacity | succeeded | 15,012ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:03:22.355199+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-08-02T04:03:37.3678171+00:00 | 1 | capacity | failed | 15,012ms | 15.0 MiB / 12.1 MiB |
| Dekaf | 2026-08-02T04:04:37.4200974+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 13.8 MiB |
| Dekaf | 2026-08-02T04:04:52.4437995+00:00 | 1 | capacity | succeeded | 15,023ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T04:05:22.4769807+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T04:05:37.4918042+00:00 | 1 | capacity | failed | 15,014ms | 16.0 MiB / 17.1 MiB |
| Dekaf | 2026-08-02T04:06:07.5158356+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:06:52.6640267+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-08-02T04:07:07.6903283+00:00 | 1 | capacity | succeeded | 15,026ms | 14.0 MiB / 7.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:07:37.7497699+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:07:52.7770095+00:00 | 1 | capacity | succeeded | 15,027ms | 12.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-08-02T04:08:22.821824+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-08-02T04:08:37.8449201+00:00 | 1 | capacity | failed | 15,022ms | 12.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-08-02T04:09:37.9122997+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-08-02T04:09:52.9373774+00:00 | 1 | capacity | failed | 15,025ms | 12.0 MiB / 6.3 MiB |
| Dekaf (3conn) | 2026-08-02T04:11:53.1270519+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-08-02T04:12:08.1604163+00:00 | 1 | capacity | failed | 15,033ms | 12.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-08-02T04:16:08.5147153+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-08-02T04:16:23.54017+00:00 | 1 | capacity | failed | 15,025ms | 12.0 MiB / 2.6 MiB |
| Dekaf (3conn) | 2026-08-02T04:20:23.8613586+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-08-02T04:20:38.8856535+00:00 | 1 | capacity | failed | 15,024ms | 12.0 MiB / 4.0 MiB |

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 2,598 |
| Dekaf | 1 | 0.002–0.004ms | 3,049 |
| Dekaf | 1 | 0.004–0.008ms | 10,993 |
| Dekaf | 1 | 0.008–0.016ms | 41,529 |
| Dekaf | 1 | 0.016–0.032ms | 59,341 |
| Dekaf | 1 | 0.032–0.064ms | 66,262 |
| Dekaf | 1 | 0.064–0.128ms | 127,162 |
| Dekaf | 1 | 0.128–0.256ms | 331,331 |
| Dekaf | 1 | 0.256–0.512ms | 304,383 |
| Dekaf | 1 | 0.512–1.024ms | 38,093 |
| Dekaf | 1 | 1.024–2.048ms | 4,580 |
| Dekaf | 1 | 2.048–4.096ms | 3,916 |
| Dekaf | 1 | 4.096–8.192ms | 759 |
| Dekaf | 1 | 8.192–16.384ms | 41 |
| Dekaf | 1 | 16.384–32.768ms | 1 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 2,456 |
| Dekaf | 1 | 0.002–0.004ms | 2,970 |
| Dekaf | 1 | 0.004–0.008ms | 11,156 |
| Dekaf | 1 | 0.008–0.016ms | 48,275 |
| Dekaf | 1 | 0.016–0.032ms | 70,749 |
| Dekaf | 1 | 0.032–0.064ms | 61,536 |
| Dekaf | 1 | 0.064–0.128ms | 115,515 |
| Dekaf | 1 | 0.128–0.256ms | 269,903 |
| Dekaf | 1 | 0.256–0.512ms | 256,729 |
| Dekaf | 1 | 0.512–1.024ms | 40,580 |
| Dekaf | 1 | 1.024–2.048ms | 5,036 |
| Dekaf | 1 | 2.048–4.096ms | 3,640 |
| Dekaf | 1 | 4.096–8.192ms | 662 |
| Dekaf | 1 | 8.192–16.384ms | 37 |
| Dekaf | 1 | 16.384–32.768ms | 1 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 130 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 137 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 372 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 948 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 3,329 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 7,978 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 7,233 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 12,546 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 13,959 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 11,520 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 6,439 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 1,749 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 411 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 41 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 1,147,977,000 | 2026-08-02T03:35:13.4110646+00:00 | 102.5ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,978,000 | 2026-08-02T03:35:13.4112339+00:00 | 102.4ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,980,000 | 2026-08-02T03:35:13.4123781+00:00 | 102.0ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,981,000 | 2026-08-02T03:35:13.4129349+00:00 | 101.9ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,983,000 | 2026-08-02T03:35:13.414254+00:00 | 106.2ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,987,000 | 2026-08-02T03:35:13.416632+00:00 | 104.3ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,988,000 | 2026-08-02T03:35:13.4176317+00:00 | 103.3ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,989,000 | 2026-08-02T03:35:13.4187287+00:00 | 102.1ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,990,000 | 2026-08-02T03:35:13.4197021+00:00 | 101.6ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,991,000 | 2026-08-02T03:35:13.420484+00:00 | 101.2ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,993,000 | 2026-08-02T03:35:13.4216848+00:00 | 100.3ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,147,997,000 | 2026-08-02T03:35:13.424177+00:00 | 100.3ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,148,000,000 | 2026-08-02T03:35:13.4259309+00:00 | 100.5ms | GC pause | - | - | 833.6s / 1,423,471 msg/s | Gen2 +0 / pause +99.6ms |
| Confluent | 1,211,967,000 | 2026-08-02T03:50:50.3679205+00:00 | 112.0ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,968,000 | 2026-08-02T03:50:50.3680249+00:00 | 112.1ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,971,000 | 2026-08-02T03:50:50.3760562+00:00 | 114.2ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,977,000 | 2026-08-02T03:50:50.3879228+00:00 | 118.8ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,978,000 | 2026-08-02T03:50:50.3884982+00:00 | 118.3ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,981,000 | 2026-08-02T03:50:50.4017682+00:00 | 115.0ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,984,000 | 2026-08-02T03:50:50.4038958+00:00 | 102.4ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,985,000 | 2026-08-02T03:50:50.404431+00:00 | 102.1ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,986,000 | 2026-08-02T03:50:50.4049622+00:00 | 101.6ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,987,000 | 2026-08-02T03:50:50.4054647+00:00 | 112.3ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,988,000 | 2026-08-02T03:50:50.4059675+00:00 | 111.8ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,989,000 | 2026-08-02T03:50:50.4064719+00:00 | 100.2ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,991,000 | 2026-08-02T03:50:50.410857+00:00 | 107.0ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,994,000 | 2026-08-02T03:50:50.4123315+00:00 | 104.2ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,997,000 | 2026-08-02T03:50:50.4197554+00:00 | 111.3ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,211,998,000 | 2026-08-02T03:50:50.42041+00:00 | 110.7ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |
| Confluent | 1,212,001,000 | 2026-08-02T03:50:50.4303295+00:00 | 100.9ms | GC pause | - | - | 869.5s / 1,154,348 msg/s | Gen2 +0 / pause +140.7ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.38x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.15x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.05 | 1068.79 | 1,222,139 | 1,231,894 | +0.5% | +0.10% | 1165.52 | 1,222,139 | 0 | 1.29 |
| Dekaf | 1.14 | 1157.27 | 1,118,170 | 1,132,715 | +2.5% | +0.27% | 1066.37 | 1,118,170 | 0 | 1.27 |
| Confluent | 1.81 | - | 855,813 | 858,516 | +4.8% | +0.43% | 816.17 | 855,813 | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 327,490 | 363.87 | 1008.66 KB |
| Dekaf | 2 | 325,371 | 361.52 | 1002.32 KB |
| Dekaf | 3 | 337,732 | 375.25 | 1018.37 KB |
| Dekaf (3conn) | 1 | 354,169 | 393.51 | 1006.44 KB |
| Dekaf (3conn) | 2 | 360,000 | 399.99 | 1006.85 KB |
| Dekaf (3conn) | 3 | 370,431 | 411.58 | 1010.97 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:27.9008089+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 304,242 msg/s |
| Dekaf | 2026-08-02T03:06:45.9067925+00:00 | 3 | 16.0 MiB / 15.2 MiB | 368.9 MB/s | 0/0 | 6,332 | 18.0s / 1,067,749 msg/s |
| Dekaf | 2026-08-02T03:07:04.9151476+00:00 | 1 | 16.0 MiB / 7.4 MiB | 383.0 MB/s | 0/0 | 9,074 | 37.0s / 1,152,573 msg/s |
| Dekaf | 2026-08-02T03:07:22.9218805+00:00 | 1 | 16.0 MiB / 13.9 MiB | 397.8 MB/s | 0/1 | 11,022 | 55.0s / 1,099,562 msg/s |
| Dekaf | 2026-08-02T03:07:40.9286872+00:00 | 2 | 16.0 MiB / 8.2 MiB | 406.6 MB/s | 0/1 | 5,619 | 73.1s / 1,092,898 msg/s |
| Dekaf | 2026-08-02T03:07:58.9396427+00:00 | 2 | 16.0 MiB / 6.8 MiB | 406.6 MB/s | 0/2 | 5,811 | 91.1s / 1,131,631 msg/s |
| Dekaf | 2026-08-02T03:08:16.9456894+00:00 | 3 | 14.0 MiB / 14.0 MiB | 420.5 MB/s | 0/2 | 48,111 | 109.1s / 1,184,172 msg/s |
| Dekaf | 2026-08-02T03:08:34.9507584+00:00 | 3 | 12.0 MiB / 9.8 MiB | 421.3 MB/s | 1/2 | 56,552 | 127.1s / 1,082,940 msg/s |
| Dekaf | 2026-08-02T03:08:53.9546032+00:00 | 1 | 12.0 MiB / 5.4 MiB | 411.8 MB/s | 2/2 | 31,607 | 146.1s / 1,124,002 msg/s |
| Dekaf | 2026-08-02T03:09:11.9648356+00:00 | 1 | 12.0 MiB / 3.2 MiB | 421.7 MB/s | 2/2 | 32,852 | 164.1s / 1,089,999 msg/s |
| Dekaf | 2026-08-02T03:09:29.9719141+00:00 | 2 | 10.0 MiB / 7.6 MiB | 407.8 MB/s | 3/2 | 12,076 | 182.1s / 1,180,315 msg/s |
| Dekaf | 2026-08-02T03:09:47.9778454+00:00 | 2 | 8.0 MiB / 7.9 MiB | 407.8 MB/s | 4/2 | 14,150 | 200.2s / 1,160,402 msg/s |
| Dekaf | 2026-08-02T03:10:05.9876862+00:00 | 3 | 12.0 MiB / 11.4 MiB | 427.5 MB/s | 2/4 | 92,980 | 218.2s / 1,135,028 msg/s |
| Dekaf | 2026-08-02T03:10:23.9906591+00:00 | 3 | 12.0 MiB / 1.6 MiB | 429.9 MB/s | 2/5 | 98,667 | 236.2s / 1,123,813 msg/s |
| Dekaf | 2026-08-02T03:10:42.997786+00:00 | 1 | 8.0 MiB / 6.6 MiB | 421.7 MB/s | 4/3 | 50,895 | 255.2s / 1,153,228 msg/s |
| Dekaf | 2026-08-02T03:11:01.0037505+00:00 | 1 | 8.0 MiB / 6.0 MiB | 421.7 MB/s | 4/3 | 53,891 | 273.2s / 1,114,731 msg/s |
| Dekaf | 2026-08-02T03:11:19.0166435+00:00 | 2 | 9.0 MiB / 2.6 MiB | 415.9 MB/s | 5/3 | 23,765 | 291.2s / 1,135,597 msg/s |
| Dekaf | 2026-08-02T03:11:37.0235203+00:00 | 2 | 9.0 MiB / 3.7 MiB | 415.9 MB/s | 5/4 | 25,193 | 309.2s / 1,112,075 msg/s |
| Dekaf | 2026-08-02T03:11:55.0301411+00:00 | 3 | 12.0 MiB / 6.4 MiB | 429.9 MB/s | 2/7 | 123,620 | 327.2s / 1,122,257 msg/s |
| Dekaf | 2026-08-02T03:12:13.0385245+00:00 | 3 | 10.0 MiB / 5.7 MiB | 429.9 MB/s | 2/7 | 128,739 | 345.2s / 1,130,196 msg/s |
| Dekaf | 2026-08-02T03:12:32.0486864+00:00 | 1 | 8.0 MiB / 3.2 MiB | 421.7 MB/s | 4/4 | 74,583 | 364.2s / 1,163,683 msg/s |
| Dekaf | 2026-08-02T03:12:50.0577432+00:00 | 1 | 8.0 MiB / 3.4 MiB | 421.7 MB/s | 4/4 | 77,871 | 382.2s / 1,148,997 msg/s |
| Dekaf | 2026-08-02T03:13:08.0643143+00:00 | 2 | 10.0 MiB / 5.9 MiB | 415.9 MB/s | 6/4 | 30,802 | 400.2s / 1,142,242 msg/s |
| Dekaf | 2026-08-02T03:13:26.0688715+00:00 | 2 | 8.0 MiB / 3.8 MiB | 415.9 MB/s | 7/4 | 32,174 | 418.2s / 1,146,382 msg/s |
| Dekaf | 2026-08-02T03:13:44.0777178+00:00 | 3 | 7.0 MiB / 4.2 MiB | 429.9 MB/s | 5/8 | 186,424 | 436.2s / 1,177,713 msg/s |
| Dekaf | 2026-08-02T03:14:02.0841011+00:00 | 3 | 6.0 MiB / 5.3 MiB | 429.9 MB/s | 5/8 | 199,197 | 454.2s / 1,132,202 msg/s |
| Dekaf | 2026-08-02T03:14:21.088105+00:00 | 1 | 8.0 MiB / 3.3 MiB | 421.7 MB/s | 4/5 | 92,684 | 473.2s / 1,162,693 msg/s |
| Dekaf | 2026-08-02T03:14:39.0978956+00:00 | 1 | 8.0 MiB / 6.5 MiB | 421.7 MB/s | 4/5 | 95,602 | 491.3s / 1,152,135 msg/s |
| Dekaf | 2026-08-02T03:14:57.103585+00:00 | 2 | 9.0 MiB / 6.7 MiB | 415.9 MB/s | 8/5 | 39,887 | 509.3s / 1,142,101 msg/s |
| Dekaf | 2026-08-02T03:15:15.1110316+00:00 | 2 | 9.0 MiB / 1.6 MiB | 415.9 MB/s | 8/5 | 40,631 | 527.3s / 1,024,848 msg/s |
| Dekaf | 2026-08-02T03:15:33.1205105+00:00 | 3 | 5.0 MiB / 5.0 MiB | 429.9 MB/s | 7/10 | 276,221 | 545.3s / 1,122,306 msg/s |
| Dekaf | 2026-08-02T03:15:51.1215777+00:00 | 3 | 5.0 MiB / 4.6 MiB | 429.9 MB/s | 7/10 | 294,318 | 563.3s / 1,126,187 msg/s |
| Dekaf | 2026-08-02T03:16:10.1283791+00:00 | 1 | 8.0 MiB / 7.9 MiB | 421.7 MB/s | 4/5 | 110,927 | 582.3s / 1,072,366 msg/s |
| Dekaf | 2026-08-02T03:16:28.1353785+00:00 | 1 | 8.0 MiB / 5.2 MiB | 421.7 MB/s | 4/5 | 113,993 | 600.3s / 1,134,771 msg/s |
| Dekaf | 2026-08-02T03:16:46.1363927+00:00 | 2 | 10.0 MiB / 3.5 MiB | 415.9 MB/s | 9/6 | 46,130 | 618.3s / 1,145,961 msg/s |
| Dekaf | 2026-08-02T03:17:04.1422574+00:00 | 2 | 10.0 MiB / 3.3 MiB | 415.9 MB/s | 9/6 | 47,737 | 636.3s / 1,161,689 msg/s |
| Dekaf | 2026-08-02T03:17:22.1464049+00:00 | 3 | 6.0 MiB / 5.0 MiB | 429.9 MB/s | 8/11 | 383,045 | 654.3s / 1,156,354 msg/s |
| Dekaf | 2026-08-02T03:17:40.1487707+00:00 | 3 | 5.0 MiB / 2.8 MiB | 429.9 MB/s | 8/11 | 399,166 | 672.3s / 1,117,202 msg/s |
| Dekaf | 2026-08-02T03:17:59.1536833+00:00 | 1 | 8.0 MiB / 7.1 MiB | 421.7 MB/s | 4/6 | 135,849 | 691.3s / 1,135,252 msg/s |
| Dekaf | 2026-08-02T03:18:17.1585929+00:00 | 1 | 8.0 MiB / 6.8 MiB | 421.7 MB/s | 4/6 | 143,636 | 709.3s / 1,090,652 msg/s |
| Dekaf | 2026-08-02T03:18:35.1604365+00:00 | 2 | 11.0 MiB / 3.6 MiB | 415.9 MB/s | 10/7 | 52,219 | 727.3s / 1,182,478 msg/s |
| Dekaf | 2026-08-02T03:18:53.165718+00:00 | 2 | 11.0 MiB / 7.1 MiB | 415.9 MB/s | 10/7 | 54,288 | 745.3s / 1,171,735 msg/s |
| Dekaf | 2026-08-02T03:19:11.1709118+00:00 | 3 | 6.0 MiB / 6.0 MiB | 429.9 MB/s | 9/12 | 475,872 | 763.3s / 1,150,055 msg/s |
| Dekaf | 2026-08-02T03:19:29.1755556+00:00 | 3 | 5.0 MiB / 5.0 MiB | 429.9 MB/s | 9/13 | 493,577 | 781.3s / 1,073,275 msg/s |
| Dekaf | 2026-08-02T03:19:48.1823192+00:00 | 1 | 9.0 MiB / 4.7 MiB | 421.7 MB/s | 7/6 | 162,404 | 800.4s / 1,178,290 msg/s |
| Dekaf | 2026-08-02T03:20:06.1832769+00:00 | 1 | 9.0 MiB / 5.2 MiB | 421.7 MB/s | 7/7 | 164,851 | 818.4s / 1,105,520 msg/s |
| Dekaf | 2026-08-02T03:20:24.1896648+00:00 | 2 | 11.0 MiB / 4.8 MiB | 415.9 MB/s | 10/9 | 57,983 | 836.4s / 1,122,010 msg/s |
| Dekaf | 2026-08-02T03:20:42.1976842+00:00 | 2 | 12.0 MiB / 10.8 MiB | 415.9 MB/s | 11/9 | 58,349 | 854.4s / 1,119,847 msg/s |
| Dekaf | 2026-08-02T03:21:00.2078399+00:00 | 3 | 5.0 MiB / 4.2 MiB | 429.9 MB/s | 9/14 | 587,455 | 872.4s / 1,154,052 msg/s |
| Dekaf | 2026-08-02T03:21:18.2123134+00:00 | 3 | 5.0 MiB / 4.2 MiB | 429.9 MB/s | 9/14 | 604,847 | 890.4s / 1,080,488 msg/s |
| Dekaf (3conn) | 2026-08-02T03:36:50.7034744+00:00 | 3 | 16.0 MiB / 4.3 MiB | 362.1 MB/s | 0/0 | 2,864 | 9.0s / 816,088 msg/s |
| Dekaf (3conn) | 2026-08-02T03:37:08.7123766+00:00 | 3 | 16.0 MiB / 9.7 MiB | 438.3 MB/s | 0/0 | 7,061 | 27.0s / 1,147,556 msg/s |
| Dekaf (3conn) | 2026-08-02T03:37:27.7157467+00:00 | 1 | 16.0 MiB / 9.6 MiB | 440.3 MB/s | 0/1 | 6,110 | 46.0s / 1,201,383 msg/s |
| Dekaf (3conn) | 2026-08-02T03:37:45.7262052+00:00 | 1 | 16.0 MiB / 6.2 MiB | 445.0 MB/s | 0/1 | 7,972 | 64.0s / 1,200,929 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:03.7303745+00:00 | 2 | 16.0 MiB / 14.4 MiB | 463.2 MB/s | 0/1 | 11,729 | 82.0s / 1,277,108 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:21.7404969+00:00 | 2 | 16.0 MiB / 11.4 MiB | 463.2 MB/s | 0/1 | 14,674 | 100.1s / 1,236,855 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:39.7488867+00:00 | 3 | 16.0 MiB / 10.7 MiB | 478.0 MB/s | 0/2 | 25,171 | 118.1s / 1,231,595 msg/s |
| Dekaf (3conn) | 2026-08-02T03:38:57.7571332+00:00 | 3 | 16.0 MiB / 15.4 MiB | 478.0 MB/s | 0/2 | 28,735 | 136.1s / 1,276,815 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:16.7660325+00:00 | 1 | 14.0 MiB / 12.4 MiB | 457.3 MB/s | 1/3 | 19,046 | 155.1s / 1,280,808 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:34.7718889+00:00 | 1 | 15.0 MiB / 8.7 MiB | 457.3 MB/s | 1/3 | 20,927 | 173.1s / 1,221,806 msg/s |
| Dekaf (3conn) | 2026-08-02T03:39:52.7813018+00:00 | 2 | 12.0 MiB / 10.7 MiB | 471.7 MB/s | 2/2 | 32,619 | 191.1s / 1,319,850 msg/s |
| Dekaf (3conn) | 2026-08-02T03:40:10.7980882+00:00 | 2 | 12.0 MiB / 7.6 MiB | 472.7 MB/s | 2/3 | 36,239 | 209.1s / 1,299,255 msg/s |
| Dekaf (3conn) | 2026-08-02T03:40:28.8059064+00:00 | 3 | 12.0 MiB / 10.5 MiB | 479.5 MB/s | 2/3 | 50,313 | 227.1s / 1,217,745 msg/s |
| Dekaf (3conn) | 2026-08-02T03:40:46.8239981+00:00 | 3 | 12.0 MiB / 10.4 MiB | 479.5 MB/s | 2/3 | 54,215 | 245.2s / 1,199,754 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:05.8315588+00:00 | 1 | 14.0 MiB / 7.3 MiB | 457.4 MB/s | 1/4 | 28,091 | 264.2s / 1,284,047 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:23.8478218+00:00 | 1 | 14.0 MiB / 6.6 MiB | 457.4 MB/s | 1/4 | 29,932 | 282.2s / 1,287,610 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:41.8541414+00:00 | 2 | 10.0 MiB / 8.1 MiB | 472.7 MB/s | 3/4 | 54,566 | 300.2s / 1,202,085 msg/s |
| Dekaf (3conn) | 2026-08-02T03:41:59.8588422+00:00 | 2 | 10.0 MiB / 6.7 MiB | 472.7 MB/s | 3/5 | 58,667 | 318.2s / 1,273,845 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:17.8711007+00:00 | 3 | 11.0 MiB / 9.2 MiB | 479.5 MB/s | 4/4 | 79,185 | 336.2s / 1,230,984 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:35.8783242+00:00 | 3 | 11.0 MiB / 2.7 MiB | 479.5 MB/s | 4/4 | 82,515 | 354.2s / 1,297,527 msg/s |
| Dekaf (3conn) | 2026-08-02T03:42:54.8859592+00:00 | 1 | 8.0 MiB / 6.6 MiB | 457.4 MB/s | 4/5 | 46,175 | 373.2s / 1,203,599 msg/s |
| Dekaf (3conn) | 2026-08-02T03:43:12.8946978+00:00 | 1 | 8.0 MiB / 4.4 MiB | 457.4 MB/s | 4/5 | 49,921 | 391.2s / 1,269,623 msg/s |
| Dekaf (3conn) | 2026-08-02T03:43:30.9025022+00:00 | 2 | 10.0 MiB / 7.7 MiB | 472.7 MB/s | 3/6 | 73,288 | 409.2s / 1,217,354 msg/s |
| Dekaf (3conn) | 2026-08-02T03:43:48.9093014+00:00 | 2 | 8.0 MiB / 4.5 MiB | 472.7 MB/s | 3/6 | 75,068 | 427.2s / 1,281,115 msg/s |
| Dekaf (3conn) | 2026-08-02T03:44:06.9192809+00:00 | 3 | 7.0 MiB / 1.0 MiB | 479.5 MB/s | 6/5 | 111,633 | 445.2s / 1,305,921 msg/s |
| Dekaf (3conn) | 2026-08-02T03:44:24.9275013+00:00 | 3 | 7.0 MiB / 7.0 MiB | 480.9 MB/s | 6/6 | 117,337 | 463.2s / 1,220,247 msg/s |
| Dekaf (3conn) | 2026-08-02T03:44:43.9314641+00:00 | 1 | 8.0 MiB / 3.9 MiB | 457.4 MB/s | 4/7 | 65,953 | 482.3s / 1,217,747 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:01.9387923+00:00 | 1 | 8.0 MiB / 2.5 MiB | 457.4 MB/s | 4/8 | 69,226 | 500.3s / 1,241,112 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:19.9543543+00:00 | 2 | 8.0 MiB / 3.2 MiB | 472.7 MB/s | 4/7 | 91,785 | 518.3s / 1,248,852 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:37.9599008+00:00 | 2 | 8.0 MiB / 3.6 MiB | 472.7 MB/s | 4/8 | 95,399 | 536.3s / 1,222,835 msg/s |
| Dekaf (3conn) | 2026-08-02T03:45:55.9758436+00:00 | 3 | 7.0 MiB / 7.0 MiB | 480.9 MB/s | 6/6 | 149,034 | 554.3s / 1,269,284 msg/s |
| Dekaf (3conn) | 2026-08-02T03:46:13.9882664+00:00 | 3 | 7.0 MiB / 5.8 MiB | 480.9 MB/s | 6/6 | 155,542 | 572.3s / 1,233,003 msg/s |
| Dekaf (3conn) | 2026-08-02T03:46:32.9919654+00:00 | 1 | 8.0 MiB / 3.3 MiB | 457.4 MB/s | 4/9 | 83,683 | 591.3s / 1,220,500 msg/s |
| Dekaf (3conn) | 2026-08-02T03:46:51.0024643+00:00 | 1 | 8.0 MiB / 3.4 MiB | 457.4 MB/s | 4/9 | 87,056 | 609.3s / 1,202,025 msg/s |
| Dekaf (3conn) | 2026-08-02T03:47:09.0077192+00:00 | 2 | 10.0 MiB / 4.7 MiB | 472.7 MB/s | 6/8 | 110,392 | 627.3s / 1,252,084 msg/s |
| Dekaf (3conn) | 2026-08-02T03:47:27.0180655+00:00 | 2 | 11.0 MiB / 4.8 MiB | 472.7 MB/s | 6/8 | 112,630 | 645.4s / 1,201,220 msg/s |
| Dekaf (3conn) | 2026-08-02T03:47:45.0275696+00:00 | 3 | 8.0 MiB / 2.2 MiB | 480.9 MB/s | 7/7 | 186,942 | 663.4s / 1,210,123 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:03.0311097+00:00 | 3 | 9.0 MiB / 4.4 MiB | 480.9 MB/s | 8/7 | 191,893 | 681.4s / 1,214,969 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:22.0447189+00:00 | 1 | 8.0 MiB / 5.2 MiB | 457.4 MB/s | 4/9 | 106,610 | 700.4s / 1,264,373 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:40.0539086+00:00 | 1 | 8.0 MiB / 4.9 MiB | 457.4 MB/s | 4/9 | 110,225 | 718.4s / 1,204,108 msg/s |
| Dekaf (3conn) | 2026-08-02T03:48:58.0613505+00:00 | 2 | 8.0 MiB / 3.2 MiB | 472.7 MB/s | 7/10 | 131,443 | 736.4s / 1,132,427 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:16.0695634+00:00 | 2 | 7.0 MiB / 6.1 MiB | 472.7 MB/s | 7/10 | 136,627 | 754.4s / 1,240,196 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:34.0726967+00:00 | 3 | 9.0 MiB / 3.2 MiB | 480.9 MB/s | 8/8 | 210,530 | 772.4s / 1,221,518 msg/s |
| Dekaf (3conn) | 2026-08-02T03:49:52.0784746+00:00 | 3 | 9.0 MiB / 4.5 MiB | 480.9 MB/s | 8/8 | 214,380 | 790.4s / 1,266,155 msg/s |
| Dekaf (3conn) | 2026-08-02T03:50:11.0816358+00:00 | 1 | 9.0 MiB / 4.5 MiB | 457.4 MB/s | 5/9 | 127,454 | 809.4s / 1,223,253 msg/s |
| Dekaf (3conn) | 2026-08-02T03:50:29.0887384+00:00 | 1 | 9.0 MiB / 8.7 MiB | 457.4 MB/s | 5/9 | 129,362 | 827.4s / 1,253,096 msg/s |
| Dekaf (3conn) | 2026-08-02T03:50:47.0984586+00:00 | 2 | 9.0 MiB / 2.8 MiB | 472.7 MB/s | 10/10 | 152,979 | 845.4s / 1,246,927 msg/s |
| Dekaf (3conn) | 2026-08-02T03:51:05.1065447+00:00 | 2 | 9.0 MiB / 7.2 MiB | 472.7 MB/s | 10/11 | 154,506 | 863.5s / 1,240,563 msg/s |
| Dekaf (3conn) | 2026-08-02T03:51:23.1126504+00:00 | 3 | 8.0 MiB / 4.4 MiB | 480.9 MB/s | 11/9 | 234,924 | 881.5s / 1,182,288 msg/s |
| Dekaf (3conn) | 2026-08-02T03:51:41.1173793+00:00 | 3 | 8.0 MiB / 3.0 MiB | 480.9 MB/s | 11/9 | 238,391 | 899.5s / 1,211,513 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-08-02T03:06:58.0922764+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 9.4 MiB |
| Dekaf | 2026-08-02T03:06:58.152682+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 2.1 MiB |
| Dekaf | 2026-08-02T03:07:13.2142408+00:00 | 2 | capacity | failed | 15,061ms | 16.0 MiB / 12.0 MiB |
| Dekaf | 2026-08-02T03:07:43.3029499+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-08-02T03:07:44.7975553+00:00 | 3 | capacity | failed | 1,504ms | 16.0 MiB / 16.9 MiB |
| Dekaf | 2026-08-02T03:08:01.3666344+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 2.5 MiB |
| Dekaf | 2026-08-02T03:08:16.4141919+00:00 | 1 | capacity | succeeded | 15,047ms | 12.0 MiB / 6.4 MiB |
| Dekaf | 2026-08-02T03:08:26.4478379+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-02T03:08:34.4723498+00:00 | 1 | capacity | failed | 15,051ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-08-02T03:08:44.5159683+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 10.6 MiB |
| Dekaf | 2026-08-02T03:08:59.5709352+00:00 | 2 | capacity | succeeded | 15,054ms | 12.0 MiB / 3.9 MiB |
| Dekaf | 2026-08-02T03:09:17.6712597+00:00 | 2 | capacity | succeeded | 15,086ms | 10.0 MiB / 7.6 MiB |
| Dekaf | 2026-08-02T03:09:19.5872099+00:00 | 3 | capacity | failed | 1,502ms | 14.0 MiB / 13.2 MiB |
| Dekaf | 2026-08-02T03:09:20.6876871+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:09:37.7270703+00:00 | 1 | capacity | succeeded | 15,060ms | 8.0 MiB / 5.4 MiB |
| Dekaf | 2026-08-02T03:09:40.7394819+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:09:53.8035568+00:00 | 2 | capacity | failed | 15,050ms | 8.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-02T03:10:07.7677223+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 10.6 MiB |
| Dekaf | 2026-08-02T03:10:23.8899361+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.0 MiB |
| Dekaf | 2026-08-02T03:10:52.8890104+00:00 | 3 | capacity | started | 0ms | 13.0 MiB / 5.2 MiB |
| Dekaf | 2026-08-02T03:11:09.0219526+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 3.8 MiB |
| Dekaf | 2026-08-02T03:11:24.0708093+00:00 | 2 | capacity | failed | 15,048ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-08-02T03:11:39.5235538+00:00 | 3 | capacity | failed | 15,042ms | 12.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:12:24.69732+00:00 | 3 | capacity | succeeded | 15,078ms | 10.0 MiB / 7.6 MiB |
| Dekaf | 2026-08-02T03:12:39.3262493+00:00 | 2 | capacity | succeeded | 15,043ms | 10.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:12:45.74874+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-08-02T03:13:09.4000741+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.4 MiB |
| Dekaf | 2026-08-02T03:13:18.8893597+00:00 | 3 | capacity | failed | 15,075ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:13:26.5030338+00:00 | 1 | capacity | failed | 15,038ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:14:04.0041479+00:00 | 3 | capacity | succeeded | 15,037ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:14:09.5778373+00:00 | 2 | capacity | succeeded | 15,040ms | 9.0 MiB / 5.0 MiB |
| Dekaf | 2026-08-02T03:14:39.7146705+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 0.8 MiB |
| Dekaf | 2026-08-02T03:15:07.2308809+00:00 | 3 | capacity | succeeded | 15,051ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:15:24.8508367+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.6 MiB |
| Dekaf | 2026-08-02T03:15:39.8961808+00:00 | 2 | capacity | succeeded | 15,045ms | 10.0 MiB / 4.6 MiB |
| Dekaf | 2026-08-02T03:16:10.4009758+00:00 | 3 | capacity | failed | 15,046ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:16:40.4971596+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:16:55.5513022+00:00 | 3 | capacity | succeeded | 15,054ms | 6.0 MiB / 3.2 MiB |
| Dekaf | 2026-08-02T03:17:27.3245679+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf | 2026-08-02T03:17:40.7282377+00:00 | 3 | capacity | failed | 15,041ms | 6.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:17:55.3487365+00:00 | 2 | capacity | failed | 15,052ms | 11.0 MiB / 3.1 MiB |
| Dekaf | 2026-08-02T03:18:30.5077368+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:18:45.5495663+00:00 | 1 | capacity | succeeded | 15,041ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-08-02T03:18:55.9309147+00:00 | 3 | capacity | succeeded | 15,041ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:19:13.9824747+00:00 | 3 | capacity | failed | 15,043ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-08-02T03:19:30.7370987+00:00 | 1 | capacity | succeeded | 15,056ms | 9.0 MiB / 8.1 MiB |
| Dekaf | 2026-08-02T03:19:55.7547494+00:00 | 2 | capacity | failed | 15,039ms | 11.0 MiB / 7.4 MiB |
| Dekaf | 2026-08-02T03:20:14.2131556+00:00 | 3 | capacity | started | 0ms | 4.0 MiB / 4.1 MiB |
| Dekaf | 2026-08-02T03:20:29.2495745+00:00 | 3 | capacity | failed | 15,036ms | 5.0 MiB / 2.2 MiB |
| Dekaf | 2026-08-02T03:20:40.9064391+00:00 | 2 | capacity | succeeded | 15,043ms | 12.0 MiB / 3.4 MiB |
| Dekaf | 2026-08-02T03:21:07.0187835+00:00 | 1 | capacity | failed | 15,043ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-08-02T03:21:24.0464639+00:00 | 2 | capacity | failed | 13,038ms | 12.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:11.9075525+00:00 | 3 | capacity | started | 0ms | 14.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:26.9609929+00:00 | 3 | capacity | failed | 15,053ms | 16.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:37:57.1564072+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:27.2172091+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:28.8168236+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 8.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:43.8761588+00:00 | 1 | capacity | succeeded | 15,059ms | 14.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:38:46.8966738+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:01.9377248+00:00 | 1 | capacity | failed | 15,041ms | 14.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:13.9278931+00:00 | 3 | capacity | succeeded | 15,061ms | 14.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:18.3937879+00:00 | 2 | capacity | failed | 15,048ms | 12.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:35.0088857+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 6.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:39:48.5196245+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:40:03.5770987+00:00 | 2 | capacity | failed | 15,057ms | 12.0 MiB / 8.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:40:50.3394221+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:08.3995008+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:23.4511467+00:00 | 3 | capacity | failed | 15,051ms | 10.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:36.9630252+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:52.0040003+00:00 | 2 | capacity | failed | 15,041ms | 10.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:41:53.6290523+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:42:11.7333779+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:42:29.7897754+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 6.0 MiB |
| Dekaf (3conn) | 2026-08-02T03:42:44.8442049+00:00 | 1 | capacity | failed | 15,054ms | 8.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:42:56.7995775+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:43:11.8593716+00:00 | 3 | capacity | succeeded | 15,060ms | 7.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:43:14.9658802+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:43:37.411142+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-08-02T03:43:55.4595897+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:44:00.1268621+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:44:15.2254418+00:00 | 1 | capacity | failed | 15,098ms | 8.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:00.3787538+00:00 | 1 | capacity | failed | 15,055ms | 8.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:25.8358868+00:00 | 2 | capacity | failed | 15,059ms | 8.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:45:55.9307176+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-08-02T03:46:15.5065965+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 3.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:46:41.0937126+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 3.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:47:15.745558+00:00 | 3 | capacity | failed | 15,096ms | 8.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-08-02T03:47:27.8253668+00:00 | 2 | capacity | failed | 1,503ms | 10.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-08-02T03:47:57.9355411+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:48:15.9977167+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-08-02T03:48:31.0351+00:00 | 2 | capacity | failed | 15,037ms | 8.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:49:01.2002649+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-08-02T03:49:34.3354133+00:00 | 2 | capacity | succeeded | 15,055ms | 8.0 MiB / 6.3 MiB |
| Dekaf (3conn) | 2026-08-02T03:49:46.5308289+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:50:01.5789368+00:00 | 1 | capacity | succeeded | 15,048ms | 9.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-08-02T03:50:19.4450491+00:00 | 3 | capacity | succeeded | 15,056ms | 7.0 MiB / 6.2 MiB |
| Dekaf (3conn) | 2026-08-02T03:50:22.4576232+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:50:37.5275824+00:00 | 3 | capacity | succeeded | 15,069ms | 8.0 MiB / 5.1 MiB |
| Dekaf (3conn) | 2026-08-02T03:51:04.6656412+00:00 | 2 | capacity | failed | 15,064ms | 9.0 MiB / 2.3 MiB |
| Dekaf (3conn) | 2026-08-02T03:51:22.6499852+00:00 | 3 | capacity | failed | 15,036ms | 8.0 MiB / 2.1 MiB |
*132 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf (3conn) | 1 | 0.001–0.002ms | 33 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 27 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 103 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 367 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 848 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 1,182 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 1,485 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 1,753 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 3,217 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 5,028 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 5,969 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 4,664 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 1,740 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 607 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 266 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 32 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 18 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 26 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 91 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 362 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 965 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 1,305 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 1,717 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 2,157 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 3,802 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 5,779 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 6,826 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 5,255 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 2,115 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 649 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 287 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 35 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 39 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 59 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 170 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 568 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 1,456 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 2,091 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 2,870 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 3,439 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 6,182 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 9,182 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 10,453 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 7,914 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 2,921 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 858 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 379 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 46 |
| Dekaf | 1 | 0.001–0.002ms | 41 |
| Dekaf | 1 | 0.002–0.004ms | 39 |
| Dekaf | 1 | 0.004–0.008ms | 160 |
| Dekaf | 1 | 0.008–0.016ms | 609 |
| Dekaf | 1 | 0.016–0.032ms | 1,346 |
| Dekaf | 1 | 0.032–0.064ms | 1,616 |
| Dekaf | 1 | 0.064–0.128ms | 2,095 |
| Dekaf | 1 | 0.128–0.256ms | 3,652 |
| Dekaf | 1 | 0.256–0.512ms | 7,017 |
| Dekaf | 1 | 0.512–1.024ms | 10,318 |
| Dekaf | 1 | 1.024–2.048ms | 9,622 |
| Dekaf | 1 | 2.048–4.096ms | 5,530 |
| Dekaf | 1 | 4.096–8.192ms | 2,068 |
| Dekaf | 1 | 8.192–16.384ms | 380 |
| Dekaf | 1 | 16.384–32.768ms | 33 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 2 | 0.001–0.002ms | 12 |
| Dekaf | 2 | 0.002–0.004ms | 16 |
| Dekaf | 2 | 0.004–0.008ms | 89 |
| Dekaf | 2 | 0.008–0.016ms | 265 |
| Dekaf | 2 | 0.016–0.032ms | 566 |
| Dekaf | 2 | 0.032–0.064ms | 671 |
| Dekaf | 2 | 0.064–0.128ms | 829 |
| Dekaf | 2 | 0.128–0.256ms | 1,426 |
| Dekaf | 2 | 0.256–0.512ms | 2,747 |
| Dekaf | 2 | 0.512–1.024ms | 3,672 |
| Dekaf | 2 | 1.024–2.048ms | 3,088 |
| Dekaf | 2 | 2.048–4.096ms | 1,639 |
| Dekaf | 2 | 4.096–8.192ms | 530 |
| Dekaf | 2 | 8.192–16.384ms | 92 |
| Dekaf | 2 | 16.384–32.768ms | 18 |
| Dekaf | 2 | 32.768–65.536ms | 3 |
| Dekaf | 3 | 0.001–0.002ms | 94 |
| Dekaf | 3 | 0.002–0.004ms | 107 |
| Dekaf | 3 | 0.004–0.008ms | 443 |
| Dekaf | 3 | 0.008–0.016ms | 1,454 |
| Dekaf | 3 | 0.016–0.032ms | 3,422 |
| Dekaf | 3 | 0.032–0.064ms | 5,013 |
| Dekaf | 3 | 0.064–0.128ms | 6,685 |
| Dekaf | 3 | 0.128–0.256ms | 12,332 |
| Dekaf | 3 | 0.256–0.512ms | 25,519 |
| Dekaf | 3 | 0.512–1.024ms | 38,845 |
| Dekaf | 3 | 1.024–2.048ms | 33,274 |
| Dekaf | 3 | 2.048–4.096ms | 15,787 |
| Dekaf | 3 | 4.096–8.192ms | 4,856 |
| Dekaf | 3 | 8.192–16.384ms | 777 |
| Dekaf | 3 | 16.384–32.768ms | 61 |
| Dekaf | 3 | 32.768–65.536ms | 4 |
| Dekaf | 3 | 65.536–131.072ms | 1 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Dekaf | 17,000 | 2026-08-02T03:06:28.0101789+00:00 | 132.9ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 34,000 | 2026-08-02T03:06:28.0322052+00:00 | 101.2ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 44,000 | 2026-08-02T03:06:28.0448879+00:00 | 136.8ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 56,000 | 2026-08-02T03:06:28.0816942+00:00 | 118.1ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 116,000 | 2026-08-02T03:06:28.2610912+00:00 | 142.2ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 137,000 | 2026-08-02T03:06:28.3197457+00:00 | 327.3ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 146,000 | 2026-08-02T03:06:28.4063332+00:00 | 100.5ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 157,000 | 2026-08-02T03:06:28.4481212+00:00 | 261.7ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 186,000 | 2026-08-02T03:06:28.5341796+00:00 | 138.2ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 194,000 | 2026-08-02T03:06:28.558471+00:00 | 139.9ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 197,000 | 2026-08-02T03:06:28.593207+00:00 | 233.4ms | GC pause | - | - | 1.0s / 304,242 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 257,000 | 2026-08-02T03:06:28.7914481+00:00 | 259.5ms | GC pause | - | - | 2.0s / 513,286 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 277,000 | 2026-08-02T03:06:28.8427541+00:00 | 288.9ms | GC pause | - | - | 2.0s / 513,286 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 289,000 | 2026-08-02T03:06:28.8802704+00:00 | 103.0ms | GC pause | - | - | 2.0s / 513,286 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 299,000 | 2026-08-02T03:06:28.9084022+00:00 | 131.6ms | GC pause | - | - | 2.0s / 513,286 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 303,000 | 2026-08-02T03:06:28.9170747+00:00 | 122.9ms | GC pause | - | - | 2.0s / 513,286 msg/s | Gen2 +1 / pause +9.5ms |
| Dekaf | 317,000 | 2026-08-02T03:06:29.0174979+00:00 | 158.7ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 327,000 | 2026-08-02T03:06:29.0635343+00:00 | 128.6ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 377,000 | 2026-08-02T03:06:29.1924126+00:00 | 122.9ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 487,000 | 2026-08-02T03:06:29.3612137+00:00 | 115.8ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 547,000 | 2026-08-02T03:06:29.4422582+00:00 | 158.4ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 591,000 | 2026-08-02T03:06:29.5418699+00:00 | 110.4ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 597,000 | 2026-08-02T03:06:29.5485957+00:00 | 149.0ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 599,000 | 2026-08-02T03:06:29.5507305+00:00 | 151.0ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 601,000 | 2026-08-02T03:06:29.5531473+00:00 | 104.8ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 603,000 | 2026-08-02T03:06:29.5546656+00:00 | 153.5ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 619,000 | 2026-08-02T03:06:29.6137176+00:00 | 122.1ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 629,000 | 2026-08-02T03:06:29.6282686+00:00 | 118.2ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 633,000 | 2026-08-02T03:06:29.6414225+00:00 | 112.7ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 689,000 | 2026-08-02T03:06:29.7385225+00:00 | 115.9ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 703,000 | 2026-08-02T03:06:29.7577402+00:00 | 121.3ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 709,000 | 2026-08-02T03:06:29.7654904+00:00 | 122.3ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 713,000 | 2026-08-02T03:06:29.7724042+00:00 | 115.4ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 719,000 | 2026-08-02T03:06:29.781861+00:00 | 118.9ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 729,000 | 2026-08-02T03:06:29.8000984+00:00 | 110.7ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 739,000 | 2026-08-02T03:06:29.8182604+00:00 | 106.2ms | throughput collapse | - | - | 2.0s / 513,286 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 778,000 | 2026-08-02T03:06:29.8850628+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 788,000 | 2026-08-02T03:06:29.8994572+00:00 | 178.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 798,000 | 2026-08-02T03:06:29.9120173+00:00 | 176.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 805,000 | 2026-08-02T03:06:29.921231+00:00 | 178.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 808,000 | 2026-08-02T03:06:29.9246677+00:00 | 175.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 815,000 | 2026-08-02T03:06:29.9389862+00:00 | 168.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 817,000 | 2026-08-02T03:06:29.9400408+00:00 | 153.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 826,000 | 2026-08-02T03:06:29.9470577+00:00 | 127.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 828,000 | 2026-08-02T03:06:29.9532355+00:00 | 195.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 835,000 | 2026-08-02T03:06:29.970949+00:00 | 194.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 836,000 | 2026-08-02T03:06:29.971383+00:00 | 118.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 846,000 | 2026-08-02T03:06:30.001869+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 847,000 | 2026-08-02T03:06:30.0089749+00:00 | 159.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 856,000 | 2026-08-02T03:06:30.0334701+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 905,000 | 2026-08-02T03:06:30.1578498+00:00 | 105.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 915,000 | 2026-08-02T03:06:30.1783308+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 918,000 | 2026-08-02T03:06:30.1834589+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 928,000 | 2026-08-02T03:06:30.2012459+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 965,000 | 2026-08-02T03:06:30.2520262+00:00 | 128.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 998,000 | 2026-08-02T03:06:30.3106324+00:00 | 114.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,018,000 | 2026-08-02T03:06:30.3382+00:00 | 115.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,035,000 | 2026-08-02T03:06:30.3787137+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,135,000 | 2026-08-02T03:06:30.5100521+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,148,000 | 2026-08-02T03:06:30.525125+00:00 | 105.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,185,000 | 2026-08-02T03:06:30.5703363+00:00 | 142.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,188,000 | 2026-08-02T03:06:30.5797573+00:00 | 142.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,195,000 | 2026-08-02T03:06:30.587222+00:00 | 142.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,225,000 | 2026-08-02T03:06:30.6490791+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,345,000 | 2026-08-02T03:06:30.8199116+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 610,587 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,445,000 | 2026-08-02T03:06:30.9558965+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,468,000 | 2026-08-02T03:06:30.9767049+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,475,000 | 2026-08-02T03:06:30.9872346+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,478,000 | 2026-08-02T03:06:30.9889415+00:00 | 108.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,505,000 | 2026-08-02T03:06:31.0263235+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,508,000 | 2026-08-02T03:06:31.0315466+00:00 | 114.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,515,000 | 2026-08-02T03:06:31.0475422+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,518,000 | 2026-08-02T03:06:31.052831+00:00 | 103.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,869,000 | 2026-08-02T03:06:31.5202133+00:00 | 141.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,887,000 | 2026-08-02T03:06:31.5308976+00:00 | 140.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,889,000 | 2026-08-02T03:06:31.5320678+00:00 | 168.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,903,000 | 2026-08-02T03:06:31.5429415+00:00 | 176.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,912,000 | 2026-08-02T03:06:31.5561461+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,915,000 | 2026-08-02T03:06:31.5590898+00:00 | 108.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,917,000 | 2026-08-02T03:06:31.5624995+00:00 | 157.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,922,000 | 2026-08-02T03:06:31.5672883+00:00 | 114.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,924,000 | 2026-08-02T03:06:31.569408+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,925,000 | 2026-08-02T03:06:31.5700637+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,930,000 | 2026-08-02T03:06:31.5759905+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,943,000 | 2026-08-02T03:06:31.6761326+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,949,000 | 2026-08-02T03:06:31.6820013+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,989,000 | 2026-08-02T03:06:31.7399786+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 1,993,000 | 2026-08-02T03:06:31.744423+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 706,040 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,123,000 | 2026-08-02T03:06:31.9194439+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,209,000 | 2026-08-02T03:06:32.0522487+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,263,000 | 2026-08-02T03:06:32.1374388+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,293,000 | 2026-08-02T03:06:32.17631+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,378,000 | 2026-08-02T03:06:32.3013007+00:00 | 124.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,398,000 | 2026-08-02T03:06:32.3306784+00:00 | 111.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,408,000 | 2026-08-02T03:06:32.3429415+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,418,000 | 2026-08-02T03:06:32.3550181+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,468,000 | 2026-08-02T03:06:32.4377979+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,548,000 | 2026-08-02T03:06:32.5524169+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,565,000 | 2026-08-02T03:06:32.5743565+00:00 | 113.1ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 732,697 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,958,000 | 2026-08-02T03:06:33.0379877+00:00 | 124.5ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,975,000 | 2026-08-02T03:06:33.0623424+00:00 | 128.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,978,000 | 2026-08-02T03:06:33.0659272+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,008,000 | 2026-08-02T03:06:33.1084158+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,408,000 | 2026-08-02T03:06:33.5890384+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,415,000 | 2026-08-02T03:06:33.5949201+00:00 | 114.4ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,438,000 | 2026-08-02T03:06:33.6324059+00:00 | 126.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,518,000 | 2026-08-02T03:06:33.7765306+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 771,142 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,708,000 | 2026-08-02T03:06:34.0164602+00:00 | 111.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,728,000 | 2026-08-02T03:06:34.036061+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,733,000 | 2026-08-02T03:06:34.0395742+00:00 | 114.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,749,000 | 2026-08-02T03:06:34.0627391+00:00 | 133.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,929,000 | 2026-08-02T03:06:34.3026102+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,975,000 | 2026-08-02T03:06:34.3676846+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 3,985,000 | 2026-08-02T03:06:34.3758242+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,108,000 | 2026-08-02T03:06:34.5461096+00:00 | 118.1ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,118,000 | 2026-08-02T03:06:34.5538072+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,145,000 | 2026-08-02T03:06:34.5811628+00:00 | 140.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,158,000 | 2026-08-02T03:06:34.6110971+00:00 | 128.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 802,540 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,495,000 | 2026-08-02T03:06:34.9974251+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,515,000 | 2026-08-02T03:06:35.029169+00:00 | 151.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,518,000 | 2026-08-02T03:06:35.0338624+00:00 | 147.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,575,000 | 2026-08-02T03:06:35.1184839+00:00 | 211.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,578,000 | 2026-08-02T03:06:35.1261994+00:00 | 204.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,588,000 | 2026-08-02T03:06:35.176014+00:00 | 180.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,615,000 | 2026-08-02T03:06:35.2621136+00:00 | 122.3ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,628,000 | 2026-08-02T03:06:35.2903911+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 4,868,000 | 2026-08-02T03:06:35.5634023+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 769,036 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,313,000 | 2026-08-02T03:06:36.0586806+00:00 | 115.0ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,353,000 | 2026-08-02T03:06:36.1018662+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,413,000 | 2026-08-02T03:06:36.2297768+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,449,000 | 2026-08-02T03:06:36.2660994+00:00 | 178.4ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,463,000 | 2026-08-02T03:06:36.3034657+00:00 | 173.2ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,469,000 | 2026-08-02T03:06:36.3109926+00:00 | 191.9ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,483,000 | 2026-08-02T03:06:36.3438869+00:00 | 173.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,503,000 | 2026-08-02T03:06:36.4352434+00:00 | 110.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,523,000 | 2026-08-02T03:06:36.4611286+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 5,583,000 | 2026-08-02T03:06:36.5623333+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 690,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,479,000 | 2026-08-02T03:06:37.5529839+00:00 | 115.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 910,345 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,489,000 | 2026-08-02T03:06:37.5625577+00:00 | 133.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 910,345 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,499,000 | 2026-08-02T03:06:37.5724951+00:00 | 131.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 910,345 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,529,000 | 2026-08-02T03:06:37.6246426+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 910,345 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,929,000 | 2026-08-02T03:06:38.0739451+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,943,000 | 2026-08-02T03:06:38.083883+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,949,000 | 2026-08-02T03:06:38.0884525+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,953,000 | 2026-08-02T03:06:38.0953063+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,957,000 | 2026-08-02T03:06:38.0976146+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,959,000 | 2026-08-02T03:06:38.099561+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 6,963,000 | 2026-08-02T03:06:38.1031522+00:00 | 119.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,047,000 | 2026-08-02T03:06:38.231631+00:00 | 109.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,137,000 | 2026-08-02T03:06:38.3549154+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,147,000 | 2026-08-02T03:06:38.3654049+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 868,917 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,788,000 | 2026-08-02T03:06:39.039346+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,817,000 | 2026-08-02T03:06:39.0913803+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,828,000 | 2026-08-02T03:06:39.1261114+00:00 | 127.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 7,838,000 | 2026-08-02T03:06:39.1427131+00:00 | 122.1ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,308,000 | 2026-08-02T03:06:39.6801423+00:00 | 122.8ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,358,000 | 2026-08-02T03:06:39.775274+00:00 | 105.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,368,000 | 2026-08-02T03:06:39.7833903+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 790,782 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,405,000 | 2026-08-02T03:06:39.8236817+00:00 | 142.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 918,343 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,565,000 | 2026-08-02T03:06:41.0890338+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,589,000 | 2026-08-02T03:06:41.1360932+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,613,000 | 2026-08-02T03:06:41.1602246+00:00 | 114.8ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,619,000 | 2026-08-02T03:06:41.1662966+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,629,000 | 2026-08-02T03:06:41.1766088+00:00 | 107.1ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,633,000 | 2026-08-02T03:06:41.1867224+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 9,999,000 | 2026-08-02T03:06:41.6001386+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 14.0s / 891,000 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,513,000 | 2026-08-02T03:06:43.1054244+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 944,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,965,000 | 2026-08-02T03:06:43.5764738+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 944,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,968,000 | 2026-08-02T03:06:43.5807921+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 944,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 11,988,000 | 2026-08-02T03:06:43.6007342+00:00 | 111.5ms | broker/backlog (no scale or GC event) | - | - | 16.0s / 944,863 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,965,000 | 2026-08-02T03:06:44.584148+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,968,000 | 2026-08-02T03:06:44.5878235+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,975,000 | 2026-08-02T03:06:44.5926344+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,989,000 | 2026-08-02T03:06:44.6034138+00:00 | 107.5ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,993,000 | 2026-08-02T03:06:44.6078491+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 12,995,000 | 2026-08-02T03:06:44.6149783+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 13,023,000 | 2026-08-02T03:06:44.6563351+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 995,255 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 14,547,000 | 2026-08-02T03:06:46.1013351+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,035,943 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,015,000 | 2026-08-02T03:06:47.5358126+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,025,000 | 2026-08-02T03:06:47.5442881+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,029,000 | 2026-08-02T03:06:47.5471071+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,033,000 | 2026-08-02T03:06:47.5557141+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,043,000 | 2026-08-02T03:06:47.5666034+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,045,000 | 2026-08-02T03:06:47.568915+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,063,000 | 2026-08-02T03:06:47.5835394+00:00 | 130.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,069,000 | 2026-08-02T03:06:47.5912172+00:00 | 162.6ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,085,000 | 2026-08-02T03:06:47.6151402+00:00 | 130.9ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,093,000 | 2026-08-02T03:06:47.6528418+00:00 | 131.1ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,095,000 | 2026-08-02T03:06:47.6550613+00:00 | 102.5ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,103,000 | 2026-08-02T03:06:47.6634632+00:00 | 129.3ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,119,000 | 2026-08-02T03:06:47.7048508+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 20.0s / 955,781 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,539,000 | 2026-08-02T03:06:48.1082159+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,010,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 16,549,000 | 2026-08-02T03:06:48.1187647+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,010,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,039,000 | 2026-08-02T03:06:48.6115989+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,010,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 17,043,000 | 2026-08-02T03:06:48.6161203+00:00 | 109.7ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,010,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,653,000 | 2026-08-02T03:06:51.0927897+00:00 | 102.7ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 1,062,390 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,699,000 | 2026-08-02T03:06:51.1556972+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 1,062,390 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,293,000 | 2026-08-02T03:06:52.6209468+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 25.0s / 1,065,377 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,899,000 | 2026-08-02T03:06:56.0554485+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,913,000 | 2026-08-02T03:06:56.0655923+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,929,000 | 2026-08-02T03:06:56.081771+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,933,000 | 2026-08-02T03:06:56.0873692+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 24,939,000 | 2026-08-02T03:06:56.1012371+00:00 | 119.3ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,413,000 | 2026-08-02T03:06:56.578206+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,433,000 | 2026-08-02T03:06:56.593185+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,439,000 | 2026-08-02T03:06:56.6021679+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,443,000 | 2026-08-02T03:06:56.6081927+00:00 | 105.2ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 999,568 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 40,049,000 | 2026-08-02T03:07:10.1147691+00:00 | 101.0ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed, 2:capacity/failed | - | 43.0s / 1,024,710 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,123,000 | 2026-08-02T03:07:12.1251519+00:00 | 107.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed, 2:capacity/failed | - | 45.0s / 1,067,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,129,000 | 2026-08-02T03:07:12.1292713+00:00 | 106.1ms | broker/backlog (no scale or GC event) | 1:capacity/failed, 3:capacity/failed, 2:capacity/failed | - | 45.0s / 1,067,728 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,829,000 | 2026-08-02T03:07:14.5643604+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,075,471 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,833,000 | 2026-08-02T03:07:14.5667275+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,075,471 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,859,000 | 2026-08-02T03:07:14.587729+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,075,471 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 44,869,000 | 2026-08-02T03:07:14.6017326+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,075,471 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 45,969,000 | 2026-08-02T03:07:15.5904092+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 1,110,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 45,973,000 | 2026-08-02T03:07:15.5926625+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 1,110,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 45,979,000 | 2026-08-02T03:07:15.6058707+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 48.0s / 1,110,201 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 47,113,000 | 2026-08-02T03:07:16.6551312+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 49.0s / 1,065,238 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 49,683,000 | 2026-08-02T03:07:19.0521294+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,079,993 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 49,689,000 | 2026-08-02T03:07:19.0599924+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,079,993 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 49,693,000 | 2026-08-02T03:07:19.062635+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 52.0s / 1,079,993 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 54,138,000 | 2026-08-02T03:07:23.1047933+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 1,020,225 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 54,155,000 | 2026-08-02T03:07:23.1209623+00:00 | 112.6ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 1,020,225 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 54,158,000 | 2026-08-02T03:07:23.122351+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 1,020,225 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 54,168,000 | 2026-08-02T03:07:23.1372431+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 56.0s / 1,020,225 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 55,693,000 | 2026-08-02T03:07:24.6119466+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,069,311 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,753,000 | 2026-08-02T03:07:25.5606971+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,768,000 | 2026-08-02T03:07:25.5704385+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,769,000 | 2026-08-02T03:07:25.5709533+00:00 | 123.3ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,790,000 | 2026-08-02T03:07:25.5985471+00:00 | 101.8ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,798,000 | 2026-08-02T03:07:25.6087538+00:00 | 143.3ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,799,000 | 2026-08-02T03:07:25.6092257+00:00 | 126.4ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,805,000 | 2026-08-02T03:07:25.6448289+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 56,815,000 | 2026-08-02T03:07:25.6569771+00:00 | 109.4ms | broker/backlog (no scale or GC event) | - | - | 58.0s / 1,045,391 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 57,879,000 | 2026-08-02T03:07:26.5959395+00:00 | 102.3ms | broker/backlog (no scale or GC event) | - | - | 59.0s / 1,176,007 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 64,499,000 | 2026-08-02T03:07:32.5814603+00:00 | 101.7ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,131,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 64,509,000 | 2026-08-02T03:07:32.5899597+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,131,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 64,519,000 | 2026-08-02T03:07:32.5997175+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,131,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 64,533,000 | 2026-08-02T03:07:32.6178017+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 65.1s / 1,131,666 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 77,429,000 | 2026-08-02T03:07:44.0945731+00:00 | 100.9ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/succeeded | - | 77.1s / 1,132,037 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 77,433,000 | 2026-08-02T03:07:44.099927+00:00 | 108.7ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/failed, 1:capacity/succeeded | - | 77.1s / 1,132,037 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 82,559,000 | 2026-08-02T03:07:48.5965259+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 81.1s / 1,049,043 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 84,349,000 | 2026-08-02T03:07:50.3372201+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 83.1s / 971,711 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 84,359,000 | 2026-08-02T03:07:50.3461947+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 83.1s / 971,711 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 84,363,000 | 2026-08-02T03:07:50.3536033+00:00 | 107.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 83.1s / 971,711 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf | 85,089,000 | 2026-08-02T03:07:51.1083314+00:00 | 100.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 84.1s / 1,038,721 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,675,000 | 2026-08-02T03:07:52.5730844+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 85.1s / 1,060,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,678,000 | 2026-08-02T03:07:52.5760161+00:00 | 100.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 85.1s / 1,060,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 86,685,000 | 2026-08-02T03:07:52.5841699+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 85.1s / 1,060,885 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 88,809,000 | 2026-08-02T03:07:54.5942883+00:00 | 125.2ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 87.1s / 1,019,839 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 88,823,000 | 2026-08-02T03:07:54.6092247+00:00 | 119.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 87.1s / 1,019,839 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 89,289,000 | 2026-08-02T03:07:55.0975392+00:00 | 102.4ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 1:capacity/succeeded | - | 88.1s / 1,038,480 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 91,449,000 | 2026-08-02T03:07:57.1083483+00:00 | 103.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 90.1s / 1,130,717 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 94,839,000 | 2026-08-02T03:08:00.1123561+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 93.1s / 1,133,787 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf | 100,977,000 | 2026-08-02T03:08:05.581165+00:00 | 103.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 98.1s / 1,105,113 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 126,723,000 | 2026-08-02T03:08:28.5989893+00:00 | 106.0ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 1:capacity/failed, 2:capacity/succeeded | - | 121.1s / 1,082,551 msg/s | Gen2 +0 / pause +0.0ms |
| Confluent | 156,349,000 | 2026-08-02T03:24:39.054603+00:00 | 104.1ms | GC pause | - | - | 191.1s / 703,181 msg/s | Gen2 +0 / pause +163.3ms |
| Dekaf (3conn) | 55,000 | 2026-08-02T03:36:41.8299505+00:00 | 110.5ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 58,000 | 2026-08-02T03:36:41.8332341+00:00 | 107.2ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 62,000 | 2026-08-02T03:36:41.8382684+00:00 | 108.5ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 71,000 | 2026-08-02T03:36:41.8499618+00:00 | 153.3ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 72,000 | 2026-08-02T03:36:41.8517905+00:00 | 162.8ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 75,000 | 2026-08-02T03:36:41.855313+00:00 | 114.2ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 81,000 | 2026-08-02T03:36:41.8631899+00:00 | 167.7ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 85,000 | 2026-08-02T03:36:41.8848512+00:00 | 121.2ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 88,000 | 2026-08-02T03:36:41.8900601+00:00 | 116.0ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 98,000 | 2026-08-02T03:36:41.9140292+00:00 | 121.4ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 193,000 | 2026-08-02T03:36:42.1056476+00:00 | 129.3ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 229,000 | 2026-08-02T03:36:42.1683515+00:00 | 143.0ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 233,000 | 2026-08-02T03:36:42.173024+00:00 | 150.7ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 239,000 | 2026-08-02T03:36:42.189203+00:00 | 146.3ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 249,000 | 2026-08-02T03:36:42.2151635+00:00 | 132.3ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 263,000 | 2026-08-02T03:36:42.2572809+00:00 | 145.5ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 273,000 | 2026-08-02T03:36:42.276261+00:00 | 135.2ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 289,000 | 2026-08-02T03:36:42.3140241+00:00 | 133.0ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 293,000 | 2026-08-02T03:36:42.3232671+00:00 | 135.3ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 303,000 | 2026-08-02T03:36:42.3434738+00:00 | 129.0ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 319,000 | 2026-08-02T03:36:42.383735+00:00 | 111.6ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 333,000 | 2026-08-02T03:36:42.4248545+00:00 | 124.4ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 339,000 | 2026-08-02T03:36:42.4318225+00:00 | 126.4ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 363,000 | 2026-08-02T03:36:42.4747356+00:00 | 107.2ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 379,000 | 2026-08-02T03:36:42.5015404+00:00 | 104.2ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 383,000 | 2026-08-02T03:36:42.5077571+00:00 | 125.0ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 409,000 | 2026-08-02T03:36:42.5699205+00:00 | 133.3ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 413,000 | 2026-08-02T03:36:42.575088+00:00 | 139.9ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 433,000 | 2026-08-02T03:36:42.5986953+00:00 | 152.9ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 436,000 | 2026-08-02T03:36:42.6043795+00:00 | 114.9ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 437,000 | 2026-08-02T03:36:42.6054592+00:00 | 115.6ms | GC pause | - | - | 1.0s / 482,227 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 439,000 | 2026-08-02T03:36:42.6249874+00:00 | 133.6ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 443,000 | 2026-08-02T03:36:42.6357418+00:00 | 126.7ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 446,000 | 2026-08-02T03:36:42.6394645+00:00 | 100.1ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 449,000 | 2026-08-02T03:36:42.6483409+00:00 | 124.7ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 453,000 | 2026-08-02T03:36:42.6562739+00:00 | 132.4ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 473,000 | 2026-08-02T03:36:42.7174207+00:00 | 104.0ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 477,000 | 2026-08-02T03:36:42.724526+00:00 | 104.1ms | GC pause | - | - | 2.0s / 615,362 msg/s | Gen2 +1 / pause +11.2ms |
| Dekaf (3conn) | 503,000 | 2026-08-02T03:36:42.7677546+00:00 | 107.8ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 519,000 | 2026-08-02T03:36:42.800461+00:00 | 127.1ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 523,000 | 2026-08-02T03:36:42.8125696+00:00 | 115.0ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 529,000 | 2026-08-02T03:36:42.8225829+00:00 | 117.7ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 539,000 | 2026-08-02T03:36:42.8450405+00:00 | 116.9ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 553,000 | 2026-08-02T03:36:42.8739464+00:00 | 107.9ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 559,000 | 2026-08-02T03:36:42.8835613+00:00 | 120.8ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 573,000 | 2026-08-02T03:36:42.9320059+00:00 | 107.5ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 583,000 | 2026-08-02T03:36:42.9538989+00:00 | 103.8ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 587,000 | 2026-08-02T03:36:42.960455+00:00 | 103.6ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 589,000 | 2026-08-02T03:36:42.9645306+00:00 | 119.7ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 590,000 | 2026-08-02T03:36:42.9660281+00:00 | 100.0ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 593,000 | 2026-08-02T03:36:42.971758+00:00 | 112.5ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 599,000 | 2026-08-02T03:36:42.9851616+00:00 | 111.2ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 603,000 | 2026-08-02T03:36:42.9930818+00:00 | 109.3ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 619,000 | 2026-08-02T03:36:43.0253617+00:00 | 107.8ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 702,000 | 2026-08-02T03:36:43.180648+00:00 | 119.5ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 711,000 | 2026-08-02T03:36:43.1944376+00:00 | 112.6ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 712,000 | 2026-08-02T03:36:43.1952456+00:00 | 117.7ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 721,000 | 2026-08-02T03:36:43.2131375+00:00 | 106.0ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 722,000 | 2026-08-02T03:36:43.2142616+00:00 | 104.9ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 731,000 | 2026-08-02T03:36:43.2378487+00:00 | 100.7ms | throughput collapse | - | - | 2.0s / 615,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,027,000 | 2026-08-02T03:36:43.6224048+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,047,000 | 2026-08-02T03:36:43.64425+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,087,000 | 2026-08-02T03:36:43.6815054+00:00 | 117.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,147,000 | 2026-08-02T03:36:43.7895063+00:00 | 102.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,157,000 | 2026-08-02T03:36:43.7954228+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,207,000 | 2026-08-02T03:36:43.8592663+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,397,000 | 2026-08-02T03:36:44.0667124+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,407,000 | 2026-08-02T03:36:44.0781651+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,457,000 | 2026-08-02T03:36:44.1420532+00:00 | 114.8ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,469,000 | 2026-08-02T03:36:44.1555388+00:00 | 116.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,477,000 | 2026-08-02T03:36:44.1639168+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,487,000 | 2026-08-02T03:36:44.1830123+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,493,000 | 2026-08-02T03:36:44.2002212+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,513,000 | 2026-08-02T03:36:44.2500802+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,529,000 | 2026-08-02T03:36:44.2751602+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,533,000 | 2026-08-02T03:36:44.2775736+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,543,000 | 2026-08-02T03:36:44.2919369+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,671,000 | 2026-08-02T03:36:44.4566783+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,672,000 | 2026-08-02T03:36:44.4571346+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,761,000 | 2026-08-02T03:36:44.5890082+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,762,000 | 2026-08-02T03:36:44.5900147+00:00 | 121.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,763,000 | 2026-08-02T03:36:44.5909336+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,771,000 | 2026-08-02T03:36:44.608542+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 758,487 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,044,000 | 2026-08-02T03:36:44.9712953+00:00 | 104.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,046,000 | 2026-08-02T03:36:44.972108+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,054,000 | 2026-08-02T03:36:44.9802096+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,064,000 | 2026-08-02T03:36:44.9894068+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,067,000 | 2026-08-02T03:36:44.9933973+00:00 | 119.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,077,000 | 2026-08-02T03:36:45.0077807+00:00 | 142.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,087,000 | 2026-08-02T03:36:45.047012+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,117,000 | 2026-08-02T03:36:45.0968944+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,167,000 | 2026-08-02T03:36:45.1675613+00:00 | 117.2ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,171,000 | 2026-08-02T03:36:45.1692077+00:00 | 110.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,391,000 | 2026-08-02T03:36:45.4777885+00:00 | 120.5ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,401,000 | 2026-08-02T03:36:45.4870067+00:00 | 119.0ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,402,000 | 2026-08-02T03:36:45.4875961+00:00 | 128.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,411,000 | 2026-08-02T03:36:45.5029604+00:00 | 119.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,421,000 | 2026-08-02T03:36:45.5138149+00:00 | 121.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,422,000 | 2026-08-02T03:36:45.5144184+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,432,000 | 2026-08-02T03:36:45.5305772+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,441,000 | 2026-08-02T03:36:45.5409522+00:00 | 112.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,442,000 | 2026-08-02T03:36:45.5412985+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,461,000 | 2026-08-02T03:36:45.6057189+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,471,000 | 2026-08-02T03:36:45.617451+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,472,000 | 2026-08-02T03:36:45.6179234+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 4.0s / 680,703 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,481,000 | 2026-08-02T03:36:45.6294934+00:00 | 114.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,482,000 | 2026-08-02T03:36:45.6300876+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,487,000 | 2026-08-02T03:36:45.6335455+00:00 | 127.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,491,000 | 2026-08-02T03:36:45.6386363+00:00 | 108.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,492,000 | 2026-08-02T03:36:45.6396258+00:00 | 113.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,501,000 | 2026-08-02T03:36:45.6469822+00:00 | 110.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,507,000 | 2026-08-02T03:36:45.6568974+00:00 | 143.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,511,000 | 2026-08-02T03:36:45.6700438+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,517,000 | 2026-08-02T03:36:45.6846219+00:00 | 125.8ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,522,000 | 2026-08-02T03:36:45.6952625+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,527,000 | 2026-08-02T03:36:45.6998015+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,867,000 | 2026-08-02T03:36:46.0911882+00:00 | 122.2ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,877,000 | 2026-08-02T03:36:46.1071344+00:00 | 110.7ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 2,927,000 | 2026-08-02T03:36:46.1591114+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 5.0s / 923,478 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,387,000 | 2026-08-02T03:36:46.6325987+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 3,397,000 | 2026-08-02T03:36:46.6419314+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,169,000 | 2026-08-02T03:36:47.4618606+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,179,000 | 2026-08-02T03:36:47.4766666+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,193,000 | 2026-08-02T03:36:47.4883438+00:00 | 122.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,213,000 | 2026-08-02T03:36:47.5211439+00:00 | 107.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,253,000 | 2026-08-02T03:36:47.5949378+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 6.0s / 892,934 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,729,000 | 2026-08-02T03:36:48.1409368+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 4,969,000 | 2026-08-02T03:36:48.4157838+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,113,000 | 2026-08-02T03:36:48.5874949+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,123,000 | 2026-08-02T03:36:48.5986072+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,133,000 | 2026-08-02T03:36:48.6098437+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,139,000 | 2026-08-02T03:36:48.6134329+00:00 | 114.7ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,143,000 | 2026-08-02T03:36:48.6194658+00:00 | 113.9ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,149,000 | 2026-08-02T03:36:48.6318506+00:00 | 105.5ms | broker/backlog (no scale or GC event) | - | - | 7.0s / 868,628 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,173,000 | 2026-08-02T03:36:48.6667625+00:00 | 105.0ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,571,000 | 2026-08-02T03:36:49.0940309+00:00 | 170.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,572,000 | 2026-08-02T03:36:49.0945779+00:00 | 170.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,585,000 | 2026-08-02T03:36:49.12794+00:00 | 126.8ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,595,000 | 2026-08-02T03:36:49.1419408+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,601,000 | 2026-08-02T03:36:49.152092+00:00 | 138.6ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,927,000 | 2026-08-02T03:36:49.592086+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,937,000 | 2026-08-02T03:36:49.5997285+00:00 | 110.9ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 796,626 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 5,967,000 | 2026-08-02T03:36:49.6421548+00:00 | 122.8ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 816,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,413,000 | 2026-08-02T03:36:50.1722126+00:00 | 100.6ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 816,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,751,000 | 2026-08-02T03:36:50.594589+00:00 | 121.1ms | broker/backlog (no scale or GC event) | - | - | 9.0s / 816,088 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,791,000 | 2026-08-02T03:36:50.6532435+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 950,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 6,801,000 | 2026-08-02T03:36:50.6657664+00:00 | 111.2ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 950,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,262,000 | 2026-08-02T03:36:51.1265732+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 950,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,281,000 | 2026-08-02T03:36:51.1491231+00:00 | 113.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 950,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,291,000 | 2026-08-02T03:36:51.1640469+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 950,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 7,301,000 | 2026-08-02T03:36:51.1705796+00:00 | 104.4ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 950,008 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,079,000 | 2026-08-02T03:36:52.0870136+00:00 | 123.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,089,000 | 2026-08-02T03:36:52.1045279+00:00 | 127.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,117,000 | 2026-08-02T03:36:52.1392907+00:00 | 122.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,119,000 | 2026-08-02T03:36:52.1406792+00:00 | 123.8ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,123,000 | 2026-08-02T03:36:52.1472498+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,133,000 | 2026-08-02T03:36:52.1566394+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,134,000 | 2026-08-02T03:36:52.156947+00:00 | 103.1ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,136,000 | 2026-08-02T03:36:52.1602262+00:00 | 108.9ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,137,000 | 2026-08-02T03:36:52.1606525+00:00 | 147.2ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,147,000 | 2026-08-02T03:36:52.2200053+00:00 | 103.3ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,167,000 | 2026-08-02T03:36:52.2462711+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,177,000 | 2026-08-02T03:36:52.2621428+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,187,000 | 2026-08-02T03:36:52.2825545+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 11.0s / 753,190 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,759,000 | 2026-08-02T03:36:52.948551+00:00 | 112.9ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 986,417 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,763,000 | 2026-08-02T03:36:52.9510574+00:00 | 118.7ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 986,417 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,769,000 | 2026-08-02T03:36:52.9621391+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 986,417 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,773,000 | 2026-08-02T03:36:52.9697353+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 986,417 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,779,000 | 2026-08-02T03:36:52.975732+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 986,417 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 8,789,000 | 2026-08-02T03:36:52.9885906+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 12.0s / 986,417 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 9,471,000 | 2026-08-02T03:36:53.6422451+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 9,472,000 | 2026-08-02T03:36:53.6428696+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 9,481,000 | 2026-08-02T03:36:53.6494495+00:00 | 103.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 9,482,000 | 2026-08-02T03:36:53.6532205+00:00 | 103.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 9,487,000 | 2026-08-02T03:36:53.6554364+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 9,939,000 | 2026-08-02T03:36:54.1253891+00:00 | 110.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 9,953,000 | 2026-08-02T03:36:54.1345781+00:00 | 108.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 947,880 msg/s | Gen2 +0 / pause +1.3ms |
| Dekaf (3conn) | 14,351,000 | 2026-08-02T03:36:58.1676104+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,111,015 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 14,361,000 | 2026-08-02T03:36:58.1726466+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,111,015 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,239,000 | 2026-08-02T03:37:01.6388416+00:00 | 114.7ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,178,370 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,243,000 | 2026-08-02T03:37:01.6414338+00:00 | 116.6ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,178,370 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,253,000 | 2026-08-02T03:37:01.6531871+00:00 | 112.8ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,178,370 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 18,259,000 | 2026-08-02T03:37:01.6578122+00:00 | 118.3ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,178,370 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 20,592,000 | 2026-08-02T03:37:03.6471929+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 1,194,115 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,157,000 | 2026-08-02T03:37:04.1304112+00:00 | 112.5ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,163,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,167,000 | 2026-08-02T03:37:04.1366178+00:00 | 109.8ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,163,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 21,207,000 | 2026-08-02T03:37:04.1733618+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,163,524 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 25,273,000 | 2026-08-02T03:37:07.6442961+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 26.0s / 1,152,209 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 25,283,000 | 2026-08-02T03:37:07.6495868+00:00 | 111.3ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,147,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 25,293,000 | 2026-08-02T03:37:07.6548822+00:00 | 124.7ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,147,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 25,299,000 | 2026-08-02T03:37:07.6577919+00:00 | 127.8ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,147,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 25,303,000 | 2026-08-02T03:37:07.6605533+00:00 | 125.0ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,147,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 25,309,000 | 2026-08-02T03:37:07.6715105+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,147,556 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 29,399,000 | 2026-08-02T03:37:11.1222379+00:00 | 108.0ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,202,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 29,413,000 | 2026-08-02T03:37:11.1309208+00:00 | 105.1ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,202,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 29,423,000 | 2026-08-02T03:37:11.1370455+00:00 | 104.9ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,202,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 29,429,000 | 2026-08-02T03:37:11.140742+00:00 | 106.1ms | broker/backlog (no scale or GC event) | - | - | 30.0s / 1,202,469 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 40,429,000 | 2026-08-02T03:37:20.1281745+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 39.0s / 1,256,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 40,439,000 | 2026-08-02T03:37:20.1375186+00:00 | 103.0ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 39.0s / 1,256,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,051,000 | 2026-08-02T03:37:20.6394988+00:00 | 107.3ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 39.0s / 1,256,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,071,000 | 2026-08-02T03:37:20.6530043+00:00 | 106.8ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 39.0s / 1,256,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,072,000 | 2026-08-02T03:37:20.6533074+00:00 | 106.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 39.0s / 1,256,026 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 41,081,000 | 2026-08-02T03:37:20.6594856+00:00 | 108.1ms | broker/backlog (no scale or GC event) | 2:capacity/failed, 3:capacity/failed, 1:capacity/failed | - | 40.0s / 1,182,361 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 50,852,000 | 2026-08-02T03:37:28.664531+00:00 | 101.4ms | broker/backlog (no scale or GC event) | - | - | 47.0s / 1,218,085 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 53,252,000 | 2026-08-02T03:37:30.650559+00:00 | 121.8ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,245,101 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 53,262,000 | 2026-08-02T03:37:30.6583912+00:00 | 120.5ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,245,101 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 53,281,000 | 2026-08-02T03:37:30.6694341+00:00 | 123.3ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,245,101 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 53,291,000 | 2026-08-02T03:37:30.679093+00:00 | 120.3ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,245,101 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 53,292,000 | 2026-08-02T03:37:30.6852136+00:00 | 120.9ms | broker/backlog (no scale or GC event) | - | - | 50.0s / 1,245,101 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 61,859,000 | 2026-08-02T03:37:37.6577323+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,171,687 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 61,863,000 | 2026-08-02T03:37:37.6598321+00:00 | 111.6ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,171,687 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 61,869,000 | 2026-08-02T03:37:37.6638564+00:00 | 112.4ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,171,687 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 61,873,000 | 2026-08-02T03:37:37.6659586+00:00 | 116.7ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,171,687 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 61,879,000 | 2026-08-02T03:37:37.6714488+00:00 | 114.3ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,171,687 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 61,889,000 | 2026-08-02T03:37:37.6928898+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 57.0s / 1,171,687 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 74,861,000 | 2026-08-02T03:37:48.1697379+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 1,200,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 74,862,000 | 2026-08-02T03:37:48.1700853+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 67.0s / 1,200,368 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 76,149,000 | 2026-08-02T03:37:49.1756824+00:00 | 108.4ms | broker/backlog (no scale or GC event) | - | - | 68.0s / 1,291,694 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 76,153,000 | 2026-08-02T03:37:49.1774323+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 68.0s / 1,291,694 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 81,623,000 | 2026-08-02T03:37:53.6429462+00:00 | 100.4ms | broker/backlog (no scale or GC event) | - | - | 72.0s / 1,195,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 81,643,000 | 2026-08-02T03:37:53.6559974+00:00 | 107.7ms | broker/backlog (no scale or GC event) | - | - | 72.0s / 1,195,161 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 81,659,000 | 2026-08-02T03:37:53.6666686+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 73.0s / 1,112,699 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 84,511,000 | 2026-08-02T03:37:56.1163824+00:00 | 101.1ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,521,000 | 2026-08-02T03:37:56.1239171+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,522,000 | 2026-08-02T03:37:56.1242199+00:00 | 104.0ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,531,000 | 2026-08-02T03:37:56.1313933+00:00 | 102.2ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,532,000 | 2026-08-02T03:37:56.1366378+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,539,000 | 2026-08-02T03:37:56.1395965+00:00 | 101.3ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,542,000 | 2026-08-02T03:37:56.1405279+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,543,000 | 2026-08-02T03:37:56.1411365+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 84,549,000 | 2026-08-02T03:37:56.1471543+00:00 | 100.7ms | broker/backlog (no scale or GC event) | - | - | 75.0s / 1,177,353 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,867,000 | 2026-08-02T03:37:59.6560973+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 78.0s / 1,196,787 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,877,000 | 2026-08-02T03:37:59.6625305+00:00 | 103.6ms | broker/backlog (no scale or GC event) | - | - | 78.0s / 1,196,787 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 88,897,000 | 2026-08-02T03:37:59.6838114+00:00 | 106.3ms | broker/backlog (no scale or GC event) | - | - | 79.0s / 1,228,897 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 105,593,000 | 2026-08-02T03:38:13.1555347+00:00 | 100.8ms | broker/backlog (no scale or GC event) | - | - | 92.1s / 1,236,267 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 108,736,000 | 2026-08-02T03:38:15.6731296+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 94.1s / 1,199,865 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 108,737,000 | 2026-08-02T03:38:15.6734966+00:00 | 121.3ms | broker/backlog (no scale or GC event) | - | - | 95.1s / 1,308,790 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 110,592,000 | 2026-08-02T03:38:17.1486449+00:00 | 100.3ms | broker/backlog (no scale or GC event) | - | - | 96.1s / 1,256,093 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 124,333,000 | 2026-08-02T03:38:28.121209+00:00 | 108.0ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/succeeded | - | 107.1s / 1,236,961 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 124,343,000 | 2026-08-02T03:38:28.1302041+00:00 | 106.5ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/succeeded | - | 107.1s / 1,236,961 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 124,369,000 | 2026-08-02T03:38:28.1517601+00:00 | 109.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/succeeded | - | 107.1s / 1,236,961 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 124,379,000 | 2026-08-02T03:38:28.1573836+00:00 | 105.4ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/succeeded | - | 107.1s / 1,236,961 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 124,383,000 | 2026-08-02T03:38:28.159404+00:00 | 112.8ms | broker/backlog (no scale or GC event) | 3:capacity/failed, 2:capacity/succeeded | - | 107.1s / 1,236,961 msg/s | Gen2 +0 / pause +0.8ms |
| Dekaf (3conn) | 133,991,000 | 2026-08-02T03:38:35.6737305+00:00 | 105.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 114.1s / 1,240,367 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 134,491,000 | 2026-08-02T03:38:36.1460919+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 115.1s / 1,235,412 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 134,502,000 | 2026-08-02T03:38:36.1515075+00:00 | 101.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 115.1s / 1,235,412 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 134,511,000 | 2026-08-02T03:38:36.157108+00:00 | 101.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/succeeded | - | 115.1s / 1,235,412 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,193,000 | 2026-08-02T03:38:51.1389414+00:00 | 104.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,203,000 | 2026-08-02T03:38:51.14393+00:00 | 105.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,213,000 | 2026-08-02T03:38:51.1516438+00:00 | 109.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,219,000 | 2026-08-02T03:38:51.1550453+00:00 | 110.4ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,223,000 | 2026-08-02T03:38:51.1566124+00:00 | 113.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,229,000 | 2026-08-02T03:38:51.1643915+00:00 | 116.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 153,233,000 | 2026-08-02T03:38:51.173744+00:00 | 106.8ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 1:capacity/failed | - | 130.1s / 1,126,928 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 175,029,000 | 2026-08-02T03:39:09.1818102+00:00 | 102.3ms | broker/backlog (no scale or GC event) | 3:capacity/succeeded, 2:capacity/failed | - | 148.1s / 1,187,382 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 233,382,000 | 2026-08-02T03:39:56.1615572+00:00 | 100.5ms | broker/backlog (no scale or GC event) | 2:capacity/failed | - | 195.1s / 1,212,105 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*972 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.59x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.32x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.01 | 3135.94 | 1,216,771 | 2,080,849 | +65.6% | +583.63% | 148.53 | 1,216,771 | 0 | 1.23 |
| Confluent | 1.74 | - | 134,415 | 1,803,137 | +12.5% | +102.08% | 16.41 | 134,415 | 0 | 0.23 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 6,402 | 608.93 | 588.77 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:22.3112114+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 458,113 msg/s |
| Dekaf | 2026-08-02T03:06:23.3107917+00:00 | 1 | 16.0 MiB / 1.0 MiB | 82.2 MB/s | 0/0 | 0 | 1.0s / 458,113 msg/s |
| Dekaf | 2026-08-02T03:06:24.3101039+00:00 | 1 | 16.0 MiB / 2.2 MiB | 89.9 MB/s | 0/0 | 0 | 2.0s / 1,309,536 msg/s |
| Dekaf | 2026-08-02T03:06:25.3103104+00:00 | 1 | 16.0 MiB / 2.4 MiB | 456.9 MB/s | 0/0 | 0 | 3.0s / 2,127,299 msg/s |
| Dekaf | 2026-08-02T03:06:26.3110818+00:00 | 1 | 16.0 MiB / 2.4 MiB | 456.9 MB/s | 0/0 | 0 | 4.0s / 2,086,473 msg/s |
| Dekaf | 2026-08-02T03:06:27.311811+00:00 | 1 | 16.0 MiB / 1.4 MiB | 456.9 MB/s | 0/0 | 0 | 5.0s / 2,075,225 msg/s |
| Dekaf | 2026-08-02T03:06:28.3147784+00:00 | 1 | 16.0 MiB / 2.9 MiB | 456.9 MB/s | 0/0 | 0 | 6.0s / 2,042,089 msg/s |
| Dekaf | 2026-08-02T03:06:29.3164294+00:00 | 1 | 16.0 MiB / 2.4 MiB | 456.9 MB/s | 0/0 | 0 | 7.0s / 2,102,310 msg/s |
| Dekaf | 2026-08-02T03:06:30.3188856+00:00 | 1 | 16.0 MiB / 3.8 MiB | 456.9 MB/s | 0/0 | 0 | 8.0s / 2,322,403 msg/s |
| Dekaf | 2026-08-02T03:06:31.3180751+00:00 | 1 | 16.0 MiB / 2.1 MiB | 474.8 MB/s | 0/0 | 0 | 9.0s / 1,999,328 msg/s |
| Dekaf | 2026-08-02T03:06:32.3183263+00:00 | 1 | 16.0 MiB / 2.4 MiB | 474.8 MB/s | 0/0 | 0 | 10.0s / 2,128,080 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.72x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.15x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 390.93 | 390.92 | 268 | 359 | +2.0% | +0.22% | 0.26 | 357 | 0 | 0.14 |
| Confluent | 267.89 | - | 129 | 172 | +0.6% | +0.07% | 0.12 | 172 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 106,907 | 118.77 | 1.16 KB |
| Dekaf | 2 | 107,137 | 119.03 | 1.16 KB |
| Dekaf | 3 | 106,962 | 118.84 | 1.16 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-08-02T03:06:28.2832812+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 180 msg/s |
| Dekaf | 2026-08-02T03:06:37.287729+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 9.0s / 307 msg/s |
| Dekaf | 2026-08-02T03:06:46.2932423+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.1 MB/s | 0/0 | 0 | 18.0s / 335 msg/s |
| Dekaf | 2026-08-02T03:06:56.3293483+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.1 MB/s | 0/0 | 0 | 28.0s / 333 msg/s |
| Dekaf | 2026-08-02T03:07:05.3343244+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 37.0s / 347 msg/s |
| Dekaf | 2026-08-02T03:07:14.3448108+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 46.0s / 327 msg/s |
| Dekaf | 2026-08-02T03:07:23.3619162+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 55.0s / 352 msg/s |
| Dekaf | 2026-08-02T03:07:32.3835286+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 64.0s / 358 msg/s |
| Dekaf | 2026-08-02T03:07:41.3906112+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 73.0s / 361 msg/s |
| Dekaf | 2026-08-02T03:07:50.4035198+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 82.0s / 359 msg/s |
| Dekaf | 2026-08-02T03:07:59.416315+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 369 msg/s |
| Dekaf | 2026-08-02T03:08:08.4238195+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 100.0s / 356 msg/s |
| Dekaf | 2026-08-02T03:08:17.4305665+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 109.0s / 371 msg/s |
| Dekaf | 2026-08-02T03:08:26.4339283+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 118.0s / 359 msg/s |
| Dekaf | 2026-08-02T03:08:35.4555408+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 358 msg/s |
| Dekaf | 2026-08-02T03:08:44.4760404+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 136.0s / 360 msg/s |
| Dekaf | 2026-08-02T03:08:53.4948223+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 145.0s / 360 msg/s |
| Dekaf | 2026-08-02T03:09:03.5101402+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 359 msg/s |
| Dekaf | 2026-08-02T03:09:12.5276702+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 360 msg/s |
| Dekaf | 2026-08-02T03:09:21.5341505+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 356 msg/s |
| Dekaf | 2026-08-02T03:09:30.542982+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 339 msg/s |
| Dekaf | 2026-08-02T03:09:39.5840484+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 361 msg/s |
| Dekaf | 2026-08-02T03:09:48.5918113+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 353 msg/s |
| Dekaf | 2026-08-02T03:09:57.6086203+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 349 msg/s |
| Dekaf | 2026-08-02T03:10:06.6325285+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 353 msg/s |
| Dekaf | 2026-08-02T03:10:15.641644+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 361 msg/s |
| Dekaf | 2026-08-02T03:10:24.6975384+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 355 msg/s |
| Dekaf | 2026-08-02T03:10:33.7021555+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 245.0s / 348 msg/s |
| Dekaf | 2026-08-02T03:10:42.7277191+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 254.0s / 370 msg/s |
| Dekaf | 2026-08-02T03:10:51.7320157+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 263.0s / 359 msg/s |
| Dekaf | 2026-08-02T03:11:00.7442882+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 272.0s / 351 msg/s |
| Dekaf | 2026-08-02T03:11:10.7559917+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 282.0s / 368 msg/s |
| Dekaf | 2026-08-02T03:11:19.7599852+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 291.0s / 360 msg/s |
| Dekaf | 2026-08-02T03:11:28.7634563+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 300.0s / 347 msg/s |
| Dekaf | 2026-08-02T03:11:37.7671633+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 309.0s / 362 msg/s |
| Dekaf | 2026-08-02T03:11:46.7727582+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 318.0s / 370 msg/s |
| Dekaf | 2026-08-02T03:11:55.7904715+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 327.0s / 349 msg/s |
| Dekaf | 2026-08-02T03:12:04.798849+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.1s / 355 msg/s |
| Dekaf | 2026-08-02T03:12:13.8148992+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 345.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:12:22.8218297+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 359 msg/s |
| Dekaf | 2026-08-02T03:12:31.8347401+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 363.1s / 358 msg/s |
| Dekaf | 2026-08-02T03:12:40.8509202+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 372.1s / 357 msg/s |
| Dekaf | 2026-08-02T03:12:49.8608957+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 381.1s / 362 msg/s |
| Dekaf | 2026-08-02T03:12:58.8738928+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 390.1s / 369 msg/s |
| Dekaf | 2026-08-02T03:13:07.8784921+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 399.1s / 359 msg/s |
| Dekaf | 2026-08-02T03:13:17.8880961+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 409.1s / 370 msg/s |
| Dekaf | 2026-08-02T03:13:26.9072462+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 419.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:13:35.9166018+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 428.1s / 371 msg/s |
| Dekaf | 2026-08-02T03:13:44.9233166+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 437.1s / 360 msg/s |
| Dekaf | 2026-08-02T03:13:53.9352917+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 446.1s / 362 msg/s |
| Dekaf | 2026-08-02T03:14:02.9519029+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 455.1s / 366 msg/s |
| Dekaf | 2026-08-02T03:14:11.9566296+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 464.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:14:20.9776522+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:14:29.9809552+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 364 msg/s |
| Dekaf | 2026-08-02T03:14:38.9885707+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 344 msg/s |
| Dekaf | 2026-08-02T03:14:47.9963529+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 348 msg/s |
| Dekaf | 2026-08-02T03:14:57.000001+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 509.1s / 344 msg/s |
| Dekaf | 2026-08-02T03:15:06.0163081+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 518.1s / 352 msg/s |
| Dekaf | 2026-08-02T03:15:15.0213301+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 527.1s / 356 msg/s |
| Dekaf | 2026-08-02T03:15:25.0394904+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 537.1s / 358 msg/s |
| Dekaf | 2026-08-02T03:15:34.0468608+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 546.1s / 360 msg/s |
| Dekaf | 2026-08-02T03:15:43.052573+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 555.1s / 358 msg/s |
| Dekaf | 2026-08-02T03:15:52.0723454+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 564.1s / 360 msg/s |
| Dekaf | 2026-08-02T03:16:01.0769537+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 573.1s / 357 msg/s |
| Dekaf | 2026-08-02T03:16:10.0791421+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 582.1s / 358 msg/s |
| Dekaf | 2026-08-02T03:16:19.1004249+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 591.1s / 369 msg/s |
| Dekaf | 2026-08-02T03:16:28.1078816+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 600.1s / 356 msg/s |
| Dekaf | 2026-08-02T03:16:37.136113+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 609.1s / 359 msg/s |
| Dekaf | 2026-08-02T03:16:46.175277+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 618.1s / 372 msg/s |
| Dekaf | 2026-08-02T03:16:55.187014+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 627.1s / 362 msg/s |
| Dekaf | 2026-08-02T03:17:04.204296+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 636.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:17:13.2071972+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 645.1s / 365 msg/s |
| Dekaf | 2026-08-02T03:17:22.2169699+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 654.1s / 353 msg/s |
| Dekaf | 2026-08-02T03:17:31.2220095+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 663.1s / 352 msg/s |
| Dekaf | 2026-08-02T03:17:41.2421167+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 673.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:17:50.245191+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 348 msg/s |
| Dekaf | 2026-08-02T03:17:59.2508119+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 351 msg/s |
| Dekaf | 2026-08-02T03:18:08.2621776+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 700.1s / 354 msg/s |
| Dekaf | 2026-08-02T03:18:17.2671375+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 709.1s / 369 msg/s |
| Dekaf | 2026-08-02T03:18:26.2779215+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 718.1s / 359 msg/s |
| Dekaf | 2026-08-02T03:18:35.298712+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 727.1s / 360 msg/s |
| Dekaf | 2026-08-02T03:18:44.319956+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 736.1s / 355 msg/s |
| Dekaf | 2026-08-02T03:18:53.3564409+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 745.1s / 364 msg/s |
| Dekaf | 2026-08-02T03:19:02.3660307+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 754.1s / 369 msg/s |
| Dekaf | 2026-08-02T03:19:11.3931453+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 763.1s / 360 msg/s |
| Dekaf | 2026-08-02T03:19:20.4038538+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 772.1s / 369 msg/s |
| Dekaf | 2026-08-02T03:19:29.423236+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 781.1s / 364 msg/s |
| Dekaf | 2026-08-02T03:19:38.4253276+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 790.1s / 366 msg/s |
| Dekaf | 2026-08-02T03:19:48.4311474+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 372 msg/s |
| Dekaf | 2026-08-02T03:19:57.4519762+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 809.1s / 360 msg/s |
| Dekaf | 2026-08-02T03:20:06.4547506+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 818.1s / 338 msg/s |
| Dekaf | 2026-08-02T03:20:15.4643974+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 827.1s / 352 msg/s |
| Dekaf | 2026-08-02T03:20:24.4866059+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 836.1s / 346 msg/s |
| Dekaf | 2026-08-02T03:20:33.5062294+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 845.1s / 358 msg/s |
| Dekaf | 2026-08-02T03:20:42.526917+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 854.1s / 354 msg/s |
| Dekaf | 2026-08-02T03:20:51.529214+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 863.1s / 370 msg/s |
| Dekaf | 2026-08-02T03:21:00.5375095+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 872.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:21:09.5563584+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 881.1s / 341 msg/s |
| Dekaf | 2026-08-02T03:21:18.5647571+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 890.1s / 361 msg/s |
| Dekaf | 2026-08-02T03:21:27.5691928+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 899.1s / 367 msg/s |
*2,595 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 155,200 | 116,400 | 38,800 | 116,400 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 321,000 | 240,800 | 80,200 | 240,800 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.46x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.09x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.73 | - | 1,775,246 | 1,769,645 | -1.3% | -0.16% | 1693.01 | - | 0 | 1.30 |
| Confluent | 1.16 | - | 1,275,356 | 1,315,604 | +6.7% | +0.62% | 1216.27 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.58x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.35x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.71 | - | 1,986,430 | 2,044,446 | -0.1% | +0.03% | 1894.41 | - | 0 | 1.42 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.42 | - | 3,744,193 | 3,758,695 | -0.9% | -0.12% | 3570.74 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.36 | - | 4,109,499 | 4,072,796 | +1.1% | +0.00% | 3919.12 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 22546 | 112 | 0 | 2608.34 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 320531 | 18 | 1 | 1542.25 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 301876 | 1 | 1 | 1487.83 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 207738 | 1 | 1 | 976.47 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 292910 | 1 | 1 | 1594.99 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 329011 | 5 | 1 | 1574.50 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 195261 | 33 | 1 | 912.58 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 312100 | 43 | 1 | 1490.21 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 292828 | 1 | 1 | 1502.52 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 196695 | 1 | 1 | 924.41 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 6790 | 1 | 1 | 16.04 GB | 870 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 83 | 1 | 1 | 192.39 MB | 1.27 KB |
| Dekaf | Consumer | 26516 | 133 | 6 | 3012.26 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 88525 | 4 | 1 | 3370.89 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 503.11 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 15 | 1 | 0 | 1000.97 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 432 | 2 | 2 | 1.74 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 429 | 2 | 2 | 181.99 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 211 | 3 | 2 | 869.15 MB | 1 B |
| Dekaf | Producer (Acks All) | 414 | 2 | 2 | 117.83 MB | 0 B |
| Dekaf | Producer (Acks All) | 421 | 2 | 2 | 1.57 GB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 209 | 5 | 4 | 735.14 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 420 | 2 | 2 | 1.58 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 415 | 1 | 1 | 142.58 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 220 | 3 | 2 | 894.72 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 605 | 6 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 31 | 2 | 1 | 127.90 MB | 418 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 367 | 2 | 2 | 1.44 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 248 | 2 | 1 | 1.09 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 387 | 2 | 2 | 1.43 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 242 | 5 | 4 | 953.03 MB | 1 B |

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
