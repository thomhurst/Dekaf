---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-17 03:19 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,562,351 | 1,552,159–1,572,609 | 0.94 | 1.22x |
| Confluent | 2 | 1,277,274 | 1,229,715–1,326,674 | 1.38 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.77 | 746.42 | 1,816,364 | 1,913,199 | +51.0% | +4.84% | 1732.22 | 1,816,364 | 0 | 1.41 |
| Dekaf (dekaf-first) | 0.93 | 954.17 | 1,562,032 | 1,572,609 | +0.9% | +0.10% | 1489.67 | 1,562,032 | 0 | 1.45 |
| Dekaf (confluent-first) | 0.95 | 966.88 | 1,539,792 | 1,552,159 | +4.1% | +0.34% | 1468.46 | 1,539,792 | 0 | 1.46 |
| Confluent (confluent-first) | 1.31 | - | 1,310,419 | 1,326,674 | +1.4% | +0.07% | 1249.71 | 1,310,419 | 0 | 1.72 |
| Confluent (dekaf-first) | 1.45 | - | 1,228,762 | 1,229,715 | -15.4% | -1.21% | 1171.84 | 1,228,762 | 0 | 1.79 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,371,645 | 1524.03 | 1018.87 KB |
| Dekaf | 1 | 1,354,608 | 1505.10 | 1017.00 KB |
| Dekaf (3conn) | 1 | 1,694,225 | 1882.44 | 959.19 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:43.8655597+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 601,078 msg/s |
| Dekaf | 2026-07-17T02:19:10.8739745+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1746.0 MB/s | 0/0 | 41,971 | 27.0s / 1,647,510 msg/s |
| Dekaf | 2026-07-17T02:19:38.8838447+00:00 | 1 | 14.0 MiB / 13.9 MiB | 1757.4 MB/s | 1/0 | 107,532 | 55.0s / 1,582,770 msg/s |
| Dekaf | 2026-07-17T02:20:05.8954209+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1757.4 MB/s | 1/0 | 168,403 | 82.0s / 1,571,606 msg/s |
| Dekaf | 2026-07-17T02:20:32.902883+00:00 | 1 | 12.0 MiB / 9.7 MiB | 1757.4 MB/s | 2/0 | 235,048 | 109.0s / 1,574,222 msg/s |
| Dekaf | 2026-07-17T02:20:59.910068+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1757.4 MB/s | 2/1 | 286,967 | 136.1s / 1,497,499 msg/s |
| Dekaf | 2026-07-17T02:21:27.9184407+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1757.4 MB/s | 2/1 | 351,002 | 164.1s / 1,510,548 msg/s |
| Dekaf | 2026-07-17T02:21:54.9210253+00:00 | 1 | 13.0 MiB / 12.2 MiB | 1757.4 MB/s | 3/1 | 408,704 | 191.1s / 1,506,299 msg/s |
| Dekaf | 2026-07-17T02:22:21.9249791+00:00 | 1 | 13.0 MiB / 12.6 MiB | 1757.4 MB/s | 3/1 | 462,633 | 218.1s / 1,533,779 msg/s |
| Dekaf | 2026-07-17T02:22:48.934809+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1757.4 MB/s | 4/1 | 513,867 | 245.1s / 1,524,280 msg/s |
| Dekaf | 2026-07-17T02:23:16.9471688+00:00 | 1 | 14.0 MiB / 10.9 MiB | 1757.4 MB/s | 4/2 | 574,298 | 273.1s / 1,548,925 msg/s |
| Dekaf | 2026-07-17T02:23:43.9628947+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1757.4 MB/s | 4/2 | 634,675 | 300.1s / 1,524,927 msg/s |
| Dekaf | 2026-07-17T02:24:10.9713586+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1757.4 MB/s | 5/2 | 698,382 | 327.1s / 1,552,159 msg/s |
| Dekaf | 2026-07-17T02:24:37.977526+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1757.4 MB/s | 5/2 | 764,251 | 354.1s / 1,525,668 msg/s |
| Dekaf | 2026-07-17T02:25:05.9880703+00:00 | 1 | 12.0 MiB / 8.7 MiB | 1757.4 MB/s | 5/3 | 823,062 | 382.1s / 1,453,680 msg/s |
| Dekaf | 2026-07-17T02:25:32.9959839+00:00 | 1 | 12.0 MiB / 10.1 MiB | 1757.4 MB/s | 5/4 | 886,479 | 409.1s / 1,527,420 msg/s |
| Dekaf | 2026-07-17T02:26:00.0075281+00:00 | 1 | 10.0 MiB / 9.1 MiB | 1757.4 MB/s | 5/4 | 952,262 | 436.2s / 1,446,510 msg/s |
| Dekaf | 2026-07-17T02:26:27.0158727+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1757.4 MB/s | 5/5 | 1,012,177 | 463.2s / 1,517,687 msg/s |
| Dekaf | 2026-07-17T02:26:55.0269279+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1757.4 MB/s | 5/5 | 1,087,940 | 491.2s / 1,574,417 msg/s |
| Dekaf | 2026-07-17T02:27:22.0347441+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1757.4 MB/s | 5/6 | 1,163,932 | 518.2s / 1,624,105 msg/s |
| Dekaf | 2026-07-17T02:27:49.0438157+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1757.4 MB/s | 5/7 | 1,231,039 | 545.2s / 1,490,931 msg/s |
| Dekaf | 2026-07-17T02:28:17.0549893+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1757.4 MB/s | 5/7 | 1,305,627 | 573.2s / 1,599,828 msg/s |
| Dekaf | 2026-07-17T02:28:44.0629476+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1757.4 MB/s | 5/8 | 1,380,615 | 600.2s / 1,569,264 msg/s |
| Dekaf | 2026-07-17T02:29:11.0723116+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1757.4 MB/s | 5/8 | 1,449,625 | 627.2s / 1,394,344 msg/s |
| Dekaf | 2026-07-17T02:29:38.0757031+00:00 | 1 | 12.0 MiB / 10.5 MiB | 1757.4 MB/s | 5/9 | 1,526,913 | 654.2s / 1,679,027 msg/s |
| Dekaf | 2026-07-17T02:30:06.088133+00:00 | 1 | 12.0 MiB / 11.2 MiB | 1757.4 MB/s | 5/10 | 1,610,384 | 682.2s / 1,557,046 msg/s |
| Dekaf | 2026-07-17T02:30:33.0945762+00:00 | 1 | 10.0 MiB / 4.1 MiB | 1757.4 MB/s | 5/10 | 1,688,442 | 709.2s / 1,475,444 msg/s |
| Dekaf | 2026-07-17T02:31:00.1063136+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1757.4 MB/s | 5/11 | 1,765,376 | 736.2s / 1,584,737 msg/s |
| Dekaf | 2026-07-17T02:31:27.1114654+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1757.4 MB/s | 5/11 | 1,843,067 | 763.2s / 1,542,536 msg/s |
| Dekaf | 2026-07-17T02:31:55.1266764+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1757.4 MB/s | 6/11 | 1,918,798 | 791.2s / 1,576,474 msg/s |
| Dekaf | 2026-07-17T02:32:22.1371367+00:00 | 1 | 13.0 MiB / 12.4 MiB | 1757.4 MB/s | 6/12 | 1,986,229 | 818.3s / 1,576,354 msg/s |
| Dekaf | 2026-07-17T02:32:49.1482376+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1757.4 MB/s | 6/12 | 2,062,174 | 845.3s / 1,575,192 msg/s |
| Dekaf | 2026-07-17T02:33:16.1621418+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1757.4 MB/s | 6/13 | 2,136,624 | 872.3s / 1,602,630 msg/s |
| Dekaf | 2026-07-17T02:33:44.6871563+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 449,409 msg/s |
| Dekaf | 2026-07-17T02:34:11.7017349+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1761.2 MB/s | 0/0 | 40,108 | 27.0s / 1,527,523 msg/s |
| Dekaf | 2026-07-17T02:34:38.7138711+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1761.2 MB/s | 0/1 | 87,416 | 54.0s / 1,562,088 msg/s |
| Dekaf | 2026-07-17T02:35:05.7262053+00:00 | 1 | 16.0 MiB / 15.4 MiB | 1761.2 MB/s | 0/1 | 129,895 | 81.0s / 1,614,237 msg/s |
| Dekaf | 2026-07-17T02:35:33.7404444+00:00 | 1 | 16.0 MiB / 14.0 MiB | 1761.2 MB/s | 0/2 | 175,029 | 109.1s / 1,536,125 msg/s |
| Dekaf | 2026-07-17T02:36:00.7522223+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1761.2 MB/s | 1/2 | 231,637 | 136.1s / 1,473,858 msg/s |
| Dekaf | 2026-07-17T02:36:27.7653356+00:00 | 1 | 14.0 MiB / 11.3 MiB | 1761.2 MB/s | 1/2 | 301,693 | 163.1s / 1,579,185 msg/s |
| Dekaf | 2026-07-17T02:36:55.7762649+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 2/2 | 377,949 | 191.1s / 1,589,745 msg/s |
| Dekaf | 2026-07-17T02:37:22.7879007+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1761.2 MB/s | 2/2 | 448,445 | 218.1s / 1,567,726 msg/s |
| Dekaf | 2026-07-17T02:37:49.7975473+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 2/3 | 517,263 | 245.1s / 1,566,458 msg/s |
| Dekaf | 2026-07-17T02:38:16.8084294+00:00 | 1 | 13.0 MiB / 11.5 MiB | 1761.2 MB/s | 3/3 | 592,464 | 272.1s / 1,500,875 msg/s |
| Dekaf | 2026-07-17T02:38:44.8238838+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1761.2 MB/s | 3/3 | 665,573 | 300.1s / 1,559,047 msg/s |
| Dekaf | 2026-07-17T02:39:11.8309609+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1761.2 MB/s | 4/3 | 732,900 | 327.1s / 1,558,797 msg/s |
| Dekaf | 2026-07-17T02:39:38.8516315+00:00 | 1 | 14.0 MiB / 13.0 MiB | 1761.2 MB/s | 4/3 | 805,060 | 354.1s / 1,587,127 msg/s |
| Dekaf | 2026-07-17T02:40:05.8648476+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1761.2 MB/s | 4/4 | 870,471 | 381.1s / 1,501,403 msg/s |
| Dekaf | 2026-07-17T02:40:33.8738811+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 5/4 | 939,743 | 409.1s / 1,580,479 msg/s |
| Dekaf | 2026-07-17T02:41:00.8858893+00:00 | 1 | 10.0 MiB / 10.0 MiB | 1761.2 MB/s | 5/4 | 1,018,455 | 436.1s / 1,471,117 msg/s |
| Dekaf | 2026-07-17T02:41:27.8924449+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 5/5 | 1,093,922 | 463.1s / 1,582,061 msg/s |
| Dekaf | 2026-07-17T02:41:54.9009756+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 5/5 | 1,170,511 | 490.2s / 1,590,508 msg/s |
| Dekaf | 2026-07-17T02:42:22.911914+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 5/6 | 1,253,173 | 518.2s / 1,627,004 msg/s |
| Dekaf | 2026-07-17T02:42:49.9208273+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 5/7 | 1,319,431 | 545.2s / 1,569,147 msg/s |
| Dekaf | 2026-07-17T02:43:16.9292+00:00 | 1 | 13.0 MiB / 11.7 MiB | 1761.2 MB/s | 5/7 | 1,399,075 | 572.2s / 1,561,650 msg/s |
| Dekaf | 2026-07-17T02:43:43.9380791+00:00 | 1 | 13.0 MiB / 3.8 MiB | 1761.2 MB/s | 6/7 | 1,473,470 | 599.2s / 1,498,181 msg/s |
| Dekaf | 2026-07-17T02:44:11.9484121+00:00 | 1 | 14.0 MiB / 13.1 MiB | 1761.2 MB/s | 6/7 | 1,547,120 | 627.2s / 1,603,858 msg/s |
| Dekaf | 2026-07-17T02:44:38.9560955+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1761.2 MB/s | 7/7 | 1,618,251 | 654.2s / 1,634,548 msg/s |
| Dekaf | 2026-07-17T02:45:05.9695683+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1761.2 MB/s | 7/8 | 1,685,413 | 681.2s / 1,569,091 msg/s |
| Dekaf | 2026-07-17T02:45:32.9801219+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 7/8 | 1,754,006 | 708.2s / 1,618,075 msg/s |
| Dekaf | 2026-07-17T02:46:00.9909456+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 8/8 | 1,830,640 | 736.2s / 1,595,006 msg/s |
| Dekaf | 2026-07-17T02:46:28.0014515+00:00 | 1 | 10.0 MiB / 9.0 MiB | 1761.2 MB/s | 8/8 | 1,905,659 | 763.2s / 1,402,242 msg/s |
| Dekaf | 2026-07-17T02:46:55.0103844+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1761.2 MB/s | 8/9 | 1,979,993 | 790.2s / 1,600,804 msg/s |
| Dekaf | 2026-07-17T02:47:23.0205786+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1761.2 MB/s | 8/10 | 2,060,990 | 818.2s / 1,545,262 msg/s |
| Dekaf | 2026-07-17T02:47:50.025606+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1761.2 MB/s | 8/10 | 2,139,470 | 845.3s / 1,554,336 msg/s |
| Dekaf | 2026-07-17T02:48:17.0313954+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1761.2 MB/s | 8/11 | 2,214,329 | 872.3s / 1,559,371 msg/s |
| Dekaf | 2026-07-17T02:48:44.0412248+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1761.2 MB/s | 8/11 | 2,291,046 | 899.3s / 1,592,514 msg/s |
| Dekaf (3conn) | 2026-07-17T03:04:12.8518965+00:00 | 1 | 48.0 MiB / 2.5 MiB | 1502.1 MB/s | 0/0 | 9 | 27.0s / 1,208,124 msg/s |
| Dekaf (3conn) | 2026-07-17T03:04:39.8609999+00:00 | 1 | 42.0 MiB / 2.5 MiB | 1502.1 MB/s | 1/0 | 12 | 54.0s / 1,087,255 msg/s |
| Dekaf (3conn) | 2026-07-17T03:05:06.8739488+00:00 | 1 | 42.0 MiB / 18.3 MiB | 1611.5 MB/s | 1/0 | 54 | 81.1s / 1,436,510 msg/s |
| Dekaf (3conn) | 2026-07-17T03:05:33.8898269+00:00 | 1 | 36.0 MiB / 4.8 MiB | 1740.3 MB/s | 2/0 | 243 | 108.1s / 1,414,115 msg/s |
| Dekaf (3conn) | 2026-07-17T03:06:01.8983463+00:00 | 1 | 30.0 MiB / 4.0 MiB | 1740.3 MB/s | 3/0 | 349 | 136.1s / 1,378,495 msg/s |
| Dekaf (3conn) | 2026-07-17T03:06:28.9138252+00:00 | 1 | 30.0 MiB / 9.1 MiB | 1740.3 MB/s | 3/0 | 477 | 163.1s / 1,392,187 msg/s |
| Dekaf (3conn) | 2026-07-17T03:06:55.9343469+00:00 | 1 | 24.0 MiB / 2.4 MiB | 1740.3 MB/s | 4/0 | 692 | 190.1s / 1,457,466 msg/s |
| Dekaf (3conn) | 2026-07-17T03:07:22.9499047+00:00 | 1 | 24.0 MiB / 8.7 MiB | 1740.3 MB/s | 4/0 | 1,016 | 217.1s / 1,454,013 msg/s |
| Dekaf (3conn) | 2026-07-17T03:07:50.9616296+00:00 | 1 | 24.0 MiB / 9.2 MiB | 1740.3 MB/s | 4/1 | 1,379 | 245.1s / 1,348,951 msg/s |
| Dekaf (3conn) | 2026-07-17T03:08:17.9758062+00:00 | 1 | 27.0 MiB / 3.0 MiB | 1763.9 MB/s | 5/1 | 1,810 | 272.2s / 1,339,788 msg/s |
| Dekaf (3conn) | 2026-07-17T03:08:44.9879631+00:00 | 1 | 27.0 MiB / 17.7 MiB | 2183.6 MB/s | 5/1 | 2,686 | 299.2s / 1,926,955 msg/s |
| Dekaf (3conn) | 2026-07-17T03:09:12.0048975+00:00 | 1 | 27.0 MiB / 6.3 MiB | 2279.6 MB/s | 5/2 | 4,911 | 326.2s / 1,890,840 msg/s |
| Dekaf (3conn) | 2026-07-17T03:09:40.0228217+00:00 | 1 | 27.0 MiB / 8.5 MiB | 2290.7 MB/s | 5/2 | 6,502 | 354.2s / 1,786,027 msg/s |
| Dekaf (3conn) | 2026-07-17T03:10:07.0458493+00:00 | 1 | 27.0 MiB / 23.6 MiB | 2442.8 MB/s | 5/3 | 8,837 | 381.2s / 2,139,326 msg/s |
| Dekaf (3conn) | 2026-07-17T03:10:34.0530343+00:00 | 1 | 30.0 MiB / 12.2 MiB | 2576.2 MB/s | 6/3 | 10,785 | 408.2s / 1,971,487 msg/s |
| Dekaf (3conn) | 2026-07-17T03:11:02.0679044+00:00 | 1 | 33.0 MiB / 17.4 MiB | 2576.2 MB/s | 6/3 | 12,279 | 436.3s / 2,240,067 msg/s |
| Dekaf (3conn) | 2026-07-17T03:11:29.0799929+00:00 | 1 | 33.0 MiB / 7.0 MiB | 2576.2 MB/s | 7/3 | 13,412 | 463.3s / 2,243,179 msg/s |
| Dekaf (3conn) | 2026-07-17T03:11:56.0925102+00:00 | 1 | 33.0 MiB / 6.7 MiB | 2724.6 MB/s | 7/3 | 14,877 | 490.3s / 2,089,278 msg/s |
| Dekaf (3conn) | 2026-07-17T03:12:23.1084572+00:00 | 1 | 36.0 MiB / 4.3 MiB | 2724.6 MB/s | 8/3 | 16,127 | 517.3s / 1,779,353 msg/s |
| Dekaf (3conn) | 2026-07-17T03:12:51.1138005+00:00 | 1 | 39.0 MiB / 7.4 MiB | 2741.8 MB/s | 9/3 | 17,586 | 545.3s / 2,058,727 msg/s |
| Dekaf (3conn) | 2026-07-17T03:13:18.1315989+00:00 | 1 | 42.0 MiB / 2.8 MiB | 2741.8 MB/s | 9/3 | 18,900 | 572.3s / 2,025,897 msg/s |
| Dekaf (3conn) | 2026-07-17T03:13:45.1441133+00:00 | 1 | 39.0 MiB / 38.9 MiB | 2741.8 MB/s | 9/4 | 20,037 | 599.3s / 1,656,006 msg/s |
| Dekaf (3conn) | 2026-07-17T03:14:12.1592997+00:00 | 1 | 39.0 MiB / 39.0 MiB | 2741.8 MB/s | 9/4 | 21,025 | 626.4s / 1,521,816 msg/s |
| Dekaf (3conn) | 2026-07-17T03:14:40.1660627+00:00 | 1 | 33.0 MiB / 2.2 MiB | 2741.8 MB/s | 10/4 | 22,368 | 654.4s / 1,919,167 msg/s |
| Dekaf (3conn) | 2026-07-17T03:15:07.1728026+00:00 | 1 | 27.0 MiB / 16.6 MiB | 2831.2 MB/s | 11/4 | 23,727 | 681.4s / 2,073,542 msg/s |
| Dekaf (3conn) | 2026-07-17T03:15:34.1815466+00:00 | 1 | 24.0 MiB / 1.6 MiB | 2831.2 MB/s | 11/4 | 25,261 | 708.4s / 2,043,009 msg/s |
| Dekaf (3conn) | 2026-07-17T03:16:01.182522+00:00 | 1 | 27.0 MiB / 8.2 MiB | 2831.2 MB/s | 11/5 | 26,984 | 735.4s / 2,496,664 msg/s |
| Dekaf (3conn) | 2026-07-17T03:16:29.1998886+00:00 | 1 | 21.0 MiB / 13.6 MiB | 2831.2 MB/s | 11/5 | 29,085 | 763.4s / 2,053,433 msg/s |
| Dekaf (3conn) | 2026-07-17T03:16:56.2078046+00:00 | 1 | 27.0 MiB / 9.1 MiB | 2831.2 MB/s | 11/6 | 30,762 | 790.4s / 2,015,708 msg/s |
| Dekaf (3conn) | 2026-07-17T03:17:23.2183037+00:00 | 1 | 30.0 MiB / 6.2 MiB | 2831.2 MB/s | 12/6 | 32,395 | 817.5s / 2,171,964 msg/s |
| Dekaf (3conn) | 2026-07-17T03:17:50.2408182+00:00 | 1 | 33.0 MiB / 7.0 MiB | 2831.2 MB/s | 12/6 | 33,925 | 844.5s / 2,254,279 msg/s |
| Dekaf (3conn) | 2026-07-17T03:18:18.2500331+00:00 | 1 | 33.0 MiB / 14.9 MiB | 2831.2 MB/s | 13/6 | 35,216 | 872.5s / 2,128,073 msg/s |
| Dekaf (3conn) | 2026-07-17T03:18:45.2654155+00:00 | 1 | 36.0 MiB / 12.4 MiB | 2831.2 MB/s | 13/6 | 36,461 | 899.5s / 2,182,258 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-17T02:19:13.9580863+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.6 MiB |
| Dekaf | 2026-07-17T02:19:28.9751496+00:00 | 1 | capacity | succeeded | 15,017ms | 14.0 MiB / 11.8 MiB |
| Dekaf | 2026-07-17T02:19:59.0043772+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:20:44.0585399+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:20:59.0759892+00:00 | 1 | capacity | failed | 15,017ms | 12.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-17T02:21:29.1056827+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.6 MiB |
| Dekaf | 2026-07-17T02:21:44.1180367+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:22:14.1403334+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:22:29.151423+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:23:14.1895399+00:00 | 1 | capacity | failed | 15,014ms | 14.0 MiB / 13.9 MiB |
| Dekaf | 2026-07-17T02:23:44.2168134+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:23:59.2291785+00:00 | 1 | capacity | succeeded | 15,012ms | 12.0 MiB / 10.8 MiB |
| Dekaf | 2026-07-17T02:24:29.2567285+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:24:44.2711818+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:25:14.2958394+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:25:59.32879+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:26:14.3386983+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.0 MiB |
| Dekaf | 2026-07-17T02:26:44.3592028+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 9.9 MiB |
| Dekaf | 2026-07-17T02:26:59.3675145+00:00 | 1 | capacity | failed | 15,008ms | 12.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-17T02:27:29.3923625+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:27:44.4019067+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:28:29.4367497+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-17T02:28:59.4596316+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-17T02:29:14.4711048+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:29:44.4919784+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:29:59.5192844+00:00 | 1 | capacity | failed | 15,027ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:30:29.5494184+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-17T02:31:14.5847382+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:31:29.5988146+00:00 | 1 | capacity | succeeded | 15,014ms | 13.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-17T02:31:59.6278807+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:32:14.6366898+00:00 | 1 | capacity | failed | 15,008ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:32:44.6645824+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:32:59.674019+00:00 | 1 | capacity | failed | 15,009ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:34:14.7975134+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.4 MiB |
| Dekaf | 2026-07-17T02:34:29.8132203+00:00 | 1 | capacity | failed | 15,015ms | 16.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-17T02:34:59.8490865+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:35:14.865129+00:00 | 1 | capacity | failed | 15,016ms | 16.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-17T02:35:44.8882011+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-17T02:36:29.9282012+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:36:44.9398299+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:37:14.9643177+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:37:29.9778314+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 6.0 MiB |
| Dekaf | 2026-07-17T02:37:59.9985614+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-17T02:38:15.0230339+00:00 | 1 | capacity | succeeded | 15,024ms | 13.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-17T02:39:00.0656877+00:00 | 1 | capacity | succeeded | 15,012ms | 14.0 MiB / 11.4 MiB |
| Dekaf | 2026-07-17T02:39:30.0934094+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-17T02:39:45.1072861+00:00 | 1 | capacity | failed | 15,014ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:40:15.132156+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:40:30.1466696+00:00 | 1 | capacity | succeeded | 15,014ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:41:00.171206+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:41:45.2112941+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:42:00.2206365+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:42:30.2438021+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.3 MiB |
| Dekaf | 2026-07-17T02:42:45.2552708+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 8.8 MiB |
| Dekaf | 2026-07-17T02:43:15.2803358+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-17T02:43:30.2907108+00:00 | 1 | capacity | succeeded | 15,010ms | 13.0 MiB / 11.9 MiB |
| Dekaf | 2026-07-17T02:44:15.3265061+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:44:45.3490484+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:45:00.3636763+00:00 | 1 | capacity | failed | 15,014ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:45:30.3879452+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 14.0 MiB |
| Dekaf | 2026-07-17T02:45:45.3969871+00:00 | 1 | capacity | succeeded | 15,010ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:46:15.4193787+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-17T02:47:00.4562478+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:47:15.4697489+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-17T02:47:45.4918652+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:48:00.507292+00:00 | 1 | capacity | failed | 15,015ms | 12.0 MiB / 9.0 MiB |
| Dekaf | 2026-07-17T02:48:30.5548281+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:04:31.0265799+00:00 | 1 | capacity | succeeded | 15,034ms | 42.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:05:01.0690013+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-17T03:05:16.0945838+00:00 | 1 | capacity | succeeded | 15,025ms | 36.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-17T03:05:46.1462815+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:06:01.1733751+00:00 | 1 | capacity | succeeded | 15,027ms | 30.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-17T03:06:31.2319858+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 6.0 MiB |
| Dekaf (3conn) | 2026-07-17T03:07:16.3118292+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-17T03:07:31.3263856+00:00 | 1 | capacity | failed | 15,014ms | 24.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-17T03:08:01.5951088+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 21.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:08:16.6184385+00:00 | 1 | capacity | succeeded | 15,025ms | 27.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:08:46.6735017+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:09:01.6932229+00:00 | 1 | capacity | failed | 15,019ms | 27.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-17T03:09:46.7758596+00:00 | 1 | capacity | failed | 15,028ms | 27.0 MiB / 6.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:10:16.8203786+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 7.9 MiB |
| Dekaf (3conn) | 2026-07-17T03:10:31.847489+00:00 | 1 | capacity | succeeded | 15,027ms | 30.0 MiB / 7.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:11:01.9019112+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 7.3 MiB |
| Dekaf (3conn) | 2026-07-17T03:11:16.9281626+00:00 | 1 | capacity | succeeded | 15,026ms | 33.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-17T03:11:46.9685315+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 18.8 MiB |
| Dekaf (3conn) | 2026-07-17T03:12:32.0618588+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 11.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:12:47.0948592+00:00 | 1 | capacity | succeeded | 15,033ms | 39.0 MiB / 6.0 MiB |
| Dekaf (3conn) | 2026-07-17T03:13:17.1538889+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 13.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:13:32.1734377+00:00 | 1 | capacity | failed | 15,020ms | 39.0 MiB / 4.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:14:02.2369258+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:14:17.2983927+00:00 | 1 | capacity | succeeded | 15,061ms | 33.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:15:02.4353006+00:00 | 1 | capacity | succeeded | 15,026ms | 27.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-17T03:15:32.4674601+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:15:47.4890088+00:00 | 1 | capacity | failed | 15,021ms | 27.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:16:17.560205+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 25.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:16:32.585637+00:00 | 1 | capacity | failed | 15,025ms | 27.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:17:02.6295054+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-17T03:17:47.7164243+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:18:02.7414397+00:00 | 1 | capacity | succeeded | 15,025ms | 33.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:18:32.8952587+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 5.1 MiB |
*17 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,849 |
| Dekaf | 1 | 0.002–0.004ms | 2,243 |
| Dekaf | 1 | 0.004–0.008ms | 8,570 |
| Dekaf | 1 | 0.008–0.016ms | 41,280 |
| Dekaf | 1 | 0.016–0.032ms | 42,570 |
| Dekaf | 1 | 0.032–0.064ms | 49,476 |
| Dekaf | 1 | 0.064–0.128ms | 87,794 |
| Dekaf | 1 | 0.128–0.256ms | 271,445 |
| Dekaf | 1 | 0.256–0.512ms | 313,566 |
| Dekaf | 1 | 0.512–1.024ms | 72,993 |
| Dekaf | 1 | 1.024–2.048ms | 16,694 |
| Dekaf | 1 | 2.048–4.096ms | 4,059 |
| Dekaf | 1 | 4.096–8.192ms | 800 |
| Dekaf | 1 | 8.192–16.384ms | 49 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,870 |
| Dekaf | 1 | 0.002–0.004ms | 2,496 |
| Dekaf | 1 | 0.004–0.008ms | 7,367 |
| Dekaf | 1 | 0.008–0.016ms | 38,869 |
| Dekaf | 1 | 0.016–0.032ms | 42,650 |
| Dekaf | 1 | 0.032–0.064ms | 50,772 |
| Dekaf | 1 | 0.064–0.128ms | 91,878 |
| Dekaf | 1 | 0.128–0.256ms | 269,407 |
| Dekaf | 1 | 0.256–0.512ms | 302,547 |
| Dekaf | 1 | 0.512–1.024ms | 61,405 |
| Dekaf | 1 | 1.024–2.048ms | 13,402 |
| Dekaf | 1 | 2.048–4.096ms | 3,906 |
| Dekaf | 1 | 4.096–8.192ms | 697 |
| Dekaf | 1 | 8.192–16.384ms | 32 |
| Dekaf | 1 | 16.384–32.768ms | 3 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 15 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 11 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 47 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 126 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 225 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 490 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 862 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 1,852 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 2,472 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 2,155 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 1,217 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 724 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 329 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 50 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 6 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 4 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 3 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 11 |

## Delivery Latency Outliers - Producer (Fire-and-Forget)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 295,071,000 | 2026-07-17T02:52:22.9510257+00:00 | 106.0ms | GC pause | - | - | 218.1s / 1,389,416 msg/s | Gen2 +0 / pause +74.7ms |
| Confluent | 295,068,000 | 2026-07-17T02:52:22.9513375+00:00 | 105.5ms | GC pause | - | - | 218.1s / 1,389,416 msg/s | Gen2 +0 / pause +74.7ms |
| Confluent | 295,077,000 | 2026-07-17T02:52:22.9564173+00:00 | 104.1ms | GC pause | - | - | 219.2s / 1,325,589 msg/s | Gen2 +0 / pause +145.9ms |
| Confluent | 295,078,000 | 2026-07-17T02:52:22.957401+00:00 | 103.1ms | GC pause | - | - | 219.2s / 1,325,589 msg/s | Gen2 +0 / pause +145.9ms |
| Confluent | 295,081,000 | 2026-07-17T02:52:22.9601994+00:00 | 105.0ms | GC pause | - | - | 219.2s / 1,325,589 msg/s | Gen2 +0 / pause +145.9ms |
| Confluent | 295,087,000 | 2026-07-17T02:52:22.9660903+00:00 | 103.5ms | GC pause | - | - | 219.2s / 1,325,589 msg/s | Gen2 +0 / pause +145.9ms |
| Confluent | 295,088,000 | 2026-07-17T02:52:22.9670344+00:00 | 102.6ms | GC pause | - | - | 219.2s / 1,325,589 msg/s | Gen2 +0 / pause +145.9ms |
| Confluent | 295,091,000 | 2026-07-17T02:52:22.9691609+00:00 | 104.4ms | GC pause | - | - | 219.2s / 1,325,589 msg/s | Gen2 +0 / pause +145.9ms |
| Confluent | 624,854,000 | 2026-07-17T02:56:55.1333384+00:00 | 130.9ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,859,000 | 2026-07-17T02:56:55.1404908+00:00 | 130.0ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,863,000 | 2026-07-17T02:56:55.148009+00:00 | 116.2ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,864,000 | 2026-07-17T02:56:55.1501256+00:00 | 121.7ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,865,000 | 2026-07-17T02:56:55.1512573+00:00 | 122.3ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,866,000 | 2026-07-17T02:56:55.1521249+00:00 | 121.5ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,869,000 | 2026-07-17T02:56:55.1538497+00:00 | 119.8ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,870,000 | 2026-07-17T02:56:55.1543984+00:00 | 117.2ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,871,000 | 2026-07-17T02:56:55.1612026+00:00 | 103.7ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,872,000 | 2026-07-17T02:56:55.1617541+00:00 | 108.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,873,000 | 2026-07-17T02:56:55.1622785+00:00 | 110.4ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,874,000 | 2026-07-17T02:56:55.1627549+00:00 | 133.8ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,875,000 | 2026-07-17T02:56:55.1731445+00:00 | 116.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,876,000 | 2026-07-17T02:56:55.1742461+00:00 | 122.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,879,000 | 2026-07-17T02:56:55.1785683+00:00 | 118.5ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,884,000 | 2026-07-17T02:56:55.1925996+00:00 | 117.0ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,885,000 | 2026-07-17T02:56:55.1932186+00:00 | 115.5ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,886,000 | 2026-07-17T02:56:55.1949111+00:00 | 113.8ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,889,000 | 2026-07-17T02:56:55.1969129+00:00 | 113.0ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,894,000 | 2026-07-17T02:56:55.209214+00:00 | 107.9ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,895,000 | 2026-07-17T02:56:55.2097996+00:00 | 107.4ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,896,000 | 2026-07-17T02:56:55.2103926+00:00 | 106.8ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,899,000 | 2026-07-17T02:56:55.2132889+00:00 | 107.0ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,904,000 | 2026-07-17T02:56:55.2166588+00:00 | 120.9ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,905,000 | 2026-07-17T02:56:55.2171339+00:00 | 104.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,906,000 | 2026-07-17T02:56:55.2186842+00:00 | 119.0ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,909,000 | 2026-07-17T02:56:55.2202427+00:00 | 117.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,914,000 | 2026-07-17T02:56:55.225842+00:00 | 138.8ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,915,000 | 2026-07-17T02:56:55.2312076+00:00 | 130.1ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,916,000 | 2026-07-17T02:56:55.2323769+00:00 | 129.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,919,000 | 2026-07-17T02:56:55.2504132+00:00 | 113.5ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,920,000 | 2026-07-17T02:56:55.2512807+00:00 | 109.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +357.3ms |
| Confluent | 624,924,000 | 2026-07-17T02:56:55.2678427+00:00 | 101.6ms | GC pause | - | - | 491.4s / 917,156 msg/s | Gen2 +0 / pause +182.4ms |
| Dekaf (3conn) | 15,644,000 | 2026-07-17T03:03:58.5313103+00:00 | 222.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,646,000 | 2026-07-17T03:03:58.5330795+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,651,000 | 2026-07-17T03:03:58.5364322+00:00 | 223.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,652,000 | 2026-07-17T03:03:58.5370712+00:00 | 222.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,654,000 | 2026-07-17T03:03:58.5377638+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,656,000 | 2026-07-17T03:03:58.5388502+00:00 | 221.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,660,000 | 2026-07-17T03:03:58.5416043+00:00 | 227.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,661,000 | 2026-07-17T03:03:58.5420094+00:00 | 218.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,662,000 | 2026-07-17T03:03:58.5431011+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,663,000 | 2026-07-17T03:03:58.5439347+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,664,000 | 2026-07-17T03:03:58.5444156+00:00 | 215.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,665,000 | 2026-07-17T03:03:58.5448618+00:00 | 224.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,666,000 | 2026-07-17T03:03:58.54511+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,667,000 | 2026-07-17T03:03:58.5453441+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,668,000 | 2026-07-17T03:03:58.5457888+00:00 | 224.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,669,000 | 2026-07-17T03:03:58.5463503+00:00 | 213.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,670,000 | 2026-07-17T03:03:58.5467285+00:00 | 223.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,671,000 | 2026-07-17T03:03:58.5471392+00:00 | 218.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,672,000 | 2026-07-17T03:03:58.5473894+00:00 | 218.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,673,000 | 2026-07-17T03:03:58.5476334+00:00 | 212.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,674,000 | 2026-07-17T03:03:58.5478942+00:00 | 212.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,675,000 | 2026-07-17T03:03:58.5487765+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,676,000 | 2026-07-17T03:03:58.5492192+00:00 | 210.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,677,000 | 2026-07-17T03:03:58.5496495+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,678,000 | 2026-07-17T03:03:58.5499241+00:00 | 221.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,679,000 | 2026-07-17T03:03:58.5501802+00:00 | 209.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,680,000 | 2026-07-17T03:03:58.5504336+00:00 | 223.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,681,000 | 2026-07-17T03:03:58.5511689+00:00 | 215.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,682,000 | 2026-07-17T03:03:58.5517314+00:00 | 214.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,683,000 | 2026-07-17T03:03:58.5521377+00:00 | 207.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,684,000 | 2026-07-17T03:03:58.5523699+00:00 | 208.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,685,000 | 2026-07-17T03:03:58.5526082+00:00 | 221.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,686,000 | 2026-07-17T03:03:58.5528611+00:00 | 208.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,687,000 | 2026-07-17T03:03:58.5534714+00:00 | 212.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,688,000 | 2026-07-17T03:03:58.5542422+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,689,000 | 2026-07-17T03:03:58.5547509+00:00 | 205.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,690,000 | 2026-07-17T03:03:58.5551013+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,691,000 | 2026-07-17T03:03:58.5554113+00:00 | 212.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,692,000 | 2026-07-17T03:03:58.5557278+00:00 | 211.7ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,693,000 | 2026-07-17T03:03:58.5565457+00:00 | 203.5ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,694,000 | 2026-07-17T03:03:58.5572185+00:00 | 205.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,695,000 | 2026-07-17T03:03:58.5583699+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,696,000 | 2026-07-17T03:03:58.5590698+00:00 | 203.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,697,000 | 2026-07-17T03:03:58.559621+00:00 | 207.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,698,000 | 2026-07-17T03:03:58.5601845+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,699,000 | 2026-07-17T03:03:58.5610878+00:00 | 200.2ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,700,000 | 2026-07-17T03:03:58.5620477+00:00 | 214.8ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,701,000 | 2026-07-17T03:03:58.5631619+00:00 | 207.4ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,702,000 | 2026-07-17T03:03:58.5637025+00:00 | 206.9ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 15,703,000 | 2026-07-17T03:03:58.5642606+00:00 | 197.0ms | broker/backlog (no scale or GC event) | - | - | 13.0s / 1,072,046 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 344,094,000 | 2026-07-17T03:07:59.8679847+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,096,000 | 2026-07-17T03:07:59.8685951+00:00 | 224.1ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,098,000 | 2026-07-17T03:07:59.8693781+00:00 | 227.0ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,100,000 | 2026-07-17T03:07:59.8713235+00:00 | 225.6ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,101,000 | 2026-07-17T03:07:59.8715836+00:00 | 222.1ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,102,000 | 2026-07-17T03:07:59.8720068+00:00 | 221.7ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,103,000 | 2026-07-17T03:07:59.8725586+00:00 | 218.6ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,104,000 | 2026-07-17T03:07:59.8728104+00:00 | 220.9ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,105,000 | 2026-07-17T03:07:59.8730534+00:00 | 224.4ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,106,000 | 2026-07-17T03:07:59.8737373+00:00 | 220.0ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,107,000 | 2026-07-17T03:07:59.8740161+00:00 | 220.3ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,108,000 | 2026-07-17T03:07:59.8742766+00:00 | 223.2ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,109,000 | 2026-07-17T03:07:59.8748518+00:00 | 217.9ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,110,000 | 2026-07-17T03:07:59.8752545+00:00 | 223.5ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,111,000 | 2026-07-17T03:07:59.8755131+00:00 | 218.8ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,112,000 | 2026-07-17T03:07:59.8762392+00:00 | 219.0ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,113,000 | 2026-07-17T03:07:59.8765003+00:00 | 216.3ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,114,000 | 2026-07-17T03:07:59.8767679+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,115,000 | 2026-07-17T03:07:59.8774062+00:00 | 221.4ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,116,000 | 2026-07-17T03:07:59.8778405+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,117,000 | 2026-07-17T03:07:59.8780974+00:00 | 216.9ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,118,000 | 2026-07-17T03:07:59.8786723+00:00 | 220.1ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,119,000 | 2026-07-17T03:07:59.8790969+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 344,120,000 | 2026-07-17T03:07:59.8793545+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 254.1s / 1,153,326 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 592,321,000 | 2026-07-17T03:10:20.3901225+00:00 | 220.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,322,000 | 2026-07-17T03:10:20.3907351+00:00 | 222.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,324,000 | 2026-07-17T03:10:20.3913732+00:00 | 219.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,326,000 | 2026-07-17T03:10:20.3920268+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,331,000 | 2026-07-17T03:10:20.3935522+00:00 | 221.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,332,000 | 2026-07-17T03:10:20.3937392+00:00 | 221.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,334,000 | 2026-07-17T03:10:20.3947508+00:00 | 227.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,336,000 | 2026-07-17T03:10:20.3952723+00:00 | 226.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,341,000 | 2026-07-17T03:10:20.4001306+00:00 | 221.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,342,000 | 2026-07-17T03:10:20.4003264+00:00 | 221.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,344,000 | 2026-07-17T03:10:20.4007124+00:00 | 221.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,346,000 | 2026-07-17T03:10:20.4036488+00:00 | 218.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,347,000 | 2026-07-17T03:10:20.4039968+00:00 | 211.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,348,000 | 2026-07-17T03:10:20.4043329+00:00 | 206.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,349,000 | 2026-07-17T03:10:20.4045249+00:00 | 205.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,350,000 | 2026-07-17T03:10:20.4047178+00:00 | 217.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,351,000 | 2026-07-17T03:10:20.404917+00:00 | 218.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,352,000 | 2026-07-17T03:10:20.4056216+00:00 | 217.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,353,000 | 2026-07-17T03:10:20.4059481+00:00 | 204.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,354,000 | 2026-07-17T03:10:20.4062661+00:00 | 216.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,355,000 | 2026-07-17T03:10:20.4064613+00:00 | 215.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,356,000 | 2026-07-17T03:10:20.4066617+00:00 | 216.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,357,000 | 2026-07-17T03:10:20.4068633+00:00 | 211.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,358,000 | 2026-07-17T03:10:20.4074447+00:00 | 214.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,359,000 | 2026-07-17T03:10:20.4081745+00:00 | 202.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,360,000 | 2026-07-17T03:10:20.4082285+00:00 | 214.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,361,000 | 2026-07-17T03:10:20.4084237+00:00 | 214.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,362,000 | 2026-07-17T03:10:20.4086134+00:00 | 214.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,363,000 | 2026-07-17T03:10:20.408819+00:00 | 201.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,364,000 | 2026-07-17T03:10:20.4092732+00:00 | 213.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,365,000 | 2026-07-17T03:10:20.4098674+00:00 | 212.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,366,000 | 2026-07-17T03:10:20.4101834+00:00 | 213.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 592,367,000 | 2026-07-17T03:10:20.4421219+00:00 | 179.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 395.2s / 1,975,706 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,017,605,000 | 2026-07-17T03:13:44.9336183+00:00 | 219.4ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,608,000 | 2026-07-17T03:13:44.934669+00:00 | 218.4ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,610,000 | 2026-07-17T03:13:44.936923+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,611,000 | 2026-07-17T03:13:44.9390893+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,612,000 | 2026-07-17T03:13:44.940354+00:00 | 227.4ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,613,000 | 2026-07-17T03:13:44.9416112+00:00 | 214.6ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,614,000 | 2026-07-17T03:13:44.9418127+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,615,000 | 2026-07-17T03:13:44.9420044+00:00 | 225.9ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,616,000 | 2026-07-17T03:13:44.9435204+00:00 | 216.0ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,617,000 | 2026-07-17T03:13:44.9438752+00:00 | 223.8ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,618,000 | 2026-07-17T03:13:44.9442124+00:00 | 223.7ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,619,000 | 2026-07-17T03:13:44.9445469+00:00 | 212.7ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,620,000 | 2026-07-17T03:13:44.9447443+00:00 | 223.1ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,621,000 | 2026-07-17T03:13:44.9449592+00:00 | 222.8ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,622,000 | 2026-07-17T03:13:44.9452874+00:00 | 222.4ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,623,000 | 2026-07-17T03:13:44.9457524+00:00 | 211.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,624,000 | 2026-07-17T03:13:44.9462297+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,625,000 | 2026-07-17T03:13:44.9465534+00:00 | 221.3ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,626,000 | 2026-07-17T03:13:44.9467447+00:00 | 216.0ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,627,000 | 2026-07-17T03:13:44.9469489+00:00 | 220.8ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,628,000 | 2026-07-17T03:13:44.9472787+00:00 | 220.6ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,629,000 | 2026-07-17T03:13:44.9477422+00:00 | 211.8ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,630,000 | 2026-07-17T03:13:44.9482303+00:00 | 219.6ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,631,000 | 2026-07-17T03:13:44.9485666+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,632,000 | 2026-07-17T03:13:44.9487671+00:00 | 219.1ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,633,000 | 2026-07-17T03:13:44.9489916+00:00 | 210.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,634,000 | 2026-07-17T03:13:44.9493476+00:00 | 213.4ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,635,000 | 2026-07-17T03:13:44.9497016+00:00 | 221.0ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,636,000 | 2026-07-17T03:13:44.9503914+00:00 | 212.3ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,637,000 | 2026-07-17T03:13:44.9507167+00:00 | 217.2ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,638,000 | 2026-07-17T03:13:44.9509139+00:00 | 219.8ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,639,000 | 2026-07-17T03:13:44.9511065+00:00 | 211.6ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,640,000 | 2026-07-17T03:13:44.9514241+00:00 | 219.2ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,641,000 | 2026-07-17T03:13:44.951615+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,642,000 | 2026-07-17T03:13:44.952336+00:00 | 215.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,643,000 | 2026-07-17T03:13:44.9526617+00:00 | 210.2ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,644,000 | 2026-07-17T03:13:44.9528593+00:00 | 214.9ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,645,000 | 2026-07-17T03:13:44.9530533+00:00 | 217.6ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,646,000 | 2026-07-17T03:13:44.9533768+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,647,000 | 2026-07-17T03:13:44.9535742+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,017,648,000 | 2026-07-17T03:13:44.9541772+00:00 | 216.5ms | broker/backlog (no scale or GC event) | - | - | 599.3s / 1,656,006 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,065,135,000 | 2026-07-17T03:14:07.1748029+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,137,000 | 2026-07-17T03:14:07.1753037+00:00 | 221.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,138,000 | 2026-07-17T03:14:07.1754892+00:00 | 221.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,145,000 | 2026-07-17T03:14:07.1789687+00:00 | 220.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,147,000 | 2026-07-17T03:14:07.1797494+00:00 | 219.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,148,000 | 2026-07-17T03:14:07.1799696+00:00 | 219.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,155,000 | 2026-07-17T03:14:07.1823479+00:00 | 222.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,157,000 | 2026-07-17T03:14:07.1830593+00:00 | 216.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,158,000 | 2026-07-17T03:14:07.183798+00:00 | 221.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,160,000 | 2026-07-17T03:14:07.184338+00:00 | 220.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,165,000 | 2026-07-17T03:14:07.1860944+00:00 | 218.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,167,000 | 2026-07-17T03:14:07.1864755+00:00 | 218.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,168,000 | 2026-07-17T03:14:07.1868319+00:00 | 217.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,170,000 | 2026-07-17T03:14:07.1876073+00:00 | 217.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,174,000 | 2026-07-17T03:14:07.1887749+00:00 | 207.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,175,000 | 2026-07-17T03:14:07.1891108+00:00 | 218.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,176,000 | 2026-07-17T03:14:07.2233131+00:00 | 173.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,177,000 | 2026-07-17T03:14:07.2236797+00:00 | 181.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,178,000 | 2026-07-17T03:14:07.2240675+00:00 | 183.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,179,000 | 2026-07-17T03:14:07.2242974+00:00 | 169.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,180,000 | 2026-07-17T03:14:07.224722+00:00 | 182.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,181,000 | 2026-07-17T03:14:07.2253947+00:00 | 171.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,182,000 | 2026-07-17T03:14:07.2260351+00:00 | 173.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,183,000 | 2026-07-17T03:14:07.2262206+00:00 | 172.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,184,000 | 2026-07-17T03:14:07.2266966+00:00 | 169.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,185,000 | 2026-07-17T03:14:07.2640855+00:00 | 145.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,186,000 | 2026-07-17T03:14:07.2644759+00:00 | 134.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,187,000 | 2026-07-17T03:14:07.2649478+00:00 | 142.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,188,000 | 2026-07-17T03:14:07.2656452+00:00 | 143.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,189,000 | 2026-07-17T03:14:07.2658592+00:00 | 133.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,190,000 | 2026-07-17T03:14:07.2662118+00:00 | 141.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,191,000 | 2026-07-17T03:14:07.2666299+00:00 | 132.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,192,000 | 2026-07-17T03:14:07.2668553+00:00 | 132.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,193,000 | 2026-07-17T03:14:07.2673243+00:00 | 131.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,194,000 | 2026-07-17T03:14:07.2678121+00:00 | 131.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,195,000 | 2026-07-17T03:14:07.268006+00:00 | 141.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,196,000 | 2026-07-17T03:14:07.2682103+00:00 | 130.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,065,197,000 | 2026-07-17T03:14:07.2685496+00:00 | 139.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 622.4s / 1,976,217 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,074,125,000 | 2026-07-17T03:14:12.0716027+00:00 | 219.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,127,000 | 2026-07-17T03:14:12.0721777+00:00 | 219.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,128,000 | 2026-07-17T03:14:12.0724481+00:00 | 218.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,135,000 | 2026-07-17T03:14:12.0767533+00:00 | 216.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,137,000 | 2026-07-17T03:14:12.0780367+00:00 | 215.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,138,000 | 2026-07-17T03:14:12.0782953+00:00 | 217.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,145,000 | 2026-07-17T03:14:12.0809801+00:00 | 222.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,147,000 | 2026-07-17T03:14:12.0815587+00:00 | 214.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,148,000 | 2026-07-17T03:14:12.0820966+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,150,000 | 2026-07-17T03:14:12.0830308+00:00 | 229.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,155,000 | 2026-07-17T03:14:12.084681+00:00 | 220.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,157,000 | 2026-07-17T03:14:12.085207+00:00 | 212.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,158,000 | 2026-07-17T03:14:12.0853952+00:00 | 219.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,160,000 | 2026-07-17T03:14:12.0859215+00:00 | 227.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,165,000 | 2026-07-17T03:14:12.0875407+00:00 | 222.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,167,000 | 2026-07-17T03:14:12.121304+00:00 | 180.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,168,000 | 2026-07-17T03:14:12.1217176+00:00 | 188.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,170,000 | 2026-07-17T03:14:12.1224588+00:00 | 190.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 627.4s / 1,983,547 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,074,175,000 | 2026-07-17T03:14:12.1252537+00:00 | 184.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,176,000 | 2026-07-17T03:14:12.1255562+00:00 | 165.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,177,000 | 2026-07-17T03:14:12.1257782+00:00 | 176.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,178,000 | 2026-07-17T03:14:12.1262483+00:00 | 183.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,179,000 | 2026-07-17T03:14:12.126925+00:00 | 162.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,180,000 | 2026-07-17T03:14:12.1272165+00:00 | 185.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 627.4s / 1,983,547 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,074,181,000 | 2026-07-17T03:14:12.1276946+00:00 | 163.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,182,000 | 2026-07-17T03:14:12.1281098+00:00 | 163.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,183,000 | 2026-07-17T03:14:12.1283714+00:00 | 161.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,184,000 | 2026-07-17T03:14:12.12876+00:00 | 164.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,185,000 | 2026-07-17T03:14:12.1293624+00:00 | 183.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,186,000 | 2026-07-17T03:14:12.1295872+00:00 | 164.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,187,000 | 2026-07-17T03:14:12.1299159+00:00 | 173.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,188,000 | 2026-07-17T03:14:12.1624069+00:00 | 150.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,189,000 | 2026-07-17T03:14:12.1626074+00:00 | 130.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,190,000 | 2026-07-17T03:14:12.1631778+00:00 | 149.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 627.4s / 1,983,547 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf (3conn) | 1,074,191,000 | 2026-07-17T03:14:12.163852+00:00 | 132.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,192,000 | 2026-07-17T03:14:12.1640305+00:00 | 131.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,074,193,000 | 2026-07-17T03:14:12.1642091+00:00 | 131.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 626.4s / 1,521,816 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,101,342,000 | 2026-07-17T03:14:25.1128878+00:00 | 219.3ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,344,000 | 2026-07-17T03:14:25.1134597+00:00 | 215.3ms | broker/backlog (no scale or GC event) | - | - | 639.4s / 1,728,998 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,101,345,000 | 2026-07-17T03:14:25.1141469+00:00 | 217.8ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,346,000 | 2026-07-17T03:14:25.1145933+00:00 | 217.7ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,347,000 | 2026-07-17T03:14:25.1161003+00:00 | 215.9ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,348,000 | 2026-07-17T03:14:25.1174295+00:00 | 224.8ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,349,000 | 2026-07-17T03:14:25.1176845+00:00 | 213.4ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,350,000 | 2026-07-17T03:14:25.117945+00:00 | 224.3ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,351,000 | 2026-07-17T03:14:25.1183174+00:00 | 216.8ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,352,000 | 2026-07-17T03:14:25.1197306+00:00 | 215.4ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,353,000 | 2026-07-17T03:14:25.1200691+00:00 | 211.9ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,354,000 | 2026-07-17T03:14:25.1205225+00:00 | 211.8ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,355,000 | 2026-07-17T03:14:25.1207143+00:00 | 221.5ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,356,000 | 2026-07-17T03:14:25.1209303+00:00 | 211.5ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,357,000 | 2026-07-17T03:14:25.1212552+00:00 | 214.3ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,358,000 | 2026-07-17T03:14:25.1217225+00:00 | 220.5ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,359,000 | 2026-07-17T03:14:25.1220666+00:00 | 210.0ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,360,000 | 2026-07-17T03:14:25.1225336+00:00 | 219.7ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,361,000 | 2026-07-17T03:14:25.1227369+00:00 | 216.1ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,362,000 | 2026-07-17T03:14:25.1229455+00:00 | 215.9ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,363,000 | 2026-07-17T03:14:25.1232756+00:00 | 208.9ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,364,000 | 2026-07-17T03:14:25.1236118+00:00 | 209.0ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,365,000 | 2026-07-17T03:14:25.1240773+00:00 | 218.2ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,366,000 | 2026-07-17T03:14:25.1244021+00:00 | 210.7ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,367,000 | 2026-07-17T03:14:25.1247217+00:00 | 217.5ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,369,000 | 2026-07-17T03:14:25.1252378+00:00 | 209.7ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,370,000 | 2026-07-17T03:14:25.1255608+00:00 | 216.7ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,371,000 | 2026-07-17T03:14:25.1260145+00:00 | 216.2ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,372,000 | 2026-07-17T03:14:25.1263387+00:00 | 215.9ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,373,000 | 2026-07-17T03:14:25.1266575+00:00 | 208.3ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,374,000 | 2026-07-17T03:14:25.1268596+00:00 | 208.2ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,376,000 | 2026-07-17T03:14:25.1275043+00:00 | 207.6ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 1,101,377,000 | 2026-07-17T03:14:25.1278372+00:00 | 214.4ms | broker/backlog (no scale or GC event) | - | - | 640.4s / 1,985,630 msg/s | Gen2 +0 / pause +1.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*213 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.48x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.22x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.34 | 1245.32 | 1,049,393 | 1,057,269 | +9.8% | +0.91% | 1000.78 | 1,049,393 | 0 | 1.41 |
| Dekaf | 1.28 | 1199.00 | 1,017,223 | 1,017,686 | -2.2% | -0.26% | 970.10 | 1,017,223 | 0 | 1.31 |
| Confluent | 1.95 | - | 762,061 | 747,092 | +4.8% | +0.23% | 726.76 | 762,061 | 0 | 1.49 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 326,433 | 362.70 | 924.24 KB |
| Dekaf | 2 | 322,543 | 358.37 | 926.36 KB |
| Dekaf | 3 | 331,733 | 368.58 | 933.34 KB |
| Dekaf (3conn) | 1 | 348,201 | 386.88 | 927.03 KB |
| Dekaf (3conn) | 2 | 333,405 | 370.44 | 924.52 KB |
| Dekaf (3conn) | 3 | 334,388 | 371.53 | 920.67 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:48.152227+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 413,897 msg/s |
| Dekaf | 2026-07-17T02:19:06.1659292+00:00 | 3 | 16.0 MiB / 3.2 MiB | 419.8 MB/s | 0/0 | 514 | 18.0s / 985,345 msg/s |
| Dekaf | 2026-07-17T02:19:25.1784861+00:00 | 1 | 16.0 MiB / 2.9 MiB | 458.4 MB/s | 0/0 | 1,779 | 37.0s / 1,026,975 msg/s |
| Dekaf | 2026-07-17T02:19:43.1941039+00:00 | 1 | 14.0 MiB / 3.5 MiB | 458.4 MB/s | 1/0 | 2,649 | 55.0s / 1,020,519 msg/s |
| Dekaf | 2026-07-17T02:20:01.2132198+00:00 | 2 | 12.0 MiB / 8.6 MiB | 467.6 MB/s | 2/0 | 2,369 | 73.1s / 1,091,863 msg/s |
| Dekaf | 2026-07-17T02:20:19.227175+00:00 | 2 | 12.0 MiB / 1.6 MiB | 467.6 MB/s | 2/0 | 3,132 | 91.1s / 1,037,059 msg/s |
| Dekaf | 2026-07-17T02:20:37.2471245+00:00 | 3 | 10.0 MiB / 2.4 MiB | 469.8 MB/s | 3/0 | 4,329 | 109.1s / 1,054,802 msg/s |
| Dekaf | 2026-07-17T02:20:55.2613461+00:00 | 3 | 10.0 MiB / 3.4 MiB | 469.8 MB/s | 3/1 | 5,070 | 127.1s / 1,055,658 msg/s |
| Dekaf | 2026-07-17T02:21:14.2905128+00:00 | 1 | 10.0 MiB / 1.4 MiB | 458.4 MB/s | 3/1 | 9,733 | 146.1s / 967,327 msg/s |
| Dekaf | 2026-07-17T02:21:32.3004049+00:00 | 1 | 10.0 MiB / 7.1 MiB | 458.4 MB/s | 3/1 | 10,895 | 164.2s / 1,183,349 msg/s |
| Dekaf | 2026-07-17T02:21:50.3083368+00:00 | 2 | 10.0 MiB / 7.5 MiB | 467.6 MB/s | 3/2 | 9,343 | 182.2s / 1,014,057 msg/s |
| Dekaf | 2026-07-17T02:22:08.3211476+00:00 | 2 | 10.0 MiB / 2.2 MiB | 467.6 MB/s | 3/2 | 10,183 | 200.2s / 1,037,650 msg/s |
| Dekaf | 2026-07-17T02:22:26.3318124+00:00 | 3 | 10.0 MiB / 1.5 MiB | 469.8 MB/s | 3/3 | 9,872 | 218.2s / 1,069,095 msg/s |
| Dekaf | 2026-07-17T02:22:44.3487828+00:00 | 3 | 10.0 MiB / 3.8 MiB | 469.8 MB/s | 3/3 | 10,916 | 236.2s / 1,015,798 msg/s |
| Dekaf | 2026-07-17T02:23:03.3659603+00:00 | 1 | 10.0 MiB / 3.7 MiB | 458.4 MB/s | 3/3 | 17,399 | 255.2s / 972,909 msg/s |
| Dekaf | 2026-07-17T02:23:21.3738495+00:00 | 1 | 10.0 MiB / 3.9 MiB | 458.4 MB/s | 3/4 | 18,083 | 273.2s / 980,481 msg/s |
| Dekaf | 2026-07-17T02:23:39.3857935+00:00 | 1 | 10.0 MiB / 1.1 MiB | 458.4 MB/s | 3/4 | 18,766 | 291.3s / 988,398 msg/s |
| Dekaf | 2026-07-17T02:23:57.396375+00:00 | 2 | 9.0 MiB / 5.4 MiB | 467.6 MB/s | 5/3 | 16,920 | 309.3s / 1,020,100 msg/s |
| Dekaf | 2026-07-17T02:24:15.4164392+00:00 | 2 | 9.0 MiB / 1.2 MiB | 467.6 MB/s | 5/3 | 17,803 | 327.3s / 1,058,873 msg/s |
| Dekaf | 2026-07-17T02:24:33.4240115+00:00 | 3 | 8.0 MiB / 1.2 MiB | 469.8 MB/s | 4/4 | 16,954 | 345.3s / 1,004,790 msg/s |
| Dekaf | 2026-07-17T02:24:51.4367562+00:00 | 3 | 9.0 MiB / 3.6 MiB | 469.8 MB/s | 5/4 | 17,942 | 363.3s / 1,028,965 msg/s |
| Dekaf | 2026-07-17T02:25:10.4557569+00:00 | 1 | 9.0 MiB / 7.9 MiB | 458.4 MB/s | 5/4 | 29,403 | 382.3s / 1,040,954 msg/s |
| Dekaf | 2026-07-17T02:25:28.4663183+00:00 | 1 | 9.0 MiB / 9.0 MiB | 458.4 MB/s | 5/5 | 31,428 | 400.4s / 992,669 msg/s |
| Dekaf | 2026-07-17T02:25:46.472764+00:00 | 2 | 10.0 MiB / 0.0 MiB | 467.6 MB/s | 6/4 | 21,062 | 418.4s / 1,049,520 msg/s |
| Dekaf | 2026-07-17T02:26:04.4786138+00:00 | 2 | 10.0 MiB / 10.0 MiB | 467.6 MB/s | 6/4 | 22,023 | 436.4s / 1,057,191 msg/s |
| Dekaf | 2026-07-17T02:26:22.4882248+00:00 | 3 | 9.0 MiB / 0.4 MiB | 469.8 MB/s | 5/6 | 23,339 | 454.4s / 977,783 msg/s |
| Dekaf | 2026-07-17T02:26:40.4992725+00:00 | 3 | 9.0 MiB / 0.8 MiB | 469.8 MB/s | 5/6 | 24,196 | 472.4s / 1,001,331 msg/s |
| Dekaf | 2026-07-17T02:26:59.5057104+00:00 | 1 | 8.0 MiB / 3.4 MiB | 458.4 MB/s | 6/6 | 40,220 | 491.4s / 1,033,846 msg/s |
| Dekaf | 2026-07-17T02:27:17.5282122+00:00 | 1 | 8.0 MiB / 3.2 MiB | 473.7 MB/s | 6/6 | 41,937 | 509.4s / 1,019,989 msg/s |
| Dekaf | 2026-07-17T02:27:35.5448826+00:00 | 1 | 8.0 MiB / 1.5 MiB | 473.7 MB/s | 6/6 | 43,518 | 527.5s / 883,976 msg/s |
| Dekaf | 2026-07-17T02:27:53.5598407+00:00 | 2 | 9.0 MiB / 0.8 MiB | 467.6 MB/s | 8/5 | 30,392 | 545.5s / 989,214 msg/s |
| Dekaf | 2026-07-17T02:28:11.5788252+00:00 | 2 | 9.0 MiB / 3.5 MiB | 467.6 MB/s | 8/5 | 31,367 | 563.5s / 1,077,217 msg/s |
| Dekaf | 2026-07-17T02:28:29.5948704+00:00 | 3 | 8.0 MiB / 2.3 MiB | 469.8 MB/s | 7/7 | 32,032 | 581.5s / 972,386 msg/s |
| Dekaf | 2026-07-17T02:28:47.6067467+00:00 | 3 | 8.0 MiB / 1.6 MiB | 469.8 MB/s | 7/7 | 34,367 | 599.5s / 1,051,588 msg/s |
| Dekaf | 2026-07-17T02:29:06.6300001+00:00 | 1 | 10.0 MiB / 7.9 MiB | 473.7 MB/s | 8/6 | 49,822 | 618.6s / 1,103,526 msg/s |
| Dekaf | 2026-07-17T02:29:24.6421205+00:00 | 1 | 10.0 MiB / 5.7 MiB | 473.7 MB/s | 8/7 | 50,716 | 636.6s / 986,836 msg/s |
| Dekaf | 2026-07-17T02:29:42.6557261+00:00 | 2 | 8.0 MiB / 2.5 MiB | 467.6 MB/s | 9/6 | 36,277 | 654.6s / 1,018,489 msg/s |
| Dekaf | 2026-07-17T02:30:00.6825775+00:00 | 2 | 10.0 MiB / 2.7 MiB | 467.6 MB/s | 9/7 | 37,189 | 672.6s / 971,877 msg/s |
| Dekaf | 2026-07-17T02:30:18.6951442+00:00 | 3 | 8.0 MiB / 2.4 MiB | 469.8 MB/s | 7/9 | 41,528 | 690.6s / 1,032,925 msg/s |
| Dekaf | 2026-07-17T02:30:36.7131291+00:00 | 3 | 8.0 MiB / 2.1 MiB | 469.8 MB/s | 7/9 | 43,240 | 708.7s / 1,046,286 msg/s |
| Dekaf | 2026-07-17T02:30:55.7362254+00:00 | 1 | 8.0 MiB / 5.6 MiB | 473.7 MB/s | 9/8 | 59,029 | 727.7s / 1,001,362 msg/s |
| Dekaf | 2026-07-17T02:31:13.7537413+00:00 | 1 | 9.0 MiB / 1.0 MiB | 473.7 MB/s | 9/8 | 60,921 | 745.7s / 1,026,633 msg/s |
| Dekaf | 2026-07-17T02:31:31.7688752+00:00 | 2 | 8.0 MiB / 2.4 MiB | 475.1 MB/s | 10/8 | 41,115 | 763.7s / 1,019,623 msg/s |
| Dekaf | 2026-07-17T02:31:49.7947649+00:00 | 2 | 8.0 MiB / 2.9 MiB | 475.1 MB/s | 10/8 | 42,353 | 781.7s / 950,404 msg/s |
| Dekaf | 2026-07-17T02:32:07.8035603+00:00 | 2 | 8.0 MiB / 1.1 MiB | 475.1 MB/s | 10/8 | 42,940 | 799.7s / 1,040,739 msg/s |
| Dekaf | 2026-07-17T02:32:25.8237038+00:00 | 3 | 10.0 MiB / 1.6 MiB | 479.1 MB/s | 9/10 | 49,406 | 817.8s / 1,014,867 msg/s |
| Dekaf | 2026-07-17T02:32:43.8365322+00:00 | 3 | 11.0 MiB / 0.9 MiB | 479.1 MB/s | 9/10 | 50,001 | 835.8s / 1,033,193 msg/s |
| Dekaf | 2026-07-17T02:33:02.858284+00:00 | 1 | 10.0 MiB / 0.7 MiB | 479.2 MB/s | 11/9 | 69,521 | 854.8s / 830,136 msg/s |
| Dekaf | 2026-07-17T02:33:20.8730809+00:00 | 1 | 10.0 MiB / 1.3 MiB | 479.2 MB/s | 11/9 | 69,993 | 872.8s / 920,436 msg/s |
| Dekaf | 2026-07-17T02:33:38.8837896+00:00 | 2 | 9.0 MiB / 3.2 MiB | 475.1 MB/s | 11/9 | 47,124 | 890.8s / 970,606 msg/s |
| Dekaf (3conn) | 2026-07-17T02:34:10.6156362+00:00 | 1 | 48.0 MiB / 8.4 MiB | 416.1 MB/s | 0/0 | 168 | 9.0s / 965,315 msg/s |
| Dekaf (3conn) | 2026-07-17T02:34:28.6324401+00:00 | 2 | 48.0 MiB / 28.2 MiB | 463.7 MB/s | 0/0 | 556 | 27.0s / 1,004,817 msg/s |
| Dekaf (3conn) | 2026-07-17T02:34:46.6681574+00:00 | 2 | 42.0 MiB / 6.1 MiB | 463.7 MB/s | 0/0 | 630 | 45.1s / 908,087 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:04.6904902+00:00 | 3 | 48.0 MiB / 2.1 MiB | 430.3 MB/s | 0/1 | 164 | 63.1s / 904,033 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:22.7076963+00:00 | 3 | 48.0 MiB / 3.2 MiB | 430.3 MB/s | 0/1 | 164 | 81.1s / 924,445 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:41.7415631+00:00 | 1 | 48.0 MiB / 1.3 MiB | 447.7 MB/s | 0/2 | 404 | 100.1s / 841,292 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:59.7638247+00:00 | 1 | 48.0 MiB / 5.2 MiB | 469.3 MB/s | 0/2 | 404 | 118.2s / 903,187 msg/s |
| Dekaf (3conn) | 2026-07-17T02:36:17.7749149+00:00 | 1 | 42.0 MiB / 2.7 MiB | 469.3 MB/s | 1/2 | 404 | 136.2s / 1,073,134 msg/s |
| Dekaf (3conn) | 2026-07-17T02:36:35.7883008+00:00 | 2 | 36.0 MiB / 2.3 MiB | 463.7 MB/s | 2/2 | 697 | 154.2s / 1,000,341 msg/s |
| Dekaf (3conn) | 2026-07-17T02:36:53.8407596+00:00 | 2 | 30.0 MiB / 3.0 MiB | 471.0 MB/s | 2/2 | 1,089 | 172.2s / 959,168 msg/s |
| Dekaf (3conn) | 2026-07-17T02:37:11.8487924+00:00 | 3 | 45.0 MiB / 1.2 MiB | 460.4 MB/s | 2/2 | 314 | 190.2s / 1,110,987 msg/s |
| Dekaf (3conn) | 2026-07-17T02:37:29.8706936+00:00 | 3 | 45.0 MiB / 2.7 MiB | 460.4 MB/s | 2/3 | 314 | 208.2s / 1,004,060 msg/s |
| Dekaf (3conn) | 2026-07-17T02:37:48.9311315+00:00 | 1 | 36.0 MiB / 1.2 MiB | 473.7 MB/s | 2/4 | 1,102 | 227.3s / 1,008,146 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:06.9597906+00:00 | 1 | 36.0 MiB / 1.9 MiB | 473.7 MB/s | 2/4 | 1,348 | 245.3s / 1,085,137 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:24.9977755+00:00 | 2 | 39.0 MiB / 7.4 MiB | 471.0 MB/s | 3/4 | 1,894 | 263.3s / 1,086,345 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:43.0029528+00:00 | 2 | 39.0 MiB / 6.6 MiB | 471.0 MB/s | 3/4 | 2,078 | 281.4s / 913,676 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:01.0108906+00:00 | 3 | 39.0 MiB / 2.4 MiB | 460.4 MB/s | 3/4 | 368 | 299.4s / 1,017,522 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:19.0240432+00:00 | 3 | 39.0 MiB / 2.9 MiB | 460.4 MB/s | 3/5 | 368 | 317.4s / 995,461 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:38.0347564+00:00 | 1 | 36.0 MiB / 2.2 MiB | 473.7 MB/s | 2/6 | 2,316 | 336.4s / 1,037,322 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:56.0425788+00:00 | 1 | 36.0 MiB / 2.1 MiB | 473.7 MB/s | 2/7 | 2,524 | 354.4s / 1,030,205 msg/s |
| Dekaf (3conn) | 2026-07-17T02:40:14.0606998+00:00 | 2 | 39.0 MiB / 1.1 MiB | 471.0 MB/s | 3/6 | 2,615 | 372.4s / 1,045,873 msg/s |
| Dekaf (3conn) | 2026-07-17T02:40:32.0880265+00:00 | 2 | 39.0 MiB / 14.4 MiB | 471.0 MB/s | 3/6 | 2,710 | 390.4s / 1,015,660 msg/s |
| Dekaf (3conn) | 2026-07-17T02:40:50.1599036+00:00 | 2 | 39.0 MiB / 24.4 MiB | 490.1 MB/s | 3/7 | 2,911 | 408.5s / 1,133,531 msg/s |
| Dekaf (3conn) | 2026-07-17T02:41:08.1850478+00:00 | 3 | 39.0 MiB / 8.9 MiB | 461.1 MB/s | 3/7 | 800 | 426.5s / 1,033,514 msg/s |
| Dekaf (3conn) | 2026-07-17T02:41:26.2132736+00:00 | 3 | 42.0 MiB / 4.0 MiB | 463.8 MB/s | 4/7 | 1,041 | 444.5s / 1,080,836 msg/s |
| Dekaf (3conn) | 2026-07-17T02:41:45.240094+00:00 | 1 | 39.0 MiB / 5.0 MiB | 504.1 MB/s | 3/8 | 4,645 | 463.6s / 1,043,148 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:03.26281+00:00 | 1 | 39.0 MiB / 1.2 MiB | 504.1 MB/s | 3/8 | 4,756 | 481.6s / 1,097,832 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:21.2871494+00:00 | 2 | 42.0 MiB / 1.8 MiB | 490.1 MB/s | 4/8 | 3,574 | 499.6s / 1,069,447 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:39.3012574+00:00 | 2 | 42.0 MiB / 4.4 MiB | 490.1 MB/s | 4/8 | 3,574 | 517.6s / 1,110,556 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:57.3237845+00:00 | 3 | 36.0 MiB / 2.2 MiB | 463.8 MB/s | 5/8 | 1,293 | 535.6s / 1,081,308 msg/s |
| Dekaf (3conn) | 2026-07-17T02:43:15.3500111+00:00 | 3 | 30.0 MiB / 0.2 MiB | 480.2 MB/s | 6/8 | 1,489 | 553.7s / 1,064,138 msg/s |
| Dekaf (3conn) | 2026-07-17T02:43:34.3840092+00:00 | 1 | 27.0 MiB / 1.2 MiB | 504.1 MB/s | 5/9 | 6,923 | 572.7s / 1,093,909 msg/s |
| Dekaf (3conn) | 2026-07-17T02:43:52.3936469+00:00 | 1 | 27.0 MiB / 1.8 MiB | 504.1 MB/s | 5/9 | 7,231 | 590.7s / 1,160,838 msg/s |
| Dekaf (3conn) | 2026-07-17T02:44:10.409747+00:00 | 2 | 24.0 MiB / 3.2 MiB | 490.1 MB/s | 7/8 | 5,039 | 608.7s / 1,056,749 msg/s |
| Dekaf (3conn) | 2026-07-17T02:44:28.4290823+00:00 | 2 | 27.0 MiB / 3.8 MiB | 490.1 MB/s | 8/8 | 5,590 | 626.7s / 1,013,279 msg/s |
| Dekaf (3conn) | 2026-07-17T02:44:46.4422834+00:00 | 2 | 27.0 MiB / 1.8 MiB | 490.1 MB/s | 8/8 | 5,679 | 644.7s / 1,065,085 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:04.4555165+00:00 | 3 | 30.0 MiB / 2.6 MiB | 480.2 MB/s | 9/8 | 3,285 | 662.8s / 1,164,064 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:22.470045+00:00 | 3 | 30.0 MiB / 1.4 MiB | 480.2 MB/s | 9/8 | 3,434 | 680.8s / 1,020,251 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:41.5052313+00:00 | 1 | 21.0 MiB / 5.8 MiB | 504.1 MB/s | 7/10 | 10,268 | 699.8s / 1,110,470 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:59.5173426+00:00 | 1 | 21.0 MiB / 2.0 MiB | 506.2 MB/s | 7/11 | 11,611 | 717.8s / 1,085,149 msg/s |
| Dekaf (3conn) | 2026-07-17T02:46:17.5295983+00:00 | 2 | 30.0 MiB / 0.9 MiB | 490.1 MB/s | 9/9 | 7,231 | 735.9s / 1,065,819 msg/s |
| Dekaf (3conn) | 2026-07-17T02:46:35.5510231+00:00 | 2 | 24.0 MiB / 6.1 MiB | 490.1 MB/s | 10/9 | 7,522 | 753.9s / 1,130,793 msg/s |
| Dekaf (3conn) | 2026-07-17T02:46:53.5731107+00:00 | 3 | 24.0 MiB / 5.9 MiB | 480.2 MB/s | 10/10 | 4,217 | 771.9s / 1,136,838 msg/s |
| Dekaf (3conn) | 2026-07-17T02:47:11.5888231+00:00 | 3 | 24.0 MiB / 9.4 MiB | 480.2 MB/s | 10/10 | 4,589 | 789.9s / 1,084,551 msg/s |
| Dekaf (3conn) | 2026-07-17T02:47:30.6069159+00:00 | 1 | 24.0 MiB / 9.2 MiB | 506.2 MB/s | 8/12 | 15,461 | 808.9s / 1,114,873 msg/s |
| Dekaf (3conn) | 2026-07-17T02:47:48.6207986+00:00 | 1 | 24.0 MiB / 19.4 MiB | 506.2 MB/s | 8/12 | 16,024 | 827.0s / 1,091,538 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:06.6537691+00:00 | 2 | 24.0 MiB / 21.1 MiB | 490.1 MB/s | 10/11 | 9,926 | 845.0s / 1,160,021 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:24.6722684+00:00 | 2 | 24.0 MiB / 9.4 MiB | 490.1 MB/s | 10/12 | 10,389 | 863.0s / 1,019,737 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:42.700089+00:00 | 3 | 24.0 MiB / 4.6 MiB | 480.2 MB/s | 12/11 | 5,916 | 881.0s / 1,103,651 msg/s |
| Dekaf (3conn) | 2026-07-17T02:49:00.708387+00:00 | 3 | 24.0 MiB / 16.6 MiB | 480.2 MB/s | 12/11 | 6,115 | 899.1s / 1,022,004 msg/s |
*5,293 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-17T02:19:18.3010626+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 0.6 MiB |
| Dekaf | 2026-07-17T02:19:33.3761198+00:00 | 1 | capacity | succeeded | 15,075ms | 14.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:19:33.3895266+00:00 | 3 | capacity | succeeded | 15,072ms | 14.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-17T02:19:36.4008326+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:19:51.4942668+00:00 | 2 | capacity | succeeded | 15,099ms | 12.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:20:21.6516593+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 2.8 MiB |
| Dekaf | 2026-07-17T02:20:36.7523423+00:00 | 3 | capacity | succeeded | 15,101ms | 10.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-17T02:20:39.6810958+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-17T02:20:54.7719585+00:00 | 1 | capacity | failed | 15,089ms | 10.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-17T02:21:24.8877995+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-17T02:21:25.0151647+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-17T02:21:40.0729391+00:00 | 2 | capacity | failed | 15,058ms | 10.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:22:10.2423923+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-17T02:22:25.2935838+00:00 | 3 | capacity | failed | 15,071ms | 10.0 MiB / 6.0 MiB |
| Dekaf | 2026-07-17T02:22:55.4220881+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-17T02:23:10.4086959+00:00 | 1 | capacity | failed | 15,071ms | 10.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-17T02:23:40.5687946+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-17T02:23:55.6445222+00:00 | 1 | capacity | succeeded | 15,075ms | 8.0 MiB / 1.4 MiB |
| Dekaf | 2026-07-17T02:23:55.7603013+00:00 | 2 | capacity | succeeded | 15,067ms | 9.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-17T02:24:25.9135167+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-17T02:24:41.01741+00:00 | 2 | capacity | succeeded | 15,103ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:25:11.0968258+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-17T02:25:26.1517658+00:00 | 3 | capacity | failed | 15,054ms | 9.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-17T02:25:56.2256972+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-17T02:26:11.2871418+00:00 | 1 | capacity | failed | 15,061ms | 9.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:26:41.3999006+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-17T02:26:41.6259299+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-17T02:26:56.6934183+00:00 | 2 | capacity | failed | 15,067ms | 8.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-17T02:27:26.8372589+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:27:41.8205868+00:00 | 3 | capacity | succeeded | 15,073ms | 10.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-17T02:28:11.981356+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-17T02:28:27.0031039+00:00 | 1 | capacity | succeeded | 15,108ms | 10.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-17T02:28:57.1601492+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-17T02:29:12.263381+00:00 | 1 | capacity | failed | 15,103ms | 10.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:29:12.4039534+00:00 | 2 | capacity | failed | 15,058ms | 10.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-17T02:29:42.6029857+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-17T02:29:57.6765033+00:00 | 2 | capacity | failed | 15,073ms | 10.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-17T02:30:27.7094134+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-17T02:30:42.7765399+00:00 | 1 | capacity | failed | 15,067ms | 8.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-17T02:31:12.8091911+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-17T02:31:27.8693243+00:00 | 3 | capacity | succeeded | 15,060ms | 9.0 MiB / 0.4 MiB |
| Dekaf | 2026-07-17T02:31:58.0614246+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-17T02:31:58.3153769+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-17T02:32:13.3879429+00:00 | 2 | capacity | succeeded | 15,072ms | 9.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-17T02:32:43.5539516+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-17T02:32:58.5098122+00:00 | 1 | capacity | succeeded | 15,123ms | 10.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:33:28.7069556+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 1.2 MiB |
| Dekaf | 2026-07-17T02:33:43.54221+00:00 | 3 | capacity | succeeded | 15,059ms | 12.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:34:31.886045+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:34:46.9591945+00:00 | 1 | capacity | failed | 15,072ms | 48.0 MiB / 10.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:34:47.0319089+00:00 | 2 | capacity | failed | 15,082ms | 48.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:35:17.2418068+00:00 | 2 | capacity | started | 0ms | 54.0 MiB / 8.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:35:32.3241064+00:00 | 2 | capacity | failed | 15,082ms | 48.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:02.4487787+00:00 | 3 | capacity | started | 0ms | 60.0 MiB / 0.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:17.5307142+00:00 | 2 | capacity | succeeded | 15,076ms | 42.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:20.531306+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:35.7691418+00:00 | 2 | capacity | succeeded | 15,215ms | 36.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:47.7649794+00:00 | 3 | capacity | started | 0ms | 45.0 MiB / 25.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:53.9298917+00:00 | 2 | capacity | failed | 15,132ms | 36.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:20.9923789+00:00 | 3 | capacity | failed | 15,104ms | 45.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:39.00294+00:00 | 1 | capacity | failed | 15,079ms | 36.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:51.2089048+00:00 | 3 | capacity | started | 0ms | 48.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:38:09.3761249+00:00 | 2 | capacity | started | 0ms | 42.0 MiB / 1.3 MiB |
| Dekaf (3conn) | 2026-07-17T02:38:24.4300759+00:00 | 2 | capacity | failed | 15,053ms | 39.0 MiB / 0.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:38:54.4829148+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:09.5615406+00:00 | 1 | capacity | failed | 15,078ms | 36.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:09.7031088+00:00 | 2 | capacity | failed | 15,137ms | 39.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:39.888276+00:00 | 2 | capacity | started | 0ms | 33.0 MiB / 1.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:54.9798374+00:00 | 2 | capacity | failed | 15,091ms | 39.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-17T02:40:25.177057+00:00 | 3 | capacity | started | 0ms | 33.0 MiB / 15.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:40:40.318086+00:00 | 3 | capacity | failed | 15,141ms | 39.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:10.4352497+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 2.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:25.5265096+00:00 | 1 | capacity | succeeded | 15,091ms | 39.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:55.7525721+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:55.8457272+00:00 | 2 | capacity | started | 0ms | 45.0 MiB / 1.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:10.9219905+00:00 | 2 | capacity | failed | 15,076ms | 42.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:41.0940474+00:00 | 2 | capacity | started | 0ms | 36.0 MiB / 10.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:56.2166564+00:00 | 2 | capacity | succeeded | 15,122ms | 36.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:59.2321777+00:00 | 3 | capacity | started | 0ms | 30.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:14.2211132+00:00 | 1 | capacity | failed | 15,070ms | 27.0 MiB / 7.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:17.3347449+00:00 | 2 | capacity | started | 0ms | 24.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:32.4328091+00:00 | 3 | capacity | succeeded | 15,051ms | 24.0 MiB / 1.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:59.425452+00:00 | 1 | capacity | failed | 15,062ms | 27.0 MiB / 2.8 MiB |
| Dekaf (3conn) | 2026-07-17T02:44:17.6361225+00:00 | 2 | capacity | succeeded | 15,103ms | 27.0 MiB / 26.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:44:44.6846219+00:00 | 1 | capacity | succeeded | 15,116ms | 24.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:44:47.912975+00:00 | 3 | capacity | started | 0ms | 30.0 MiB / 1.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:45:14.8529237+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 3.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:45:32.9780717+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 0.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:45:48.0577789+00:00 | 1 | capacity | failed | 15,079ms | 21.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:18.2633312+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 3.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:18.4578877+00:00 | 3 | capacity | started | 0ms | 24.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:33.5298459+00:00 | 3 | capacity | succeeded | 15,071ms | 24.0 MiB / 5.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:51.6882169+00:00 | 2 | capacity | failed | 15,157ms | 24.0 MiB / 17.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:47:03.5310314+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 2.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:47:21.8947762+00:00 | 3 | capacity | started | 0ms | 27.0 MiB / 2.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:47:36.9624591+00:00 | 3 | capacity | failed | 15,067ms | 24.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:07.1631488+00:00 | 3 | capacity | started | 0ms | 21.0 MiB / 13.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:22.2607937+00:00 | 2 | capacity | failed | 15,082ms | 24.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:34.1058439+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:52.4152385+00:00 | 2 | capacity | started | 0ms | 27.0 MiB / 2.1 MiB |
*161 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 7 |
| Dekaf | 1 | 0.002–0.004ms | 3 |
| Dekaf | 1 | 0.004–0.008ms | 18 |
| Dekaf | 1 | 0.008–0.016ms | 56 |
| Dekaf | 1 | 0.016–0.032ms | 163 |
| Dekaf | 1 | 0.032–0.064ms | 367 |
| Dekaf | 1 | 0.064–0.128ms | 393 |
| Dekaf | 1 | 0.128–0.256ms | 513 |
| Dekaf | 1 | 0.256–0.512ms | 828 |
| Dekaf | 1 | 0.512–1.024ms | 1,428 |
| Dekaf | 1 | 1.024–2.048ms | 1,835 |
| Dekaf | 1 | 2.048–4.096ms | 1,671 |
| Dekaf | 1 | 4.096–8.192ms | 1,294 |
| Dekaf | 1 | 8.192–16.384ms | 648 |
| Dekaf | 1 | 16.384–32.768ms | 323 |
| Dekaf | 1 | 32.768–65.536ms | 19 |
| Dekaf | 2 | 0.001–0.002ms | 5 |
| Dekaf | 2 | 0.002–0.004ms | 5 |
| Dekaf | 2 | 0.004–0.008ms | 16 |
| Dekaf | 2 | 0.008–0.016ms | 41 |
| Dekaf | 2 | 0.016–0.032ms | 115 |
| Dekaf | 2 | 0.032–0.064ms | 252 |
| Dekaf | 2 | 0.064–0.128ms | 310 |
| Dekaf | 2 | 0.128–0.256ms | 359 |
| Dekaf | 2 | 0.256–0.512ms | 561 |
| Dekaf | 2 | 0.512–1.024ms | 1,011 |
| Dekaf | 2 | 1.024–2.048ms | 1,293 |
| Dekaf | 2 | 2.048–4.096ms | 1,225 |
| Dekaf | 2 | 4.096–8.192ms | 826 |
| Dekaf | 2 | 8.192–16.384ms | 428 |
| Dekaf | 2 | 16.384–32.768ms | 129 |
| Dekaf | 2 | 32.768–65.536ms | 12 |
| Dekaf | 2 | 131.072–262.144ms | 1 |
| Dekaf | 3 | 0.001–0.002ms | 5 |
| Dekaf | 3 | 0.002–0.004ms | 10 |
| Dekaf | 3 | 0.004–0.008ms | 14 |
| Dekaf | 3 | 0.008–0.016ms | 58 |
| Dekaf | 3 | 0.016–0.032ms | 134 |
| Dekaf | 3 | 0.032–0.064ms | 305 |
| Dekaf | 3 | 0.064–0.128ms | 407 |
| Dekaf | 3 | 0.128–0.256ms | 466 |
| Dekaf | 3 | 0.256–0.512ms | 695 |
| Dekaf | 3 | 0.512–1.024ms | 1,233 |
| Dekaf | 3 | 1.024–2.048ms | 1,519 |
| Dekaf | 3 | 2.048–4.096ms | 1,419 |
| Dekaf | 3 | 4.096–8.192ms | 839 |
| Dekaf | 3 | 8.192–16.384ms | 313 |
| Dekaf | 3 | 16.384–32.768ms | 98 |
| Dekaf | 3 | 32.768–65.536ms | 11 |
| Dekaf | 3 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 2 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 11 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 13 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 43 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 60 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 74 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 101 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 172 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 226 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 242 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 205 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 84 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 11 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 5 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 7 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 13 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 31 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 40 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 49 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 99 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 127 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 153 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 117 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 61 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 6 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 1 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 5 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 16 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 13 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 23 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 33 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 59 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 82 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 93 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 74 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 24 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 14,000 | 2026-07-17T02:03:48.04575+00:00 | 121.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 17,000 | 2026-07-17T02:03:48.0510622+00:00 | 128.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 18,000 | 2026-07-17T02:03:48.0521419+00:00 | 127.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 20,000 | 2026-07-17T02:03:48.0546911+00:00 | 132.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 21,000 | 2026-07-17T02:03:48.0566024+00:00 | 140.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 22,000 | 2026-07-17T02:03:48.0577635+00:00 | 129.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 23,000 | 2026-07-17T02:03:48.0592302+00:00 | 131.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 24,000 | 2026-07-17T02:03:48.060304+00:00 | 144.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 27,000 | 2026-07-17T02:03:48.0657773+00:00 | 140.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 28,000 | 2026-07-17T02:03:48.0666736+00:00 | 139.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 30,000 | 2026-07-17T02:03:48.0694718+00:00 | 150.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 31,000 | 2026-07-17T02:03:48.070931+00:00 | 150.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 32,000 | 2026-07-17T02:03:48.0720781+00:00 | 148.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 33,000 | 2026-07-17T02:03:48.0730626+00:00 | 147.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 34,000 | 2026-07-17T02:03:48.0750066+00:00 | 167.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 37,000 | 2026-07-17T02:03:48.0806074+00:00 | 157.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 38,000 | 2026-07-17T02:03:48.0823866+00:00 | 155.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 40,000 | 2026-07-17T02:03:48.0850542+00:00 | 150.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 41,000 | 2026-07-17T02:03:48.0961181+00:00 | 141.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 42,000 | 2026-07-17T02:03:48.0972942+00:00 | 147.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 43,000 | 2026-07-17T02:03:48.0981078+00:00 | 141.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 44,000 | 2026-07-17T02:03:48.098984+00:00 | 304.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 47,000 | 2026-07-17T02:03:48.1112175+00:00 | 151.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 48,000 | 2026-07-17T02:03:48.1124891+00:00 | 213.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 50,000 | 2026-07-17T02:03:48.115415+00:00 | 142.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 51,000 | 2026-07-17T02:03:48.1169718+00:00 | 209.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 52,000 | 2026-07-17T02:03:48.1188671+00:00 | 226.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 53,000 | 2026-07-17T02:03:48.1203136+00:00 | 137.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 54,000 | 2026-07-17T02:03:48.1229955+00:00 | 280.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 57,000 | 2026-07-17T02:03:48.1278026+00:00 | 267.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 58,000 | 2026-07-17T02:03:48.1301526+00:00 | 265.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 60,000 | 2026-07-17T02:03:48.1330627+00:00 | 126.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 61,000 | 2026-07-17T02:03:48.134561+00:00 | 267.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 62,000 | 2026-07-17T02:03:48.1372383+00:00 | 230.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 63,000 | 2026-07-17T02:03:48.1385956+00:00 | 134.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 64,000 | 2026-07-17T02:03:48.1402822+00:00 | 405.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 67,000 | 2026-07-17T02:03:48.145329+00:00 | 258.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 68,000 | 2026-07-17T02:03:48.1469112+00:00 | 257.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 70,000 | 2026-07-17T02:03:48.1501986+00:00 | 188.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 71,000 | 2026-07-17T02:03:48.1523703+00:00 | 251.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 72,000 | 2026-07-17T02:03:48.1536595+00:00 | 228.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 73,000 | 2026-07-17T02:03:48.1550789+00:00 | 188.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 74,000 | 2026-07-17T02:03:48.1561548+00:00 | 406.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 77,000 | 2026-07-17T02:03:48.1610791+00:00 | 302.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 78,000 | 2026-07-17T02:03:48.1627492+00:00 | 300.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 80,000 | 2026-07-17T02:03:48.166758+00:00 | 198.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 81,000 | 2026-07-17T02:03:48.1684283+00:00 | 295.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 82,000 | 2026-07-17T02:03:48.1698063+00:00 | 263.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 83,000 | 2026-07-17T02:03:48.1712444+00:00 | 194.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 84,000 | 2026-07-17T02:03:48.172746+00:00 | 402.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 87,000 | 2026-07-17T02:03:48.182808+00:00 | 300.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 88,000 | 2026-07-17T02:03:48.1848328+00:00 | 298.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 90,000 | 2026-07-17T02:03:48.1889084+00:00 | 186.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 91,000 | 2026-07-17T02:03:48.1897029+00:00 | 311.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 92,000 | 2026-07-17T02:03:48.1909022+00:00 | 242.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 93,000 | 2026-07-17T02:03:48.1921019+00:00 | 190.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 94,000 | 2026-07-17T02:03:48.1933038+00:00 | 394.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 97,000 | 2026-07-17T02:03:48.1975327+00:00 | 324.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 98,000 | 2026-07-17T02:03:48.1984393+00:00 | 323.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 100,000 | 2026-07-17T02:03:48.2008604+00:00 | 226.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 101,000 | 2026-07-17T02:03:48.203102+00:00 | 359.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 102,000 | 2026-07-17T02:03:48.2071163+00:00 | 255.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 103,000 | 2026-07-17T02:03:48.2095465+00:00 | 251.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 104,000 | 2026-07-17T02:03:48.2110164+00:00 | 417.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 107,000 | 2026-07-17T02:03:48.2147861+00:00 | 354.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 108,000 | 2026-07-17T02:03:48.2159072+00:00 | 353.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 110,000 | 2026-07-17T02:03:48.2190146+00:00 | 244.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 111,000 | 2026-07-17T02:03:48.2208872+00:00 | 348.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 112,000 | 2026-07-17T02:03:48.2219101+00:00 | 248.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 113,000 | 2026-07-17T02:03:48.2235869+00:00 | 239.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 114,000 | 2026-07-17T02:03:48.2250371+00:00 | 404.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 117,000 | 2026-07-17T02:03:48.2297189+00:00 | 345.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 118,000 | 2026-07-17T02:03:48.2318547+00:00 | 343.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 120,000 | 2026-07-17T02:03:48.2346724+00:00 | 231.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 121,000 | 2026-07-17T02:03:48.2361686+00:00 | 339.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 122,000 | 2026-07-17T02:03:48.2387749+00:00 | 259.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 123,000 | 2026-07-17T02:03:48.2403755+00:00 | 236.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 124,000 | 2026-07-17T02:03:48.2421262+00:00 | 388.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 127,000 | 2026-07-17T02:03:48.2464621+00:00 | 329.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 128,000 | 2026-07-17T02:03:48.2479214+00:00 | 328.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 130,000 | 2026-07-17T02:03:48.2518376+00:00 | 231.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 131,000 | 2026-07-17T02:03:48.2528997+00:00 | 376.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 132,000 | 2026-07-17T02:03:48.2543999+00:00 | 276.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 133,000 | 2026-07-17T02:03:48.2570576+00:00 | 231.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 134,000 | 2026-07-17T02:03:48.258357+00:00 | 381.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 137,000 | 2026-07-17T02:03:48.2631676+00:00 | 367.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 138,000 | 2026-07-17T02:03:48.2639779+00:00 | 366.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 140,000 | 2026-07-17T02:03:48.2662131+00:00 | 235.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 141,000 | 2026-07-17T02:03:48.2677799+00:00 | 362.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 142,000 | 2026-07-17T02:03:48.2691846+00:00 | 293.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 143,000 | 2026-07-17T02:03:48.2702553+00:00 | 231.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 144,000 | 2026-07-17T02:03:48.2715755+00:00 | 369.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 147,000 | 2026-07-17T02:03:48.2758741+00:00 | 363.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 148,000 | 2026-07-17T02:03:48.2769549+00:00 | 362.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 150,000 | 2026-07-17T02:03:48.3268675+00:00 | 200.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 151,000 | 2026-07-17T02:03:48.3282035+00:00 | 311.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 152,000 | 2026-07-17T02:03:48.3296793+00:00 | 258.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 153,000 | 2026-07-17T02:03:48.3361791+00:00 | 191.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 154,000 | 2026-07-17T02:03:48.3376206+00:00 | 308.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 157,000 | 2026-07-17T02:03:48.3421786+00:00 | 297.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 158,000 | 2026-07-17T02:03:48.3436145+00:00 | 297.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 160,000 | 2026-07-17T02:03:48.3464549+00:00 | 192.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 161,000 | 2026-07-17T02:03:48.348033+00:00 | 292.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 162,000 | 2026-07-17T02:03:48.3494366+00:00 | 327.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 163,000 | 2026-07-17T02:03:48.3508614+00:00 | 190.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 164,000 | 2026-07-17T02:03:48.3522923+00:00 | 298.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 167,000 | 2026-07-17T02:03:48.3566238+00:00 | 284.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 168,000 | 2026-07-17T02:03:48.3580348+00:00 | 283.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 170,000 | 2026-07-17T02:03:48.3676775+00:00 | 279.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 171,000 | 2026-07-17T02:03:48.368703+00:00 | 277.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 172,000 | 2026-07-17T02:03:48.3707874+00:00 | 319.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 173,000 | 2026-07-17T02:03:48.3744596+00:00 | 276.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 174,000 | 2026-07-17T02:03:48.3770612+00:00 | 298.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 177,000 | 2026-07-17T02:03:48.3808042+00:00 | 270.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 178,000 | 2026-07-17T02:03:48.3830978+00:00 | 267.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 180,000 | 2026-07-17T02:03:48.3896333+00:00 | 262.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 181,000 | 2026-07-17T02:03:48.4040114+00:00 | 247.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 182,000 | 2026-07-17T02:03:48.4072179+00:00 | 287.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 183,000 | 2026-07-17T02:03:48.4087813+00:00 | 243.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 184,000 | 2026-07-17T02:03:48.4101824+00:00 | 266.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 187,000 | 2026-07-17T02:03:48.4152696+00:00 | 258.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 188,000 | 2026-07-17T02:03:48.4277959+00:00 | 246.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 190,000 | 2026-07-17T02:03:48.4340543+00:00 | 218.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 191,000 | 2026-07-17T02:03:48.4356758+00:00 | 238.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 192,000 | 2026-07-17T02:03:48.4674254+00:00 | 251.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 193,000 | 2026-07-17T02:03:48.4684699+00:00 | 194.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 194,000 | 2026-07-17T02:03:48.4696146+00:00 | 265.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 197,000 | 2026-07-17T02:03:48.4732686+00:00 | 202.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 198,000 | 2026-07-17T02:03:48.4741641+00:00 | 216.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 200,000 | 2026-07-17T02:03:48.482311+00:00 | 192.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 201,000 | 2026-07-17T02:03:48.4840141+00:00 | 206.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 202,000 | 2026-07-17T02:03:48.4856564+00:00 | 237.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 203,000 | 2026-07-17T02:03:48.4909271+00:00 | 183.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 204,000 | 2026-07-17T02:03:48.4918469+00:00 | 345.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 207,000 | 2026-07-17T02:03:48.4960322+00:00 | 237.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 208,000 | 2026-07-17T02:03:48.4973023+00:00 | 236.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 210,000 | 2026-07-17T02:03:48.5020894+00:00 | 187.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 211,000 | 2026-07-17T02:03:48.5038692+00:00 | 321.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 212,000 | 2026-07-17T02:03:48.5058749+00:00 | 227.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 213,000 | 2026-07-17T02:03:48.507023+00:00 | 182.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 214,000 | 2026-07-17T02:03:48.508516+00:00 | 343.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 217,000 | 2026-07-17T02:03:48.5141597+00:00 | 322.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 218,000 | 2026-07-17T02:03:48.5171883+00:00 | 319.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 220,000 | 2026-07-17T02:03:48.5214311+00:00 | 168.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 221,000 | 2026-07-17T02:03:48.5231246+00:00 | 375.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 222,000 | 2026-07-17T02:03:48.5258345+00:00 | 207.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 223,000 | 2026-07-17T02:03:48.5282617+00:00 | 162.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 224,000 | 2026-07-17T02:03:48.5295406+00:00 | 424.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 225,000 | 2026-07-17T02:03:48.5312808+00:00 | 110.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 226,000 | 2026-07-17T02:03:48.5329119+00:00 | 112.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 227,000 | 2026-07-17T02:03:48.5362215+00:00 | 417.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 228,000 | 2026-07-17T02:03:48.5377409+00:00 | 415.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 229,000 | 2026-07-17T02:03:48.5396098+00:00 | 207.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 230,000 | 2026-07-17T02:03:48.5409067+00:00 | 150.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 231,000 | 2026-07-17T02:03:48.5435171+00:00 | 409.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 232,000 | 2026-07-17T02:03:48.5455564+00:00 | 188.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 233,000 | 2026-07-17T02:03:48.5467129+00:00 | 144.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 234,000 | 2026-07-17T02:03:48.5497393+00:00 | 412.5ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 235,000 | 2026-07-17T02:03:48.5560166+00:00 | 206.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 236,000 | 2026-07-17T02:03:48.5575066+00:00 | 205.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 237,000 | 2026-07-17T02:03:48.5635067+00:00 | 391.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 238,000 | 2026-07-17T02:03:48.5695032+00:00 | 386.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 239,000 | 2026-07-17T02:03:48.5705083+00:00 | 192.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 240,000 | 2026-07-17T02:03:48.5807718+00:00 | 114.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 241,000 | 2026-07-17T02:03:48.5819398+00:00 | 374.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 242,000 | 2026-07-17T02:03:48.5931399+00:00 | 141.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 244,000 | 2026-07-17T02:03:48.5964926+00:00 | 366.1ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 245,000 | 2026-07-17T02:03:48.6014647+00:00 | 166.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 246,000 | 2026-07-17T02:03:48.6471958+00:00 | 127.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 247,000 | 2026-07-17T02:03:48.6569127+00:00 | 301.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 248,000 | 2026-07-17T02:03:48.6809179+00:00 | 277.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 251,000 | 2026-07-17T02:03:48.7114024+00:00 | 250.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 254,000 | 2026-07-17T02:03:48.7160168+00:00 | 254.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 257,000 | 2026-07-17T02:03:48.7385663+00:00 | 229.3ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 258,000 | 2026-07-17T02:03:48.7408925+00:00 | 227.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 261,000 | 2026-07-17T02:03:48.7520565+00:00 | 218.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 264,000 | 2026-07-17T02:03:48.7650818+00:00 | 213.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 267,000 | 2026-07-17T02:03:48.776436+00:00 | 197.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 268,000 | 2026-07-17T02:03:48.7772226+00:00 | 196.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 271,000 | 2026-07-17T02:03:48.7895111+00:00 | 184.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 274,000 | 2026-07-17T02:03:48.7992135+00:00 | 179.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 277,000 | 2026-07-17T02:03:48.801519+00:00 | 172.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 278,000 | 2026-07-17T02:03:48.8052771+00:00 | 168.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 281,000 | 2026-07-17T02:03:48.8074987+00:00 | 170.2ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 284,000 | 2026-07-17T02:03:48.8108708+00:00 | 174.4ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 287,000 | 2026-07-17T02:03:48.816795+00:00 | 164.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 288,000 | 2026-07-17T02:03:48.8177828+00:00 | 163.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 291,000 | 2026-07-17T02:03:48.82761+00:00 | 155.8ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 294,000 | 2026-07-17T02:03:48.8383553+00:00 | 158.7ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 297,000 | 2026-07-17T02:03:48.8419882+00:00 | 154.9ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 298,000 | 2026-07-17T02:03:48.8498944+00:00 | 147.0ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 301,000 | 2026-07-17T02:03:48.8573469+00:00 | 139.6ms | GC pause | - | - | 1.0s / 417,437 msg/s | Gen2 +0 / pause +201.8ms |
| Confluent | 365,000 | 2026-07-17T02:03:48.9499324+00:00 | 114.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 366,000 | 2026-07-17T02:03:48.9509148+00:00 | 168.1ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 369,000 | 2026-07-17T02:03:48.9555571+00:00 | 163.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 375,000 | 2026-07-17T02:03:48.9636723+00:00 | 160.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 376,000 | 2026-07-17T02:03:48.9663108+00:00 | 157.9ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 379,000 | 2026-07-17T02:03:48.9686299+00:00 | 177.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 385,000 | 2026-07-17T02:03:48.9771511+00:00 | 198.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 386,000 | 2026-07-17T02:03:48.9778609+00:00 | 197.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 389,000 | 2026-07-17T02:03:48.979992+00:00 | 247.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 395,000 | 2026-07-17T02:03:48.9851342+00:00 | 243.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 396,000 | 2026-07-17T02:03:48.9862592+00:00 | 242.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 399,000 | 2026-07-17T02:03:48.9898951+00:00 | 239.1ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 405,000 | 2026-07-17T02:03:48.9955557+00:00 | 235.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 406,000 | 2026-07-17T02:03:48.9979636+00:00 | 233.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 409,000 | 2026-07-17T02:03:49.0008496+00:00 | 231.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 415,000 | 2026-07-17T02:03:49.0078254+00:00 | 230.9ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 416,000 | 2026-07-17T02:03:49.0084117+00:00 | 230.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +432.8ms |
| Confluent | 419,000 | 2026-07-17T02:03:49.0112023+00:00 | 228.6ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 425,000 | 2026-07-17T02:03:49.0187008+00:00 | 230.1ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 426,000 | 2026-07-17T02:03:49.0193631+00:00 | 229.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 429,000 | 2026-07-17T02:03:49.0218243+00:00 | 227.1ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 435,000 | 2026-07-17T02:03:49.0265519+00:00 | 222.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 436,000 | 2026-07-17T02:03:49.0288603+00:00 | 220.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 439,000 | 2026-07-17T02:03:49.0309292+00:00 | 218.8ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 445,000 | 2026-07-17T02:03:49.0363251+00:00 | 285.9ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 446,000 | 2026-07-17T02:03:49.0369208+00:00 | 285.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 449,000 | 2026-07-17T02:03:49.0389626+00:00 | 284.6ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 455,000 | 2026-07-17T02:03:49.0435307+00:00 | 280.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 456,000 | 2026-07-17T02:03:49.0456118+00:00 | 278.6ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 459,000 | 2026-07-17T02:03:49.0473375+00:00 | 277.0ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 465,000 | 2026-07-17T02:03:49.0524818+00:00 | 272.9ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 466,000 | 2026-07-17T02:03:49.0531674+00:00 | 272.3ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 469,000 | 2026-07-17T02:03:49.0562061+00:00 | 270.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 475,000 | 2026-07-17T02:03:49.0605071+00:00 | 272.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 476,000 | 2026-07-17T02:03:49.0613891+00:00 | 271.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 479,000 | 2026-07-17T02:03:49.0632429+00:00 | 293.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 485,000 | 2026-07-17T02:03:49.0685472+00:00 | 293.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 486,000 | 2026-07-17T02:03:49.0698859+00:00 | 292.1ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 489,000 | 2026-07-17T02:03:49.0722592+00:00 | 291.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 495,000 | 2026-07-17T02:03:49.0760786+00:00 | 299.8ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 496,000 | 2026-07-17T02:03:49.080262+00:00 | 295.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 499,000 | 2026-07-17T02:03:49.088673+00:00 | 287.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 505,000 | 2026-07-17T02:03:49.0933293+00:00 | 283.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 506,000 | 2026-07-17T02:03:49.0938246+00:00 | 282.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 509,000 | 2026-07-17T02:03:49.1133165+00:00 | 269.0ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 515,000 | 2026-07-17T02:03:49.1256361+00:00 | 259.0ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 516,000 | 2026-07-17T02:03:49.1266638+00:00 | 258.0ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 519,000 | 2026-07-17T02:03:49.1312121+00:00 | 253.9ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 558,000 | 2026-07-17T02:03:49.1846531+00:00 | 136.8ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 561,000 | 2026-07-17T02:03:49.1864339+00:00 | 135.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 567,000 | 2026-07-17T02:03:49.1942439+00:00 | 133.3ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 568,000 | 2026-07-17T02:03:49.194971+00:00 | 132.6ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 571,000 | 2026-07-17T02:03:49.1988761+00:00 | 133.6ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 574,000 | 2026-07-17T02:03:49.2037975+00:00 | 143.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 577,000 | 2026-07-17T02:03:49.2063572+00:00 | 141.0ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 578,000 | 2026-07-17T02:03:49.2081176+00:00 | 139.3ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 581,000 | 2026-07-17T02:03:49.210489+00:00 | 150.3ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 584,000 | 2026-07-17T02:03:49.2184492+00:00 | 143.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 587,000 | 2026-07-17T02:03:49.2236091+00:00 | 148.6ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 588,000 | 2026-07-17T02:03:49.2246422+00:00 | 147.7ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 591,000 | 2026-07-17T02:03:49.2299995+00:00 | 143.4ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 594,000 | 2026-07-17T02:03:49.2378288+00:00 | 141.5ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 597,000 | 2026-07-17T02:03:49.2506227+00:00 | 132.2ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Confluent | 598,000 | 2026-07-17T02:03:49.2519041+00:00 | 131.1ms | GC pause | - | - | 2.0s / 561,482 msg/s | Gen2 +0 / pause +231.0ms |
| Dekaf | 20,969,000 | 2026-07-17T02:19:09.4031377+00:00 | 118.6ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,973,000 | 2026-07-17T02:19:09.4049742+00:00 | 116.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,977,000 | 2026-07-17T02:19:09.4071013+00:00 | 131.0ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,979,000 | 2026-07-17T02:19:09.4088071+00:00 | 128.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,981,000 | 2026-07-17T02:19:09.4110198+00:00 | 165.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,982,000 | 2026-07-17T02:19:09.4115192+00:00 | 165.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,983,000 | 2026-07-17T02:19:09.4120125+00:00 | 141.9ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,985,000 | 2026-07-17T02:19:09.4134176+00:00 | 179.0ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,987,000 | 2026-07-17T02:19:09.4154088+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,988,000 | 2026-07-17T02:19:09.415829+00:00 | 176.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,989,000 | 2026-07-17T02:19:09.4163045+00:00 | 142.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,991,000 | 2026-07-17T02:19:09.4172836+00:00 | 175.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,992,000 | 2026-07-17T02:19:09.4183543+00:00 | 174.1ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,993,000 | 2026-07-17T02:19:09.4194248+00:00 | 139.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,995,000 | 2026-07-17T02:19:09.4209017+00:00 | 173.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,997,000 | 2026-07-17T02:19:09.4219569+00:00 | 126.8ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,998,000 | 2026-07-17T02:19:09.4226131+00:00 | 172.0ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,999,000 | 2026-07-17T02:19:09.4235929+00:00 | 135.1ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,001,000 | 2026-07-17T02:19:09.4254501+00:00 | 172.0ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,002,000 | 2026-07-17T02:19:09.4258109+00:00 | 171.7ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,003,000 | 2026-07-17T02:19:09.4263352+00:00 | 132.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,005,000 | 2026-07-17T02:19:09.4279545+00:00 | 198.9ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,007,000 | 2026-07-17T02:19:09.4291971+00:00 | 120.6ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,008,000 | 2026-07-17T02:19:09.4295637+00:00 | 197.3ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,009,000 | 2026-07-17T02:19:09.4302247+00:00 | 128.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,011,000 | 2026-07-17T02:19:09.4316067+00:00 | 179.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,012,000 | 2026-07-17T02:19:09.4324302+00:00 | 178.6ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,013,000 | 2026-07-17T02:19:09.4331754+00:00 | 127.2ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,015,000 | 2026-07-17T02:19:09.4341582+00:00 | 191.4ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,018,000 | 2026-07-17T02:19:09.5228998+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 22.0s / 824,180 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 869,364,000 | 2026-07-17T02:32:59.3639235+00:00 | 184.0ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,366,000 | 2026-07-17T02:32:59.3649319+00:00 | 183.0ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,370,000 | 2026-07-17T02:32:59.3664951+00:00 | 186.7ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,374,000 | 2026-07-17T02:32:59.3697211+00:00 | 185.4ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,376,000 | 2026-07-17T02:32:59.372032+00:00 | 187.5ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,380,000 | 2026-07-17T02:32:59.3743483+00:00 | 190.3ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,384,000 | 2026-07-17T02:32:59.3781746+00:00 | 186.5ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf | 869,386,000 | 2026-07-17T02:32:59.3789951+00:00 | 185.7ms | broker/backlog (no scale or GC event) | - | - | 851.8s / 801,731 msg/s | Gen2 +0 / pause +1.0ms |
| Dekaf (3conn) | 25,000 | 2026-07-17T02:34:01.6989712+00:00 | 138.6ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 28,000 | 2026-07-17T02:34:01.7031596+00:00 | 134.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 35,000 | 2026-07-17T02:34:01.7132379+00:00 | 128.7ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 38,000 | 2026-07-17T02:34:01.7173906+00:00 | 124.6ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 45,000 | 2026-07-17T02:34:01.7268395+00:00 | 115.1ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 48,000 | 2026-07-17T02:34:01.7309164+00:00 | 111.1ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 55,000 | 2026-07-17T02:34:01.7428255+00:00 | 106.7ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 58,000 | 2026-07-17T02:34:01.7461442+00:00 | 103.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 241,000 | 2026-07-17T02:34:02.0262525+00:00 | 143.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 242,000 | 2026-07-17T02:34:02.0293932+00:00 | 139.8ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 251,000 | 2026-07-17T02:34:02.0429968+00:00 | 139.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 252,000 | 2026-07-17T02:34:02.0441148+00:00 | 137.8ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 261,000 | 2026-07-17T02:34:02.0560637+00:00 | 163.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 262,000 | 2026-07-17T02:34:02.0573217+00:00 | 162.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 269,000 | 2026-07-17T02:34:02.0690168+00:00 | 100.2ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 271,000 | 2026-07-17T02:34:02.0726803+00:00 | 168.4ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 272,000 | 2026-07-17T02:34:02.0740436+00:00 | 167.1ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 281,000 | 2026-07-17T02:34:02.0834137+00:00 | 157.7ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 282,000 | 2026-07-17T02:34:02.0850623+00:00 | 156.1ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 291,000 | 2026-07-17T02:34:02.0952952+00:00 | 165.2ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 292,000 | 2026-07-17T02:34:02.0961377+00:00 | 164.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 299,000 | 2026-07-17T02:34:02.1039537+00:00 | 115.4ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 301,000 | 2026-07-17T02:34:02.106828+00:00 | 163.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 302,000 | 2026-07-17T02:34:02.1080024+00:00 | 161.9ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 303,000 | 2026-07-17T02:34:02.1089325+00:00 | 110.4ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 309,000 | 2026-07-17T02:34:02.116724+00:00 | 102.6ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 311,000 | 2026-07-17T02:34:02.1183982+00:00 | 156.9ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 312,000 | 2026-07-17T02:34:02.1218268+00:00 | 153.4ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 313,000 | 2026-07-17T02:34:02.122887+00:00 | 118.2ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 319,000 | 2026-07-17T02:34:02.1302011+00:00 | 110.9ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 321,000 | 2026-07-17T02:34:02.1322533+00:00 | 143.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 322,000 | 2026-07-17T02:34:02.1332062+00:00 | 142.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 323,000 | 2026-07-17T02:34:02.1341779+00:00 | 106.9ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 329,000 | 2026-07-17T02:34:02.1407567+00:00 | 119.7ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 331,000 | 2026-07-17T02:34:02.1443558+00:00 | 137.6ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 332,000 | 2026-07-17T02:34:02.1454717+00:00 | 136.5ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 333,000 | 2026-07-17T02:34:02.146355+00:00 | 114.1ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 339,000 | 2026-07-17T02:34:02.1531197+00:00 | 107.4ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 341,000 | 2026-07-17T02:34:02.1549447+00:00 | 136.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 342,000 | 2026-07-17T02:34:02.1559794+00:00 | 135.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 343,000 | 2026-07-17T02:34:02.1571923+00:00 | 103.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 351,000 | 2026-07-17T02:34:02.1705035+00:00 | 121.2ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 352,000 | 2026-07-17T02:34:02.1724285+00:00 | 119.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 361,000 | 2026-07-17T02:34:02.1859384+00:00 | 105.7ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 362,000 | 2026-07-17T02:34:02.1871475+00:00 | 104.5ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 473,000 | 2026-07-17T02:34:02.3456974+00:00 | 125.5ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 479,000 | 2026-07-17T02:34:02.3538831+00:00 | 117.3ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 483,000 | 2026-07-17T02:34:02.3590833+00:00 | 112.1ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 489,000 | 2026-07-17T02:34:02.3665708+00:00 | 113.4ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 493,000 | 2026-07-17T02:34:02.3733336+00:00 | 106.7ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 499,000 | 2026-07-17T02:34:02.3826287+00:00 | 102.6ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 503,000 | 2026-07-17T02:34:02.3869112+00:00 | 100.0ms | GC pause | - | - | 1.0s / 653,594 msg/s | Gen2 +1 / pause +0.9ms |
| Dekaf (3conn) | 790,000 | 2026-07-17T02:34:02.7644585+00:00 | 105.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 796,000 | 2026-07-17T02:34:02.7685755+00:00 | 101.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 800,000 | 2026-07-17T02:34:02.772061+00:00 | 113.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 810,000 | 2026-07-17T02:34:02.7800473+00:00 | 105.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 816,000 | 2026-07-17T02:34:02.7838686+00:00 | 101.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 820,000 | 2026-07-17T02:34:02.7872626+00:00 | 152.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 824,000 | 2026-07-17T02:34:02.7913424+00:00 | 116.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 826,000 | 2026-07-17T02:34:02.7924721+00:00 | 115.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 830,000 | 2026-07-17T02:34:02.795202+00:00 | 144.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 834,000 | 2026-07-17T02:34:02.7975369+00:00 | 110.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 836,000 | 2026-07-17T02:34:02.7995728+00:00 | 108.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 840,000 | 2026-07-17T02:34:02.8026931+00:00 | 142.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 844,000 | 2026-07-17T02:34:02.8059102+00:00 | 133.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 846,000 | 2026-07-17T02:34:02.8067451+00:00 | 132.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 850,000 | 2026-07-17T02:34:02.8097587+00:00 | 143.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 854,000 | 2026-07-17T02:34:02.8129269+00:00 | 132.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 856,000 | 2026-07-17T02:34:02.8142566+00:00 | 130.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 860,000 | 2026-07-17T02:34:02.8174074+00:00 | 136.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 864,000 | 2026-07-17T02:34:02.8205915+00:00 | 124.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 866,000 | 2026-07-17T02:34:02.823147+00:00 | 121.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 870,000 | 2026-07-17T02:34:02.8269625+00:00 | 146.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 874,000 | 2026-07-17T02:34:02.8342589+00:00 | 119.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 876,000 | 2026-07-17T02:34:02.8358386+00:00 | 117.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 880,000 | 2026-07-17T02:34:02.8405894+00:00 | 193.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 884,000 | 2026-07-17T02:34:02.8436941+00:00 | 109.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 886,000 | 2026-07-17T02:34:02.8475449+00:00 | 106.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 890,000 | 2026-07-17T02:34:02.8506267+00:00 | 213.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 894,000 | 2026-07-17T02:34:02.8570512+00:00 | 116.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 896,000 | 2026-07-17T02:34:02.860197+00:00 | 113.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 900,000 | 2026-07-17T02:34:02.865241+00:00 | 263.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 904,000 | 2026-07-17T02:34:02.870983+00:00 | 102.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 906,000 | 2026-07-17T02:34:02.8738073+00:00 | 113.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 910,000 | 2026-07-17T02:34:02.8791829+00:00 | 271.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 914,000 | 2026-07-17T02:34:02.8836365+00:00 | 103.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 916,000 | 2026-07-17T02:34:02.8860905+00:00 | 100.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 920,000 | 2026-07-17T02:34:02.8895804+00:00 | 263.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 924,000 | 2026-07-17T02:34:02.8924326+00:00 | 108.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 926,000 | 2026-07-17T02:34:02.893895+00:00 | 106.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 930,000 | 2026-07-17T02:34:02.8969676+00:00 | 260.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 934,000 | 2026-07-17T02:34:02.9005036+00:00 | 134.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 936,000 | 2026-07-17T02:34:02.9016184+00:00 | 132.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 940,000 | 2026-07-17T02:34:02.9065844+00:00 | 277.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 944,000 | 2026-07-17T02:34:02.9125125+00:00 | 122.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 946,000 | 2026-07-17T02:34:02.9143693+00:00 | 149.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 950,000 | 2026-07-17T02:34:02.9186699+00:00 | 265.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 954,000 | 2026-07-17T02:34:02.9239207+00:00 | 140.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 956,000 | 2026-07-17T02:34:02.9254181+00:00 | 138.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 960,000 | 2026-07-17T02:34:02.9301263+00:00 | 265.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 964,000 | 2026-07-17T02:34:02.935691+00:00 | 139.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 966,000 | 2026-07-17T02:34:02.9372457+00:00 | 138.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 970,000 | 2026-07-17T02:34:02.9436752+00:00 | 263.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 974,000 | 2026-07-17T02:34:02.9490917+00:00 | 126.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 976,000 | 2026-07-17T02:34:02.9514265+00:00 | 124.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 980,000 | 2026-07-17T02:34:02.9544692+00:00 | 254.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 984,000 | 2026-07-17T02:34:02.958451+00:00 | 135.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 986,000 | 2026-07-17T02:34:02.9608188+00:00 | 132.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 990,000 | 2026-07-17T02:34:02.9635766+00:00 | 245.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 994,000 | 2026-07-17T02:34:02.9672629+00:00 | 126.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 996,000 | 2026-07-17T02:34:02.9688869+00:00 | 124.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,000,000 | 2026-07-17T02:34:02.9739759+00:00 | 239.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,004,000 | 2026-07-17T02:34:02.978599+00:00 | 131.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,006,000 | 2026-07-17T02:34:02.9797272+00:00 | 130.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,010,000 | 2026-07-17T02:34:02.9850452+00:00 | 272.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,014,000 | 2026-07-17T02:34:02.9885734+00:00 | 140.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,016,000 | 2026-07-17T02:34:02.9942191+00:00 | 134.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,020,000 | 2026-07-17T02:34:03.001262+00:00 | 254.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,024,000 | 2026-07-17T02:34:03.0058768+00:00 | 130.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,026,000 | 2026-07-17T02:34:03.0069144+00:00 | 129.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,030,000 | 2026-07-17T02:34:03.019067+00:00 | 242.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,034,000 | 2026-07-17T02:34:03.0239759+00:00 | 128.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,036,000 | 2026-07-17T02:34:03.0285161+00:00 | 129.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,040,000 | 2026-07-17T02:34:03.0339882+00:00 | 227.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,044,000 | 2026-07-17T02:34:03.0384679+00:00 | 129.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,046,000 | 2026-07-17T02:34:03.0397926+00:00 | 127.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,050,000 | 2026-07-17T02:34:03.043098+00:00 | 227.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,054,000 | 2026-07-17T02:34:03.0494794+00:00 | 118.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,056,000 | 2026-07-17T02:34:03.0527863+00:00 | 131.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,060,000 | 2026-07-17T02:34:03.0598607+00:00 | 218.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,064,000 | 2026-07-17T02:34:03.0658445+00:00 | 129.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,066,000 | 2026-07-17T02:34:03.0711803+00:00 | 124.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,070,000 | 2026-07-17T02:34:03.0772902+00:00 | 203.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,074,000 | 2026-07-17T02:34:03.0834927+00:00 | 125.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,076,000 | 2026-07-17T02:34:03.0848153+00:00 | 124.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,080,000 | 2026-07-17T02:34:03.0914515+00:00 | 200.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,084,000 | 2026-07-17T02:34:03.0972199+00:00 | 112.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,086,000 | 2026-07-17T02:34:03.1053922+00:00 | 103.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,090,000 | 2026-07-17T02:34:03.1083698+00:00 | 184.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,100,000 | 2026-07-17T02:34:03.1282959+00:00 | 165.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,104,000 | 2026-07-17T02:34:03.1315442+00:00 | 124.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,106,000 | 2026-07-17T02:34:03.1323635+00:00 | 123.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,110,000 | 2026-07-17T02:34:03.1352975+00:00 | 166.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,114,000 | 2026-07-17T02:34:03.1381061+00:00 | 117.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,116,000 | 2026-07-17T02:34:03.1392583+00:00 | 116.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,120,000 | 2026-07-17T02:34:03.1548468+00:00 | 146.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,130,000 | 2026-07-17T02:34:03.1652174+00:00 | 140.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,140,000 | 2026-07-17T02:34:03.1781785+00:00 | 133.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,150,000 | 2026-07-17T02:34:03.1896165+00:00 | 122.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,160,000 | 2026-07-17T02:34:03.1994911+00:00 | 122.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,170,000 | 2026-07-17T02:34:03.2093668+00:00 | 117.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,180,000 | 2026-07-17T02:34:03.2172559+00:00 | 109.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,181,000 | 2026-07-17T02:34:03.218335+00:00 | 162.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,182,000 | 2026-07-17T02:34:03.2192232+00:00 | 161.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,190,000 | 2026-07-17T02:34:03.2275382+00:00 | 105.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,191,000 | 2026-07-17T02:34:03.2282362+00:00 | 181.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,192,000 | 2026-07-17T02:34:03.2291045+00:00 | 180.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,199,000 | 2026-07-17T02:34:03.2346166+00:00 | 120.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,200,000 | 2026-07-17T02:34:03.2359431+00:00 | 100.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,201,000 | 2026-07-17T02:34:03.2366688+00:00 | 173.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,202,000 | 2026-07-17T02:34:03.2371011+00:00 | 172.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,209,000 | 2026-07-17T02:34:03.2711817+00:00 | 109.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,211,000 | 2026-07-17T02:34:03.2719567+00:00 | 180.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,212,000 | 2026-07-17T02:34:03.2727994+00:00 | 179.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,213,000 | 2026-07-17T02:34:03.2730902+00:00 | 107.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,219,000 | 2026-07-17T02:34:03.2769467+00:00 | 103.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,221,000 | 2026-07-17T02:34:03.2790003+00:00 | 181.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,222,000 | 2026-07-17T02:34:03.2842722+00:00 | 176.3ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,223,000 | 2026-07-17T02:34:03.2847852+00:00 | 124.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,229,000 | 2026-07-17T02:34:03.2892481+00:00 | 120.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,231,000 | 2026-07-17T02:34:03.2930349+00:00 | 171.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,232,000 | 2026-07-17T02:34:03.2933375+00:00 | 171.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,233,000 | 2026-07-17T02:34:03.2948347+00:00 | 114.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,239,000 | 2026-07-17T02:34:03.307343+00:00 | 102.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,241,000 | 2026-07-17T02:34:03.3086059+00:00 | 178.9ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,242,000 | 2026-07-17T02:34:03.3089195+00:00 | 178.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,251,000 | 2026-07-17T02:34:03.3230977+00:00 | 180.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,252,000 | 2026-07-17T02:34:03.3234801+00:00 | 180.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,261,000 | 2026-07-17T02:34:03.3354327+00:00 | 168.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,262,000 | 2026-07-17T02:34:03.3357095+00:00 | 168.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,271,000 | 2026-07-17T02:34:03.3465717+00:00 | 174.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,272,000 | 2026-07-17T02:34:03.3471252+00:00 | 173.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,281,000 | 2026-07-17T02:34:03.3605406+00:00 | 160.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,282,000 | 2026-07-17T02:34:03.3618957+00:00 | 159.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,291,000 | 2026-07-17T02:34:03.373992+00:00 | 147.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,292,000 | 2026-07-17T02:34:03.3748002+00:00 | 146.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,301,000 | 2026-07-17T02:34:03.3838416+00:00 | 171.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,302,000 | 2026-07-17T02:34:03.3846644+00:00 | 170.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,311,000 | 2026-07-17T02:34:03.3907139+00:00 | 164.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,312,000 | 2026-07-17T02:34:03.3914035+00:00 | 178.4ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,321,000 | 2026-07-17T02:34:03.3973357+00:00 | 172.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,322,000 | 2026-07-17T02:34:03.3982081+00:00 | 171.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,331,000 | 2026-07-17T02:34:03.4072+00:00 | 164.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,332,000 | 2026-07-17T02:34:03.4077258+00:00 | 163.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,341,000 | 2026-07-17T02:34:03.4159322+00:00 | 159.1ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,342,000 | 2026-07-17T02:34:03.4167103+00:00 | 210.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,351,000 | 2026-07-17T02:34:03.4259231+00:00 | 201.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,352,000 | 2026-07-17T02:34:03.4283106+00:00 | 199.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,361,000 | 2026-07-17T02:34:03.4380082+00:00 | 189.5ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,362,000 | 2026-07-17T02:34:03.4384913+00:00 | 189.0ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,371,000 | 2026-07-17T02:34:03.447358+00:00 | 180.2ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,372,000 | 2026-07-17T02:34:03.4478747+00:00 | 179.6ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,381,000 | 2026-07-17T02:34:03.4527086+00:00 | 174.8ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,382,000 | 2026-07-17T02:34:03.4537879+00:00 | 173.7ms | GC pause | - | - | 2.0s / 910,483 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,391,000 | 2026-07-17T02:34:03.467037+00:00 | 185.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,392,000 | 2026-07-17T02:34:03.4682372+00:00 | 184.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,401,000 | 2026-07-17T02:34:03.4787282+00:00 | 187.5ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,402,000 | 2026-07-17T02:34:03.4798734+00:00 | 186.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,411,000 | 2026-07-17T02:34:03.4888398+00:00 | 177.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,412,000 | 2026-07-17T02:34:03.489662+00:00 | 176.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,421,000 | 2026-07-17T02:34:03.5043027+00:00 | 183.7ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,422,000 | 2026-07-17T02:34:03.5047136+00:00 | 183.3ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,431,000 | 2026-07-17T02:34:03.5220614+00:00 | 165.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,432,000 | 2026-07-17T02:34:03.5243038+00:00 | 175.0ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,440,000 | 2026-07-17T02:34:03.5295901+00:00 | 141.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,441,000 | 2026-07-17T02:34:03.5301386+00:00 | 169.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,442,000 | 2026-07-17T02:34:03.5304959+00:00 | 168.8ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,450,000 | 2026-07-17T02:34:03.5359948+00:00 | 142.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,451,000 | 2026-07-17T02:34:03.5368694+00:00 | 162.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,452,000 | 2026-07-17T02:34:03.5372645+00:00 | 162.0ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,460,000 | 2026-07-17T02:34:03.543192+00:00 | 134.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,461,000 | 2026-07-17T02:34:03.5437235+00:00 | 155.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,462,000 | 2026-07-17T02:34:03.5444682+00:00 | 161.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,470,000 | 2026-07-17T02:34:03.5499017+00:00 | 129.5ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,471,000 | 2026-07-17T02:34:03.5506725+00:00 | 155.7ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,472,000 | 2026-07-17T02:34:03.5510687+00:00 | 155.3ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,480,000 | 2026-07-17T02:34:03.559232+00:00 | 120.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,481,000 | 2026-07-17T02:34:03.5601245+00:00 | 156.0ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,482,000 | 2026-07-17T02:34:03.5606142+00:00 | 155.5ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,486,000 | 2026-07-17T02:34:03.5646133+00:00 | 100.5ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,490,000 | 2026-07-17T02:34:03.5689763+00:00 | 110.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,491,000 | 2026-07-17T02:34:03.5693927+00:00 | 154.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,492,000 | 2026-07-17T02:34:03.5720896+00:00 | 151.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,500,000 | 2026-07-17T02:34:03.5779061+00:00 | 108.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,501,000 | 2026-07-17T02:34:03.5784692+00:00 | 145.5ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,502,000 | 2026-07-17T02:34:03.5788748+00:00 | 145.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,510,000 | 2026-07-17T02:34:03.5846484+00:00 | 105.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,511,000 | 2026-07-17T02:34:03.5855975+00:00 | 143.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,512,000 | 2026-07-17T02:34:03.5864172+00:00 | 142.8ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,513,000 | 2026-07-17T02:34:03.5868482+00:00 | 101.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,521,000 | 2026-07-17T02:34:03.591226+00:00 | 147.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,522,000 | 2026-07-17T02:34:03.5919379+00:00 | 151.0ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,529,000 | 2026-07-17T02:34:03.5970604+00:00 | 102.7ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,531,000 | 2026-07-17T02:34:03.5985214+00:00 | 157.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,532,000 | 2026-07-17T02:34:03.5990346+00:00 | 156.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,533,000 | 2026-07-17T02:34:03.5996224+00:00 | 106.7ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,539,000 | 2026-07-17T02:34:03.6037851+00:00 | 102.6ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,541,000 | 2026-07-17T02:34:03.6054385+00:00 | 150.5ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,542,000 | 2026-07-17T02:34:03.6059306+00:00 | 150.0ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,543,000 | 2026-07-17T02:34:03.6068143+00:00 | 109.3ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,549,000 | 2026-07-17T02:34:03.6130042+00:00 | 103.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,551,000 | 2026-07-17T02:34:03.6279952+00:00 | 127.9ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,552,000 | 2026-07-17T02:34:03.6300431+00:00 | 131.0ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,561,000 | 2026-07-17T02:34:03.637659+00:00 | 123.4ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,562,000 | 2026-07-17T02:34:03.6379799+00:00 | 123.1ms | GC pause | - | - | 3.0s / 1,086,974 msg/s | Gen2 +1 / pause +2.5ms |
| Dekaf (3conn) | 1,582,000 | 2026-07-17T02:34:03.6653935+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,086,974 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*3,738 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.52x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.36x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,544,647 | 1,521,592–1,568,052 | 0.94 | 1.29x |
| Confluent | 2 | 1,200,897 | 1,160,314–1,242,899 | 1.46 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.90 | 922.14 | 1,547,334 | 1,568,052 | +1.0% | +0.17% | 1475.65 | 1,547,334 | 0 | 1.39 |
| Dekaf (confluent-first) | 0.99 | 1009.64 | 1,478,717 | 1,521,592 | -1.1% | -0.07% | 1410.21 | 1,478,717 | 0 | 1.46 |
| Confluent (dekaf-first) | 1.41 | - | 1,234,254 | 1,242,899 | -1.1% | +0.04% | 1177.08 | 1,234,254 | 0 | 1.74 |
| Confluent (confluent-first) | 1.50 | - | 1,170,328 | 1,160,314 | +3.4% | +0.23% | 1116.11 | 1,170,328 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,302,997 | 1447.76 | 1015.35 KB |
| Dekaf | 1 | 1,360,132 | 1511.24 | 1017.83 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:48.143999+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 521,986 msg/s |
| Dekaf | 2026-07-17T02:19:06.1463126+00:00 | 1 | 16.0 MiB / 13.3 MiB | 1671.7 MB/s | 0/0 | 19,953 | 18.0s / 1,494,342 msg/s |
| Dekaf | 2026-07-17T02:19:24.1551896+00:00 | 1 | 16.0 MiB / 15.7 MiB | 1671.7 MB/s | 0/0 | 43,938 | 36.0s / 1,525,850 msg/s |
| Dekaf | 2026-07-17T02:19:43.1641862+00:00 | 1 | 16.0 MiB / 3.1 MiB | 1671.7 MB/s | 0/1 | 59,856 | 55.0s / 733,565 msg/s |
| Dekaf | 2026-07-17T02:20:01.1712299+00:00 | 1 | 16.0 MiB / 2.4 MiB | 1671.7 MB/s | 0/1 | 61,873 | 73.0s / 877,994 msg/s |
| Dekaf | 2026-07-17T02:20:19.1772316+00:00 | 1 | 18.0 MiB / 17.9 MiB | 1671.7 MB/s | 1/1 | 83,437 | 91.0s / 1,478,623 msg/s |
| Dekaf | 2026-07-17T02:20:37.1914083+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1671.7 MB/s | 1/1 | 102,905 | 109.0s / 1,490,841 msg/s |
| Dekaf | 2026-07-17T02:20:55.1992342+00:00 | 1 | 18.0 MiB / 14.6 MiB | 1671.7 MB/s | 1/1 | 124,590 | 127.1s / 1,482,498 msg/s |
| Dekaf | 2026-07-17T02:21:13.2050355+00:00 | 1 | 20.0 MiB / 18.2 MiB | 1671.7 MB/s | 2/1 | 145,223 | 145.1s / 1,507,454 msg/s |
| Dekaf | 2026-07-17T02:21:32.218362+00:00 | 1 | 20.0 MiB / 16.1 MiB | 1671.7 MB/s | 2/1 | 168,322 | 164.1s / 1,532,080 msg/s |
| Dekaf | 2026-07-17T02:21:50.2330504+00:00 | 1 | 22.0 MiB / 21.1 MiB | 1671.7 MB/s | 3/1 | 191,158 | 182.1s / 1,509,458 msg/s |
| Dekaf | 2026-07-17T02:22:08.2412371+00:00 | 1 | 22.0 MiB / 19.1 MiB | 1671.7 MB/s | 3/1 | 214,275 | 200.1s / 1,494,934 msg/s |
| Dekaf | 2026-07-17T02:22:26.2497763+00:00 | 1 | 22.0 MiB / 14.1 MiB | 1671.7 MB/s | 3/1 | 236,574 | 218.1s / 1,524,070 msg/s |
| Dekaf | 2026-07-17T02:22:44.2603767+00:00 | 1 | 22.0 MiB / 22.0 MiB | 1671.7 MB/s | 3/2 | 259,755 | 236.1s / 1,418,576 msg/s |
| Dekaf | 2026-07-17T02:23:02.268503+00:00 | 1 | 22.0 MiB / 18.6 MiB | 1671.7 MB/s | 3/2 | 284,169 | 254.1s / 1,581,923 msg/s |
| Dekaf | 2026-07-17T02:23:21.2764539+00:00 | 1 | 19.0 MiB / 18.6 MiB | 1702.7 MB/s | 4/2 | 310,051 | 273.1s / 1,561,919 msg/s |
| Dekaf | 2026-07-17T02:23:39.2842579+00:00 | 1 | 16.0 MiB / 15.9 MiB | 1702.7 MB/s | 5/2 | 336,463 | 291.1s / 1,597,260 msg/s |
| Dekaf | 2026-07-17T02:23:57.2934805+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1744.9 MB/s | 5/2 | 364,093 | 309.1s / 1,591,638 msg/s |
| Dekaf | 2026-07-17T02:24:15.2996699+00:00 | 1 | 16.0 MiB / 15.8 MiB | 1744.9 MB/s | 5/2 | 394,648 | 327.1s / 1,555,277 msg/s |
| Dekaf | 2026-07-17T02:24:33.3100206+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1744.9 MB/s | 6/2 | 434,896 | 345.2s / 1,587,752 msg/s |
| Dekaf | 2026-07-17T02:24:51.3195809+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1763.4 MB/s | 6/2 | 477,179 | 363.2s / 1,620,716 msg/s |
| Dekaf | 2026-07-17T02:25:10.3292171+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1763.4 MB/s | 6/3 | 526,707 | 382.2s / 1,606,870 msg/s |
| Dekaf | 2026-07-17T02:25:28.3355396+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1763.4 MB/s | 6/3 | 570,761 | 400.2s / 1,599,294 msg/s |
| Dekaf | 2026-07-17T02:25:46.3413598+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1763.4 MB/s | 6/3 | 610,246 | 418.2s / 1,627,814 msg/s |
| Dekaf | 2026-07-17T02:26:04.3435763+00:00 | 1 | 15.0 MiB / 13.7 MiB | 1763.4 MB/s | 7/3 | 647,233 | 436.2s / 1,558,392 msg/s |
| Dekaf | 2026-07-17T02:26:22.3482978+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1763.4 MB/s | 7/3 | 683,584 | 454.2s / 1,610,961 msg/s |
| Dekaf | 2026-07-17T02:26:40.3542168+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1763.4 MB/s | 8/3 | 713,313 | 472.2s / 1,589,953 msg/s |
| Dekaf | 2026-07-17T02:26:59.3659139+00:00 | 1 | 16.0 MiB / 15.1 MiB | 1763.4 MB/s | 8/3 | 741,716 | 491.2s / 1,597,146 msg/s |
| Dekaf | 2026-07-17T02:27:17.3772219+00:00 | 1 | 16.0 MiB / 13.3 MiB | 1763.4 MB/s | 8/3 | 765,908 | 509.2s / 1,545,285 msg/s |
| Dekaf | 2026-07-17T02:27:35.386696+00:00 | 1 | 18.0 MiB / 15.5 MiB | 1763.4 MB/s | 9/3 | 786,717 | 527.2s / 1,546,885 msg/s |
| Dekaf | 2026-07-17T02:27:53.3936299+00:00 | 1 | 20.0 MiB / 18.1 MiB | 1763.4 MB/s | 9/3 | 810,338 | 545.2s / 1,607,635 msg/s |
| Dekaf | 2026-07-17T02:28:11.4068371+00:00 | 1 | 18.0 MiB / 17.0 MiB | 1763.4 MB/s | 9/4 | 830,788 | 563.2s / 1,597,960 msg/s |
| Dekaf | 2026-07-17T02:28:29.4174477+00:00 | 1 | 18.0 MiB / 16.9 MiB | 1763.4 MB/s | 9/4 | 852,598 | 581.2s / 1,526,212 msg/s |
| Dekaf | 2026-07-17T02:28:48.4225522+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1763.4 MB/s | 9/4 | 878,161 | 600.2s / 1,502,182 msg/s |
| Dekaf | 2026-07-17T02:29:06.4318947+00:00 | 1 | 18.0 MiB / 4.6 MiB | 1763.4 MB/s | 9/5 | 904,655 | 618.3s / 1,557,262 msg/s |
| Dekaf | 2026-07-17T02:29:24.4386342+00:00 | 1 | 20.0 MiB / 20.0 MiB | 1763.4 MB/s | 9/5 | 927,245 | 636.3s / 1,461,822 msg/s |
| Dekaf | 2026-07-17T02:29:42.4468674+00:00 | 1 | 18.0 MiB / 18.0 MiB | 1763.4 MB/s | 9/6 | 950,309 | 654.3s / 1,589,512 msg/s |
| Dekaf | 2026-07-17T02:30:00.4619479+00:00 | 1 | 18.0 MiB / 16.6 MiB | 1763.4 MB/s | 9/6 | 971,392 | 672.3s / 1,546,160 msg/s |
| Dekaf | 2026-07-17T02:30:19.4684036+00:00 | 1 | 15.0 MiB / 14.5 MiB | 1763.4 MB/s | 9/6 | 998,374 | 691.3s / 1,502,301 msg/s |
| Dekaf | 2026-07-17T02:30:37.4763222+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1763.4 MB/s | 10/6 | 1,028,288 | 709.3s / 1,273,978 msg/s |
| Dekaf | 2026-07-17T02:30:55.4792817+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1763.4 MB/s | 10/6 | 1,055,434 | 727.3s / 1,497,377 msg/s |
| Dekaf | 2026-07-17T02:31:13.4855146+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1763.4 MB/s | 11/6 | 1,086,522 | 745.3s / 1,507,262 msg/s |
| Dekaf | 2026-07-17T02:31:31.4879802+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1763.4 MB/s | 11/6 | 1,114,735 | 763.3s / 1,243,348 msg/s |
| Dekaf | 2026-07-17T02:31:49.4954438+00:00 | 1 | 11.0 MiB / 10.6 MiB | 1763.4 MB/s | 11/6 | 1,146,209 | 781.3s / 1,259,598 msg/s |
| Dekaf | 2026-07-17T02:32:08.4995329+00:00 | 1 | 13.0 MiB / 11.9 MiB | 1763.4 MB/s | 11/7 | 1,180,958 | 800.3s / 1,204,765 msg/s |
| Dekaf | 2026-07-17T02:32:26.5052533+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1763.4 MB/s | 11/7 | 1,217,541 | 818.3s / 1,352,965 msg/s |
| Dekaf | 2026-07-17T02:32:44.5113388+00:00 | 1 | 13.0 MiB / 11.9 MiB | 1763.4 MB/s | 11/8 | 1,252,030 | 836.3s / 1,350,500 msg/s |
| Dekaf | 2026-07-17T02:33:02.5172497+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1763.4 MB/s | 11/8 | 1,288,084 | 854.3s / 1,396,257 msg/s |
| Dekaf | 2026-07-17T02:33:20.5224764+00:00 | 1 | 11.0 MiB / 10.8 MiB | 1763.4 MB/s | 11/8 | 1,323,140 | 872.3s / 1,383,386 msg/s |
| Dekaf | 2026-07-17T02:33:38.5272592+00:00 | 1 | 11.0 MiB / 2.7 MiB | 1763.4 MB/s | 12/8 | 1,369,572 | 890.3s / 1,265,289 msg/s |
| Dekaf | 2026-07-17T02:33:58.024605+00:00 | 1 | 16.0 MiB / 15.2 MiB | 1625.1 MB/s | 0/0 | 10,041 | 9.0s / 1,433,252 msg/s |
| Dekaf | 2026-07-17T02:34:16.034638+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1625.1 MB/s | 0/0 | 36,856 | 27.0s / 1,449,488 msg/s |
| Dekaf | 2026-07-17T02:34:34.0432909+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1628.2 MB/s | 0/0 | 69,127 | 45.0s / 1,440,690 msg/s |
| Dekaf | 2026-07-17T02:34:52.0590708+00:00 | 1 | 14.0 MiB / 13.2 MiB | 1673.1 MB/s | 1/0 | 114,502 | 63.0s / 1,489,521 msg/s |
| Dekaf | 2026-07-17T02:35:10.0686894+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1693.0 MB/s | 1/0 | 165,397 | 81.0s / 1,481,961 msg/s |
| Dekaf | 2026-07-17T02:35:28.075234+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1766.2 MB/s | 2/0 | 219,305 | 99.0s / 1,586,987 msg/s |
| Dekaf | 2026-07-17T02:35:47.0826386+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1766.2 MB/s | 2/0 | 276,367 | 118.0s / 1,642,557 msg/s |
| Dekaf | 2026-07-17T02:36:05.0864881+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1766.2 MB/s | 2/1 | 330,509 | 136.0s / 1,544,003 msg/s |
| Dekaf | 2026-07-17T02:36:23.0923377+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1766.2 MB/s | 2/1 | 390,439 | 154.1s / 1,623,036 msg/s |
| Dekaf | 2026-07-17T02:36:41.1005793+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/1 | 448,384 | 172.1s / 1,618,471 msg/s |
| Dekaf | 2026-07-17T02:36:59.1066422+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/2 | 505,016 | 190.1s / 1,536,884 msg/s |
| Dekaf | 2026-07-17T02:37:17.1144916+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/2 | 561,562 | 208.1s / 1,596,847 msg/s |
| Dekaf | 2026-07-17T02:37:36.1201737+00:00 | 1 | 12.0 MiB / 11.5 MiB | 1770.1 MB/s | 2/3 | 613,939 | 227.1s / 1,498,836 msg/s |
| Dekaf | 2026-07-17T02:37:54.1255148+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/3 | 667,903 | 245.1s / 1,552,285 msg/s |
| Dekaf | 2026-07-17T02:38:12.1283796+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/3 | 720,190 | 263.1s / 1,572,673 msg/s |
| Dekaf | 2026-07-17T02:38:30.1325213+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/4 | 773,322 | 281.1s / 1,556,786 msg/s |
| Dekaf | 2026-07-17T02:38:48.1362166+00:00 | 1 | 12.0 MiB / 11.3 MiB | 1770.1 MB/s | 2/4 | 828,446 | 299.1s / 1,513,561 msg/s |
| Dekaf | 2026-07-17T02:39:07.1396346+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/5 | 881,177 | 318.1s / 1,530,729 msg/s |
| Dekaf | 2026-07-17T02:39:25.1422672+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 2/5 | 935,368 | 336.1s / 1,598,563 msg/s |
| Dekaf | 2026-07-17T02:39:43.1495078+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1770.1 MB/s | 2/5 | 988,667 | 354.1s / 1,546,785 msg/s |
| Dekaf | 2026-07-17T02:40:01.1580956+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1770.1 MB/s | 3/5 | 1,040,397 | 372.1s / 1,580,308 msg/s |
| Dekaf | 2026-07-17T02:40:19.16371+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1770.1 MB/s | 3/5 | 1,093,568 | 390.1s / 1,572,864 msg/s |
| Dekaf | 2026-07-17T02:40:37.1724986+00:00 | 1 | 14.0 MiB / 13.7 MiB | 1770.1 MB/s | 4/5 | 1,141,655 | 408.1s / 1,574,697 msg/s |
| Dekaf | 2026-07-17T02:40:56.1825758+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1770.1 MB/s | 4/5 | 1,192,489 | 427.1s / 1,576,268 msg/s |
| Dekaf | 2026-07-17T02:41:14.1897853+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1770.1 MB/s | 4/5 | 1,239,951 | 445.1s / 1,582,891 msg/s |
| Dekaf | 2026-07-17T02:41:32.196448+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1770.1 MB/s | 5/5 | 1,282,412 | 463.1s / 1,563,491 msg/s |
| Dekaf | 2026-07-17T02:41:50.2052467+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1770.1 MB/s | 5/5 | 1,323,346 | 481.2s / 1,577,483 msg/s |
| Dekaf | 2026-07-17T02:42:08.2197492+00:00 | 1 | 15.0 MiB / 15.0 MiB | 1770.1 MB/s | 5/6 | 1,358,436 | 499.2s / 1,564,375 msg/s |
| Dekaf | 2026-07-17T02:42:26.2322382+00:00 | 1 | 15.0 MiB / 14.9 MiB | 1770.1 MB/s | 5/6 | 1,396,422 | 517.2s / 1,615,035 msg/s |
| Dekaf | 2026-07-17T02:42:45.2420777+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1770.1 MB/s | 5/6 | 1,440,878 | 536.2s / 1,541,660 msg/s |
| Dekaf | 2026-07-17T02:43:03.2506547+00:00 | 1 | 13.0 MiB / 12.1 MiB | 1770.1 MB/s | 6/6 | 1,493,288 | 554.2s / 1,629,789 msg/s |
| Dekaf | 2026-07-17T02:43:21.2574262+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.1 MB/s | 6/6 | 1,544,824 | 572.2s / 1,611,529 msg/s |
| Dekaf | 2026-07-17T02:43:39.2650943+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.1 MB/s | 7/6 | 1,600,804 | 590.2s / 1,607,386 msg/s |
| Dekaf | 2026-07-17T02:43:57.2732225+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.1 MB/s | 7/6 | 1,657,776 | 608.2s / 1,581,035 msg/s |
| Dekaf | 2026-07-17T02:44:15.2866079+00:00 | 1 | 11.0 MiB / 10.7 MiB | 1770.1 MB/s | 7/6 | 1,707,201 | 626.2s / 1,584,631 msg/s |
| Dekaf | 2026-07-17T02:44:34.2902165+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.1 MB/s | 7/7 | 1,761,020 | 645.2s / 1,575,233 msg/s |
| Dekaf | 2026-07-17T02:44:52.2951733+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1770.1 MB/s | 7/7 | 1,815,122 | 663.2s / 1,605,853 msg/s |
| Dekaf | 2026-07-17T02:45:10.3004441+00:00 | 1 | 11.0 MiB / 8.2 MiB | 1770.1 MB/s | 7/8 | 1,871,636 | 681.2s / 1,541,403 msg/s |
| Dekaf | 2026-07-17T02:45:28.3126996+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1770.1 MB/s | 7/8 | 1,926,931 | 699.2s / 1,568,838 msg/s |
| Dekaf | 2026-07-17T02:45:46.3140866+00:00 | 1 | 9.0 MiB / 6.5 MiB | 1770.1 MB/s | 7/8 | 1,975,293 | 717.2s / 1,443,441 msg/s |
| Dekaf | 2026-07-17T02:46:04.3178642+00:00 | 1 | 11.0 MiB / 10.5 MiB | 1770.1 MB/s | 7/9 | 2,025,851 | 735.2s / 1,578,921 msg/s |
| Dekaf | 2026-07-17T02:46:23.3192542+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1770.1 MB/s | 7/9 | 2,082,069 | 754.2s / 1,542,947 msg/s |
| Dekaf | 2026-07-17T02:46:41.3233748+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.1 MB/s | 7/10 | 2,137,978 | 772.2s / 1,556,300 msg/s |
| Dekaf | 2026-07-17T02:46:59.3277981+00:00 | 1 | 11.0 MiB / 10.1 MiB | 1770.1 MB/s | 7/10 | 2,194,036 | 790.2s / 1,531,123 msg/s |
| Dekaf | 2026-07-17T02:47:17.3343374+00:00 | 1 | 9.0 MiB / 9.0 MiB | 1770.1 MB/s | 7/10 | 2,239,912 | 808.2s / 1,465,088 msg/s |
| Dekaf | 2026-07-17T02:47:35.3394808+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1770.1 MB/s | 7/11 | 2,291,062 | 826.2s / 1,596,541 msg/s |
| Dekaf | 2026-07-17T02:47:53.3468121+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1770.1 MB/s | 7/11 | 2,345,698 | 844.2s / 1,619,490 msg/s |
| Dekaf | 2026-07-17T02:48:12.3575223+00:00 | 1 | 12.0 MiB / 10.6 MiB | 1770.1 MB/s | 8/11 | 2,402,945 | 863.2s / 1,605,082 msg/s |
| Dekaf | 2026-07-17T02:48:30.3616444+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1770.1 MB/s | 8/11 | 2,456,841 | 881.2s / 1,583,024 msg/s |
| Dekaf | 2026-07-17T02:48:48.3679597+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1770.1 MB/s | 8/11 | 2,511,427 | 899.3s / 1,566,707 msg/s |
*1,700 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-17T02:19:18.2611038+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.8 MiB |
| Dekaf | 2026-07-17T02:19:33.2947727+00:00 | 1 | capacity | failed | 15,033ms | 16.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-17T02:20:03.3422838+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-17T02:20:18.364782+00:00 | 1 | capacity | succeeded | 15,022ms | 18.0 MiB / 11.6 MiB |
| Dekaf | 2026-07-17T02:20:48.4146976+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-17T02:21:03.4371813+00:00 | 1 | capacity | succeeded | 15,022ms | 20.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-17T02:21:33.4900011+00:00 | 1 | capacity | started | 0ms | 22.0 MiB / 17.1 MiB |
| Dekaf | 2026-07-17T02:21:48.5131201+00:00 | 1 | capacity | succeeded | 15,023ms | 22.0 MiB / 19.1 MiB |
| Dekaf | 2026-07-17T02:22:18.5616401+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 13.2 MiB |
| Dekaf | 2026-07-17T02:22:33.5945067+00:00 | 1 | capacity | failed | 15,032ms | 22.0 MiB / 21.1 MiB |
| Dekaf | 2026-07-17T02:23:03.6397379+00:00 | 1 | capacity | started | 0ms | 19.0 MiB / 9.4 MiB |
| Dekaf | 2026-07-17T02:23:18.6611115+00:00 | 1 | capacity | succeeded | 15,021ms | 19.0 MiB / 14.7 MiB |
| Dekaf | 2026-07-17T02:23:21.6650203+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-17T02:23:36.6838666+00:00 | 1 | capacity | succeeded | 15,018ms | 16.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-17T02:24:06.7189883+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.8 MiB |
| Dekaf | 2026-07-17T02:24:21.7344102+00:00 | 1 | capacity | succeeded | 15,015ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:24:51.7872208+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:25:06.8058196+00:00 | 1 | capacity | failed | 15,018ms | 14.0 MiB / 7.2 MiB |
| Dekaf | 2026-07-17T02:25:36.8336989+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:25:51.8452115+00:00 | 1 | capacity | succeeded | 15,011ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:26:21.8768794+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:26:36.8944675+00:00 | 1 | capacity | succeeded | 15,017ms | 16.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-17T02:27:06.927449+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 14.8 MiB |
| Dekaf | 2026-07-17T02:27:21.9482648+00:00 | 1 | capacity | succeeded | 15,020ms | 18.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:27:51.991865+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 16.3 MiB |
| Dekaf | 2026-07-17T02:28:07.0134304+00:00 | 1 | capacity | failed | 15,021ms | 18.0 MiB / 15.9 MiB |
| Dekaf | 2026-07-17T02:28:37.0600493+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 16.9 MiB |
| Dekaf | 2026-07-17T02:28:52.0802094+00:00 | 1 | capacity | failed | 15,020ms | 18.0 MiB / 13.2 MiB |
| Dekaf | 2026-07-17T02:29:22.1174542+00:00 | 1 | capacity | started | 0ms | 20.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:29:37.1342384+00:00 | 1 | capacity | failed | 15,016ms | 18.0 MiB / 18.1 MiB |
| Dekaf | 2026-07-17T02:30:07.1674066+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-17T02:30:22.1834478+00:00 | 1 | capacity | succeeded | 15,016ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:30:52.2158379+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:31:07.2272959+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:31:37.2546758+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:31:52.2822296+00:00 | 1 | capacity | failed | 15,027ms | 13.0 MiB / 9.6 MiB |
| Dekaf | 2026-07-17T02:32:22.3145665+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-17T02:32:37.3293789+00:00 | 1 | capacity | failed | 15,014ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:33:07.3551124+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-17T02:33:22.371066+00:00 | 1 | capacity | succeeded | 15,016ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:34:19.1307168+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:34:34.1446562+00:00 | 1 | capacity | succeeded | 15,013ms | 14.0 MiB / 12.3 MiB |
| Dekaf | 2026-07-17T02:35:04.170161+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.8 MiB |
| Dekaf | 2026-07-17T02:35:19.1833782+00:00 | 1 | capacity | succeeded | 15,013ms | 12.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-17T02:35:49.2082099+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:36:04.2183323+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.2 MiB |
| Dekaf | 2026-07-17T02:36:34.2415133+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:36:49.2503265+00:00 | 1 | capacity | failed | 15,008ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:37:19.2897152+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:37:34.3014499+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 8.9 MiB |
| Dekaf | 2026-07-17T02:38:04.3269813+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:38:19.338347+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:38:49.3638268+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:39:04.3751831+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:39:34.3994765+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:39:49.4121595+00:00 | 1 | capacity | succeeded | 15,012ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:40:19.4362672+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 10.0 MiB |
| Dekaf | 2026-07-17T02:40:34.4512447+00:00 | 1 | capacity | succeeded | 15,014ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:41:04.4749397+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 12.6 MiB |
| Dekaf | 2026-07-17T02:41:19.4905298+00:00 | 1 | capacity | succeeded | 15,015ms | 15.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:41:49.5203808+00:00 | 1 | capacity | started | 0ms | 16.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:42:04.5397694+00:00 | 1 | capacity | failed | 15,019ms | 15.0 MiB / 13.0 MiB |
| Dekaf | 2026-07-17T02:42:34.5651193+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 14.7 MiB |
| Dekaf | 2026-07-17T02:42:49.5744664+00:00 | 1 | capacity | succeeded | 15,009ms | 13.0 MiB / 10.5 MiB |
| Dekaf | 2026-07-17T02:43:19.5999379+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:43:34.6097608+00:00 | 1 | capacity | succeeded | 15,010ms | 11.0 MiB / 9.8 MiB |
| Dekaf | 2026-07-17T02:44:04.6349747+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:44:19.651306+00:00 | 1 | capacity | failed | 15,016ms | 11.0 MiB / 7.0 MiB |
| Dekaf | 2026-07-17T02:44:49.6730722+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 9.8 MiB |
| Dekaf | 2026-07-17T02:45:04.6834974+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 10.5 MiB |
| Dekaf | 2026-07-17T02:45:34.7053509+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-17T02:45:49.7192707+00:00 | 1 | capacity | failed | 15,014ms | 11.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:46:19.7400793+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 5.5 MiB |
| Dekaf | 2026-07-17T02:46:34.7486571+00:00 | 1 | capacity | failed | 15,008ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:47:04.7900097+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-17T02:47:19.8008584+00:00 | 1 | capacity | failed | 15,010ms | 11.0 MiB / 7.4 MiB |
| Dekaf | 2026-07-17T02:47:49.8251492+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:48:04.8364822+00:00 | 1 | capacity | succeeded | 15,011ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:48:34.8584435+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |

## Producer Admission Block Durations - Producer (Acks All)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 1,098 |
| Dekaf | 1 | 0.002–0.004ms | 1,284 |
| Dekaf | 1 | 0.004–0.008ms | 4,348 |
| Dekaf | 1 | 0.008–0.016ms | 24,967 |
| Dekaf | 1 | 0.016–0.032ms | 29,841 |
| Dekaf | 1 | 0.032–0.064ms | 27,483 |
| Dekaf | 1 | 0.064–0.128ms | 47,597 |
| Dekaf | 1 | 0.128–0.256ms | 114,405 |
| Dekaf | 1 | 0.256–0.512ms | 143,878 |
| Dekaf | 1 | 0.512–1.024ms | 89,663 |
| Dekaf | 1 | 1.024–2.048ms | 30,964 |
| Dekaf | 1 | 2.048–4.096ms | 4,543 |
| Dekaf | 1 | 4.096–8.192ms | 1,251 |
| Dekaf | 1 | 8.192–16.384ms | 135 |
| Dekaf | 1 | 16.384–32.768ms | 5 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 1,615 |
| Dekaf | 1 | 0.002–0.004ms | 2,079 |
| Dekaf | 1 | 0.004–0.008ms | 9,131 |
| Dekaf | 1 | 0.008–0.016ms | 40,746 |
| Dekaf | 1 | 0.016–0.032ms | 40,772 |
| Dekaf | 1 | 0.032–0.064ms | 43,880 |
| Dekaf | 1 | 0.064–0.128ms | 89,089 |
| Dekaf | 1 | 0.128–0.256ms | 260,373 |
| Dekaf | 1 | 0.256–0.512ms | 366,385 |
| Dekaf | 1 | 0.512–1.024ms | 92,022 |
| Dekaf | 1 | 1.024–2.048ms | 20,679 |
| Dekaf | 1 | 2.048–4.096ms | 4,250 |
| Dekaf | 1 | 4.096–8.192ms | 942 |
| Dekaf | 1 | 8.192–16.384ms | 78 |
| Dekaf | 1 | 16.384–32.768ms | 6 |
| Dekaf | 1 | 32.768–65.536ms | 1 |

## Delivery Latency Outliers - Producer (Acks All)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 420,112,000 | 2026-07-17T02:09:50.0867032+00:00 | 110.4ms | GC pause | - | - | 362.3s / 999,157 msg/s | Gen2 +0 / pause +128.7ms |
| Confluent | 420,122,000 | 2026-07-17T02:09:50.093871+00:00 | 110.0ms | GC pause | - | - | 362.3s / 999,157 msg/s | Gen2 +0 / pause +128.7ms |
| Confluent | 420,126,000 | 2026-07-17T02:09:50.0957702+00:00 | 108.1ms | GC pause | - | - | 362.3s / 999,157 msg/s | Gen2 +0 / pause +128.7ms |
| Confluent | 420,129,000 | 2026-07-17T02:09:50.1034579+00:00 | 100.6ms | GC pause | - | - | 362.3s / 999,157 msg/s | Gen2 +0 / pause +128.7ms |
| Confluent | 674,241,000 | 2026-07-17T02:13:21.0222885+00:00 | 100.7ms | GC pause | - | - | 573.5s / 1,110,099 msg/s | Gen2 +0 / pause +101.5ms |
| Confluent | 674,247,000 | 2026-07-17T02:13:21.0273605+00:00 | 100.5ms | GC pause | - | - | 573.5s / 1,110,099 msg/s | Gen2 +0 / pause +101.5ms |
| Confluent | 674,257,000 | 2026-07-17T02:13:21.0344107+00:00 | 101.8ms | GC pause | - | - | 573.5s / 1,110,099 msg/s | Gen2 +0 / pause +101.5ms |
| Confluent | 674,258,000 | 2026-07-17T02:13:21.0356006+00:00 | 100.7ms | GC pause | - | - | 573.5s / 1,110,099 msg/s | Gen2 +0 / pause +101.5ms |
| Confluent | 674,267,000 | 2026-07-17T02:13:21.0469942+00:00 | 100.7ms | GC pause | - | - | 573.5s / 1,110,099 msg/s | Gen2 +0 / pause +101.5ms |
| Confluent | 241,763,000 | 2026-07-17T02:51:48.6951155+00:00 | 115.6ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 241,764,000 | 2026-07-17T02:51:48.6962627+00:00 | 114.7ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 241,762,000 | 2026-07-17T02:51:48.6992269+00:00 | 101.7ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 241,765,000 | 2026-07-17T02:51:48.7055999+00:00 | 105.6ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 241,766,000 | 2026-07-17T02:51:48.7074852+00:00 | 103.7ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 241,767,000 | 2026-07-17T02:51:48.7080711+00:00 | 103.3ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 241,768,000 | 2026-07-17T02:51:48.7086568+00:00 | 102.8ms | GC pause | - | - | 180.1s / 763,123 msg/s | Gen2 +0 / pause +210.9ms |
| Confluent | 827,758,000 | 2026-07-17T03:00:10.7324974+00:00 | 104.1ms | GC pause | - | - | 682.5s / 1,324,915 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 827,761,000 | 2026-07-17T03:00:10.7346428+00:00 | 102.1ms | GC pause | - | - | 682.5s / 1,324,915 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 827,771,000 | 2026-07-17T03:00:10.7414918+00:00 | 104.1ms | GC pause | - | - | 682.5s / 1,324,915 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 827,777,000 | 2026-07-17T03:00:10.745534+00:00 | 101.5ms | GC pause | - | - | 682.5s / 1,324,915 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 827,778,000 | 2026-07-17T03:00:10.7464068+00:00 | 100.7ms | GC pause | - | - | 682.5s / 1,324,915 msg/s | Gen2 +0 / pause +103.4ms |
| Confluent | 827,781,000 | 2026-07-17T03:00:10.7482907+00:00 | 102.8ms | GC pause | - | - | 682.5s / 1,324,915 msg/s | Gen2 +0 / pause +103.4ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

:::tip
**Dekaf uses 1.54x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.29x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.15 | 1158.41 | 1,080,362 | 1,104,948 | -8.0% | -0.80% | 1030.31 | 1,080,362 | 0 | 1.24 |
| Confluent | 1.69 | - | 899,599 | 905,092 | +3.8% | +0.36% | 857.92 | 899,599 | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Acks All), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 317,819 | 353.12 | 998.48 KB |
| Dekaf | 2 | 318,702 | 354.11 | 994.49 KB |
| Dekaf | 3 | 326,702 | 362.99 | 1017.19 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Acks All), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:55.9685754+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 793,005 msg/s |
| Dekaf | 2026-07-17T02:19:04.9799195+00:00 | 3 | 16.0 MiB / 12.9 MiB | 404.9 MB/s | 0/0 | 4,001 | 9.0s / 1,109,855 msg/s |
| Dekaf | 2026-07-17T02:19:13.9828336+00:00 | 3 | 16.0 MiB / 11.1 MiB | 417.8 MB/s | 0/0 | 8,820 | 18.0s / 1,101,435 msg/s |
| Dekaf | 2026-07-17T02:19:23.9893962+00:00 | 1 | 16.0 MiB / 15.7 MiB | 401.8 MB/s | 0/0 | 472 | 28.0s / 1,146,472 msg/s |
| Dekaf | 2026-07-17T02:19:32.9989024+00:00 | 1 | 16.0 MiB / 4.6 MiB | 401.8 MB/s | 0/0 | 931 | 37.0s / 1,012,226 msg/s |
| Dekaf | 2026-07-17T02:19:42.0013367+00:00 | 1 | 14.0 MiB / 7.9 MiB | 401.8 MB/s | 1/0 | 948 | 46.0s / 1,114,288 msg/s |
| Dekaf | 2026-07-17T02:19:51.0040903+00:00 | 1 | 14.0 MiB / 6.5 MiB | 401.8 MB/s | 1/0 | 1,229 | 55.0s / 1,119,500 msg/s |
| Dekaf | 2026-07-17T02:20:00.0089862+00:00 | 2 | 12.0 MiB / 4.4 MiB | 410.2 MB/s | 2/0 | 4,395 | 64.0s / 1,085,713 msg/s |
| Dekaf | 2026-07-17T02:20:09.0135233+00:00 | 2 | 12.0 MiB / 2.9 MiB | 410.2 MB/s | 2/0 | 4,546 | 73.0s / 1,068,556 msg/s |
| Dekaf | 2026-07-17T02:20:18.0142612+00:00 | 2 | 10.0 MiB / 9.2 MiB | 410.2 MB/s | 3/0 | 5,120 | 82.1s / 1,101,205 msg/s |
| Dekaf | 2026-07-17T02:20:27.0183894+00:00 | 2 | 10.0 MiB / 5.9 MiB | 410.2 MB/s | 3/0 | 5,751 | 91.1s / 1,109,944 msg/s |
| Dekaf | 2026-07-17T02:20:36.0208614+00:00 | 3 | 8.0 MiB / 6.8 MiB | 432.8 MB/s | 4/0 | 63,207 | 100.1s / 1,131,662 msg/s |
| Dekaf | 2026-07-17T02:20:45.0283467+00:00 | 3 | 8.0 MiB / 7.8 MiB | 432.8 MB/s | 4/0 | 70,244 | 109.1s / 1,160,827 msg/s |
| Dekaf | 2026-07-17T02:20:54.0289301+00:00 | 3 | 8.0 MiB / 6.1 MiB | 432.8 MB/s | 4/1 | 77,087 | 118.1s / 1,088,982 msg/s |
| Dekaf | 2026-07-17T02:21:03.0328502+00:00 | 3 | 8.0 MiB / 4.9 MiB | 432.8 MB/s | 4/1 | 84,528 | 127.1s / 1,120,720 msg/s |
| Dekaf | 2026-07-17T02:21:13.0375785+00:00 | 1 | 8.0 MiB / 2.8 MiB | 410.1 MB/s | 4/1 | 11,797 | 137.1s / 1,124,908 msg/s |
| Dekaf | 2026-07-17T02:21:22.0397619+00:00 | 1 | 8.0 MiB / 7.3 MiB | 410.1 MB/s | 4/1 | 13,136 | 146.1s / 1,114,612 msg/s |
| Dekaf | 2026-07-17T02:21:31.0410583+00:00 | 1 | 8.0 MiB / 6.9 MiB | 410.1 MB/s | 4/1 | 14,446 | 155.1s / 1,113,829 msg/s |
| Dekaf | 2026-07-17T02:21:40.045732+00:00 | 1 | 8.0 MiB / 2.6 MiB | 410.1 MB/s | 4/2 | 15,038 | 164.1s / 1,052,853 msg/s |
| Dekaf | 2026-07-17T02:21:49.0478707+00:00 | 2 | 8.0 MiB / 5.4 MiB | 410.2 MB/s | 4/1 | 17,171 | 173.1s / 1,115,224 msg/s |
| Dekaf | 2026-07-17T02:21:58.0529456+00:00 | 2 | 8.0 MiB / 2.7 MiB | 410.2 MB/s | 4/1 | 17,485 | 182.1s / 1,142,866 msg/s |
| Dekaf | 2026-07-17T02:22:07.0537629+00:00 | 2 | 8.0 MiB / 7.5 MiB | 410.2 MB/s | 4/2 | 18,122 | 191.1s / 1,141,212 msg/s |
| Dekaf | 2026-07-17T02:22:16.0534182+00:00 | 2 | 8.0 MiB / 6.7 MiB | 410.2 MB/s | 4/2 | 19,068 | 200.1s / 1,134,129 msg/s |
| Dekaf | 2026-07-17T02:22:25.0574227+00:00 | 3 | 7.0 MiB / 6.4 MiB | 432.8 MB/s | 5/2 | 145,159 | 209.1s / 1,027,928 msg/s |
| Dekaf | 2026-07-17T02:22:34.0603783+00:00 | 3 | 7.0 MiB / 7.0 MiB | 432.8 MB/s | 5/2 | 150,737 | 218.1s / 1,162,797 msg/s |
| Dekaf | 2026-07-17T02:22:43.0608617+00:00 | 3 | 7.0 MiB / 5.0 MiB | 432.8 MB/s | 5/3 | 156,422 | 227.1s / 1,075,396 msg/s |
| Dekaf | 2026-07-17T02:22:52.0640507+00:00 | 3 | 7.0 MiB / 4.2 MiB | 432.8 MB/s | 5/3 | 162,193 | 236.1s / 1,172,549 msg/s |
| Dekaf | 2026-07-17T02:23:02.0704634+00:00 | 1 | 9.0 MiB / 4.7 MiB | 410.1 MB/s | 5/2 | 27,098 | 246.1s / 1,152,841 msg/s |
| Dekaf | 2026-07-17T02:23:11.0800507+00:00 | 1 | 9.0 MiB / 8.4 MiB | 410.1 MB/s | 5/3 | 28,189 | 255.1s / 1,104,258 msg/s |
| Dekaf | 2026-07-17T02:23:20.0810546+00:00 | 1 | 9.0 MiB / 3.2 MiB | 410.1 MB/s | 5/3 | 29,485 | 264.1s / 1,132,499 msg/s |
| Dekaf | 2026-07-17T02:23:29.0832877+00:00 | 1 | 9.0 MiB / 3.4 MiB | 410.1 MB/s | 5/3 | 30,398 | 273.1s / 1,092,567 msg/s |
| Dekaf | 2026-07-17T02:23:38.0878535+00:00 | 2 | 7.0 MiB / 6.7 MiB | 410.2 MB/s | 5/3 | 31,094 | 282.1s / 1,186,506 msg/s |
| Dekaf | 2026-07-17T02:23:47.0891908+00:00 | 2 | 7.0 MiB / 5.2 MiB | 410.2 MB/s | 5/3 | 32,247 | 291.1s / 1,082,855 msg/s |
| Dekaf | 2026-07-17T02:23:56.0909813+00:00 | 2 | 7.0 MiB / 3.4 MiB | 410.2 MB/s | 5/4 | 32,882 | 300.1s / 1,090,845 msg/s |
| Dekaf | 2026-07-17T02:24:05.0947429+00:00 | 2 | 7.0 MiB / 4.0 MiB | 410.2 MB/s | 5/4 | 33,586 | 309.1s / 1,111,156 msg/s |
| Dekaf | 2026-07-17T02:24:14.0977832+00:00 | 2 | 7.0 MiB / 7.0 MiB | 410.2 MB/s | 5/4 | 34,661 | 318.1s / 1,133,438 msg/s |
| Dekaf | 2026-07-17T02:24:23.0978585+00:00 | 3 | 6.0 MiB / 6.0 MiB | 432.8 MB/s | 6/4 | 231,559 | 327.1s / 1,056,851 msg/s |
| Dekaf | 2026-07-17T02:24:32.0997369+00:00 | 3 | 6.0 MiB / 6.0 MiB | 432.8 MB/s | 6/5 | 238,608 | 336.1s / 1,088,128 msg/s |
| Dekaf | 2026-07-17T02:24:41.1023848+00:00 | 3 | 6.0 MiB / 5.3 MiB | 432.8 MB/s | 6/5 | 245,825 | 345.1s / 1,121,747 msg/s |
| Dekaf | 2026-07-17T02:24:50.1033055+00:00 | 3 | 6.0 MiB / 5.3 MiB | 432.8 MB/s | 6/5 | 252,901 | 354.1s / 1,077,443 msg/s |
| Dekaf | 2026-07-17T02:25:00.1049875+00:00 | 1 | 8.0 MiB / 4.1 MiB | 410.1 MB/s | 7/4 | 44,462 | 364.1s / 1,056,684 msg/s |
| Dekaf | 2026-07-17T02:25:09.1073503+00:00 | 1 | 8.0 MiB / 2.9 MiB | 410.1 MB/s | 7/4 | 45,599 | 373.1s / 1,137,636 msg/s |
| Dekaf | 2026-07-17T02:25:18.1076785+00:00 | 1 | 8.0 MiB / 0.5 MiB | 410.1 MB/s | 7/4 | 46,822 | 382.2s / 1,020,292 msg/s |
| Dekaf | 2026-07-17T02:25:27.1143988+00:00 | 1 | 8.0 MiB / 5.6 MiB | 410.1 MB/s | 7/4 | 48,558 | 391.2s / 1,134,519 msg/s |
| Dekaf | 2026-07-17T02:25:36.1194965+00:00 | 2 | 8.0 MiB / 8.0 MiB | 410.2 MB/s | 6/5 | 45,962 | 400.2s / 1,155,272 msg/s |
| Dekaf | 2026-07-17T02:25:45.1242268+00:00 | 2 | 8.0 MiB / 6.7 MiB | 410.2 MB/s | 6/5 | 46,586 | 409.2s / 1,114,145 msg/s |
| Dekaf | 2026-07-17T02:25:54.1338531+00:00 | 2 | 8.0 MiB / 3.9 MiB | 410.2 MB/s | 6/5 | 49,212 | 418.2s / 1,147,225 msg/s |
| Dekaf | 2026-07-17T02:26:03.1377969+00:00 | 2 | 8.0 MiB / 3.9 MiB | 410.2 MB/s | 6/5 | 50,177 | 427.2s / 1,129,551 msg/s |
| Dekaf | 2026-07-17T02:26:12.1399398+00:00 | 3 | 6.0 MiB / 3.7 MiB | 432.8 MB/s | 6/7 | 316,646 | 436.2s / 1,109,444 msg/s |
| Dekaf | 2026-07-17T02:26:21.1447533+00:00 | 3 | 6.0 MiB / 2.8 MiB | 432.8 MB/s | 6/7 | 323,433 | 445.2s / 1,149,311 msg/s |
| Dekaf | 2026-07-17T02:26:30.1509719+00:00 | 3 | 6.0 MiB / 5.6 MiB | 432.8 MB/s | 6/7 | 330,900 | 454.2s / 1,097,978 msg/s |
| Dekaf | 2026-07-17T02:26:39.1537675+00:00 | 3 | 6.0 MiB / 5.2 MiB | 432.8 MB/s | 6/7 | 337,260 | 463.2s / 1,054,280 msg/s |
| Dekaf | 2026-07-17T02:26:49.1552252+00:00 | 1 | 8.0 MiB / 7.9 MiB | 410.1 MB/s | 7/6 | 58,046 | 473.2s / 1,118,586 msg/s |
| Dekaf | 2026-07-17T02:26:58.1632016+00:00 | 1 | 9.0 MiB / 8.3 MiB | 410.1 MB/s | 7/6 | 59,101 | 482.2s / 1,106,682 msg/s |
| Dekaf | 2026-07-17T02:27:07.1652008+00:00 | 1 | 8.0 MiB / 2.7 MiB | 410.1 MB/s | 7/6 | 59,782 | 491.2s / 1,150,621 msg/s |
| Dekaf | 2026-07-17T02:27:16.1688745+00:00 | 1 | 8.0 MiB / 6.3 MiB | 410.1 MB/s | 7/7 | 60,073 | 500.2s / 1,063,460 msg/s |
| Dekaf | 2026-07-17T02:27:25.1758679+00:00 | 2 | 9.0 MiB / 2.5 MiB | 410.2 MB/s | 6/7 | 58,117 | 509.2s / 1,151,455 msg/s |
| Dekaf | 2026-07-17T02:27:34.1778525+00:00 | 2 | 8.0 MiB / 3.5 MiB | 410.2 MB/s | 6/7 | 58,737 | 518.2s / 1,139,730 msg/s |
| Dekaf | 2026-07-17T02:27:43.1794642+00:00 | 2 | 9.0 MiB / 8.1 MiB | 410.2 MB/s | 7/7 | 59,352 | 527.2s / 1,112,446 msg/s |
| Dekaf | 2026-07-17T02:27:52.1790436+00:00 | 2 | 9.0 MiB / 5.1 MiB | 410.2 MB/s | 7/7 | 59,580 | 536.2s / 1,149,570 msg/s |
| Dekaf | 2026-07-17T02:28:01.1882187+00:00 | 3 | 6.0 MiB / 4.3 MiB | 432.8 MB/s | 8/8 | 408,075 | 545.2s / 1,011,200 msg/s |
| Dekaf | 2026-07-17T02:28:10.191103+00:00 | 3 | 6.0 MiB / 4.6 MiB | 432.8 MB/s | 8/8 | 416,024 | 554.2s / 1,117,432 msg/s |
| Dekaf | 2026-07-17T02:28:19.191962+00:00 | 3 | 7.0 MiB / 6.2 MiB | 432.8 MB/s | 8/8 | 423,803 | 563.2s / 1,164,203 msg/s |
| Dekaf | 2026-07-17T02:28:28.1940109+00:00 | 3 | 6.0 MiB / 4.9 MiB | 432.8 MB/s | 8/8 | 430,755 | 572.2s / 1,041,527 msg/s |
| Dekaf | 2026-07-17T02:28:38.1948324+00:00 | 1 | 8.0 MiB / 4.9 MiB | 410.1 MB/s | 7/8 | 68,406 | 582.2s / 1,109,689 msg/s |
| Dekaf | 2026-07-17T02:28:47.1965421+00:00 | 1 | 8.0 MiB / 3.2 MiB | 410.1 MB/s | 7/9 | 69,509 | 591.2s / 1,066,965 msg/s |
| Dekaf | 2026-07-17T02:28:56.2010031+00:00 | 1 | 8.0 MiB / 4.7 MiB | 410.1 MB/s | 7/9 | 69,892 | 600.2s / 1,143,059 msg/s |
| Dekaf | 2026-07-17T02:29:05.2030426+00:00 | 1 | 8.0 MiB / 3.6 MiB | 410.1 MB/s | 7/9 | 71,188 | 609.2s / 1,073,379 msg/s |
| Dekaf | 2026-07-17T02:29:14.2048651+00:00 | 1 | 9.0 MiB / 2.8 MiB | 410.1 MB/s | 7/9 | 71,484 | 618.2s / 867,799 msg/s |
| Dekaf | 2026-07-17T02:29:23.209395+00:00 | 2 | 8.0 MiB / 3.9 MiB | 410.2 MB/s | 8/8 | 64,605 | 627.2s / 1,092,678 msg/s |
| Dekaf | 2026-07-17T02:29:32.2108861+00:00 | 2 | 8.0 MiB / 7.4 MiB | 410.2 MB/s | 8/8 | 65,112 | 636.2s / 1,120,124 msg/s |
| Dekaf | 2026-07-17T02:29:41.2169406+00:00 | 2 | 9.0 MiB / 7.3 MiB | 410.2 MB/s | 8/8 | 65,484 | 645.3s / 1,122,869 msg/s |
| Dekaf | 2026-07-17T02:29:50.2233109+00:00 | 2 | 8.0 MiB / 3.7 MiB | 410.2 MB/s | 8/8 | 65,842 | 654.3s / 1,147,254 msg/s |
| Dekaf | 2026-07-17T02:29:59.2273316+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 9/10 | 499,711 | 663.3s / 1,124,092 msg/s |
| Dekaf | 2026-07-17T02:30:08.2312459+00:00 | 3 | 6.0 MiB / 6.0 MiB | 432.8 MB/s | 9/10 | 507,945 | 672.3s / 1,130,508 msg/s |
| Dekaf | 2026-07-17T02:30:17.2348507+00:00 | 3 | 5.0 MiB / 5.0 MiB | 432.8 MB/s | 9/10 | 516,033 | 681.3s / 1,121,283 msg/s |
| Dekaf | 2026-07-17T02:30:26.2387104+00:00 | 3 | 5.0 MiB / 4.7 MiB | 432.8 MB/s | 9/11 | 523,377 | 690.3s / 1,134,103 msg/s |
| Dekaf | 2026-07-17T02:30:36.242706+00:00 | 1 | 8.0 MiB / 4.4 MiB | 410.1 MB/s | 7/11 | 76,531 | 700.3s / 1,119,517 msg/s |
| Dekaf | 2026-07-17T02:30:45.2435271+00:00 | 1 | 9.0 MiB / 3.9 MiB | 410.1 MB/s | 7/11 | 77,117 | 709.3s / 1,156,734 msg/s |
| Dekaf | 2026-07-17T02:30:54.2472025+00:00 | 1 | 8.0 MiB / 7.1 MiB | 410.1 MB/s | 7/11 | 77,481 | 718.3s / 1,116,844 msg/s |
| Dekaf | 2026-07-17T02:31:03.2510873+00:00 | 1 | 9.0 MiB / 9.0 MiB | 410.1 MB/s | 8/11 | 78,561 | 727.3s / 1,052,050 msg/s |
| Dekaf | 2026-07-17T02:31:12.258117+00:00 | 2 | 11.0 MiB / 5.7 MiB | 410.2 MB/s | 10/8 | 69,194 | 736.3s / 1,102,485 msg/s |
| Dekaf | 2026-07-17T02:31:21.2606717+00:00 | 2 | 10.0 MiB / 3.8 MiB | 410.2 MB/s | 10/8 | 69,294 | 745.3s / 1,117,967 msg/s |
| Dekaf | 2026-07-17T02:31:30.2618559+00:00 | 2 | 10.0 MiB / 2.7 MiB | 410.2 MB/s | 10/9 | 69,297 | 754.3s / 946,716 msg/s |
| Dekaf | 2026-07-17T02:31:39.2629641+00:00 | 2 | 10.0 MiB / 5.1 MiB | 410.2 MB/s | 10/9 | 69,409 | 763.3s / 908,327 msg/s |
| Dekaf | 2026-07-17T02:31:48.2643747+00:00 | 3 | 5.0 MiB / 4.3 MiB | 432.8 MB/s | 9/12 | 593,209 | 772.3s / 1,079,787 msg/s |
| Dekaf | 2026-07-17T02:31:57.269582+00:00 | 3 | 5.0 MiB / 4.6 MiB | 432.8 MB/s | 9/13 | 601,810 | 781.3s / 1,031,773 msg/s |
| Dekaf | 2026-07-17T02:32:06.2729963+00:00 | 3 | 5.0 MiB / 3.3 MiB | 432.8 MB/s | 9/13 | 609,233 | 790.3s / 884,486 msg/s |
| Dekaf | 2026-07-17T02:32:15.2789653+00:00 | 3 | 5.0 MiB / 3.6 MiB | 432.8 MB/s | 9/13 | 615,476 | 799.3s / 870,598 msg/s |
| Dekaf | 2026-07-17T02:32:25.285573+00:00 | 1 | 11.0 MiB / 3.9 MiB | 410.1 MB/s | 9/11 | 82,146 | 809.3s / 901,152 msg/s |
| Dekaf | 2026-07-17T02:32:34.2878824+00:00 | 1 | 11.0 MiB / 5.1 MiB | 410.1 MB/s | 10/11 | 82,361 | 818.3s / 941,856 msg/s |
| Dekaf | 2026-07-17T02:32:43.2897991+00:00 | 1 | 11.0 MiB / 3.2 MiB | 410.1 MB/s | 10/11 | 82,860 | 827.3s / 1,092,729 msg/s |
| Dekaf | 2026-07-17T02:32:52.2927188+00:00 | 1 | 11.0 MiB / 3.7 MiB | 410.1 MB/s | 10/11 | 83,000 | 836.3s / 1,011,227 msg/s |
| Dekaf | 2026-07-17T02:33:01.2925037+00:00 | 2 | 7.0 MiB / 1.3 MiB | 410.2 MB/s | 11/10 | 74,433 | 845.3s / 894,810 msg/s |
| Dekaf | 2026-07-17T02:33:10.2958645+00:00 | 2 | 7.0 MiB / 3.7 MiB | 410.2 MB/s | 11/10 | 74,711 | 854.3s / 985,384 msg/s |
| Dekaf | 2026-07-17T02:33:19.3029124+00:00 | 2 | 8.0 MiB / 7.6 MiB | 410.2 MB/s | 11/11 | 75,244 | 863.3s / 967,407 msg/s |
| Dekaf | 2026-07-17T02:33:28.3058405+00:00 | 2 | 8.0 MiB / 4.9 MiB | 410.2 MB/s | 11/11 | 75,618 | 872.3s / 919,412 msg/s |
| Dekaf | 2026-07-17T02:33:37.3088445+00:00 | 3 | 6.0 MiB / 6.0 MiB | 432.8 MB/s | 10/14 | 671,224 | 881.3s / 865,472 msg/s |
| Dekaf | 2026-07-17T02:33:46.312124+00:00 | 3 | 6.0 MiB / 4.5 MiB | 432.8 MB/s | 10/14 | 676,696 | 890.3s / 795,829 msg/s |
| Dekaf | 2026-07-17T02:33:55.3238484+00:00 | 3 | 5.0 MiB / 4.8 MiB | 432.8 MB/s | 10/14 | 682,522 | 899.3s / 910,453 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Acks All), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-17T02:19:26.131907+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.1 MiB |
| Dekaf | 2026-07-17T02:19:26.1405484+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-17T02:19:41.1927175+00:00 | 1 | capacity | succeeded | 15,061ms | 14.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-17T02:19:41.2033787+00:00 | 2 | capacity | succeeded | 15,062ms | 14.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-17T02:19:44.1994337+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 6.0 MiB |
| Dekaf | 2026-07-17T02:19:44.2124696+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:19:44.3152754+00:00 | 3 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:19:59.2634169+00:00 | 2 | capacity | succeeded | 15,050ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:19:59.3823537+00:00 | 3 | capacity | succeeded | 15,067ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:20:02.2777974+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-17T02:20:02.3927424+00:00 | 3 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:20:17.3346566+00:00 | 2 | capacity | succeeded | 15,057ms | 10.0 MiB / 0.5 MiB |
| Dekaf | 2026-07-17T02:20:17.4422603+00:00 | 3 | capacity | succeeded | 15,049ms | 10.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-17T02:20:20.3457633+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:20:20.4521892+00:00 | 3 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:20:35.3937665+00:00 | 1 | capacity | succeeded | 15,048ms | 8.0 MiB / 1.6 MiB |
| Dekaf | 2026-07-17T02:20:35.3938352+00:00 | 2 | capacity | succeeded | 15,044ms | 8.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:20:38.4026199+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-17T02:20:38.5013486+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:20:53.4551859+00:00 | 1 | capacity | failed | 15,052ms | 8.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-17T02:21:05.4839079+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-17T02:21:20.5339395+00:00 | 2 | capacity | failed | 15,050ms | 8.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-17T02:21:23.6768385+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:21:38.6544829+00:00 | 1 | capacity | failed | 15,116ms | 8.0 MiB / 1.1 MiB |
| Dekaf | 2026-07-17T02:21:38.7223355+00:00 | 3 | capacity | failed | 15,045ms | 8.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-17T02:22:05.7324797+00:00 | 2 | capacity | failed | 15,046ms | 8.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-17T02:22:08.7506628+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-17T02:22:23.7897152+00:00 | 1 | capacity | succeeded | 15,039ms | 9.0 MiB / 6.6 MiB |
| Dekaf | 2026-07-17T02:22:23.8435203+00:00 | 3 | capacity | succeeded | 15,048ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:22:26.8489588+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:22:41.8854391+00:00 | 3 | capacity | failed | 15,036ms | 7.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-17T02:22:50.8751062+00:00 | 2 | capacity | succeeded | 15,058ms | 7.0 MiB / 4.5 MiB |
| Dekaf | 2026-07-17T02:22:53.8842251+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-17T02:23:08.9110399+00:00 | 1 | capacity | failed | 15,039ms | 9.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-17T02:23:08.946601+00:00 | 2 | capacity | failed | 15,062ms | 7.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-17T02:23:27.0430454+00:00 | 3 | capacity | failed | 15,043ms | 7.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-17T02:23:39.0057724+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:23:54.0689278+00:00 | 1 | capacity | succeeded | 15,063ms | 7.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-17T02:23:54.1259075+00:00 | 2 | capacity | failed | 15,097ms | 7.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-17T02:23:57.1196123+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-17T02:24:12.1621642+00:00 | 1 | capacity | failed | 15,042ms | 7.0 MiB / 2.1 MiB |
| Dekaf | 2026-07-17T02:24:12.242568+00:00 | 3 | capacity | succeeded | 15,039ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:24:24.227221+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-17T02:24:30.2908976+00:00 | 3 | capacity | failed | 15,038ms | 6.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-17T02:24:39.2864099+00:00 | 2 | capacity | failed | 15,059ms | 7.0 MiB / 1.9 MiB |
| Dekaf | 2026-07-17T02:24:57.3258199+00:00 | 1 | capacity | succeeded | 15,058ms | 8.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-17T02:25:00.3608121+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-17T02:25:15.4009366+00:00 | 3 | capacity | failed | 15,039ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:25:24.4210153+00:00 | 2 | capacity | succeeded | 15,052ms | 8.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:25:27.4346498+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-17T02:25:45.4815301+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:25:54.5145047+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 6.8 MiB |
| Dekaf | 2026-07-17T02:26:00.5205445+00:00 | 3 | capacity | failed | 15,039ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:26:12.6017631+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:26:27.6628826+00:00 | 1 | capacity | failed | 15,061ms | 8.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-17T02:26:39.6993084+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:26:45.6842606+00:00 | 3 | capacity | succeeded | 15,039ms | 5.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:26:48.6935143+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:26:57.7753355+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-17T02:27:03.7432608+00:00 | 3 | capacity | failed | 15,049ms | 5.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:27:24.849258+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 2.2 MiB |
| Dekaf | 2026-07-17T02:27:33.8480991+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:27:39.8979295+00:00 | 2 | capacity | succeeded | 15,048ms | 9.0 MiB / 7.7 MiB |
| Dekaf | 2026-07-17T02:27:48.8795468+00:00 | 3 | capacity | succeeded | 15,031ms | 6.0 MiB / 3.9 MiB |
| Dekaf | 2026-07-17T02:27:57.983105+00:00 | 1 | capacity | failed | 15,035ms | 8.0 MiB / 2.3 MiB |
| Dekaf | 2026-07-17T02:28:18.9889131+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-17T02:28:25.0557879+00:00 | 2 | capacity | failed | 15,049ms | 9.0 MiB / 2.5 MiB |
| Dekaf | 2026-07-17T02:28:28.1252058+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:28:43.1874761+00:00 | 1 | capacity | failed | 15,062ms | 8.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-17T02:28:55.1882313+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 4.3 MiB |
| Dekaf | 2026-07-17T02:29:10.2391316+00:00 | 2 | capacity | succeeded | 15,050ms | 8.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-17T02:29:13.2771768+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.7 MiB |
| Dekaf | 2026-07-17T02:29:19.2129346+00:00 | 3 | capacity | succeeded | 15,041ms | 5.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-17T02:29:28.3229176+00:00 | 1 | capacity | failed | 15,045ms | 8.0 MiB / 4.8 MiB |
| Dekaf | 2026-07-17T02:29:37.2696385+00:00 | 3 | capacity | failed | 15,048ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:29:55.3818951+00:00 | 2 | capacity | succeeded | 15,038ms | 9.0 MiB / 4.0 MiB |
| Dekaf | 2026-07-17T02:29:58.4086519+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:30:07.3544553+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:30:22.3972815+00:00 | 3 | capacity | failed | 15,042ms | 5.0 MiB / 4.7 MiB |
| Dekaf | 2026-07-17T02:30:25.4781608+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 5.2 MiB |
| Dekaf | 2026-07-17T02:30:43.5528726+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-17T02:30:52.4817123+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:30:58.6300631+00:00 | 1 | capacity | succeeded | 15,077ms | 9.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:31:10.6498777+00:00 | 2 | capacity | started | 0ms | 11.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-17T02:31:25.6974453+00:00 | 2 | capacity | failed | 15,046ms | 10.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:31:28.7581038+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 1.7 MiB |
| Dekaf | 2026-07-17T02:31:43.8319018+00:00 | 1 | capacity | succeeded | 15,073ms | 10.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:31:52.6884206+00:00 | 3 | capacity | failed | 15,041ms | 5.0 MiB / 3.1 MiB |
| Dekaf | 2026-07-17T02:32:10.8905472+00:00 | 2 | capacity | succeeded | 15,074ms | 8.0 MiB / 2.6 MiB |
| Dekaf | 2026-07-17T02:32:13.8943762+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-17T02:32:13.9450505+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:32:28.9569924+00:00 | 2 | capacity | failed | 15,063ms | 8.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-17T02:32:29.0035129+00:00 | 1 | capacity | succeeded | 15,058ms | 11.0 MiB / 4.2 MiB |
| Dekaf | 2026-07-17T02:32:59.0637514+00:00 | 2 | capacity | started | 0ms | 7.0 MiB / 7.1 MiB |
| Dekaf | 2026-07-17T02:32:59.1488952+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-17T02:33:07.9420878+00:00 | 3 | capacity | started | 0ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:33:14.2101136+00:00 | 1 | capacity | failed | 15,061ms | 11.0 MiB / 3.2 MiB |
| Dekaf | 2026-07-17T02:33:22.988201+00:00 | 3 | capacity | succeeded | 15,046ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:33:44.3272027+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:33:53.1489112+00:00 | 3 | capacity | started | 0ms | 5.0 MiB / 5.2 MiB |
*39 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Acks All), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 20 |
| Dekaf | 1 | 0.002–0.004ms | 25 |
| Dekaf | 1 | 0.004–0.008ms | 112 |
| Dekaf | 1 | 0.008–0.016ms | 292 |
| Dekaf | 1 | 0.016–0.032ms | 488 |
| Dekaf | 1 | 0.032–0.064ms | 798 |
| Dekaf | 1 | 0.064–0.128ms | 1,062 |
| Dekaf | 1 | 0.128–0.256ms | 1,882 |
| Dekaf | 1 | 0.256–0.512ms | 3,642 |
| Dekaf | 1 | 0.512–1.024ms | 5,037 |
| Dekaf | 1 | 1.024–2.048ms | 4,178 |
| Dekaf | 1 | 2.048–4.096ms | 2,094 |
| Dekaf | 1 | 4.096–8.192ms | 677 |
| Dekaf | 1 | 8.192–16.384ms | 177 |
| Dekaf | 1 | 16.384–32.768ms | 62 |
| Dekaf | 1 | 32.768–65.536ms | 7 |
| Dekaf | 2 | 0.001–0.002ms | 16 |
| Dekaf | 2 | 0.002–0.004ms | 26 |
| Dekaf | 2 | 0.004–0.008ms | 98 |
| Dekaf | 2 | 0.008–0.016ms | 254 |
| Dekaf | 2 | 0.016–0.032ms | 462 |
| Dekaf | 2 | 0.032–0.064ms | 743 |
| Dekaf | 2 | 0.064–0.128ms | 987 |
| Dekaf | 2 | 0.128–0.256ms | 1,634 |
| Dekaf | 2 | 0.256–0.512ms | 3,300 |
| Dekaf | 2 | 0.512–1.024ms | 4,538 |
| Dekaf | 2 | 1.024–2.048ms | 3,874 |
| Dekaf | 2 | 2.048–4.096ms | 1,871 |
| Dekaf | 2 | 4.096–8.192ms | 652 |
| Dekaf | 2 | 8.192–16.384ms | 214 |
| Dekaf | 2 | 16.384–32.768ms | 66 |
| Dekaf | 2 | 32.768–65.536ms | 8 |
| Dekaf | 3 | 0.001–0.002ms | 105 |
| Dekaf | 3 | 0.002–0.004ms | 132 |
| Dekaf | 3 | 0.004–0.008ms | 515 |
| Dekaf | 3 | 0.008–0.016ms | 1,591 |
| Dekaf | 3 | 0.016–0.032ms | 3,418 |
| Dekaf | 3 | 0.032–0.064ms | 5,540 |
| Dekaf | 3 | 0.064–0.128ms | 7,726 |
| Dekaf | 3 | 0.128–0.256ms | 13,619 |
| Dekaf | 3 | 0.256–0.512ms | 27,836 |
| Dekaf | 3 | 0.512–1.024ms | 41,670 |
| Dekaf | 3 | 1.024–2.048ms | 34,691 |
| Dekaf | 3 | 2.048–4.096ms | 15,473 |
| Dekaf | 3 | 4.096–8.192ms | 5,381 |
| Dekaf | 3 | 8.192–16.384ms | 1,557 |
| Dekaf | 3 | 16.384–32.768ms | 641 |
| Dekaf | 3 | 32.768–65.536ms | 73 |

## Delivery Latency Outliers - Producer (Acks All), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 1,000 | 2026-07-17T02:03:55.8394193+00:00 | 100.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 5,000 | 2026-07-17T02:03:55.8484274+00:00 | 103.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 6,000 | 2026-07-17T02:03:55.8506574+00:00 | 114.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 7,000 | 2026-07-17T02:03:55.8525424+00:00 | 127.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 8,000 | 2026-07-17T02:03:55.8557345+00:00 | 135.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 9,000 | 2026-07-17T02:03:55.8573955+00:00 | 158.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 11,000 | 2026-07-17T02:03:55.8709056+00:00 | 146.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 12,000 | 2026-07-17T02:03:55.8728458+00:00 | 156.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 13,000 | 2026-07-17T02:03:55.8755455+00:00 | 100.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 14,000 | 2026-07-17T02:03:55.8781742+00:00 | 147.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 15,000 | 2026-07-17T02:03:55.8796561+00:00 | 198.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 16,000 | 2026-07-17T02:03:55.8820536+00:00 | 196.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 17,000 | 2026-07-17T02:03:55.8834915+00:00 | 195.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 18,000 | 2026-07-17T02:03:55.8849819+00:00 | 193.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 19,000 | 2026-07-17T02:03:55.8865054+00:00 | 193.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 20,000 | 2026-07-17T02:03:55.8881497+00:00 | 128.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 21,000 | 2026-07-17T02:03:55.8895802+00:00 | 204.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 22,000 | 2026-07-17T02:03:55.8911715+00:00 | 195.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 23,000 | 2026-07-17T02:03:55.8940071+00:00 | 139.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 24,000 | 2026-07-17T02:03:55.8954657+00:00 | 240.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 25,000 | 2026-07-17T02:03:55.9020601+00:00 | 213.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 26,000 | 2026-07-17T02:03:55.9033476+00:00 | 211.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 27,000 | 2026-07-17T02:03:55.9050953+00:00 | 220.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 28,000 | 2026-07-17T02:03:55.9063277+00:00 | 219.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 29,000 | 2026-07-17T02:03:55.908395+00:00 | 228.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 30,000 | 2026-07-17T02:03:55.9097085+00:00 | 169.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 31,000 | 2026-07-17T02:03:55.9110885+00:00 | 238.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 32,000 | 2026-07-17T02:03:55.9125969+00:00 | 230.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 33,000 | 2026-07-17T02:03:55.9151599+00:00 | 202.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 34,000 | 2026-07-17T02:03:55.9166492+00:00 | 229.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 35,000 | 2026-07-17T02:03:55.9181069+00:00 | 260.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 36,000 | 2026-07-17T02:03:55.9195574+00:00 | 258.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 37,000 | 2026-07-17T02:03:55.9210138+00:00 | 307.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 38,000 | 2026-07-17T02:03:55.9224491+00:00 | 305.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 39,000 | 2026-07-17T02:03:55.9238135+00:00 | 276.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 40,000 | 2026-07-17T02:03:55.9252536+00:00 | 196.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 41,000 | 2026-07-17T02:03:55.9288686+00:00 | 340.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 42,000 | 2026-07-17T02:03:55.9303468+00:00 | 300.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 43,000 | 2026-07-17T02:03:55.9319797+00:00 | 201.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 44,000 | 2026-07-17T02:03:55.9334506+00:00 | 238.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 45,000 | 2026-07-17T02:03:55.9349308+00:00 | 283.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 46,000 | 2026-07-17T02:03:55.9363982+00:00 | 282.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 47,000 | 2026-07-17T02:03:55.9412328+00:00 | 334.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 48,000 | 2026-07-17T02:03:55.9425303+00:00 | 333.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 49,000 | 2026-07-17T02:03:55.944029+00:00 | 314.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 50,000 | 2026-07-17T02:03:55.945457+00:00 | 219.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 51,000 | 2026-07-17T02:03:55.9468988+00:00 | 328.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 52,000 | 2026-07-17T02:03:55.948601+00:00 | 410.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 53,000 | 2026-07-17T02:03:55.9499429+00:00 | 257.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 54,000 | 2026-07-17T02:03:55.9511888+00:00 | 240.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 55,000 | 2026-07-17T02:03:55.9524738+00:00 | 325.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 56,000 | 2026-07-17T02:03:55.9538951+00:00 | 324.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 57,000 | 2026-07-17T02:03:55.9554736+00:00 | 384.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 58,000 | 2026-07-17T02:03:55.9568964+00:00 | 383.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 59,000 | 2026-07-17T02:03:55.9582797+00:00 | 377.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 60,000 | 2026-07-17T02:03:55.9595199+00:00 | 253.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 61,000 | 2026-07-17T02:03:55.960998+00:00 | 379.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 62,000 | 2026-07-17T02:03:55.962558+00:00 | 410.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 63,000 | 2026-07-17T02:03:55.9639896+00:00 | 249.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 64,000 | 2026-07-17T02:03:55.965378+00:00 | 269.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 65,000 | 2026-07-17T02:03:55.966589+00:00 | 382.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 66,000 | 2026-07-17T02:03:55.9682094+00:00 | 380.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 67,000 | 2026-07-17T02:03:55.9695809+00:00 | 380.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 68,000 | 2026-07-17T02:03:55.9709668+00:00 | 379.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 69,000 | 2026-07-17T02:03:55.9723278+00:00 | 376.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 70,000 | 2026-07-17T02:03:55.9736968+00:00 | 266.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 71,000 | 2026-07-17T02:03:55.975234+00:00 | 420.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 72,000 | 2026-07-17T02:03:55.9765437+00:00 | 446.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 73,000 | 2026-07-17T02:03:55.977948+00:00 | 270.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 74,000 | 2026-07-17T02:03:55.9791962+00:00 | 264.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 75,000 | 2026-07-17T02:03:55.9805763+00:00 | 411.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 76,000 | 2026-07-17T02:03:55.982039+00:00 | 410.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 77,000 | 2026-07-17T02:03:55.9832129+00:00 | 426.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 78,000 | 2026-07-17T02:03:55.9845735+00:00 | 425.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 79,000 | 2026-07-17T02:03:55.9860958+00:00 | 419.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 80,000 | 2026-07-17T02:03:55.9875001+00:00 | 272.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 81,000 | 2026-07-17T02:03:55.9889046+00:00 | 430.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 82,000 | 2026-07-17T02:03:55.9902344+00:00 | 488.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 83,000 | 2026-07-17T02:03:55.9914456+00:00 | 351.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 84,000 | 2026-07-17T02:03:55.9930143+00:00 | 267.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 85,000 | 2026-07-17T02:03:55.9943834+00:00 | 438.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 86,000 | 2026-07-17T02:03:55.9956347+00:00 | 437.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 87,000 | 2026-07-17T02:03:55.9970237+00:00 | 428.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 88,000 | 2026-07-17T02:03:55.9985855+00:00 | 427.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 89,000 | 2026-07-17T02:03:56.0009576+00:00 | 437.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 90,000 | 2026-07-17T02:03:56.0022738+00:00 | 347.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 91,000 | 2026-07-17T02:03:56.0036465+00:00 | 422.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 92,000 | 2026-07-17T02:03:56.0051928+00:00 | 483.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 93,000 | 2026-07-17T02:03:56.0065543+00:00 | 343.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 94,000 | 2026-07-17T02:03:56.0078577+00:00 | 355.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 95,000 | 2026-07-17T02:03:56.0092624+00:00 | 439.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 96,000 | 2026-07-17T02:03:56.0106333+00:00 | 438.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 97,000 | 2026-07-17T02:03:56.0174728+00:00 | 427.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 98,000 | 2026-07-17T02:03:56.0183963+00:00 | 440.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 99,000 | 2026-07-17T02:03:56.0197745+00:00 | 429.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 100,000 | 2026-07-17T02:03:56.022287+00:00 | 397.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 101,000 | 2026-07-17T02:03:56.0263736+00:00 | 432.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 102,000 | 2026-07-17T02:03:56.0272875+00:00 | 584.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 103,000 | 2026-07-17T02:03:56.0281832+00:00 | 392.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 104,000 | 2026-07-17T02:03:56.0291569+00:00 | 362.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 105,000 | 2026-07-17T02:03:56.0303197+00:00 | 430.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 106,000 | 2026-07-17T02:03:56.0317557+00:00 | 429.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 107,000 | 2026-07-17T02:03:56.0332963+00:00 | 448.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 108,000 | 2026-07-17T02:03:56.0347878+00:00 | 447.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 109,000 | 2026-07-17T02:03:56.0364032+00:00 | 433.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 110,000 | 2026-07-17T02:03:56.0378405+00:00 | 391.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 111,000 | 2026-07-17T02:03:56.0393576+00:00 | 455.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 112,000 | 2026-07-17T02:03:56.0410384+00:00 | 616.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 113,000 | 2026-07-17T02:03:56.0462398+00:00 | 392.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 114,000 | 2026-07-17T02:03:56.0476858+00:00 | 383.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 115,000 | 2026-07-17T02:03:56.0491364+00:00 | 452.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 116,000 | 2026-07-17T02:03:56.0507577+00:00 | 450.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 117,000 | 2026-07-17T02:03:56.0793967+00:00 | 455.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 118,000 | 2026-07-17T02:03:56.0811437+00:00 | 453.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 119,000 | 2026-07-17T02:03:56.082608+00:00 | 433.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 120,000 | 2026-07-17T02:03:56.0872722+00:00 | 362.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 121,000 | 2026-07-17T02:03:56.0893039+00:00 | 457.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 122,000 | 2026-07-17T02:03:56.0907236+00:00 | 584.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 123,000 | 2026-07-17T02:03:56.0983196+00:00 | 351.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 124,000 | 2026-07-17T02:03:56.0999254+00:00 | 462.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 125,000 | 2026-07-17T02:03:56.1016225+00:00 | 453.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 126,000 | 2026-07-17T02:03:56.103187+00:00 | 452.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 127,000 | 2026-07-17T02:03:56.1047984+00:00 | 446.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 128,000 | 2026-07-17T02:03:56.1062781+00:00 | 444.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 129,000 | 2026-07-17T02:03:56.107998+00:00 | 447.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 130,000 | 2026-07-17T02:03:56.1095049+00:00 | 369.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 131,000 | 2026-07-17T02:03:56.1110022+00:00 | 440.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 132,000 | 2026-07-17T02:03:56.1124686+00:00 | 683.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 133,000 | 2026-07-17T02:03:56.1140534+00:00 | 365.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 134,000 | 2026-07-17T02:03:56.1155565+00:00 | 473.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 135,000 | 2026-07-17T02:03:56.117017+00:00 | 454.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 136,000 | 2026-07-17T02:03:56.1185684+00:00 | 458.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 137,000 | 2026-07-17T02:03:56.1201351+00:00 | 463.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 138,000 | 2026-07-17T02:03:56.1215867+00:00 | 513.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 139,000 | 2026-07-17T02:03:56.1230332+00:00 | 453.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 140,000 | 2026-07-17T02:03:56.1245897+00:00 | 390.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 141,000 | 2026-07-17T02:03:56.1261442+00:00 | 508.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 142,000 | 2026-07-17T02:03:56.1284597+00:00 | 696.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 143,000 | 2026-07-17T02:03:56.1300048+00:00 | 441.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 144,000 | 2026-07-17T02:03:56.1314536+00:00 | 476.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 145,000 | 2026-07-17T02:03:56.1329298+00:00 | 494.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 146,000 | 2026-07-17T02:03:56.1345387+00:00 | 492.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 147,000 | 2026-07-17T02:03:56.1360673+00:00 | 503.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 148,000 | 2026-07-17T02:03:56.1375477+00:00 | 502.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 149,000 | 2026-07-17T02:03:56.1391089+00:00 | 501.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 150,000 | 2026-07-17T02:03:56.1405386+00:00 | 446.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 151,000 | 2026-07-17T02:03:56.1419726+00:00 | 508.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 152,000 | 2026-07-17T02:03:56.1433916+00:00 | 691.6ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 153,000 | 2026-07-17T02:03:56.144932+00:00 | 443.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 154,000 | 2026-07-17T02:03:56.1464069+00:00 | 471.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 155,000 | 2026-07-17T02:03:56.147874+00:00 | 514.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 156,000 | 2026-07-17T02:03:56.1492902+00:00 | 513.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 157,000 | 2026-07-17T02:03:56.1508999+00:00 | 507.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 158,000 | 2026-07-17T02:03:56.1523857+00:00 | 506.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 159,000 | 2026-07-17T02:03:56.1538418+00:00 | 508.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 160,000 | 2026-07-17T02:03:56.1553078+00:00 | 515.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 161,000 | 2026-07-17T02:03:56.1570123+00:00 | 508.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 162,000 | 2026-07-17T02:03:56.1584319+00:00 | 773.0ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 163,000 | 2026-07-17T02:03:56.1598608+00:00 | 511.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 164,000 | 2026-07-17T02:03:56.1612725+00:00 | 465.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 165,000 | 2026-07-17T02:03:56.1628121+00:00 | 505.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 166,000 | 2026-07-17T02:03:56.1642571+00:00 | 524.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 167,000 | 2026-07-17T02:03:56.165836+00:00 | 508.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 168,000 | 2026-07-17T02:03:56.1672458+00:00 | 506.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 169,000 | 2026-07-17T02:03:56.1687677+00:00 | 520.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 170,000 | 2026-07-17T02:03:56.1702423+00:00 | 524.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 171,000 | 2026-07-17T02:03:56.1716975+00:00 | 502.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 172,000 | 2026-07-17T02:03:56.173276+00:00 | 807.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 173,000 | 2026-07-17T02:03:56.1752156+00:00 | 521.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 174,000 | 2026-07-17T02:03:56.1767202+00:00 | 512.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 175,000 | 2026-07-17T02:03:56.1781843+00:00 | 541.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 176,000 | 2026-07-17T02:03:56.1796346+00:00 | 540.0ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 177,000 | 2026-07-17T02:03:56.1811767+00:00 | 512.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 178,000 | 2026-07-17T02:03:56.182595+00:00 | 521.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 179,000 | 2026-07-17T02:03:56.1840109+00:00 | 546.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 180,000 | 2026-07-17T02:03:56.1854162+00:00 | 520.4ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 181,000 | 2026-07-17T02:03:56.1874827+00:00 | 517.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 182,000 | 2026-07-17T02:03:56.1890874+00:00 | 797.1ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 183,000 | 2026-07-17T02:03:56.1905082+00:00 | 553.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 184,000 | 2026-07-17T02:03:56.1920006+00:00 | 594.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 185,000 | 2026-07-17T02:03:56.1934089+00:00 | 547.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 186,000 | 2026-07-17T02:03:56.1950377+00:00 | 545.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 187,000 | 2026-07-17T02:03:56.1964461+00:00 | 516.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 188,000 | 2026-07-17T02:03:56.1978582+00:00 | 514.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 189,000 | 2026-07-17T02:03:56.1993583+00:00 | 541.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 190,000 | 2026-07-17T02:03:56.2014282+00:00 | 607.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 191,000 | 2026-07-17T02:03:56.202915+00:00 | 518.1ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 192,000 | 2026-07-17T02:03:56.2043608+00:00 | 809.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 193,000 | 2026-07-17T02:03:56.20579+00:00 | 603.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 194,000 | 2026-07-17T02:03:56.208025+00:00 | 581.8ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 195,000 | 2026-07-17T02:03:56.2095066+00:00 | 558.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 196,000 | 2026-07-17T02:03:56.213684+00:00 | 569.3ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 197,000 | 2026-07-17T02:03:56.215102+00:00 | 513.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 198,000 | 2026-07-17T02:03:56.2169701+00:00 | 511.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 199,000 | 2026-07-17T02:03:56.2189292+00:00 | 564.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 200,000 | 2026-07-17T02:03:56.2203827+00:00 | 597.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 201,000 | 2026-07-17T02:03:56.2232563+00:00 | 596.5ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 203,000 | 2026-07-17T02:03:56.2262609+00:00 | 591.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 204,000 | 2026-07-17T02:03:56.2289461+00:00 | 713.4ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 205,000 | 2026-07-17T02:03:56.2328714+00:00 | 592.7ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 206,000 | 2026-07-17T02:03:56.2344498+00:00 | 591.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 207,000 | 2026-07-17T02:03:56.2359568+00:00 | 654.3ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 208,000 | 2026-07-17T02:03:56.2373621+00:00 | 652.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 209,000 | 2026-07-17T02:03:56.2387699+00:00 | 588.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 210,000 | 2026-07-17T02:03:56.2422798+00:00 | 580.2ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 211,000 | 2026-07-17T02:03:56.245186+00:00 | 645.2ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 213,000 | 2026-07-17T02:03:56.2481828+00:00 | 586.9ms | GC pause | - | - | 1.0s / 611,316 msg/s | Gen2 +0 / pause +62.5ms |
| Confluent | 214,000 | 2026-07-17T02:03:56.2497884+00:00 | 708.5ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 215,000 | 2026-07-17T02:03:56.2512989+00:00 | 646.3ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 216,000 | 2026-07-17T02:03:56.2527481+00:00 | 644.8ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 217,000 | 2026-07-17T02:03:56.2541695+00:00 | 642.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 218,000 | 2026-07-17T02:03:56.2557247+00:00 | 641.2ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 219,000 | 2026-07-17T02:03:56.2575505+00:00 | 647.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 220,000 | 2026-07-17T02:03:56.2591275+00:00 | 631.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 221,000 | 2026-07-17T02:03:56.2605782+00:00 | 644.5ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 223,000 | 2026-07-17T02:03:56.263604+00:00 | 627.6ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 224,000 | 2026-07-17T02:03:56.2651832+00:00 | 701.6ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 225,000 | 2026-07-17T02:03:56.2666761+00:00 | 655.0ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 226,000 | 2026-07-17T02:03:56.2682807+00:00 | 653.4ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 227,000 | 2026-07-17T02:03:56.269672+00:00 | 643.3ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 228,000 | 2026-07-17T02:03:56.2712465+00:00 | 641.8ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 229,000 | 2026-07-17T02:03:56.2726702+00:00 | 649.1ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 230,000 | 2026-07-17T02:03:56.2742054+00:00 | 631.3ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 231,000 | 2026-07-17T02:03:56.2756268+00:00 | 654.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 233,000 | 2026-07-17T02:03:56.2785169+00:00 | 627.2ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 234,000 | 2026-07-17T02:03:56.2800665+00:00 | 724.4ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 235,000 | 2026-07-17T02:03:56.2814917+00:00 | 673.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 236,000 | 2026-07-17T02:03:56.2829648+00:00 | 672.3ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 237,000 | 2026-07-17T02:03:56.2843884+00:00 | 655.3ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 238,000 | 2026-07-17T02:03:56.3346764+00:00 | 605.1ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 239,000 | 2026-07-17T02:03:56.3363712+00:00 | 630.0ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 240,000 | 2026-07-17T02:03:56.3379359+00:00 | 610.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 241,000 | 2026-07-17T02:03:56.3393879+00:00 | 600.6ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 243,000 | 2026-07-17T02:03:56.3442016+00:00 | 621.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 244,000 | 2026-07-17T02:03:56.3456558+00:00 | 668.1ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 245,000 | 2026-07-17T02:03:56.3470821+00:00 | 619.8ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 246,000 | 2026-07-17T02:03:56.3509121+00:00 | 616.0ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 247,000 | 2026-07-17T02:03:56.3527348+00:00 | 628.6ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 248,000 | 2026-07-17T02:03:56.3539068+00:00 | 627.5ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 249,000 | 2026-07-17T02:03:56.3555044+00:00 | 613.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 250,000 | 2026-07-17T02:03:56.3573156+00:00 | 634.1ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 251,000 | 2026-07-17T02:03:56.3588388+00:00 | 622.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 253,000 | 2026-07-17T02:03:56.3623232+00:00 | 645.8ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 254,000 | 2026-07-17T02:03:56.3646669+00:00 | 655.0ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 255,000 | 2026-07-17T02:03:56.3661442+00:00 | 632.2ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 256,000 | 2026-07-17T02:03:56.3675201+00:00 | 630.9ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 257,000 | 2026-07-17T02:03:56.3688176+00:00 | 614.8ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 258,000 | 2026-07-17T02:03:56.3704489+00:00 | 613.2ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 259,000 | 2026-07-17T02:03:56.371915+00:00 | 626.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 261,000 | 2026-07-17T02:03:56.3750934+00:00 | 615.8ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 265,000 | 2026-07-17T02:03:56.3811638+00:00 | 621.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 266,000 | 2026-07-17T02:03:56.3828894+00:00 | 620.4ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 267,000 | 2026-07-17T02:03:56.3842915+00:00 | 610.7ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 268,000 | 2026-07-17T02:03:56.3859614+00:00 | 609.0ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 269,000 | 2026-07-17T02:03:56.387574+00:00 | 632.5ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Confluent | 271,000 | 2026-07-17T02:03:56.391513+00:00 | 624.4ms | GC pause | - | - | 2.0s / 500,329 msg/s | Gen2 +0 / pause +145.6ms |
| Dekaf | 21,801,000 | 2026-07-17T02:19:16.024613+00:00 | 101.9ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,090,542 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 21,802,000 | 2026-07-17T02:19:16.0248811+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 21.0s / 1,090,542 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*2,753 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.47x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.22x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

### Order-Balanced Aggregate

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,576,208 | 1,568,015–1,584,443 | 0.92 | 1.19x |
| Confluent | 2 | 1,325,136 | 1,255,970–1,398,110 | 1.33 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.78 | 733.56 | 1,655,118 | 1,617,000 | +46.4% | +4.19% | 1578.44 | 1,655,118 | 0 | 1.30 |
| Dekaf (confluent-first) | 0.93 | 951.52 | 1,567,486 | 1,584,443 | -1.2% | -0.15% | 1494.87 | 1,567,486 | 0 | 1.46 |
| Dekaf (dekaf-first) | 0.92 | 940.19 | 1,540,968 | 1,568,015 | -2.9% | -0.27% | 1469.58 | 1,540,968 | 0 | 1.42 |
| Confluent (confluent-first) | 1.29 | - | 1,368,750 | 1,398,110 | +1.2% | +0.30% | 1305.34 | 1,368,750 | 0 | 1.77 |
| Confluent (dekaf-first) | 1.36 | - | 1,258,912 | 1,255,970 | -12.6% | -1.05% | 1200.59 | 1,258,912 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 1,357,594 | 1508.42 | 1015.54 KB |
| Dekaf | 1 | 1,376,844 | 1529.81 | 1018.57 KB |
| Dekaf (3conn) | 1 | 1,591,471 | 1768.29 | 930.47 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent)

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:43.6337589+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 466,916 msg/s |
| Dekaf | 2026-07-17T02:19:10.6442394+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1731.1 MB/s | 0/0 | 51,056 | 27.0s / 1,627,191 msg/s |
| Dekaf | 2026-07-17T02:19:38.6533542+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1739.6 MB/s | 1/0 | 109,162 | 55.0s / 1,580,065 msg/s |
| Dekaf | 2026-07-17T02:20:05.6618365+00:00 | 1 | 14.0 MiB / 12.5 MiB | 1750.6 MB/s | 1/0 | 167,916 | 82.0s / 1,617,327 msg/s |
| Dekaf | 2026-07-17T02:20:32.6679548+00:00 | 1 | 12.0 MiB / 11.0 MiB | 1750.6 MB/s | 2/0 | 229,702 | 109.0s / 1,567,623 msg/s |
| Dekaf | 2026-07-17T02:20:59.6712135+00:00 | 1 | 12.0 MiB / 11.8 MiB | 1753.2 MB/s | 2/1 | 289,661 | 136.1s / 1,627,458 msg/s |
| Dekaf | 2026-07-17T02:21:27.6802384+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.2 MB/s | 2/1 | 358,153 | 164.1s / 1,612,356 msg/s |
| Dekaf | 2026-07-17T02:21:54.682396+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.2 MB/s | 2/2 | 421,096 | 191.1s / 1,575,999 msg/s |
| Dekaf | 2026-07-17T02:22:21.6875217+00:00 | 1 | 12.0 MiB / 11.9 MiB | 1753.2 MB/s | 2/2 | 483,351 | 218.1s / 1,608,285 msg/s |
| Dekaf | 2026-07-17T02:22:48.6968366+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.2 MB/s | 2/3 | 545,090 | 245.1s / 1,588,707 msg/s |
| Dekaf | 2026-07-17T02:23:16.7021344+00:00 | 1 | 13.0 MiB / 11.3 MiB | 1753.2 MB/s | 3/3 | 610,853 | 273.1s / 1,567,038 msg/s |
| Dekaf | 2026-07-17T02:23:43.7105092+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.2 MB/s | 3/3 | 672,027 | 300.1s / 1,604,961 msg/s |
| Dekaf | 2026-07-17T02:24:10.7176686+00:00 | 1 | 14.0 MiB / 13.6 MiB | 1753.2 MB/s | 4/3 | 730,728 | 327.1s / 1,597,995 msg/s |
| Dekaf | 2026-07-17T02:24:37.7261802+00:00 | 1 | 14.0 MiB / 13.3 MiB | 1753.2 MB/s | 4/3 | 789,724 | 354.1s / 1,609,239 msg/s |
| Dekaf | 2026-07-17T02:25:05.7345176+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1753.2 MB/s | 4/4 | 851,350 | 382.1s / 1,590,060 msg/s |
| Dekaf | 2026-07-17T02:25:32.7551061+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1753.2 MB/s | 4/5 | 913,645 | 409.2s / 1,556,855 msg/s |
| Dekaf | 2026-07-17T02:25:59.7664193+00:00 | 1 | 15.0 MiB / 14.7 MiB | 1753.2 MB/s | 4/5 | 971,618 | 436.2s / 1,602,072 msg/s |
| Dekaf | 2026-07-17T02:26:26.7728614+00:00 | 1 | 14.0 MiB / 10.6 MiB | 1753.2 MB/s | 4/6 | 1,031,256 | 463.2s / 1,544,236 msg/s |
| Dekaf | 2026-07-17T02:26:54.7827855+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1753.2 MB/s | 4/6 | 1,097,242 | 491.2s / 1,602,415 msg/s |
| Dekaf | 2026-07-17T02:27:21.7876761+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1753.2 MB/s | 5/6 | 1,160,086 | 518.2s / 1,613,605 msg/s |
| Dekaf | 2026-07-17T02:27:48.8027161+00:00 | 1 | 12.0 MiB / 11.4 MiB | 1753.2 MB/s | 5/7 | 1,226,859 | 545.2s / 1,630,480 msg/s |
| Dekaf | 2026-07-17T02:28:16.823627+00:00 | 1 | 13.0 MiB / 11.1 MiB | 1753.2 MB/s | 5/7 | 1,287,961 | 573.2s / 1,600,874 msg/s |
| Dekaf | 2026-07-17T02:28:43.8343875+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1753.2 MB/s | 5/8 | 1,350,466 | 600.2s / 1,588,079 msg/s |
| Dekaf | 2026-07-17T02:29:10.8427959+00:00 | 1 | 10.0 MiB / 9.7 MiB | 1753.2 MB/s | 5/8 | 1,408,015 | 627.2s / 1,442,076 msg/s |
| Dekaf | 2026-07-17T02:29:37.8607748+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1753.2 MB/s | 5/9 | 1,467,732 | 654.2s / 1,555,370 msg/s |
| Dekaf | 2026-07-17T02:30:05.8684668+00:00 | 1 | 13.0 MiB / 12.7 MiB | 1753.2 MB/s | 6/9 | 1,537,927 | 682.2s / 1,596,805 msg/s |
| Dekaf | 2026-07-17T02:30:32.8801505+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1753.2 MB/s | 6/9 | 1,601,105 | 709.2s / 1,576,543 msg/s |
| Dekaf | 2026-07-17T02:30:59.8980735+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.2 MB/s | 6/10 | 1,659,616 | 736.2s / 1,521,873 msg/s |
| Dekaf | 2026-07-17T02:31:26.9073268+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1753.2 MB/s | 6/10 | 1,726,934 | 763.3s / 1,515,327 msg/s |
| Dekaf | 2026-07-17T02:31:54.9142237+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.2 MB/s | 6/11 | 1,792,247 | 791.3s / 1,524,689 msg/s |
| Dekaf | 2026-07-17T02:32:21.9234101+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.2 MB/s | 6/12 | 1,853,246 | 818.3s / 1,015,982 msg/s |
| Dekaf | 2026-07-17T02:32:48.9299511+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1753.2 MB/s | 6/12 | 1,920,278 | 845.3s / 1,596,926 msg/s |
| Dekaf | 2026-07-17T02:33:15.9358057+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1753.2 MB/s | 7/12 | 1,988,816 | 872.3s / 1,563,285 msg/s |
| Dekaf | 2026-07-17T02:33:44.4742485+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 930,382 msg/s |
| Dekaf | 2026-07-17T02:34:11.4852199+00:00 | 1 | 16.0 MiB / 16.0 MiB | 1724.4 MB/s | 0/0 | 59,547 | 27.0s / 1,608,561 msg/s |
| Dekaf | 2026-07-17T02:34:38.496398+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1724.4 MB/s | 1/0 | 121,558 | 54.0s / 1,571,771 msg/s |
| Dekaf | 2026-07-17T02:35:05.5007017+00:00 | 1 | 14.0 MiB / 13.4 MiB | 1724.4 MB/s | 1/0 | 184,302 | 81.0s / 1,578,009 msg/s |
| Dekaf | 2026-07-17T02:35:33.5082462+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1724.4 MB/s | 2/0 | 250,551 | 109.0s / 1,602,025 msg/s |
| Dekaf | 2026-07-17T02:36:00.5138974+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1727.7 MB/s | 2/1 | 315,513 | 136.1s / 1,602,651 msg/s |
| Dekaf | 2026-07-17T02:36:27.5188651+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1727.7 MB/s | 2/1 | 384,977 | 163.1s / 1,574,196 msg/s |
| Dekaf | 2026-07-17T02:36:55.5257267+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1727.7 MB/s | 2/2 | 458,319 | 191.1s / 1,606,113 msg/s |
| Dekaf | 2026-07-17T02:37:22.5335665+00:00 | 1 | 12.0 MiB / 10.3 MiB | 1727.7 MB/s | 2/2 | 522,463 | 218.1s / 1,566,728 msg/s |
| Dekaf | 2026-07-17T02:37:49.5465329+00:00 | 1 | 12.0 MiB / 11.6 MiB | 1727.7 MB/s | 2/3 | 589,019 | 245.1s / 1,650,094 msg/s |
| Dekaf | 2026-07-17T02:38:16.5557171+00:00 | 1 | 12.0 MiB / 10.7 MiB | 1750.3 MB/s | 2/4 | 655,192 | 272.1s / 1,598,586 msg/s |
| Dekaf | 2026-07-17T02:38:44.5647809+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1750.3 MB/s | 2/4 | 725,288 | 300.1s / 1,573,100 msg/s |
| Dekaf | 2026-07-17T02:39:11.5737189+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1750.3 MB/s | 2/5 | 788,751 | 327.1s / 1,575,709 msg/s |
| Dekaf | 2026-07-17T02:39:38.5816031+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1750.3 MB/s | 2/5 | 853,213 | 354.1s / 1,558,627 msg/s |
| Dekaf | 2026-07-17T02:40:05.5869244+00:00 | 1 | 13.0 MiB / 13.0 MiB | 1750.3 MB/s | 3/5 | 913,335 | 381.1s / 1,599,261 msg/s |
| Dekaf | 2026-07-17T02:40:33.5998244+00:00 | 1 | 13.0 MiB / 12.9 MiB | 1750.3 MB/s | 3/6 | 977,064 | 409.1s / 1,554,714 msg/s |
| Dekaf | 2026-07-17T02:41:00.6063884+00:00 | 1 | 11.0 MiB / 6.5 MiB | 1750.3 MB/s | 3/6 | 1,036,014 | 436.1s / 1,560,172 msg/s |
| Dekaf | 2026-07-17T02:41:27.6105728+00:00 | 1 | 11.0 MiB / 2.8 MiB | 1750.3 MB/s | 4/6 | 1,107,682 | 463.2s / 1,555,039 msg/s |
| Dekaf | 2026-07-17T02:41:54.6155494+00:00 | 1 | 11.0 MiB / 10.9 MiB | 1750.3 MB/s | 4/6 | 1,172,736 | 490.2s / 1,534,467 msg/s |
| Dekaf | 2026-07-17T02:42:22.6200976+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1750.3 MB/s | 4/7 | 1,234,223 | 518.2s / 1,417,544 msg/s |
| Dekaf | 2026-07-17T02:42:49.6218496+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1750.3 MB/s | 4/8 | 1,298,822 | 545.2s / 1,515,218 msg/s |
| Dekaf | 2026-07-17T02:43:16.6290824+00:00 | 1 | 9.0 MiB / 2.6 MiB | 1750.3 MB/s | 4/8 | 1,355,742 | 572.2s / 1,277,092 msg/s |
| Dekaf | 2026-07-17T02:43:43.6350705+00:00 | 1 | 11.0 MiB / 10.2 MiB | 1750.3 MB/s | 4/9 | 1,411,855 | 599.2s / 1,482,945 msg/s |
| Dekaf | 2026-07-17T02:44:11.6393405+00:00 | 1 | 12.0 MiB / 12.0 MiB | 1750.3 MB/s | 4/9 | 1,477,471 | 627.2s / 1,525,059 msg/s |
| Dekaf | 2026-07-17T02:44:38.6453393+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1750.3 MB/s | 4/10 | 1,539,200 | 654.2s / 1,459,638 msg/s |
| Dekaf | 2026-07-17T02:45:05.6509647+00:00 | 1 | 11.0 MiB / 11.0 MiB | 1750.3 MB/s | 4/11 | 1,589,432 | 681.2s / 1,520,591 msg/s |
| Dekaf | 2026-07-17T02:45:32.6550142+00:00 | 1 | 12.0 MiB / 11.7 MiB | 1750.3 MB/s | 4/11 | 1,656,460 | 708.2s / 1,498,485 msg/s |
| Dekaf | 2026-07-17T02:46:00.6577737+00:00 | 1 | 11.0 MiB / 10.6 MiB | 1750.3 MB/s | 4/12 | 1,723,503 | 736.2s / 1,576,728 msg/s |
| Dekaf | 2026-07-17T02:46:27.6743108+00:00 | 1 | 9.0 MiB / 6.2 MiB | 1750.3 MB/s | 4/12 | 1,779,603 | 763.2s / 1,292,888 msg/s |
| Dekaf | 2026-07-17T02:46:54.682024+00:00 | 1 | 11.0 MiB / 8.8 MiB | 1750.3 MB/s | 4/13 | 1,850,426 | 790.2s / 1,606,997 msg/s |
| Dekaf | 2026-07-17T02:47:22.6905979+00:00 | 1 | 12.0 MiB / 10.9 MiB | 1750.3 MB/s | 5/13 | 1,923,735 | 818.2s / 1,546,780 msg/s |
| Dekaf | 2026-07-17T02:47:49.6999578+00:00 | 1 | 12.0 MiB / 11.1 MiB | 1750.3 MB/s | 5/13 | 1,992,751 | 845.2s / 1,580,863 msg/s |
| Dekaf | 2026-07-17T02:48:16.7105475+00:00 | 1 | 13.0 MiB / 12.8 MiB | 1750.3 MB/s | 6/13 | 2,059,357 | 872.2s / 1,599,047 msg/s |
| Dekaf | 2026-07-17T02:48:43.7139991+00:00 | 1 | 14.0 MiB / 14.0 MiB | 1750.3 MB/s | 6/13 | 2,124,594 | 899.3s / 1,602,060 msg/s |
| Dekaf (3conn) | 2026-07-17T03:04:12.6540153+00:00 | 1 | 48.0 MiB / 5.1 MiB | 1689.9 MB/s | 0/0 | 18 | 27.0s / 1,068,189 msg/s |
| Dekaf (3conn) | 2026-07-17T03:04:39.664034+00:00 | 1 | 42.0 MiB / 3.6 MiB | 1689.9 MB/s | 1/0 | 30 | 54.0s / 1,259,755 msg/s |
| Dekaf (3conn) | 2026-07-17T03:05:06.677774+00:00 | 1 | 42.0 MiB / 6.5 MiB | 1689.9 MB/s | 1/0 | 237 | 81.0s / 1,404,957 msg/s |
| Dekaf (3conn) | 2026-07-17T03:05:33.6895741+00:00 | 1 | 36.0 MiB / 2.6 MiB | 1756.0 MB/s | 2/0 | 356 | 108.1s / 1,622,643 msg/s |
| Dekaf (3conn) | 2026-07-17T03:06:01.6927398+00:00 | 1 | 30.0 MiB / 2.2 MiB | 2121.2 MB/s | 3/0 | 496 | 136.1s / 1,416,763 msg/s |
| Dekaf (3conn) | 2026-07-17T03:06:28.7095366+00:00 | 1 | 30.0 MiB / 5.1 MiB | 2161.2 MB/s | 3/0 | 927 | 163.1s / 1,385,299 msg/s |
| Dekaf (3conn) | 2026-07-17T03:06:55.7170653+00:00 | 1 | 30.0 MiB / 3.1 MiB | 2161.2 MB/s | 3/1 | 1,155 | 190.1s / 1,074,029 msg/s |
| Dekaf (3conn) | 2026-07-17T03:07:22.7293809+00:00 | 1 | 30.0 MiB / 3.4 MiB | 2161.2 MB/s | 3/1 | 1,349 | 217.2s / 1,301,316 msg/s |
| Dekaf (3conn) | 2026-07-17T03:07:50.7505251+00:00 | 1 | 33.0 MiB / 11.7 MiB | 2161.2 MB/s | 4/1 | 1,400 | 245.2s / 1,337,650 msg/s |
| Dekaf (3conn) | 2026-07-17T03:08:17.7627843+00:00 | 1 | 33.0 MiB / 8.0 MiB | 2161.2 MB/s | 4/2 | 1,682 | 272.2s / 1,394,703 msg/s |
| Dekaf (3conn) | 2026-07-17T03:08:44.7768217+00:00 | 1 | 33.0 MiB / 4.5 MiB | 2161.2 MB/s | 4/2 | 1,927 | 299.2s / 1,654,126 msg/s |
| Dekaf (3conn) | 2026-07-17T03:09:11.7890057+00:00 | 1 | 36.0 MiB / 13.1 MiB | 2161.2 MB/s | 5/2 | 2,061 | 326.2s / 1,598,712 msg/s |
| Dekaf (3conn) | 2026-07-17T03:09:39.8015411+00:00 | 1 | 36.0 MiB / 16.4 MiB | 2161.2 MB/s | 5/2 | 2,329 | 354.2s / 1,627,022 msg/s |
| Dekaf (3conn) | 2026-07-17T03:10:06.8314832+00:00 | 1 | 36.0 MiB / 33.3 MiB | 2161.2 MB/s | 5/3 | 2,623 | 381.2s / 1,560,329 msg/s |
| Dekaf (3conn) | 2026-07-17T03:10:33.8461583+00:00 | 1 | 36.0 MiB / 29.6 MiB | 2161.2 MB/s | 5/4 | 2,808 | 408.3s / 1,426,271 msg/s |
| Dekaf (3conn) | 2026-07-17T03:11:01.8645444+00:00 | 1 | 39.0 MiB / 6.4 MiB | 2161.2 MB/s | 5/4 | 2,892 | 436.3s / 1,545,679 msg/s |
| Dekaf (3conn) | 2026-07-17T03:11:28.8747519+00:00 | 1 | 39.0 MiB / 5.2 MiB | 2161.2 MB/s | 6/4 | 2,943 | 463.3s / 1,222,127 msg/s |
| Dekaf (3conn) | 2026-07-17T03:11:55.8887929+00:00 | 1 | 39.0 MiB / 5.2 MiB | 2327.8 MB/s | 6/4 | 3,191 | 490.3s / 1,647,431 msg/s |
| Dekaf (3conn) | 2026-07-17T03:12:22.8965375+00:00 | 1 | 42.0 MiB / 5.3 MiB | 2327.8 MB/s | 7/4 | 3,343 | 517.3s / 1,463,648 msg/s |
| Dekaf (3conn) | 2026-07-17T03:12:50.9065897+00:00 | 1 | 45.0 MiB / 3.4 MiB | 2542.4 MB/s | 8/4 | 3,526 | 545.3s / 2,054,456 msg/s |
| Dekaf (3conn) | 2026-07-17T03:13:17.9148784+00:00 | 1 | 48.0 MiB / 1.9 MiB | 2659.5 MB/s | 8/4 | 3,841 | 572.3s / 1,879,890 msg/s |
| Dekaf (3conn) | 2026-07-17T03:13:44.9259921+00:00 | 1 | 48.0 MiB / 7.1 MiB | 2659.5 MB/s | 9/4 | 4,156 | 599.3s / 1,854,947 msg/s |
| Dekaf (3conn) | 2026-07-17T03:14:11.9287957+00:00 | 1 | 48.0 MiB / 4.2 MiB | 2659.5 MB/s | 9/4 | 4,262 | 626.3s / 1,618,011 msg/s |
| Dekaf (3conn) | 2026-07-17T03:14:39.9385428+00:00 | 1 | 54.0 MiB / 6.8 MiB | 2659.5 MB/s | 10/4 | 4,427 | 654.3s / 1,966,072 msg/s |
| Dekaf (3conn) | 2026-07-17T03:15:06.9506764+00:00 | 1 | 60.0 MiB / 29.9 MiB | 2671.6 MB/s | 11/4 | 4,628 | 681.4s / 2,113,799 msg/s |
| Dekaf (3conn) | 2026-07-17T03:15:33.9646229+00:00 | 1 | 66.0 MiB / 8.1 MiB | 2671.6 MB/s | 11/4 | 4,798 | 708.4s / 1,971,111 msg/s |
| Dekaf (3conn) | 2026-07-17T03:16:00.9763478+00:00 | 1 | 66.0 MiB / 2.4 MiB | 2671.6 MB/s | 12/4 | 4,948 | 735.4s / 2,131,673 msg/s |
| Dekaf (3conn) | 2026-07-17T03:16:28.9885966+00:00 | 1 | 72.0 MiB / 6.4 MiB | 2774.3 MB/s | 12/4 | 4,991 | 763.4s / 1,732,910 msg/s |
| Dekaf (3conn) | 2026-07-17T03:16:56.0077794+00:00 | 1 | 66.0 MiB / 6.9 MiB | 2774.3 MB/s | 12/5 | 5,074 | 790.4s / 1,999,890 msg/s |
| Dekaf (3conn) | 2026-07-17T03:17:23.0182256+00:00 | 1 | 57.0 MiB / 42.4 MiB | 2774.3 MB/s | 13/5 | 5,229 | 817.4s / 1,632,198 msg/s |
| Dekaf (3conn) | 2026-07-17T03:17:50.0232848+00:00 | 1 | 48.0 MiB / 4.3 MiB | 2774.3 MB/s | 13/5 | 5,807 | 844.4s / 2,152,437 msg/s |
| Dekaf (3conn) | 2026-07-17T03:18:18.0403809+00:00 | 1 | 48.0 MiB / 1.7 MiB | 2774.3 MB/s | 14/5 | 6,631 | 872.4s / 2,313,101 msg/s |
| Dekaf (3conn) | 2026-07-17T03:18:45.0511806+00:00 | 1 | 42.0 MiB / 11.8 MiB | 2774.3 MB/s | 14/5 | 7,611 | 899.5s / 2,039,566 msg/s |
*2,600 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent)

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-17T02:19:13.7274246+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 15.7 MiB |
| Dekaf | 2026-07-17T02:19:28.7377064+00:00 | 1 | capacity | succeeded | 15,010ms | 14.0 MiB / 13.6 MiB |
| Dekaf | 2026-07-17T02:19:58.7639032+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:20:43.7976418+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:20:58.8127623+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 9.6 MiB |
| Dekaf | 2026-07-17T02:21:28.8549466+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:21:43.865625+00:00 | 1 | capacity | failed | 15,010ms | 12.0 MiB / 11.8 MiB |
| Dekaf | 2026-07-17T02:22:13.8882242+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:22:28.9021293+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-17T02:23:13.9368665+00:00 | 1 | capacity | succeeded | 15,011ms | 13.0 MiB / 11.8 MiB |
| Dekaf | 2026-07-17T02:23:43.9608573+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:23:58.9711193+00:00 | 1 | capacity | succeeded | 15,010ms | 14.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:24:28.989755+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:24:44.0007834+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 14.1 MiB |
| Dekaf | 2026-07-17T02:25:14.0210935+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:25:59.0578708+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:26:14.0704843+00:00 | 1 | capacity | failed | 15,011ms | 14.0 MiB / 15.0 MiB |
| Dekaf | 2026-07-17T02:26:44.0915261+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:26:59.108238+00:00 | 1 | capacity | succeeded | 15,016ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:27:29.1341664+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:27:44.1438188+00:00 | 1 | capacity | failed | 15,009ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:28:29.1832445+00:00 | 1 | capacity | failed | 15,014ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:28:59.204274+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.9 MiB |
| Dekaf | 2026-07-17T02:29:14.2175175+00:00 | 1 | capacity | failed | 15,013ms | 12.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:29:44.2346796+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:29:59.2478749+00:00 | 1 | capacity | succeeded | 15,013ms | 13.0 MiB / 11.7 MiB |
| Dekaf | 2026-07-17T02:30:29.2701812+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:31:14.3038772+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:31:29.3401776+00:00 | 1 | capacity | failed | 15,036ms | 13.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:31:59.3724471+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:32:14.3858775+00:00 | 1 | capacity | failed | 15,013ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:32:44.4155994+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:32:59.4248185+00:00 | 1 | capacity | succeeded | 15,009ms | 11.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:34:14.5670711+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 14.9 MiB |
| Dekaf | 2026-07-17T02:34:29.5786819+00:00 | 1 | capacity | succeeded | 15,011ms | 14.0 MiB / 13.7 MiB |
| Dekaf | 2026-07-17T02:34:59.6035454+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:35:14.6114679+00:00 | 1 | capacity | succeeded | 15,007ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:35:44.6374531+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:36:29.6709004+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.5 MiB |
| Dekaf | 2026-07-17T02:36:44.6785392+00:00 | 1 | capacity | failed | 15,007ms | 12.0 MiB / 12.0 MiB |
| Dekaf | 2026-07-17T02:37:14.6971333+00:00 | 1 | capacity | started | 0ms | 10.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:37:29.7083972+00:00 | 1 | capacity | failed | 15,011ms | 12.0 MiB / 2.0 MiB |
| Dekaf | 2026-07-17T02:37:59.7293197+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:38:14.7379355+00:00 | 1 | capacity | failed | 15,008ms | 12.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:38:59.7716664+00:00 | 1 | capacity | failed | 15,012ms | 12.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-17T02:39:29.7928589+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:39:44.8016702+00:00 | 1 | capacity | succeeded | 15,008ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:40:14.8468731+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:40:29.8564088+00:00 | 1 | capacity | failed | 15,009ms | 13.0 MiB / 13.1 MiB |
| Dekaf | 2026-07-17T02:40:59.8813912+00:00 | 1 | capacity | started | 0ms | 11.0 MiB / 12.2 MiB |
| Dekaf | 2026-07-17T02:41:44.913464+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:41:59.9262884+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 4.6 MiB |
| Dekaf | 2026-07-17T02:42:29.9607211+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:42:44.9695312+00:00 | 1 | capacity | failed | 15,008ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:43:15.0032055+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-17T02:43:30.0182737+00:00 | 1 | capacity | failed | 15,015ms | 11.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-17T02:44:15.0534624+00:00 | 1 | capacity | failed | 15,008ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:44:45.073903+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.2 MiB |
| Dekaf | 2026-07-17T02:45:00.0904951+00:00 | 1 | capacity | failed | 15,016ms | 11.0 MiB / 0.5 MiB |
| Dekaf | 2026-07-17T02:45:30.1160211+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.7 MiB |
| Dekaf | 2026-07-17T02:45:45.1280536+00:00 | 1 | capacity | failed | 15,012ms | 11.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:46:15.147975+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:47:00.1793457+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 10.1 MiB |
| Dekaf | 2026-07-17T02:47:15.1872986+00:00 | 1 | capacity | succeeded | 15,007ms | 12.0 MiB / 11.1 MiB |
| Dekaf | 2026-07-17T02:47:45.2075151+00:00 | 1 | capacity | started | 0ms | 13.0 MiB / 10.3 MiB |
| Dekaf | 2026-07-17T02:48:00.2181662+00:00 | 1 | capacity | succeeded | 15,010ms | 13.0 MiB / 12.1 MiB |
| Dekaf | 2026-07-17T02:48:30.2387404+00:00 | 1 | capacity | started | 0ms | 14.0 MiB / 11.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:04:30.8262187+00:00 | 1 | capacity | succeeded | 15,029ms | 42.0 MiB / 19.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:05:00.8751128+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:05:15.9012461+00:00 | 1 | capacity | succeeded | 15,026ms | 36.0 MiB / 10.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:05:45.9575824+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 5.0 MiB |
| Dekaf (3conn) | 2026-07-17T03:06:00.9796999+00:00 | 1 | capacity | succeeded | 15,022ms | 30.0 MiB / 4.3 MiB |
| Dekaf (3conn) | 2026-07-17T03:06:31.0245563+00:00 | 1 | capacity | started | 0ms | 24.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:07:16.0805666+00:00 | 1 | capacity | started | 0ms | 33.0 MiB / 9.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:07:31.1078295+00:00 | 1 | capacity | succeeded | 15,027ms | 33.0 MiB / 5.3 MiB |
| Dekaf (3conn) | 2026-07-17T03:08:01.1495208+00:00 | 1 | capacity | started | 0ms | 27.0 MiB / 29.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:08:16.1682363+00:00 | 1 | capacity | failed | 15,019ms | 33.0 MiB / 2.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:08:46.2344738+00:00 | 1 | capacity | started | 0ms | 36.0 MiB / 5.2 MiB |
| Dekaf (3conn) | 2026-07-17T03:09:01.2709682+00:00 | 1 | capacity | succeeded | 15,036ms | 36.0 MiB / 12.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:09:46.3769577+00:00 | 1 | capacity | failed | 15,026ms | 36.0 MiB / 4.9 MiB |
| Dekaf (3conn) | 2026-07-17T03:10:16.4389813+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-17T03:10:31.4599903+00:00 | 1 | capacity | failed | 15,021ms | 36.0 MiB / 6.0 MiB |
| Dekaf (3conn) | 2026-07-17T03:11:01.5045602+00:00 | 1 | capacity | started | 0ms | 39.0 MiB / 7.8 MiB |
| Dekaf (3conn) | 2026-07-17T03:11:16.5232624+00:00 | 1 | capacity | succeeded | 15,018ms | 39.0 MiB / 1.2 MiB |
| Dekaf (3conn) | 2026-07-17T03:11:46.5685665+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 1.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:12:31.6341079+00:00 | 1 | capacity | started | 0ms | 45.0 MiB / 9.2 MiB |
| Dekaf (3conn) | 2026-07-17T03:12:46.6535633+00:00 | 1 | capacity | succeeded | 15,019ms | 45.0 MiB / 3.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:13:16.6907622+00:00 | 1 | capacity | started | 0ms | 48.0 MiB / 1.4 MiB |
| Dekaf (3conn) | 2026-07-17T03:13:31.7058835+00:00 | 1 | capacity | succeeded | 15,015ms | 48.0 MiB / 2.2 MiB |
| Dekaf (3conn) | 2026-07-17T03:14:01.7579438+00:00 | 1 | capacity | started | 0ms | 54.0 MiB / 8.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:14:16.7801891+00:00 | 1 | capacity | succeeded | 15,022ms | 54.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:15:01.8605383+00:00 | 1 | capacity | succeeded | 15,034ms | 60.0 MiB / 45.0 MiB |
| Dekaf (3conn) | 2026-07-17T03:15:31.900692+00:00 | 1 | capacity | started | 0ms | 66.0 MiB / 6.6 MiB |
| Dekaf (3conn) | 2026-07-17T03:15:46.9218694+00:00 | 1 | capacity | succeeded | 15,021ms | 66.0 MiB / 47.5 MiB |
| Dekaf (3conn) | 2026-07-17T03:16:16.9701372+00:00 | 1 | capacity | started | 0ms | 72.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-17T03:16:31.990248+00:00 | 1 | capacity | failed | 15,020ms | 66.0 MiB / 16.3 MiB |
| Dekaf (3conn) | 2026-07-17T03:17:02.0488793+00:00 | 1 | capacity | started | 0ms | 57.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-17T03:17:47.1043734+00:00 | 1 | capacity | started | 0ms | 48.0 MiB / 57.0 MiB |
| Dekaf (3conn) | 2026-07-17T03:18:02.2160365+00:00 | 1 | capacity | succeeded | 15,111ms | 48.0 MiB / 7.2 MiB |
| Dekaf (3conn) | 2026-07-17T03:18:32.2577698+00:00 | 1 | capacity | started | 0ms | 42.0 MiB / 9.9 MiB |
*17 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent)

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 2,105 |
| Dekaf | 1 | 0.002–0.004ms | 2,483 |
| Dekaf | 1 | 0.004–0.008ms | 8,073 |
| Dekaf | 1 | 0.008–0.016ms | 36,776 |
| Dekaf | 1 | 0.016–0.032ms | 53,363 |
| Dekaf | 1 | 0.032–0.064ms | 58,577 |
| Dekaf | 1 | 0.064–0.128ms | 112,352 |
| Dekaf | 1 | 0.128–0.256ms | 305,102 |
| Dekaf | 1 | 0.256–0.512ms | 354,385 |
| Dekaf | 1 | 0.512–1.024ms | 51,680 |
| Dekaf | 1 | 1.024–2.048ms | 4,462 |
| Dekaf | 1 | 2.048–4.096ms | 4,031 |
| Dekaf | 1 | 4.096–8.192ms | 712 |
| Dekaf | 1 | 8.192–16.384ms | 47 |
| Dekaf | 1 | 16.384–32.768ms | 2 |
| Dekaf | 1 | 32.768–65.536ms | 1 |
| Dekaf | 1 | 0.001–0.002ms | 2,252 |
| Dekaf | 1 | 0.002–0.004ms | 2,714 |
| Dekaf | 1 | 0.004–0.008ms | 9,052 |
| Dekaf | 1 | 0.008–0.016ms | 41,553 |
| Dekaf | 1 | 0.016–0.032ms | 62,902 |
| Dekaf | 1 | 0.032–0.064ms | 61,463 |
| Dekaf | 1 | 0.064–0.128ms | 117,915 |
| Dekaf | 1 | 0.128–0.256ms | 300,698 |
| Dekaf | 1 | 0.256–0.512ms | 323,696 |
| Dekaf | 1 | 0.512–1.024ms | 46,953 |
| Dekaf | 1 | 1.024–2.048ms | 4,909 |
| Dekaf | 1 | 2.048–4.096ms | 3,927 |
| Dekaf | 1 | 4.096–8.192ms | 756 |
| Dekaf | 1 | 8.192–16.384ms | 40 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 6 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 2 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 16 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 56 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 129 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 203 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 277 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 427 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 514 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 486 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 269 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 186 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 52 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 6 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 1 |
| Dekaf (3conn) | 1 | 131.072–262.144ms | 2 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent)

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 671,460,000 | 2026-07-17T02:57:25.1337057+00:00 | 109.3ms | GC pause | - | - | 521.4s / 911,457 msg/s | Gen2 +0 / pause +187.8ms |
| Confluent | 671,462,000 | 2026-07-17T02:57:25.13519+00:00 | 108.7ms | GC pause | - | - | 521.4s / 911,457 msg/s | Gen2 +0 / pause +187.8ms |
| Confluent | 671,463,000 | 2026-07-17T02:57:25.135717+00:00 | 108.3ms | GC pause | - | - | 521.4s / 911,457 msg/s | Gen2 +0 / pause +187.8ms |
| Confluent | 671,464,000 | 2026-07-17T02:57:25.1362164+00:00 | 107.1ms | GC pause | - | - | 521.4s / 911,457 msg/s | Gen2 +0 / pause +187.8ms |
| Confluent | 671,465,000 | 2026-07-17T02:57:25.1367315+00:00 | 106.9ms | GC pause | - | - | 521.4s / 911,457 msg/s | Gen2 +0 / pause +187.8ms |
| Dekaf (3conn) | 94,890,000 | 2026-07-17T03:05:01.9501176+00:00 | 120.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,900,000 | 2026-07-17T03:05:01.957243+00:00 | 117.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,910,000 | 2026-07-17T03:05:01.963676+00:00 | 136.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,920,000 | 2026-07-17T03:05:01.9719193+00:00 | 129.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,930,000 | 2026-07-17T03:05:01.9874102+00:00 | 122.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,940,000 | 2026-07-17T03:05:01.9960015+00:00 | 124.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,950,000 | 2026-07-17T03:05:02.0096475+00:00 | 112.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 94,960,000 | 2026-07-17T03:05:02.0218325+00:00 | 107.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,010,000 | 2026-07-17T03:05:02.0821101+00:00 | 105.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,020,000 | 2026-07-17T03:05:02.093601+00:00 | 102.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,040,000 | 2026-07-17T03:05:02.1232511+00:00 | 155.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,050,000 | 2026-07-17T03:05:02.1336288+00:00 | 145.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,060,000 | 2026-07-17T03:05:02.1405636+00:00 | 145.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,070,000 | 2026-07-17T03:05:02.1547832+00:00 | 130.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,080,000 | 2026-07-17T03:05:02.1670354+00:00 | 118.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,090,000 | 2026-07-17T03:05:02.1768587+00:00 | 112.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,100,000 | 2026-07-17T03:05:02.1850855+00:00 | 112.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,110,000 | 2026-07-17T03:05:02.1941767+00:00 | 103.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,120,000 | 2026-07-17T03:05:02.2072228+00:00 | 107.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,130,000 | 2026-07-17T03:05:02.2182534+00:00 | 128.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,140,000 | 2026-07-17T03:05:02.2290868+00:00 | 120.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 95,150,000 | 2026-07-17T03:05:02.2441828+00:00 | 105.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 77.0s / 1,051,012 msg/s | Gen2 +0 / pause +1.7ms |
| Dekaf (3conn) | 279,655,000 | 2026-07-17T03:07:10.7739669+00:00 | 102.9ms | broker/backlog (no scale or GC event) | - | - | 205.2s / 1,120,137 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 279,660,000 | 2026-07-17T03:07:10.7813034+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 205.2s / 1,120,137 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 279,670,000 | 2026-07-17T03:07:10.7875716+00:00 | 106.5ms | broker/backlog (no scale or GC event) | - | - | 205.2s / 1,120,137 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 1,027,730,000 | 2026-07-17T03:14:53.7706783+00:00 | 101.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 668.3s / 2,095,318 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,054,500,000 | 2026-07-17T03:15:06.8168442+00:00 | 111.4ms | broker/backlog (no scale or GC event) | - | - | 681.4s / 2,113,799 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,054,510,000 | 2026-07-17T03:15:06.8206328+00:00 | 109.0ms | broker/backlog (no scale or GC event) | - | - | 681.4s / 2,113,799 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,054,520,000 | 2026-07-17T03:15:06.824536+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 681.4s / 2,113,799 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,054,530,000 | 2026-07-17T03:15:06.8287165+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 681.4s / 2,113,799 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,054,540,000 | 2026-07-17T03:15:06.8324479+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 681.4s / 2,113,799 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,054,550,000 | 2026-07-17T03:15:06.8365289+00:00 | 100.5ms | broker/backlog (no scale or GC event) | - | - | 681.4s / 2,113,799 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,091,610,000 | 2026-07-17T03:15:24.8226949+00:00 | 101.2ms | broker/backlog (no scale or GC event) | - | - | 699.4s / 2,023,967 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,132,190,000 | 2026-07-17T03:15:44.8051486+00:00 | 102.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 719.4s / 2,178,835 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,149,320,000 | 2026-07-17T03:15:52.8115871+00:00 | 123.5ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,330,000 | 2026-07-17T03:15:52.8194813+00:00 | 120.4ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,340,000 | 2026-07-17T03:15:52.8265242+00:00 | 115.9ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,350,000 | 2026-07-17T03:15:52.8371702+00:00 | 110.2ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,360,000 | 2026-07-17T03:15:52.8430637+00:00 | 109.5ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,370,000 | 2026-07-17T03:15:52.8472532+00:00 | 107.2ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,380,000 | 2026-07-17T03:15:52.8513188+00:00 | 109.3ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,390,000 | 2026-07-17T03:15:52.8564806+00:00 | 109.1ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,400,000 | 2026-07-17T03:15:52.8602995+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,410,000 | 2026-07-17T03:15:52.8643552+00:00 | 107.0ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,420,000 | 2026-07-17T03:15:52.8706284+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,149,430,000 | 2026-07-17T03:15:52.876508+00:00 | 100.2ms | broker/backlog (no scale or GC event) | - | - | 727.4s / 2,466,492 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,163,320,000 | 2026-07-17T03:15:59.8183569+00:00 | 145.0ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,330,000 | 2026-07-17T03:15:59.8223868+00:00 | 144.9ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,340,000 | 2026-07-17T03:15:59.8261739+00:00 | 141.1ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,350,000 | 2026-07-17T03:15:59.8314214+00:00 | 137.5ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,360,000 | 2026-07-17T03:15:59.8355807+00:00 | 133.4ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,370,000 | 2026-07-17T03:15:59.8398425+00:00 | 129.8ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,380,000 | 2026-07-17T03:15:59.8436515+00:00 | 126.5ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,390,000 | 2026-07-17T03:15:59.8478938+00:00 | 123.4ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,400,000 | 2026-07-17T03:15:59.8559031+00:00 | 116.0ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,410,000 | 2026-07-17T03:15:59.8604549+00:00 | 117.3ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,420,000 | 2026-07-17T03:15:59.8667118+00:00 | 111.1ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,163,430,000 | 2026-07-17T03:15:59.8765806+00:00 | 102.4ms | broker/backlog (no scale or GC event) | - | - | 734.4s / 2,072,344 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,171,230,000 | 2026-07-17T03:16:03.814821+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 738.4s / 1,801,557 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,171,240,000 | 2026-07-17T03:16:03.819097+00:00 | 101.5ms | broker/backlog (no scale or GC event) | - | - | 738.4s / 1,801,557 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,179,630,000 | 2026-07-17T03:16:07.8133678+00:00 | 104.6ms | broker/backlog (no scale or GC event) | - | - | 742.4s / 2,399,985 msg/s | Gen2 +0 / pause +0.6ms |
| Dekaf (3conn) | 1,200,540,000 | 2026-07-17T03:16:18.8131562+00:00 | 107.7ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 753.4s / 2,141,065 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,200,550,000 | 2026-07-17T03:16:18.817311+00:00 | 104.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 753.4s / 2,141,065 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,200,560,000 | 2026-07-17T03:16:18.821182+00:00 | 101.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 753.4s / 2,141,065 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,212,730,000 | 2026-07-17T03:16:24.8047209+00:00 | 101.4ms | broker/backlog (no scale or GC event) | 1:capacity/failed | - | 759.4s / 2,144,045 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,257,950,000 | 2026-07-17T03:16:46.8146445+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 781.4s / 2,382,487 msg/s | Gen2 +0 / pause +1.5ms |
| Dekaf (3conn) | 1,264,090,000 | 2026-07-17T03:16:49.8076074+00:00 | 129.9ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,100,000 | 2026-07-17T03:16:49.8112467+00:00 | 129.6ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,110,000 | 2026-07-17T03:16:49.8155362+00:00 | 126.2ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,120,000 | 2026-07-17T03:16:49.8194639+00:00 | 122.0ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,130,000 | 2026-07-17T03:16:49.825087+00:00 | 119.6ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,140,000 | 2026-07-17T03:16:49.8316792+00:00 | 113.0ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,150,000 | 2026-07-17T03:16:49.8386857+00:00 | 106.8ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,160,000 | 2026-07-17T03:16:49.8432459+00:00 | 106.2ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,264,170,000 | 2026-07-17T03:16:49.8473625+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 784.4s / 2,076,727 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,278,090,000 | 2026-07-17T03:16:56.8108272+00:00 | 111.7ms | broker/backlog (no scale or GC event) | - | - | 791.4s / 1,923,555 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,278,100,000 | 2026-07-17T03:16:56.8150246+00:00 | 108.5ms | broker/backlog (no scale or GC event) | - | - | 791.4s / 1,923,555 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,278,110,000 | 2026-07-17T03:16:56.8195426+00:00 | 104.8ms | broker/backlog (no scale or GC event) | - | - | 791.4s / 1,923,555 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,278,120,000 | 2026-07-17T03:16:56.8238641+00:00 | 102.1ms | broker/backlog (no scale or GC event) | - | - | 791.4s / 1,923,555 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,278,130,000 | 2026-07-17T03:16:56.8284892+00:00 | 102.6ms | broker/backlog (no scale or GC event) | - | - | 791.4s / 1,923,555 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,325,860,000 | 2026-07-17T03:17:22.3501618+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,632,198 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 1,326,800,000 | 2026-07-17T03:17:22.8914851+00:00 | 119.2ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,632,198 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 1,326,810,000 | 2026-07-17T03:17:22.897383+00:00 | 113.7ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,632,198 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 1,326,820,000 | 2026-07-17T03:17:22.9034186+00:00 | 109.2ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,632,198 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 1,326,830,000 | 2026-07-17T03:17:22.9085959+00:00 | 106.9ms | broker/backlog (no scale or GC event) | - | - | 817.4s / 1,632,198 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf (3conn) | 1,357,900,000 | 2026-07-17T03:17:39.817604+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 834.4s / 2,611,247 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,431,000 | 2026-07-17T03:17:46.9243687+00:00 | 223.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,432,000 | 2026-07-17T03:17:46.9247411+00:00 | 223.4ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,434,000 | 2026-07-17T03:17:46.9253846+00:00 | 221.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,436,000 | 2026-07-17T03:17:46.9262478+00:00 | 221.3ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,438,000 | 2026-07-17T03:17:46.9272118+00:00 | 247.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,440,000 | 2026-07-17T03:17:46.9280135+00:00 | 272.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,441,000 | 2026-07-17T03:17:46.9283707+00:00 | 229.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,442,000 | 2026-07-17T03:17:46.9287391+00:00 | 228.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,444,000 | 2026-07-17T03:17:46.9297078+00:00 | 219.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,445,000 | 2026-07-17T03:17:46.9300994+00:00 | 245.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,446,000 | 2026-07-17T03:17:46.9304735+00:00 | 219.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,448,000 | 2026-07-17T03:17:46.9313059+00:00 | 243.9ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,450,000 | 2026-07-17T03:17:46.9321428+00:00 | 268.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,451,000 | 2026-07-17T03:17:46.9324635+00:00 | 226.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,452,000 | 2026-07-17T03:17:46.9326724+00:00 | 226.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,454,000 | 2026-07-17T03:17:46.9332657+00:00 | 216.9ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,455,000 | 2026-07-17T03:17:46.9337619+00:00 | 242.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,456,000 | 2026-07-17T03:17:46.9342641+00:00 | 216.6ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,458,000 | 2026-07-17T03:17:46.9349093+00:00 | 242.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,460,000 | 2026-07-17T03:17:46.9355513+00:00 | 266.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,461,000 | 2026-07-17T03:17:46.9360591+00:00 | 223.6ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,462,000 | 2026-07-17T03:17:46.9364188+00:00 | 223.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,464,000 | 2026-07-17T03:17:46.9371514+00:00 | 214.3ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,465,000 | 2026-07-17T03:17:46.9375118+00:00 | 240.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,466,000 | 2026-07-17T03:17:46.9377207+00:00 | 213.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,468,000 | 2026-07-17T03:17:46.9385644+00:00 | 239.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,470,000 | 2026-07-17T03:17:46.9393182+00:00 | 266.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,471,000 | 2026-07-17T03:17:46.9397268+00:00 | 223.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,472,000 | 2026-07-17T03:17:46.9399708+00:00 | 222.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,474,000 | 2026-07-17T03:17:46.9408724+00:00 | 211.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,475,000 | 2026-07-17T03:17:46.9412511+00:00 | 238.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,476,000 | 2026-07-17T03:17:46.9416373+00:00 | 211.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,478,000 | 2026-07-17T03:17:46.9422545+00:00 | 238.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,480,000 | 2026-07-17T03:17:46.9430051+00:00 | 263.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,481,000 | 2026-07-17T03:17:46.9434995+00:00 | 220.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,482,000 | 2026-07-17T03:17:46.9438479+00:00 | 220.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,484,000 | 2026-07-17T03:17:46.9444136+00:00 | 209.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,485,000 | 2026-07-17T03:17:46.944771+00:00 | 235.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,486,000 | 2026-07-17T03:17:46.9449987+00:00 | 208.4ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,488,000 | 2026-07-17T03:17:46.9458722+00:00 | 234.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,490,000 | 2026-07-17T03:17:46.9465928+00:00 | 260.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,491,000 | 2026-07-17T03:17:46.9469545+00:00 | 218.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,492,000 | 2026-07-17T03:17:46.9471646+00:00 | 218.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,494,000 | 2026-07-17T03:17:46.9480257+00:00 | 207.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,495,000 | 2026-07-17T03:17:46.9483997+00:00 | 234.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,496,000 | 2026-07-17T03:17:46.9487545+00:00 | 206.7ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,498,000 | 2026-07-17T03:17:46.9493266+00:00 | 233.6ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,500,000 | 2026-07-17T03:17:46.9501694+00:00 | 258.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,501,000 | 2026-07-17T03:17:46.9505399+00:00 | 216.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,502,000 | 2026-07-17T03:17:46.9508961+00:00 | 216.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,504,000 | 2026-07-17T03:17:46.9514865+00:00 | 204.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,505,000 | 2026-07-17T03:17:46.9520385+00:00 | 231.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,506,000 | 2026-07-17T03:17:46.9522827+00:00 | 203.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,508,000 | 2026-07-17T03:17:46.9531545+00:00 | 232.2ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,510,000 | 2026-07-17T03:17:46.9537623+00:00 | 255.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,371,511,000 | 2026-07-17T03:17:46.9567399+00:00 | 214.1ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,512,000 | 2026-07-17T03:17:46.9569794+00:00 | 213.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,514,000 | 2026-07-17T03:17:46.9609724+00:00 | 195.8ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,515,000 | 2026-07-17T03:17:46.9630189+00:00 | 223.0ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,516,000 | 2026-07-17T03:17:46.9632454+00:00 | 193.6ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,518,000 | 2026-07-17T03:17:46.9671364+00:00 | 218.9ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 841.4s / 1,572,515 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,371,520,000 | 2026-07-17T03:17:46.9771094+00:00 | 232.5ms | broker/backlog (no scale or GC event) | 1:capacity/started, 1:capacity/succeeded | - | 842.4s / 1,748,758 msg/s | Gen2 +0 / pause +0.4ms |
| Dekaf (3conn) | 1,379,760,000 | 2026-07-17T03:17:51.482032+00:00 | 217.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,763,000 | 2026-07-17T03:17:51.4829585+00:00 | 215.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,766,000 | 2026-07-17T03:17:51.4856729+00:00 | 219.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,767,000 | 2026-07-17T03:17:51.4860098+00:00 | 213.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,768,000 | 2026-07-17T03:17:51.4863312+00:00 | 234.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,769,000 | 2026-07-17T03:17:51.4866597+00:00 | 213.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,770,000 | 2026-07-17T03:17:51.4880524+00:00 | 243.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,771,000 | 2026-07-17T03:17:51.4905303+00:00 | 220.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,772,000 | 2026-07-17T03:17:51.4911333+00:00 | 219.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,773,000 | 2026-07-17T03:17:51.4914305+00:00 | 210.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,774,000 | 2026-07-17T03:17:51.4917479+00:00 | 213.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,775,000 | 2026-07-17T03:17:51.4920598+00:00 | 228.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,776,000 | 2026-07-17T03:17:51.4925108+00:00 | 212.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,777,000 | 2026-07-17T03:17:51.4931056+00:00 | 210.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,778,000 | 2026-07-17T03:17:51.4938254+00:00 | 227.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,779,000 | 2026-07-17T03:17:51.4941433+00:00 | 210.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,780,000 | 2026-07-17T03:17:51.4944416+00:00 | 236.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,781,000 | 2026-07-17T03:17:51.4947548+00:00 | 218.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,782,000 | 2026-07-17T03:17:51.4950728+00:00 | 218.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,783,000 | 2026-07-17T03:17:51.4957773+00:00 | 209.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,784,000 | 2026-07-17T03:17:51.4964572+00:00 | 208.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,785,000 | 2026-07-17T03:17:51.4967159+00:00 | 225.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,786,000 | 2026-07-17T03:17:51.4969462+00:00 | 208.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,787,000 | 2026-07-17T03:17:51.4971812+00:00 | 206.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,788,000 | 2026-07-17T03:17:51.4974076+00:00 | 225.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,789,000 | 2026-07-17T03:17:51.4979097+00:00 | 206.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,790,000 | 2026-07-17T03:17:51.4986835+00:00 | 235.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,791,000 | 2026-07-17T03:17:51.4989434+00:00 | 215.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,792,000 | 2026-07-17T03:17:51.4991817+00:00 | 215.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,793,000 | 2026-07-17T03:17:51.4994214+00:00 | 205.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,794,000 | 2026-07-17T03:17:51.4996822+00:00 | 206.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,795,000 | 2026-07-17T03:17:51.5001976+00:00 | 223.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,796,000 | 2026-07-17T03:17:51.5007199+00:00 | 205.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,797,000 | 2026-07-17T03:17:51.5012374+00:00 | 204.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,798,000 | 2026-07-17T03:17:51.5014857+00:00 | 221.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,799,000 | 2026-07-17T03:17:51.5017229+00:00 | 203.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,800,000 | 2026-07-17T03:17:51.5019686+00:00 | 234.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,801,000 | 2026-07-17T03:17:51.5024824+00:00 | 214.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,802,000 | 2026-07-17T03:17:51.5030415+00:00 | 213.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,803,000 | 2026-07-17T03:17:51.503577+00:00 | 202.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,804,000 | 2026-07-17T03:17:51.5038484+00:00 | 206.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,805,000 | 2026-07-17T03:17:51.5041088+00:00 | 219.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,806,000 | 2026-07-17T03:17:51.5043635+00:00 | 206.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,807,000 | 2026-07-17T03:17:51.5049093+00:00 | 201.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,808,000 | 2026-07-17T03:17:51.5052926+00:00 | 218.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,809,000 | 2026-07-17T03:17:51.5058178+00:00 | 202.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,810,000 | 2026-07-17T03:17:51.5062097+00:00 | 230.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,811,000 | 2026-07-17T03:17:51.5064583+00:00 | 211.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,812,000 | 2026-07-17T03:17:51.5067138+00:00 | 211.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,813,000 | 2026-07-17T03:17:51.5072375+00:00 | 200.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,814,000 | 2026-07-17T03:17:51.5076229+00:00 | 203.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,815,000 | 2026-07-17T03:17:51.5081311+00:00 | 216.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,816,000 | 2026-07-17T03:17:51.5083533+00:00 | 202.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,817,000 | 2026-07-17T03:17:51.5087207+00:00 | 199.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,818,000 | 2026-07-17T03:17:51.5089512+00:00 | 215.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,819,000 | 2026-07-17T03:17:51.5093249+00:00 | 199.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,820,000 | 2026-07-17T03:17:51.5098253+00:00 | 228.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,821,000 | 2026-07-17T03:17:51.5101999+00:00 | 209.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,379,822,000 | 2026-07-17T03:17:51.510561+00:00 | 209.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 846.4s / 1,550,112 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 1,389,341,000 | 2026-07-17T03:17:56.4324262+00:00 | 218.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,342,000 | 2026-07-17T03:17:56.4326591+00:00 | 218.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,346,000 | 2026-07-17T03:17:56.4357246+00:00 | 220.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,347,000 | 2026-07-17T03:17:56.4371017+00:00 | 219.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,348,000 | 2026-07-17T03:17:56.4384611+00:00 | 218.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,349,000 | 2026-07-17T03:17:56.4398456+00:00 | 213.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,351,000 | 2026-07-17T03:17:56.4425642+00:00 | 218.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,352,000 | 2026-07-17T03:17:56.4430052+00:00 | 219.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,353,000 | 2026-07-17T03:17:56.4434441+00:00 | 213.1ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,354,000 | 2026-07-17T03:17:56.4438612+00:00 | 213.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,355,000 | 2026-07-17T03:17:56.4442677+00:00 | 229.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,356,000 | 2026-07-17T03:17:56.4447043+00:00 | 213.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,357,000 | 2026-07-17T03:17:56.4451432+00:00 | 212.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,358,000 | 2026-07-17T03:17:56.4455802+00:00 | 227.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,359,000 | 2026-07-17T03:17:56.4459872+00:00 | 210.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,361,000 | 2026-07-17T03:17:56.446988+00:00 | 216.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,362,000 | 2026-07-17T03:17:56.4472203+00:00 | 216.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,363,000 | 2026-07-17T03:17:56.4476208+00:00 | 208.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,364,000 | 2026-07-17T03:17:56.4480147+00:00 | 210.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,365,000 | 2026-07-17T03:17:56.4482404+00:00 | 225.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,366,000 | 2026-07-17T03:17:56.4487711+00:00 | 210.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,367,000 | 2026-07-17T03:17:56.4491335+00:00 | 208.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,368,000 | 2026-07-17T03:17:56.4495178+00:00 | 225.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,369,000 | 2026-07-17T03:17:56.4498847+00:00 | 207.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,371,000 | 2026-07-17T03:17:56.4505255+00:00 | 214.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,372,000 | 2026-07-17T03:17:56.45108+00:00 | 214.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,373,000 | 2026-07-17T03:17:56.4514545+00:00 | 206.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,374,000 | 2026-07-17T03:17:56.451836+00:00 | 207.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,375,000 | 2026-07-17T03:17:56.4523683+00:00 | 224.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,376,000 | 2026-07-17T03:17:56.4528065+00:00 | 206.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,377,000 | 2026-07-17T03:17:56.4530175+00:00 | 204.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,378,000 | 2026-07-17T03:17:56.4533504+00:00 | 223.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,379,000 | 2026-07-17T03:17:56.4538372+00:00 | 203.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,381,000 | 2026-07-17T03:17:56.4546182+00:00 | 211.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,382,000 | 2026-07-17T03:17:56.4550292+00:00 | 212.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,383,000 | 2026-07-17T03:17:56.4553108+00:00 | 202.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,384,000 | 2026-07-17T03:17:56.4556026+00:00 | 204.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,386,000 | 2026-07-17T03:17:56.4566116+00:00 | 203.7ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,387,000 | 2026-07-17T03:17:56.4571955+00:00 | 202.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,389,000 | 2026-07-17T03:17:56.4579807+00:00 | 201.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,391,000 | 2026-07-17T03:17:56.4588976+00:00 | 209.2ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,392,000 | 2026-07-17T03:17:56.459356+00:00 | 208.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,393,000 | 2026-07-17T03:17:56.4599505+00:00 | 200.0ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,394,000 | 2026-07-17T03:17:56.4603685+00:00 | 201.8ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,396,000 | 2026-07-17T03:17:56.4609425+00:00 | 201.3ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,397,000 | 2026-07-17T03:17:56.4614755+00:00 | 198.9ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,399,000 | 2026-07-17T03:17:56.4622136+00:00 | 198.5ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,401,000 | 2026-07-17T03:17:56.4629119+00:00 | 206.6ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |
| Dekaf (3conn) | 1,389,402,000 | 2026-07-17T03:17:56.4631297+00:00 | 206.4ms | broker/backlog (no scale or GC event) | 1:capacity/succeeded | - | 851.4s / 1,887,647 msg/s | Gen2 +0 / pause +0.5ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*18 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.44x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.19x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.14 | 1159.32 | 1,215,642 | 1,220,702 | -0.8% | -0.09% | 1159.33 | 1,215,642 | 0 | 1.38 |
| Dekaf | 1.17 | 1191.41 | 1,089,426 | 1,095,858 | -1.5% | -0.11% | 1038.96 | 1,089,426 | 0 | 1.28 |
| Confluent | 1.85 | - | 848,917 | 850,797 | +5.0% | +0.56% | 809.59 | 848,917 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 325,675 | 361.85 | 1018.18 KB |
| Dekaf | 2 | 320,283 | 355.86 | 1003.07 KB |
| Dekaf | 3 | 320,563 | 356.17 | 1004.01 KB |
| Dekaf (3conn) | 1 | 367,468 | 408.28 | 1017.60 KB |
| Dekaf (3conn) | 2 | 352,198 | 391.32 | 1013.84 KB |
| Dekaf (3conn) | 3 | 352,575 | 391.74 | 1011.52 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:46.2294846+00:00 | 2 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 837,869 msg/s |
| Dekaf | 2026-07-17T02:19:04.2301062+00:00 | 3 | 16.0 MiB / 12.4 MiB | 388.9 MB/s | 0/0 | 504 | 18.0s / 1,091,555 msg/s |
| Dekaf | 2026-07-17T02:19:23.2458048+00:00 | 1 | 16.0 MiB / 9.6 MiB | 409.5 MB/s | 0/0 | 16,493 | 37.0s / 1,079,946 msg/s |
| Dekaf | 2026-07-17T02:19:41.2562609+00:00 | 1 | 14.0 MiB / 14.0 MiB | 409.5 MB/s | 1/0 | 22,653 | 55.0s / 1,056,953 msg/s |
| Dekaf | 2026-07-17T02:19:59.2610454+00:00 | 2 | 12.0 MiB / 6.9 MiB | 393.4 MB/s | 2/0 | 3,935 | 73.1s / 1,105,429 msg/s |
| Dekaf | 2026-07-17T02:20:17.2633476+00:00 | 2 | 10.0 MiB / 3.7 MiB | 404.6 MB/s | 3/0 | 5,416 | 91.1s / 1,100,782 msg/s |
| Dekaf | 2026-07-17T02:20:35.2710443+00:00 | 3 | 8.0 MiB / 3.2 MiB | 395.0 MB/s | 4/0 | 11,654 | 109.1s / 1,098,154 msg/s |
| Dekaf | 2026-07-17T02:20:53.2788004+00:00 | 3 | 8.0 MiB / 1.8 MiB | 395.0 MB/s | 4/1 | 14,455 | 127.1s / 1,090,469 msg/s |
| Dekaf | 2026-07-17T02:21:12.2863284+00:00 | 1 | 9.0 MiB / 5.7 MiB | 420.5 MB/s | 5/0 | 66,101 | 146.1s / 1,112,147 msg/s |
| Dekaf | 2026-07-17T02:21:30.2913615+00:00 | 1 | 7.0 MiB / 3.9 MiB | 420.5 MB/s | 6/0 | 74,107 | 164.1s / 1,087,662 msg/s |
| Dekaf | 2026-07-17T02:21:48.2984438+00:00 | 2 | 9.0 MiB / 6.9 MiB | 404.6 MB/s | 5/1 | 18,944 | 182.1s / 1,118,858 msg/s |
| Dekaf | 2026-07-17T02:22:06.3065391+00:00 | 2 | 9.0 MiB / 6.3 MiB | 404.6 MB/s | 5/1 | 21,907 | 200.1s / 1,131,144 msg/s |
| Dekaf | 2026-07-17T02:22:24.313703+00:00 | 3 | 9.0 MiB / 2.6 MiB | 401.4 MB/s | 5/2 | 30,418 | 218.1s / 1,085,464 msg/s |
| Dekaf | 2026-07-17T02:22:42.3172876+00:00 | 3 | 9.0 MiB / 6.2 MiB | 410.6 MB/s | 5/2 | 31,865 | 236.1s / 1,122,726 msg/s |
| Dekaf | 2026-07-17T02:23:01.3283334+00:00 | 1 | 6.0 MiB / 5.4 MiB | 420.5 MB/s | 7/2 | 129,623 | 255.1s / 1,026,489 msg/s |
| Dekaf | 2026-07-17T02:23:19.3358485+00:00 | 1 | 6.0 MiB / 6.0 MiB | 420.5 MB/s | 7/2 | 142,175 | 273.1s / 1,111,857 msg/s |
| Dekaf | 2026-07-17T02:23:37.3433521+00:00 | 2 | 9.0 MiB / 2.9 MiB | 404.6 MB/s | 5/3 | 30,871 | 291.1s / 1,083,534 msg/s |
| Dekaf | 2026-07-17T02:23:55.3524472+00:00 | 2 | 7.0 MiB / 4.7 MiB | 404.6 MB/s | 6/3 | 34,306 | 309.1s / 1,112,889 msg/s |
| Dekaf | 2026-07-17T02:24:13.3564591+00:00 | 3 | 9.0 MiB / 6.7 MiB | 410.6 MB/s | 5/4 | 42,552 | 327.2s / 1,063,310 msg/s |
| Dekaf | 2026-07-17T02:24:31.3607592+00:00 | 3 | 7.0 MiB / 1.6 MiB | 410.6 MB/s | 6/4 | 45,742 | 345.2s / 1,107,888 msg/s |
| Dekaf | 2026-07-17T02:24:50.3740686+00:00 | 1 | 8.0 MiB / 5.6 MiB | 420.5 MB/s | 9/2 | 191,970 | 364.2s / 1,163,599 msg/s |
| Dekaf | 2026-07-17T02:25:08.3803617+00:00 | 1 | 8.0 MiB / 5.7 MiB | 420.5 MB/s | 9/3 | 199,733 | 382.2s / 1,088,470 msg/s |
| Dekaf | 2026-07-17T02:25:26.3862828+00:00 | 2 | 7.0 MiB / 6.1 MiB | 404.6 MB/s | 6/5 | 58,573 | 400.2s / 1,041,951 msg/s |
| Dekaf | 2026-07-17T02:25:44.3905346+00:00 | 2 | 7.0 MiB / 6.4 MiB | 404.6 MB/s | 6/6 | 63,149 | 418.2s / 1,153,422 msg/s |
| Dekaf | 2026-07-17T02:26:02.3984216+00:00 | 3 | 7.0 MiB / 3.4 MiB | 410.6 MB/s | 6/6 | 69,644 | 436.2s / 1,083,234 msg/s |
| Dekaf | 2026-07-17T02:26:20.4018121+00:00 | 3 | 7.0 MiB / 4.9 MiB | 410.6 MB/s | 6/7 | 72,785 | 454.2s / 1,077,855 msg/s |
| Dekaf | 2026-07-17T02:26:39.4076182+00:00 | 1 | 8.0 MiB / 5.8 MiB | 420.5 MB/s | 9/5 | 249,543 | 473.2s / 1,132,960 msg/s |
| Dekaf | 2026-07-17T02:26:57.4145844+00:00 | 1 | 8.0 MiB / 6.7 MiB | 420.5 MB/s | 9/5 | 257,620 | 491.2s / 1,131,787 msg/s |
| Dekaf | 2026-07-17T02:27:15.4163561+00:00 | 2 | 7.0 MiB / 7.0 MiB | 404.6 MB/s | 6/8 | 83,078 | 509.2s / 1,074,903 msg/s |
| Dekaf | 2026-07-17T02:27:33.4219706+00:00 | 2 | 6.0 MiB / 2.8 MiB | 404.6 MB/s | 6/8 | 88,008 | 527.2s / 1,111,018 msg/s |
| Dekaf | 2026-07-17T02:27:51.4243202+00:00 | 3 | 8.0 MiB / 4.0 MiB | 410.6 MB/s | 7/8 | 95,598 | 545.2s / 1,007,893 msg/s |
| Dekaf | 2026-07-17T02:28:09.4273165+00:00 | 3 | 8.0 MiB / 7.1 MiB | 410.6 MB/s | 7/8 | 96,162 | 563.3s / 1,099,265 msg/s |
| Dekaf | 2026-07-17T02:28:28.4341982+00:00 | 1 | 7.0 MiB / 7.0 MiB | 420.5 MB/s | 11/6 | 297,951 | 582.3s / 993,039 msg/s |
| Dekaf | 2026-07-17T02:28:46.4368704+00:00 | 1 | 7.0 MiB / 7.0 MiB | 420.5 MB/s | 11/6 | 308,091 | 600.3s / 1,098,242 msg/s |
| Dekaf | 2026-07-17T02:29:04.4371293+00:00 | 2 | 7.0 MiB / 6.0 MiB | 404.6 MB/s | 8/9 | 118,001 | 618.3s / 1,062,824 msg/s |
| Dekaf | 2026-07-17T02:29:22.4431353+00:00 | 2 | 8.0 MiB / 8.0 MiB | 404.6 MB/s | 8/9 | 121,212 | 636.3s / 1,132,065 msg/s |
| Dekaf | 2026-07-17T02:29:40.4472729+00:00 | 3 | 9.0 MiB / 9.0 MiB | 410.6 MB/s | 8/9 | 101,997 | 654.3s / 1,124,328 msg/s |
| Dekaf | 2026-07-17T02:29:58.449434+00:00 | 3 | 9.0 MiB / 8.1 MiB | 410.6 MB/s | 8/9 | 103,000 | 672.3s / 1,063,449 msg/s |
| Dekaf | 2026-07-17T02:30:17.457391+00:00 | 1 | 7.0 MiB / 6.7 MiB | 420.5 MB/s | 13/7 | 363,335 | 691.3s / 1,117,025 msg/s |
| Dekaf | 2026-07-17T02:30:35.4623431+00:00 | 1 | 7.0 MiB / 3.7 MiB | 420.5 MB/s | 13/7 | 376,833 | 709.3s / 1,095,610 msg/s |
| Dekaf | 2026-07-17T02:30:53.4686924+00:00 | 2 | 9.0 MiB / 6.2 MiB | 404.6 MB/s | 9/10 | 132,801 | 727.3s / 1,111,939 msg/s |
| Dekaf | 2026-07-17T02:31:11.4748785+00:00 | 2 | 9.0 MiB / 3.0 MiB | 404.6 MB/s | 10/10 | 134,279 | 745.3s / 1,039,907 msg/s |
| Dekaf | 2026-07-17T02:31:29.4787226+00:00 | 3 | 9.0 MiB / 7.6 MiB | 410.6 MB/s | 10/9 | 116,761 | 763.3s / 1,069,734 msg/s |
| Dekaf | 2026-07-17T02:31:47.4854123+00:00 | 3 | 7.0 MiB / 7.0 MiB | 410.6 MB/s | 11/9 | 120,931 | 781.3s / 986,775 msg/s |
| Dekaf | 2026-07-17T02:32:06.4874032+00:00 | 1 | 7.0 MiB / 6.2 MiB | 420.5 MB/s | 13/9 | 427,965 | 800.3s / 1,049,379 msg/s |
| Dekaf | 2026-07-17T02:32:24.4918616+00:00 | 1 | 6.0 MiB / 5.9 MiB | 420.5 MB/s | 13/9 | 437,962 | 818.4s / 1,086,819 msg/s |
| Dekaf | 2026-07-17T02:32:42.4946892+00:00 | 2 | 11.0 MiB / 4.6 MiB | 404.6 MB/s | 12/10 | 140,024 | 836.4s / 1,146,164 msg/s |
| Dekaf | 2026-07-17T02:33:00.5036134+00:00 | 2 | 11.0 MiB / 2.6 MiB | 404.6 MB/s | 12/10 | 140,581 | 854.4s / 1,083,357 msg/s |
| Dekaf | 2026-07-17T02:33:18.5098051+00:00 | 3 | 9.0 MiB / 9.0 MiB | 410.6 MB/s | 12/10 | 136,000 | 872.4s / 1,100,977 msg/s |
| Dekaf | 2026-07-17T02:33:36.5119139+00:00 | 3 | 9.0 MiB / 6.2 MiB | 410.6 MB/s | 13/10 | 138,277 | 890.4s / 1,096,646 msg/s |
| Dekaf (3conn) | 2026-07-17T02:34:08.4203391+00:00 | 3 | 48.0 MiB / 8.0 MiB | 337.9 MB/s | 0/0 | 961 | 9.0s / 943,240 msg/s |
| Dekaf (3conn) | 2026-07-17T02:34:26.4337947+00:00 | 3 | 48.0 MiB / 34.7 MiB | 468.1 MB/s | 0/0 | 2,293 | 27.0s / 1,266,385 msg/s |
| Dekaf (3conn) | 2026-07-17T02:34:45.442129+00:00 | 1 | 48.0 MiB / 30.6 MiB | 485.6 MB/s | 0/1 | 6,764 | 46.0s / 1,272,222 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:03.4514876+00:00 | 1 | 48.0 MiB / 25.4 MiB | 485.6 MB/s | 0/1 | 8,510 | 64.1s / 1,188,246 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:21.4652827+00:00 | 2 | 48.0 MiB / 26.3 MiB | 484.1 MB/s | 0/1 | 6,424 | 82.1s / 1,251,065 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:39.4762638+00:00 | 2 | 48.0 MiB / 32.0 MiB | 484.1 MB/s | 0/2 | 7,658 | 100.1s / 1,155,879 msg/s |
| Dekaf (3conn) | 2026-07-17T02:35:57.4903321+00:00 | 3 | 48.0 MiB / 13.7 MiB | 474.1 MB/s | 0/2 | 3,101 | 118.1s / 1,302,998 msg/s |
| Dekaf (3conn) | 2026-07-17T02:36:15.5159427+00:00 | 3 | 42.0 MiB / 4.9 MiB | 474.1 MB/s | 1/2 | 3,535 | 136.1s / 1,131,711 msg/s |
| Dekaf (3conn) | 2026-07-17T02:36:34.5294159+00:00 | 1 | 24.0 MiB / 14.8 MiB | 511.9 MB/s | 4/1 | 26,286 | 155.1s / 1,134,904 msg/s |
| Dekaf (3conn) | 2026-07-17T02:36:52.5463815+00:00 | 1 | 21.0 MiB / 18.5 MiB | 511.9 MB/s | 5/1 | 33,148 | 173.1s / 1,234,503 msg/s |
| Dekaf (3conn) | 2026-07-17T02:37:10.5524272+00:00 | 2 | 42.0 MiB / 10.5 MiB | 503.4 MB/s | 1/3 | 13,097 | 191.1s / 1,235,958 msg/s |
| Dekaf (3conn) | 2026-07-17T02:37:28.5590261+00:00 | 2 | 36.0 MiB / 21.7 MiB | 503.4 MB/s | 2/3 | 15,223 | 209.1s / 1,188,488 msg/s |
| Dekaf (3conn) | 2026-07-17T02:37:46.5654404+00:00 | 3 | 30.0 MiB / 26.3 MiB | 474.1 MB/s | 3/3 | 8,720 | 227.2s / 1,250,842 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:04.5747747+00:00 | 3 | 24.0 MiB / 10.0 MiB | 474.1 MB/s | 4/3 | 9,844 | 245.2s / 1,222,961 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:23.5855879+00:00 | 1 | 15.0 MiB / 15.0 MiB | 511.9 MB/s | 7/3 | 64,555 | 264.2s / 1,256,441 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:41.5908352+00:00 | 1 | 15.0 MiB / 5.6 MiB | 511.9 MB/s | 7/3 | 71,886 | 282.2s / 1,199,480 msg/s |
| Dekaf (3conn) | 2026-07-17T02:38:59.6104849+00:00 | 2 | 21.0 MiB / 19.7 MiB | 503.4 MB/s | 5/4 | 22,468 | 300.2s / 1,241,206 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:17.6194612+00:00 | 2 | 18.0 MiB / 15.4 MiB | 503.4 MB/s | 6/4 | 23,487 | 318.2s / 1,263,186 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:35.6244626+00:00 | 3 | 18.0 MiB / 2.1 MiB | 474.1 MB/s | 6/5 | 15,495 | 336.2s / 1,263,622 msg/s |
| Dekaf (3conn) | 2026-07-17T02:39:53.6329279+00:00 | 3 | 18.0 MiB / 13.2 MiB | 474.1 MB/s | 6/5 | 15,794 | 354.2s / 1,221,945 msg/s |
| Dekaf (3conn) | 2026-07-17T02:40:12.6397712+00:00 | 1 | 15.0 MiB / 11.4 MiB | 511.9 MB/s | 7/5 | 89,887 | 373.2s / 1,195,755 msg/s |
| Dekaf (3conn) | 2026-07-17T02:40:30.6495277+00:00 | 1 | 15.0 MiB / 9.7 MiB | 511.9 MB/s | 7/5 | 92,483 | 391.3s / 1,222,195 msg/s |
| Dekaf (3conn) | 2026-07-17T02:40:48.6528375+00:00 | 2 | 18.0 MiB / 16.5 MiB | 503.4 MB/s | 6/6 | 29,508 | 409.3s / 1,286,150 msg/s |
| Dekaf (3conn) | 2026-07-17T02:41:06.6602098+00:00 | 2 | 18.0 MiB / 3.0 MiB | 503.4 MB/s | 6/7 | 31,757 | 427.3s / 1,319,467 msg/s |
| Dekaf (3conn) | 2026-07-17T02:41:24.6626948+00:00 | 3 | 21.0 MiB / 5.9 MiB | 474.1 MB/s | 7/6 | 17,855 | 445.3s / 1,295,636 msg/s |
| Dekaf (3conn) | 2026-07-17T02:41:42.6724817+00:00 | 3 | 21.0 MiB / 18.2 MiB | 474.1 MB/s | 7/6 | 18,650 | 463.3s / 1,293,796 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:01.6802435+00:00 | 1 | 18.0 MiB / 8.7 MiB | 511.9 MB/s | 8/6 | 104,415 | 482.3s / 1,299,027 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:19.6844951+00:00 | 1 | 18.0 MiB / 17.4 MiB | 511.9 MB/s | 8/7 | 106,225 | 500.3s / 1,224,980 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:37.6912013+00:00 | 2 | 18.0 MiB / 15.6 MiB | 503.4 MB/s | 7/8 | 40,673 | 518.3s / 1,260,732 msg/s |
| Dekaf (3conn) | 2026-07-17T02:42:55.7026904+00:00 | 2 | 15.0 MiB / 11.1 MiB | 503.4 MB/s | 7/9 | 42,574 | 536.3s / 1,075,721 msg/s |
| Dekaf (3conn) | 2026-07-17T02:43:13.7113318+00:00 | 3 | 18.0 MiB / 16.7 MiB | 474.1 MB/s | 8/8 | 23,725 | 554.4s / 1,240,675 msg/s |
| Dekaf (3conn) | 2026-07-17T02:43:31.7230259+00:00 | 3 | 18.0 MiB / 12.4 MiB | 474.1 MB/s | 8/8 | 26,245 | 572.4s / 1,203,502 msg/s |
| Dekaf (3conn) | 2026-07-17T02:43:50.7298039+00:00 | 1 | 15.0 MiB / 10.9 MiB | 511.9 MB/s | 9/8 | 129,415 | 591.4s / 1,254,024 msg/s |
| Dekaf (3conn) | 2026-07-17T02:44:08.7345565+00:00 | 1 | 15.0 MiB / 14.7 MiB | 511.9 MB/s | 9/9 | 136,229 | 609.4s / 1,102,907 msg/s |
| Dekaf (3conn) | 2026-07-17T02:44:26.746808+00:00 | 2 | 15.0 MiB / 10.9 MiB | 503.4 MB/s | 8/10 | 63,243 | 627.4s / 1,212,049 msg/s |
| Dekaf (3conn) | 2026-07-17T02:44:44.7525256+00:00 | 2 | 15.0 MiB / 3.0 MiB | 503.4 MB/s | 9/10 | 67,158 | 645.4s / 1,198,797 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:02.7662509+00:00 | 3 | 21.0 MiB / 3.5 MiB | 474.1 MB/s | 9/9 | 34,193 | 663.4s / 1,288,759 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:20.7871473+00:00 | 3 | 21.0 MiB / 6.4 MiB | 474.1 MB/s | 9/10 | 35,513 | 681.4s / 1,223,219 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:39.7989209+00:00 | 1 | 12.0 MiB / 5.1 MiB | 511.9 MB/s | 10/10 | 171,498 | 700.5s / 1,172,356 msg/s |
| Dekaf (3conn) | 2026-07-17T02:45:57.8063366+00:00 | 1 | 12.0 MiB / 12.0 MiB | 511.9 MB/s | 10/11 | 176,455 | 718.5s / 1,167,835 msg/s |
| Dekaf (3conn) | 2026-07-17T02:46:15.8122321+00:00 | 2 | 15.0 MiB / 7.5 MiB | 503.4 MB/s | 9/12 | 80,822 | 736.5s / 1,177,535 msg/s |
| Dekaf (3conn) | 2026-07-17T02:46:33.8199109+00:00 | 2 | 15.0 MiB / 11.1 MiB | 503.4 MB/s | 9/12 | 82,939 | 754.5s / 1,163,087 msg/s |
| Dekaf (3conn) | 2026-07-17T02:46:51.8280586+00:00 | 3 | 21.0 MiB / 18.9 MiB | 474.1 MB/s | 9/12 | 41,377 | 772.5s / 1,208,760 msg/s |
| Dekaf (3conn) | 2026-07-17T02:47:09.8339382+00:00 | 3 | 24.0 MiB / 4.8 MiB | 474.1 MB/s | 9/12 | 42,849 | 790.5s / 1,200,280 msg/s |
| Dekaf (3conn) | 2026-07-17T02:47:28.8448638+00:00 | 1 | 6.0 MiB / 5.2 MiB | 511.9 MB/s | 11/12 | 218,804 | 809.5s / 1,244,905 msg/s |
| Dekaf (3conn) | 2026-07-17T02:47:46.852233+00:00 | 1 | 9.0 MiB / 8.3 MiB | 511.9 MB/s | 12/12 | 229,444 | 827.5s / 1,160,307 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:04.858698+00:00 | 2 | 15.0 MiB / 5.6 MiB | 503.4 MB/s | 9/14 | 92,742 | 845.5s / 1,286,038 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:22.8724216+00:00 | 2 | 15.0 MiB / 8.6 MiB | 503.4 MB/s | 9/14 | 93,750 | 863.5s / 1,200,312 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:40.8788803+00:00 | 3 | 24.0 MiB / 7.1 MiB | 474.1 MB/s | 9/14 | 44,667 | 881.6s / 1,244,275 msg/s |
| Dekaf (3conn) | 2026-07-17T02:48:58.8882034+00:00 | 3 | 24.0 MiB / 2.2 MiB | 474.1 MB/s | 10/14 | 45,081 | 899.6s / 1,170,014 msg/s |
*5,296 budget sample(s) omitted; rows sampled across the full timeline.*

## Producer Budget Probe Events - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | UTC | Broker | Probe | Outcome | Duration | Budget / unacked |
|--------|-----|-------:|-------|---------|---------:|------------------|
| Dekaf | 2026-07-17T02:19:16.3185556+00:00 | 2 | capacity | started | 0ms | 14.0 MiB / 8.6 MiB |
| Dekaf | 2026-07-17T02:19:31.3642884+00:00 | 2 | capacity | succeeded | 15,046ms | 14.0 MiB / 7.4 MiB |
| Dekaf | 2026-07-17T02:19:34.3691043+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 7.8 MiB |
| Dekaf | 2026-07-17T02:19:49.4103798+00:00 | 2 | capacity | succeeded | 15,041ms | 12.0 MiB / 1.5 MiB |
| Dekaf | 2026-07-17T02:19:52.4325483+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-17T02:20:07.5026104+00:00 | 2 | capacity | succeeded | 15,070ms | 10.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:20:10.5094853+00:00 | 2 | capacity | started | 0ms | 8.0 MiB / 7.0 MiB |
| Dekaf | 2026-07-17T02:20:10.5825266+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:20:25.6274787+00:00 | 1 | capacity | succeeded | 15,044ms | 8.0 MiB / 6.2 MiB |
| Dekaf | 2026-07-17T02:20:28.6367392+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-17T02:20:43.6901375+00:00 | 1 | capacity | succeeded | 15,053ms | 9.0 MiB / 6.3 MiB |
| Dekaf | 2026-07-17T02:21:13.777517+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 6.7 MiB |
| Dekaf | 2026-07-17T02:21:28.8267941+00:00 | 1 | capacity | succeeded | 15,049ms | 7.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-17T02:21:58.83755+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-17T02:22:13.9656414+00:00 | 3 | capacity | succeeded | 15,073ms | 9.0 MiB / 3.4 MiB |
| Dekaf | 2026-07-17T02:22:35.0760105+00:00 | 1 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:22:50.1218768+00:00 | 1 | capacity | failed | 15,045ms | 6.0 MiB / 3.7 MiB |
| Dekaf | 2026-07-17T02:23:20.2215318+00:00 | 1 | capacity | started | 0ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:23:35.2683974+00:00 | 1 | capacity | succeeded | 15,046ms | 7.0 MiB / 5.9 MiB |
| Dekaf | 2026-07-17T02:23:44.2582942+00:00 | 3 | capacity | failed | 15,045ms | 9.0 MiB / 9.1 MiB |
| Dekaf | 2026-07-17T02:24:05.3586654+00:00 | 1 | capacity | started | 0ms | 8.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:24:29.3981313+00:00 | 3 | capacity | succeeded | 15,041ms | 7.0 MiB / 2.9 MiB |
| Dekaf | 2026-07-17T02:24:47.4274443+00:00 | 2 | capacity | failed | 15,062ms | 7.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:25:05.5971864+00:00 | 1 | capacity | failed | 15,053ms | 8.0 MiB / 5.4 MiB |
| Dekaf | 2026-07-17T02:25:32.595135+00:00 | 2 | capacity | failed | 15,042ms | 7.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-17T02:25:50.7336627+00:00 | 1 | capacity | failed | 15,053ms | 8.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:26:17.7285613+00:00 | 2 | capacity | failed | 15,046ms | 7.0 MiB / 2.4 MiB |
| Dekaf | 2026-07-17T02:26:35.8772143+00:00 | 1 | capacity | failed | 15,057ms | 8.0 MiB / 5.6 MiB |
| Dekaf | 2026-07-17T02:27:02.871974+00:00 | 2 | capacity | failed | 15,042ms | 7.0 MiB / 3.0 MiB |
| Dekaf | 2026-07-17T02:27:21.0793612+00:00 | 1 | capacity | succeeded | 15,051ms | 9.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-17T02:27:48.0556258+00:00 | 2 | capacity | succeeded | 15,043ms | 6.0 MiB / 4.1 MiB |
| Dekaf | 2026-07-17T02:27:51.0886684+00:00 | 2 | capacity | started | 0ms | 5.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:28:06.2119652+00:00 | 1 | capacity | succeeded | 15,039ms | 7.0 MiB / 5.7 MiB |
| Dekaf | 2026-07-17T02:28:24.259044+00:00 | 1 | capacity | failed | 15,042ms | 7.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:28:51.2788102+00:00 | 2 | capacity | succeeded | 15,057ms | 7.0 MiB / 1.8 MiB |
| Dekaf | 2026-07-17T02:29:09.3798882+00:00 | 1 | capacity | failed | 15,038ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:29:36.4035386+00:00 | 2 | capacity | succeeded | 15,036ms | 8.0 MiB / 6.4 MiB |
| Dekaf | 2026-07-17T02:29:54.5490801+00:00 | 1 | capacity | succeeded | 15,034ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:30:06.5190568+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 4.4 MiB |
| Dekaf | 2026-07-17T02:30:33.6516716+00:00 | 3 | capacity | started | 0ms | 9.0 MiB / 4.9 MiB |
| Dekaf | 2026-07-17T02:30:51.6809416+00:00 | 2 | capacity | started | 0ms | 9.0 MiB / 3.6 MiB |
| Dekaf | 2026-07-17T02:31:18.8021191+00:00 | 3 | capacity | started | 0ms | 7.0 MiB / 8.1 MiB |
| Dekaf | 2026-07-17T02:31:36.8207659+00:00 | 2 | capacity | started | 0ms | 10.0 MiB / 0.9 MiB |
| Dekaf | 2026-07-17T02:31:51.8623224+00:00 | 2 | capacity | succeeded | 15,040ms | 10.0 MiB / 3.8 MiB |
| Dekaf | 2026-07-17T02:32:13.0015917+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:32:28.0564044+00:00 | 1 | capacity | failed | 15,054ms | 7.0 MiB / 5.0 MiB |
| Dekaf | 2026-07-17T02:32:58.1489017+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf | 2026-07-17T02:33:13.2091252+00:00 | 1 | capacity | failed | 15,060ms | 7.0 MiB / 5.1 MiB |
| Dekaf | 2026-07-17T02:33:43.3033961+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:34:29.6533436+00:00 | 2 | capacity | started | 0ms | 42.0 MiB / 35.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:34:44.7138026+00:00 | 3 | capacity | failed | 15,061ms | 48.0 MiB / 38.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:35:14.8206937+00:00 | 3 | capacity | started | 0ms | 42.0 MiB / 18.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:35:29.8894148+00:00 | 3 | capacity | failed | 15,068ms | 48.0 MiB / 31.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:35:50.8479902+00:00 | 1 | capacity | started | 0ms | 30.0 MiB / 31.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:05.9011691+00:00 | 1 | capacity | succeeded | 15,053ms | 30.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:15.1288779+00:00 | 3 | capacity | succeeded | 15,120ms | 42.0 MiB / 4.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:18.1408523+00:00 | 3 | capacity | started | 0ms | 36.0 MiB / 21.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:33.1524615+00:00 | 2 | capacity | failed | 15,107ms | 42.0 MiB / 32.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:36:45.1109313+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 16.8 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:03.3171012+00:00 | 3 | capacity | started | 0ms | 36.0 MiB / 18.8 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:21.3558174+00:00 | 2 | capacity | started | 0ms | 30.0 MiB / 6.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:36.4058757+00:00 | 2 | capacity | succeeded | 15,050ms | 30.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:39.4402271+00:00 | 3 | capacity | started | 0ms | 24.0 MiB / 2.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:54.4609989+00:00 | 2 | capacity | succeeded | 15,041ms | 24.0 MiB / 15.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:37:57.5015872+00:00 | 3 | capacity | started | 0ms | 21.0 MiB / 3.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:38:12.5449442+00:00 | 2 | capacity | failed | 15,070ms | 24.0 MiB / 23.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:38:21.4674415+00:00 | 1 | capacity | failed | 15,057ms | 15.0 MiB / 6.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:38:51.6276857+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 5.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:00.8009675+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 6.8 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:15.8014357+00:00 | 2 | capacity | succeeded | 15,064ms | 18.0 MiB / 2.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:18.8446826+00:00 | 3 | capacity | started | 0ms | 15.0 MiB / 7.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:39:36.7839368+00:00 | 1 | capacity | started | 0ms | 12.0 MiB / 12.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:40:04.0162734+00:00 | 3 | capacity | started | 0ms | 21.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:40:21.9686263+00:00 | 1 | capacity | started | 0ms | 18.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:40:49.2229369+00:00 | 3 | capacity | started | 0ms | 24.0 MiB / 1.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:07.1896051+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 13.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:34.3704375+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 17.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:41:52.3235739+00:00 | 1 | capacity | started | 0ms | 15.0 MiB / 17.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:07.4102989+00:00 | 2 | capacity | failed | 15,054ms | 15.0 MiB / 4.8 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:37.453036+00:00 | 1 | capacity | started | 0ms | 21.0 MiB / 5.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:52.5006569+00:00 | 1 | capacity | failed | 15,047ms | 18.0 MiB / 10.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:42:52.679086+00:00 | 3 | capacity | failed | 15,039ms | 18.0 MiB / 3.3 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:22.8071254+00:00 | 3 | capacity | started | 0ms | 21.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:37.8615688+00:00 | 3 | capacity | succeeded | 15,054ms | 21.0 MiB / 5.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:43:55.7524369+00:00 | 1 | capacity | failed | 15,043ms | 15.0 MiB / 11.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:44:23.1003682+00:00 | 3 | capacity | failed | 15,130ms | 21.0 MiB / 5.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:44:40.9477567+00:00 | 1 | capacity | succeeded | 15,065ms | 12.0 MiB / 4.2 MiB |
| Dekaf (3conn) | 2026-07-17T02:44:53.2195345+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 16.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:45:11.1283873+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 3.9 MiB |
| Dekaf (3conn) | 2026-07-17T02:45:38.3717058+00:00 | 3 | capacity | started | 0ms | 24.0 MiB / 4.4 MiB |
| Dekaf (3conn) | 2026-07-17T02:45:56.3108706+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 11.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:23.5728964+00:00 | 3 | capacity | started | 0ms | 18.0 MiB / 15.7 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:41.5186911+00:00 | 2 | capacity | started | 0ms | 18.0 MiB / 4.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:46:59.6083125+00:00 | 1 | capacity | started | 0ms | 9.0 MiB / 11.0 MiB |
| Dekaf (3conn) | 2026-07-17T02:47:17.6875495+00:00 | 1 | capacity | started | 0ms | 6.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:47:32.7358571+00:00 | 1 | capacity | succeeded | 15,048ms | 6.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:47:50.8017152+00:00 | 1 | capacity | failed | 15,058ms | 6.0 MiB / 4.6 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:11.9159869+00:00 | 2 | capacity | started | 0ms | 18.0 MiB / 3.5 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:35.9698826+00:00 | 1 | capacity | failed | 15,043ms | 6.0 MiB / 8.1 MiB |
| Dekaf (3conn) | 2026-07-17T02:48:57.1202181+00:00 | 2 | capacity | started | 0ms | 12.0 MiB / 14.1 MiB |
*190 probe event(s) omitted; rows sampled across the full timeline.*

## Producer Admission Block Durations - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Broker | Duration bucket | Episodes |
|--------|-------:|-----------------|---------:|
| Dekaf | 1 | 0.001–0.002ms | 125 |
| Dekaf | 1 | 0.002–0.004ms | 107 |
| Dekaf | 1 | 0.004–0.008ms | 442 |
| Dekaf | 1 | 0.008–0.016ms | 1,460 |
| Dekaf | 1 | 0.016–0.032ms | 3,404 |
| Dekaf | 1 | 0.032–0.064ms | 5,324 |
| Dekaf | 1 | 0.064–0.128ms | 7,087 |
| Dekaf | 1 | 0.128–0.256ms | 12,621 |
| Dekaf | 1 | 0.256–0.512ms | 24,444 |
| Dekaf | 1 | 0.512–1.024ms | 34,246 |
| Dekaf | 1 | 1.024–2.048ms | 25,223 |
| Dekaf | 1 | 2.048–4.096ms | 10,703 |
| Dekaf | 1 | 4.096–8.192ms | 3,670 |
| Dekaf | 1 | 8.192–16.384ms | 938 |
| Dekaf | 1 | 16.384–32.768ms | 315 |
| Dekaf | 1 | 32.768–65.536ms | 20 |
| Dekaf | 2 | 0.001–0.002ms | 44 |
| Dekaf | 2 | 0.002–0.004ms | 55 |
| Dekaf | 2 | 0.004–0.008ms | 166 |
| Dekaf | 2 | 0.008–0.016ms | 511 |
| Dekaf | 2 | 0.016–0.032ms | 1,102 |
| Dekaf | 2 | 0.032–0.064ms | 1,529 |
| Dekaf | 2 | 0.064–0.128ms | 2,160 |
| Dekaf | 2 | 0.128–0.256ms | 3,539 |
| Dekaf | 2 | 0.256–0.512ms | 6,852 |
| Dekaf | 2 | 0.512–1.024ms | 9,245 |
| Dekaf | 2 | 1.024–2.048ms | 7,358 |
| Dekaf | 2 | 2.048–4.096ms | 3,308 |
| Dekaf | 2 | 4.096–8.192ms | 1,199 |
| Dekaf | 2 | 8.192–16.384ms | 407 |
| Dekaf | 2 | 16.384–32.768ms | 142 |
| Dekaf | 2 | 32.768–65.536ms | 6 |
| Dekaf | 3 | 0.001–0.002ms | 31 |
| Dekaf | 3 | 0.002–0.004ms | 61 |
| Dekaf | 3 | 0.004–0.008ms | 152 |
| Dekaf | 3 | 0.008–0.016ms | 486 |
| Dekaf | 3 | 0.016–0.032ms | 1,138 |
| Dekaf | 3 | 0.032–0.064ms | 1,444 |
| Dekaf | 3 | 0.064–0.128ms | 2,007 |
| Dekaf | 3 | 0.128–0.256ms | 3,446 |
| Dekaf | 3 | 0.256–0.512ms | 6,460 |
| Dekaf | 3 | 0.512–1.024ms | 8,958 |
| Dekaf | 3 | 1.024–2.048ms | 7,119 |
| Dekaf | 3 | 2.048–4.096ms | 3,366 |
| Dekaf | 3 | 4.096–8.192ms | 1,306 |
| Dekaf | 3 | 8.192–16.384ms | 541 |
| Dekaf | 3 | 16.384–32.768ms | 198 |
| Dekaf | 3 | 32.768–65.536ms | 11 |
| Dekaf (3conn) | 1 | 0.001–0.002ms | 44 |
| Dekaf (3conn) | 1 | 0.002–0.004ms | 40 |
| Dekaf (3conn) | 1 | 0.004–0.008ms | 148 |
| Dekaf (3conn) | 1 | 0.008–0.016ms | 516 |
| Dekaf (3conn) | 1 | 0.016–0.032ms | 1,613 |
| Dekaf (3conn) | 1 | 0.032–0.064ms | 2,157 |
| Dekaf (3conn) | 1 | 0.064–0.128ms | 2,701 |
| Dekaf (3conn) | 1 | 0.128–0.256ms | 3,824 |
| Dekaf (3conn) | 1 | 0.256–0.512ms | 6,768 |
| Dekaf (3conn) | 1 | 0.512–1.024ms | 10,319 |
| Dekaf (3conn) | 1 | 1.024–2.048ms | 12,059 |
| Dekaf (3conn) | 1 | 2.048–4.096ms | 9,829 |
| Dekaf (3conn) | 1 | 4.096–8.192ms | 4,042 |
| Dekaf (3conn) | 1 | 8.192–16.384ms | 1,156 |
| Dekaf (3conn) | 1 | 16.384–32.768ms | 437 |
| Dekaf (3conn) | 1 | 32.768–65.536ms | 48 |
| Dekaf (3conn) | 1 | 65.536–131.072ms | 1 |
| Dekaf (3conn) | 2 | 0.001–0.002ms | 18 |
| Dekaf (3conn) | 2 | 0.002–0.004ms | 16 |
| Dekaf (3conn) | 2 | 0.004–0.008ms | 46 |
| Dekaf (3conn) | 2 | 0.008–0.016ms | 158 |
| Dekaf (3conn) | 2 | 0.016–0.032ms | 496 |
| Dekaf (3conn) | 2 | 0.032–0.064ms | 744 |
| Dekaf (3conn) | 2 | 0.064–0.128ms | 899 |
| Dekaf (3conn) | 2 | 0.128–0.256ms | 1,304 |
| Dekaf (3conn) | 2 | 0.256–0.512ms | 2,143 |
| Dekaf (3conn) | 2 | 0.512–1.024ms | 3,477 |
| Dekaf (3conn) | 2 | 1.024–2.048ms | 4,046 |
| Dekaf (3conn) | 2 | 2.048–4.096ms | 3,455 |
| Dekaf (3conn) | 2 | 4.096–8.192ms | 1,576 |
| Dekaf (3conn) | 2 | 8.192–16.384ms | 545 |
| Dekaf (3conn) | 2 | 16.384–32.768ms | 200 |
| Dekaf (3conn) | 2 | 32.768–65.536ms | 24 |
| Dekaf (3conn) | 2 | 65.536–131.072ms | 2 |
| Dekaf (3conn) | 3 | 0.001–0.002ms | 2 |
| Dekaf (3conn) | 3 | 0.002–0.004ms | 4 |
| Dekaf (3conn) | 3 | 0.004–0.008ms | 34 |
| Dekaf (3conn) | 3 | 0.008–0.016ms | 74 |
| Dekaf (3conn) | 3 | 0.016–0.032ms | 240 |
| Dekaf (3conn) | 3 | 0.032–0.064ms | 309 |
| Dekaf (3conn) | 3 | 0.064–0.128ms | 439 |
| Dekaf (3conn) | 3 | 0.128–0.256ms | 662 |
| Dekaf (3conn) | 3 | 0.256–0.512ms | 1,093 |
| Dekaf (3conn) | 3 | 0.512–1.024ms | 1,735 |
| Dekaf (3conn) | 3 | 1.024–2.048ms | 2,054 |
| Dekaf (3conn) | 3 | 2.048–4.096ms | 1,621 |
| Dekaf (3conn) | 3 | 4.096–8.192ms | 661 |
| Dekaf (3conn) | 3 | 8.192–16.384ms | 193 |
| Dekaf (3conn) | 3 | 16.384–32.768ms | 60 |
| Dekaf (3conn) | 3 | 32.768–65.536ms | 5 |

## Delivery Latency Outliers - Producer (Fire-and-Forget, Idempotent), 3 Brokers

| Client | Message | Started UTC | Latency | Correlated signal | Probe windows in stall | Scale events in stall | Throughput interval | GC interval delta |
|--------|--------:|-------------|--------:|------------------|------------------------|-----------------------|---------------------|-------------------|
| Confluent | 192,000 | 2026-07-17T02:03:46.7005573+00:00 | 102.1ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 202,000 | 2026-07-17T02:03:46.7249254+00:00 | 106.9ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 212,000 | 2026-07-17T02:03:46.7476749+00:00 | 112.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 216,000 | 2026-07-17T02:03:46.7552853+00:00 | 105.4ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 217,000 | 2026-07-17T02:03:46.7561354+00:00 | 104.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 218,000 | 2026-07-17T02:03:46.7589012+00:00 | 102.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 219,000 | 2026-07-17T02:03:46.7600162+00:00 | 100.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 222,000 | 2026-07-17T02:03:46.7666125+00:00 | 142.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 225,000 | 2026-07-17T02:03:46.7796405+00:00 | 126.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 226,000 | 2026-07-17T02:03:46.7810415+00:00 | 125.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 227,000 | 2026-07-17T02:03:46.7916003+00:00 | 129.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 228,000 | 2026-07-17T02:03:46.7923728+00:00 | 128.2ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 229,000 | 2026-07-17T02:03:46.7937635+00:00 | 128.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 231,000 | 2026-07-17T02:03:46.8024095+00:00 | 118.3ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 232,000 | 2026-07-17T02:03:46.8034618+00:00 | 117.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 233,000 | 2026-07-17T02:03:46.8048213+00:00 | 115.3ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 234,000 | 2026-07-17T02:03:46.8110815+00:00 | 109.2ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 235,000 | 2026-07-17T02:03:46.8126934+00:00 | 139.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 236,000 | 2026-07-17T02:03:46.8138322+00:00 | 138.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 237,000 | 2026-07-17T02:03:46.8188162+00:00 | 127.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 238,000 | 2026-07-17T02:03:46.8194983+00:00 | 127.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 239,000 | 2026-07-17T02:03:46.8208925+00:00 | 131.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 240,000 | 2026-07-17T02:03:46.8220653+00:00 | 100.3ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 241,000 | 2026-07-17T02:03:46.8253296+00:00 | 136.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 242,000 | 2026-07-17T02:03:46.8272785+00:00 | 140.1ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 244,000 | 2026-07-17T02:03:46.8333244+00:00 | 113.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 245,000 | 2026-07-17T02:03:46.8339961+00:00 | 139.2ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 246,000 | 2026-07-17T02:03:46.835086+00:00 | 168.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 247,000 | 2026-07-17T02:03:46.8403525+00:00 | 133.2ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 248,000 | 2026-07-17T02:03:46.8410375+00:00 | 132.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 249,000 | 2026-07-17T02:03:46.842023+00:00 | 161.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 250,000 | 2026-07-17T02:03:46.8464103+00:00 | 118.4ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 251,000 | 2026-07-17T02:03:46.850313+00:00 | 136.1ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 252,000 | 2026-07-17T02:03:46.8515722+00:00 | 146.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 253,000 | 2026-07-17T02:03:46.8549757+00:00 | 110.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 254,000 | 2026-07-17T02:03:46.8560449+00:00 | 127.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 255,000 | 2026-07-17T02:03:46.8614726+00:00 | 163.6ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 256,000 | 2026-07-17T02:03:46.8622067+00:00 | 162.9ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 257,000 | 2026-07-17T02:03:46.8629397+00:00 | 140.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 258,000 | 2026-07-17T02:03:46.8677739+00:00 | 135.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 259,000 | 2026-07-17T02:03:46.8690659+00:00 | 177.2ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 260,000 | 2026-07-17T02:03:46.8702011+00:00 | 112.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 261,000 | 2026-07-17T02:03:46.8715431+00:00 | 132.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 262,000 | 2026-07-17T02:03:46.8758953+00:00 | 139.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 263,000 | 2026-07-17T02:03:46.8799381+00:00 | 113.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 264,000 | 2026-07-17T02:03:46.8806371+00:00 | 125.1ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 265,000 | 2026-07-17T02:03:46.8826086+00:00 | 275.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 266,000 | 2026-07-17T02:03:46.8839581+00:00 | 274.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 267,000 | 2026-07-17T02:03:46.8909724+00:00 | 125.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 268,000 | 2026-07-17T02:03:46.8919076+00:00 | 131.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 269,000 | 2026-07-17T02:03:46.8932535+00:00 | 264.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 270,000 | 2026-07-17T02:03:46.8994614+00:00 | 109.9ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 271,000 | 2026-07-17T02:03:46.9005082+00:00 | 123.3ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 272,000 | 2026-07-17T02:03:46.9018388+00:00 | 256.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 273,000 | 2026-07-17T02:03:46.9031797+00:00 | 116.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 274,000 | 2026-07-17T02:03:46.9073888+00:00 | 114.7ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 275,000 | 2026-07-17T02:03:46.9117879+00:00 | 247.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 276,000 | 2026-07-17T02:03:46.9124597+00:00 | 247.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 277,000 | 2026-07-17T02:03:46.9136462+00:00 | 132.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 278,000 | 2026-07-17T02:03:46.9156539+00:00 | 130.5ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 279,000 | 2026-07-17T02:03:46.9210091+00:00 | 239.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 280,000 | 2026-07-17T02:03:46.9217685+00:00 | 120.8ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 281,000 | 2026-07-17T02:03:46.9265929+00:00 | 232.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 282,000 | 2026-07-17T02:03:46.9273304+00:00 | 242.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 283,000 | 2026-07-17T02:03:46.9316857+00:00 | 111.0ms | GC pause | - | - | 1.0s / 329,534 msg/s | Gen2 +0 / pause +49.8ms |
| Confluent | 284,000 | 2026-07-17T02:03:46.9323522+00:00 | 227.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 285,000 | 2026-07-17T02:03:46.9331567+00:00 | 235.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 286,000 | 2026-07-17T02:03:46.9345107+00:00 | 233.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 287,000 | 2026-07-17T02:03:46.9390263+00:00 | 221.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 288,000 | 2026-07-17T02:03:46.9403754+00:00 | 220.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 289,000 | 2026-07-17T02:03:46.9416945+00:00 | 250.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 290,000 | 2026-07-17T02:03:46.9454511+00:00 | 212.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 291,000 | 2026-07-17T02:03:46.9467597+00:00 | 221.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 292,000 | 2026-07-17T02:03:46.9480411+00:00 | 273.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 293,000 | 2026-07-17T02:03:46.9487034+00:00 | 210.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 294,000 | 2026-07-17T02:03:46.9495914+00:00 | 248.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 295,000 | 2026-07-17T02:03:46.950949+00:00 | 254.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 296,000 | 2026-07-17T02:03:46.9551268+00:00 | 250.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 297,000 | 2026-07-17T02:03:46.955815+00:00 | 240.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 298,000 | 2026-07-17T02:03:46.9603866+00:00 | 235.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 299,000 | 2026-07-17T02:03:46.9617089+00:00 | 259.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 300,000 | 2026-07-17T02:03:46.9674936+00:00 | 193.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 301,000 | 2026-07-17T02:03:46.9682067+00:00 | 229.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 302,000 | 2026-07-17T02:03:46.9689085+00:00 | 289.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 303,000 | 2026-07-17T02:03:46.9728291+00:00 | 219.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 304,000 | 2026-07-17T02:03:46.9765888+00:00 | 245.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 305,000 | 2026-07-17T02:03:46.9772582+00:00 | 254.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 306,000 | 2026-07-17T02:03:46.9779608+00:00 | 253.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 307,000 | 2026-07-17T02:03:46.9833993+00:00 | 226.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 308,000 | 2026-07-17T02:03:46.9840935+00:00 | 239.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 309,000 | 2026-07-17T02:03:46.9847939+00:00 | 247.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 310,000 | 2026-07-17T02:03:46.9855224+00:00 | 214.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 311,000 | 2026-07-17T02:03:46.9927592+00:00 | 230.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 312,000 | 2026-07-17T02:03:46.9936292+00:00 | 292.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 313,000 | 2026-07-17T02:03:46.9942724+00:00 | 206.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 314,000 | 2026-07-17T02:03:46.9949613+00:00 | 280.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 315,000 | 2026-07-17T02:03:46.9993541+00:00 | 243.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 316,000 | 2026-07-17T02:03:47.0003339+00:00 | 242.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 317,000 | 2026-07-17T02:03:47.0039361+00:00 | 229.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 318,000 | 2026-07-17T02:03:47.0088717+00:00 | 224.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 319,000 | 2026-07-17T02:03:47.0100738+00:00 | 249.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 320,000 | 2026-07-17T02:03:47.0107574+00:00 | 219.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 321,000 | 2026-07-17T02:03:47.014844+00:00 | 226.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 322,000 | 2026-07-17T02:03:47.0165349+00:00 | 286.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 323,000 | 2026-07-17T02:03:47.0201361+00:00 | 211.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 324,000 | 2026-07-17T02:03:47.0209326+00:00 | 271.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 325,000 | 2026-07-17T02:03:47.0226071+00:00 | 295.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 326,000 | 2026-07-17T02:03:47.0242909+00:00 | 294.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 327,000 | 2026-07-17T02:03:47.025878+00:00 | 225.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 328,000 | 2026-07-17T02:03:47.0285499+00:00 | 223.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 329,000 | 2026-07-17T02:03:47.0298035+00:00 | 295.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 330,000 | 2026-07-17T02:03:47.0319498+00:00 | 209.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 331,000 | 2026-07-17T02:03:47.034704+00:00 | 235.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 332,000 | 2026-07-17T02:03:47.037044+00:00 | 281.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 333,000 | 2026-07-17T02:03:47.0431231+00:00 | 206.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 334,000 | 2026-07-17T02:03:47.0438143+00:00 | 264.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 335,000 | 2026-07-17T02:03:47.0471013+00:00 | 306.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 336,000 | 2026-07-17T02:03:47.04781+00:00 | 306.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 337,000 | 2026-07-17T02:03:47.0508348+00:00 | 261.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 338,000 | 2026-07-17T02:03:47.0518263+00:00 | 260.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 339,000 | 2026-07-17T02:03:47.0574133+00:00 | 296.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 340,000 | 2026-07-17T02:03:47.0581646+00:00 | 202.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 341,000 | 2026-07-17T02:03:47.0611041+00:00 | 251.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 342,000 | 2026-07-17T02:03:47.0627646+00:00 | 323.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +86.9ms |
| Confluent | 344,000 | 2026-07-17T02:03:47.1624407+00:00 | 182.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 345,000 | 2026-07-17T02:03:47.1631592+00:00 | 212.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 346,000 | 2026-07-17T02:03:47.1638998+00:00 | 211.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 347,000 | 2026-07-17T02:03:47.1692803+00:00 | 161.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 348,000 | 2026-07-17T02:03:47.1702474+00:00 | 183.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 349,000 | 2026-07-17T02:03:47.1710794+00:00 | 237.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 350,000 | 2026-07-17T02:03:47.1719058+00:00 | 149.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 351,000 | 2026-07-17T02:03:47.1733412+00:00 | 180.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 352,000 | 2026-07-17T02:03:47.1739785+00:00 | 247.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 353,000 | 2026-07-17T02:03:47.1766171+00:00 | 144.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 354,000 | 2026-07-17T02:03:47.1772467+00:00 | 253.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 355,000 | 2026-07-17T02:03:47.1798546+00:00 | 251.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 356,000 | 2026-07-17T02:03:47.1804979+00:00 | 251.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 357,000 | 2026-07-17T02:03:47.1810761+00:00 | 181.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 358,000 | 2026-07-17T02:03:47.1816897+00:00 | 180.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 359,000 | 2026-07-17T02:03:47.1856632+00:00 | 261.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 360,000 | 2026-07-17T02:03:47.1863247+00:00 | 160.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 361,000 | 2026-07-17T02:03:47.1870251+00:00 | 185.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 362,000 | 2026-07-17T02:03:47.1904599+00:00 | 275.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 363,000 | 2026-07-17T02:03:47.1929169+00:00 | 162.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 364,000 | 2026-07-17T02:03:47.1961336+00:00 | 253.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 365,000 | 2026-07-17T02:03:47.1968252+00:00 | 261.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 366,000 | 2026-07-17T02:03:47.1984574+00:00 | 259.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 367,000 | 2026-07-17T02:03:47.1998199+00:00 | 185.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 368,000 | 2026-07-17T02:03:47.2011368+00:00 | 184.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 369,000 | 2026-07-17T02:03:47.2017903+00:00 | 256.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 370,000 | 2026-07-17T02:03:47.2060269+00:00 | 160.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 371,000 | 2026-07-17T02:03:47.2070263+00:00 | 207.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 372,000 | 2026-07-17T02:03:47.2102334+00:00 | 346.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 373,000 | 2026-07-17T02:03:47.2111401+00:00 | 165.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 374,000 | 2026-07-17T02:03:47.2138362+00:00 | 336.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 375,000 | 2026-07-17T02:03:47.2187302+00:00 | 252.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 376,000 | 2026-07-17T02:03:47.2194258+00:00 | 252.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 377,000 | 2026-07-17T02:03:47.2204154+00:00 | 209.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 378,000 | 2026-07-17T02:03:47.2224098+00:00 | 207.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 379,000 | 2026-07-17T02:03:47.2243603+00:00 | 254.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 380,000 | 2026-07-17T02:03:47.2266537+00:00 | 183.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 381,000 | 2026-07-17T02:03:47.2273706+00:00 | 202.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 382,000 | 2026-07-17T02:03:47.2285783+00:00 | 366.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 383,000 | 2026-07-17T02:03:47.2326804+00:00 | 177.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 384,000 | 2026-07-17T02:03:47.2341993+00:00 | 373.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 385,000 | 2026-07-17T02:03:47.2348428+00:00 | 257.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 386,000 | 2026-07-17T02:03:47.2354997+00:00 | 256.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 387,000 | 2026-07-17T02:03:47.238514+00:00 | 206.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 388,000 | 2026-07-17T02:03:47.2398551+00:00 | 205.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 389,000 | 2026-07-17T02:03:47.2412292+00:00 | 265.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 390,000 | 2026-07-17T02:03:47.2459532+00:00 | 183.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 391,000 | 2026-07-17T02:03:47.248006+00:00 | 237.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 392,000 | 2026-07-17T02:03:47.2487457+00:00 | 391.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 393,000 | 2026-07-17T02:03:47.2513452+00:00 | 227.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 394,000 | 2026-07-17T02:03:47.2523616+00:00 | 387.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 395,000 | 2026-07-17T02:03:47.2553416+00:00 | 258.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 396,000 | 2026-07-17T02:03:47.25595+00:00 | 258.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 397,000 | 2026-07-17T02:03:47.2565961+00:00 | 239.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 398,000 | 2026-07-17T02:03:47.2586821+00:00 | 237.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 399,000 | 2026-07-17T02:03:47.2602689+00:00 | 254.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 400,000 | 2026-07-17T02:03:47.2629864+00:00 | 227.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 401,000 | 2026-07-17T02:03:47.263928+00:00 | 250.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 402,000 | 2026-07-17T02:03:47.2648708+00:00 | 400.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 403,000 | 2026-07-17T02:03:47.265521+00:00 | 241.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 404,000 | 2026-07-17T02:03:47.2660889+00:00 | 399.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 405,000 | 2026-07-17T02:03:47.2665824+00:00 | 254.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 406,000 | 2026-07-17T02:03:47.2671503+00:00 | 253.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 407,000 | 2026-07-17T02:03:47.2698235+00:00 | 254.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 408,000 | 2026-07-17T02:03:47.2702582+00:00 | 253.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 409,000 | 2026-07-17T02:03:47.2708227+00:00 | 262.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 410,000 | 2026-07-17T02:03:47.27131+00:00 | 248.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 411,000 | 2026-07-17T02:03:47.2720073+00:00 | 252.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 412,000 | 2026-07-17T02:03:47.2725085+00:00 | 405.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 413,000 | 2026-07-17T02:03:47.2730839+00:00 | 246.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 414,000 | 2026-07-17T02:03:47.2736596+00:00 | 414.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 415,000 | 2026-07-17T02:03:47.2743453+00:00 | 265.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 416,000 | 2026-07-17T02:03:47.2748667+00:00 | 265.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 417,000 | 2026-07-17T02:03:47.2763287+00:00 | 270.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 418,000 | 2026-07-17T02:03:47.2768821+00:00 | 269.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 419,000 | 2026-07-17T02:03:47.2773614+00:00 | 271.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 420,000 | 2026-07-17T02:03:47.2810834+00:00 | 249.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 421,000 | 2026-07-17T02:03:47.2821183+00:00 | 264.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 422,000 | 2026-07-17T02:03:47.2867031+00:00 | 403.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 423,000 | 2026-07-17T02:03:47.2873837+00:00 | 252.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 425,000 | 2026-07-17T02:03:47.2883298+00:00 | 266.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 426,000 | 2026-07-17T02:03:47.2887745+00:00 | 266.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 427,000 | 2026-07-17T02:03:47.2893409+00:00 | 268.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 428,000 | 2026-07-17T02:03:47.292856+00:00 | 264.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 429,000 | 2026-07-17T02:03:47.2937943+00:00 | 261.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 430,000 | 2026-07-17T02:03:47.2946606+00:00 | 254.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 431,000 | 2026-07-17T02:03:47.2951501+00:00 | 268.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 433,000 | 2026-07-17T02:03:47.3003532+00:00 | 261.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 435,000 | 2026-07-17T02:03:47.3059133+00:00 | 255.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 436,000 | 2026-07-17T02:03:47.3093554+00:00 | 258.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 437,000 | 2026-07-17T02:03:47.3106899+00:00 | 265.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 438,000 | 2026-07-17T02:03:47.3137026+00:00 | 262.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 439,000 | 2026-07-17T02:03:47.3149984+00:00 | 253.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 440,000 | 2026-07-17T02:03:47.3224595+00:00 | 248.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 441,000 | 2026-07-17T02:03:47.3238005+00:00 | 259.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 443,000 | 2026-07-17T02:03:47.330904+00:00 | 239.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 445,000 | 2026-07-17T02:03:47.3437102+00:00 | 229.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 446,000 | 2026-07-17T02:03:47.3453307+00:00 | 227.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 447,000 | 2026-07-17T02:03:47.3471725+00:00 | 245.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 448,000 | 2026-07-17T02:03:47.3484799+00:00 | 244.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 449,000 | 2026-07-17T02:03:47.3498096+00:00 | 233.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 450,000 | 2026-07-17T02:03:47.3550111+00:00 | 235.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 451,000 | 2026-07-17T02:03:47.3624943+00:00 | 230.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 453,000 | 2026-07-17T02:03:47.3651195+00:00 | 225.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 455,000 | 2026-07-17T02:03:47.3730958+00:00 | 216.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 456,000 | 2026-07-17T02:03:47.3743908+00:00 | 215.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 457,000 | 2026-07-17T02:03:47.3791876+00:00 | 229.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 458,000 | 2026-07-17T02:03:47.380823+00:00 | 235.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 459,000 | 2026-07-17T02:03:47.385564+00:00 | 204.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 460,000 | 2026-07-17T02:03:47.3868393+00:00 | 220.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 461,000 | 2026-07-17T02:03:47.3881439+00:00 | 228.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 463,000 | 2026-07-17T02:03:47.396886+00:00 | 214.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 465,000 | 2026-07-17T02:03:47.3995539+00:00 | 198.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 466,000 | 2026-07-17T02:03:47.4038581+00:00 | 212.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 467,000 | 2026-07-17T02:03:47.409107+00:00 | 230.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 468,000 | 2026-07-17T02:03:47.410446+00:00 | 229.1ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 469,000 | 2026-07-17T02:03:47.4118507+00:00 | 204.7ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 470,000 | 2026-07-17T02:03:47.4140343+00:00 | 209.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 471,000 | 2026-07-17T02:03:47.4177032+00:00 | 239.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 473,000 | 2026-07-17T02:03:47.4236647+00:00 | 200.4ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 475,000 | 2026-07-17T02:03:47.4294232+00:00 | 191.3ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 476,000 | 2026-07-17T02:03:47.4329838+00:00 | 187.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 477,000 | 2026-07-17T02:03:47.4349729+00:00 | 230.8ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 478,000 | 2026-07-17T02:03:47.4395773+00:00 | 226.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 479,000 | 2026-07-17T02:03:47.4441622+00:00 | 211.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 480,000 | 2026-07-17T02:03:47.4458297+00:00 | 215.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 481,000 | 2026-07-17T02:03:47.4471546+00:00 | 237.2ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 483,000 | 2026-07-17T02:03:47.4540096+00:00 | 207.5ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 485,000 | 2026-07-17T02:03:47.4716027+00:00 | 213.0ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 486,000 | 2026-07-17T02:03:47.4729642+00:00 | 211.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 489,000 | 2026-07-17T02:03:47.4898523+00:00 | 194.9ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Confluent | 490,000 | 2026-07-17T02:03:47.4929234+00:00 | 178.6ms | GC pause | - | - | 2.0s / 315,895 msg/s | Gen2 +1 / pause +37.1ms |
| Dekaf | 2,607,000 | 2026-07-17T02:18:48.8699596+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,085,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 2,617,000 | 2026-07-17T02:18:48.8784953+00:00 | 102.0ms | broker/backlog (no scale or GC event) | - | - | 3.0s / 1,085,381 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 8,087,000 | 2026-07-17T02:18:53.8643112+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 8.0s / 1,094,202 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 10,357,000 | 2026-07-17T02:18:55.9101379+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 10.0s / 1,098,942 msg/s | Gen2 +0 / pause +0.9ms |
| Dekaf | 18,097,000 | 2026-07-17T02:19:02.8737794+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,095,535 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,107,000 | 2026-07-17T02:19:02.8811396+00:00 | 106.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,095,535 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,117,000 | 2026-07-17T02:19:02.8903908+00:00 | 105.7ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,095,535 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,127,000 | 2026-07-17T02:19:02.8965692+00:00 | 107.4ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,095,535 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 18,147,000 | 2026-07-17T02:19:02.9175014+00:00 | 100.1ms | broker/backlog (no scale or GC event) | - | - | 17.0s / 1,095,535 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,781,000 | 2026-07-17T02:19:04.4162415+00:00 | 101.6ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,079,279 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 19,782,000 | 2026-07-17T02:19:04.4168549+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,079,279 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,317,000 | 2026-07-17T02:19:04.9006797+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,079,279 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 20,337,000 | 2026-07-17T02:19:04.9159228+00:00 | 104.1ms | broker/backlog (no scale or GC event) | - | - | 19.0s / 1,079,279 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 23,980,000 | 2026-07-17T02:19:08.3939283+00:00 | 100.9ms | broker/backlog (no scale or GC event) | - | - | 23.0s / 1,029,196 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,017,000 | 2026-07-17T02:19:09.3882029+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 970,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,037,000 | 2026-07-17T02:19:09.4043164+00:00 | 103.4ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 970,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,047,000 | 2026-07-17T02:19:09.4112664+00:00 | 105.8ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 970,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,057,000 | 2026-07-17T02:19:09.4179406+00:00 | 107.6ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 970,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 25,067,000 | 2026-07-17T02:19:09.4356381+00:00 | 106.0ms | broker/backlog (no scale or GC event) | - | - | 24.0s / 970,181 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,087,000 | 2026-07-17T02:19:12.3894098+00:00 | 117.7ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,002,410 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,097,000 | 2026-07-17T02:19:12.3958922+00:00 | 116.5ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,002,410 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,107,000 | 2026-07-17T02:19:12.4028549+00:00 | 122.9ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,002,410 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,117,000 | 2026-07-17T02:19:12.4126507+00:00 | 126.6ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,002,410 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,127,000 | 2026-07-17T02:19:12.4415934+00:00 | 104.3ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,002,410 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 28,597,000 | 2026-07-17T02:19:12.9117887+00:00 | 106.4ms | broker/backlog (no scale or GC event) | - | - | 27.0s / 1,002,410 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 29,127,000 | 2026-07-17T02:19:13.3911327+00:00 | 103.8ms | broker/backlog (no scale or GC event) | - | - | 28.0s / 1,096,362 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 30,737,000 | 2026-07-17T02:19:14.9018967+00:00 | 103.9ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 1,096,088 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 30,747,000 | 2026-07-17T02:19:14.9096848+00:00 | 101.0ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 1,096,088 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 30,757,000 | 2026-07-17T02:19:14.9178094+00:00 | 104.5ms | broker/backlog (no scale or GC event) | - | - | 29.0s / 1,096,088 msg/s | Gen2 +0 / pause +0.7ms |
| Dekaf | 41,737,000 | 2026-07-17T02:19:24.9017031+00:00 | 100.1ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 1,112,363 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 41,747,000 | 2026-07-17T02:19:24.9080204+00:00 | 100.7ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 39.0s / 1,112,363 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 42,837,000 | 2026-07-17T02:19:25.902534+00:00 | 101.3ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 40.0s / 1,076,059 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,317,000 | 2026-07-17T02:19:26.3552861+00:00 | 103.6ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,327,000 | 2026-07-17T02:19:26.3642031+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,337,000 | 2026-07-17T02:19:26.3719189+00:00 | 106.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,347,000 | 2026-07-17T02:19:26.3791248+00:00 | 109.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,357,000 | 2026-07-17T02:19:26.3894547+00:00 | 104.9ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,367,000 | 2026-07-17T02:19:26.3986414+00:00 | 106.2ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf | 43,377,000 | 2026-07-17T02:19:26.4076841+00:00 | 105.5ms | broker/backlog (no scale or GC event) | 2:capacity/succeeded, 3:capacity/succeeded, 1:capacity/succeeded | - | 41.0s / 1,078,480 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 36,000 | 2026-07-17T02:33:59.5418908+00:00 | 120.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 40,000 | 2026-07-17T02:33:59.5469039+00:00 | 144.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 44,000 | 2026-07-17T02:33:59.5520649+00:00 | 126.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 46,000 | 2026-07-17T02:33:59.5542486+00:00 | 124.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 50,000 | 2026-07-17T02:33:59.5585025+00:00 | 150.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 54,000 | 2026-07-17T02:33:59.5629756+00:00 | 131.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 55,000 | 2026-07-17T02:33:59.5643126+00:00 | 101.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 56,000 | 2026-07-17T02:33:59.5654951+00:00 | 151.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 60,000 | 2026-07-17T02:33:59.5705405+00:00 | 455.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 64,000 | 2026-07-17T02:33:59.5800572+00:00 | 149.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 66,000 | 2026-07-17T02:33:59.5821537+00:00 | 147.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 68,000 | 2026-07-17T02:33:59.5855336+00:00 | 100.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 70,000 | 2026-07-17T02:33:59.5883167+00:00 | 454.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 74,000 | 2026-07-17T02:33:59.5969712+00:00 | 149.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 76,000 | 2026-07-17T02:33:59.5991725+00:00 | 146.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 77,000 | 2026-07-17T02:33:59.6007057+00:00 | 328.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 80,000 | 2026-07-17T02:33:59.6076297+00:00 | 446.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 84,000 | 2026-07-17T02:33:59.6117911+00:00 | 154.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 86,000 | 2026-07-17T02:33:59.6143117+00:00 | 151.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 87,000 | 2026-07-17T02:33:59.6156583+00:00 | 328.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 90,000 | 2026-07-17T02:33:59.6187476+00:00 | 461.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 93,000 | 2026-07-17T02:33:59.6227591+00:00 | 102.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 94,000 | 2026-07-17T02:33:59.62388+00:00 | 158.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 96,000 | 2026-07-17T02:33:59.6265636+00:00 | 155.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 97,000 | 2026-07-17T02:33:59.6283218+00:00 | 351.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 99,000 | 2026-07-17T02:33:59.6311926+00:00 | 113.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 100,000 | 2026-07-17T02:33:59.633082+00:00 | 483.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 101,000 | 2026-07-17T02:33:59.6346722+00:00 | 100.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 103,000 | 2026-07-17T02:33:59.6372403+00:00 | 107.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 104,000 | 2026-07-17T02:33:59.6383493+00:00 | 162.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 106,000 | 2026-07-17T02:33:59.6409432+00:00 | 159.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 107,000 | 2026-07-17T02:33:59.6430153+00:00 | 350.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 109,000 | 2026-07-17T02:33:59.6455499+00:00 | 111.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 110,000 | 2026-07-17T02:33:59.6466186+00:00 | 477.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 111,000 | 2026-07-17T02:33:59.6478475+00:00 | 502.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 112,000 | 2026-07-17T02:33:59.6491371+00:00 | 500.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 113,000 | 2026-07-17T02:33:59.6505704+00:00 | 106.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 114,000 | 2026-07-17T02:33:59.6517359+00:00 | 164.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 116,000 | 2026-07-17T02:33:59.6544895+00:00 | 162.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 117,000 | 2026-07-17T02:33:59.6556115+00:00 | 345.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 119,000 | 2026-07-17T02:33:59.6586566+00:00 | 108.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 120,000 | 2026-07-17T02:33:59.6600595+00:00 | 585.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 121,000 | 2026-07-17T02:33:59.6617611+00:00 | 500.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 122,000 | 2026-07-17T02:33:59.6629092+00:00 | 499.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 123,000 | 2026-07-17T02:33:59.6639306+00:00 | 114.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 124,000 | 2026-07-17T02:33:59.6654873+00:00 | 159.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 126,000 | 2026-07-17T02:33:59.6681206+00:00 | 162.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 127,000 | 2026-07-17T02:33:59.6781591+00:00 | 334.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 129,000 | 2026-07-17T02:33:59.6805953+00:00 | 113.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 130,000 | 2026-07-17T02:33:59.6819344+00:00 | 603.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 131,000 | 2026-07-17T02:33:59.6834835+00:00 | 486.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 132,000 | 2026-07-17T02:33:59.6849532+00:00 | 485.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 133,000 | 2026-07-17T02:33:59.6896529+00:00 | 112.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 134,000 | 2026-07-17T02:33:59.6909487+00:00 | 153.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 136,000 | 2026-07-17T02:33:59.6965203+00:00 | 147.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 137,000 | 2026-07-17T02:33:59.7008747+00:00 | 321.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 139,000 | 2026-07-17T02:33:59.7063663+00:00 | 110.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 140,000 | 2026-07-17T02:33:59.7072359+00:00 | 596.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 141,000 | 2026-07-17T02:33:59.7081232+00:00 | 486.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 142,000 | 2026-07-17T02:33:59.709235+00:00 | 485.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 143,000 | 2026-07-17T02:33:59.7101601+00:00 | 106.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 144,000 | 2026-07-17T02:33:59.7114284+00:00 | 140.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 146,000 | 2026-07-17T02:33:59.715227+00:00 | 148.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 147,000 | 2026-07-17T02:33:59.7163606+00:00 | 337.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 149,000 | 2026-07-17T02:33:59.7190221+00:00 | 107.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 150,000 | 2026-07-17T02:33:59.7206838+00:00 | 597.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 151,000 | 2026-07-17T02:33:59.7222688+00:00 | 484.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 152,000 | 2026-07-17T02:33:59.7235682+00:00 | 483.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 153,000 | 2026-07-17T02:33:59.7246605+00:00 | 108.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 154,000 | 2026-07-17T02:33:59.7259053+00:00 | 143.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 156,000 | 2026-07-17T02:33:59.7284064+00:00 | 140.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 157,000 | 2026-07-17T02:33:59.7299682+00:00 | 345.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 159,000 | 2026-07-17T02:33:59.7321187+00:00 | 107.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 160,000 | 2026-07-17T02:33:59.7333224+00:00 | 621.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 161,000 | 2026-07-17T02:33:59.7344317+00:00 | 493.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 162,000 | 2026-07-17T02:33:59.7354462+00:00 | 492.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 163,000 | 2026-07-17T02:33:59.7371996+00:00 | 111.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 164,000 | 2026-07-17T02:33:59.7382996+00:00 | 140.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 166,000 | 2026-07-17T02:33:59.7409178+00:00 | 147.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 167,000 | 2026-07-17T02:33:59.7420113+00:00 | 343.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 169,000 | 2026-07-17T02:33:59.7453322+00:00 | 110.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 170,000 | 2026-07-17T02:33:59.7464608+00:00 | 688.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 171,000 | 2026-07-17T02:33:59.7479119+00:00 | 495.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 172,000 | 2026-07-17T02:33:59.7491971+00:00 | 494.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 173,000 | 2026-07-17T02:33:59.7502413+00:00 | 105.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 174,000 | 2026-07-17T02:33:59.7512827+00:00 | 150.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 176,000 | 2026-07-17T02:33:59.7542597+00:00 | 147.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 177,000 | 2026-07-17T02:33:59.7554221+00:00 | 359.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 179,000 | 2026-07-17T02:33:59.7583482+00:00 | 103.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 180,000 | 2026-07-17T02:33:59.7596361+00:00 | 699.6ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 181,000 | 2026-07-17T02:33:59.7612345+00:00 | 488.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 182,000 | 2026-07-17T02:33:59.7667382+00:00 | 513.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 183,000 | 2026-07-17T02:33:59.7678971+00:00 | 103.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 184,000 | 2026-07-17T02:33:59.7726689+00:00 | 148.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 186,000 | 2026-07-17T02:33:59.7754315+00:00 | 145.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 187,000 | 2026-07-17T02:33:59.7773126+00:00 | 355.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 189,000 | 2026-07-17T02:33:59.7793198+00:00 | 108.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 190,000 | 2026-07-17T02:33:59.7803904+00:00 | 706.0ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 191,000 | 2026-07-17T02:33:59.7815976+00:00 | 511.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 192,000 | 2026-07-17T02:33:59.7825651+00:00 | 510.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 193,000 | 2026-07-17T02:33:59.7840718+00:00 | 133.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 194,000 | 2026-07-17T02:33:59.7850875+00:00 | 143.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 196,000 | 2026-07-17T02:33:59.7876042+00:00 | 141.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 197,000 | 2026-07-17T02:33:59.7888907+00:00 | 381.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 199,000 | 2026-07-17T02:33:59.7912313+00:00 | 135.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 200,000 | 2026-07-17T02:33:59.7927243+00:00 | 700.6ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 201,000 | 2026-07-17T02:33:59.794079+00:00 | 513.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 202,000 | 2026-07-17T02:33:59.7953896+00:00 | 511.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 203,000 | 2026-07-17T02:33:59.7964057+00:00 | 130.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 204,000 | 2026-07-17T02:33:59.7976117+00:00 | 157.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 206,000 | 2026-07-17T02:33:59.8003339+00:00 | 155.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 207,000 | 2026-07-17T02:33:59.8014246+00:00 | 384.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 209,000 | 2026-07-17T02:33:59.8039481+00:00 | 130.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 210,000 | 2026-07-17T02:33:59.8053507+00:00 | 701.2ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 211,000 | 2026-07-17T02:33:59.8063348+00:00 | 509.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 212,000 | 2026-07-17T02:33:59.807685+00:00 | 514.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 213,000 | 2026-07-17T02:33:59.8085971+00:00 | 134.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 214,000 | 2026-07-17T02:33:59.8097398+00:00 | 150.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 216,000 | 2026-07-17T02:33:59.8121716+00:00 | 148.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 217,000 | 2026-07-17T02:33:59.8134014+00:00 | 376.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 219,000 | 2026-07-17T02:33:59.8158309+00:00 | 147.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 220,000 | 2026-07-17T02:33:59.8168451+00:00 | 703.2ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 221,000 | 2026-07-17T02:33:59.8182655+00:00 | 507.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 222,000 | 2026-07-17T02:33:59.8194186+00:00 | 506.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 223,000 | 2026-07-17T02:33:59.8204629+00:00 | 159.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 224,000 | 2026-07-17T02:33:59.8217392+00:00 | 154.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 226,000 | 2026-07-17T02:33:59.8235884+00:00 | 152.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 227,000 | 2026-07-17T02:33:59.8249883+00:00 | 375.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 229,000 | 2026-07-17T02:33:59.8272318+00:00 | 164.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 230,000 | 2026-07-17T02:33:59.8288668+00:00 | 706.9ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 231,000 | 2026-07-17T02:33:59.8298619+00:00 | 506.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 232,000 | 2026-07-17T02:33:59.8309455+00:00 | 505.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 233,000 | 2026-07-17T02:33:59.8323313+00:00 | 159.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 234,000 | 2026-07-17T02:33:59.8334212+00:00 | 148.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 236,000 | 2026-07-17T02:33:59.8359345+00:00 | 153.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 237,000 | 2026-07-17T02:33:59.8369169+00:00 | 371.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 239,000 | 2026-07-17T02:33:59.8390976+00:00 | 162.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 240,000 | 2026-07-17T02:33:59.8416376+00:00 | 701.4ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 241,000 | 2026-07-17T02:33:59.8426707+00:00 | 501.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 242,000 | 2026-07-17T02:33:59.8441999+00:00 | 504.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 243,000 | 2026-07-17T02:33:59.8466551+00:00 | 159.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 244,000 | 2026-07-17T02:33:59.8476413+00:00 | 149.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 246,000 | 2026-07-17T02:33:59.8522684+00:00 | 145.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 247,000 | 2026-07-17T02:33:59.8534394+00:00 | 378.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 249,000 | 2026-07-17T02:33:59.856165+00:00 | 157.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 250,000 | 2026-07-17T02:33:59.8641877+00:00 | 693.5ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 251,000 | 2026-07-17T02:33:59.8656186+00:00 | 487.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 252,000 | 2026-07-17T02:33:59.8667496+00:00 | 486.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 253,000 | 2026-07-17T02:33:59.869145+00:00 | 156.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 254,000 | 2026-07-17T02:33:59.8703118+00:00 | 133.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 255,000 | 2026-07-17T02:33:59.8719823+00:00 | 100.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 256,000 | 2026-07-17T02:33:59.8774518+00:00 | 134.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 257,000 | 2026-07-17T02:33:59.8785387+00:00 | 366.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 259,000 | 2026-07-17T02:33:59.8884533+00:00 | 144.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 260,000 | 2026-07-17T02:33:59.8898474+00:00 | 686.2ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 261,000 | 2026-07-17T02:33:59.8916451+00:00 | 477.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 262,000 | 2026-07-17T02:33:59.8932817+00:00 | 476.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 263,000 | 2026-07-17T02:33:59.9026801+00:00 | 130.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 264,000 | 2026-07-17T02:33:59.9041158+00:00 | 114.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 266,000 | 2026-07-17T02:33:59.9163248+00:00 | 102.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 267,000 | 2026-07-17T02:33:59.9193645+00:00 | 351.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 269,000 | 2026-07-17T02:33:59.9232849+00:00 | 118.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 270,000 | 2026-07-17T02:33:59.9246766+00:00 | 674.0ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 271,000 | 2026-07-17T02:33:59.9258879+00:00 | 463.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 272,000 | 2026-07-17T02:33:59.9307273+00:00 | 462.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 273,000 | 2026-07-17T02:33:59.9320833+00:00 | 117.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 277,000 | 2026-07-17T02:33:59.9426744+00:00 | 332.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 280,000 | 2026-07-17T02:33:59.9586809+00:00 | 647.0ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 281,000 | 2026-07-17T02:33:59.9606697+00:00 | 436.6ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 282,000 | 2026-07-17T02:33:59.9624685+00:00 | 434.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 283,000 | 2026-07-17T02:33:59.963626+00:00 | 108.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 287,000 | 2026-07-17T02:33:59.9741314+00:00 | 320.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 289,000 | 2026-07-17T02:33:59.9784959+00:00 | 105.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 290,000 | 2026-07-17T02:33:59.9800085+00:00 | 649.1ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 291,000 | 2026-07-17T02:33:59.9827474+00:00 | 424.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 292,000 | 2026-07-17T02:33:59.9841589+00:00 | 423.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 297,000 | 2026-07-17T02:33:59.9968858+00:00 | 316.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 300,000 | 2026-07-17T02:34:00.0048895+00:00 | 645.6ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 301,000 | 2026-07-17T02:34:00.0067733+00:00 | 403.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 302,000 | 2026-07-17T02:34:00.0080231+00:00 | 406.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 307,000 | 2026-07-17T02:34:00.0197393+00:00 | 322.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 310,000 | 2026-07-17T02:34:00.0272626+00:00 | 626.6ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 311,000 | 2026-07-17T02:34:00.0285748+00:00 | 388.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 312,000 | 2026-07-17T02:34:00.0308346+00:00 | 386.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 317,000 | 2026-07-17T02:34:00.0400319+00:00 | 319.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 320,000 | 2026-07-17T02:34:00.0449936+00:00 | 627.2ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 321,000 | 2026-07-17T02:34:00.0462918+00:00 | 377.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 322,000 | 2026-07-17T02:34:00.0476748+00:00 | 376.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 327,000 | 2026-07-17T02:34:00.056126+00:00 | 312.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 330,000 | 2026-07-17T02:34:00.0602637+00:00 | 633.8ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 331,000 | 2026-07-17T02:34:00.0613657+00:00 | 365.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 332,000 | 2026-07-17T02:34:00.062648+00:00 | 366.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 334,000 | 2026-07-17T02:34:00.0654639+00:00 | 169.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 336,000 | 2026-07-17T02:34:00.0684872+00:00 | 166.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 337,000 | 2026-07-17T02:34:00.0725786+00:00 | 315.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 341,000 | 2026-07-17T02:34:00.0891438+00:00 | 347.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 342,000 | 2026-07-17T02:34:00.0913356+00:00 | 345.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 344,000 | 2026-07-17T02:34:00.0960842+00:00 | 175.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 346,000 | 2026-07-17T02:34:00.1035734+00:00 | 168.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 347,000 | 2026-07-17T02:34:00.107008+00:00 | 294.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 351,000 | 2026-07-17T02:34:00.1191919+00:00 | 330.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 352,000 | 2026-07-17T02:34:00.1227232+00:00 | 327.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 354,000 | 2026-07-17T02:34:00.1270179+00:00 | 170.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 356,000 | 2026-07-17T02:34:00.1372472+00:00 | 160.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 357,000 | 2026-07-17T02:34:00.1383219+00:00 | 264.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 361,000 | 2026-07-17T02:34:00.1474585+00:00 | 304.5ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 362,000 | 2026-07-17T02:34:00.1488332+00:00 | 305.3ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 364,000 | 2026-07-17T02:34:00.1512875+00:00 | 158.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 366,000 | 2026-07-17T02:34:00.2253342+00:00 | 114.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 367,000 | 2026-07-17T02:34:00.2272344+00:00 | 184.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 371,000 | 2026-07-17T02:34:00.2350252+00:00 | 225.0ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 372,000 | 2026-07-17T02:34:00.2370516+00:00 | 222.4ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 374,000 | 2026-07-17T02:34:00.2402916+00:00 | 105.9ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 377,000 | 2026-07-17T02:34:00.2507705+00:00 | 165.1ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 381,000 | 2026-07-17T02:34:00.2766862+00:00 | 223.9ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 382,000 | 2026-07-17T02:34:00.2777396+00:00 | 222.8ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 384,000 | 2026-07-17T02:34:00.2815817+00:00 | 149.8ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 386,000 | 2026-07-17T02:34:00.2844193+00:00 | 157.2ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 387,000 | 2026-07-17T02:34:00.2857303+00:00 | 138.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 391,000 | 2026-07-17T02:34:00.2980536+00:00 | 229.4ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 392,000 | 2026-07-17T02:34:00.2997675+00:00 | 238.4ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 394,000 | 2026-07-17T02:34:00.3039697+00:00 | 148.4ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 396,000 | 2026-07-17T02:34:00.3064492+00:00 | 146.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 397,000 | 2026-07-17T02:34:00.309759+00:00 | 134.7ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 401,000 | 2026-07-17T02:34:00.3159081+00:00 | 226.9ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 402,000 | 2026-07-17T02:34:00.3172059+00:00 | 225.4ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 404,000 | 2026-07-17T02:34:00.3200129+00:00 | 141.3ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 406,000 | 2026-07-17T02:34:00.3235088+00:00 | 142.4ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 407,000 | 2026-07-17T02:34:00.3401114+00:00 | 110.0ms | GC pause | - | - | 1.0s / 429,036 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 411,000 | 2026-07-17T02:34:00.3456734+00:00 | 209.2ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 412,000 | 2026-07-17T02:34:00.3469551+00:00 | 207.9ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 414,000 | 2026-07-17T02:34:00.3495326+00:00 | 129.2ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 416,000 | 2026-07-17T02:34:00.3550554+00:00 | 123.7ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 417,000 | 2026-07-17T02:34:00.3562966+00:00 | 103.5ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 421,000 | 2026-07-17T02:34:00.4371827+00:00 | 126.9ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 422,000 | 2026-07-17T02:34:00.4388148+00:00 | 136.9ms | GC pause | - | - | 2.0s / 728,821 msg/s | Gen2 +4 / pause +12.6ms |
| Dekaf (3conn) | 431,000 | 2026-07-17T02:34:00.4589764+00:00 | 122.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 432,000 | 2026-07-17T02:34:00.4601407+00:00 | 121.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 441,000 | 2026-07-17T02:34:00.4703+00:00 | 125.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 442,000 | 2026-07-17T02:34:00.4710196+00:00 | 124.5ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 451,000 | 2026-07-17T02:34:00.4846561+00:00 | 118.8ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 452,000 | 2026-07-17T02:34:00.4859858+00:00 | 123.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 461,000 | 2026-07-17T02:34:00.4998512+00:00 | 114.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 462,000 | 2026-07-17T02:34:00.5007771+00:00 | 114.0ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 471,000 | 2026-07-17T02:34:00.5125971+00:00 | 116.3ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 472,000 | 2026-07-17T02:34:00.5134977+00:00 | 115.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 481,000 | 2026-07-17T02:34:00.5228362+00:00 | 119.9ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 482,000 | 2026-07-17T02:34:00.5245934+00:00 | 118.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 491,000 | 2026-07-17T02:34:00.5357453+00:00 | 121.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 492,000 | 2026-07-17T02:34:00.5371004+00:00 | 120.1ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 501,000 | 2026-07-17T02:34:00.5489795+00:00 | 130.2ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 502,000 | 2026-07-17T02:34:00.549718+00:00 | 129.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 511,000 | 2026-07-17T02:34:00.5596802+00:00 | 128.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 512,000 | 2026-07-17T02:34:00.5606845+00:00 | 127.7ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 521,000 | 2026-07-17T02:34:00.5703633+00:00 | 128.6ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |
| Dekaf (3conn) | 522,000 | 2026-07-17T02:34:00.5714786+00:00 | 127.4ms | broker/backlog (no scale or GC event) | - | - | 2.0s / 728,821 msg/s | Gen2 +0 / pause +0.0ms |

*Probe overlap is temporal correlation only. Compare no-probe outliers, admission-block durations, GC, and throughput before attributing a stall.*

*42,741 additional latency outlier sample(s) exceeded the bounded diagnostic capacity.*

:::tip
**Dekaf uses 1.57x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.29x.
:::

## Producer → Consumer Round-Trip Steady State Throughput (15 minutes, 128B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.99 | 4181.05 | 1,261,360 | 2,053,648 | +2.6% | +83.79% | 153.97 | 1,261,360 | 0 | 1.24 |
| Confluent | 1.86 | - | 126,617 | 1,506,909 | +26.5% | +200.60% | 15.46 | 126,617 | 0 | 0.24 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer → Consumer Round-Trip Steady State

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 4,663 | 477.22 | 808.37 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer → Consumer Round-Trip Steady State

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:06:20.1665159+00:00 | 1 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 875,298 msg/s |
| Dekaf | 2026-07-17T02:06:25.1419607+00:00 | 1 | 16.0 MiB / 6.4 MiB | 513.5 MB/s | 0/0 | 0 | 5.0s / 2,312,860 msg/s |
| Dekaf | 2026-07-17T02:06:26.1445581+00:00 | 1 | 16.0 MiB / 2.6 MiB | 513.5 MB/s | 0/0 | 0 | 6.0s / 2,053,648 msg/s |
| Dekaf | 2026-07-17T02:06:27.1446239+00:00 | 1 | 16.0 MiB / 3.3 MiB | 513.5 MB/s | 0/0 | 0 | 7.0s / 2,028,734 msg/s |
| Dekaf | 2026-07-17T02:06:28.1464029+00:00 | 1 | 16.0 MiB / 3.0 MiB | 513.5 MB/s | 0/0 | 0 | 8.0s / 1,891,142 msg/s |
| Dekaf | 2026-07-17T02:06:29.1489833+00:00 | 1 | 16.0 MiB / 3.0 MiB | 513.5 MB/s | 0/0 | 0 | 9.0s / 1,998,728 msg/s |

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.89x less CPU per message** than Confluent.Kafka for producer → consumer round-trip steady state; comparison throughput is 1.36x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 400.60 | 400.59 | 264 | 353 | -0.8% | -0.08% | 0.25 | 353 | 0 | 0.14 |
| Confluent | 263.66 | - | 128 | 172 | +2.8% | +0.26% | 0.12 | 171 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

## Producer Request Diagnostics - Producer (Transactional EOS), 3 Brokers

| Client | Broker | Requests | Requests/s | Avg bytes/request |
|--------|-------:|---------:|-----------:|------------------:|
| Dekaf | 1 | 105,757 | 117.49 | 1.20 KB |
| Dekaf | 2 | 105,720 | 117.45 | 1.20 KB |
| Dekaf | 3 | 105,829 | 117.57 | 1.20 KB |

*Average bytes/request is Kafka ProduceRequest body bytes; smaller requests at higher request rates indicate batching fragmentation.*

## Producer Budget Timeline - Producer (Transactional EOS), 3 Brokers

| Client | Sample UTC | Broker | Budget / unacked | Max rate | Probe success/failure | Admission blocks | Nearest throughput sample |
|--------|------------|-------:|------------------|---------:|----------------------:|-----------------:|---------------------------|
| Dekaf | 2026-07-17T02:18:52.8306536+00:00 | 3 | 1.0 MiB / 0.1 MiB | 0.0 MB/s | 0/0 | 0 | 1.0s / 300 msg/s |
| Dekaf | 2026-07-17T02:19:01.8420091+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 9.0s / 351 msg/s |
| Dekaf | 2026-07-17T02:19:10.8480471+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 18.0s / 360 msg/s |
| Dekaf | 2026-07-17T02:19:20.8511459+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 28.0s / 353 msg/s |
| Dekaf | 2026-07-17T02:19:29.8727089+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 37.0s / 353 msg/s |
| Dekaf | 2026-07-17T02:19:38.8764396+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 46.0s / 356 msg/s |
| Dekaf | 2026-07-17T02:19:47.8797556+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 55.0s / 365 msg/s |
| Dekaf | 2026-07-17T02:19:56.8971901+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 64.0s / 361 msg/s |
| Dekaf | 2026-07-17T02:20:05.9120307+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 73.0s / 353 msg/s |
| Dekaf | 2026-07-17T02:20:14.9348706+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 82.0s / 361 msg/s |
| Dekaf | 2026-07-17T02:20:23.9614897+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 91.0s / 357 msg/s |
| Dekaf | 2026-07-17T02:20:32.9842049+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 100.0s / 356 msg/s |
| Dekaf | 2026-07-17T02:20:41.9895404+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 109.0s / 357 msg/s |
| Dekaf | 2026-07-17T02:20:50.9916097+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 118.0s / 358 msg/s |
| Dekaf | 2026-07-17T02:20:59.9976079+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 127.0s / 364 msg/s |
| Dekaf | 2026-07-17T02:21:10.002541+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 137.0s / 352 msg/s |
| Dekaf | 2026-07-17T02:21:19.0093258+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 146.0s / 348 msg/s |
| Dekaf | 2026-07-17T02:21:28.0245641+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 155.0s / 352 msg/s |
| Dekaf | 2026-07-17T02:21:37.0291467+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 164.0s / 352 msg/s |
| Dekaf | 2026-07-17T02:21:46.0448477+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 173.0s / 357 msg/s |
| Dekaf | 2026-07-17T02:21:55.0467134+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 182.0s / 365 msg/s |
| Dekaf | 2026-07-17T02:22:04.0690424+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 191.0s / 352 msg/s |
| Dekaf | 2026-07-17T02:22:13.0777204+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 200.0s / 358 msg/s |
| Dekaf | 2026-07-17T02:22:22.0845547+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 209.0s / 363 msg/s |
| Dekaf | 2026-07-17T02:22:31.0874573+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 218.0s / 353 msg/s |
| Dekaf | 2026-07-17T02:22:40.0886168+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 227.0s / 355 msg/s |
| Dekaf | 2026-07-17T02:22:49.0912115+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 236.0s / 366 msg/s |
| Dekaf | 2026-07-17T02:22:59.1137344+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 246.0s / 353 msg/s |
| Dekaf | 2026-07-17T02:23:08.1186344+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 255.0s / 356 msg/s |
| Dekaf | 2026-07-17T02:23:17.1328999+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 264.0s / 359 msg/s |
| Dekaf | 2026-07-17T02:23:26.1378818+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 273.1s / 344 msg/s |
| Dekaf | 2026-07-17T02:23:35.1456116+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 282.1s / 354 msg/s |
| Dekaf | 2026-07-17T02:23:44.1507909+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 291.1s / 341 msg/s |
| Dekaf | 2026-07-17T02:23:53.1584885+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 300.1s / 346 msg/s |
| Dekaf | 2026-07-17T02:24:02.2124934+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 309.1s / 335 msg/s |
| Dekaf | 2026-07-17T02:24:11.2174358+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 318.1s / 338 msg/s |
| Dekaf | 2026-07-17T02:24:20.2384664+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 327.1s / 349 msg/s |
| Dekaf | 2026-07-17T02:24:29.2454141+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 336.1s / 348 msg/s |
| Dekaf | 2026-07-17T02:24:38.2479676+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 345.1s / 353 msg/s |
| Dekaf | 2026-07-17T02:24:47.2490726+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 354.1s / 360 msg/s |
| Dekaf | 2026-07-17T02:24:57.2535581+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 364.1s / 358 msg/s |
| Dekaf | 2026-07-17T02:25:06.2591311+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 373.1s / 368 msg/s |
| Dekaf | 2026-07-17T02:25:15.2899852+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 382.1s / 356 msg/s |
| Dekaf | 2026-07-17T02:25:24.2935105+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 391.1s / 367 msg/s |
| Dekaf | 2026-07-17T02:25:33.3133459+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 400.1s / 368 msg/s |
| Dekaf | 2026-07-17T02:25:42.3182217+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 409.1s / 357 msg/s |
| Dekaf | 2026-07-17T02:25:51.3237597+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 418.1s / 361 msg/s |
| Dekaf | 2026-07-17T02:26:00.3250295+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 427.1s / 361 msg/s |
| Dekaf | 2026-07-17T02:26:09.3481189+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 436.1s / 361 msg/s |
| Dekaf | 2026-07-17T02:26:18.3596851+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 445.1s / 356 msg/s |
| Dekaf | 2026-07-17T02:26:27.3630281+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 454.1s / 343 msg/s |
| Dekaf | 2026-07-17T02:26:36.3668131+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 463.1s / 355 msg/s |
| Dekaf | 2026-07-17T02:26:46.372849+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 473.1s / 328 msg/s |
| Dekaf | 2026-07-17T02:26:55.3871373+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 482.1s / 350 msg/s |
| Dekaf | 2026-07-17T02:27:04.3900248+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 491.1s / 345 msg/s |
| Dekaf | 2026-07-17T02:27:13.3981499+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 500.1s / 354 msg/s |
| Dekaf | 2026-07-17T02:27:22.4011807+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 509.1s / 357 msg/s |
| Dekaf | 2026-07-17T02:27:31.4037233+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 518.1s / 344 msg/s |
| Dekaf | 2026-07-17T02:27:40.4231229+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 527.1s / 354 msg/s |
| Dekaf | 2026-07-17T02:27:49.427114+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 536.1s / 345 msg/s |
| Dekaf | 2026-07-17T02:27:58.430878+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 545.1s / 360 msg/s |
| Dekaf | 2026-07-17T02:28:07.4345055+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 554.1s / 333 msg/s |
| Dekaf | 2026-07-17T02:28:16.4360456+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 563.1s / 347 msg/s |
| Dekaf | 2026-07-17T02:28:25.4593591+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 572.1s / 354 msg/s |
| Dekaf | 2026-07-17T02:28:35.4733766+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 582.1s / 348 msg/s |
| Dekaf | 2026-07-17T02:28:44.477618+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 591.1s / 363 msg/s |
| Dekaf | 2026-07-17T02:28:53.481602+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 600.1s / 355 msg/s |
| Dekaf | 2026-07-17T02:29:02.4860979+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 609.1s / 359 msg/s |
| Dekaf | 2026-07-17T02:29:11.4925712+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 618.1s / 357 msg/s |
| Dekaf | 2026-07-17T02:29:20.5097826+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 627.1s / 346 msg/s |
| Dekaf | 2026-07-17T02:29:29.5356508+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 636.1s / 344 msg/s |
| Dekaf | 2026-07-17T02:29:38.5419206+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 645.1s / 353 msg/s |
| Dekaf | 2026-07-17T02:29:47.5573628+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 654.1s / 342 msg/s |
| Dekaf | 2026-07-17T02:29:56.5648189+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 663.1s / 356 msg/s |
| Dekaf | 2026-07-17T02:30:05.5660354+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 672.1s / 348 msg/s |
| Dekaf | 2026-07-17T02:30:14.5790877+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 682.1s / 361 msg/s |
| Dekaf | 2026-07-17T02:30:23.5853715+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 691.1s / 359 msg/s |
| Dekaf | 2026-07-17T02:30:33.5895316+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 701.1s / 351 msg/s |
| Dekaf | 2026-07-17T02:30:42.5942776+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 710.1s / 352 msg/s |
| Dekaf | 2026-07-17T02:30:51.6082061+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 719.1s / 354 msg/s |
| Dekaf | 2026-07-17T02:31:00.6125728+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 728.1s / 357 msg/s |
| Dekaf | 2026-07-17T02:31:09.6247806+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 737.1s / 356 msg/s |
| Dekaf | 2026-07-17T02:31:18.6354496+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 746.1s / 348 msg/s |
| Dekaf | 2026-07-17T02:31:27.6493639+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 755.1s / 358 msg/s |
| Dekaf | 2026-07-17T02:31:36.6521335+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 764.1s / 352 msg/s |
| Dekaf | 2026-07-17T02:31:45.6587268+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 773.1s / 357 msg/s |
| Dekaf | 2026-07-17T02:31:54.6682576+00:00 | 3 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 782.1s / 350 msg/s |
| Dekaf | 2026-07-17T02:32:03.691714+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 791.1s / 345 msg/s |
| Dekaf | 2026-07-17T02:32:12.7068811+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 800.1s / 331 msg/s |
| Dekaf | 2026-07-17T02:32:22.7290335+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 810.1s / 336 msg/s |
| Dekaf | 2026-07-17T02:32:31.7363091+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 819.1s / 352 msg/s |
| Dekaf | 2026-07-17T02:32:40.7390612+00:00 | 1 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 828.1s / 355 msg/s |
| Dekaf | 2026-07-17T02:32:49.742869+00:00 | 1 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 837.1s / 344 msg/s |
| Dekaf | 2026-07-17T02:32:58.7472699+00:00 | 2 | 16.0 MiB / 0.1 MiB | 0.2 MB/s | 0/0 | 0 | 846.1s / 353 msg/s |
| Dekaf | 2026-07-17T02:33:07.7513599+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 855.1s / 360 msg/s |
| Dekaf | 2026-07-17T02:33:16.7712962+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 864.1s / 353 msg/s |
| Dekaf | 2026-07-17T02:33:25.796901+00:00 | 2 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 873.1s / 347 msg/s |
| Dekaf | 2026-07-17T02:33:34.8033558+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 882.1s / 358 msg/s |
| Dekaf | 2026-07-17T02:33:43.8044975+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 891.1s / 358 msg/s |
| Dekaf | 2026-07-17T02:33:52.8171037+00:00 | 3 | 16.0 MiB / 0.0 MiB | 0.2 MB/s | 0/0 | 0 | 899.1s / 357 msg/s |
*2,598 budget sample(s) omitted; rows sampled across the full timeline.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 153,900 | 115,500 | 38,400 | 115,500 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 317,300 | 238,000 | 79,300 | 238,000 | 0 | 0 | 0 | 0 | 0 | PASS |

:::note
Confluent.Kafka uses 1.52x less CPU per message for producer (transactional eos), 3 brokers; comparison throughput is 2.06x.
:::

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.72 | - | 1,814,115 | 1,818,898 | -1.2% | -0.01% | 1730.07 | - | 0 | 1.30 |
| Confluent | 1.06 | - | 1,275,816 | 1,367,796 | +4.8% | +0.50% | 1216.71 | - | 0 | 1.36 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.48x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 1.33x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.69 | - | 1,967,473 | 1,985,534 | +3.6% | +0.25% | 1876.33 | - | 0 | 1.35 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.41 | - | 3,778,727 | 3,779,633 | +6.3% | +0.54% | 3603.67 | - | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.36 | - | 4,098,032 | 4,046,680 | +3.2% | +0.33% | 3908.19 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 68019 | 0 | 0 | 2609.29 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 273828 | 9 | 1 | 1327.17 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 288375 | 1 | 1 | 1415.32 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 169637 | 0 | 0 | 823.07 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 240728 | 1 | 1 | 1263.95 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 275969 | 9 | 1 | 1333.12 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 190169 | 0 | 0 | 971.60 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 284557 | 17 | 1 | 1359.69 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 288673 | 1 | 1 | 1478.31 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 194347 | 2 | 2 | 916.88 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 8494 | 2 | 2 | 21.42 GB | 1.13 KB |
| Confluent | Producer (Transactional EOS), 3 Brokers | 98 | 2 | 1 | 254.92 MB | 1.70 KB |
| Dekaf | Consumer | 79258 | 1797 | 514 | 3082.65 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 29747 | 1452 | 546 | 3341.93 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 4 | 1 | 0 | 530.90 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 9 | 2 | 1 | 984.13 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 464 | 1 | 1 | 1.78 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 420 | 2 | 2 | 94.80 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 189 | 2 | 2 | 117.65 MB | 0 B |
| Dekaf | Producer (Acks All) | 442 | 2 | 2 | 111.70 MB | 0 B |
| Dekaf | Producer (Acks All) | 455 | 2 | 2 | 1.74 GB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 236 | 2 | 2 | 133.66 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 437 | 2 | 2 | 1.64 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 453 | 2 | 2 | 157.57 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 233 | 2 | 2 | 116.72 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 2225 | 7 | 1 | 5.63 GB | 306 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 52 | 1 | 1 | 91.05 MB | 301 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 521 | 4 | 3 | 1.62 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 207 | 5 | 4 | 1023.07 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 445 | 5 | 4 | 1.53 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 307 | 7 | 5 | 1.41 GB | 1 B |

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
