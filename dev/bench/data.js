window.BENCHMARK_DATA = {
  "lastUpdate": 1769611890689,
  "repoUrl": "https://github.com/thomhurst/Dekaf",
  "entries": {
    "Dekaf Benchmarks": [
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "id": "902fd131adb7db1138b0714bc537426d71ce2b5b",
          "message": "Implement lazy consumer iteration to reduce allocations\n\nReplace Channel<ConsumeResult> buffer with Queue<PendingFetchData> that\nstores raw fetch response data. Records are now only parsed and\ndeserialized when actually yielded to the consumer, avoiding allocations\nfor records that may never be consumed.\n\nThis builds on the lazy RecordBatch parsing to achieve true lazy\niteration where:\n- Fetch responses are queued without processing records\n- Record batches are parsed lazily via LazyRecordList\n- Records are only deserialized when yielded\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-22T00:12:34Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/902fd131adb7db1138b0714bc537426d71ce2b5b"
        },
        "date": 1769040855842,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3020290983.5,
            "unit": "ns",
            "range": "± 2053290.1586771251"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3179439458.8,
            "unit": "ns",
            "range": "± 3514774.35880096"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3021806634.2,
            "unit": "ns",
            "range": "± 3583900.3926846515"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3181191283.8,
            "unit": "ns",
            "range": "± 2254453.6407164596"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3017988976.75,
            "unit": "ns",
            "range": "± 1725152.7916497474"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3176142555.7,
            "unit": "ns",
            "range": "± 3774761.8375085206"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3017866936,
            "unit": "ns",
            "range": "± 583056.746112246"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3180834308,
            "unit": "ns",
            "range": "± 915142.939000788"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3020636868.8,
            "unit": "ns",
            "range": "± 1861321.0566086387"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3175776349,
            "unit": "ns",
            "range": "± 1341726.2054336942"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3017785744.2,
            "unit": "ns",
            "range": "± 2000892.283817822"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3180977000.75,
            "unit": "ns",
            "range": "± 603655.9341061623"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3023362046.3,
            "unit": "ns",
            "range": "± 1676485.0615295384"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3176715556.6,
            "unit": "ns",
            "range": "± 1482959.504257382"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3016559169.8,
            "unit": "ns",
            "range": "± 1313278.751234558"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3182122118.6,
            "unit": "ns",
            "range": "± 1696213.2096985332"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8765974.8,
            "unit": "ns",
            "range": "± 590592.9836172756"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7822280.666666667,
            "unit": "ns",
            "range": "± 323633.6778794043"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 511107596.1666667,
            "unit": "ns",
            "range": "± 5247675.180540331"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8322504.777777778,
            "unit": "ns",
            "range": "± 453280.35245192837"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 152268.5,
            "unit": "ns",
            "range": "± 29022.55675332551"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 213342.8,
            "unit": "ns",
            "range": "± 39451.95311824808"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8795562.5,
            "unit": "ns",
            "range": "± 412092.6605127784"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5896259.8,
            "unit": "ns",
            "range": "± 38897.65477877668"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 4960181775.3,
            "unit": "ns",
            "range": "± 48659464.656585194"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8672278.777777778,
            "unit": "ns",
            "range": "± 468275.89930477354"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1596729.0555555555,
            "unit": "ns",
            "range": "± 343450.9790130577"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 3898719.0555555555,
            "unit": "ns",
            "range": "± 1299776.5232263114"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 9067734.4,
            "unit": "ns",
            "range": "± 660028.0553436431"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5628138.6,
            "unit": "ns",
            "range": "± 27245.831360860408"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 499926484.2,
            "unit": "ns",
            "range": "± 7723751.270542573"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 6002895,
            "unit": "ns",
            "range": "± 158034.2881584563"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 124057.8,
            "unit": "ns",
            "range": "± 15534.05861718766"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 299538.125,
            "unit": "ns",
            "range": "± 14155.147215639567"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 9133437.9,
            "unit": "ns",
            "range": "± 358110.6582240405"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5553073,
            "unit": "ns",
            "range": "± 31637.41187967885"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5008497963.8,
            "unit": "ns",
            "range": "± 61771223.406210706"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 14031548.944444444,
            "unit": "ns",
            "range": "± 946637.8861621417"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 1394997,
            "unit": "ns",
            "range": "± 197015.36699899833"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 4594186,
            "unit": "ns",
            "range": "± 628438.343611567"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 31249.777777777777,
            "unit": "ns",
            "range": "± 4377.4913985574485"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 14729.8,
            "unit": "ns",
            "range": "± 692.7932271288133"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15713.8,
            "unit": "ns",
            "range": "± 838.5028721874879"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 132760.83333333334,
            "unit": "ns",
            "range": "± 12278.62698961085"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 24654.5,
            "unit": "ns",
            "range": "± 1661.3816204860607"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5925.388888888889,
            "unit": "ns",
            "range": "± 598.0375081139235"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 37733.25,
            "unit": "ns",
            "range": "± 5212.868274074949"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 83692.94444444444,
            "unit": "ns",
            "range": "± 33859.848091475214"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 665.7777777777778,
            "unit": "ns",
            "range": "± 48.230638026512196"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 1571.5,
            "unit": "ns",
            "range": "± 718.6529064854604"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 1198.7,
            "unit": "ns",
            "range": "± 749.0798503645804"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 480.7,
            "unit": "ns",
            "range": "± 43.784319872149055"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1900.5555555555557,
            "unit": "ns",
            "range": "± 194.52770439651465"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 2718.8,
            "unit": "ns",
            "range": "± 794.0732963650144"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 2215.2,
            "unit": "ns",
            "range": "± 688.4621832590209"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 1341.5,
            "unit": "ns",
            "range": "± 806.9911193232963"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 1622.8,
            "unit": "ns",
            "range": "± 811.7364172630861"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 1722.4,
            "unit": "ns",
            "range": "± 724.7378759861313"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 3343.125,
            "unit": "ns",
            "range": "± 526.4316364516317"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 169.125,
            "unit": "ns",
            "range": "± 113.13637472411023"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 2968.7,
            "unit": "ns",
            "range": "± 853.2496117784057"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 4352.75,
            "unit": "ns",
            "range": "± 162.1690035558143"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 6826.777777777777,
            "unit": "ns",
            "range": "± 797.9050347280962"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 3838.7,
            "unit": "ns",
            "range": "± 986.2937414606484"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 3323.2,
            "unit": "ns",
            "range": "± 423.90664590737947"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 4206.8,
            "unit": "ns",
            "range": "± 962.1066930901628"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 4080.0555555555557,
            "unit": "ns",
            "range": "± 424.01359385965185"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 1657.6,
            "unit": "ns",
            "range": "± 751.0731877342803"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 498.125,
            "unit": "ns",
            "range": "± 59.99032660116367"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 1655.4,
            "unit": "ns",
            "range": "± 714.7147371892898"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 574.75,
            "unit": "ns",
            "range": "± 403.60763744012576"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 2505.7,
            "unit": "ns",
            "range": "± 820.9893489632561"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1325.3,
            "unit": "ns",
            "range": "± 698.5320799123449"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1638.8333333333333,
            "unit": "ns",
            "range": "± 72.11969217904358"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 1551.1,
            "unit": "ns",
            "range": "± 770.7879734401673"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 1690.6,
            "unit": "ns",
            "range": "± 801.5520292255242"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 1481.1,
            "unit": "ns",
            "range": "± 808.5871697665696"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 2297.9,
            "unit": "ns",
            "range": "± 168.52444596820038"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 122.75,
            "unit": "ns",
            "range": "± 56.177651886035356"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 2750.3888888888887,
            "unit": "ns",
            "range": "± 396.5351322532609"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4666.125,
            "unit": "ns",
            "range": "± 376.94256383243777"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 6088.1,
            "unit": "ns",
            "range": "± 853.8490889300443"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 3431.8,
            "unit": "ns",
            "range": "± 908.1957449311856"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 2681.4,
            "unit": "ns",
            "range": "± 917.8406361309861"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 4380.1,
            "unit": "ns",
            "range": "± 729.0225495674176"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3809,
            "unit": "ns",
            "range": "± 356.5148764678091"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 1088.4,
            "unit": "ns",
            "range": "± 723.2796370238369"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 367.95,
            "unit": "ns",
            "range": "± 598.335536207644"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 1520.6,
            "unit": "ns",
            "range": "± 777.3875695773205"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 1260,
            "unit": "ns",
            "range": "± 796.018006496503"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1722.5,
            "unit": "ns",
            "range": "± 139.79372558983366"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 461.375,
            "unit": "ns",
            "range": "± 281.45536946379264"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 2929.2,
            "unit": "ns",
            "range": "± 817.9024391698559"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 1283.2,
            "unit": "ns",
            "range": "± 799.8455475902831"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 650.9444444444445,
            "unit": "ns",
            "range": "± 73.83276899709084"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 2011.7,
            "unit": "ns",
            "range": "± 659.8240590406573"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 2965.4,
            "unit": "ns",
            "range": "± 741.5188916451601"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 235.5,
            "unit": "ns",
            "range": "± 76.12208239776044"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 2257.8888888888887,
            "unit": "ns",
            "range": "± 200.51025188531162"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 5971.5,
            "unit": "ns",
            "range": "± 823.8157830755881"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 6888.277777777777,
            "unit": "ns",
            "range": "± 863.9415167963884"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 3831,
            "unit": "ns",
            "range": "± 933.9635967209857"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 1697.5555555555557,
            "unit": "ns",
            "range": "± 124.73182343643413"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 4779.1,
            "unit": "ns",
            "range": "± 871.146683655769"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 5109.1,
            "unit": "ns",
            "range": "± 877.0253005345842"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "id": "b11170f8cf4ea3d0c9f1d5cfe5b062c9d34c6ae2",
          "message": "Use spans for zero-allocation in more hot paths\n\nPartitioner:\n- Change IPartitioner.Partition to accept ReadOnlySpan<byte> for key\n- Update Murmur2.Hash to accept ReadOnlySpan<byte> instead of byte[]\n- Eliminates array allocation when partitioning messages\n\nConsumerCoordinator:\n- Replace GroupBy/ToDictionary LINQ in BuildAssignmentData with imperative loops\n- Convert IReadOnlySet to List for writer without ToArray().AsSpan()\n\nSASL Plain:\n- Write directly to byte array using Encoding.GetBytes span overload\n- Eliminates intermediate string allocation from interpolation\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-22T00:27:32Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/b11170f8cf4ea3d0c9f1d5cfe5b062c9d34c6ae2"
        },
        "date": 1769041662696,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3021867862.3,
            "unit": "ns",
            "range": "± 2107242.331294315"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3175995538.7,
            "unit": "ns",
            "range": "± 2522556.507431439"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3019836074.7,
            "unit": "ns",
            "range": "± 3403247.7564279246"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3182512358.2,
            "unit": "ns",
            "range": "± 2785291.4386258936"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3018925053.6,
            "unit": "ns",
            "range": "± 2200354.641755574"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3176102735.8,
            "unit": "ns",
            "range": "± 1624390.2516985259"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3017593826,
            "unit": "ns",
            "range": "± 2128987.159219144"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3179824682.5,
            "unit": "ns",
            "range": "± 1496834.3178601966"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3018247106.5,
            "unit": "ns",
            "range": "± 836802.290582429"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3175700294,
            "unit": "ns",
            "range": "± 981824.6006970899"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3015996592,
            "unit": "ns",
            "range": "± 325761.6823476942"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3182593283.3,
            "unit": "ns",
            "range": "± 702539.3813881184"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3021526776.6,
            "unit": "ns",
            "range": "± 3256339.7039380427"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3176965468.9,
            "unit": "ns",
            "range": "± 1136755.8525047495"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3016023133.25,
            "unit": "ns",
            "range": "± 843590.0071118177"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3180922920.75,
            "unit": "ns",
            "range": "± 1008609.1606806457"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8916731.875,
            "unit": "ns",
            "range": "± 231478.3338423074"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 6751867.055555556,
            "unit": "ns",
            "range": "± 202451.40776869835"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 507942686.2,
            "unit": "ns",
            "range": "± 3648593.5269410866"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7818425.5,
            "unit": "ns",
            "range": "± 416411.62020396226"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 168235.6,
            "unit": "ns",
            "range": "± 32460.767154623234"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 224133.1,
            "unit": "ns",
            "range": "± 35063.68867963298"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8940735.888888888,
            "unit": "ns",
            "range": "± 273256.91416707303"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5622465,
            "unit": "ns",
            "range": "± 60680.91266382112"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5016537895.2,
            "unit": "ns",
            "range": "± 64121003.990916364"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9015099.666666666,
            "unit": "ns",
            "range": "± 361015.4449954323"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1682878,
            "unit": "ns",
            "range": "± 284390.79695508786"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 2211321.222222222,
            "unit": "ns",
            "range": "± 328825.4436938304"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8922172.722222222,
            "unit": "ns",
            "range": "± 259416.34348031052"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5522764.2,
            "unit": "ns",
            "range": "± 32225.864907968982"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 504283130.3,
            "unit": "ns",
            "range": "± 6747219.525299293"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 6531712.8,
            "unit": "ns",
            "range": "± 171819.88720365806"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 157554.22222222222,
            "unit": "ns",
            "range": "± 24150.111597970816"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 304908.55555555556,
            "unit": "ns",
            "range": "± 11702.61247661298"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 8934360.5,
            "unit": "ns",
            "range": "± 369690.33084630436"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5500239.1,
            "unit": "ns",
            "range": "± 28091.157040566657"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 4960336093.2,
            "unit": "ns",
            "range": "± 62677048.233471535"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 19042301.5,
            "unit": "ns",
            "range": "± 932457.9348477335"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 1638372.2222222222,
            "unit": "ns",
            "range": "± 341025.1633936922"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 5906296.611111111,
            "unit": "ns",
            "range": "± 849457.4403265363"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 29867.444444444445,
            "unit": "ns",
            "range": "± 6046.683432078926"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13718.111111111111,
            "unit": "ns",
            "range": "± 123.19440373292575"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14211.888888888889,
            "unit": "ns",
            "range": "± 155.49151459520584"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 136724.11111111112,
            "unit": "ns",
            "range": "± 8018.804266292519"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 22710,
            "unit": "ns",
            "range": "± 215.3360422946217"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5933.388888888889,
            "unit": "ns",
            "range": "± 509.48244436006934"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 41526.22222222222,
            "unit": "ns",
            "range": "± 7468.974223040567"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 107359.77777777778,
            "unit": "ns",
            "range": "± 55489.919460154604"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 414.5,
            "unit": "ns",
            "range": "± 48.03867489548693"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 345.3,
            "unit": "ns",
            "range": "± 47.001300218421854"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 449.9,
            "unit": "ns",
            "range": "± 159.52390973699767"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 352.22222222222223,
            "unit": "ns",
            "range": "± 19.860625479688306"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1301.6,
            "unit": "ns",
            "range": "± 68.80964079739601"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1246.5555555555557,
            "unit": "ns",
            "range": "± 108.09961969303026"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1696.0555555555557,
            "unit": "ns",
            "range": "± 134.40992440209828"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 601.5555555555555,
            "unit": "ns",
            "range": "± 40.96068575814836"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 670.5,
            "unit": "ns",
            "range": "± 185.7423544112172"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 850.6,
            "unit": "ns",
            "range": "± 112.39558117055432"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 1766,
            "unit": "ns",
            "range": "± 284.9179219042253"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 55.833333333333336,
            "unit": "ns",
            "range": "± 22.26544407821232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 2027,
            "unit": "ns",
            "range": "± 168.27226615088878"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 4260.375,
            "unit": "ns",
            "range": "± 39.92112759644232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 5302.722222222223,
            "unit": "ns",
            "range": "± 108.69427052261975"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 2929.1,
            "unit": "ns",
            "range": "± 172.09006040120067"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 2196.6666666666665,
            "unit": "ns",
            "range": "± 205.66720691447142"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 2965.8,
            "unit": "ns",
            "range": "± 509.58219269428076"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 3323.75,
            "unit": "ns",
            "range": "± 61.66904757308506"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 403.4,
            "unit": "ns",
            "range": "± 42.17476865509888"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 386.6,
            "unit": "ns",
            "range": "± 162.25508038613492"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 474.6,
            "unit": "ns",
            "range": "± 175.87065954527176"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 319.3,
            "unit": "ns",
            "range": "± 54.97282156767207"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1631.9,
            "unit": "ns",
            "range": "± 167.3017566488104"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1608.6,
            "unit": "ns",
            "range": "± 58.30037163975315"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1298.5,
            "unit": "ns",
            "range": "± 42.23404178757091"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 536.1111111111111,
            "unit": "ns",
            "range": "± 23.634955280497383"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 853.4,
            "unit": "ns",
            "range": "± 138.51048897305776"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 529.5,
            "unit": "ns",
            "range": "± 48.94129136015927"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 1626.388888888889,
            "unit": "ns",
            "range": "± 97.34531889675596"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 237.1,
            "unit": "ns",
            "range": "± 175.95419227110725"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 1757.4444444444443,
            "unit": "ns",
            "range": "± 35.74601764921203"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4265.5,
            "unit": "ns",
            "range": "± 84.25471584936444"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 5352,
            "unit": "ns",
            "range": "± 139.21822540786204"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 2417.125,
            "unit": "ns",
            "range": "± 50.094018747608125"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 1744.4,
            "unit": "ns",
            "range": "± 302.3311098778953"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 3396.3,
            "unit": "ns",
            "range": "± 89.80355103100199"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3417.1111111111113,
            "unit": "ns",
            "range": "± 226.6304505381197"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 458.77777777777777,
            "unit": "ns",
            "range": "± 23.758039574940614"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 369.6,
            "unit": "ns",
            "range": "± 92.85257131603842"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 613.1111111111111,
            "unit": "ns",
            "range": "± 130.9011501519796"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 264.94444444444446,
            "unit": "ns",
            "range": "± 26.034165586355517"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1460.0555555555557,
            "unit": "ns",
            "range": "± 143.3545526928872"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1300.4444444444443,
            "unit": "ns",
            "range": "± 55.55877768433875"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 1492.875,
            "unit": "ns",
            "range": "± 41.80204198703353"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 533.3,
            "unit": "ns",
            "range": "± 98.1665817769865"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 615.2,
            "unit": "ns",
            "range": "± 163.36941778272538"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 553.6666666666666,
            "unit": "ns",
            "range": "± 63.46258740391853"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 1928.4,
            "unit": "ns",
            "range": "± 188.28831320315365"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 75.375,
            "unit": "ns",
            "range": "± 21.58331035115526"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 1828.4444444444443,
            "unit": "ns",
            "range": "± 59.395098937351534"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 5107.888888888889,
            "unit": "ns",
            "range": "± 100.67949697486132"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 5774.5,
            "unit": "ns",
            "range": "± 201.19429131350336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 3278.2,
            "unit": "ns",
            "range": "± 126.21590848841343"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 2165.9,
            "unit": "ns",
            "range": "± 283.57458356567224"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3200.8,
            "unit": "ns",
            "range": "± 211.8950473963729"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 3169.777777777778,
            "unit": "ns",
            "range": "± 92.47266863481579"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "id": "deb2bcc87db6efb0b616b2caa296a7a457847eda",
          "message": "Major producer performance optimization - eliminate per-message allocations\n\nKafkaProducer:\n- Add thread-local ArrayBufferWriter for serialization (t_serializationBuffer)\n- Reuse buffer across serialize calls instead of allocating new per message\n- Eliminates 2 ArrayBufferWriter allocations per message\n\nRecordBatch:\n- Add thread-local buffers for record and CRC serialization\n- t_recordsBuffer and t_crcBuffer are reused across batch writes\n- Eliminates 2 ArrayBufferWriter allocations per batch\n\nProduceRequestPartitionData:\n- Add thread-local buffer for record batch serialization\n- Eliminates 1 ArrayBufferWriter allocation per partition per request\n\nRecordAccumulator:\n- Remove unnecessary .ToList() copies in Complete()\n- Pass _records and _completionSources directly (batch is discarded after)\n- Eliminates 2 list allocations per batch\n\nRecordHeader:\n- Add WriteStringContent to KafkaProtocolWriter for headerless string writing\n- Use it for header key encoding instead of Encoding.UTF8.GetBytes\n- Eliminates 1 byte[] allocation per header\n\nThese changes should dramatically reduce allocations for batch produce:\n- Before: ~4000 allocations for 1000 messages\n- After: ~1000 allocations (serialized byte[] still needed per message)\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-22T00:35:03Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/deb2bcc87db6efb0b616b2caa296a7a457847eda"
        },
        "date": 1769042113991,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3023603652.5,
            "unit": "ns",
            "range": "± 1846986.8156787152"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3174140945.1,
            "unit": "ns",
            "range": "± 1690626.2976844704"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3020642949.8,
            "unit": "ns",
            "range": "± 1876475.5240606524"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3179903291.25,
            "unit": "ns",
            "range": "± 959044.8808314012"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3020005547.8,
            "unit": "ns",
            "range": "± 2799777.060247601"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3173533206.25,
            "unit": "ns",
            "range": "± 854033.9821144413"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3018022411.2,
            "unit": "ns",
            "range": "± 1209810.2525054084"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3166674616.25,
            "unit": "ns",
            "range": "± 25803014.342637196"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3018279238,
            "unit": "ns",
            "range": "± 822240.994714749"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3176561616.2,
            "unit": "ns",
            "range": "± 1258982.2634613246"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3016190617.2,
            "unit": "ns",
            "range": "± 1709859.033487761"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3182344962.8,
            "unit": "ns",
            "range": "± 1068181.4189327578"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3020769469.2,
            "unit": "ns",
            "range": "± 3879909.744264317"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3175721876.2,
            "unit": "ns",
            "range": "± 532314.4563485947"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3016972966.5,
            "unit": "ns",
            "range": "± 1728062.3744202638"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179797474.4,
            "unit": "ns",
            "range": "± 968564.4639549296"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8546518.833333334,
            "unit": "ns",
            "range": "± 733999.3508893588"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 6607669.833333333,
            "unit": "ns",
            "range": "± 256210.28847657543"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 500871494.4,
            "unit": "ns",
            "range": "± 6912712.263216647"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7126569.555555556,
            "unit": "ns",
            "range": "± 654477.5582716933"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 172190.9,
            "unit": "ns",
            "range": "± 21495.014995471753"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 220821.7,
            "unit": "ns",
            "range": "± 38615.267901727966"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9032314.444444444,
            "unit": "ns",
            "range": "± 161996.62832996796"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5636154.4,
            "unit": "ns",
            "range": "± 62711.39015663564"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 4980325403.9,
            "unit": "ns",
            "range": "± 35576749.732489236"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9450382.5,
            "unit": "ns",
            "range": "± 706400.3545258777"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1554148.2222222222,
            "unit": "ns",
            "range": "± 283639.74914263067"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 2376154.888888889,
            "unit": "ns",
            "range": "± 188138.57418432593"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8994412.1,
            "unit": "ns",
            "range": "± 739987.6052057224"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5458538.611111111,
            "unit": "ns",
            "range": "± 22401.166222121363"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 499093900.4,
            "unit": "ns",
            "range": "± 6051464.679703221"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 6498596,
            "unit": "ns",
            "range": "± 119629.30608620391"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 154665.4,
            "unit": "ns",
            "range": "± 20272.303767565354"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 292909,
            "unit": "ns",
            "range": "± 8713.319889686136"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 8961074.3,
            "unit": "ns",
            "range": "± 328695.4067779165"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5535759.277777778,
            "unit": "ns",
            "range": "± 59858.02135006172"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5043590703.6,
            "unit": "ns",
            "range": "± 9880335.53610334"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 19361652.625,
            "unit": "ns",
            "range": "± 969987.1488622101"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 1725326.388888889,
            "unit": "ns",
            "range": "± 237146.70193861247"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 6498797.111111111,
            "unit": "ns",
            "range": "± 1521466.7417582157"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 34749.88888888889,
            "unit": "ns",
            "range": "± 9808.018995246242"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13693.75,
            "unit": "ns",
            "range": "± 310.4060704487407"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14438,
            "unit": "ns",
            "range": "± 377.3074932259422"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 139474.44444444444,
            "unit": "ns",
            "range": "± 13260.370650090359"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 23189.666666666668,
            "unit": "ns",
            "range": "± 245.56923667267446"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5698.125,
            "unit": "ns",
            "range": "± 102.8145869306213"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 43099.666666666664,
            "unit": "ns",
            "range": "± 7397.9891862586555"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 90043.11111111111,
            "unit": "ns",
            "range": "± 39912.11442483486"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 501.55555555555554,
            "unit": "ns",
            "range": "± 33.094981156933414"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 434.5,
            "unit": "ns",
            "range": "± 29.29258533410043"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 597.8333333333334,
            "unit": "ns",
            "range": "± 202.99076333666022"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 401.44444444444446,
            "unit": "ns",
            "range": "± 20.561560684388184"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1690.7,
            "unit": "ns",
            "range": "± 193.9799817850629"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1412.0555555555557,
            "unit": "ns",
            "range": "± 141.70136829889037"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1440,
            "unit": "ns",
            "range": "± 62.85101431162428"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 680.7777777777778,
            "unit": "ns",
            "range": "± 31.783556195687797"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 662.25,
            "unit": "ns",
            "range": "± 33.99054490379851"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 699.8888888888889,
            "unit": "ns",
            "range": "± 48.03760101328032"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 2271.7,
            "unit": "ns",
            "range": "± 178.2676202916403"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 181.83333333333334,
            "unit": "ns",
            "range": "± 15.173990905493518"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 2518.6666666666665,
            "unit": "ns",
            "range": "± 85.42686930936894"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 5140.8,
            "unit": "ns",
            "range": "± 139.5572761748141"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 6016.333333333333,
            "unit": "ns",
            "range": "± 217.57584884356993"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 3233.3333333333335,
            "unit": "ns",
            "range": "± 95.60334722173695"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 1775.125,
            "unit": "ns",
            "range": "± 20.392137840704336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 3698.3,
            "unit": "ns",
            "range": "± 157.62317934449447"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 3716.4444444444443,
            "unit": "ns",
            "range": "± 270.5328589613058"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 508.7,
            "unit": "ns",
            "range": "± 25.927462917403492"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 458.3,
            "unit": "ns",
            "range": "± 44.11361593783841"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 525.5,
            "unit": "ns",
            "range": "± 31.54135607194375"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 390.875,
            "unit": "ns",
            "range": "± 38.566964027334514"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1893.2,
            "unit": "ns",
            "range": "± 171.7690956552494"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1384,
            "unit": "ns",
            "range": "± 36.41820580816296"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1486,
            "unit": "ns",
            "range": "± 39.19548078906182"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 640.0555555555555,
            "unit": "ns",
            "range": "± 70.67904765754685"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 618.8333333333334,
            "unit": "ns",
            "range": "± 31.304951684997057"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 640.9444444444445,
            "unit": "ns",
            "range": "± 28.771127502720113"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 2187.5,
            "unit": "ns",
            "range": "± 144.62105655816515"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 147.66666666666666,
            "unit": "ns",
            "range": "± 14.798648586948742"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 1989.875,
            "unit": "ns",
            "range": "± 48.324609229075584"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4563.944444444444,
            "unit": "ns",
            "range": "± 49.06911225789374"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 5408.125,
            "unit": "ns",
            "range": "± 96.25626139188482"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 3347,
            "unit": "ns",
            "range": "± 100.6690121139569"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 2450.625,
            "unit": "ns",
            "range": "± 41.596488519379335"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 3047.6111111111113,
            "unit": "ns",
            "range": "± 204.04315992238284"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3636.4444444444443,
            "unit": "ns",
            "range": "± 161.03657279567824"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 483.77777777777777,
            "unit": "ns",
            "range": "± 32.740562677578474"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 451.6,
            "unit": "ns",
            "range": "± 39.271703129182804"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 480.3333333333333,
            "unit": "ns",
            "range": "± 22.02839077191069"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 421.6666666666667,
            "unit": "ns",
            "range": "± 28.89636655359978"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1518.8333333333333,
            "unit": "ns",
            "range": "± 77.52902682221672"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1459.75,
            "unit": "ns",
            "range": "± 41.230190048416844"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 2019.4,
            "unit": "ns",
            "range": "± 310.02838579717184"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 586.3333333333334,
            "unit": "ns",
            "range": "± 17.684739183827396"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 693.2222222222222,
            "unit": "ns",
            "range": "± 47.375568011839654"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 640.9,
            "unit": "ns",
            "range": "± 33.3381663163008"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 1890.3333333333333,
            "unit": "ns",
            "range": "± 53.74244132899063"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 175,
            "unit": "ns",
            "range": "± 13.093073414159543"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 2034.4,
            "unit": "ns",
            "range": "± 60.795102141903214"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 4782.777777777777,
            "unit": "ns",
            "range": "± 90.51074214945122"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 6693.111111111111,
            "unit": "ns",
            "range": "± 96.88194419555747"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 2722.5,
            "unit": "ns",
            "range": "± 86.95811799777111"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 1763.375,
            "unit": "ns",
            "range": "± 85.94090577666891"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3894.1,
            "unit": "ns",
            "range": "± 156.27287388126933"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 3757.5555555555557,
            "unit": "ns",
            "range": "± 235.69106427223278"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "id": "1034545b7c9a3b2007cdfef024c4fd58197f4417",
          "message": "Use ArrayPool for producer serialization to eliminate per-message allocations\n\n- Add PooledMemory struct to wrap arrays rented from ArrayPool<byte>.Shared\n- Producer now rents arrays from pool instead of calling .ToArray()\n- PartitionBatch tracks pooled arrays and returns them when batch completes\n- ReadyBatch returns arrays to pool in Complete() and Fail()\n- Convert Record and RecordHeader to readonly record struct\n- Convert ConsumeResult to readonly record struct\n- Fix integration tests for nullable value type access patterns\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-22T13:51:00Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/1034545b7c9a3b2007cdfef024c4fd58197f4417"
        },
        "date": 1769090876219,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3024060569.6,
            "unit": "ns",
            "range": "± 5229125.913382877"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3175977166,
            "unit": "ns",
            "range": "± 423011.9718672274"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3016963813,
            "unit": "ns",
            "range": "± 1251321.7401049712"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3181417783,
            "unit": "ns",
            "range": "± 1695194.052989067"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3017922254.75,
            "unit": "ns",
            "range": "± 332348.9847558587"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3175267543.25,
            "unit": "ns",
            "range": "± 986183.9045142223"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3015938510.3,
            "unit": "ns",
            "range": "± 1279440.9604927849"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3179904360.5,
            "unit": "ns",
            "range": "± 1990743.4226028477"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3017166201,
            "unit": "ns",
            "range": "± 277257.64424267906"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3166385072.8,
            "unit": "ns",
            "range": "± 20743075.242713727"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3016647852,
            "unit": "ns",
            "range": "± 559169.6436663922"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3180234490,
            "unit": "ns",
            "range": "± 647369.7493915926"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3022067717.3,
            "unit": "ns",
            "range": "± 4108940.072152513"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3166209509.4,
            "unit": "ns",
            "range": "± 22110044.79111027"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3016845338.5,
            "unit": "ns",
            "range": "± 771009.7685768969"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3180715199.8,
            "unit": "ns",
            "range": "± 1761980.2098086118"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8368436.055555556,
            "unit": "ns",
            "range": "± 491835.3053409014"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7029391,
            "unit": "ns",
            "range": "± 430775.7666910039"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 506661505.1111111,
            "unit": "ns",
            "range": "± 5191258.943175332"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7389608,
            "unit": "ns",
            "range": "± 534949.1100992692"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 143381.6,
            "unit": "ns",
            "range": "± 14665.681135070256"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 192674.22222222222,
            "unit": "ns",
            "range": "± 30342.40407325109"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8655866.6,
            "unit": "ns",
            "range": "± 435496.0089811897"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5651356.2,
            "unit": "ns",
            "range": "± 46711.78192514414"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5073765141.1,
            "unit": "ns",
            "range": "± 21517144.654679567"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9558068.666666666,
            "unit": "ns",
            "range": "± 461998.487403368"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1663237.3333333333,
            "unit": "ns",
            "range": "± 212985.1383753101"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1943059.6666666667,
            "unit": "ns",
            "range": "± 255307.47870655888"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8549468.4,
            "unit": "ns",
            "range": "± 670061.8573106101"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5502503.3,
            "unit": "ns",
            "range": "± 28313.233262714606"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 505850865.8,
            "unit": "ns",
            "range": "± 2499918.9416948017"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 6602011,
            "unit": "ns",
            "range": "± 145656.02871300743"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 133750.77777777778,
            "unit": "ns",
            "range": "± 18918.446875852267"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 261399.625,
            "unit": "ns",
            "range": "± 4849.975476742125"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 8849484.6,
            "unit": "ns",
            "range": "± 451237.75939894235"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5467903.2,
            "unit": "ns",
            "range": "± 21422.14108917324"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 4927415306.8,
            "unit": "ns",
            "range": "± 65574112.02226621"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 19149799.777777776,
            "unit": "ns",
            "range": "± 1064629.0747950408"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 1510657.2222222222,
            "unit": "ns",
            "range": "± 181952.32534429024"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 5930963.722222222,
            "unit": "ns",
            "range": "± 759913.211064885"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 30780.61111111111,
            "unit": "ns",
            "range": "± 6766.819368145652"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13402.125,
            "unit": "ns",
            "range": "± 306.936213708507"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14306.875,
            "unit": "ns",
            "range": "± 105.29677447237268"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 133757.11111111112,
            "unit": "ns",
            "range": "± 7744.675565258438"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21647.61111111111,
            "unit": "ns",
            "range": "± 574.1549103779494"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 6246.722222222223,
            "unit": "ns",
            "range": "± 440.32169426959246"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 44753.11111111111,
            "unit": "ns",
            "range": "± 10658.293724190149"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 90576.05555555556,
            "unit": "ns",
            "range": "± 35743.1253219102"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 603.9,
            "unit": "ns",
            "range": "± 194.07870682907088"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 397.4,
            "unit": "ns",
            "range": "± 47.954144763513405"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 445.5,
            "unit": "ns",
            "range": "± 13.212548148310702"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 456.22222222222223,
            "unit": "ns",
            "range": "± 133.92047806233535"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1308.125,
            "unit": "ns",
            "range": "± 35.958060888445345"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1527.2,
            "unit": "ns",
            "range": "± 169.34960551737015"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1470.5,
            "unit": "ns",
            "range": "± 150.5515784632555"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 712.6111111111111,
            "unit": "ns",
            "range": "± 136.6056774483078"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 643.5,
            "unit": "ns",
            "range": "± 61.548761157313315"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 619,
            "unit": "ns",
            "range": "± 39.668767451373014"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 1724.5555555555557,
            "unit": "ns",
            "range": "± 103.12263465300805"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 219.3,
            "unit": "ns",
            "range": "± 62.749590525445754"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 1919.5555555555557,
            "unit": "ns",
            "range": "± 62.271805640898016"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 4504.625,
            "unit": "ns",
            "range": "± 88.79500870785796"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 6219.3,
            "unit": "ns",
            "range": "± 99.39489144037758"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 1284.75,
            "unit": "ns",
            "range": "± 39.17269457160179"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 562.8,
            "unit": "ns",
            "range": "± 61.255475945692666"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 3555.222222222222,
            "unit": "ns",
            "range": "± 146.14870661228736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 3545.1,
            "unit": "ns",
            "range": "± 390.31851665587743"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 505.6666666666667,
            "unit": "ns",
            "range": "± 102.28758477938563"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 349.1666666666667,
            "unit": "ns",
            "range": "± 29.5423424934449"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 451.2,
            "unit": "ns",
            "range": "± 36.97987440637287"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 397.5,
            "unit": "ns",
            "range": "± 44.246065200357485"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1426.5,
            "unit": "ns",
            "range": "± 64.21837743200929"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1340.9444444444443,
            "unit": "ns",
            "range": "± 50.638698421047295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1803.611111111111,
            "unit": "ns",
            "range": "± 131.21684766489062"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 792.1666666666666,
            "unit": "ns",
            "range": "± 284.37079667223213"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 627.5555555555555,
            "unit": "ns",
            "range": "± 18.688974765293512"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 748.5,
            "unit": "ns",
            "range": "± 46.61698035204401"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 2176.1111111111113,
            "unit": "ns",
            "range": "± 104.2010609884137"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 166.9,
            "unit": "ns",
            "range": "± 22.639444437627976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 2344.8,
            "unit": "ns",
            "range": "± 105.81724287132457"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4721.5,
            "unit": "ns",
            "range": "± 401.0079661164961"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 5562.25,
            "unit": "ns",
            "range": "± 143.44809912598055"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 1345.625,
            "unit": "ns",
            "range": "± 25.427417710584994"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 492.55555555555554,
            "unit": "ns",
            "range": "± 35.57425723437916"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 3701.5,
            "unit": "ns",
            "range": "± 123.56846954893739"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3488.5,
            "unit": "ns",
            "range": "± 161.09092374893663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 473.1666666666667,
            "unit": "ns",
            "range": "± 30.57776970284131"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 386.7,
            "unit": "ns",
            "range": "± 30.14336116331791"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 456.5,
            "unit": "ns",
            "range": "± 47.331572737209754"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 336.6,
            "unit": "ns",
            "range": "± 30.94870307819412"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1959.8,
            "unit": "ns",
            "range": "± 75.64985128868398"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1710.8,
            "unit": "ns",
            "range": "± 153.8684864132715"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 1595.5,
            "unit": "ns",
            "range": "± 31.874754901018456"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 704.75,
            "unit": "ns",
            "range": "± 84.67374361124502"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 660.25,
            "unit": "ns",
            "range": "± 49.381170500505554"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 649.625,
            "unit": "ns",
            "range": "± 21.53361160073778"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 2110.4,
            "unit": "ns",
            "range": "± 307.9935785766392"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 141.9,
            "unit": "ns",
            "range": "± 22.717834403833475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 2356.1,
            "unit": "ns",
            "range": "± 80.87638029047102"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 5197.125,
            "unit": "ns",
            "range": "± 39.12776690353211"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 6584.611111111111,
            "unit": "ns",
            "range": "± 132.4107665981551"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 1824.4,
            "unit": "ns",
            "range": "± 152.1384238120009"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 559.5555555555555,
            "unit": "ns",
            "range": "± 44.78311933952098"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3856.625,
            "unit": "ns",
            "range": "± 66.4722014937716"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 4247.5,
            "unit": "ns",
            "range": "± 229.73958687561398"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "id": "60d28cbceda6907ececf765e13fa6570738efc6e",
          "message": "Implement lazy deserialization for ConsumeResult Key/Value\n\n- ConsumeResult now stores raw bytes and deserializes lazily on property access\n- Key and Value are only deserialized when accessed, not when consuming\n- Reduces allocations significantly when Key/Value aren't accessed\n- Remove unused DeserializeKey/DeserializeValue methods from KafkaConsumer\n\nThis matches Confluent.Kafka's approach where deserialization happens on-demand,\nreducing allocations from ~320KB to potentially ~17KB for 1000 messages.\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-23T18:22:38Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/60d28cbceda6907ececf765e13fa6570738efc6e"
        },
        "date": 1769193696321,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3025201832.6,
            "unit": "ns",
            "range": "± 4187454.507252264"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3177500709.2,
            "unit": "ns",
            "range": "± 1444077.2962233357"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3019891978.6,
            "unit": "ns",
            "range": "± 2168887.1499889754"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3181004474,
            "unit": "ns",
            "range": "± 1747030.1588225374"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3018906634.75,
            "unit": "ns",
            "range": "± 958741.0605853125"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3176964602.6,
            "unit": "ns",
            "range": "± 3297350.4543274282"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3016808092,
            "unit": "ns",
            "range": "± 808401.4895223783"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3180409544.4,
            "unit": "ns",
            "range": "± 603309.6294953363"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3017171137.75,
            "unit": "ns",
            "range": "± 1764850.699034978"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3176297802.2,
            "unit": "ns",
            "range": "± 880552.3752390882"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3016136025.25,
            "unit": "ns",
            "range": "± 1028279.029739942"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3182050642.7,
            "unit": "ns",
            "range": "± 754603.9036260679"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3021535623.25,
            "unit": "ns",
            "range": "± 1519165.9957795648"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3163318275.25,
            "unit": "ns",
            "range": "± 25613119.845453598"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3018864048.4,
            "unit": "ns",
            "range": "± 1817254.9586412744"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3181221120.6,
            "unit": "ns",
            "range": "± 2335675.592560491"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8570322.9,
            "unit": "ns",
            "range": "± 510799.0452591465"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 6855697.222222222,
            "unit": "ns",
            "range": "± 407297.3266422755"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 507122269.3333333,
            "unit": "ns",
            "range": "± 4896629.706778888"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7809945.2,
            "unit": "ns",
            "range": "± 897349.9155058744"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 133235.38888888888,
            "unit": "ns",
            "range": "± 15632.73938281807"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 203106,
            "unit": "ns",
            "range": "± 27557.49015694281"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8741564.5,
            "unit": "ns",
            "range": "± 448912.7318357347"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5625545.875,
            "unit": "ns",
            "range": "± 30786.042256829405"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 4983039076,
            "unit": "ns",
            "range": "± 77693971.27020729"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9332841.055555556,
            "unit": "ns",
            "range": "± 456759.07522377453"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1403268.0555555555,
            "unit": "ns",
            "range": "± 218851.3611113209"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1895165.25,
            "unit": "ns",
            "range": "± 62748.08661795422"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 9216869.5,
            "unit": "ns",
            "range": "± 650354.5899357519"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5496893.333333333,
            "unit": "ns",
            "range": "± 33237.606885875524"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 500451492.8,
            "unit": "ns",
            "range": "± 5553818.313133067"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 6571210.6,
            "unit": "ns",
            "range": "± 140728.43290808957"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 141843.22222222222,
            "unit": "ns",
            "range": "± 16699.691791899764"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 261100.72222222222,
            "unit": "ns",
            "range": "± 7192.623821975152"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 9362760.5,
            "unit": "ns",
            "range": "± 648086.6235399315"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5503680.833333333,
            "unit": "ns",
            "range": "± 35741.66112535902"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 4990234807.9,
            "unit": "ns",
            "range": "± 42776882.44532613"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 19037420.777777776,
            "unit": "ns",
            "range": "± 1460533.503092173"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 1462300.2222222222,
            "unit": "ns",
            "range": "± 209201.0536342837"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 5833925.888888889,
            "unit": "ns",
            "range": "± 1469216.0087114016"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 30595.777777777777,
            "unit": "ns",
            "range": "± 6447.279538258323"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13661.75,
            "unit": "ns",
            "range": "± 170.84307753859136"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14777.1,
            "unit": "ns",
            "range": "± 289.636342870005"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 135010.66666666666,
            "unit": "ns",
            "range": "± 8479.766388291602"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21667.666666666668,
            "unit": "ns",
            "range": "± 764.1145202127755"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 6091.777777777777,
            "unit": "ns",
            "range": "± 608.357373954195"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 41156.444444444445,
            "unit": "ns",
            "range": "± 7410.8975858378835"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 86609.11111111111,
            "unit": "ns",
            "range": "± 35290.483265479816"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 397.3,
            "unit": "ns",
            "range": "± 28.186087978922426"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 355.3888888888889,
            "unit": "ns",
            "range": "± 30.636760780329094"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 458.8,
            "unit": "ns",
            "range": "± 23.319281483117976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 398.1111111111111,
            "unit": "ns",
            "range": "± 67.05491116324822"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1335.6,
            "unit": "ns",
            "range": "± 65.3918955223046"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1329.5,
            "unit": "ns",
            "range": "± 62.547759529996135"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1482.4444444444443,
            "unit": "ns",
            "range": "± 68.777014894351"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 605.75,
            "unit": "ns",
            "range": "± 49.542910693660296"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 660.1666666666666,
            "unit": "ns",
            "range": "± 44.61782155148322"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 606.7,
            "unit": "ns",
            "range": "± 51.40049718739218"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 1745,
            "unit": "ns",
            "range": "± 62.034264725230685"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 197.9,
            "unit": "ns",
            "range": "± 29.12978620663812"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 2272.6,
            "unit": "ns",
            "range": "± 218.14480409937698"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 4658.2,
            "unit": "ns",
            "range": "± 269.221841610223"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 5527.375,
            "unit": "ns",
            "range": "± 135.2330660114498"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 1431.7,
            "unit": "ns",
            "range": "± 98.71710422549208"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 514,
            "unit": "ns",
            "range": "± 22.02839077191069"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 3155.4444444444443,
            "unit": "ns",
            "range": "± 704.8345747604736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 4718.6,
            "unit": "ns",
            "range": "± 348.43274498499386"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 395.1,
            "unit": "ns",
            "range": "± 33.13423875355789"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 361.3,
            "unit": "ns",
            "range": "± 27.174538736757903"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 425.77777777777777,
            "unit": "ns",
            "range": "± 64.8628895782823"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 343.7,
            "unit": "ns",
            "range": "± 21.83167932666249"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1443.6666666666667,
            "unit": "ns",
            "range": "± 64.22616289332565"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1321.7777777777778,
            "unit": "ns",
            "range": "± 38.45054543754151"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1668.3,
            "unit": "ns",
            "range": "± 204.78324584247068"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 647.7,
            "unit": "ns",
            "range": "± 85.20178662706813"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 629.6,
            "unit": "ns",
            "range": "± 62.24003713223686"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 689.6666666666666,
            "unit": "ns",
            "range": "± 81.15263396834386"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 1764,
            "unit": "ns",
            "range": "± 54.55534542357618"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 171.88888888888889,
            "unit": "ns",
            "range": "± 16.335884154557142"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 2101.4,
            "unit": "ns",
            "range": "± 212.32061709698482"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4700.125,
            "unit": "ns",
            "range": "± 198.54250174710702"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 5929.7,
            "unit": "ns",
            "range": "± 439.4784914469017"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 1361.125,
            "unit": "ns",
            "range": "± 32.72586351583626"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 643.2,
            "unit": "ns",
            "range": "± 143.00508926452778"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 3272.3,
            "unit": "ns",
            "range": "± 412.0600819190219"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 4284.875,
            "unit": "ns",
            "range": "± 210.8441790923877"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 470,
            "unit": "ns",
            "range": "± 44.04858428800837"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 385.4,
            "unit": "ns",
            "range": "± 48.28434298794774"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 515.75,
            "unit": "ns",
            "range": "± 19.20379426794909"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 355.4,
            "unit": "ns",
            "range": "± 53.26287011918652"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1532.625,
            "unit": "ns",
            "range": "± 36.80426023476863"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1540.3333333333333,
            "unit": "ns",
            "range": "± 39.68626966596886"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 1867.8,
            "unit": "ns",
            "range": "± 226.1607343854759"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 590.7777777777778,
            "unit": "ns",
            "range": "± 45.168511647434705"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 632.5,
            "unit": "ns",
            "range": "± 83.29499117927533"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 612,
            "unit": "ns",
            "range": "± 37.17824931626316"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 1748.9444444444443,
            "unit": "ns",
            "range": "± 59.44979207514336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 165.33333333333334,
            "unit": "ns",
            "range": "± 25.622255950637918"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 2372.722222222222,
            "unit": "ns",
            "range": "± 220.16742366763626"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 5743.5,
            "unit": "ns",
            "range": "± 1301.0818575324151"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 6683.722222222223,
            "unit": "ns",
            "range": "± 338.8863296806828"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 1527.75,
            "unit": "ns",
            "range": "± 58.26233774918408"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 478.8333333333333,
            "unit": "ns",
            "range": "± 16.955824957813167"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3548.6,
            "unit": "ns",
            "range": "± 436.8448618598292"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 4362.9,
            "unit": "ns",
            "range": "± 223.11504257271005"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "id": "f81f49010b76258fe7f379aa02357ac457710818",
          "message": "Fix benchmark workflow: filter null Statistics and improve summary\n\n- Filter out benchmarks with null Statistics before passing to\n  github-action-benchmark (fixes 'Cannot read properties of null' error)\n- Rewrite summary generation in Python for cleaner output\n- Auto-format time units (ns/µs/ms/s) based on magnitude\n- Auto-format memory (B/KB/MB) with zero-allocation highlighting\n- Group benchmarks by category and parameters for easier comparison\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-25T14:50:29Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/f81f49010b76258fe7f379aa02357ac457710818"
        },
        "date": 1769353378588,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 131.31027054786682,
            "unit": "ns",
            "range": "± 0.46019265694869144"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 123.40660084618463,
            "unit": "ns",
            "range": "± 0.5728347577146982"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 144794.29604492188,
            "unit": "ns",
            "range": "± 970.5720833802912"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 126.89117626349132,
            "unit": "ns",
            "range": "± 0.5956763666146911"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3015422098.4,
            "unit": "ns",
            "range": "± 786170.3998111987"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3173314262.4,
            "unit": "ns",
            "range": "± 574939.8990792864"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3014506480.8,
            "unit": "ns",
            "range": "± 570528.8491476132"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3179775419.75,
            "unit": "ns",
            "range": "± 350691.37184745504"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3014177805.5,
            "unit": "ns",
            "range": "± 637904.2426403041"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3172667690.5,
            "unit": "ns",
            "range": "± 223442.88914694363"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3012661648.8,
            "unit": "ns",
            "range": "± 700481.8582587988"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3178503120,
            "unit": "ns",
            "range": "± 220944.00812875645"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3013893450,
            "unit": "ns",
            "range": "± 515180.90280017175"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3173639358.8,
            "unit": "ns",
            "range": "± 1234688.815276829"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3012638585.25,
            "unit": "ns",
            "range": "± 203666.8979639303"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3178910835.5,
            "unit": "ns",
            "range": "± 1437461.883584744"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015594399.5,
            "unit": "ns",
            "range": "± 692630.1486031343"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174199908.5,
            "unit": "ns",
            "range": "± 647724.878083666"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014395925.25,
            "unit": "ns",
            "range": "± 345058.4871519368"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177872674.4,
            "unit": "ns",
            "range": "± 973892.210299374"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8622587.5,
            "unit": "ns",
            "range": "± 496003.0084548659"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 6128014.111111111,
            "unit": "ns",
            "range": "± 108169.06180886988"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8788972.1,
            "unit": "ns",
            "range": "± 425056.24181655195"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7443984,
            "unit": "ns",
            "range": "± 59048.22799083088"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 156649.44444444444,
            "unit": "ns",
            "range": "± 5712.220695822053"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8764356.8,
            "unit": "ns",
            "range": "± 394143.06044268014"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5740276.6,
            "unit": "ns",
            "range": "± 28127.600257872455"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 12572930.875,
            "unit": "ns",
            "range": "± 1487273.951622319"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8922565.444444444,
            "unit": "ns",
            "range": "± 341954.3096921397"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1863558.7222222222,
            "unit": "ns",
            "range": "± 94071.17889632533"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8672600.2,
            "unit": "ns",
            "range": "± 588460.9794277499"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5599094.4,
            "unit": "ns",
            "range": "± 29092.42620874222"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8772294.3,
            "unit": "ns",
            "range": "± 464348.769634552"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 7284174,
            "unit": "ns",
            "range": "± 421748.8898348031"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 254544.11111111112,
            "unit": "ns",
            "range": "± 17585.614848822068"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 8849863,
            "unit": "ns",
            "range": "± 311448.4735022615"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5577630.722222222,
            "unit": "ns",
            "range": "± 36958.508454677176"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 47708306.7,
            "unit": "ns",
            "range": "± 2807097.1148946653"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 20715976.4,
            "unit": "ns",
            "range": "± 1617642.1073057882"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 3446412.8333333335,
            "unit": "ns",
            "range": "± 707494.028791763"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20693.666666666668,
            "unit": "ns",
            "range": "± 5443.297277386198"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 10436.25,
            "unit": "ns",
            "range": "± 121.82510883581078"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 10868.555555555555,
            "unit": "ns",
            "range": "± 458.6624878685609"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 106379.88888888889,
            "unit": "ns",
            "range": "± 14819.850230724705"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 17682.6,
            "unit": "ns",
            "range": "± 271.55200034043"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5863.666666666667,
            "unit": "ns",
            "range": "± 84.26149773176358"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 25925.88888888889,
            "unit": "ns",
            "range": "± 5541.492949658162"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 78181.33333333333,
            "unit": "ns",
            "range": "± 38394.76837083407"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 70212.55555555556,
            "unit": "ns",
            "range": "± 5734.780229248352"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 357.4,
            "unit": "ns",
            "range": "± 38.3903923165969"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 423.3,
            "unit": "ns",
            "range": "± 19.98360439071101"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 436.1,
            "unit": "ns",
            "range": "± 24.703575989452755"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 476.7,
            "unit": "ns",
            "range": "± 152.71764068953453"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1266.5,
            "unit": "ns",
            "range": "± 41.20332857566589"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1537.5,
            "unit": "ns",
            "range": "± 107.16679626741775"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1395,
            "unit": "ns",
            "range": "± 232.55441418204805"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 540.3,
            "unit": "ns",
            "range": "± 41.69678911165969"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 467.5,
            "unit": "ns",
            "range": "± 29.556913084947"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 609.6,
            "unit": "ns",
            "range": "± 32.11853047696921"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 1671.6666666666667,
            "unit": "ns",
            "range": "± 23.680160472429236"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 222,
            "unit": "ns",
            "range": "± 15.246128834705695"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 1850.5,
            "unit": "ns",
            "range": "± 38.21358106456103"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 4126.5,
            "unit": "ns",
            "range": "± 117.21408984052582"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 5098.888888888889,
            "unit": "ns",
            "range": "± 86.0529552723851"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 1248.9,
            "unit": "ns",
            "range": "± 37.24528307196914"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 459.25,
            "unit": "ns",
            "range": "± 12.758750498607387"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 2862.9,
            "unit": "ns",
            "range": "± 59.78191848525587"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 3323.5555555555557,
            "unit": "ns",
            "range": "± 138.5407080167334"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 94728.05555555556,
            "unit": "ns",
            "range": "± 8729.841609546978"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 111692.33333333333,
            "unit": "ns",
            "range": "± 15768.397659559452"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 431.3,
            "unit": "ns",
            "range": "± 18.637179090314188"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 314.5,
            "unit": "ns",
            "range": "± 27.162065049951153"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 404.55555555555554,
            "unit": "ns",
            "range": "± 22.89711286991829"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 314.3,
            "unit": "ns",
            "range": "± 34.016499264654236"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1254.111111111111,
            "unit": "ns",
            "range": "± 59.83402970811101"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1106,
            "unit": "ns",
            "range": "± 22.085062825357777"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1337.5,
            "unit": "ns",
            "range": "± 21.213203435596427"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 473.8,
            "unit": "ns",
            "range": "± 35.54980856332266"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 484.875,
            "unit": "ns",
            "range": "± 20.673913859879416"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 486.25,
            "unit": "ns",
            "range": "± 56.03761491814481"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 1547.25,
            "unit": "ns",
            "range": "± 17.814520562090433"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 176.6,
            "unit": "ns",
            "range": "± 163.2016135541149"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 1643.5555555555557,
            "unit": "ns",
            "range": "± 34.53661503068559"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 3882,
            "unit": "ns",
            "range": "± 78.83255310194785"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 4993.75,
            "unit": "ns",
            "range": "± 197.51726868446573"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 1626.4,
            "unit": "ns",
            "range": "± 181.4718833440719"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 494.77777777777777,
            "unit": "ns",
            "range": "± 36.39291750388315"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 2892.722222222222,
            "unit": "ns",
            "range": "± 59.953685828683156"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3575.3,
            "unit": "ns",
            "range": "± 259.1563449177178"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 81655.5,
            "unit": "ns",
            "range": "± 5915.768589118408"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 109494,
            "unit": "ns",
            "range": "± 5095.207103599561"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 390.3333333333333,
            "unit": "ns",
            "range": "± 20.56088519495209"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 306.2,
            "unit": "ns",
            "range": "± 31.19401652026662"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 373.25,
            "unit": "ns",
            "range": "± 11.597413504743201"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 324.4,
            "unit": "ns",
            "range": "± 29.870833042730276"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1165.1666666666667,
            "unit": "ns",
            "range": "± 39.102429592034305"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1255.5,
            "unit": "ns",
            "range": "± 45.27692569068709"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 1162.9,
            "unit": "ns",
            "range": "± 60.667032965927135"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 505.5,
            "unit": "ns",
            "range": "± 20.43106877842105"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 567.4,
            "unit": "ns",
            "range": "± 19.530318311111404"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 580.9,
            "unit": "ns",
            "range": "± 24.542253813010372"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 1843.5,
            "unit": "ns",
            "range": "± 246.07270831560695"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 117.4,
            "unit": "ns",
            "range": "± 23.48734884050466"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 1801.8,
            "unit": "ns",
            "range": "± 37.54641571885835"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 4255.5,
            "unit": "ns",
            "range": "± 125.76167937809991"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 5359.444444444444,
            "unit": "ns",
            "range": "± 118.98646048092101"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 1327.625,
            "unit": "ns",
            "range": "± 73.42719912558678"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 521.7222222222222,
            "unit": "ns",
            "range": "± 21.78748366481188"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3059.9444444444443,
            "unit": "ns",
            "range": "± 93.95625459636935"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 3413,
            "unit": "ns",
            "range": "± 58.65151319446072"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 128839.77777777778,
            "unit": "ns",
            "range": "± 12802.91493935832"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 106245.44444444444,
            "unit": "ns",
            "range": "± 6464.584308196296"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Tom Longhurst",
            "username": "thomhurst",
            "email": "30480171+thomhurst@users.noreply.github.com"
          },
          "committer": {
            "name": "GitHub",
            "username": "web-flow",
            "email": "noreply@github.com"
          },
          "id": "b887c51def7b026f9d948372f823a0f2162563d6",
          "message": "Merge pull request #107 from thomhurst/fix/zero-copy-record-batch-parsing\n\nImplement zero-copy parsing for RecordBatch to reduce consumer allocations",
          "timestamp": "2026-01-25T16:49:53Z",
          "url": "https://github.com/thomhurst/Dekaf/commit/b887c51def7b026f9d948372f823a0f2162563d6"
        },
        "date": 1769360557879,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 136.80167751312257,
            "unit": "ns",
            "range": "± 2.016320814686047"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 128.49439392089843,
            "unit": "ns",
            "range": "± 2.26174875989903"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 149647.99528808595,
            "unit": "ns",
            "range": "± 1347.1238408070315"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 130.42901945114136,
            "unit": "ns",
            "range": "± 2.0014442036130515"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3016068318.6,
            "unit": "ns",
            "range": "± 1153424.0404104642"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3174533497.2,
            "unit": "ns",
            "range": "± 814164.9944579416"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3013663710.6,
            "unit": "ns",
            "range": "± 410830.3946938444"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3179912854.75,
            "unit": "ns",
            "range": "± 713488.835176078"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3013872393.4,
            "unit": "ns",
            "range": "± 559602.9867033414"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3172448211,
            "unit": "ns",
            "range": "± 1094559.7020176651"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3013077961.4,
            "unit": "ns",
            "range": "± 721378.0043949219"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3179210328,
            "unit": "ns",
            "range": "± 823770.1686535511"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3013976376.8,
            "unit": "ns",
            "range": "± 485524.33834844985"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3173656169.9,
            "unit": "ns",
            "range": "± 294522.41602516436"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3013015718.25,
            "unit": "ns",
            "range": "± 111754.49605683284"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3179339406,
            "unit": "ns",
            "range": "± 768906.6210330745"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014754661.25,
            "unit": "ns",
            "range": "± 499612.98067295714"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3175164201,
            "unit": "ns",
            "range": "± 578336.5414501318"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013334814.25,
            "unit": "ns",
            "range": "± 330395.06359949854"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178743469.5,
            "unit": "ns",
            "range": "± 179870.707875129"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8840092,
            "unit": "ns",
            "range": "± 693785.6702391957"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 6235039.3,
            "unit": "ns",
            "range": "± 91374.13238718665"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 11533703.9,
            "unit": "ns",
            "range": "± 2437935.953260848"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 7647735.4,
            "unit": "ns",
            "range": "± 263827.5320740174"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 171253.2,
            "unit": "ns",
            "range": "± 19560.74796229542"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8859011.7,
            "unit": "ns",
            "range": "± 293181.05567798123"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5832654.3,
            "unit": "ns",
            "range": "± 75185.50195763956"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 13891276.1,
            "unit": "ns",
            "range": "± 4383062.088901629"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9403273.388888888,
            "unit": "ns",
            "range": "± 418318.74208474223"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1764346.0555555555,
            "unit": "ns",
            "range": "± 219997.0907751459"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8809835.7,
            "unit": "ns",
            "range": "± 609866.7844077006"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5661326.5,
            "unit": "ns",
            "range": "± 85389.27016155029"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8245725.5,
            "unit": "ns",
            "range": "± 509002.25373857084"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 7146695.2,
            "unit": "ns",
            "range": "± 373685.70531034353"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 255262.5,
            "unit": "ns",
            "range": "± 12463.237964968355"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 8683936.1,
            "unit": "ns",
            "range": "± 542854.207366019"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5611780.6,
            "unit": "ns",
            "range": "± 67517.47008235885"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 46587031.6,
            "unit": "ns",
            "range": "± 4739848.001654769"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 18885565.277777776,
            "unit": "ns",
            "range": "± 642826.0293097927"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 3068293.0555555555,
            "unit": "ns",
            "range": "± 481767.0783877597"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 21022.222222222223,
            "unit": "ns",
            "range": "± 5343.830105312522"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13689.25,
            "unit": "ns",
            "range": "± 91.71968163922071"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 10890.9,
            "unit": "ns",
            "range": "± 370.5372825746119"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 136940.22222222222,
            "unit": "ns",
            "range": "± 11126.249298143759"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 17244.1,
            "unit": "ns",
            "range": "± 154.75819849041923"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 6238.666666666667,
            "unit": "ns",
            "range": "± 152.0115127218988"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 28662,
            "unit": "ns",
            "range": "± 7906.967086057713"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 77435.77777777778,
            "unit": "ns",
            "range": "± 38409.037106447286"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 64985.77777777778,
            "unit": "ns",
            "range": "± 770.2877348396796"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 433.5,
            "unit": "ns",
            "range": "± 38.836194458262774"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 339.1,
            "unit": "ns",
            "range": "± 37.125762244326005"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 407,
            "unit": "ns",
            "range": "± 51.69864601708637"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 400.125,
            "unit": "ns",
            "range": "± 38.61323792246829"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1260.6666666666667,
            "unit": "ns",
            "range": "± 173.03467860518597"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1329,
            "unit": "ns",
            "range": "± 47.89108012517107"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1342.6,
            "unit": "ns",
            "range": "± 42.97402316128508"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 509.8333333333333,
            "unit": "ns",
            "range": "± 28.240042492885877"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 614.2222222222222,
            "unit": "ns",
            "range": "± 42.61389966248623"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 598.625,
            "unit": "ns",
            "range": "± 20.62202359476046"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 1603.6666666666667,
            "unit": "ns",
            "range": "± 150.55729806289696"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 99.8,
            "unit": "ns",
            "range": "± 32.19316697686017"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 1830.7222222222222,
            "unit": "ns",
            "range": "± 79.33753490274603"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 4184.8,
            "unit": "ns",
            "range": "± 75.98983850197048"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 5047.277777777777,
            "unit": "ns",
            "range": "± 99.97221836312549"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 1169.8,
            "unit": "ns",
            "range": "± 96.68597508314109"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 465.77777777777777,
            "unit": "ns",
            "range": "± 64.41812201891983"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 2861.4,
            "unit": "ns",
            "range": "± 40.65149990399425"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 3243.6,
            "unit": "ns",
            "range": "± 75.25246396851955"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 82464.33333333333,
            "unit": "ns",
            "range": "± 16499.326508982118"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 104338.05555555556,
            "unit": "ns",
            "range": "± 8201.985081538358"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 437.2,
            "unit": "ns",
            "range": "± 15.540627757948233"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 391,
            "unit": "ns",
            "range": "± 97.45768312452333"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 407.2,
            "unit": "ns",
            "range": "± 21.09133576719228"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 341.25,
            "unit": "ns",
            "range": "± 26.423744732991302"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1704.8,
            "unit": "ns",
            "range": "± 206.3808776671586"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1321,
            "unit": "ns",
            "range": "± 23.37733945512192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1431.2222222222222,
            "unit": "ns",
            "range": "± 11.38834687057101"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 523.1,
            "unit": "ns",
            "range": "± 27.805674880418845"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 608.2222222222222,
            "unit": "ns",
            "range": "± 20.048552178260767"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 520.5555555555555,
            "unit": "ns",
            "range": "± 23.297591673342072"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 1538.625,
            "unit": "ns",
            "range": "± 105.41880761989295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 146.8,
            "unit": "ns",
            "range": "± 21.616865843338367"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 1763.8,
            "unit": "ns",
            "range": "± 33.03466193090053"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4292,
            "unit": "ns",
            "range": "± 135.87678241701192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 5193.3,
            "unit": "ns",
            "range": "± 107.08153694991286"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 1138,
            "unit": "ns",
            "range": "± 39.9177726260944"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 478,
            "unit": "ns",
            "range": "± 43.30992957740754"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 2888.3888888888887,
            "unit": "ns",
            "range": "± 63.83659695747504"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3396.8,
            "unit": "ns",
            "range": "± 94.03049150851724"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 76618.66666666667,
            "unit": "ns",
            "range": "± 4785.649433462506"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 108372.875,
            "unit": "ns",
            "range": "± 5664.912795634445"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 435.44444444444446,
            "unit": "ns",
            "range": "± 11.52292401162907"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 379.94444444444446,
            "unit": "ns",
            "range": "± 44.7524052736585"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 449.5,
            "unit": "ns",
            "range": "± 22.416511771459895"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 362,
            "unit": "ns",
            "range": "± 77.90449994134556"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1319.6666666666667,
            "unit": "ns",
            "range": "± 43.58898943540674"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1305.2222222222222,
            "unit": "ns",
            "range": "± 31.135902820449004"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 1263.125,
            "unit": "ns",
            "range": "± 54.85158546998827"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 586.7,
            "unit": "ns",
            "range": "± 41.448897586197766"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 544.25,
            "unit": "ns",
            "range": "± 9.09866552224634"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 609.6111111111111,
            "unit": "ns",
            "range": "± 26.633833954410527"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 1639,
            "unit": "ns",
            "range": "± 60.76594440967737"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 139.5,
            "unit": "ns",
            "range": "± 19.329022507905336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 1683.8333333333333,
            "unit": "ns",
            "range": "± 99.36422897602537"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 4498,
            "unit": "ns",
            "range": "± 26.853512081497108"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 5783.222222222223,
            "unit": "ns",
            "range": "± 255.8172090467028"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 1345.9444444444443,
            "unit": "ns",
            "range": "± 51.810016191637864"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 434.22222222222223,
            "unit": "ns",
            "range": "± 29.10230995031914"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3034.5,
            "unit": "ns",
            "range": "± 44.654227123532216"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 3177.777777777778,
            "unit": "ns",
            "range": "± 117.53486480378682"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 88025.16666666667,
            "unit": "ns",
            "range": "± 8109.101907733063"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 114326.66666666667,
            "unit": "ns",
            "range": "± 7478.344485913978"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "0c29e44f7c7eaeb1fc9026cb263df865b343a218",
          "message": "Merge pull request #109 from thomhurst/perf/pooled-value-task-source\n\nReplace TaskCompletionSource with IValueTaskSource for zero-allocation producer",
          "timestamp": "2026-01-25T18:26:20Z",
          "tree_id": "92f4023b4eb231f9f31c54cd9508a816c3b7e573",
          "url": "https://github.com/thomhurst/Dekaf/commit/0c29e44f7c7eaeb1fc9026cb263df865b343a218"
        },
        "date": 1769366718500,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 139.76664339171515,
            "unit": "ns",
            "range": "± 0.47778622150565353"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 133.37078680992127,
            "unit": "ns",
            "range": "± 0.6157799656080404"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 149925.70161946616,
            "unit": "ns",
            "range": "± 448.54726396138443"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 137.4026644759708,
            "unit": "ns",
            "range": "± 0.7017920818034166"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3015692740.9,
            "unit": "ns",
            "range": "± 544305.5486684844"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3174078189.25,
            "unit": "ns",
            "range": "± 709666.8191047473"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3014435696.7,
            "unit": "ns",
            "range": "± 368342.4469657278"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3180330176.2,
            "unit": "ns",
            "range": "± 283936.6909633202"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3014287203.4,
            "unit": "ns",
            "range": "± 250733.62886597402"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3173370224.4,
            "unit": "ns",
            "range": "± 323075.73305000796"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3013422859.5,
            "unit": "ns",
            "range": "± 593100.9349292918"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3179288829.4,
            "unit": "ns",
            "range": "± 342117.7813382403"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3014254711.4,
            "unit": "ns",
            "range": "± 401375.7080813436"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3174522563.8,
            "unit": "ns",
            "range": "± 574714.0279958373"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3013186596,
            "unit": "ns",
            "range": "± 570251.6990759431"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3180303455.3,
            "unit": "ns",
            "range": "± 782490.8172532761"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015194941.75,
            "unit": "ns",
            "range": "± 153309.9252785568"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174737830.6,
            "unit": "ns",
            "range": "± 300471.132723761"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.DekafPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014138782.6,
            "unit": "ns",
            "range": "± 575365.0798478301"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConfluentPollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178849449.6,
            "unit": "ns",
            "range": "± 549204.4175617126"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8933812,
            "unit": "ns",
            "range": "± 498551.6685750836"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 100)",
            "value": 6711286.166666667,
            "unit": "ns",
            "range": "± 86557.2888915197"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 13143966,
            "unit": "ns",
            "range": "± 1342131.6589324942"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 100)",
            "value": 8011906.9,
            "unit": "ns",
            "range": "± 227211.2899474848"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 62991.16632080078,
            "unit": "ns",
            "range": "± 6669.769473991142"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 134039.11456298828,
            "unit": "ns",
            "range": "± 1533.3848801313256"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8624551.5,
            "unit": "ns",
            "range": "± 538641.3086384244"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 5519075.9,
            "unit": "ns",
            "range": "± 43326.57768673225"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 9066785.5,
            "unit": "ns",
            "range": "± 461826.17399035697"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 100, BatchSize: 1000)",
            "value": 8818496.166666666,
            "unit": "ns",
            "range": "± 259824.78311306256"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 578781.3978515625,
            "unit": "ns",
            "range": "± 4898.6436236135005"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentFireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1356973.5029296875,
            "unit": "ns",
            "range": "± 10644.033504376339"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8788906.333333334,
            "unit": "ns",
            "range": "± 442460.01614609204"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 5508161.4,
            "unit": "ns",
            "range": "± 36331.2863680266"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 8713863,
            "unit": "ns",
            "range": "± 239799.75375456497"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 100)",
            "value": 6558874.5,
            "unit": "ns",
            "range": "± 475718.3054900009"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 64780.498779296875,
            "unit": "ns",
            "range": "± 3285.244604609949"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 8972592.9,
            "unit": "ns",
            "range": "± 576195.4268152141"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentSingleProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 5508462.2,
            "unit": "ns",
            "range": "± 47824.37000119407"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 36464007.4,
            "unit": "ns",
            "range": "± 2317048.8097897386"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.ConfluentBatchProduce(MessageSize: 1000, BatchSize: 1000)",
            "value": 12587482.333333334,
            "unit": "ns",
            "range": "± 452247.2362240039"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.DekafFireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 635937.248828125,
            "unit": "ns",
            "range": "± 46071.654263532364"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 34230.666666666664,
            "unit": "ns",
            "range": "± 10166.869208364982"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13692.277777777777,
            "unit": "ns",
            "range": "± 251.39897462886447"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 10883.3,
            "unit": "ns",
            "range": "± 280.180021494118"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 93407.88888888889,
            "unit": "ns",
            "range": "± 45539.40121050244"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 17584.833333333332,
            "unit": "ns",
            "range": "± 672.8027199707207"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 6755.944444444444,
            "unit": "ns",
            "range": "± 210.99828856599237"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 26079.11111111111,
            "unit": "ns",
            "range": "± 5468.05338864857"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 78424.44444444444,
            "unit": "ns",
            "range": "± 35841.20919804154"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 56598,
            "unit": "ns",
            "range": "± 1664.2038851741006"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 10)",
            "value": 489.3,
            "unit": "ns",
            "range": "± 50.78724686813063"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 10)",
            "value": 679.1111111111111,
            "unit": "ns",
            "range": "± 198.9858314330724"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 10)",
            "value": 697.5,
            "unit": "ns",
            "range": "± 147.3745228999911"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 10)",
            "value": 510.4,
            "unit": "ns",
            "range": "± 155.33490700204297"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 10)",
            "value": 1030,
            "unit": "ns",
            "range": "± 31.959796173138706"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 10)",
            "value": 1722,
            "unit": "ns",
            "range": "± 235.7908677903649"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 10)",
            "value": 1766.1666666666667,
            "unit": "ns",
            "range": "± 239.4039473358783"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 10)",
            "value": 998.5,
            "unit": "ns",
            "range": "± 167.14680839177143"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 10)",
            "value": 722.2,
            "unit": "ns",
            "range": "± 248.9617552066091"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 10)",
            "value": 807.6,
            "unit": "ns",
            "range": "± 167.4484663676825"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 10)",
            "value": 1533.111111111111,
            "unit": "ns",
            "range": "± 176.84770598204295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 10)",
            "value": 307.27777777777777,
            "unit": "ns",
            "range": "± 122.5293615605845"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 10)",
            "value": 1493.4444444444443,
            "unit": "ns",
            "range": "± 169.4270573957353"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 10)",
            "value": 3951.1,
            "unit": "ns",
            "range": "± 197.5170597414028"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 10)",
            "value": 4938.833333333333,
            "unit": "ns",
            "range": "± 88.5395391901268"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 10)",
            "value": 1186.2777777777778,
            "unit": "ns",
            "range": "± 24.36071518745795"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 10)",
            "value": 512.4,
            "unit": "ns",
            "range": "± 80.29418133062667"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 10)",
            "value": 2865.875,
            "unit": "ns",
            "range": "± 85.62616005470692"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 10)",
            "value": 3572.7,
            "unit": "ns",
            "range": "± 424.6166375554412"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 82973.77777777778,
            "unit": "ns",
            "range": "± 6397.808468096278"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 107205.66666666667,
            "unit": "ns",
            "range": "± 8484.998850913298"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 100)",
            "value": 374.25,
            "unit": "ns",
            "range": "± 11.58508893855743"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 100)",
            "value": 465.1,
            "unit": "ns",
            "range": "± 137.57135522258176"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 100)",
            "value": 457.3,
            "unit": "ns",
            "range": "± 37.80961077116064"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 100)",
            "value": 738.8,
            "unit": "ns",
            "range": "± 98.12667549878803"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 100)",
            "value": 1399.7222222222222,
            "unit": "ns",
            "range": "± 110.88031585653265"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 100)",
            "value": 1422.2222222222222,
            "unit": "ns",
            "range": "± 274.9367098887386"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 100)",
            "value": 1451.888888888889,
            "unit": "ns",
            "range": "± 49.103066208854116"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 100)",
            "value": 653.25,
            "unit": "ns",
            "range": "± 37.79928192817575"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 100)",
            "value": 748,
            "unit": "ns",
            "range": "± 127.05641791477255"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 100)",
            "value": 745,
            "unit": "ns",
            "range": "± 43.617020123392706"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 100)",
            "value": 1492.1,
            "unit": "ns",
            "range": "± 296.1277689706853"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 100)",
            "value": 243.22222222222223,
            "unit": "ns",
            "range": "± 39.93049516903647"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 100)",
            "value": 1588.4,
            "unit": "ns",
            "range": "± 126.30404233875934"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 100)",
            "value": 4413.8,
            "unit": "ns",
            "range": "± 531.7237795865235"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 100)",
            "value": 5007.888888888889,
            "unit": "ns",
            "range": "± 651.8610750084032"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 100)",
            "value": 1653.5,
            "unit": "ns",
            "range": "± 232.93883317300273"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 100)",
            "value": 733.1111111111111,
            "unit": "ns",
            "range": "± 128.24824018718974"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 100)",
            "value": 3156.7,
            "unit": "ns",
            "range": "± 289.4980521907224"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 100)",
            "value": 3544.2,
            "unit": "ns",
            "range": "± 219.68806167938314"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 77764.11111111111,
            "unit": "ns",
            "range": "± 5528.272118041144"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 104107.66666666667,
            "unit": "ns",
            "range": "± 5404.44793665366"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt32(StringLength: 1000)",
            "value": 487.22222222222223,
            "unit": "ns",
            "range": "± 44.124193414094776"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt32(StringLength: 1000)",
            "value": 420.6111111111111,
            "unit": "ns",
            "range": "± 7.078920193865101"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteInt64(StringLength: 1000)",
            "value": 484.1,
            "unit": "ns",
            "range": "± 76.63252863141373"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteInt64(StringLength: 1000)",
            "value": 382.3333333333333,
            "unit": "ns",
            "range": "± 9.9498743710662"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteString(StringLength: 1000)",
            "value": 1579.5,
            "unit": "ns",
            "range": "± 67.81698059588658"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineWriteString(StringLength: 1000)",
            "value": 1530.2222222222222,
            "unit": "ns",
            "range": "± 97.01517636145617"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteCompactString(StringLength: 1000)",
            "value": 1464.5,
            "unit": "ns",
            "range": "± 95.4476296195982"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntSmall(StringLength: 1000)",
            "value": 613.8888888888889,
            "unit": "ns",
            "range": "± 12.302890355973716"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntLarge(StringLength: 1000)",
            "value": 618.375,
            "unit": "ns",
            "range": "± 44.921956133467106"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafWriteVarIntNegative(StringLength: 1000)",
            "value": 645.7,
            "unit": "ns",
            "range": "± 49.04204545308263"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt32(StringLength: 1000)",
            "value": 1395.3,
            "unit": "ns",
            "range": "± 125.0644722621985"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.BaselineReadInt32(StringLength: 1000)",
            "value": 151.875,
            "unit": "ns",
            "range": "± 20.9655329392928"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadInt64(StringLength: 1000)",
            "value": 1473.5,
            "unit": "ns",
            "range": "± 73.09875902796553"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadString(StringLength: 1000)",
            "value": 4128.1,
            "unit": "ns",
            "range": "± 278.17148987230485"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafReadFullSequence(StringLength: 1000)",
            "value": 6134.388888888889,
            "unit": "ns",
            "range": "± 252.89891480809305"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeString(StringLength: 1000)",
            "value": 1362.111111111111,
            "unit": "ns",
            "range": "± 122.66564764069487"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafSerializeInt32(StringLength: 1000)",
            "value": 453.5,
            "unit": "ns",
            "range": "± 44.129679031560734"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeString(StringLength: 1000)",
            "value": 3245.777777777778,
            "unit": "ns",
            "range": "± 84.72127504024267"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DekafDeserializeInt32(StringLength: 1000)",
            "value": 3447.625,
            "unit": "ns",
            "range": "± 173.057742221656"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 124963.11111111111,
            "unit": "ns",
            "range": "± 18819.03142728422"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 121976,
            "unit": "ns",
            "range": "± 16034.069087415084"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b43dc73ee986689f4526cfb5d55ccac43d6a223c",
          "message": "Merge pull request #110 from thomhurst/perf/networking-pooled-pending-request\n\nReplace TaskCompletionSource with IValueTaskSource for zero-allocation networking",
          "timestamp": "2026-01-25T21:06:50Z",
          "tree_id": "712aeaf5cd2879f120a8585f584216dc73cde058",
          "url": "https://github.com/thomhurst/Dekaf/commit/b43dc73ee986689f4526cfb5d55ccac43d6a223c"
        },
        "date": 1769376357811,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 135.31778303782144,
            "unit": "ns",
            "range": "± 0.664931937141597"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 127.62734904289246,
            "unit": "ns",
            "range": "± 0.4182691618154023"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 146441.38850097655,
            "unit": "ns",
            "range": "± 1303.7519184567502"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 125.49598299132452,
            "unit": "ns",
            "range": "± 0.32626989250756944"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014651475.7,
            "unit": "ns",
            "range": "± 1025261.21803041"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3172657262.4,
            "unit": "ns",
            "range": "± 612138.5810327593"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014179304.2,
            "unit": "ns",
            "range": "± 1648061.5895356277"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3177883108.1,
            "unit": "ns",
            "range": "± 1375967.1498358164"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013498706.4,
            "unit": "ns",
            "range": "± 562004.2584663393"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172179711.4,
            "unit": "ns",
            "range": "± 1034154.4086640544"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013214589,
            "unit": "ns",
            "range": "± 293264.9570209506"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177611305.3,
            "unit": "ns",
            "range": "± 427814.9448262648"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014096475.9,
            "unit": "ns",
            "range": "± 698858.7198556658"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173600228.6,
            "unit": "ns",
            "range": "± 372018.43116007035"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012706375.4,
            "unit": "ns",
            "range": "± 624174.7144095955"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3178913658.1,
            "unit": "ns",
            "range": "± 429656.2570384609"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014846597,
            "unit": "ns",
            "range": "± 844294.6853223109"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174046050.3,
            "unit": "ns",
            "range": "± 744638.5420092758"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013950075.4,
            "unit": "ns",
            "range": "± 363659.8739994557"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178309771.6,
            "unit": "ns",
            "range": "± 369126.33484255767"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8838754.555555556,
            "unit": "ns",
            "range": "± 530827.9649858584"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6065387,
            "unit": "ns",
            "range": "± 79953.19471103078"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8723727.6,
            "unit": "ns",
            "range": "± 376366.10476508696"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7382033.9,
            "unit": "ns",
            "range": "± 119816.78514038748"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 55091.12550354004,
            "unit": "ns",
            "range": "± 876.3835840333604"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 153452.78244628906,
            "unit": "ns",
            "range": "± 26248.245047102275"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8731749.7,
            "unit": "ns",
            "range": "± 322904.39167931565"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5455302.944444444,
            "unit": "ns",
            "range": "± 16696.9471169965"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8516436.875,
            "unit": "ns",
            "range": "± 455811.69199867663"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8241109.75,
            "unit": "ns",
            "range": "± 116782.7756474754"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 601948.041015625,
            "unit": "ns",
            "range": "± 50105.9067338054"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1334868.93359375,
            "unit": "ns",
            "range": "± 11996.789976935372"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8989713.1,
            "unit": "ns",
            "range": "± 293468.8854491506"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5470107.5,
            "unit": "ns",
            "range": "± 23090.997293317585"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9489584.2,
            "unit": "ns",
            "range": "± 769654.887208373"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6822494.6,
            "unit": "ns",
            "range": "± 266244.6732672996"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 61994.09385986328,
            "unit": "ns",
            "range": "± 1724.2722579150666"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8722237.222222222,
            "unit": "ns",
            "range": "± 598848.9439394916"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5435743.8,
            "unit": "ns",
            "range": "± 24190.28223068098"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 34014143.61111111,
            "unit": "ns",
            "range": "± 1339242.3096378455"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12364705.333333334,
            "unit": "ns",
            "range": "± 307839.27779565426"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 629212.620703125,
            "unit": "ns",
            "range": "± 18061.246532008754"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20678.11111111111,
            "unit": "ns",
            "range": "± 5472.444984749605"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13327.5,
            "unit": "ns",
            "range": "± 1281.555153362941"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 13055.1,
            "unit": "ns",
            "range": "± 1943.6947319759631"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 68013.33333333333,
            "unit": "ns",
            "range": "± 35794.9336708423"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 22644.1,
            "unit": "ns",
            "range": "± 338.97736732052715"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5337,
            "unit": "ns",
            "range": "± 102.54754994635415"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 37889.875,
            "unit": "ns",
            "range": "± 8215.852402303037"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 76651.5,
            "unit": "ns",
            "range": "± 36854.05902339659"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 55504.75,
            "unit": "ns",
            "range": "± 1895.0365054908195"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 414,
            "unit": "ns",
            "range": "± 14.916433890176299"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 350.8888888888889,
            "unit": "ns",
            "range": "± 7.166666666666667"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 584.1,
            "unit": "ns",
            "range": "± 137.92767831165884"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 362.3,
            "unit": "ns",
            "range": "± 17.543913157813137"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1332.1,
            "unit": "ns",
            "range": "± 96.17166364833713"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1219.111111111111,
            "unit": "ns",
            "range": "± 54.47578463052287"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1604.8,
            "unit": "ns",
            "range": "± 171.76521443205223"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 580.6,
            "unit": "ns",
            "range": "± 29.85595045845598"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 444.6,
            "unit": "ns",
            "range": "± 28.19456212345447"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 857.2,
            "unit": "ns",
            "range": "± 205.0581489345021"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 1438.125,
            "unit": "ns",
            "range": "± 68.96466486542221"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 86.11111111111111,
            "unit": "ns",
            "range": "± 27.67871223722504"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 1426.7777777777778,
            "unit": "ns",
            "range": "± 39.71390744367071"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 3716.722222222222,
            "unit": "ns",
            "range": "± 54.93354571156357"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 4668.222222222223,
            "unit": "ns",
            "range": "± 57.13750470964273"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1403.1,
            "unit": "ns",
            "range": "± 177.81479128576453"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 580.5,
            "unit": "ns",
            "range": "± 283.82398692773586"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 3298.7,
            "unit": "ns",
            "range": "± 261.9274920872399"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3167.6666666666665,
            "unit": "ns",
            "range": "± 75.87819186037581"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 78315.375,
            "unit": "ns",
            "range": "± 4120.3849486425415"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 116860.88888888889,
            "unit": "ns",
            "range": "± 6524.171258566955"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 394.9,
            "unit": "ns",
            "range": "± 17.57649946187617"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 337.3,
            "unit": "ns",
            "range": "± 18.22726894883963"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 395.77777777777777,
            "unit": "ns",
            "range": "± 17.45549897437608"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 342.7,
            "unit": "ns",
            "range": "± 30.139674848942878"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1644.5,
            "unit": "ns",
            "range": "± 228.06005544349253"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1087,
            "unit": "ns",
            "range": "± 62.77141068989927"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1365.2222222222222,
            "unit": "ns",
            "range": "± 41.163630117428234"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 530.5,
            "unit": "ns",
            "range": "± 25.522321385189258"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 636.6666666666666,
            "unit": "ns",
            "range": "± 35.601966237835796"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 561.4,
            "unit": "ns",
            "range": "± 20.117433014952756"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 1366.4,
            "unit": "ns",
            "range": "± 22.333084575729046"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 202.33333333333334,
            "unit": "ns",
            "range": "± 18.81488772222678"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 1469.375,
            "unit": "ns",
            "range": "± 20.756668463756068"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 3871.9444444444443,
            "unit": "ns",
            "range": "± 32.44653722321964"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 4736.111111111111,
            "unit": "ns",
            "range": "± 117.05174544239446"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1241.388888888889,
            "unit": "ns",
            "range": "± 65.56570072157477"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 483.6666666666667,
            "unit": "ns",
            "range": "± 17.399712641305314"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2841.125,
            "unit": "ns",
            "range": "± 29.95442967480531"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3378.222222222222,
            "unit": "ns",
            "range": "± 83.03279138054101"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 78089.75,
            "unit": "ns",
            "range": "± 4828.299989200576"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 104528.75,
            "unit": "ns",
            "range": "± 5757.535329585791"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 447.6,
            "unit": "ns",
            "range": "± 25.203394833059914"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 366.2,
            "unit": "ns",
            "range": "± 21.233097853220674"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 410.3,
            "unit": "ns",
            "range": "± 23.13751163034704"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 322.05555555555554,
            "unit": "ns",
            "range": "± 109.3481951281217"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1250.1666666666667,
            "unit": "ns",
            "range": "± 73.61385739111897"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1204.5555555555557,
            "unit": "ns",
            "range": "± 112.5878669208089"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1475.1,
            "unit": "ns",
            "range": "± 34.6681089443566"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 520.4444444444445,
            "unit": "ns",
            "range": "± 38.797909451126074"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 520.4444444444445,
            "unit": "ns",
            "range": "± 22.864334186190025"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 646.1,
            "unit": "ns",
            "range": "± 193.46673558464198"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 1300.7777777777778,
            "unit": "ns",
            "range": "± 56.79739821897166"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 95.05555555555556,
            "unit": "ns",
            "range": "± 18.104634152000358"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 1754.9,
            "unit": "ns",
            "range": "± 199.73229305470085"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 4802.5,
            "unit": "ns",
            "range": "± 488.5293690705242"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 5901,
            "unit": "ns",
            "range": "± 184.7786320361282"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 966.6666666666666,
            "unit": "ns",
            "range": "± 19.384271974980127"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 479.27777777777777,
            "unit": "ns",
            "range": "± 25.13850521499726"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3060.5,
            "unit": "ns",
            "range": "± 54.51081150953975"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3471.4444444444443,
            "unit": "ns",
            "range": "± 109.44303439588002"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 92982.38888888889,
            "unit": "ns",
            "range": "± 11726.809108240446"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 126195.11111111111,
            "unit": "ns",
            "range": "± 12467.33626766805"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "15277183819a59fc5e9cbe31f820de386231a3bf",
          "message": "Merge pull request #111 from thomhurst/perf/reduce-hot-path-allocations\n\nReduce heap allocations in hot paths",
          "timestamp": "2026-01-25T21:28:02Z",
          "tree_id": "8741ef2073bd1b58aa9e52b8e17e550c76d449a2",
          "url": "https://github.com/thomhurst/Dekaf/commit/15277183819a59fc5e9cbe31f820de386231a3bf"
        },
        "date": 1769377628256,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 138.191348195076,
            "unit": "ns",
            "range": "± 0.7192460509745755"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 127.16834735870361,
            "unit": "ns",
            "range": "± 0.5324335660987525"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 146902.82023925782,
            "unit": "ns",
            "range": "± 566.6936868969235"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 128.95173329777188,
            "unit": "ns",
            "range": "± 0.9095322922175174"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015183102.4,
            "unit": "ns",
            "range": "± 604278.6997361399"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3174595062.3,
            "unit": "ns",
            "range": "± 1127418.9588119849"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014027014.6,
            "unit": "ns",
            "range": "± 792625.130300762"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3179517806.2,
            "unit": "ns",
            "range": "± 686760.7240434618"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013551828.6,
            "unit": "ns",
            "range": "± 778722.8145504022"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172511228.4,
            "unit": "ns",
            "range": "± 649816.0085157183"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012929484.8,
            "unit": "ns",
            "range": "± 688204.1447163044"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177620065,
            "unit": "ns",
            "range": "± 449914.0540447697"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014246683.9,
            "unit": "ns",
            "range": "± 598854.7370709361"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173094070.4,
            "unit": "ns",
            "range": "± 1140943.5588596396"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013050215.3,
            "unit": "ns",
            "range": "± 521186.5091152111"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179300000.75,
            "unit": "ns",
            "range": "± 757571.7595415742"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014839438.7,
            "unit": "ns",
            "range": "± 684966.8407242062"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173866437,
            "unit": "ns",
            "range": "± 452867.12902756216"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013191334.75,
            "unit": "ns",
            "range": "± 218041.6176381243"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177322385.5,
            "unit": "ns",
            "range": "± 923289.0575375984"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8631226.2,
            "unit": "ns",
            "range": "± 412522.67795622797"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6395307.1,
            "unit": "ns",
            "range": "± 124245.26777614778"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 10334549.3,
            "unit": "ns",
            "range": "± 2073275.4727266196"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7739355.055555556,
            "unit": "ns",
            "range": "± 150713.71772014574"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 59795.97850206163,
            "unit": "ns",
            "range": "± 3413.099999982402"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 120600.73449707031,
            "unit": "ns",
            "range": "± 2021.557384637139"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8874914.375,
            "unit": "ns",
            "range": "± 397312.5845493292"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5490401.4,
            "unit": "ns",
            "range": "± 35623.4123153424"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8594750.888888888,
            "unit": "ns",
            "range": "± 552641.1139284798"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8374074.777777778,
            "unit": "ns",
            "range": "± 388583.5081867145"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 609352.2017578125,
            "unit": "ns",
            "range": "± 73193.49901791308"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1214653.5033203126,
            "unit": "ns",
            "range": "± 19915.389893204487"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8846292.9,
            "unit": "ns",
            "range": "± 402813.3693784808"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5516018.375,
            "unit": "ns",
            "range": "± 8032.810528211335"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8762626.555555556,
            "unit": "ns",
            "range": "± 225563.1627849011"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6621937.8,
            "unit": "ns",
            "range": "± 70569.10753462405"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 60708.17705078125,
            "unit": "ns",
            "range": "± 1806.3021737004694"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8815244.375,
            "unit": "ns",
            "range": "± 397692.93013775535"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5519774.1,
            "unit": "ns",
            "range": "± 37642.85040113491"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 27398499.388888888,
            "unit": "ns",
            "range": "± 1189496.3547208167"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 9706726.055555556,
            "unit": "ns",
            "range": "± 460172.87562613655"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 606640.8173828125,
            "unit": "ns",
            "range": "± 34145.83472436483"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20786.833333333332,
            "unit": "ns",
            "range": "± 4723.140930567285"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 11400.444444444445,
            "unit": "ns",
            "range": "± 2857.7450074801595"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15919.277777777777,
            "unit": "ns",
            "range": "± 862.9647701061988"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 61471.61111111111,
            "unit": "ns",
            "range": "± 27412.042501628937"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 17904.125,
            "unit": "ns",
            "range": "± 819.1877401251977"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 7791.5,
            "unit": "ns",
            "range": "± 307.9078595943923"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 33262.77777777778,
            "unit": "ns",
            "range": "± 4560.111341233287"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 72294.72222222222,
            "unit": "ns",
            "range": "± 33439.591107614404"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 55783.5,
            "unit": "ns",
            "range": "± 4088.6322285086976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 470.9,
            "unit": "ns",
            "range": "± 93.57403486010422"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 270.77777777777777,
            "unit": "ns",
            "range": "± 70.48719347827976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 527.1111111111111,
            "unit": "ns",
            "range": "± 55.72352385762328"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 242.16666666666666,
            "unit": "ns",
            "range": "± 77.85884663928691"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1266,
            "unit": "ns",
            "range": "± 59.266950559071525"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1190.111111111111,
            "unit": "ns",
            "range": "± 163.7127396115254"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1355.5,
            "unit": "ns",
            "range": "± 174.32942952926794"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 580,
            "unit": "ns",
            "range": "± 126.14123477717789"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 461.22222222222223,
            "unit": "ns",
            "range": "± 100.07719242886685"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 676.2,
            "unit": "ns",
            "range": "± 251.64439195022803"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 1166.6,
            "unit": "ns",
            "range": "± 364.8622875673627"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 124.22222222222223,
            "unit": "ns",
            "range": "± 50.66502190312804"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 924.875,
            "unit": "ns",
            "range": "± 139.6085727003488"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2906.777777777778,
            "unit": "ns",
            "range": "± 373.73980848237784"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 4176.277777777777,
            "unit": "ns",
            "range": "± 502.57133269262823"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1226.888888888889,
            "unit": "ns",
            "range": "± 128.2189966857919"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 483.1,
            "unit": "ns",
            "range": "± 130.36738685559192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2467.5555555555557,
            "unit": "ns",
            "range": "± 276.3716298352235"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3499.9444444444443,
            "unit": "ns",
            "range": "± 293.43828614851503"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 74062.88888888889,
            "unit": "ns",
            "range": "± 10122.758794474514"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 102066.75,
            "unit": "ns",
            "range": "± 8249.043039736704"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 424.1,
            "unit": "ns",
            "range": "± 96.79095458196953"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 265.3333333333333,
            "unit": "ns",
            "range": "± 57.38466694161429"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 393.875,
            "unit": "ns",
            "range": "± 35.154505909119045"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 559.3,
            "unit": "ns",
            "range": "± 232.8566798125691"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1354.125,
            "unit": "ns",
            "range": "± 119.16427736532455"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1131.2777777777778,
            "unit": "ns",
            "range": "± 300.61261524501003"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1178.4,
            "unit": "ns",
            "range": "± 368.35134194286724"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 637.8,
            "unit": "ns",
            "range": "± 154.108763901055"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 540.4,
            "unit": "ns",
            "range": "± 122.5574241823898"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 635.2,
            "unit": "ns",
            "range": "± 87.62679701755368"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 1184.3333333333333,
            "unit": "ns",
            "range": "± 141.76124294037493"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 121,
            "unit": "ns",
            "range": "± 32.16278042154385"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 1469.5,
            "unit": "ns",
            "range": "± 74.56540752922899"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2848.875,
            "unit": "ns",
            "range": "± 117.07987200442025"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3841.3333333333335,
            "unit": "ns",
            "range": "± 280.7810534918623"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 975.6666666666666,
            "unit": "ns",
            "range": "± 134.46653858860202"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 516.1,
            "unit": "ns",
            "range": "± 78.8732457103731"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2801.2,
            "unit": "ns",
            "range": "± 379.0675108444112"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 2959.6111111111113,
            "unit": "ns",
            "range": "± 228.15424850550363"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 81003,
            "unit": "ns",
            "range": "± 6723.120927069511"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 96845.55555555556,
            "unit": "ns",
            "range": "± 6318.5142460690695"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 505.3,
            "unit": "ns",
            "range": "± 137.11718427032486"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 207.375,
            "unit": "ns",
            "range": "± 48.39255403173391"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 342.27777777777777,
            "unit": "ns",
            "range": "± 73.37195952436083"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 172.875,
            "unit": "ns",
            "range": "± 52.05337233483122"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1575,
            "unit": "ns",
            "range": "± 78.82711643663166"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1514.7,
            "unit": "ns",
            "range": "± 256.48350607571024"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1240.388888888889,
            "unit": "ns",
            "range": "± 217.48301338520926"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 518.0555555555555,
            "unit": "ns",
            "range": "± 47.97163513762875"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 511.8888888888889,
            "unit": "ns",
            "range": "± 98.06432129531673"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 625.0555555555555,
            "unit": "ns",
            "range": "± 87.2741529765702"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 1141.388888888889,
            "unit": "ns",
            "range": "± 97.43644652341911"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 172.6,
            "unit": "ns",
            "range": "± 78.56377451896431"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 1509.111111111111,
            "unit": "ns",
            "range": "± 60.00509237649011"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 4104.944444444444,
            "unit": "ns",
            "range": "± 488.50514611186827"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3792.125,
            "unit": "ns",
            "range": "± 320.0535390479894"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1225,
            "unit": "ns",
            "range": "± 167.21318727899424"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 472,
            "unit": "ns",
            "range": "± 43.739488533164824"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2268.0555555555557,
            "unit": "ns",
            "range": "± 299.9846292358623"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3336.2,
            "unit": "ns",
            "range": "± 609.2598515138402"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 69664.77777777778,
            "unit": "ns",
            "range": "± 6580.645955713197"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 95452.94444444444,
            "unit": "ns",
            "range": "± 13670.231143904544"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "2171561b8d9831fe5828005708af67b907ced8b9",
          "message": "Merge pull request #112 from thomhurst/perf/reduce-allocations-and-contention\n\nperf: Reduce allocations and lock contention in hot paths",
          "timestamp": "2026-01-25T21:53:13Z",
          "tree_id": "6e12d16446dfe237116c44879c0f5ab1fe1c2172",
          "url": "https://github.com/thomhurst/Dekaf/commit/2171561b8d9831fe5828005708af67b907ced8b9"
        },
        "date": 1769379144845,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 134.65271978378297,
            "unit": "ns",
            "range": "± 3.01989354795816"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 123.63801302909852,
            "unit": "ns",
            "range": "± 0.8940545729570194"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 142014.85603841147,
            "unit": "ns",
            "range": "± 1340.479010015883"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 123.35285026497311,
            "unit": "ns",
            "range": "± 1.2030531931414967"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014809752.75,
            "unit": "ns",
            "range": "± 704775.4901051233"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3172905905.6,
            "unit": "ns",
            "range": "± 884918.2921283185"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3012777686,
            "unit": "ns",
            "range": "± 689721.3006631737"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3178121885.5,
            "unit": "ns",
            "range": "± 331565.42762025114"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013167202.6,
            "unit": "ns",
            "range": "± 607525.0837902086"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3170881522.75,
            "unit": "ns",
            "range": "± 625225.0153096217"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012306955.4,
            "unit": "ns",
            "range": "± 664394.0161427554"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177162448.2,
            "unit": "ns",
            "range": "± 771924.8653630093"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013406722.9,
            "unit": "ns",
            "range": "± 646487.7662518448"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3172544724.6,
            "unit": "ns",
            "range": "± 641016.9745406903"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012110719.5,
            "unit": "ns",
            "range": "± 546916.3406692105"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3177728792.4,
            "unit": "ns",
            "range": "± 540166.3111563883"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014700786.9,
            "unit": "ns",
            "range": "± 401352.1287240171"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173786585.1,
            "unit": "ns",
            "range": "± 789697.5539827257"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013961540.5,
            "unit": "ns",
            "range": "± 261081.2952734582"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177762926.5,
            "unit": "ns",
            "range": "± 230608.83069171483"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8751208.8,
            "unit": "ns",
            "range": "± 448649.5775155087"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6074332.5,
            "unit": "ns",
            "range": "± 127655.76422660192"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8701839.7,
            "unit": "ns",
            "range": "± 441243.6739540359"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7309156.2,
            "unit": "ns",
            "range": "± 146845.82500538146"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 64617.53767089844,
            "unit": "ns",
            "range": "± 6811.952894684732"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 131580.83277723525,
            "unit": "ns",
            "range": "± 1054.6016455560589"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8866796.5,
            "unit": "ns",
            "range": "± 289008.2072299639"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5414274.555555556,
            "unit": "ns",
            "range": "± 11641.848780489196"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 9539505.6,
            "unit": "ns",
            "range": "± 2055580.374777682"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8563717,
            "unit": "ns",
            "range": "± 314496.5859608018"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 579621.8777126736,
            "unit": "ns",
            "range": "± 25566.370566272995"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1320680.2228515625,
            "unit": "ns",
            "range": "± 9726.389171061997"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9006519.5,
            "unit": "ns",
            "range": "± 590442.2963305183"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5438287.4,
            "unit": "ns",
            "range": "± 23238.970987211214"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9424965.6,
            "unit": "ns",
            "range": "± 741599.9929600338"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6774434.8,
            "unit": "ns",
            "range": "± 322144.652774633"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 61728.584765625,
            "unit": "ns",
            "range": "± 5700.06554945435"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8900663.5,
            "unit": "ns",
            "range": "± 322983.65008817054"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5445415.5,
            "unit": "ns",
            "range": "± 18751.217842354363"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 33152844.444444444,
            "unit": "ns",
            "range": "± 1170198.113844202"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12044146.055555556,
            "unit": "ns",
            "range": "± 322242.568742908"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 665042.7531467014,
            "unit": "ns",
            "range": "± 20184.936185589835"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 29028.333333333332,
            "unit": "ns",
            "range": "± 6868.771687863849"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 11960.2,
            "unit": "ns",
            "range": "± 1804.4794755767598"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 10845.5,
            "unit": "ns",
            "range": "± 426.4203064791565"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 70573.55555555556,
            "unit": "ns",
            "range": "± 34075.35000227844"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21534.555555555555,
            "unit": "ns",
            "range": "± 1401.4738769516105"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 6535.111111111111,
            "unit": "ns",
            "range": "± 181.94878705589414"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 34936.22222222222,
            "unit": "ns",
            "range": "± 6370.82982777318"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 89133.27777777778,
            "unit": "ns",
            "range": "± 39513.944284827405"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 66421,
            "unit": "ns",
            "range": "± 1795.9072518208889"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 710,
            "unit": "ns",
            "range": "± 184.87653537789305"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 315.6111111111111,
            "unit": "ns",
            "range": "± 21.333333333333336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 390.1111111111111,
            "unit": "ns",
            "range": "± 52.97981796034327"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 619.8333333333334,
            "unit": "ns",
            "range": "± 95.26279441628824"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 941.3333333333334,
            "unit": "ns",
            "range": "± 72.98287470359057"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1538.1666666666667,
            "unit": "ns",
            "range": "± 76.78053138654356"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1493.5,
            "unit": "ns",
            "range": "± 133.4176816534367"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 590.75,
            "unit": "ns",
            "range": "± 47.751589352756476"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 848,
            "unit": "ns",
            "range": "± 223.44574285494903"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 867.8,
            "unit": "ns",
            "range": "± 170.8300324884357"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 1402,
            "unit": "ns",
            "range": "± 43.42481186734475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 94.22222222222223,
            "unit": "ns",
            "range": "± 23.02595154264953"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 1442.4444444444443,
            "unit": "ns",
            "range": "± 71.11805521650446"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 4312.7,
            "unit": "ns",
            "range": "± 214.3994454802116"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 4766.444444444444,
            "unit": "ns",
            "range": "± 182.38154450979349"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1456.3,
            "unit": "ns",
            "range": "± 184.6360082853709"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 446,
            "unit": "ns",
            "range": "± 25.80697580112788"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2983.5,
            "unit": "ns",
            "range": "± 123.42406572463896"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3500.3,
            "unit": "ns",
            "range": "± 207.35265183310827"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 76455.05555555556,
            "unit": "ns",
            "range": "± 4759.215300632844"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 114545.33333333333,
            "unit": "ns",
            "range": "± 7954.760367226659"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 392.4,
            "unit": "ns",
            "range": "± 44.97703117518245"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 646.8,
            "unit": "ns",
            "range": "± 196.4930306934857"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 412.75,
            "unit": "ns",
            "range": "± 22.237355957937087"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 638.9,
            "unit": "ns",
            "range": "± 183.335424230501"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1279.3,
            "unit": "ns",
            "range": "± 52.44001864564462"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1201.2777777777778,
            "unit": "ns",
            "range": "± 94.34481673332374"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1240.625,
            "unit": "ns",
            "range": "± 62.969238521678186"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 590,
            "unit": "ns",
            "range": "± 110.71610341569809"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 603.625,
            "unit": "ns",
            "range": "± 69.14464652348107"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 513.5,
            "unit": "ns",
            "range": "± 31.38028397293788"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 1284.2222222222222,
            "unit": "ns",
            "range": "± 46.647019673763126"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 145.44444444444446,
            "unit": "ns",
            "range": "± 18.214768123085666"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 1442.5,
            "unit": "ns",
            "range": "± 40.620192023179804"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 3941.3,
            "unit": "ns",
            "range": "± 269.01427058388145"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 4654.5,
            "unit": "ns",
            "range": "± 257.62209310760375"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1160.5,
            "unit": "ns",
            "range": "± 23.85671513731212"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 497.375,
            "unit": "ns",
            "range": "± 21.168963130016547"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 3270.6,
            "unit": "ns",
            "range": "± 213.53958779475894"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3716.777777777778,
            "unit": "ns",
            "range": "± 263.2972549124742"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 75854.27777777778,
            "unit": "ns",
            "range": "± 5966.598921868676"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 107413.11111111111,
            "unit": "ns",
            "range": "± 7679.834803634198"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 415.875,
            "unit": "ns",
            "range": "± 27.155307294996945"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 312.1,
            "unit": "ns",
            "range": "± 73.52165213957949"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 734.9,
            "unit": "ns",
            "range": "± 169.2561832121815"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 316.375,
            "unit": "ns",
            "range": "± 45.23885024432619"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1519.7222222222222,
            "unit": "ns",
            "range": "± 183.97539086639944"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1627.2,
            "unit": "ns",
            "range": "± 174.98765035789748"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1380.6,
            "unit": "ns",
            "range": "± 98.81795833191904"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 784.1,
            "unit": "ns",
            "range": "± 193.70220557454797"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 500.625,
            "unit": "ns",
            "range": "± 21.6526969616786"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 631.8888888888889,
            "unit": "ns",
            "range": "± 138.27277791058916"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 1729,
            "unit": "ns",
            "range": "± 144.18506626323452"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 93.375,
            "unit": "ns",
            "range": "± 8.991066995317391"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 1339.5,
            "unit": "ns",
            "range": "± 47.43416490252569"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 4432.4,
            "unit": "ns",
            "range": "± 166.66679999994668"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 4693.777777777777,
            "unit": "ns",
            "range": "± 83.48769037675221"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1573.7,
            "unit": "ns",
            "range": "± 348.3268101462572"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 524.4,
            "unit": "ns",
            "range": "± 20.791023704153353"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3084.1666666666665,
            "unit": "ns",
            "range": "± 30.826125283596706"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3457.277777777778,
            "unit": "ns",
            "range": "± 74.93460111620296"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 136264.61111111112,
            "unit": "ns",
            "range": "± 20506.914604862213"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 109953.44444444444,
            "unit": "ns",
            "range": "± 7282.850353932709"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6c93303ccdac1bbdd17dc7719ab68fbddd68e61b",
          "message": "Merge pull request #113 from thomhurst/perf/producer-optimizations\n\nOptimize producer hot paths with lock-free reads and sync fast paths",
          "timestamp": "2026-01-25T22:21:42Z",
          "tree_id": "02db857c636dfb6033e66376802a47c401a893dc",
          "url": "https://github.com/thomhurst/Dekaf/commit/6c93303ccdac1bbdd17dc7719ab68fbddd68e61b"
        },
        "date": 1769380475070,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 143.66325118806628,
            "unit": "ns",
            "range": "± 1.6035498989926076"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 133.7297477722168,
            "unit": "ns",
            "range": "± 1.5502823713354308"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 147026.9478515625,
            "unit": "ns",
            "range": "± 1828.7824373227722"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 125.64312488502927,
            "unit": "ns",
            "range": "± 0.976792868314438"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015601364.2,
            "unit": "ns",
            "range": "± 290988.85235726816"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3173496575.4,
            "unit": "ns",
            "range": "± 609325.6656528593"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3013667396.7,
            "unit": "ns",
            "range": "± 1218323.2416141457"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3177734159.1,
            "unit": "ns",
            "range": "± 816943.3819459584"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012553776.2,
            "unit": "ns",
            "range": "± 429534.8029423227"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3171124788.5,
            "unit": "ns",
            "range": "± 371069.945006329"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012367242.7,
            "unit": "ns",
            "range": "± 537962.0440948413"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3178144807.75,
            "unit": "ns",
            "range": "± 148803.94656364687"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013570557.5,
            "unit": "ns",
            "range": "± 362824.6521333228"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3172926883.4,
            "unit": "ns",
            "range": "± 845101.0118978678"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012765760.5,
            "unit": "ns",
            "range": "± 278594.1652757047"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3178286769.8,
            "unit": "ns",
            "range": "± 874698.3111414472"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014565525.25,
            "unit": "ns",
            "range": "± 174400.28989344218"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174416567.4,
            "unit": "ns",
            "range": "± 690580.7755482338"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013688077.75,
            "unit": "ns",
            "range": "± 249081.31489452062"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179577761.7,
            "unit": "ns",
            "range": "± 2268868.0458159526"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8640521,
            "unit": "ns",
            "range": "± 454771.64068549586"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6026043.555555556,
            "unit": "ns",
            "range": "± 68216.96068997635"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8778379.4,
            "unit": "ns",
            "range": "± 346412.06070787687"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7406214.888888889,
            "unit": "ns",
            "range": "± 91482.2734597316"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 153881.8664794922,
            "unit": "ns",
            "range": "± 20837.67925487129"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8831211.166666666,
            "unit": "ns",
            "range": "± 325749.5605231111"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5425779,
            "unit": "ns",
            "range": "± 16301.027347116149"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8606590,
            "unit": "ns",
            "range": "± 563828.2994664687"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8598372.444444444,
            "unit": "ns",
            "range": "± 271126.3798766689"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1321522.6498046876,
            "unit": "ns",
            "range": "± 17427.599296250904"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9145081.3,
            "unit": "ns",
            "range": "± 536016.1834186331"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5440183.5,
            "unit": "ns",
            "range": "± 28231.684730497793"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9362026.6,
            "unit": "ns",
            "range": "± 582705.4716557929"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6858147,
            "unit": "ns",
            "range": "± 49770.827273614814"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 9126457.4,
            "unit": "ns",
            "range": "± 511229.43090645055"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5449715.8,
            "unit": "ns",
            "range": "± 35933.090984958886"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 35120247.277777776,
            "unit": "ns",
            "range": "± 865386.5421970083"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 14224617.5,
            "unit": "ns",
            "range": "± 1897846.1865825693"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20368.555555555555,
            "unit": "ns",
            "range": "± 5348.346195580254"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 14310.9,
            "unit": "ns",
            "range": "± 326.309822101634"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14316.4,
            "unit": "ns",
            "range": "± 1070.999138914479"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 89097.88888888889,
            "unit": "ns",
            "range": "± 42272.107868086154"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21132.4,
            "unit": "ns",
            "range": "± 1214.1770372469487"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5520,
            "unit": "ns",
            "range": "± 73.48469228349535"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 45631.11111111111,
            "unit": "ns",
            "range": "± 11556.39786486737"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 89028.77777777778,
            "unit": "ns",
            "range": "± 34785.604730181774"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 64472,
            "unit": "ns",
            "range": "± 1268.626478857013"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 538.6,
            "unit": "ns",
            "range": "± 204.19173995699893"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 305.875,
            "unit": "ns",
            "range": "± 20.399142138825347"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 472.22222222222223,
            "unit": "ns",
            "range": "± 24.9438257780246"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 377.375,
            "unit": "ns",
            "range": "± 16.71558297774009"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1188.2,
            "unit": "ns",
            "range": "± 31.016303956324503"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1188.5555555555557,
            "unit": "ns",
            "range": "± 79.88602992875398"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1344.5,
            "unit": "ns",
            "range": "± 28.90982147606204"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 494.875,
            "unit": "ns",
            "range": "± 38.49281008930666"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 802.1666666666666,
            "unit": "ns",
            "range": "± 134.7942877127959"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 864.6,
            "unit": "ns",
            "range": "± 112.53883280400987"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 1519.4,
            "unit": "ns",
            "range": "± 305.12554793068375"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 117.8,
            "unit": "ns",
            "range": "± 26.902292343466446"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 1374.4444444444443,
            "unit": "ns",
            "range": "± 25.661796074666672"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 3905.9,
            "unit": "ns",
            "range": "± 51.306161639926074"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 4701.8,
            "unit": "ns",
            "range": "± 91.33217760825954"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1180.4444444444443,
            "unit": "ns",
            "range": "± 38.21685201292458"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 506.6,
            "unit": "ns",
            "range": "± 13.745302227791623"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2807.222222222222,
            "unit": "ns",
            "range": "± 79.33753490274603"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3409.9,
            "unit": "ns",
            "range": "± 38.88573003043661"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 79415.16666666667,
            "unit": "ns",
            "range": "± 5711.452420356839"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 121892.11111111111,
            "unit": "ns",
            "range": "± 14327.094266148704"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 430.22222222222223,
            "unit": "ns",
            "range": "± 16.052864057371334"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 244.94444444444446,
            "unit": "ns",
            "range": "± 90.70709882791851"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 87.1,
            "unit": "ns",
            "range": "± 58.49681852697442"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 314.5,
            "unit": "ns",
            "range": "± 41.21757985239901"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1060.375,
            "unit": "ns",
            "range": "± 47.841815242675374"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1160.3333333333333,
            "unit": "ns",
            "range": "± 38.890872965260115"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1309.7777777777778,
            "unit": "ns",
            "range": "± 31.928740101113355"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 557,
            "unit": "ns",
            "range": "± 13.169228201704586"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 577.4,
            "unit": "ns",
            "range": "± 17.970345943618707"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 483.2,
            "unit": "ns",
            "range": "± 29.390285621083866"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 1262.888888888889,
            "unit": "ns",
            "range": "± 28.685555792264356"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 134.88888888888889,
            "unit": "ns",
            "range": "± 10.42166546724232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 1416.1,
            "unit": "ns",
            "range": "± 22.91748483387981"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 3827.8,
            "unit": "ns",
            "range": "± 66.28691843460183"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 4894.9,
            "unit": "ns",
            "range": "± 433.7634916249484"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1189.4444444444443,
            "unit": "ns",
            "range": "± 66.43438701288495"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 418.9,
            "unit": "ns",
            "range": "± 19.489028252384013"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2885,
            "unit": "ns",
            "range": "± 49.88876515698588"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3278.125,
            "unit": "ns",
            "range": "± 63.20813125811313"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 96667.66666666667,
            "unit": "ns",
            "range": "± 11123.018036935839"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 114212.22222222222,
            "unit": "ns",
            "range": "± 9233.689684760066"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 404.3,
            "unit": "ns",
            "range": "± 24.436084429020585"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 343,
            "unit": "ns",
            "range": "± 54.76182194610715"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 475.1,
            "unit": "ns",
            "range": "± 205.4037757956536"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 543,
            "unit": "ns",
            "range": "± 167.15345577575647"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1821.6666666666667,
            "unit": "ns",
            "range": "± 132.65839588959304"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1366,
            "unit": "ns",
            "range": "± 48.579831205964474"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1450.5,
            "unit": "ns",
            "range": "± 86.43719621140478"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 692.5,
            "unit": "ns",
            "range": "± 209.70667344862656"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 512.6666666666666,
            "unit": "ns",
            "range": "± 27.34044622898463"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 523.5,
            "unit": "ns",
            "range": "± 27.370909131168197"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 1340.5,
            "unit": "ns",
            "range": "± 36.47221164423981"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 105.4,
            "unit": "ns",
            "range": "± 18.199206331901156"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 1403.7,
            "unit": "ns",
            "range": "± 31.229615573824933"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 3997,
            "unit": "ns",
            "range": "± 74.16198487095663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 4943.055555555556,
            "unit": "ns",
            "range": "± 119.93968391561559"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1254,
            "unit": "ns",
            "range": "± 43.31611379481906"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 514.5,
            "unit": "ns",
            "range": "± 33.06979151901492"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3087.3,
            "unit": "ns",
            "range": "± 43.32063916631168"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3286.4,
            "unit": "ns",
            "range": "± 74.80522857542928"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 79449.66666666667,
            "unit": "ns",
            "range": "± 7691.423714111711"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 111343.55555555556,
            "unit": "ns",
            "range": "± 12774.035042921158"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "799efa36e47a50531ffe38cc64f5e241f0c1111c",
          "message": "Merge pull request #114 from thomhurst/feature/modular-pipelines\n\nAdd ModularPipelines CI/CD pipeline",
          "timestamp": "2026-01-25T23:25:01Z",
          "tree_id": "c33a4ede15993f06e9233fb4150c77574b95b34c",
          "url": "https://github.com/thomhurst/Dekaf/commit/799efa36e47a50531ffe38cc64f5e241f0c1111c"
        },
        "date": 1769384272388,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 128.9376686414083,
            "unit": "ns",
            "range": "± 1.1619141794592909"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 122.56789560317993,
            "unit": "ns",
            "range": "± 2.550601256369843"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 139134.43364800347,
            "unit": "ns",
            "range": "± 937.7529291942399"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 117.18611188729604,
            "unit": "ns",
            "range": "± 0.6685095042023396"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014908533.8,
            "unit": "ns",
            "range": "± 1078581.645058778"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3173195789.3,
            "unit": "ns",
            "range": "± 1125533.2211999341"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3013364067.8,
            "unit": "ns",
            "range": "± 1365167.1044076253"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3177725588,
            "unit": "ns",
            "range": "± 334583.08800754807"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013466257.8,
            "unit": "ns",
            "range": "± 886269.3412316596"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172161781.7,
            "unit": "ns",
            "range": "± 1364058.7504474286"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013097743.9,
            "unit": "ns",
            "range": "± 342244.80704066204"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177902802.75,
            "unit": "ns",
            "range": "± 734838.500795186"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013472123.9,
            "unit": "ns",
            "range": "± 286485.6008603225"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3171874502,
            "unit": "ns",
            "range": "± 186230.50550191823"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012827788.7,
            "unit": "ns",
            "range": "± 527158.4263925789"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3177912195.9,
            "unit": "ns",
            "range": "± 1001548.8812940186"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014762156.7,
            "unit": "ns",
            "range": "± 777704.3516328683"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3171889671.4,
            "unit": "ns",
            "range": "± 514977.2639416229"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013883504.2,
            "unit": "ns",
            "range": "± 915116.8184290463"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178108641.8,
            "unit": "ns",
            "range": "± 1333281.7854966742"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 9080169.055555556,
            "unit": "ns",
            "range": "± 537207.167870113"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6488606.3,
            "unit": "ns",
            "range": "± 322592.78158420307"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8952014.7,
            "unit": "ns",
            "range": "± 471295.5556621556"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7600308.5,
            "unit": "ns",
            "range": "± 230133.61544944567"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 132310.42947387695,
            "unit": "ns",
            "range": "± 7710.439660989696"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 9098221,
            "unit": "ns",
            "range": "± 605951.1232018727"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5817322.4,
            "unit": "ns",
            "range": "± 305329.53396378877"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8340526.111111111,
            "unit": "ns",
            "range": "± 494382.2793424751"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8471064.222222222,
            "unit": "ns",
            "range": "± 533187.2260352778"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1214728.77890625,
            "unit": "ns",
            "range": "± 15013.473484552329"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9016823.6,
            "unit": "ns",
            "range": "± 414787.6213352242"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5670138.4,
            "unit": "ns",
            "range": "± 315767.19790067564"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8944708.333333334,
            "unit": "ns",
            "range": "± 466678.1797296184"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6479226.5,
            "unit": "ns",
            "range": "± 60470.647143882954"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 9052957.333333334,
            "unit": "ns",
            "range": "± 190222.16504918665"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5568976.4,
            "unit": "ns",
            "range": "± 253023.33184750195"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 26895556.9,
            "unit": "ns",
            "range": "± 1689071.9748861075"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 9314486.222222222,
            "unit": "ns",
            "range": "± 1096211.5493510796"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 22625.444444444445,
            "unit": "ns",
            "range": "± 4375.082716678369"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 9154.875,
            "unit": "ns",
            "range": "± 252.81045271111714"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15961,
            "unit": "ns",
            "range": "± 622.1984767294472"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 63047.444444444445,
            "unit": "ns",
            "range": "± 26461.91710322171"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 17731.833333333332,
            "unit": "ns",
            "range": "± 433.865474542513"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5510.166666666667,
            "unit": "ns",
            "range": "± 120.94006780219696"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 24848.11111111111,
            "unit": "ns",
            "range": "± 4819.804831226168"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 73657.11111111111,
            "unit": "ns",
            "range": "± 32962.48340327396"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 54722.8,
            "unit": "ns",
            "range": "± 3763.874180209063"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 327.27777777777777,
            "unit": "ns",
            "range": "± 103.7988171630315"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 168.75,
            "unit": "ns",
            "range": "± 44.132106874053235"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 1415.4,
            "unit": "ns",
            "range": "± 620.3250402454794"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 183.75,
            "unit": "ns",
            "range": "± 74.5304348188424"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1676.5,
            "unit": "ns",
            "range": "± 420.5713375873349"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1006.2,
            "unit": "ns",
            "range": "± 379.50466253906404"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 2418.0555555555557,
            "unit": "ns",
            "range": "± 125.15401622711825"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 565.3333333333334,
            "unit": "ns",
            "range": "± 86.58810541870055"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 399.1666666666667,
            "unit": "ns",
            "range": "± 56.32495006655576"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 472.22222222222223,
            "unit": "ns",
            "range": "± 130.3934217836331"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 2023.7,
            "unit": "ns",
            "range": "± 514.7297133231942"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 1246.1,
            "unit": "ns",
            "range": "± 314.43996070615594"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 2143.4,
            "unit": "ns",
            "range": "± 650.2569492131552"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 4583.4,
            "unit": "ns",
            "range": "± 164.58851046709722"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3531.5,
            "unit": "ns",
            "range": "± 285.4159245732445"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1294.5555555555557,
            "unit": "ns",
            "range": "± 80.55605363830689"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 404.25,
            "unit": "ns",
            "range": "± 70.04233413749553"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2902,
            "unit": "ns",
            "range": "± 437.5553298346012"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3307.3333333333335,
            "unit": "ns",
            "range": "± 245.9461323135617"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 71942.38888888889,
            "unit": "ns",
            "range": "± 4898.220045191019"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 99429.55555555556,
            "unit": "ns",
            "range": "± 7748.084297281347"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 921,
            "unit": "ns",
            "range": "± 640.8756596338412"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 518.75,
            "unit": "ns",
            "range": "± 33.644146168814736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 1274.2,
            "unit": "ns",
            "range": "± 568.9774551284474"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 340.44444444444446,
            "unit": "ns",
            "range": "± 55.7564146782931"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 172.55555555555554,
            "unit": "ns",
            "range": "± 107.83217413081208"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 919.8333333333334,
            "unit": "ns",
            "range": "± 123.80730996189199"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 2326.4444444444443,
            "unit": "ns",
            "range": "± 154.919423500663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 1598.0555555555557,
            "unit": "ns",
            "range": "± 269.0005163150766"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 1193.4,
            "unit": "ns",
            "range": "± 604.2358259267099"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 373.125,
            "unit": "ns",
            "range": "± 62.17357615119603"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 2556.5,
            "unit": "ns",
            "range": "± 285.45013185804936"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 129.11111111111111,
            "unit": "ns",
            "range": "± 24.680176480550358"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 1691.3,
            "unit": "ns",
            "range": "± 243.84913368720424"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 3871.4,
            "unit": "ns",
            "range": "± 591.2168618855334"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 4271.9,
            "unit": "ns",
            "range": "± 821.4137474162727"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1092,
            "unit": "ns",
            "range": "± 109.74515934655159"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 920.1,
            "unit": "ns",
            "range": "± 757.7600323761254"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 3857.7,
            "unit": "ns",
            "range": "± 231.5176019226184"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 4109.722222222223,
            "unit": "ns",
            "range": "± 173.66258792395223"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 70175.875,
            "unit": "ns",
            "range": "± 7344.427575428482"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 101561.61111111111,
            "unit": "ns",
            "range": "± 6906.823138108512"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 1068.8,
            "unit": "ns",
            "range": "± 623.3208198387445"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 267.3333333333333,
            "unit": "ns",
            "range": "± 74.19737192111322"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 900.4,
            "unit": "ns",
            "range": "± 670.9378924725862"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 1388.6666666666667,
            "unit": "ns",
            "range": "± 238.28134631145596"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 2657,
            "unit": "ns",
            "range": "± 246.13410978570198"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1202.5,
            "unit": "ns",
            "range": "± 62.48656998564914"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1451.0555555555557,
            "unit": "ns",
            "range": "± 145.4468211332849"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 709.5,
            "unit": "ns",
            "range": "± 62.14958912632833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 1398,
            "unit": "ns",
            "range": "± 589.0942671216175"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 1433.1,
            "unit": "ns",
            "range": "± 553.5953596465764"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 1519.9,
            "unit": "ns",
            "range": "± 199.39920873575312"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 160.7,
            "unit": "ns",
            "range": "± 55.525869846605936"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 2079.3,
            "unit": "ns",
            "range": "± 532.2559534659993"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 4912.9,
            "unit": "ns",
            "range": "± 562.0044384957195"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 5883.777777777777,
            "unit": "ns",
            "range": "± 607.6382101583511"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 2507.5,
            "unit": "ns",
            "range": "± 360.1613835799477"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 1454.4,
            "unit": "ns",
            "range": "± 582.0626541304066"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3875.3,
            "unit": "ns",
            "range": "± 218.3677072177925"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3398.9,
            "unit": "ns",
            "range": "± 942.4670286010011"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 83787.25,
            "unit": "ns",
            "range": "± 6507.177087098302"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 96094.66666666667,
            "unit": "ns",
            "range": "± 7685.164458227293"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a8638a47c011b1bb3b6a61d78ba3c2f5f5f08601",
          "message": "Merge pull request #115 from thomhurst/perf/reader-and-producer-optimizations\n\nOptimize protocol reader and producer hot paths",
          "timestamp": "2026-01-25T23:26:35Z",
          "tree_id": "cfcbb6d8aa389adc800e56ccae5211673038fcf3",
          "url": "https://github.com/thomhurst/Dekaf/commit/a8638a47c011b1bb3b6a61d78ba3c2f5f5f08601"
        },
        "date": 1769384438202,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 129.5549898677402,
            "unit": "ns",
            "range": "± 0.35540721203212006"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 123.93366270595126,
            "unit": "ns",
            "range": "± 0.19036971146899243"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 141346.1077202691,
            "unit": "ns",
            "range": "± 296.23284937646605"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 123.90652171770732,
            "unit": "ns",
            "range": "± 0.876960143470336"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014642835.1,
            "unit": "ns",
            "range": "± 1219154.461123487"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3172315873.4,
            "unit": "ns",
            "range": "± 1495938.1122298476"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3012685092.8,
            "unit": "ns",
            "range": "± 1011170.5160932057"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176752569.7,
            "unit": "ns",
            "range": "± 698608.3194213336"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012873526.6,
            "unit": "ns",
            "range": "± 749793.2460223952"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3170853972.7,
            "unit": "ns",
            "range": "± 451093.2939167906"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012206784.2,
            "unit": "ns",
            "range": "± 499080.4356390861"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3176758263.4,
            "unit": "ns",
            "range": "± 412742.9844265072"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013100689,
            "unit": "ns",
            "range": "± 422648.10147095186"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3171315427.7,
            "unit": "ns",
            "range": "± 895165.4117098694"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012075999.4,
            "unit": "ns",
            "range": "± 543575.6382977442"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3176929250.4,
            "unit": "ns",
            "range": "± 608332.4090867591"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013239087.75,
            "unit": "ns",
            "range": "± 285167.24919641454"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3172606978,
            "unit": "ns",
            "range": "± 555779.8956497976"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013258480.8,
            "unit": "ns",
            "range": "± 343974.96262475266"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3176452769.5,
            "unit": "ns",
            "range": "± 999489.1686669246"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8656137.8,
            "unit": "ns",
            "range": "± 461452.35993757314"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6063953.7,
            "unit": "ns",
            "range": "± 105687.22217315476"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8754526.7,
            "unit": "ns",
            "range": "± 409196.3118952538"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7376216.5,
            "unit": "ns",
            "range": "± 143005.6343994879"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 466958.136328125,
            "unit": "ns",
            "range": "± 35087.630490002906"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 127088.4182671441,
            "unit": "ns",
            "range": "± 1088.0563893541403"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8784492.7,
            "unit": "ns",
            "range": "± 350312.8104705691"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5401840.1,
            "unit": "ns",
            "range": "± 23526.373666117306"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 10260664.777777778,
            "unit": "ns",
            "range": "± 1916827.3328602642"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8547461.333333334,
            "unit": "ns",
            "range": "± 343693.1720615788"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 6624007.8890625,
            "unit": "ns",
            "range": "± 35839.038189495135"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9009770.555555556,
            "unit": "ns",
            "range": "± 292978.7395439774"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5414116.1,
            "unit": "ns",
            "range": "± 26731.768098192748"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9161620.777777778,
            "unit": "ns",
            "range": "± 373706.92283050425"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6833006.5,
            "unit": "ns",
            "range": "± 120782.00083989704"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 643488.1943359375,
            "unit": "ns",
            "range": "± 13701.02292526644"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8792341,
            "unit": "ns",
            "range": "± 401791.6688655674"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5407955.111111111,
            "unit": "ns",
            "range": "± 17302.308266850152"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 26446729.8,
            "unit": "ns",
            "range": "± 424943.6869683244"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11659912.222222222,
            "unit": "ns",
            "range": "± 465333.75875837164"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 10146769.9375,
            "unit": "ns",
            "range": "± 344459.1665127284"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20298.666666666668,
            "unit": "ns",
            "range": "± 5290.315562421584"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 13840.833333333334,
            "unit": "ns",
            "range": "± 173.29238298321135"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 10657.5,
            "unit": "ns",
            "range": "± 266.10451163238685"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 40251.77777777778,
            "unit": "ns",
            "range": "± 5504.520432739299"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 17777.1,
            "unit": "ns",
            "range": "± 208.2424868592702"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3876.8,
            "unit": "ns",
            "range": "± 116.0275829275091"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 27101.333333333332,
            "unit": "ns",
            "range": "± 5941.52594036246"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 45946.666666666664,
            "unit": "ns",
            "range": "± 12051.70328210913"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 52209.375,
            "unit": "ns",
            "range": "± 479.144457042222"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 379,
            "unit": "ns",
            "range": "± 19.261360284258224"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 368.1,
            "unit": "ns",
            "range": "± 19.393297811357407"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 340.6666666666667,
            "unit": "ns",
            "range": "± 27.730849247724095"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 444.5,
            "unit": "ns",
            "range": "± 35.687688508939765"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1299.625,
            "unit": "ns",
            "range": "± 62.83297018967942"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1141.7777777777778,
            "unit": "ns",
            "range": "± 31.0675786704475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1502.3,
            "unit": "ns",
            "range": "± 285.2445539454795"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 554,
            "unit": "ns",
            "range": "± 37.08436328157732"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 493.6666666666667,
            "unit": "ns",
            "range": "± 46.04345773288536"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 548.7,
            "unit": "ns",
            "range": "± 29.23107174832212"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 268,
            "unit": "ns",
            "range": "± 22.140962540554153"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 120.77777777777777,
            "unit": "ns",
            "range": "± 16.42237633366269"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 339.2,
            "unit": "ns",
            "range": "± 18.72490913788962"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2353.25,
            "unit": "ns",
            "range": "± 65.88680769753098"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3547.6,
            "unit": "ns",
            "range": "± 220.4674075181585"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1346.7,
            "unit": "ns",
            "range": "± 186.51708292331355"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 426.75,
            "unit": "ns",
            "range": "± 25.63340454507416"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 3576.222222222222,
            "unit": "ns",
            "range": "± 71.24039896325992"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3198.6666666666665,
            "unit": "ns",
            "range": "± 69.62578545337927"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 75923.55555555556,
            "unit": "ns",
            "range": "± 5628.371014581197"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 110748.05555555556,
            "unit": "ns",
            "range": "± 5823.176691272366"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 407.1,
            "unit": "ns",
            "range": "± 35.25919895730916"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 306.8,
            "unit": "ns",
            "range": "± 35.786558618813544"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 390.8,
            "unit": "ns",
            "range": "± 24.088032990124645"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 437.1,
            "unit": "ns",
            "range": "± 42.31745108896171"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1239.3333333333333,
            "unit": "ns",
            "range": "± 20.130822139197395"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1083.6666666666667,
            "unit": "ns",
            "range": "± 37.98025802966588"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1359,
            "unit": "ns",
            "range": "± 45.791799362865056"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 574.5,
            "unit": "ns",
            "range": "± 14.990737881179239"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 601.2,
            "unit": "ns",
            "range": "± 44.39982482447926"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 540.4444444444445,
            "unit": "ns",
            "range": "± 28.901172602124255"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 337.2,
            "unit": "ns",
            "range": "± 28.78579896793247"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 128.8,
            "unit": "ns",
            "range": "± 20.638690742281973"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 361.1,
            "unit": "ns",
            "range": "± 12.0595743429581"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 3290.2,
            "unit": "ns",
            "range": "± 225.40090209816523"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3075.7,
            "unit": "ns",
            "range": "± 63.04460502074878"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1213.125,
            "unit": "ns",
            "range": "± 15.81534065393471"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 447,
            "unit": "ns",
            "range": "± 22.36626924634504"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2773.625,
            "unit": "ns",
            "range": "± 77.18796630859724"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3323.875,
            "unit": "ns",
            "range": "± 17.671102318272542"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 76789.55555555556,
            "unit": "ns",
            "range": "± 7864.56194125126"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 108931.88888888889,
            "unit": "ns",
            "range": "± 5729.004591646886"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 385.8888888888889,
            "unit": "ns",
            "range": "± 20.027758514399736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 364.4,
            "unit": "ns",
            "range": "± 19.693202436938037"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 403.4,
            "unit": "ns",
            "range": "± 32.056200648236526"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 308,
            "unit": "ns",
            "range": "± 91.12994385308633"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1640.5,
            "unit": "ns",
            "range": "± 254.5720810388375"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1263.2222222222222,
            "unit": "ns",
            "range": "± 46.647019673763126"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1454.6666666666667,
            "unit": "ns",
            "range": "± 38.144462245521304"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 554.9,
            "unit": "ns",
            "range": "± 47.4890630683646"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 597.3333333333334,
            "unit": "ns",
            "range": "± 46.64493541639863"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 604.1,
            "unit": "ns",
            "range": "± 25.22432688233061"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 264.55555555555554,
            "unit": "ns",
            "range": "± 8.762292952063277"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 144,
            "unit": "ns",
            "range": "± 20.65591117977289"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 316.1,
            "unit": "ns",
            "range": "± 73.92856912813431"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2572.0555555555557,
            "unit": "ns",
            "range": "± 33.023140035099296"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3185.8888888888887,
            "unit": "ns",
            "range": "± 50.28529716637967"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1316.5,
            "unit": "ns",
            "range": "± 27.7758888246623"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 470.72222222222223,
            "unit": "ns",
            "range": "± 23.456224002265248"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2977.3,
            "unit": "ns",
            "range": "± 37.785358716483465"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3006.1111111111113,
            "unit": "ns",
            "range": "± 76.10099283919436"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 121199.11111111111,
            "unit": "ns",
            "range": "± 15355.812217564759"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 143041.16666666666,
            "unit": "ns",
            "range": "± 38234.50101348257"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "1f3392b1b459113dae0be00d55e1f1c59efd8a96",
          "message": "Merge pull request #116 from thomhurst/feature/parallel-benchmarks\n\nRun benchmark jobs in parallel",
          "timestamp": "2026-01-25T23:46:42Z",
          "tree_id": "133a5e63293d018eeae59928a2c05c3d04252a58",
          "url": "https://github.com/thomhurst/Dekaf/commit/1f3392b1b459113dae0be00d55e1f1c59efd8a96"
        },
        "date": 1769385434581,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 138.67040207386017,
            "unit": "ns",
            "range": "± 2.376829448506373"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 131.63165726661683,
            "unit": "ns",
            "range": "± 2.5640523366616206"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 149317.69396972656,
            "unit": "ns",
            "range": "± 867.7003502478458"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 126.42935557365418,
            "unit": "ns",
            "range": "± 0.4654507635388502"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3018726552.6,
            "unit": "ns",
            "range": "± 1060479.0092075374"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176917290.3,
            "unit": "ns",
            "range": "± 448082.210342812"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015700624.8,
            "unit": "ns",
            "range": "± 654314.3726655407"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3181019345.1,
            "unit": "ns",
            "range": "± 661381.1724038566"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3015228793,
            "unit": "ns",
            "range": "± 927858.9974519296"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3174213101.2,
            "unit": "ns",
            "range": "± 604728.6532662398"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014688604.5,
            "unit": "ns",
            "range": "± 371397.8933260123"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3179955455,
            "unit": "ns",
            "range": "± 869735.324637329"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3015292230.7,
            "unit": "ns",
            "range": "± 990258.2400483724"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3174404450.6,
            "unit": "ns",
            "range": "± 556507.9231720784"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013835957.2,
            "unit": "ns",
            "range": "± 422622.2955704064"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179182743.25,
            "unit": "ns",
            "range": "± 286469.097629948"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015358261.8,
            "unit": "ns",
            "range": "± 458730.66447066737"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3175579493.8,
            "unit": "ns",
            "range": "± 1073137.7304180018"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014007115.6,
            "unit": "ns",
            "range": "± 882102.8292258789"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178533982,
            "unit": "ns",
            "range": "± 366801.7349404625"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8893818,
            "unit": "ns",
            "range": "± 469074.1949218813"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6270199,
            "unit": "ns",
            "range": "± 102458.37633996658"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 9253051.75,
            "unit": "ns",
            "range": "± 524776.8209585453"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7651930.7,
            "unit": "ns",
            "range": "± 272205.9731886254"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 712604.9521484375,
            "unit": "ns",
            "range": "± 1322.326201235339"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 124965.35278320312,
            "unit": "ns",
            "range": "± 1597.3373099814153"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8914904.25,
            "unit": "ns",
            "range": "± 310280.4547312226"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5462679.4,
            "unit": "ns",
            "range": "± 26991.319207190383"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 10553663.777777778,
            "unit": "ns",
            "range": "± 2119829.0145696173"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8352039.222222222,
            "unit": "ns",
            "range": "± 440357.20940924133"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 3454302.17890625,
            "unit": "ns",
            "range": "± 18219.45524793772"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1249633.11171875,
            "unit": "ns",
            "range": "± 34972.601932153964"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9104870.2,
            "unit": "ns",
            "range": "± 997274.6944488041"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5489852.8,
            "unit": "ns",
            "range": "± 23535.173822269604"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8803858.25,
            "unit": "ns",
            "range": "± 672754.8049128725"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6645606.3,
            "unit": "ns",
            "range": "± 79973.69575165185"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 658092.7396484375,
            "unit": "ns",
            "range": "± 129956.0504132017"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8776200.25,
            "unit": "ns",
            "range": "± 257251.75134972358"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5500569.4,
            "unit": "ns",
            "range": "± 23241.15313260797"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 24539458.3,
            "unit": "ns",
            "range": "± 2777360.5876503526"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 9682188.833333334,
            "unit": "ns",
            "range": "± 780585.1831179286"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8125302.1828125,
            "unit": "ns",
            "range": "± 1050042.7066411257"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20346.11111111111,
            "unit": "ns",
            "range": "± 5631.268761221676"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 11004.055555555555,
            "unit": "ns",
            "range": "± 770.2292047032349"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14208.611111111111,
            "unit": "ns",
            "range": "± 1327.813093440154"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 35865.055555555555,
            "unit": "ns",
            "range": "± 7460.239994650157"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21958.4,
            "unit": "ns",
            "range": "± 288.747948525661"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3945.3,
            "unit": "ns",
            "range": "± 135.61612490162568"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 25396.166666666668,
            "unit": "ns",
            "range": "± 5472.793368107369"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 58899.88888888889,
            "unit": "ns",
            "range": "± 11372.36029200232"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 52129.875,
            "unit": "ns",
            "range": "± 292.1276323311928"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 325.8888888888889,
            "unit": "ns",
            "range": "± 15.854371987281963"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 368.1111111111111,
            "unit": "ns",
            "range": "± 44.428156737716584"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 401.55555555555554,
            "unit": "ns",
            "range": "± 41.925860489413665"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 349.9,
            "unit": "ns",
            "range": "± 15.130543061414992"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1243,
            "unit": "ns",
            "range": "± 63.17700003429518"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1101.7777777777778,
            "unit": "ns",
            "range": "± 48.47364690679302"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1443,
            "unit": "ns",
            "range": "± 125.20316822402432"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 531.4,
            "unit": "ns",
            "range": "± 27.657428176411003"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 546.4,
            "unit": "ns",
            "range": "± 57.486616800009294"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 612.2222222222222,
            "unit": "ns",
            "range": "± 38.466796649116034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 203.33333333333334,
            "unit": "ns",
            "range": "± 21.213203435596423"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 140.875,
            "unit": "ns",
            "range": "± 21.31691146216342"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 338.1,
            "unit": "ns",
            "range": "± 17.527438806371887"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 3486.277777777778,
            "unit": "ns",
            "range": "± 67.91313896768757"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3180.5,
            "unit": "ns",
            "range": "± 305.03633663330453"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1061.388888888889,
            "unit": "ns",
            "range": "± 80.58294553508894"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 401.05555555555554,
            "unit": "ns",
            "range": "± 62.592154282927325"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2571.1111111111113,
            "unit": "ns",
            "range": "± 54.46201530526677"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3246.5,
            "unit": "ns",
            "range": "± 169.5877354056006"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 75313.61111111111,
            "unit": "ns",
            "range": "± 5071.268737812177"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 104623.38888888889,
            "unit": "ns",
            "range": "± 8804.40056796095"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 388.2,
            "unit": "ns",
            "range": "± 85.61126613296224"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 371.72222222222223,
            "unit": "ns",
            "range": "± 10.533016872883309"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 425,
            "unit": "ns",
            "range": "± 36.84426685388108"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 352.6,
            "unit": "ns",
            "range": "± 77.19880540238661"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1344,
            "unit": "ns",
            "range": "± 157.223831950927"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1266.9,
            "unit": "ns",
            "range": "± 72.93901562264192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1338,
            "unit": "ns",
            "range": "± 55.21674464226308"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 526.1111111111111,
            "unit": "ns",
            "range": "± 26.619750395357034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 528.1111111111111,
            "unit": "ns",
            "range": "± 20.09007494040555"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 597.4,
            "unit": "ns",
            "range": "± 26.026482239783725"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 272.3333333333333,
            "unit": "ns",
            "range": "± 6.819090848492928"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 166.44444444444446,
            "unit": "ns",
            "range": "± 15.306135298558475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 309.3,
            "unit": "ns",
            "range": "± 21.55123095221142"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2876,
            "unit": "ns",
            "range": "± 428.91413282070033"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3752.4,
            "unit": "ns",
            "range": "± 177.02303679339465"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1436.5555555555557,
            "unit": "ns",
            "range": "± 204.75235719712182"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 410.9,
            "unit": "ns",
            "range": "± 41.20733483791987"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2744.8888888888887,
            "unit": "ns",
            "range": "± 58.80145500845291"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3341.5,
            "unit": "ns",
            "range": "± 56.295352087598374"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 77283.38888888889,
            "unit": "ns",
            "range": "± 4896.991128347193"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 104809.16666666667,
            "unit": "ns",
            "range": "± 6281.988120810162"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 392,
            "unit": "ns",
            "range": "± 18.38628716069548"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 354.1,
            "unit": "ns",
            "range": "± 53.9679946305627"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 390.6,
            "unit": "ns",
            "range": "± 88.5339859413698"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 314.9,
            "unit": "ns",
            "range": "± 15.877657257920642"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1399.8,
            "unit": "ns",
            "range": "± 161.8401474706858"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1242.125,
            "unit": "ns",
            "range": "± 20.003124755897513"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1437.5,
            "unit": "ns",
            "range": "± 161.3808469979563"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 555.8888888888889,
            "unit": "ns",
            "range": "± 34.25800798515744"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 549.9,
            "unit": "ns",
            "range": "± 107.13900628000367"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 567.5,
            "unit": "ns",
            "range": "± 88.83348968091308"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 236.1,
            "unit": "ns",
            "range": "± 32.2505813901083"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 148.8,
            "unit": "ns",
            "range": "± 34.13632799363295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 322.1,
            "unit": "ns",
            "range": "± 22.333084575729046"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2791.9,
            "unit": "ns",
            "range": "± 99.04370527981855"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3201.8888888888887,
            "unit": "ns",
            "range": "± 62.680627877448"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1324.111111111111,
            "unit": "ns",
            "range": "± 26.666666666666668"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 497.2,
            "unit": "ns",
            "range": "± 74.4338632612872"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2771,
            "unit": "ns",
            "range": "± 80.20865646193415"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 4169.4,
            "unit": "ns",
            "range": "± 237.16061130709616"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 137598.77777777778,
            "unit": "ns",
            "range": "± 17730.865621690453"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 112073.33333333333,
            "unit": "ns",
            "range": "± 12247.659296779937"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "debfc083600dd7b384ff95eafb53b0b4be6dcd56",
          "message": "Add GitVersion configuration to fix NuGet version format\n\nNuGet was rejecting versions like 0.0.1-294 where the pre-release\ntag is purely numeric. Added GitVersion.yml with ContinuousDelivery\nmode and 'ci' label to produce valid versions like 0.0.1-ci.294.\n\nCo-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>",
          "timestamp": "2026-01-25T23:56:27Z",
          "tree_id": "dc3c731ee824cdd7a8d2c5067553ee55e6f3e5cc",
          "url": "https://github.com/thomhurst/Dekaf/commit/debfc083600dd7b384ff95eafb53b0b4be6dcd56"
        },
        "date": 1769386019387,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 141.17289426922798,
            "unit": "ns",
            "range": "± 0.43641248655194875"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 128.44921836588117,
            "unit": "ns",
            "range": "± 0.6393492447828639"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 153913.2212890625,
            "unit": "ns",
            "range": "± 886.5869901202575"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 129.74701804584927,
            "unit": "ns",
            "range": "± 0.7810828068574464"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017742298.4,
            "unit": "ns",
            "range": "± 705780.368268557"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176289734.5,
            "unit": "ns",
            "range": "± 516398.1022308919"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014604002.75,
            "unit": "ns",
            "range": "± 893622.35902025"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180963132.4,
            "unit": "ns",
            "range": "± 1084972.5152681519"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014595136,
            "unit": "ns",
            "range": "± 318878.3815845784"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3173828509.5,
            "unit": "ns",
            "range": "± 445223.5507233042"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013467252.25,
            "unit": "ns",
            "range": "± 208602.27357561726"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3179619641.2,
            "unit": "ns",
            "range": "± 270582.62687615404"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014379427.5,
            "unit": "ns",
            "range": "± 160265.7496119076"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3175246488.6,
            "unit": "ns",
            "range": "± 483399.9751740788"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013820183.3,
            "unit": "ns",
            "range": "± 96625.10425194894"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3180189683,
            "unit": "ns",
            "range": "± 736461.1703901978"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014468423.6,
            "unit": "ns",
            "range": "± 479840.23162590276"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174767326.5,
            "unit": "ns",
            "range": "± 540486.4061898319"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014181541,
            "unit": "ns",
            "range": "± 1136674.4859580512"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179211143.8,
            "unit": "ns",
            "range": "± 323995.9403722522"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8784868.555555556,
            "unit": "ns",
            "range": "± 210424.31629514156"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5975602.611111111,
            "unit": "ns",
            "range": "± 57740.28593937781"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8687367.777777778,
            "unit": "ns",
            "range": "± 438342.8357965879"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7336143.388888889,
            "unit": "ns",
            "range": "± 179807.39457711717"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 699023.3552734375,
            "unit": "ns",
            "range": "± 24300.75573371031"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 132145.11435546874,
            "unit": "ns",
            "range": "± 1473.20060840569"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8901297.055555556,
            "unit": "ns",
            "range": "± 132087.18501345156"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5421590.9,
            "unit": "ns",
            "range": "± 22369.494443450338"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 11155089.4,
            "unit": "ns",
            "range": "± 3112717.966973351"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8276716.777777778,
            "unit": "ns",
            "range": "± 206560.97207469866"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 6408991.924479167,
            "unit": "ns",
            "range": "± 21054.05997353266"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1307943.6166015626,
            "unit": "ns",
            "range": "± 10710.170388994991"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8926731.1,
            "unit": "ns",
            "range": "± 298625.4947010274"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5428698.8,
            "unit": "ns",
            "range": "± 26207.24595645682"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8982783.3,
            "unit": "ns",
            "range": "± 218299.33884554027"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6739166.9,
            "unit": "ns",
            "range": "± 264349.9276654168"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 933510.1037109375,
            "unit": "ns",
            "range": "± 33980.51803847901"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8902484.611111112,
            "unit": "ns",
            "range": "± 143458.11210719706"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5425171.055555556,
            "unit": "ns",
            "range": "± 20317.157226781943"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 26791819,
            "unit": "ns",
            "range": "± 496761.7204573177"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 13544135.3,
            "unit": "ns",
            "range": "± 3411462.1391777345"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 10144138.24375,
            "unit": "ns",
            "range": "± 211071.17493096399"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 24613.11111111111,
            "unit": "ns",
            "range": "± 9711.802245778643"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 12863,
            "unit": "ns",
            "range": "± 1459.739036799234"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14369.4,
            "unit": "ns",
            "range": "± 2168.5838415375542"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 33015.666666666664,
            "unit": "ns",
            "range": "± 7225.475866681722"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 20990.1,
            "unit": "ns",
            "range": "± 899.1091208029819"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 2430.222222222222,
            "unit": "ns",
            "range": "± 236.25239563747166"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 33538.77777777778,
            "unit": "ns",
            "range": "± 4503.822314928115"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 53553.666666666664,
            "unit": "ns",
            "range": "± 11683.618350066045"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 51199.6,
            "unit": "ns",
            "range": "± 3946.657304324481"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 411.2,
            "unit": "ns",
            "range": "± 14.838388651662207"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 343.5,
            "unit": "ns",
            "range": "± 12.874393189583733"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 445,
            "unit": "ns",
            "range": "± 40.527768258318886"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 332.55555555555554,
            "unit": "ns",
            "range": "± 15.500896031448562"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1603,
            "unit": "ns",
            "range": "± 150.01573991492432"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1514.4,
            "unit": "ns",
            "range": "± 165.0408703590989"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1751.1,
            "unit": "ns",
            "range": "± 112.58818765749805"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 572.0555555555555,
            "unit": "ns",
            "range": "± 62.612121652103255"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 537.2,
            "unit": "ns",
            "range": "± 59.38396715912095"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 521.3333333333334,
            "unit": "ns",
            "range": "± 23.73288857261164"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 274,
            "unit": "ns",
            "range": "± 20.027758514399736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 114.55555555555556,
            "unit": "ns",
            "range": "± 11.74852236571807"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 272.77777777777777,
            "unit": "ns",
            "range": "± 12.01850425154663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2467.5555555555557,
            "unit": "ns",
            "range": "± 72.083477841859"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2964.75,
            "unit": "ns",
            "range": "± 37.961446607992016"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1424.2,
            "unit": "ns",
            "range": "± 220.2512706483716"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 533.7777777777778,
            "unit": "ns",
            "range": "± 16.56636485305224"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2817,
            "unit": "ns",
            "range": "± 77.82351829620657"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3340.6,
            "unit": "ns",
            "range": "± 61.33007237707924"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 76783.33333333333,
            "unit": "ns",
            "range": "± 6102.323942564833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 111198.55555555556,
            "unit": "ns",
            "range": "± 7809.446509054132"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 483.44444444444446,
            "unit": "ns",
            "range": "± 31.480593669398576"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 350.2,
            "unit": "ns",
            "range": "± 40.49910835781176"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 414.1666666666667,
            "unit": "ns",
            "range": "± 35.78058132562969"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 319.2,
            "unit": "ns",
            "range": "± 14.950287994253191"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1252.888888888889,
            "unit": "ns",
            "range": "± 24.446085803480095"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1430.4,
            "unit": "ns",
            "range": "± 184.5403900384833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1311.9444444444443,
            "unit": "ns",
            "range": "± 58.11865258054232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 548.4,
            "unit": "ns",
            "range": "± 31.861505858254024"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 526.75,
            "unit": "ns",
            "range": "± 20.211382931407737"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 543.8,
            "unit": "ns",
            "range": "± 22.582195543293736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 280,
            "unit": "ns",
            "range": "± 17.320508075688775"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 159.77777777777777,
            "unit": "ns",
            "range": "± 13.818264885449418"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 340.1111111111111,
            "unit": "ns",
            "range": "± 31.37452965561573"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2596.6,
            "unit": "ns",
            "range": "± 59.25500447688411"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2972.8888888888887,
            "unit": "ns",
            "range": "± 110.18217238333573"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1195.625,
            "unit": "ns",
            "range": "± 119.24517061200304"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 481.1,
            "unit": "ns",
            "range": "± 72.30521726987925"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2932.1,
            "unit": "ns",
            "range": "± 43.54933345579981"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3327.2,
            "unit": "ns",
            "range": "± 41.604086337762546"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 82017.5,
            "unit": "ns",
            "range": "± 5077.332454856867"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 104894.33333333333,
            "unit": "ns",
            "range": "± 5342.929393132572"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 445.4,
            "unit": "ns",
            "range": "± 55.04785796619762"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 322.4,
            "unit": "ns",
            "range": "± 13.841965178398622"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 395.6666666666667,
            "unit": "ns",
            "range": "± 20.633710281963346"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 330.2,
            "unit": "ns",
            "range": "± 46.38198884145536"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1357.125,
            "unit": "ns",
            "range": "± 48.03402811698034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1247.4444444444443,
            "unit": "ns",
            "range": "± 40.645144578138456"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1315.9444444444443,
            "unit": "ns",
            "range": "± 29.568188611712046"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 589.7,
            "unit": "ns",
            "range": "± 57.44137107617742"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 593.7,
            "unit": "ns",
            "range": "± 29.758472183004734"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 581.6,
            "unit": "ns",
            "range": "± 27.568903577118267"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 234.2,
            "unit": "ns",
            "range": "± 63.392603844787935"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 143.9,
            "unit": "ns",
            "range": "± 15.737075826072504"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 286.55555555555554,
            "unit": "ns",
            "range": "± 13.248689662671467"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2773.4,
            "unit": "ns",
            "range": "± 40.934093369708336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 4125,
            "unit": "ns",
            "range": "± 160.15071027004532"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1330.75,
            "unit": "ns",
            "range": "± 11.272596354497422"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 480,
            "unit": "ns",
            "range": "± 43.46454493799132"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2909.3888888888887,
            "unit": "ns",
            "range": "± 75.57189365836423"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3288.7,
            "unit": "ns",
            "range": "± 83.54247090219708"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 79567.75,
            "unit": "ns",
            "range": "± 4288.732305205617"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 104114.22222222222,
            "unit": "ns",
            "range": "± 5160.289787835994"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a3b903dd7ea3f2260749666e62208f54644cdc2c",
          "message": "Merge pull request #117 from thomhurst/perf/producer-optimizations\n\nImprove producer performance with multi-connection support and serialization optimizations",
          "timestamp": "2026-01-26T00:30:24Z",
          "tree_id": "e55fc9a98ffc10f0b873f3c1c23baddab51cdba7",
          "url": "https://github.com/thomhurst/Dekaf/commit/a3b903dd7ea3f2260749666e62208f54644cdc2c"
        },
        "date": 1769388049815,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 140.56051511764525,
            "unit": "ns",
            "range": "± 1.376221898073731"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 133.0896523952484,
            "unit": "ns",
            "range": "± 1.5728852674915887"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 151311.7840576172,
            "unit": "ns",
            "range": "± 749.5013986895267"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 130.5268450975418,
            "unit": "ns",
            "range": "± 1.1754269993272783"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3018193988.75,
            "unit": "ns",
            "range": "± 688791.5649514373"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176354311.25,
            "unit": "ns",
            "range": "± 326398.42542448535"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015063354.2,
            "unit": "ns",
            "range": "± 1024650.7317026128"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180687014.1,
            "unit": "ns",
            "range": "± 1291457.003985731"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3015280696.75,
            "unit": "ns",
            "range": "± 505520.57203729765"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3174745082.9,
            "unit": "ns",
            "range": "± 616118.0344968812"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013638349.8,
            "unit": "ns",
            "range": "± 1194515.9444960123"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3179549641.6,
            "unit": "ns",
            "range": "± 600624.7578041551"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014327919.2,
            "unit": "ns",
            "range": "± 832645.079117267"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3175067739,
            "unit": "ns",
            "range": "± 580782.481337032"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013184631.5,
            "unit": "ns",
            "range": "± 292648.78770920384"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3180205094.8,
            "unit": "ns",
            "range": "± 457264.1441209884"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015054716,
            "unit": "ns",
            "range": "± 1092396.1585546702"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3175348473.4,
            "unit": "ns",
            "range": "± 903687.9949934601"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014043933,
            "unit": "ns",
            "range": "± 603997.7796378891"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179394508.1,
            "unit": "ns",
            "range": "± 846997.2013550575"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8678969.555555556,
            "unit": "ns",
            "range": "± 371488.7487664031"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5995549.3,
            "unit": "ns",
            "range": "± 70109.75221877324"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8647426.333333334,
            "unit": "ns",
            "range": "± 432548.1712878925"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7436149.9,
            "unit": "ns",
            "range": "± 229464.59137084504"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 892315.0180053711,
            "unit": "ns",
            "range": "± 2300.89888404205"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 153688.82001953124,
            "unit": "ns",
            "range": "± 19601.240088965005"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8664705,
            "unit": "ns",
            "range": "± 228422.91375283114"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5414798.5,
            "unit": "ns",
            "range": "± 30670.717207134236"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12666294,
            "unit": "ns",
            "range": "± 851588.9519141556"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8401624.222222222,
            "unit": "ns",
            "range": "± 342912.10015971214"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 4140700.6669921875,
            "unit": "ns",
            "range": "± 14703.88766563567"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1305563.962109375,
            "unit": "ns",
            "range": "± 18686.188254313773"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8461580.944444444,
            "unit": "ns",
            "range": "± 327670.6489766482"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5494858.5,
            "unit": "ns",
            "range": "± 57181.6770128334"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8756875.333333334,
            "unit": "ns",
            "range": "± 267466.7178374722"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6744377.5,
            "unit": "ns",
            "range": "± 43193.910695451814"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 1001021.4673828125,
            "unit": "ns",
            "range": "± 47017.33722966412"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8797011.6,
            "unit": "ns",
            "range": "± 279162.79550129885"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5406658.2,
            "unit": "ns",
            "range": "± 17489.020300367505"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 27589458.111111112,
            "unit": "ns",
            "range": "± 764402.3469865926"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11937986,
            "unit": "ns",
            "range": "± 627331.0364247492"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8989120.28515625,
            "unit": "ns",
            "range": "± 32594.593190593838"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 24725.11111111111,
            "unit": "ns",
            "range": "± 6210.770613306461"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15081.1,
            "unit": "ns",
            "range": "± 1082.2192425238468"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14697.8,
            "unit": "ns",
            "range": "± 136.681137445272"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 49601,
            "unit": "ns",
            "range": "± 10533.116644659358"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 18939.375,
            "unit": "ns",
            "range": "± 299.7031567115321"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3170.25,
            "unit": "ns",
            "range": "± 200.16403987015963"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 88883.16666666667,
            "unit": "ns",
            "range": "± 17973.093925365218"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 88944.44444444444,
            "unit": "ns",
            "range": "± 13932.45463935834"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 53071.88888888889,
            "unit": "ns",
            "range": "± 523.8579111849998"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 380.2,
            "unit": "ns",
            "range": "± 23.384705352953336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 373.44444444444446,
            "unit": "ns",
            "range": "± 22.176063171306527"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 406.875,
            "unit": "ns",
            "range": "± 9.226321043622967"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 448.4,
            "unit": "ns",
            "range": "± 29.356808788119704"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1604.6666666666667,
            "unit": "ns",
            "range": "± 50.84289527554465"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1213,
            "unit": "ns",
            "range": "± 50.870205206759074"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1488,
            "unit": "ns",
            "range": "± 95.43322272667942"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 669,
            "unit": "ns",
            "range": "± 33.10211473607087"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 753,
            "unit": "ns",
            "range": "± 120.90124528354168"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 645.7,
            "unit": "ns",
            "range": "± 91.83566240240711"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 275,
            "unit": "ns",
            "range": "± 14.298407059684811"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 207,
            "unit": "ns",
            "range": "± 15.67730135507313"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 407.1111111111111,
            "unit": "ns",
            "range": "± 36.484395446699004"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2546.222222222222,
            "unit": "ns",
            "range": "± 39.98055082717651"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3126.3333333333335,
            "unit": "ns",
            "range": "± 115.66222373791712"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1151.5555555555557,
            "unit": "ns",
            "range": "± 86.95273300924922"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 474.6,
            "unit": "ns",
            "range": "± 43.01730917768903"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2922.1,
            "unit": "ns",
            "range": "± 487.90993021253416"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3321.1111111111113,
            "unit": "ns",
            "range": "± 35.08719297850872"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 103350.22222222222,
            "unit": "ns",
            "range": "± 15281.083385821978"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 130769.22222222222,
            "unit": "ns",
            "range": "± 18127.4272842134"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 487.1,
            "unit": "ns",
            "range": "± 15.6236999459155"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 364.55555555555554,
            "unit": "ns",
            "range": "± 13.45465636044926"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 426.9,
            "unit": "ns",
            "range": "± 21.172308959267212"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 379.7,
            "unit": "ns",
            "range": "± 30.597022367834715"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1424.875,
            "unit": "ns",
            "range": "± 32.76948711399502"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1350.1,
            "unit": "ns",
            "range": "± 45.098534097881476"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1503.4444444444443,
            "unit": "ns",
            "range": "± 63.40763185751205"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 605.9,
            "unit": "ns",
            "range": "± 33.58637289801393"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 641.625,
            "unit": "ns",
            "range": "± 24.2660109147401"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 593.8,
            "unit": "ns",
            "range": "± 33.2726113592947"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 265,
            "unit": "ns",
            "range": "± 41.166329283367816"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 200.88888888888889,
            "unit": "ns",
            "range": "± 32.044673677712986"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 326.5,
            "unit": "ns",
            "range": "± 17.82454166912254"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2574.6,
            "unit": "ns",
            "range": "± 69.26952191741088"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3004,
            "unit": "ns",
            "range": "± 33.751190455195164"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1276.7222222222222,
            "unit": "ns",
            "range": "± 29.752217471046496"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 577.7,
            "unit": "ns",
            "range": "± 47.64929753475444"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2939.3333333333335,
            "unit": "ns",
            "range": "± 56.78908345800274"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3230,
            "unit": "ns",
            "range": "± 84.81008194784391"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 82544.11111111111,
            "unit": "ns",
            "range": "± 3406.414333446698"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 106582.22222222222,
            "unit": "ns",
            "range": "± 6177.876896187269"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 413.55555555555554,
            "unit": "ns",
            "range": "± 27.991566190154096"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 332.25,
            "unit": "ns",
            "range": "± 21.57875939767755"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 463.7,
            "unit": "ns",
            "range": "± 40.68046214093444"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 354.2,
            "unit": "ns",
            "range": "± 27.979357470185846"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 2003,
            "unit": "ns",
            "range": "± 49.876991546346154"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1392,
            "unit": "ns",
            "range": "± 18.027756377319946"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1578.1666666666667,
            "unit": "ns",
            "range": "± 76.8293563685132"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 606.2222222222222,
            "unit": "ns",
            "range": "± 19.305295761641272"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 651,
            "unit": "ns",
            "range": "± 24.675030756964475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 548.5555555555555,
            "unit": "ns",
            "range": "± 27.870733355578892"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 217.2,
            "unit": "ns",
            "range": "± 41.20895264111645"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 162.8,
            "unit": "ns",
            "range": "± 17.980236063213656"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 300.8333333333333,
            "unit": "ns",
            "range": "± 14.594519519326425"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 3319.5,
            "unit": "ns",
            "range": "± 193.84028362431673"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 2993.125,
            "unit": "ns",
            "range": "± 76.06939970466819"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1362,
            "unit": "ns",
            "range": "± 144.36604710095637"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 562.3,
            "unit": "ns",
            "range": "± 43.951488408623135"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3569.1,
            "unit": "ns",
            "range": "± 249.09322217461747"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3336.777777777778,
            "unit": "ns",
            "range": "± 62.59148859425253"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 80392.27777777778,
            "unit": "ns",
            "range": "± 5946.320307084411"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 109657.88888888889,
            "unit": "ns",
            "range": "± 5221.175524832613"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "690f581af4a3bfbf1f375065cd49b7d39cc22cb9",
          "message": "Merge pull request #118 from thomhurst/perf/librdkafka-inspired-optimizations\n\nperf: librdkafka-inspired performance optimizations",
          "timestamp": "2026-01-26T07:59:05Z",
          "tree_id": "db48025e3fa8079cda60a96518595aa3ae60dad7",
          "url": "https://github.com/thomhurst/Dekaf/commit/690f581af4a3bfbf1f375065cd49b7d39cc22cb9"
        },
        "date": 1769414973819,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 134.2638070821762,
            "unit": "ns",
            "range": "± 0.9979722105331206"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 127.2297581911087,
            "unit": "ns",
            "range": "± 1.351334589431414"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 145676.8942138672,
            "unit": "ns",
            "range": "± 971.4379127665402"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 127.78220221996307,
            "unit": "ns",
            "range": "± 1.2451880817916514"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017262819.1,
            "unit": "ns",
            "range": "± 1234895.2768009522"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176004896,
            "unit": "ns",
            "range": "± 384062.98734908056"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3013824783.25,
            "unit": "ns",
            "range": "± 419593.2824179267"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180461969.5,
            "unit": "ns",
            "range": "± 396349.306878415"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014707490.4,
            "unit": "ns",
            "range": "± 614259.7479110934"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3174004449.8,
            "unit": "ns",
            "range": "± 740624.6286629955"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014257122.6,
            "unit": "ns",
            "range": "± 1343768.4929082093"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3179575756.5,
            "unit": "ns",
            "range": "± 309903.88195535727"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014269774.3,
            "unit": "ns",
            "range": "± 726041.9443291138"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3174913694.3,
            "unit": "ns",
            "range": "± 1337342.975891824"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013245004,
            "unit": "ns",
            "range": "± 612157.2930873241"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3180172859.6,
            "unit": "ns",
            "range": "± 799255.1875679633"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015064027.7,
            "unit": "ns",
            "range": "± 642129.0760086323"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174444558.2,
            "unit": "ns",
            "range": "± 1115795.1922748636"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014057978.3,
            "unit": "ns",
            "range": "± 508595.994184677"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179106595,
            "unit": "ns",
            "range": "± 840111.155445516"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8142106.3,
            "unit": "ns",
            "range": "± 620681.0217092279"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6624153.4,
            "unit": "ns",
            "range": "± 209121.6408808785"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 11707245,
            "unit": "ns",
            "range": "± 2062200.9650482796"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 8165156.2,
            "unit": "ns",
            "range": "± 189183.0927768711"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 675847.5101318359,
            "unit": "ns",
            "range": "± 1067.5562205135577"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 236356.3716308594,
            "unit": "ns",
            "range": "± 51288.19264183171"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8259508.2,
            "unit": "ns",
            "range": "± 631238.1547560231"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5753801.3,
            "unit": "ns",
            "range": "± 89090.01924745056"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12968539.222222222,
            "unit": "ns",
            "range": "± 1029526.2668534467"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 9548355.444444444,
            "unit": "ns",
            "range": "± 516504.52361259895"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 6462783.42265625,
            "unit": "ns",
            "range": "± 1638082.5605581747"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1693715.7651909722,
            "unit": "ns",
            "range": "± 172649.11829123728"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8219320.8,
            "unit": "ns",
            "range": "± 816149.5163865573"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5759269.555555556,
            "unit": "ns",
            "range": "± 92285.77992723353"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8490734.1,
            "unit": "ns",
            "range": "± 686663.1746992845"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6471077.888888889,
            "unit": "ns",
            "range": "± 161696.57917102362"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 727543.178515625,
            "unit": "ns",
            "range": "± 25299.992736618406"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8633243.9,
            "unit": "ns",
            "range": "± 398481.32215595193"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5600894.375,
            "unit": "ns",
            "range": "± 64462.44947129519"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 34442092.88888889,
            "unit": "ns",
            "range": "± 2429633.413777706"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12722170.722222222,
            "unit": "ns",
            "range": "± 622898.446695723"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 6706518.56875,
            "unit": "ns",
            "range": "± 1069950.7033223587"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 21149.88888888889,
            "unit": "ns",
            "range": "± 5547.454403698251"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15027.5,
            "unit": "ns",
            "range": "± 1068.7640057561819"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 18425.1,
            "unit": "ns",
            "range": "± 1845.3612383487412"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 30486.444444444445,
            "unit": "ns",
            "range": "± 5636.534443057877"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 18570.375,
            "unit": "ns",
            "range": "± 161.52482117972724"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3095.125,
            "unit": "ns",
            "range": "± 35.409593131159895"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 76631.77777777778,
            "unit": "ns",
            "range": "± 28814.46252222041"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 95681.11111111111,
            "unit": "ns",
            "range": "± 22063.88335291662"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 53269,
            "unit": "ns",
            "range": "± 846.7799511763878"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 424.4,
            "unit": "ns",
            "range": "± 20.413503156271613"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 350.1111111111111,
            "unit": "ns",
            "range": "± 26.459612829954846"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 388,
            "unit": "ns",
            "range": "± 42.16930163045151"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 423.9,
            "unit": "ns",
            "range": "± 48.41762764393426"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1576.5,
            "unit": "ns",
            "range": "± 111.24672279817206"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1175.2222222222222,
            "unit": "ns",
            "range": "± 69.28884790819114"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1510,
            "unit": "ns",
            "range": "± 34.041990876814815"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 577.6,
            "unit": "ns",
            "range": "± 58.05016987246654"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 683.8888888888889,
            "unit": "ns",
            "range": "± 11.016401913107162"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 603.75,
            "unit": "ns",
            "range": "± 22.108498689094976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 273,
            "unit": "ns",
            "range": "± 32.74819757550703"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 93,
            "unit": "ns",
            "range": "± 26.091186251299497"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 288,
            "unit": "ns",
            "range": "± 31.640339933558096"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 3100.6666666666665,
            "unit": "ns",
            "range": "± 112.30872628607271"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2906.1,
            "unit": "ns",
            "range": "± 115.06080518095156"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1154.8,
            "unit": "ns",
            "range": "± 34.48606804042597"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 497.44444444444446,
            "unit": "ns",
            "range": "± 71.07058306907139"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2930.1,
            "unit": "ns",
            "range": "± 55.06854314801186"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3205.7,
            "unit": "ns",
            "range": "± 176.96267654194452"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 74488.11111111111,
            "unit": "ns",
            "range": "± 4711.64200264739"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 112695.16666666667,
            "unit": "ns",
            "range": "± 13474.562506812605"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 421.77777777777777,
            "unit": "ns",
            "range": "± 21.276617316773933"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 416.5,
            "unit": "ns",
            "range": "± 23.311554461866574"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 464.3,
            "unit": "ns",
            "range": "± 17.882642111525044"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 379.3888888888889,
            "unit": "ns",
            "range": "± 20.70292518247388"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1428.2,
            "unit": "ns",
            "range": "± 91.19064279482481"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1297.6666666666667,
            "unit": "ns",
            "range": "± 51.85556864985669"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1384.6666666666667,
            "unit": "ns",
            "range": "± 43.87482193696061"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 609.6,
            "unit": "ns",
            "range": "± 36.94801152370119"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 691.4,
            "unit": "ns",
            "range": "± 37.29894249320094"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 629.7,
            "unit": "ns",
            "range": "± 22.779376637651875"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 268.1,
            "unit": "ns",
            "range": "± 41.85278432261772"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 179.875,
            "unit": "ns",
            "range": "± 15.21688441924205"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 314.3,
            "unit": "ns",
            "range": "± 28.429444829847334"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2600.6111111111113,
            "unit": "ns",
            "range": "± 91.58936134241308"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2804.777777777778,
            "unit": "ns",
            "range": "± 56.53268474470715"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1304.2222222222222,
            "unit": "ns",
            "range": "± 27.28450923957483"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 509.5,
            "unit": "ns",
            "range": "± 37.04051835490427"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2543.625,
            "unit": "ns",
            "range": "± 81.64109171944516"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3185.2,
            "unit": "ns",
            "range": "± 45.82769662308785"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 79693.125,
            "unit": "ns",
            "range": "± 3730.7158615050967"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 119447.94444444444,
            "unit": "ns",
            "range": "± 16038.72882362495"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 450.3,
            "unit": "ns",
            "range": "± 104.04171172072178"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 360.2,
            "unit": "ns",
            "range": "± 30.143361163317905"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 428.6,
            "unit": "ns",
            "range": "± 41.363429902914646"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 383.125,
            "unit": "ns",
            "range": "± 17.671102318272542"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1452.5555555555557,
            "unit": "ns",
            "range": "± 141.6519600209534"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1268.4444444444443,
            "unit": "ns",
            "range": "± 59.55483001216423"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1635.125,
            "unit": "ns",
            "range": "± 34.65518926304029"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 641.5555555555555,
            "unit": "ns",
            "range": "± 52.98846834715812"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 658.6666666666666,
            "unit": "ns",
            "range": "± 24.474476501040833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 585.2222222222222,
            "unit": "ns",
            "range": "± 42.26043592350231"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 242,
            "unit": "ns",
            "range": "± 24.770053604212396"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 120.77777777777777,
            "unit": "ns",
            "range": "± 16.03728295081322"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 321.25,
            "unit": "ns",
            "range": "± 19.27804080146261"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2743.722222222222,
            "unit": "ns",
            "range": "± 66.92118083569987"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3276.125,
            "unit": "ns",
            "range": "± 63.80984585917309"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1467.4,
            "unit": "ns",
            "range": "± 40.486486085551505"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 502.6666666666667,
            "unit": "ns",
            "range": "± 27.92848008753788"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2999,
            "unit": "ns",
            "range": "± 28.819636163064732"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3778.3,
            "unit": "ns",
            "range": "± 430.17123205636244"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 125056.94444444444,
            "unit": "ns",
            "range": "± 20025.68472057267"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 107101.5,
            "unit": "ns",
            "range": "± 5384.357741245866"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9a87bbb16a4e8eea3097cd848b006d464f96c51b",
          "message": "Merge pull request #120 from thomhurst/perf/consumer-allocation-optimizations\n\nperf: Add zero-copy RawBytes deserializer and pool LazyRecordList",
          "timestamp": "2026-01-26T11:14:40Z",
          "tree_id": "f751416ac72929dd9069cbdbcf3abbe7f8aae6a4",
          "url": "https://github.com/thomhurst/Dekaf/commit/9a87bbb16a4e8eea3097cd848b006d464f96c51b"
        },
        "date": 1769426714857,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 145.72440285682677,
            "unit": "ns",
            "range": "± 3.145026468562146"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 129.92272503376006,
            "unit": "ns",
            "range": "± 2.9619614295315535"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 146976.8670654297,
            "unit": "ns",
            "range": "± 3357.3226429405377"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 127.10234088897705,
            "unit": "ns",
            "range": "± 2.3050886162449737"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3018102208.6,
            "unit": "ns",
            "range": "± 950755.2918702583"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176394538.6,
            "unit": "ns",
            "range": "± 1288964.542937586"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015210180,
            "unit": "ns",
            "range": "± 1151705.4397221105"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180547649.4,
            "unit": "ns",
            "range": "± 381006.225017781"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014721155.7,
            "unit": "ns",
            "range": "± 777051.9019523085"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3174545816.5,
            "unit": "ns",
            "range": "± 827413.8483528058"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013933084.25,
            "unit": "ns",
            "range": "± 348737.78030909796"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3181067246,
            "unit": "ns",
            "range": "± 1100292.5600037018"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014845515.2,
            "unit": "ns",
            "range": "± 321937.91437822295"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3175036960.4,
            "unit": "ns",
            "range": "± 988828.9385512036"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013790997,
            "unit": "ns",
            "range": "± 987799.4231252111"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3180164406.25,
            "unit": "ns",
            "range": "± 420510.44125869614"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015133477.8,
            "unit": "ns",
            "range": "± 504347.32324976206"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174901593.8,
            "unit": "ns",
            "range": "± 379401.9276403324"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014883437.2,
            "unit": "ns",
            "range": "± 462637.2624737441"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179431854.1,
            "unit": "ns",
            "range": "± 680050.943241975"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8497744.722222222,
            "unit": "ns",
            "range": "± 247991.37532169226"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6633625.125,
            "unit": "ns",
            "range": "± 49644.46222012078"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 13059863,
            "unit": "ns",
            "range": "± 2120104.5238423715"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 9012535.333333334,
            "unit": "ns",
            "range": "± 1743487.713681975"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 870591.2780273438,
            "unit": "ns",
            "range": "± 9269.819600787556"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 159713.10625,
            "unit": "ns",
            "range": "± 34226.85891791397"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8632185.75,
            "unit": "ns",
            "range": "± 220893.36430294014"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5447622.111111111,
            "unit": "ns",
            "range": "± 15075.562969956083"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12272818,
            "unit": "ns",
            "range": "± 1923521.423845534"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8377081.875,
            "unit": "ns",
            "range": "± 165588.08955178724"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 4015857.65859375,
            "unit": "ns",
            "range": "± 18337.961440663075"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1344783.87109375,
            "unit": "ns",
            "range": "± 10620.125851492803"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9067489.7,
            "unit": "ns",
            "range": "± 502850.1690848654"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5455759.2,
            "unit": "ns",
            "range": "± 17593.01433997786"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9061648.1,
            "unit": "ns",
            "range": "± 718662.1608457157"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6474882.3,
            "unit": "ns",
            "range": "± 453562.78095762123"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 901966.9185546875,
            "unit": "ns",
            "range": "± 149448.74081344434"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8657761.611111112,
            "unit": "ns",
            "range": "± 186064.25223255836"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5441910.6,
            "unit": "ns",
            "range": "± 30534.50573149444"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 28640367.666666668,
            "unit": "ns",
            "range": "± 941841.1939714147"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11953953.375,
            "unit": "ns",
            "range": "± 446984.84914909507"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8892426.28125,
            "unit": "ns",
            "range": "± 1335635.1724996965"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 21016.555555555555,
            "unit": "ns",
            "range": "± 5379.052265759999"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 14951.5,
            "unit": "ns",
            "range": "± 851.810131426012"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14228.375,
            "unit": "ns",
            "range": "± 86.85610020520805"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 52590,
            "unit": "ns",
            "range": "± 12247.106362320856"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 23183.9,
            "unit": "ns",
            "range": "± 324.2010521608809"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3121,
            "unit": "ns",
            "range": "± 102.74969586329684"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 68728.94444444444,
            "unit": "ns",
            "range": "± 16584.24258981331"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 113584.44444444444,
            "unit": "ns",
            "range": "± 15559.731304806577"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 53982.5,
            "unit": "ns",
            "range": "± 431.4887848236283"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 423.8,
            "unit": "ns",
            "range": "± 155.84397468123188"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 397.3333333333333,
            "unit": "ns",
            "range": "± 51.166395221864136"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 470.3,
            "unit": "ns",
            "range": "± 93.79267917415872"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 409,
            "unit": "ns",
            "range": "± 52.052857750559674"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1329.3333333333333,
            "unit": "ns",
            "range": "± 139.84008724253573"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1297.4444444444443,
            "unit": "ns",
            "range": "± 154.66666666666669"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1417.2777777777778,
            "unit": "ns",
            "range": "± 143.7530676001192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 564.3888888888889,
            "unit": "ns",
            "range": "± 64.27372022149575"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 667.5,
            "unit": "ns",
            "range": "± 66.50355044824467"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 493.1666666666667,
            "unit": "ns",
            "range": "± 88.1986394452885"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 357.55555555555554,
            "unit": "ns",
            "range": "± 46.02746764463343"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 208.44444444444446,
            "unit": "ns",
            "range": "± 45.6100622426431"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 354.4,
            "unit": "ns",
            "range": "± 119.0036880847723"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 1909.625,
            "unit": "ns",
            "range": "± 199.09110441489557"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2921.777777777778,
            "unit": "ns",
            "range": "± 685.241705126333"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1758.5,
            "unit": "ns",
            "range": "± 368.4575205426596"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 390.4,
            "unit": "ns",
            "range": "± 150.3966607194307"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2945.7,
            "unit": "ns",
            "range": "± 399.83275670376645"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 2736.625,
            "unit": "ns",
            "range": "± 81.34395666375153"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 103817.11111111111,
            "unit": "ns",
            "range": "± 47392.20566571165"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 104369.77777777778,
            "unit": "ns",
            "range": "± 13384.070483019897"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 398.8333333333333,
            "unit": "ns",
            "range": "± 59.69715236089574"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 184.5,
            "unit": "ns",
            "range": "± 44.85532298401161"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 307.1111111111111,
            "unit": "ns",
            "range": "± 86.27926234681837"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 400.3888888888889,
            "unit": "ns",
            "range": "± 47.6719111334034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1373.5,
            "unit": "ns",
            "range": "± 151.83296743461216"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1296.7222222222222,
            "unit": "ns",
            "range": "± 145.89017939684783"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1407.9444444444443,
            "unit": "ns",
            "range": "± 290.74091177159397"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 622.3333333333334,
            "unit": "ns",
            "range": "± 75.14319663149817"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 647.5,
            "unit": "ns",
            "range": "± 85.78461400507669"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 654.1111111111111,
            "unit": "ns",
            "range": "± 125.05743125105005"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 405.6,
            "unit": "ns",
            "range": "± 61.12864394955208"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 176.83333333333334,
            "unit": "ns",
            "range": "± 28.075790282732914"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 388.3333333333333,
            "unit": "ns",
            "range": "± 43.90330283703038"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2641.375,
            "unit": "ns",
            "range": "± 145.9965141462533"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2820.222222222222,
            "unit": "ns",
            "range": "± 372.08828850750524"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1294.7,
            "unit": "ns",
            "range": "± 208.09722620822114"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 464.1111111111111,
            "unit": "ns",
            "range": "± 82.24877574232404"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2841,
            "unit": "ns",
            "range": "± 450.9390202677076"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 2610.1666666666665,
            "unit": "ns",
            "range": "± 245.16677996824936"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 69897.11111111111,
            "unit": "ns",
            "range": "± 6409.684010238813"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 95177.88888888889,
            "unit": "ns",
            "range": "± 6261.396618256274"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 502.7,
            "unit": "ns",
            "range": "± 63.442099586946206"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 298.55555555555554,
            "unit": "ns",
            "range": "± 79.33648453125319"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 508,
            "unit": "ns",
            "range": "± 34.694585827104"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 110,
            "unit": "ns",
            "range": "± 46.7882769200027"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1280.2777777777778,
            "unit": "ns",
            "range": "± 169.93732504792595"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1374.611111111111,
            "unit": "ns",
            "range": "± 141.39699823939372"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1766.5,
            "unit": "ns",
            "range": "± 196.27644903157497"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 519.1,
            "unit": "ns",
            "range": "± 209.18795376407314"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 541.7777777777778,
            "unit": "ns",
            "range": "± 59.35860884862822"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 669.9444444444445,
            "unit": "ns",
            "range": "± 59.50653558877191"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 314.8333333333333,
            "unit": "ns",
            "range": "± 61.45730225123781"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 160,
            "unit": "ns",
            "range": "± 32.264531609803356"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 440.6,
            "unit": "ns",
            "range": "± 65.57345330069005"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2653.222222222222,
            "unit": "ns",
            "range": "± 202.41589474259288"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3141.9444444444443,
            "unit": "ns",
            "range": "± 375.238960900621"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1221.25,
            "unit": "ns",
            "range": "± 55.82562135793923"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 403.05555555555554,
            "unit": "ns",
            "range": "± 62.50422207961457"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3228.777777777778,
            "unit": "ns",
            "range": "± 505.2055962125167"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3058.75,
            "unit": "ns",
            "range": "± 206.70942338046837"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 68412.875,
            "unit": "ns",
            "range": "± 5796.759228532044"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 95357.88888888889,
            "unit": "ns",
            "range": "± 11066.799531079938"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3cb924c8ae1c1cccd95802c22603090b1cc2cd06",
          "message": "Merge pull request #119 from thomhurst/perf/producer-serialization-optimizations\n\nperf: Eliminate double-copy serialization and add fast Produce overload",
          "timestamp": "2026-01-26T12:24:27Z",
          "tree_id": "3f4954549f99a194791ec1202e88da76f9eb7175",
          "url": "https://github.com/thomhurst/Dekaf/commit/3cb924c8ae1c1cccd95802c22603090b1cc2cd06"
        },
        "date": 1769430896778,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 142.89124443531037,
            "unit": "ns",
            "range": "± 1.2053501132092854"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 138.18399906158447,
            "unit": "ns",
            "range": "± 1.0866999248631262"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 157801.76595052084,
            "unit": "ns",
            "range": "± 929.1269721591214"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 137.31246948242188,
            "unit": "ns",
            "range": "± 2.474698260442579"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3018994614.2,
            "unit": "ns",
            "range": "± 760446.4233929962"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3177178441.75,
            "unit": "ns",
            "range": "± 287325.660702712"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3016020217.6,
            "unit": "ns",
            "range": "± 559998.104145273"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3182282007.2,
            "unit": "ns",
            "range": "± 1209547.4055642465"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3015673411.75,
            "unit": "ns",
            "range": "± 494581.4416173033"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3175155233,
            "unit": "ns",
            "range": "± 668493.2153619811"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014778311.4,
            "unit": "ns",
            "range": "± 650376.5672564625"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3180747280.4,
            "unit": "ns",
            "range": "± 792776.238213722"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014814606.3,
            "unit": "ns",
            "range": "± 1023776.5171272488"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3175704172.7,
            "unit": "ns",
            "range": "± 1106288.0414095146"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013957774.25,
            "unit": "ns",
            "range": "± 213363.6471994468"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3181726045.9,
            "unit": "ns",
            "range": "± 1127030.6955854397"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015879559.75,
            "unit": "ns",
            "range": "± 365759.27259458054"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3176560937.5,
            "unit": "ns",
            "range": "± 531283.2943294002"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015292764.5,
            "unit": "ns",
            "range": "± 647170.835333299"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3181468957.6,
            "unit": "ns",
            "range": "± 897795.8244043018"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8581138.5,
            "unit": "ns",
            "range": "± 383798.16934114095"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6257220.2,
            "unit": "ns",
            "range": "± 199517.98741622828"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8742268.125,
            "unit": "ns",
            "range": "± 382889.2836813041"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7472631.5,
            "unit": "ns",
            "range": "± 289073.0540000995"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 646430.9069010416,
            "unit": "ns",
            "range": "± 14293.700994957358"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 133400.6928222656,
            "unit": "ns",
            "range": "± 2512.5095954096723"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8760432.8,
            "unit": "ns",
            "range": "± 129849.89571899289"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5438078.055555556,
            "unit": "ns",
            "range": "± 13766.505585215798"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12965917.055555556,
            "unit": "ns",
            "range": "± 375807.10489661817"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8419413.111111112,
            "unit": "ns",
            "range": "± 397019.9560785466"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 6562870.75,
            "unit": "ns",
            "range": "± 44871.01184237136"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1347520.6931640625,
            "unit": "ns",
            "range": "± 10105.332772206879"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8865894.333333334,
            "unit": "ns",
            "range": "± 258253.5024012259"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5440931.5,
            "unit": "ns",
            "range": "± 37020.71992816996"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9397203,
            "unit": "ns",
            "range": "± 874770.1961361052"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6825232.166666667,
            "unit": "ns",
            "range": "± 66980.14114272976"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 957898.1616210938,
            "unit": "ns",
            "range": "± 46283.39680935999"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8735575.666666666,
            "unit": "ns",
            "range": "± 159647.90225837607"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5424392.777777778,
            "unit": "ns",
            "range": "± 12018.941174015472"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 28734370.4,
            "unit": "ns",
            "range": "± 1127688.1275769663"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11885516.777777778,
            "unit": "ns",
            "range": "± 406782.6338884128"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8769805.0671875,
            "unit": "ns",
            "range": "± 1228350.5987604538"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20790.722222222223,
            "unit": "ns",
            "range": "± 5470.42260199744"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15322.9,
            "unit": "ns",
            "range": "± 518.8211959175659"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14337.111111111111,
            "unit": "ns",
            "range": "± 153.82656178667943"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 30663.11111111111,
            "unit": "ns",
            "range": "± 6433.066481943982"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 23954.8,
            "unit": "ns",
            "range": "± 395.55110640444144"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3159.8888888888887,
            "unit": "ns",
            "range": "± 47.31396317273698"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 64519.333333333336,
            "unit": "ns",
            "range": "± 12091.71019748654"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 80788.625,
            "unit": "ns",
            "range": "± 4834.815240597544"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 53450.75,
            "unit": "ns",
            "range": "± 597.0672969966259"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 455,
            "unit": "ns",
            "range": "± 37.92170296222937"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 310.1111111111111,
            "unit": "ns",
            "range": "± 41.687661377332155"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 435.8888888888889,
            "unit": "ns",
            "range": "± 32.30497037780891"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 356.5,
            "unit": "ns",
            "range": "± 34.021001916883215"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1359.375,
            "unit": "ns",
            "range": "± 48.04146274685173"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1304.1,
            "unit": "ns",
            "range": "± 96.1127231720939"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1716.875,
            "unit": "ns",
            "range": "± 142.688909870389"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 622.3333333333334,
            "unit": "ns",
            "range": "± 112.81954617884261"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 643.625,
            "unit": "ns",
            "range": "± 37.75271986417478"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 548.6,
            "unit": "ns",
            "range": "± 88.10751008474436"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 267,
            "unit": "ns",
            "range": "± 36.53004851412662"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 145.11111111111111,
            "unit": "ns",
            "range": "± 41.5163956902705"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 346.125,
            "unit": "ns",
            "range": "± 15.263752581103471"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2652.222222222222,
            "unit": "ns",
            "range": "± 120.86953480693323"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3230.3888888888887,
            "unit": "ns",
            "range": "± 359.8244170579744"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1431,
            "unit": "ns",
            "range": "± 147.0518615999131"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 784.3333333333334,
            "unit": "ns",
            "range": "± 45"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2845.875,
            "unit": "ns",
            "range": "± 159.19479307708886"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3392.6,
            "unit": "ns",
            "range": "± 479.6749362270708"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 72375.72222222222,
            "unit": "ns",
            "range": "± 5148.346088254406"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 118580.83333333333,
            "unit": "ns",
            "range": "± 12151.934588780505"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 425.6666666666667,
            "unit": "ns",
            "range": "± 32.810059433045836"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 395.4,
            "unit": "ns",
            "range": "± 47.993518080859396"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 455.4,
            "unit": "ns",
            "range": "± 62.38981398344516"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 325,
            "unit": "ns",
            "range": "± 91.75571432401969"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1840.7777777777778,
            "unit": "ns",
            "range": "± 160.78306641075247"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1180.611111111111,
            "unit": "ns",
            "range": "± 49.74295036596755"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1485.3333333333333,
            "unit": "ns",
            "range": "± 40.94508517514648"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 538.8888888888889,
            "unit": "ns",
            "range": "± 39.161347156489796"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 518.4444444444445,
            "unit": "ns",
            "range": "± 35.889103886524914"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 965.625,
            "unit": "ns",
            "range": "± 345.4956842815013"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 239.61111111111111,
            "unit": "ns",
            "range": "± 18.48948650209386"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 139.4,
            "unit": "ns",
            "range": "± 27.411270998948183"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 313,
            "unit": "ns",
            "range": "± 16.36391694484477"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2719.8888888888887,
            "unit": "ns",
            "range": "± 112.39933768092723"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3441.9,
            "unit": "ns",
            "range": "± 382.91787631292436"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1471.625,
            "unit": "ns",
            "range": "± 41.79349744363863"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 777.9,
            "unit": "ns",
            "range": "± 33.65329767562823"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 3409.9,
            "unit": "ns",
            "range": "± 213.5719758977963"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3288.777777777778,
            "unit": "ns",
            "range": "± 89.92743988596833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 74985.72222222222,
            "unit": "ns",
            "range": "± 5633.755558634439"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 125619.5,
            "unit": "ns",
            "range": "± 2810.3678457759634"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 422.55555555555554,
            "unit": "ns",
            "range": "± 20.013190094979304"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 371.22222222222223,
            "unit": "ns",
            "range": "± 9.884050002121825"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 401.22222222222223,
            "unit": "ns",
            "range": "± 17.633616884928752"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 279.875,
            "unit": "ns",
            "range": "± 21.682366107046526"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1416.611111111111,
            "unit": "ns",
            "range": "± 52.71964634850192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1321.1,
            "unit": "ns",
            "range": "± 41.02153905775193"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1977.7777777777778,
            "unit": "ns",
            "range": "± 164.3158374729729"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 548.2,
            "unit": "ns",
            "range": "± 115.07200644234317"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 508.3333333333333,
            "unit": "ns",
            "range": "± 23.879907872519105"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 577.3333333333334,
            "unit": "ns",
            "range": "± 28.952547383606856"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 245.55555555555554,
            "unit": "ns",
            "range": "± 32.82952600598701"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 176.625,
            "unit": "ns",
            "range": "± 35.83867623511943"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 343.8,
            "unit": "ns",
            "range": "± 27.27249326498934"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 3404.6111111111113,
            "unit": "ns",
            "range": "± 282.9882702712448"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3276.6666666666665,
            "unit": "ns",
            "range": "± 61.032778078668514"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1735.9,
            "unit": "ns",
            "range": "± 211.58158394970644"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 711.7777777777778,
            "unit": "ns",
            "range": "± 83.30482845816587"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3278.8,
            "unit": "ns",
            "range": "± 294.93758285063944"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3378.7,
            "unit": "ns",
            "range": "± 80.75346569790192"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 80062.125,
            "unit": "ns",
            "range": "± 4558.152709705983"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 122172.88888888889,
            "unit": "ns",
            "range": "± 11852.191721834031"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e65b179421c56eaf99e23939a9896377ce43967a",
          "message": "Merge pull request #121 from thomhurst/perf/thread-local-caching-optimizations\n\nperf: Add thread-local caching for fire-and-forget producer path",
          "timestamp": "2026-01-26T17:31:54Z",
          "tree_id": "f9ca14e6e86a2118dfca2238b67b982f53c35ab2",
          "url": "https://github.com/thomhurst/Dekaf/commit/e65b179421c56eaf99e23939a9896377ce43967a"
        },
        "date": 1769449376938,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 113.18931707143784,
            "unit": "ns",
            "range": "± 0.729803412724947"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 102.8435109257698,
            "unit": "ns",
            "range": "± 0.6306984345861112"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 129849.73413085938,
            "unit": "ns",
            "range": "± 2184.5169061607303"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 100.55129387378693,
            "unit": "ns",
            "range": "± 0.9902017949424375"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3016463387.8,
            "unit": "ns",
            "range": "± 452343.5071941456"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3174391181,
            "unit": "ns",
            "range": "± 421717.66622054146"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3013330401.4,
            "unit": "ns",
            "range": "± 338745.99705723464"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3179633752,
            "unit": "ns",
            "range": "± 283485.5112711524"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013998164.8,
            "unit": "ns",
            "range": "± 209119.72523939484"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172548045.4,
            "unit": "ns",
            "range": "± 413486.48576138495"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012816285.4,
            "unit": "ns",
            "range": "± 709440.7579849779"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177606663.25,
            "unit": "ns",
            "range": "± 663307.4250659217"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012777754.6,
            "unit": "ns",
            "range": "± 489147.3307484157"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3171724575.8,
            "unit": "ns",
            "range": "± 1176094.1793609473"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012931169.5,
            "unit": "ns",
            "range": "± 511557.5233737975"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3177499037,
            "unit": "ns",
            "range": "± 1050929.503605578"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013314825.5,
            "unit": "ns",
            "range": "± 579603.7337884393"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173872464.5,
            "unit": "ns",
            "range": "± 1313790.6274919405"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3012894823.5,
            "unit": "ns",
            "range": "± 546624.0700329005"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178631404.75,
            "unit": "ns",
            "range": "± 769170.3650234127"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8407006.6,
            "unit": "ns",
            "range": "± 530913.9516999488"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6119973.6,
            "unit": "ns",
            "range": "± 102893.49310730112"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8615869.444444444,
            "unit": "ns",
            "range": "± 357611.08939290146"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7574356,
            "unit": "ns",
            "range": "± 358560.1004864999"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 426121.2655273437,
            "unit": "ns",
            "range": "± 24739.154524041012"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 458025.63903808594,
            "unit": "ns",
            "range": "± 2774.073939854512"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 132078.1946533203,
            "unit": "ns",
            "range": "± 1315.3796987527776"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8603094.4,
            "unit": "ns",
            "range": "± 405319.9258192224"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5484849.055555556,
            "unit": "ns",
            "range": "± 31094.95848650996"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 11937143.222222222,
            "unit": "ns",
            "range": "± 1845627.6214936194"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8476792.722222222,
            "unit": "ns",
            "range": "± 298469.9338517105"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 4308710.217447917,
            "unit": "ns",
            "range": "± 31782.08694804347"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 4062589.92734375,
            "unit": "ns",
            "range": "± 40003.72371326728"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1340480.1609375,
            "unit": "ns",
            "range": "± 12373.86994465622"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8661963.5,
            "unit": "ns",
            "range": "± 538384.8875830685"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5591772.1,
            "unit": "ns",
            "range": "± 41719.807580386136"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9187860.7,
            "unit": "ns",
            "range": "± 621491.5679501389"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6711922.1,
            "unit": "ns",
            "range": "± 371281.81057346595"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 923600.2448730469,
            "unit": "ns",
            "range": "± 31005.7160949058"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 1127518.8577148437,
            "unit": "ns",
            "range": "± 379599.28599654377"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8783845.222222222,
            "unit": "ns",
            "range": "± 185151.51340306253"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5461051.666666667,
            "unit": "ns",
            "range": "± 48295.807908140436"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 29605718.1,
            "unit": "ns",
            "range": "± 1456969.8686138105"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12907818.888888888,
            "unit": "ns",
            "range": "± 1113657.7677592929"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 11059195.7640625,
            "unit": "ns",
            "range": "± 2176856.6238960125"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 11879270.89609375,
            "unit": "ns",
            "range": "± 2934306.4916732977"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20882,
            "unit": "ns",
            "range": "± 5461.464295406498"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15873.8,
            "unit": "ns",
            "range": "± 342.88321627049635"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14672.555555555555,
            "unit": "ns",
            "range": "± 149.0898312353253"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 39776.75,
            "unit": "ns",
            "range": "± 4084.5420708534057"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 23768.444444444445,
            "unit": "ns",
            "range": "± 443.17296598255825"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3862.5,
            "unit": "ns",
            "range": "± 111.6542281629615"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 65098.11111111111,
            "unit": "ns",
            "range": "± 11035.349081977929"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 85758,
            "unit": "ns",
            "range": "± 10185.699485062378"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 64409,
            "unit": "ns",
            "range": "± 676.0434527454578"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 100.55555555555556,
            "unit": "ns",
            "range": "± 52.60967380413775"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 448.1,
            "unit": "ns",
            "range": "± 178.4366990155208"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 313.1111111111111,
            "unit": "ns",
            "range": "± 33.13021447426972"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 289.9,
            "unit": "ns",
            "range": "± 89.584782934008"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1003.6666666666666,
            "unit": "ns",
            "range": "± 52.678268764263684"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 888.2,
            "unit": "ns",
            "range": "± 61.40403533608809"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1108.4,
            "unit": "ns",
            "range": "± 78.15006931112302"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 377,
            "unit": "ns",
            "range": "± 60.58969476000941"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 509.1,
            "unit": "ns",
            "range": "± 124.79534357410046"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 301.25,
            "unit": "ns",
            "range": "± 34.8209706929603"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 342.8,
            "unit": "ns",
            "range": "± 148.4825466735625"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 35.888888888888886,
            "unit": "ns",
            "range": "± 15.933019522711668"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 148.16666666666666,
            "unit": "ns",
            "range": "± 27.3221521846285"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2061.5,
            "unit": "ns",
            "range": "± 70.05508037047909"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2310.8333333333335,
            "unit": "ns",
            "range": "± 104.6780779342074"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1331.8,
            "unit": "ns",
            "range": "± 111.74479853666568"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 605.5555555555555,
            "unit": "ns",
            "range": "± 22.119624268458487"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 1774.3333333333333,
            "unit": "ns",
            "range": "± 210.9780794300678"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 2208.722222222222,
            "unit": "ns",
            "range": "± 124.2766850396503"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 60480.555555555555,
            "unit": "ns",
            "range": "± 4322.491443343501"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 82881.75,
            "unit": "ns",
            "range": "± 4720.295412970918"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 205.3,
            "unit": "ns",
            "range": "± 81.99058211499388"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 247.55555555555554,
            "unit": "ns",
            "range": "± 10.21164912136026"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 295.9,
            "unit": "ns",
            "range": "± 47.20275415693453"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 171.4,
            "unit": "ns",
            "range": "± 63.92217490389736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 892.375,
            "unit": "ns",
            "range": "± 27.089468169646402"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1200.7,
            "unit": "ns",
            "range": "± 200.29578128358068"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 984,
            "unit": "ns",
            "range": "± 93.53208005812765"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 544,
            "unit": "ns",
            "range": "± 137.94604420247472"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 674.5555555555555,
            "unit": "ns",
            "range": "± 153.19441170544627"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 380.8888888888889,
            "unit": "ns",
            "range": "± 39.520388549596916"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 224.5,
            "unit": "ns",
            "range": "± 11.856282240712245"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 159.33333333333334,
            "unit": "ns",
            "range": "± 7.745966692414834"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 211.77777777777777,
            "unit": "ns",
            "range": "± 51.28054645227998"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2005.6,
            "unit": "ns",
            "range": "± 196.20465505860625"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 1839.7777777777778,
            "unit": "ns",
            "range": "± 179.57851888364723"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 780.6111111111111,
            "unit": "ns",
            "range": "± 138.69520940216756"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 355.75,
            "unit": "ns",
            "range": "± 57.967355345179286"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2231.9,
            "unit": "ns",
            "range": "± 596.3051884545176"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 2622.5,
            "unit": "ns",
            "range": "± 188.50530496513883"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 61916.555555555555,
            "unit": "ns",
            "range": "± 6487.9129947755755"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 85932.33333333333,
            "unit": "ns",
            "range": "± 9387.638534264088"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 224.44444444444446,
            "unit": "ns",
            "range": "± 29.202359113225388"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 126.22222222222223,
            "unit": "ns",
            "range": "± 107.81787627496864"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 146.25,
            "unit": "ns",
            "range": "± 31.16660300477328"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 188.6,
            "unit": "ns",
            "range": "± 75.85541802959393"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1065.8,
            "unit": "ns",
            "range": "± 159.5262430518008"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 797.4444444444445,
            "unit": "ns",
            "range": "± 149.92840884161274"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1117.5555555555557,
            "unit": "ns",
            "range": "± 99.63823451756748"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 440.4,
            "unit": "ns",
            "range": "± 64.8098929348153"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 248.55555555555554,
            "unit": "ns",
            "range": "± 55.28135108495248"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 345.44444444444446,
            "unit": "ns",
            "range": "± 29.627314724385293"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 136.3,
            "unit": "ns",
            "range": "± 157.0088815605311"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 60.27777777777778,
            "unit": "ns",
            "range": "± 10.009717500731199"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 182.7,
            "unit": "ns",
            "range": "± 52.3175135324895"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2008.3333333333333,
            "unit": "ns",
            "range": "± 152.87331356387878"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 2795.722222222222,
            "unit": "ns",
            "range": "± 264.76299674320893"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1353.2,
            "unit": "ns",
            "range": "± 138.40664723921319"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 391,
            "unit": "ns",
            "range": "± 49.28053803045811"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2442.9,
            "unit": "ns",
            "range": "± 129.51657980522975"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 2322.777777777778,
            "unit": "ns",
            "range": "± 86.92493568846885"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 61097.875,
            "unit": "ns",
            "range": "± 4818.321415997188"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 82636.33333333333,
            "unit": "ns",
            "range": "± 3768.6758284575235"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7c37edd52cacb1ca47734a0cf184c4327ae12b42",
          "message": "Merge pull request #122 from thomhurst/perf/lock-free-single-producer-optimization\n\nperf: Use volatile read instead of SpinLock in ShouldFlush",
          "timestamp": "2026-01-26T23:11:18Z",
          "tree_id": "28731d62268e29cc9fb14dbd4aff2a98eb119fef",
          "url": "https://github.com/thomhurst/Dekaf/commit/7c37edd52cacb1ca47734a0cf184c4327ae12b42"
        },
        "date": 1769469716657,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 140.73110824161105,
            "unit": "ns",
            "range": "± 0.5952710031420277"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 130.86873409748077,
            "unit": "ns",
            "range": "± 1.226423773502446"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 154645.82053222656,
            "unit": "ns",
            "range": "± 901.9870682485542"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 131.1951268672943,
            "unit": "ns",
            "range": "± 1.7262890392220298"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017924371.2,
            "unit": "ns",
            "range": "± 1111788.7118374608"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176664411.2,
            "unit": "ns",
            "range": "± 966677.558403111"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014897240.5,
            "unit": "ns",
            "range": "± 947744.3735927249"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3181736469.6,
            "unit": "ns",
            "range": "± 504484.5707063795"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014972889.8,
            "unit": "ns",
            "range": "± 561885.0129832615"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3174458010,
            "unit": "ns",
            "range": "± 671736.6546638645"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014063405.6,
            "unit": "ns",
            "range": "± 934043.0927292916"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3180260803.5,
            "unit": "ns",
            "range": "± 355273.8932002557"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014288055.4,
            "unit": "ns",
            "range": "± 880798.8064781309"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3175263150.25,
            "unit": "ns",
            "range": "± 373430.8337576273"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013485406.3,
            "unit": "ns",
            "range": "± 516003.5643590071"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3180591144,
            "unit": "ns",
            "range": "± 631164.0877628416"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015056123.5,
            "unit": "ns",
            "range": "± 460625.676609001"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174774502.5,
            "unit": "ns",
            "range": "± 791757.1656429766"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013946445.9,
            "unit": "ns",
            "range": "± 811846.621427533"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179427571.6,
            "unit": "ns",
            "range": "± 440061.27182620834"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8605706.888888888,
            "unit": "ns",
            "range": "± 582065.7291982677"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5941095,
            "unit": "ns",
            "range": "± 60035.7596270756"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8876465.888888888,
            "unit": "ns",
            "range": "± 482071.7336658634"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7377556.3,
            "unit": "ns",
            "range": "± 257879.59260392567"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 133338.89025878906,
            "unit": "ns",
            "range": "± 2352.437387908533"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 124233.87646484375,
            "unit": "ns",
            "range": "± 4982.875298824184"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 133457.36865234375,
            "unit": "ns",
            "range": "± 4267.992113720869"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8771713.25,
            "unit": "ns",
            "range": "± 150548.71010193726"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5412228.666666667,
            "unit": "ns",
            "range": "± 18538.909123786114"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12743873.2,
            "unit": "ns",
            "range": "± 1489530.2397086322"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8560194.944444444,
            "unit": "ns",
            "range": "± 379567.3853507803"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 1271656.667578125,
            "unit": "ns",
            "range": "± 17078.59518235774"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 1252288.673046875,
            "unit": "ns",
            "range": "± 21491.693048811354"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1338924.806640625,
            "unit": "ns",
            "range": "± 7303.917724412042"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8588051.888888888,
            "unit": "ns",
            "range": "± 322476.10324931226"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5423992.6,
            "unit": "ns",
            "range": "± 28378.0922356196"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 9341927.8,
            "unit": "ns",
            "range": "± 738154.1584720031"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6728061.9,
            "unit": "ns",
            "range": "± 307265.25027958787"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 278415.7613389757,
            "unit": "ns",
            "range": "± 5415.150954604493"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 261058.4033203125,
            "unit": "ns",
            "range": "± 2939.228388537802"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8736833.625,
            "unit": "ns",
            "range": "± 240833.37733553315"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5416775.7,
            "unit": "ns",
            "range": "± 27260.614764609483"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 30015543.1,
            "unit": "ns",
            "range": "± 2549534.620197585"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11733271.555555556,
            "unit": "ns",
            "range": "± 318841.3720971257"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 2780454.945746528,
            "unit": "ns",
            "range": "± 75744.53129425742"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 2656254.134765625,
            "unit": "ns",
            "range": "± 25984.43804713701"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20604,
            "unit": "ns",
            "range": "± 5344.247538241469"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 12192,
            "unit": "ns",
            "range": "± 103.79788051786029"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14483.1,
            "unit": "ns",
            "range": "± 122.85963083490398"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 39552.833333333336,
            "unit": "ns",
            "range": "± 13002.509613532304"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 23100.2,
            "unit": "ns",
            "range": "± 1470.1179128824251"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3656.4444444444443,
            "unit": "ns",
            "range": "± 352.7797156552199"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 66108.27777777778,
            "unit": "ns",
            "range": "± 14573.508592457909"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 112161,
            "unit": "ns",
            "range": "± 6592.982741413132"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 61911.333333333336,
            "unit": "ns",
            "range": "± 3471.0970167945466"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 483.3333333333333,
            "unit": "ns",
            "range": "± 47.5998949578673"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 313.6666666666667,
            "unit": "ns",
            "range": "± 71.43178564196754"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 582.1,
            "unit": "ns",
            "range": "± 80.30663456748486"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 343.3888888888889,
            "unit": "ns",
            "range": "± 56.234874509605795"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1353.75,
            "unit": "ns",
            "range": "± 238.35672545877233"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1007.5,
            "unit": "ns",
            "range": "± 195.1211162329695"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1495.8333333333333,
            "unit": "ns",
            "range": "± 179.8228294739019"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 710,
            "unit": "ns",
            "range": "± 75.47920979389697"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 533.1666666666666,
            "unit": "ns",
            "range": "± 139.06203651608155"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 636.625,
            "unit": "ns",
            "range": "± 45.841146520192034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 284.125,
            "unit": "ns",
            "range": "± 34.221703639649505"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 21.6875,
            "unit": "ns",
            "range": "± 27.750080437463858"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 271.5,
            "unit": "ns",
            "range": "± 25.68490384864786"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2630.6111111111113,
            "unit": "ns",
            "range": "± 137.21647536324167"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2586.9444444444443,
            "unit": "ns",
            "range": "± 215.19183947765717"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 2016.611111111111,
            "unit": "ns",
            "range": "± 115.11129879864578"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 815.1111111111111,
            "unit": "ns",
            "range": "± 58.69080942627313"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2579.6666666666665,
            "unit": "ns",
            "range": "± 168.909739210029"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 2636.375,
            "unit": "ns",
            "range": "± 88.80868281230808"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 72336.16666666667,
            "unit": "ns",
            "range": "± 7967.337415975302"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 94169.66666666667,
            "unit": "ns",
            "range": "± 10978.981111651483"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 383.4,
            "unit": "ns",
            "range": "± 119.35214004505043"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 201.77777777777777,
            "unit": "ns",
            "range": "± 52.87196274439265"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 406.8,
            "unit": "ns",
            "range": "± 104.62849622460519"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 324.44444444444446,
            "unit": "ns",
            "range": "± 82.35913900580663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1120.4444444444443,
            "unit": "ns",
            "range": "± 180.53677680123178"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 998.5,
            "unit": "ns",
            "range": "± 79.26064597263891"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1280.388888888889,
            "unit": "ns",
            "range": "± 140.25819445262766"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 607.2222222222222,
            "unit": "ns",
            "range": "± 72.19206635388991"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 504.2,
            "unit": "ns",
            "range": "± 160.90027967657485"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 584.4444444444445,
            "unit": "ns",
            "range": "± 84.93837635473012"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 316.1666666666667,
            "unit": "ns",
            "range": "± 30.066592756745816"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 71.88888888888889,
            "unit": "ns",
            "range": "± 58.85245203992022"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 398.6,
            "unit": "ns",
            "range": "± 94.78800908694447"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2017.375,
            "unit": "ns",
            "range": "± 179.68340212718593"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2333.1666666666665,
            "unit": "ns",
            "range": "± 168.83201710576108"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1386.875,
            "unit": "ns",
            "range": "± 67.33909393085544"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 799.1111111111111,
            "unit": "ns",
            "range": "± 91.44321249338908"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2539.222222222222,
            "unit": "ns",
            "range": "± 167.32661009069787"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 2820.222222222222,
            "unit": "ns",
            "range": "± 332.4668320967438"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 65973.25,
            "unit": "ns",
            "range": "± 6472.2691041969865"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 96470.61111111111,
            "unit": "ns",
            "range": "± 8051.684085401706"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 262.8,
            "unit": "ns",
            "range": "± 151.5292784323288"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 331.1111111111111,
            "unit": "ns",
            "range": "± 77.01533036422755"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 544,
            "unit": "ns",
            "range": "± 49.969990994595946"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 202.27777777777777,
            "unit": "ns",
            "range": "± 60.915469664482146"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1581.7,
            "unit": "ns",
            "range": "± 298.49243429831404"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1221.5,
            "unit": "ns",
            "range": "± 522.5184738892546"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1481.611111111111,
            "unit": "ns",
            "range": "± 144.57648879092034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 551.4444444444445,
            "unit": "ns",
            "range": "± 125.08608147103249"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 646.5555555555555,
            "unit": "ns",
            "range": "± 34.099527530125364"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 484.94444444444446,
            "unit": "ns",
            "range": "± 191.4955032834395"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 299.1,
            "unit": "ns",
            "range": "± 80.75779976090372"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 134.875,
            "unit": "ns",
            "range": "± 55.46024960018018"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 302.22222222222223,
            "unit": "ns",
            "range": "± 48.59212327573724"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2514.4444444444443,
            "unit": "ns",
            "range": "± 170.83845520777157"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3031.722222222222,
            "unit": "ns",
            "range": "± 320.22522455990946"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1345.375,
            "unit": "ns",
            "range": "± 94.14189518259886"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 333.375,
            "unit": "ns",
            "range": "± 71.21584394340509"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2756.0555555555557,
            "unit": "ns",
            "range": "± 228.7428201666181"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3476.5555555555557,
            "unit": "ns",
            "range": "± 295.47128080031365"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 65513.27777777778,
            "unit": "ns",
            "range": "± 6049.9665862254515"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 205710.72222222222,
            "unit": "ns",
            "range": "± 36779.05602981736"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e0d83250c38a9eea41859a6f12197587cb6a63cc",
          "message": "Merge pull request #123 from thomhurst/perf/reduce-hot-path-allocations\n\nperf: Reduce allocations in producer hot path",
          "timestamp": "2026-01-27T00:18:49Z",
          "tree_id": "49e939a4e597848849b16b32a8febc516a53fd4e",
          "url": "https://github.com/thomhurst/Dekaf/commit/e0d83250c38a9eea41859a6f12197587cb6a63cc"
        },
        "date": 1769473766248,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 128.61306455731392,
            "unit": "ns",
            "range": "± 0.6118773838883282"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 124.14316737651825,
            "unit": "ns",
            "range": "± 0.49462199384977"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 141041.36857910157,
            "unit": "ns",
            "range": "± 208.15730710488262"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 122.58902421593666,
            "unit": "ns",
            "range": "± 0.1699894716021376"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017424340.75,
            "unit": "ns",
            "range": "± 674241.5027458016"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176986987.1,
            "unit": "ns",
            "range": "± 1118342.3782363343"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014967491.25,
            "unit": "ns",
            "range": "± 155338.30315663724"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180414810.4,
            "unit": "ns",
            "range": "± 1046886.1877753475"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014803908.75,
            "unit": "ns",
            "range": "± 225238.59120256605"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3173099813.4,
            "unit": "ns",
            "range": "± 600390.5667894857"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013455659.75,
            "unit": "ns",
            "range": "± 149144.75413587296"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3178490811.4,
            "unit": "ns",
            "range": "± 534948.9264498061"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014123174.8,
            "unit": "ns",
            "range": "± 414957.7733103695"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173932138.75,
            "unit": "ns",
            "range": "± 406240.9024461348"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012840503.5,
            "unit": "ns",
            "range": "± 696743.2593954534"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3178669653,
            "unit": "ns",
            "range": "± 573852.7323142237"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014627615,
            "unit": "ns",
            "range": "± 302077.8619263583"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174228653.7,
            "unit": "ns",
            "range": "± 553799.2197003351"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013649274,
            "unit": "ns",
            "range": "± 661027.9152369436"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178171716.2,
            "unit": "ns",
            "range": "± 536470.6130681344"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8540625.1,
            "unit": "ns",
            "range": "± 492522.86761066574"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5904016.777777778,
            "unit": "ns",
            "range": "± 79135.62767138734"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8847234.888888888,
            "unit": "ns",
            "range": "± 907378.2575700505"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7199036.9,
            "unit": "ns",
            "range": "± 117189.62542767078"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 191308.7689941406,
            "unit": "ns",
            "range": "± 7489.793823660338"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 171184.65615234376,
            "unit": "ns",
            "range": "± 3103.8964890659677"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 127688.78942871094,
            "unit": "ns",
            "range": "± 1446.4205039065614"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8701024.3,
            "unit": "ns",
            "range": "± 256100.3849235903"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5408670.1,
            "unit": "ns",
            "range": "± 23524.536062021143"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12622932.555555556,
            "unit": "ns",
            "range": "± 408417.4653737004"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8565130.722222222,
            "unit": "ns",
            "range": "± 454362.3182276392"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 1874304.665625,
            "unit": "ns",
            "range": "± 36392.40532465977"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 1750523.215625,
            "unit": "ns",
            "range": "± 12136.492558321252"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1294140.4298828125,
            "unit": "ns",
            "range": "± 10128.644732145365"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8875877.7,
            "unit": "ns",
            "range": "± 362313.64501279214"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5410040.6,
            "unit": "ns",
            "range": "± 19895.217289310738"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8736741.9,
            "unit": "ns",
            "range": "± 442221.04966538574"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6830866.6,
            "unit": "ns",
            "range": "± 89085.49512743363"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 367836.63850911456,
            "unit": "ns",
            "range": "± 5216.602724840804"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 345559.57490234374,
            "unit": "ns",
            "range": "± 4681.834948213751"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8818328,
            "unit": "ns",
            "range": "± 444056.1304270366"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5409035.9,
            "unit": "ns",
            "range": "± 27196.914392792594"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 26944322.875,
            "unit": "ns",
            "range": "± 764673.8068152118"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11410642.666666666,
            "unit": "ns",
            "range": "± 401984.3377197674"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 3812080.91796875,
            "unit": "ns",
            "range": "± 97910.49504476103"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 3619042.5265625,
            "unit": "ns",
            "range": "± 29371.95063752457"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20591.222222222223,
            "unit": "ns",
            "range": "± 5423.153759063488"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 14954.625,
            "unit": "ns",
            "range": "± 1091.1753738187879"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14731.444444444445,
            "unit": "ns",
            "range": "± 92.21050795748704"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 31103.11111111111,
            "unit": "ns",
            "range": "± 5927.00770719181"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 22545.5,
            "unit": "ns",
            "range": "± 1610.2065528549879"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3355.875,
            "unit": "ns",
            "range": "± 55.20206388481193"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 68070.66666666667,
            "unit": "ns",
            "range": "± 11645.181310310287"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 89596.88888888889,
            "unit": "ns",
            "range": "± 12279.095726522824"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 55162.625,
            "unit": "ns",
            "range": "± 604.6612835771303"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 440,
            "unit": "ns",
            "range": "± 68.11958439228601"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 275.2,
            "unit": "ns",
            "range": "± 43.47617482918406"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 477.8888888888889,
            "unit": "ns",
            "range": "± 31.801904205740747"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 383.55555555555554,
            "unit": "ns",
            "range": "± 23.68602494674397"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1393.5,
            "unit": "ns",
            "range": "± 29.140055887778548"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1054.7222222222222,
            "unit": "ns",
            "range": "± 34.050615918723764"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1567.125,
            "unit": "ns",
            "range": "± 74.47614670867061"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 667.3333333333334,
            "unit": "ns",
            "range": "± 78.00801240898271"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 581.9,
            "unit": "ns",
            "range": "± 43.07732066361087"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 705,
            "unit": "ns",
            "range": "± 66.5749368923638"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 252.9,
            "unit": "ns",
            "range": "± 20.21248239193901"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 90.5,
            "unit": "ns",
            "range": "± 28.01289385661935"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 326.7,
            "unit": "ns",
            "range": "± 44.606427040655625"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2768.7,
            "unit": "ns",
            "range": "± 525.9104380870273"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3222.9,
            "unit": "ns",
            "range": "± 279.8628433913854"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1882.9,
            "unit": "ns",
            "range": "± 188.4034972546364"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 709.5555555555555,
            "unit": "ns",
            "range": "± 28.20510198133979"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 3158.3,
            "unit": "ns",
            "range": "± 506.4690513743164"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3116.625,
            "unit": "ns",
            "range": "± 40.50022045795377"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 96940.66666666667,
            "unit": "ns",
            "range": "± 9962.251163768158"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 127792.33333333333,
            "unit": "ns",
            "range": "± 10165.677067957648"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 495.5,
            "unit": "ns",
            "range": "± 65.1685847970051"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 413.6666666666667,
            "unit": "ns",
            "range": "± 13.047988350699887"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 349,
            "unit": "ns",
            "range": "± 43.32051092342595"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 405.4,
            "unit": "ns",
            "range": "± 69.25989700637255"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1456.7777777777778,
            "unit": "ns",
            "range": "± 44.010731014656464"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1510.9,
            "unit": "ns",
            "range": "± 184.69145441339006"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1727.111111111111,
            "unit": "ns",
            "range": "± 239.25271390542494"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 618.9,
            "unit": "ns",
            "range": "± 117.53245414683461"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 587.2777777777778,
            "unit": "ns",
            "range": "± 22.24172755080964"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 773.8,
            "unit": "ns",
            "range": "± 252.2361327539468"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 271.22222222222223,
            "unit": "ns",
            "range": "± 29.469381473733794"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 147.3,
            "unit": "ns",
            "range": "± 39.41178616719735"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 375.77777777777777,
            "unit": "ns",
            "range": "± 47.88208897327313"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2987.4,
            "unit": "ns",
            "range": "± 434.0847843451783"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3054.5,
            "unit": "ns",
            "range": "± 50.18964036531842"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1554.9,
            "unit": "ns",
            "range": "± 78.23497370812565"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 745.2222222222222,
            "unit": "ns",
            "range": "± 65.08413358449542"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2821.125,
            "unit": "ns",
            "range": "± 112.42195705720226"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3952.5555555555557,
            "unit": "ns",
            "range": "± 173.0369260527295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 85258,
            "unit": "ns",
            "range": "± 7315.4958820301445"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 117404.55555555556,
            "unit": "ns",
            "range": "± 18775.59738804009"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 450.8,
            "unit": "ns",
            "range": "± 21.607868937033103"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 451.44444444444446,
            "unit": "ns",
            "range": "± 19.249098102970375"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 376.8,
            "unit": "ns",
            "range": "± 18.457458594773488"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 348,
            "unit": "ns",
            "range": "± 13.260216187277392"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1453.3,
            "unit": "ns",
            "range": "± 105.5294061177052"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1263.9,
            "unit": "ns",
            "range": "± 99.82924309929321"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1474.4444444444443,
            "unit": "ns",
            "range": "± 37.95428536776549"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 550.6,
            "unit": "ns",
            "range": "± 26.83778430993637"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 560.1,
            "unit": "ns",
            "range": "± 24.02406201198198"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 628.4444444444445,
            "unit": "ns",
            "range": "± 28.213964233651705"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 253.11111111111111,
            "unit": "ns",
            "range": "± 45.11220578857912"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 157.3,
            "unit": "ns",
            "range": "± 11.399805066559496"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 316.1,
            "unit": "ns",
            "range": "± 11.929887770730376"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2627.3,
            "unit": "ns",
            "range": "± 102.49666661246437"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3289.222222222222,
            "unit": "ns",
            "range": "± 85.84546839783941"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1580.3,
            "unit": "ns",
            "range": "± 97.50789141853538"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 794,
            "unit": "ns",
            "range": "± 30.987452657845598"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2896.25,
            "unit": "ns",
            "range": "± 42.27376761741765"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3343.3888888888887,
            "unit": "ns",
            "range": "± 53.28096387182867"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 87307.33333333333,
            "unit": "ns",
            "range": "± 9037.866396445568"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 116968,
            "unit": "ns",
            "range": "± 20024.248837347182"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d1546290d622c338f11581ef31ee6febd1a634d4",
          "message": "Merge pull request #124 from thomhurst/perf/reduce-allocation-overhead\n\nperf: Reduce allocation overhead in producer hot path",
          "timestamp": "2026-01-27T01:57:17Z",
          "tree_id": "7f17cd8a4e870c30b5d65828c8c6185d2ce73969",
          "url": "https://github.com/thomhurst/Dekaf/commit/d1546290d622c338f11581ef31ee6febd1a634d4"
        },
        "date": 1769479665131,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 128.4277821779251,
            "unit": "ns",
            "range": "± 0.12849752205092518"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 123.20523111025493,
            "unit": "ns",
            "range": "± 0.29955009852998815"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 139543.53823242188,
            "unit": "ns",
            "range": "± 794.1814214773827"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 123.55762804879083,
            "unit": "ns",
            "range": "± 0.15029038518306537"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017125759.5,
            "unit": "ns",
            "range": "± 1574182.122597954"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3175194651.2,
            "unit": "ns",
            "range": "± 1288275.5895594312"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014850836.6,
            "unit": "ns",
            "range": "± 576563.7296616047"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3179828653.3,
            "unit": "ns",
            "range": "± 691544.75589885"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013987233.8,
            "unit": "ns",
            "range": "± 435391.36865893425"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172623016.7,
            "unit": "ns",
            "range": "± 588296.1722726913"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013613017.75,
            "unit": "ns",
            "range": "± 286605.87949118676"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3179072846.2,
            "unit": "ns",
            "range": "± 1120853.5172404107"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014383102.9,
            "unit": "ns",
            "range": "± 575201.5185800538"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3174303535.2,
            "unit": "ns",
            "range": "± 343450.6402275296"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012978477.6,
            "unit": "ns",
            "range": "± 710029.5591729264"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179868739.8,
            "unit": "ns",
            "range": "± 287456.2024947453"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014538983.6,
            "unit": "ns",
            "range": "± 451491.48330926464"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173639971.6,
            "unit": "ns",
            "range": "± 1009047.8823754104"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013420447.5,
            "unit": "ns",
            "range": "± 361798.078318556"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177616625.6,
            "unit": "ns",
            "range": "± 943682.7592386649"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8230407.6,
            "unit": "ns",
            "range": "± 788908.6035702813"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5964751.9,
            "unit": "ns",
            "range": "± 73122.67738298123"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 9916215.6,
            "unit": "ns",
            "range": "± 1698345.0809114934"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7235190,
            "unit": "ns",
            "range": "± 189038.80675612026"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 202273.33793945314,
            "unit": "ns",
            "range": "± 6375.128143636822"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 178203.13193359374,
            "unit": "ns",
            "range": "± 1350.0827888589677"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 139331.52797851563,
            "unit": "ns",
            "range": "± 1198.3574237827438"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8630567.5,
            "unit": "ns",
            "range": "± 423515.25213129376"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5419538.3,
            "unit": "ns",
            "range": "± 23144.450008107295"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12534009.7,
            "unit": "ns",
            "range": "± 688632.7927496466"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8646147.75,
            "unit": "ns",
            "range": "± 360848.0697902611"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 1925611.646701389,
            "unit": "ns",
            "range": "± 39433.138594728145"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 1791614.968359375,
            "unit": "ns",
            "range": "± 13656.96662953247"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1351319.8017578125,
            "unit": "ns",
            "range": "± 11186.240321516741"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8397910.4,
            "unit": "ns",
            "range": "± 724239.824287385"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5412359.125,
            "unit": "ns",
            "range": "± 11434.336153352198"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8417909.8,
            "unit": "ns",
            "range": "± 539608.7986284961"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6713723.7,
            "unit": "ns",
            "range": "± 286190.09564625245"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 387611.1641601563,
            "unit": "ns",
            "range": "± 6266.744998020096"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 366706.2839626736,
            "unit": "ns",
            "range": "± 3262.842217039764"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8626306,
            "unit": "ns",
            "range": "± 373142.3550166731"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5408490.5,
            "unit": "ns",
            "range": "± 16452.281397018065"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 28695088.222222224,
            "unit": "ns",
            "range": "± 717261.158144608"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11848652.555555556,
            "unit": "ns",
            "range": "± 399091.62501457956"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 3839241.8921875,
            "unit": "ns",
            "range": "± 41719.05721785986"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 3707673.64765625,
            "unit": "ns",
            "range": "± 57240.16401519729"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 22322.61111111111,
            "unit": "ns",
            "range": "± 4671.358165577877"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 11373.833333333334,
            "unit": "ns",
            "range": "± 212.78040323300453"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15024.75,
            "unit": "ns",
            "range": "± 255.72739391782022"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 36889.166666666664,
            "unit": "ns",
            "range": "± 6348.258954233042"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 22134,
            "unit": "ns",
            "range": "± 328.6891714501277"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3045,
            "unit": "ns",
            "range": "± 192.63177308014377"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 62586.88888888889,
            "unit": "ns",
            "range": "± 12560.805870289976"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 117130,
            "unit": "ns",
            "range": "± 16442.047127714966"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 43399.625,
            "unit": "ns",
            "range": "± 717.9649688031941"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 365.25,
            "unit": "ns",
            "range": "± 25.245933194420502"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 333.8,
            "unit": "ns",
            "range": "± 42.15592748620557"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 398.5,
            "unit": "ns",
            "range": "± 19.086270308410555"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 394.72222222222223,
            "unit": "ns",
            "range": "± 9.202958461519016"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1368,
            "unit": "ns",
            "range": "± 46.23610902506587"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1183.3,
            "unit": "ns",
            "range": "± 39.92437295131328"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 2126,
            "unit": "ns",
            "range": "± 176.6297131163258"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 686.4444444444445,
            "unit": "ns",
            "range": "± 98.20528385875058"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 607,
            "unit": "ns",
            "range": "± 36.05936463358413"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 609.3333333333334,
            "unit": "ns",
            "range": "± 26.22022120425379"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 289.8888888888889,
            "unit": "ns",
            "range": "± 27.58824226207808"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 87,
            "unit": "ns",
            "range": "± 30.258148581093913"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 314.3,
            "unit": "ns",
            "range": "± 22.39568410802998"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 3180.4,
            "unit": "ns",
            "range": "± 212.3917763630849"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2940.5,
            "unit": "ns",
            "range": "± 53.969899017878475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1634.6,
            "unit": "ns",
            "range": "± 304.3861582485862"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 695.3333333333334,
            "unit": "ns",
            "range": "± 41.83300132670378"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2876.1,
            "unit": "ns",
            "range": "± 62.0849776068611"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3255.375,
            "unit": "ns",
            "range": "± 61.46064362082221"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 86017.22222222222,
            "unit": "ns",
            "range": "± 8065.57040415893"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 118853.94444444444,
            "unit": "ns",
            "range": "± 11066.533582282113"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 458.3333333333333,
            "unit": "ns",
            "range": "± 13.047988350699889"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 380.3,
            "unit": "ns",
            "range": "± 26.1153424467517"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 395.77777777777777,
            "unit": "ns",
            "range": "± 34.310267332745234"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 375.44444444444446,
            "unit": "ns",
            "range": "± 12.360330811826106"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1472.3333333333333,
            "unit": "ns",
            "range": "± 45.417507637473896"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1186.611111111111,
            "unit": "ns",
            "range": "± 50.26291984267439"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1516.6666666666667,
            "unit": "ns",
            "range": "± 48.89529629729224"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 569.6,
            "unit": "ns",
            "range": "± 41.50823211524833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 612.8888888888889,
            "unit": "ns",
            "range": "± 27.82734466511512"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 551.5,
            "unit": "ns",
            "range": "± 23.670187531529567"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 284.44444444444446,
            "unit": "ns",
            "range": "± 26.034165586355517"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 118.55555555555556,
            "unit": "ns",
            "range": "± 19.262081345944363"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 285.6,
            "unit": "ns",
            "range": "± 55.197524099063244"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2606.7,
            "unit": "ns",
            "range": "± 37.16943188517504"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3010.5555555555557,
            "unit": "ns",
            "range": "± 52.20658749408715"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1578.6,
            "unit": "ns",
            "range": "± 100.48239204512954"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 739.4,
            "unit": "ns",
            "range": "± 40.82061026273642"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2795.777777777778,
            "unit": "ns",
            "range": "± 63.97417013486337"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3260.6,
            "unit": "ns",
            "range": "± 37.52687925563045"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 78705.25,
            "unit": "ns",
            "range": "± 5851.247968229879"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 122798,
            "unit": "ns",
            "range": "± 6328.940551150722"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 487,
            "unit": "ns",
            "range": "± 18.446619684315547"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 418.25,
            "unit": "ns",
            "range": "± 15.772942473923953"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 427.2,
            "unit": "ns",
            "range": "± 30.006665926090488"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 373.4,
            "unit": "ns",
            "range": "± 34.235134649135595"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1535,
            "unit": "ns",
            "range": "± 30.37268509697488"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1398.388888888889,
            "unit": "ns",
            "range": "± 27.227947243799175"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 2178.3333333333335,
            "unit": "ns",
            "range": "± 180.3094562134776"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 597.1,
            "unit": "ns",
            "range": "± 37.387460761894786"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 620.4,
            "unit": "ns",
            "range": "± 55.53817505904285"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 622.2,
            "unit": "ns",
            "range": "± 42.13483647107752"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 303.1,
            "unit": "ns",
            "range": "± 16.421192269611716"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 144.6,
            "unit": "ns",
            "range": "± 24.61345612103717"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 316.1111111111111,
            "unit": "ns",
            "range": "± 36.55285366576885"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2771.5,
            "unit": "ns",
            "range": "± 85.86973338208935"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3142,
            "unit": "ns",
            "range": "± 187.91021792334763"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1751.9,
            "unit": "ns",
            "range": "± 44.518285880947595"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 808.4,
            "unit": "ns",
            "range": "± 35.031573060756365"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2927.9,
            "unit": "ns",
            "range": "± 62.60715259102234"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3284.6666666666665,
            "unit": "ns",
            "range": "± 46.26823964665178"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 83370.875,
            "unit": "ns",
            "range": "± 3792.8255369277704"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 98511.66666666667,
            "unit": "ns",
            "range": "± 6079.173689737776"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "01cc46e3c28a6c81920d4141b1ea5d95cfec38c5",
          "message": "Merge pull request #126 from thomhurst/perf/arena-based-serialization\n\nperf: Add arena-based serialization for fire-and-forget path",
          "timestamp": "2026-01-27T11:56:48Z",
          "tree_id": "9f50e399e8dac8dfe92c08af986f3a8eeb60e036",
          "url": "https://github.com/thomhurst/Dekaf/commit/01cc46e3c28a6c81920d4141b1ea5d95cfec38c5"
        },
        "date": 1769515649271,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 134.4589642763138,
            "unit": "ns",
            "range": "± 0.7381449462647588"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 134.3479336977005,
            "unit": "ns",
            "range": "± 1.0124790680454783"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 143783.46576605903,
            "unit": "ns",
            "range": "± 877.8303570724665"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 123.99030906624265,
            "unit": "ns",
            "range": "± 0.29388049267147875"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3021119408.4,
            "unit": "ns",
            "range": "± 659700.6810268578"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3177294131.5,
            "unit": "ns",
            "range": "± 524869.9630432285"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3016646504,
            "unit": "ns",
            "range": "± 1304684.6619246737"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3184622049.75,
            "unit": "ns",
            "range": "± 943102.782109626"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3016559096.6,
            "unit": "ns",
            "range": "± 987221.2467447709"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3174128695.8,
            "unit": "ns",
            "range": "± 502166.1343725799"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013200382.75,
            "unit": "ns",
            "range": "± 218099.68470613766"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3178651629,
            "unit": "ns",
            "range": "± 383095.49142826867"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013674735.75,
            "unit": "ns",
            "range": "± 33587.866672108445"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3177488754.6,
            "unit": "ns",
            "range": "± 2021322.926471869"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014775491.8,
            "unit": "ns",
            "range": "± 476720.54443321406"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3184220534.2,
            "unit": "ns",
            "range": "± 2696286.3219583523"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015444311.7,
            "unit": "ns",
            "range": "± 464415.5210748237"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3176692738.8,
            "unit": "ns",
            "range": "± 1046660.4828602253"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013723882.8,
            "unit": "ns",
            "range": "± 690191.596763319"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178682662.4,
            "unit": "ns",
            "range": "± 680756.6256514145"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8457510.6,
            "unit": "ns",
            "range": "± 472424.286816699"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5992917.4,
            "unit": "ns",
            "range": "± 80461.75874731493"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 9850057.5,
            "unit": "ns",
            "range": "± 1836734.8360684481"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7206728.4,
            "unit": "ns",
            "range": "± 180036.11258513297"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 62102.38065592448,
            "unit": "ns",
            "range": "± 1101.7460745704366"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 57881.07620239258,
            "unit": "ns",
            "range": "± 625.745815756745"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 130611.18469238281,
            "unit": "ns",
            "range": "± 1323.1789170147608"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8527531.7,
            "unit": "ns",
            "range": "± 462595.14666451776"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5408544.8,
            "unit": "ns",
            "range": "± 11676.966444434292"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12832110.4,
            "unit": "ns",
            "range": "± 572045.7957337954"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8460764.166666666,
            "unit": "ns",
            "range": "± 406250.29844389035"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 615828.5794921875,
            "unit": "ns",
            "range": "± 4382.880765338037"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 570872.31015625,
            "unit": "ns",
            "range": "± 8094.936270028066"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1303989.3757595485,
            "unit": "ns",
            "range": "± 5638.980051531201"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8522907.3,
            "unit": "ns",
            "range": "± 446232.28709920676"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5430651.4,
            "unit": "ns",
            "range": "± 24840.74333048653"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8445882.5,
            "unit": "ns",
            "range": "± 490092.65529291047"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6812395.2,
            "unit": "ns",
            "range": "± 94631.03350005689"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 288519.769140625,
            "unit": "ns",
            "range": "± 15235.476649905033"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 276254.1946777344,
            "unit": "ns",
            "range": "± 4140.457778537171"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8534283.5,
            "unit": "ns",
            "range": "± 459009.56847258523"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5416063.3,
            "unit": "ns",
            "range": "± 16933.06072483977"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 28091212.722222224,
            "unit": "ns",
            "range": "± 581964.4843200867"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12090494.777777778,
            "unit": "ns",
            "range": "± 586928.734541677"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 2769188.069878472,
            "unit": "ns",
            "range": "± 73869.90223577096"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 2767421.9736328125,
            "unit": "ns",
            "range": "± 33365.77035971125"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 19906.5,
            "unit": "ns",
            "range": "± 5429.755927163893"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 12187.166666666666,
            "unit": "ns",
            "range": "± 80.45650999142332"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14789.111111111111,
            "unit": "ns",
            "range": "± 246.24553013427695"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 30955,
            "unit": "ns",
            "range": "± 6111.301375320972"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 18470.75,
            "unit": "ns",
            "range": "± 307.6620362856434"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3895.9,
            "unit": "ns",
            "range": "± 97.58694129396162"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 66764.33333333333,
            "unit": "ns",
            "range": "± 13776.72295032458"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 84561.11111111111,
            "unit": "ns",
            "range": "± 10673.085465839347"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 54793.5,
            "unit": "ns",
            "range": "± 1224.645021453739"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 446.5,
            "unit": "ns",
            "range": "± 46.23911283270426"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 371.1111111111111,
            "unit": "ns",
            "range": "± 14.920157878223376"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 429.3,
            "unit": "ns",
            "range": "± 34.567325612491345"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 393.1,
            "unit": "ns",
            "range": "± 22.570629883397878"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1895,
            "unit": "ns",
            "range": "± 232.248789017295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1104.1666666666667,
            "unit": "ns",
            "range": "± 63.12685640834652"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1492.3,
            "unit": "ns",
            "range": "± 306.6996540229183"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 556.75,
            "unit": "ns",
            "range": "± 31.212405958437195"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 573.625,
            "unit": "ns",
            "range": "± 19.334923990393282"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 626.5,
            "unit": "ns",
            "range": "± 37.04726710568541"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 294.375,
            "unit": "ns",
            "range": "± 32.184457207078786"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 119.7,
            "unit": "ns",
            "range": "± 26.79572934463832"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 321.1111111111111,
            "unit": "ns",
            "range": "± 18.333333333333332"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2572.375,
            "unit": "ns",
            "range": "± 26.877699200000627"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3777.4,
            "unit": "ns",
            "range": "± 145.2631328926159"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1526,
            "unit": "ns",
            "range": "± 43.165379646193315"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 754.1111111111111,
            "unit": "ns",
            "range": "± 41.744593795018666"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2899.5555555555557,
            "unit": "ns",
            "range": "± 58.21750404970809"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3758.7,
            "unit": "ns",
            "range": "± 280.8242036102539"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 78271.77777777778,
            "unit": "ns",
            "range": "± 13322.700512450336"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 109422.55555555556,
            "unit": "ns",
            "range": "± 8825.72488681682"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 432.2,
            "unit": "ns",
            "range": "± 69.82326896449986"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 374.8888888888889,
            "unit": "ns",
            "range": "± 43.94440932713866"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 423.6666666666667,
            "unit": "ns",
            "range": "± 30.454884665682123"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 354.1,
            "unit": "ns",
            "range": "± 85.07179974063739"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1448.375,
            "unit": "ns",
            "range": "± 53.42000026742258"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1408.1,
            "unit": "ns",
            "range": "± 203.0935471374925"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1343,
            "unit": "ns",
            "range": "± 94.56668093391637"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 539.8,
            "unit": "ns",
            "range": "± 26.828881618302482"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 591.375,
            "unit": "ns",
            "range": "± 33.97872233366893"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 599.125,
            "unit": "ns",
            "range": "± 58.354672967491275"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 281.1111111111111,
            "unit": "ns",
            "range": "± 19.649710204252663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 156.9,
            "unit": "ns",
            "range": "± 23.48734884050466"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 436.3,
            "unit": "ns",
            "range": "± 107.52059234294508"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2538.0555555555557,
            "unit": "ns",
            "range": "± 100.86390721054673"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2966.125,
            "unit": "ns",
            "range": "± 141.93100688111008"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1702.875,
            "unit": "ns",
            "range": "± 196.97420969688972"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 754.8,
            "unit": "ns",
            "range": "± 30.738322082450185"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2754.4444444444443,
            "unit": "ns",
            "range": "± 102.1899103521369"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3155,
            "unit": "ns",
            "range": "± 57.93530875036397"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 76523.125,
            "unit": "ns",
            "range": "± 6316.380980943801"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 135178.5,
            "unit": "ns",
            "range": "± 12362.500434782602"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 441.2,
            "unit": "ns",
            "range": "± 34.36341724049639"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 398.72222222222223,
            "unit": "ns",
            "range": "± 18.55921454276674"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 431.2,
            "unit": "ns",
            "range": "± 20.405881505095532"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 278.6111111111111,
            "unit": "ns",
            "range": "± 27.60183166224863"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1423.3333333333333,
            "unit": "ns",
            "range": "± 31.67412192942371"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1305.1666666666667,
            "unit": "ns",
            "range": "± 48.46648326421054"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1584.888888888889,
            "unit": "ns",
            "range": "± 44.62466931094404"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 554.8888888888889,
            "unit": "ns",
            "range": "± 75.33997020912015"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 572.4,
            "unit": "ns",
            "range": "± 40.202819138297585"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 608.1111111111111,
            "unit": "ns",
            "range": "± 20.823330932180642"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 207.6,
            "unit": "ns",
            "range": "± 25.33969218439719"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 200.8,
            "unit": "ns",
            "range": "± 22.562505771005725"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 376.9,
            "unit": "ns",
            "range": "± 24.704700407457327"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2538.25,
            "unit": "ns",
            "range": "± 139.2374641702851"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3296.9444444444443,
            "unit": "ns",
            "range": "± 99.73478719974179"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1721.75,
            "unit": "ns",
            "range": "± 76.83888524513019"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 753.1666666666666,
            "unit": "ns",
            "range": "± 37.080992435478315"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2736.5,
            "unit": "ns",
            "range": "± 87.12388552269366"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3295.6,
            "unit": "ns",
            "range": "± 38.76195156192331"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 78047.22222222222,
            "unit": "ns",
            "range": "± 8217.839250340958"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 175931.11111111112,
            "unit": "ns",
            "range": "± 5324.324568535535"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "ae2eabb1f61017d4f3ab1d9c5eb95a06274e6528",
          "message": "Merge pull request #127 from thomhurst/perf/batch-pooling-and-struct-metadata\n\nperf: Add batch pooling and convert RecordMetadata to struct",
          "timestamp": "2026-01-27T14:35:05Z",
          "tree_id": "19d96764537878a5e44bce11953f0961588c186c",
          "url": "https://github.com/thomhurst/Dekaf/commit/ae2eabb1f61017d4f3ab1d9c5eb95a06274e6528"
        },
        "date": 1769525135985,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 133.93680896759034,
            "unit": "ns",
            "range": "± 2.82999773310977"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 124.1638656258583,
            "unit": "ns",
            "range": "± 0.529687142219108"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 142916.2768825955,
            "unit": "ns",
            "range": "± 1338.8449935528586"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 127.18836141957178,
            "unit": "ns",
            "range": "± 1.8866882774630933"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017250580.6,
            "unit": "ns",
            "range": "± 954244.9256377001"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3175368770.8,
            "unit": "ns",
            "range": "± 1103877.8975591909"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014889152,
            "unit": "ns",
            "range": "± 1409824.3436909083"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3179599092.8,
            "unit": "ns",
            "range": "± 1123291.867244974"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014516292.8,
            "unit": "ns",
            "range": "± 638353.241432751"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172820139,
            "unit": "ns",
            "range": "± 838088.2421711332"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013123954,
            "unit": "ns",
            "range": "± 158476.39993597363"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177489324.5,
            "unit": "ns",
            "range": "± 252650.91987892438"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013750880.2,
            "unit": "ns",
            "range": "± 761815.5689146159"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173690865.25,
            "unit": "ns",
            "range": "± 397435.69951743976"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013908041.5,
            "unit": "ns",
            "range": "± 307662.70999315684"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179963072.8,
            "unit": "ns",
            "range": "± 763083.4962297376"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014288676.4,
            "unit": "ns",
            "range": "± 809791.20644355"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174601980.5,
            "unit": "ns",
            "range": "± 835496.9470623457"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014130060,
            "unit": "ns",
            "range": "± 412828.56418494106"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178823099.6,
            "unit": "ns",
            "range": "± 927458.5530988972"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8074294.1,
            "unit": "ns",
            "range": "± 570918.3713165716"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6724946.4,
            "unit": "ns",
            "range": "± 170998.55273994443"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 12759212.6,
            "unit": "ns",
            "range": "± 499802.915031115"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 8654753.944444444,
            "unit": "ns",
            "range": "± 529835.725624724"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 90400.36491699218,
            "unit": "ns",
            "range": "± 2458.814766001683"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 79046.82491048177,
            "unit": "ns",
            "range": "± 1459.6846747080858"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 134228.19907226562,
            "unit": "ns",
            "range": "± 1632.2671666808988"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8067107.1,
            "unit": "ns",
            "range": "± 706138.2922044221"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5555101.6,
            "unit": "ns",
            "range": "± 72259.95120888964"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12550098.5,
            "unit": "ns",
            "range": "± 694919.1599605309"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8741713.5,
            "unit": "ns",
            "range": "± 473125.187384375"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 936897.02421875,
            "unit": "ns",
            "range": "± 46735.272548296765"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 842958.615234375,
            "unit": "ns",
            "range": "± 57370.07993945977"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1344878.343359375,
            "unit": "ns",
            "range": "± 10612.495557613405"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8129754.8,
            "unit": "ns",
            "range": "± 644887.6194977781"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5485026.8,
            "unit": "ns",
            "range": "± 39246.028588324145"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 7926784.3,
            "unit": "ns",
            "range": "± 529399.1398842444"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6443880.6,
            "unit": "ns",
            "range": "± 444428.3011249021"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 443118.36284722225,
            "unit": "ns",
            "range": "± 14742.145841319025"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 446831.6817382813,
            "unit": "ns",
            "range": "± 13820.864572078366"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8123318.8,
            "unit": "ns",
            "range": "± 480226.15805843676"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5665448.3,
            "unit": "ns",
            "range": "± 27279.74183671425"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 30802415.222222224,
            "unit": "ns",
            "range": "± 583649.1745892343"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12132213.833333334,
            "unit": "ns",
            "range": "± 444016.6400243351"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 4509448.41796875,
            "unit": "ns",
            "range": "± 127625.19562928611"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 4340368.56875,
            "unit": "ns",
            "range": "± 62303.43475950904"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 21383.38888888889,
            "unit": "ns",
            "range": "± 4755.5940071784"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15737.3,
            "unit": "ns",
            "range": "± 174.40489289772427"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 18701.222222222223,
            "unit": "ns",
            "range": "± 1470.2299801202682"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 31257.666666666668,
            "unit": "ns",
            "range": "± 6471.918610427669"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 18332.8,
            "unit": "ns",
            "range": "± 190.72015566734885"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 3254.4444444444443,
            "unit": "ns",
            "range": "± 49.67673276069772"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 64604.444444444445,
            "unit": "ns",
            "range": "± 12363.048482384018"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 87310.77777777778,
            "unit": "ns",
            "range": "± 13374.313241974125"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 66240.375,
            "unit": "ns",
            "range": "± 614.480253663905"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 405,
            "unit": "ns",
            "range": "± 29.669475522458733"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 294,
            "unit": "ns",
            "range": "± 53.78971401051816"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 318.3333333333333,
            "unit": "ns",
            "range": "± 16.47725705328408"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 362.44444444444446,
            "unit": "ns",
            "range": "± 17.847346519238588"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1640.7,
            "unit": "ns",
            "range": "± 191.85935242023282"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1134.888888888889,
            "unit": "ns",
            "range": "± 106.30786006270237"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1883.5555555555557,
            "unit": "ns",
            "range": "± 166.0407413190443"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 550.4,
            "unit": "ns",
            "range": "± 44.92017611521823"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 516.625,
            "unit": "ns",
            "range": "± 14.302222604496526"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 610.8,
            "unit": "ns",
            "range": "± 42.25267907350833"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 274.44444444444446,
            "unit": "ns",
            "range": "± 29.82076085175859"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 72.7,
            "unit": "ns",
            "range": "± 43.906592772485645"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 292.44444444444446,
            "unit": "ns",
            "range": "± 10.887046329366736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2604.6666666666665,
            "unit": "ns",
            "range": "± 36.238791370574155"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2963.75,
            "unit": "ns",
            "range": "± 52.76294695549682"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1524.4444444444443,
            "unit": "ns",
            "range": "± 48.918583153825885"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 724.3333333333334,
            "unit": "ns",
            "range": "± 20.6155281280883"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2662.875,
            "unit": "ns",
            "range": "± 110.38042981305284"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3797.6,
            "unit": "ns",
            "range": "± 340.0853487647541"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 83229.11111111111,
            "unit": "ns",
            "range": "± 6827.818858252694"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 118977.875,
            "unit": "ns",
            "range": "± 9422.075695446003"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 442.3,
            "unit": "ns",
            "range": "± 32.12838827786625"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 331.8,
            "unit": "ns",
            "range": "± 23.86559587914508"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 425.3888888888889,
            "unit": "ns",
            "range": "± 18.937030155520986"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 295.6666666666667,
            "unit": "ns",
            "range": "± 18.255136263528684"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1769,
            "unit": "ns",
            "range": "± 192.31051279982938"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1212.9,
            "unit": "ns",
            "range": "± 30.296497780730004"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1359.1,
            "unit": "ns",
            "range": "± 86.58772045349926"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 491.875,
            "unit": "ns",
            "range": "± 12.85565467577817"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 580.6,
            "unit": "ns",
            "range": "± 44.49019867091827"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 603.7777777777778,
            "unit": "ns",
            "range": "± 58.66808710401631"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 221.8,
            "unit": "ns",
            "range": "± 65.21383629602268"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 163.5,
            "unit": "ns",
            "range": "± 15.042796092672209"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 311.6111111111111,
            "unit": "ns",
            "range": "± 27.58824226207808"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2656.0555555555557,
            "unit": "ns",
            "range": "± 118.31220468648945"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2981.7,
            "unit": "ns",
            "range": "± 180.37048292642316"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1879.75,
            "unit": "ns",
            "range": "± 135.71477443521024"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 782.9,
            "unit": "ns",
            "range": "± 38.92285817985222"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2937.9,
            "unit": "ns",
            "range": "± 57.78975303248454"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3287.222222222222,
            "unit": "ns",
            "range": "± 48.03066150329854"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 77086.38888888889,
            "unit": "ns",
            "range": "± 11889.937378351122"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 109544.5,
            "unit": "ns",
            "range": "± 5701.437362630585"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 361.27777777777777,
            "unit": "ns",
            "range": "± 37.751747568085435"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 354.2,
            "unit": "ns",
            "range": "± 31.1012682842499"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 380.2,
            "unit": "ns",
            "range": "± 32.91672995774506"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 302.1,
            "unit": "ns",
            "range": "± 44.31816783216563"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1402.3,
            "unit": "ns",
            "range": "± 39.24580657004431"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1248.6666666666667,
            "unit": "ns",
            "range": "± 131.07822092170767"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1467.125,
            "unit": "ns",
            "range": "± 27.751126103277322"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 509.6,
            "unit": "ns",
            "range": "± 16.21521918856891"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 545.5,
            "unit": "ns",
            "range": "± 34.58082320207738"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 484.05555555555554,
            "unit": "ns",
            "range": "± 27.404886020156656"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 265,
            "unit": "ns",
            "range": "± 77.34626328688701"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 155.6,
            "unit": "ns",
            "range": "± 6.915361322607969"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 291.9,
            "unit": "ns",
            "range": "± 18.573876517541752"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2853.8,
            "unit": "ns",
            "range": "± 56.46395113501869"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3258.6666666666665,
            "unit": "ns",
            "range": "± 55.812185049503306"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1574.875,
            "unit": "ns",
            "range": "± 33.54501240337909"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 753,
            "unit": "ns",
            "range": "± 31.55242550986462"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2939.3,
            "unit": "ns",
            "range": "± 76.12715970298935"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3154.1,
            "unit": "ns",
            "range": "± 140.5617302113203"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 127350.77777777778,
            "unit": "ns",
            "range": "± 9931.996787879285"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 110344.33333333333,
            "unit": "ns",
            "range": "± 8815.255583362288"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a6c51023e502795dbc267135ed073a7bff18fd0e",
          "message": "Merge pull request #128 from thomhurst/perf/reduce-metadata-and-decompression-allocations\n\nperf: Reduce allocations in MetadataRequest and decompression paths",
          "timestamp": "2026-01-27T20:04:26Z",
          "tree_id": "84dec807f58dbf839ca28eeb584ec61deff864a6",
          "url": "https://github.com/thomhurst/Dekaf/commit/a6c51023e502795dbc267135ed073a7bff18fd0e"
        },
        "date": 1769544900716,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 130.00660771131516,
            "unit": "ns",
            "range": "± 0.30651321369152335"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 124.37915921211243,
            "unit": "ns",
            "range": "± 0.46647289713539186"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 141565.75389268663,
            "unit": "ns",
            "range": "± 508.50922171739535"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 128.82325063811408,
            "unit": "ns",
            "range": "± 0.3849067157656727"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017495768,
            "unit": "ns",
            "range": "± 1280238.5050518906"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3174533305.3,
            "unit": "ns",
            "range": "± 991657.6873753866"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014479916,
            "unit": "ns",
            "range": "± 783417.8121057115"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3179883437.5,
            "unit": "ns",
            "range": "± 130807.26576404946"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014344793.7,
            "unit": "ns",
            "range": "± 356531.23370989534"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172952179.2,
            "unit": "ns",
            "range": "± 714776.0333427527"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013500942.8,
            "unit": "ns",
            "range": "± 522169.8276286174"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3178407658.9,
            "unit": "ns",
            "range": "± 478604.4402200005"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013946567.6,
            "unit": "ns",
            "range": "± 846200.9379921533"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173865332.25,
            "unit": "ns",
            "range": "± 151917.03908685382"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013029952.5,
            "unit": "ns",
            "range": "± 116633.35489330086"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3178613368.5,
            "unit": "ns",
            "range": "± 268585.71244390495"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014378365.6,
            "unit": "ns",
            "range": "± 325416.4153377945"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174252522.7,
            "unit": "ns",
            "range": "± 1083664.1568932694"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013717697.2,
            "unit": "ns",
            "range": "± 885568.6810071254"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178029391.4,
            "unit": "ns",
            "range": "± 505559.96841799095"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8333895.1,
            "unit": "ns",
            "range": "± 525589.5523723706"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5924176,
            "unit": "ns",
            "range": "± 46821.38109880997"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 9517108,
            "unit": "ns",
            "range": "± 2098309.0143699176"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7181728.555555556,
            "unit": "ns",
            "range": "± 138994.5242402656"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 90857.57319335938,
            "unit": "ns",
            "range": "± 7459.8002865856615"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 76585.00143771702,
            "unit": "ns",
            "range": "± 2640.009317937512"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 129188.55166015626,
            "unit": "ns",
            "range": "± 1359.8303854033315"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8559819.6,
            "unit": "ns",
            "range": "± 468223.9887467085"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5410114.6,
            "unit": "ns",
            "range": "± 15204.957255959502"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12702201.833333334,
            "unit": "ns",
            "range": "± 1043716.9571023554"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8340640.111111111,
            "unit": "ns",
            "range": "± 381515.87195241445"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 839455.185546875,
            "unit": "ns",
            "range": "± 14268.897539732232"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 756486.292578125,
            "unit": "ns",
            "range": "± 18965.14676158645"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1274649.2700195312,
            "unit": "ns",
            "range": "± 52977.51470146201"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8506717.4,
            "unit": "ns",
            "range": "± 613536.9743907406"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5425699.055555556,
            "unit": "ns",
            "range": "± 12574.45885626009"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8186935.4,
            "unit": "ns",
            "range": "± 390962.34441622806"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6758710.1,
            "unit": "ns",
            "range": "± 78206.42553481992"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 416066.2879882812,
            "unit": "ns",
            "range": "± 8677.692337385883"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 411527.52314453124,
            "unit": "ns",
            "range": "± 8268.99907345735"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8458093.3,
            "unit": "ns",
            "range": "± 588965.8924779941"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5401377,
            "unit": "ns",
            "range": "± 15352.475972949771"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 27791064.5,
            "unit": "ns",
            "range": "± 498751.2871084001"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11422940.888888888,
            "unit": "ns",
            "range": "± 396499.75302244653"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 4171137.228125,
            "unit": "ns",
            "range": "± 116621.08628794274"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 4041422.4248046875,
            "unit": "ns",
            "range": "± 34006.890215535655"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 21335.777777777777,
            "unit": "ns",
            "range": "± 5105.0707090543265"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15660.2,
            "unit": "ns",
            "range": "± 372.8422514206839"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15021,
            "unit": "ns",
            "range": "± 563.9355016311706"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 40161,
            "unit": "ns",
            "range": "± 5549.170816437353"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 22805.4,
            "unit": "ns",
            "range": "± 1287.0188809803842"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5282.5,
            "unit": "ns",
            "range": "± 355.91579840680794"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 71600,
            "unit": "ns",
            "range": "± 15416.713503532457"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 85009.16666666667,
            "unit": "ns",
            "range": "± 11113.496187069126"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 57270,
            "unit": "ns",
            "range": "± 1057.3210081549096"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 589.8,
            "unit": "ns",
            "range": "± 149.02408455608034"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 377.125,
            "unit": "ns",
            "range": "± 24.761361028828766"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 525.3,
            "unit": "ns",
            "range": "± 39.35889000241524"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 373.2,
            "unit": "ns",
            "range": "± 31.445897100328438"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1710.6,
            "unit": "ns",
            "range": "± 216.2787296265837"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1195.8333333333333,
            "unit": "ns",
            "range": "± 17.0293863659264"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1792,
            "unit": "ns",
            "range": "± 138.728351664523"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 790.9444444444445,
            "unit": "ns",
            "range": "± 118.52437630199864"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 499.7,
            "unit": "ns",
            "range": "± 42.171738825373986"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 563.8,
            "unit": "ns",
            "range": "± 21.729652039960918"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 328.27777777777777,
            "unit": "ns",
            "range": "± 19.992359651738074"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 110.66666666666667,
            "unit": "ns",
            "range": "± 27.572631357924475"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 134.2,
            "unit": "ns",
            "range": "± 15.186251091767918"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2541.777777777778,
            "unit": "ns",
            "range": "± 97.37014144204807"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3075.125,
            "unit": "ns",
            "range": "± 64.7201392811498"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1444.4,
            "unit": "ns",
            "range": "± 100.96556068503976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 760.9,
            "unit": "ns",
            "range": "± 41.07026499386955"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2874.8888888888887,
            "unit": "ns",
            "range": "± 115.68863864317494"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3244.6,
            "unit": "ns",
            "range": "± 543.9724666234904"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 74473.25,
            "unit": "ns",
            "range": "± 4348.758468147629"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 116140.33333333333,
            "unit": "ns",
            "range": "± 14619.812165346038"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 405.25,
            "unit": "ns",
            "range": "± 85.32249745189466"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 371,
            "unit": "ns",
            "range": "± 22.82785822435191"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 405.05555555555554,
            "unit": "ns",
            "range": "± 22.344524559224297"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 303.5,
            "unit": "ns",
            "range": "± 21.17191535974013"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1436.4,
            "unit": "ns",
            "range": "± 47.968044918813746"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1208.9,
            "unit": "ns",
            "range": "± 84.39648491890327"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1503.9,
            "unit": "ns",
            "range": "± 44.51516595498662"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 578,
            "unit": "ns",
            "range": "± 28.2444684849972"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 608.5555555555555,
            "unit": "ns",
            "range": "± 36.218472880255156"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 601.4,
            "unit": "ns",
            "range": "± 29.182186347153632"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 181.125,
            "unit": "ns",
            "range": "± 18.011405116917288"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 340.4,
            "unit": "ns",
            "range": "± 132.2961324705551"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 332.5,
            "unit": "ns",
            "range": "± 16.690459207925603"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2578.5,
            "unit": "ns",
            "range": "± 65.8132880875047"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3562.5,
            "unit": "ns",
            "range": "± 386.0754819692359"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1575.7777777777778,
            "unit": "ns",
            "range": "± 63.89009660694249"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 700.625,
            "unit": "ns",
            "range": "± 26.09837159671078"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2954.1,
            "unit": "ns",
            "range": "± 77.3540920414399"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 4031.1666666666665,
            "unit": "ns",
            "range": "± 290.41048534789513"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 82576,
            "unit": "ns",
            "range": "± 5633.255319972635"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 116457.33333333333,
            "unit": "ns",
            "range": "± 7982.1390616801455"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 421.77777777777777,
            "unit": "ns",
            "range": "± 21.235452536841414"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 12,
            "unit": "ns",
            "range": "± 15.311578770474469"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 409.6111111111111,
            "unit": "ns",
            "range": "± 31.927435085066122"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 383.3333333333333,
            "unit": "ns",
            "range": "± 25.816661286851176"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1483,
            "unit": "ns",
            "range": "± 42.8985514240949"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1526.6666666666667,
            "unit": "ns",
            "range": "± 219.10214512870473"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1714.6666666666667,
            "unit": "ns",
            "range": "± 278.4784551810068"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 704.8,
            "unit": "ns",
            "range": "± 95.5670794084797"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 640.75,
            "unit": "ns",
            "range": "± 26.73013280924732"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 566,
            "unit": "ns",
            "range": "± 36.296398876866164"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 230.375,
            "unit": "ns",
            "range": "± 21.38382232302862"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 154.5,
            "unit": "ns",
            "range": "± 21.327380121738884"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 0,
            "unit": "ns",
            "range": "± 0"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2376.5,
            "unit": "ns",
            "range": "± 122.9735743971037"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 2934.75,
            "unit": "ns",
            "range": "± 36.03074084326168"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 2054.3,
            "unit": "ns",
            "range": "± 206.70056173659088"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 1172.5,
            "unit": "ns",
            "range": "± 169.43845162444353"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2798.1111111111113,
            "unit": "ns",
            "range": "± 64.65377878446945"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3335.5555555555557,
            "unit": "ns",
            "range": "± 29.879387172058564"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 74198.11111111111,
            "unit": "ns",
            "range": "± 5350.035944842904"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 110180.22222222222,
            "unit": "ns",
            "range": "± 11236.296273436566"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "329603dd0c39bae64047d692523e001d769a8c5f",
          "message": "Merge pull request #129 from thomhurst/feature/api-improvements-and-docs\n\nfeat: API improvements and Docusaurus documentation site",
          "timestamp": "2026-01-27T22:00:57Z",
          "tree_id": "186fe56e526842a30e0502665c12503eb5764c1f",
          "url": "https://github.com/thomhurst/Dekaf/commit/329603dd0c39bae64047d692523e001d769a8c5f"
        },
        "date": 1769551897504,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 131.48040596644083,
            "unit": "ns",
            "range": "± 0.6084557086019032"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 126.84183642599318,
            "unit": "ns",
            "range": "± 0.23853766510125504"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 143181.09817504883,
            "unit": "ns",
            "range": "± 397.32105398140254"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 123.77385732862685,
            "unit": "ns",
            "range": "± 0.6947642710006684"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017843219.25,
            "unit": "ns",
            "range": "± 725193.0287247091"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3175820795.5,
            "unit": "ns",
            "range": "± 794197.0536076043"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014125938.9,
            "unit": "ns",
            "range": "± 1026248.6437339637"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180316612.2,
            "unit": "ns",
            "range": "± 916350.4278880978"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014349258.5,
            "unit": "ns",
            "range": "± 185982.55532083288"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172508989.8,
            "unit": "ns",
            "range": "± 443279.77217858704"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013088283.5,
            "unit": "ns",
            "range": "± 667064.8700956053"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177914348,
            "unit": "ns",
            "range": "± 206573.99364715136"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013737676.1,
            "unit": "ns",
            "range": "± 571115.5876408032"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3174065886.9,
            "unit": "ns",
            "range": "± 583217.9633788726"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012903276,
            "unit": "ns",
            "range": "± 657876.1520331923"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179548650.7,
            "unit": "ns",
            "range": "± 446462.52991230966"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013636434,
            "unit": "ns",
            "range": "± 605182.2725295248"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174119878.6,
            "unit": "ns",
            "range": "± 361543.50305640954"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013463280,
            "unit": "ns",
            "range": "± 722750.8455999896"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178077258.5,
            "unit": "ns",
            "range": "± 898343.3995519197"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8150640.5,
            "unit": "ns",
            "range": "± 492676.0292557033"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7057926.8,
            "unit": "ns",
            "range": "± 113804.82471006618"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 12147134.944444444,
            "unit": "ns",
            "range": "± 1058627.289978762"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 9425316,
            "unit": "ns",
            "range": "± 498785.9597217583"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 83671.89147949219,
            "unit": "ns",
            "range": "± 5293.70173269821"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 73228.49211425781,
            "unit": "ns",
            "range": "± 3526.490759305823"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 124795.7721923828,
            "unit": "ns",
            "range": "± 3544.342288367061"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8359938.5,
            "unit": "ns",
            "range": "± 686413.6503707756"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5591495,
            "unit": "ns",
            "range": "± 27700.21162775155"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12307582.625,
            "unit": "ns",
            "range": "± 729746.831644458"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8936460.722222222,
            "unit": "ns",
            "range": "± 429002.3357951498"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 814915.6174045139,
            "unit": "ns",
            "range": "± 71046.64537513554"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 712311.9392578125,
            "unit": "ns",
            "range": "± 28626.879899312396"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1252335.8921875,
            "unit": "ns",
            "range": "± 31247.373407387662"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8601500.3,
            "unit": "ns",
            "range": "± 455367.41998377285"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5613852.6,
            "unit": "ns",
            "range": "± 52128.751135369945"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8458215.9,
            "unit": "ns",
            "range": "± 327954.2496816693"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6458520.6,
            "unit": "ns",
            "range": "± 60986.388664375125"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 383843.794140625,
            "unit": "ns",
            "range": "± 31573.830999134116"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 344242.25625,
            "unit": "ns",
            "range": "± 19757.628232634444"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8346091.3,
            "unit": "ns",
            "range": "± 697523.8051397401"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5788172.5,
            "unit": "ns",
            "range": "± 145062.22853227507"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 23815122.25,
            "unit": "ns",
            "range": "± 821622.00216223"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 9313200.444444444,
            "unit": "ns",
            "range": "± 453789.9242714384"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 3747928.24921875,
            "unit": "ns",
            "range": "± 135550.9084264494"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 3932999.2984375,
            "unit": "ns",
            "range": "± 266754.6291371634"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20721.5,
            "unit": "ns",
            "range": "± 5477.312479674681"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 12149.2,
            "unit": "ns",
            "range": "± 108.67055207777723"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 14942.3,
            "unit": "ns",
            "range": "± 189.64296864254038"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 41485.38888888889,
            "unit": "ns",
            "range": "± 7536.818882068954"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 22887.9,
            "unit": "ns",
            "range": "± 295.5019082472696"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5350.5,
            "unit": "ns",
            "range": "± 173.045433982588"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 64566.055555555555,
            "unit": "ns",
            "range": "± 11904.88692839112"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 83213.875,
            "unit": "ns",
            "range": "± 8399.223219823876"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 55879.375,
            "unit": "ns",
            "range": "± 1485.7152137703144"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 344.44444444444446,
            "unit": "ns",
            "range": "± 30.86709862908689"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 382.6,
            "unit": "ns",
            "range": "± 38.63921439274988"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 394.77777777777777,
            "unit": "ns",
            "range": "± 37.34560810114684"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 390.2,
            "unit": "ns",
            "range": "± 54.686581738322445"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1469.6,
            "unit": "ns",
            "range": "± 113.75919987216663"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1095.6,
            "unit": "ns",
            "range": "± 143.74916424877824"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1539.875,
            "unit": "ns",
            "range": "± 67.31257046686854"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 556.7222222222222,
            "unit": "ns",
            "range": "± 26.4517380231327"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 663,
            "unit": "ns",
            "range": "± 47.89166014152267"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 614,
            "unit": "ns",
            "range": "± 23.903742152409713"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 292.2,
            "unit": "ns",
            "range": "± 15.230087181482435"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 142.375,
            "unit": "ns",
            "range": "± 12.850097275896397"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 333.1111111111111,
            "unit": "ns",
            "range": "± 34.58845343624243"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2647.6666666666665,
            "unit": "ns",
            "range": "± 96.73417183188162"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2933.75,
            "unit": "ns",
            "range": "± 119.16944718700823"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1487.25,
            "unit": "ns",
            "range": "± 16.04235465438733"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 840,
            "unit": "ns",
            "range": "± 66.75702210254738"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2699,
            "unit": "ns",
            "range": "± 142.38152970101143"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3255.0555555555557,
            "unit": "ns",
            "range": "± 67.84561723337609"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 79548.11111111111,
            "unit": "ns",
            "range": "± 9912.036161208813"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 104534.27777777778,
            "unit": "ns",
            "range": "± 6478.520409356171"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 390.3,
            "unit": "ns",
            "range": "± 21.024060290798044"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 309.1111111111111,
            "unit": "ns",
            "range": "± 32.12259502454792"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 389.3333333333333,
            "unit": "ns",
            "range": "± 44.384682042344295"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 375.8888888888889,
            "unit": "ns",
            "range": "± 18.134528146911105"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1325.375,
            "unit": "ns",
            "range": "± 69.77092620036761"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1390,
            "unit": "ns",
            "range": "± 282.1729178279793"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1449.75,
            "unit": "ns",
            "range": "± 56.08857790520786"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 529.2222222222222,
            "unit": "ns",
            "range": "± 50.343266128097454"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 600.5555555555555,
            "unit": "ns",
            "range": "± 35.5003912341509"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 620.1,
            "unit": "ns",
            "range": "± 101.37630667742614"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 283.27777777777777,
            "unit": "ns",
            "range": "± 34.56073558887953"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 142.9,
            "unit": "ns",
            "range": "± 19.450506991392853"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 338.6,
            "unit": "ns",
            "range": "± 24.140560612103993"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2715.5555555555557,
            "unit": "ns",
            "range": "± 85.33919250718147"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3153.222222222222,
            "unit": "ns",
            "range": "± 55.672654368589654"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1590.6,
            "unit": "ns",
            "range": "± 405.052863490955"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 835.6111111111111,
            "unit": "ns",
            "range": "± 49.94357927813255"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2888.5,
            "unit": "ns",
            "range": "± 60.70145522692736"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3885.8,
            "unit": "ns",
            "range": "± 217.82653649176908"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 76950.88888888889,
            "unit": "ns",
            "range": "± 4832.674193561067"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 108417,
            "unit": "ns",
            "range": "± 5916.4350511338935"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 408.05555555555554,
            "unit": "ns",
            "range": "± 25.060482393157915"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 358.8888888888889,
            "unit": "ns",
            "range": "± 44.01830427346232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 377.7,
            "unit": "ns",
            "range": "± 22.369622258768697"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 350.7,
            "unit": "ns",
            "range": "± 49.409400814914655"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1473.9444444444443,
            "unit": "ns",
            "range": "± 42.667643217991056"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1340.5,
            "unit": "ns",
            "range": "± 76.85484153042452"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1574.875,
            "unit": "ns",
            "range": "± 47.8044752836265"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 548.3333333333334,
            "unit": "ns",
            "range": "± 37.282703764614496"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 627.3333333333334,
            "unit": "ns",
            "range": "± 45.71925196238451"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 639.5555555555555,
            "unit": "ns",
            "range": "± 30.708757346688223"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 324.2,
            "unit": "ns",
            "range": "± 106.17888469726716"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 163.8,
            "unit": "ns",
            "range": "± 18.760478553479267"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 340.2,
            "unit": "ns",
            "range": "± 21.811312049790434"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2679.222222222222,
            "unit": "ns",
            "range": "± 50.35071443827232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3258.1666666666665,
            "unit": "ns",
            "range": "± 74.91662031885848"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1596,
            "unit": "ns",
            "range": "± 374.8784247370517"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 827,
            "unit": "ns",
            "range": "± 39.6540596212348"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3004,
            "unit": "ns",
            "range": "± 48.69804924224378"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3840.8888888888887,
            "unit": "ns",
            "range": "± 233.28922202088788"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 93270.66666666667,
            "unit": "ns",
            "range": "± 4072.625903517287"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 114283.55555555556,
            "unit": "ns",
            "range": "± 9022.108125475872"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "dd4ce8a028a61489ad42652dde5a3b9f800268f6",
          "message": "Merge pull request #130 from thomhurst/feature/performance-optimizations\n\nperf: Optimize VarInt encoding and configurable batch capacity",
          "timestamp": "2026-01-27T23:09:16Z",
          "tree_id": "160bba557609f76c1bfe638ca4a9b91bae71a499",
          "url": "https://github.com/thomhurst/Dekaf/commit/dd4ce8a028a61489ad42652dde5a3b9f800268f6"
        },
        "date": 1769555991306,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 135.32193987369538,
            "unit": "ns",
            "range": "± 2.3967270465719155"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 127.70802783966064,
            "unit": "ns",
            "range": "± 2.6874014579968155"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 143282.9840576172,
            "unit": "ns",
            "range": "± 1451.5830960794397"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 125.9982330083847,
            "unit": "ns",
            "range": "± 1.5492200347585123"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017616721.25,
            "unit": "ns",
            "range": "± 593960.6260375486"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3175970132.9,
            "unit": "ns",
            "range": "± 1235275.3874091397"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015691806.1,
            "unit": "ns",
            "range": "± 923859.6214968484"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180745602.9,
            "unit": "ns",
            "range": "± 838980.6888884868"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3015113137.6,
            "unit": "ns",
            "range": "± 1386353.9296589454"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3173520679.75,
            "unit": "ns",
            "range": "± 366597.1824926964"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013478231.5,
            "unit": "ns",
            "range": "± 482278.294227859"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3179365076,
            "unit": "ns",
            "range": "± 558534.797230665"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014326505.7,
            "unit": "ns",
            "range": "± 543576.821039768"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3174044665.75,
            "unit": "ns",
            "range": "± 1004468.3949188828"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013194483.6,
            "unit": "ns",
            "range": "± 438364.1001579623"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179663150.1,
            "unit": "ns",
            "range": "± 1010826.1796922853"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014929511.7,
            "unit": "ns",
            "range": "± 952110.8414516137"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174196382.25,
            "unit": "ns",
            "range": "± 365490.30256481044"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013471280,
            "unit": "ns",
            "range": "± 59159.03236474827"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177651071.25,
            "unit": "ns",
            "range": "± 144571.1088988737"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 8299854.9,
            "unit": "ns",
            "range": "± 691550.9630543748"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 6405906,
            "unit": "ns",
            "range": "± 125517.47626348912"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 11112202.5,
            "unit": "ns",
            "range": "± 2376958.224220763"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 7834658,
            "unit": "ns",
            "range": "± 280138.48572536407"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 87441.66118164062,
            "unit": "ns",
            "range": "± 1661.521501703123"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 75508.8359375,
            "unit": "ns",
            "range": "± 968.089523023892"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 131876.3612060547,
            "unit": "ns",
            "range": "± 1314.0935713832914"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 8412565.2,
            "unit": "ns",
            "range": "± 400284.3013994484"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5431564.125,
            "unit": "ns",
            "range": "± 17984.09606082552"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 12512382.222222222,
            "unit": "ns",
            "range": "± 1057333.9667477084"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 8472791.666666666,
            "unit": "ns",
            "range": "± 351722.2270780594"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 919495.36640625,
            "unit": "ns",
            "range": "± 74485.17890810268"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 823413.666796875,
            "unit": "ns",
            "range": "± 69361.64132618261"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1332756.733984375,
            "unit": "ns",
            "range": "± 22387.23307796675"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8435892.4,
            "unit": "ns",
            "range": "± 507029.13527253154"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5435708.8,
            "unit": "ns",
            "range": "± 32368.03933254737"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 8306086.4,
            "unit": "ns",
            "range": "± 582988.4318309508"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6623925.3,
            "unit": "ns",
            "range": "± 435590.5168870823"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 365548.1509765625,
            "unit": "ns",
            "range": "± 11986.812209265887"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 342283.3154296875,
            "unit": "ns",
            "range": "± 5073.50068000541"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 8411315.2,
            "unit": "ns",
            "range": "± 583532.9631654258"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5470087.9,
            "unit": "ns",
            "range": "± 43379.275441415826"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 31238982.3,
            "unit": "ns",
            "range": "± 1630256.0429397312"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 12614286.444444444,
            "unit": "ns",
            "range": "± 474601.4186949695"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 3487385.02734375,
            "unit": "ns",
            "range": "± 71143.47892531229"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 3393241.3541666665,
            "unit": "ns",
            "range": "± 33424.28393548628"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20643.777777777777,
            "unit": "ns",
            "range": "± 5270.045701362033"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15320.3,
            "unit": "ns",
            "range": "± 1059.4957972754987"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 12952.3,
            "unit": "ns",
            "range": "± 300.51762751034164"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 48003.61111111111,
            "unit": "ns",
            "range": "± 7729.838556600721"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21959.555555555555,
            "unit": "ns",
            "range": "± 608.8583807239396"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 4862.611111111111,
            "unit": "ns",
            "range": "± 238.09055233484403"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 44089.444444444445,
            "unit": "ns",
            "range": "± 8582.613487614235"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 67085.77777777778,
            "unit": "ns",
            "range": "± 10851.745456121076"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 55626.875,
            "unit": "ns",
            "range": "± 676.0083341624886"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 441,
            "unit": "ns",
            "range": "± 31.556827047935816"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 359.22222222222223,
            "unit": "ns",
            "range": "± 23.44556342774565"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 394.8333333333333,
            "unit": "ns",
            "range": "± 34.058772731852805"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 338.125,
            "unit": "ns",
            "range": "± 28.14725310119723"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1376.3333333333333,
            "unit": "ns",
            "range": "± 93.81764226412855"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1311.75,
            "unit": "ns",
            "range": "± 50.400538545875534"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1747.4,
            "unit": "ns",
            "range": "± 188.5355551495674"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 402.5,
            "unit": "ns",
            "range": "± 33.702126540224924"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 437,
            "unit": "ns",
            "range": "± 24.804233509624925"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 529.4,
            "unit": "ns",
            "range": "± 95.70696015558232"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 296,
            "unit": "ns",
            "range": "± 41.41926551202418"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 128.625,
            "unit": "ns",
            "range": "± 20.374616560809187"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 335.625,
            "unit": "ns",
            "range": "± 7.661359446692772"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2639.375,
            "unit": "ns",
            "range": "± 81.19806032166039"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2946.222222222222,
            "unit": "ns",
            "range": "± 92.27781122482503"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1471.3,
            "unit": "ns",
            "range": "± 218.4689400756496"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 628.6,
            "unit": "ns",
            "range": "± 34.231888966608636"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 3614.1,
            "unit": "ns",
            "range": "± 231.4017430069762"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3766.8888888888887,
            "unit": "ns",
            "range": "± 490.8333333333333"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 80607.88888888889,
            "unit": "ns",
            "range": "± 11842.768283265155"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 99895.88888888889,
            "unit": "ns",
            "range": "± 8838.520838415845"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 422.3333333333333,
            "unit": "ns",
            "range": "± 23.323807579381203"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 351.8,
            "unit": "ns",
            "range": "± 19.62453113381877"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 337.25,
            "unit": "ns",
            "range": "± 14.53812721285458"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 418.6,
            "unit": "ns",
            "range": "± 107.08584303155006"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1312,
            "unit": "ns",
            "range": "± 45.669621037559374"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1112.6,
            "unit": "ns",
            "range": "± 212.20611049951728"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1412.1,
            "unit": "ns",
            "range": "± 166.45416452852385"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 504.5,
            "unit": "ns",
            "range": "± 29.556913084947"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 505.3,
            "unit": "ns",
            "range": "± 44.54972253311773"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 476,
            "unit": "ns",
            "range": "± 54.72659317004851"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 271.1666666666667,
            "unit": "ns",
            "range": "± 19.595917942265423"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 157.61111111111111,
            "unit": "ns",
            "range": "± 46.396240269132925"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 299.125,
            "unit": "ns",
            "range": "± 9.75320460156558"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2721.3333333333335,
            "unit": "ns",
            "range": "± 110.70682002478438"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2971.7,
            "unit": "ns",
            "range": "± 87.40556809113097"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1479.7,
            "unit": "ns",
            "range": "± 58.938385907092275"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 661.7,
            "unit": "ns",
            "range": "± 141.51643014152103"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2989.0555555555557,
            "unit": "ns",
            "range": "± 218.3718795490339"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3287.8333333333335,
            "unit": "ns",
            "range": "± 57.599913194379035"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 72521.22222222222,
            "unit": "ns",
            "range": "± 6374.677458071463"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 112508.33333333333,
            "unit": "ns",
            "range": "± 15150.547267343185"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 450.44444444444446,
            "unit": "ns",
            "range": "± 29.542812624693976"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 356.8888888888889,
            "unit": "ns",
            "range": "± 25.76550234540579"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 461.2,
            "unit": "ns",
            "range": "± 63.18192427866972"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 349.1,
            "unit": "ns",
            "range": "± 21.37729844692469"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1528.888888888889,
            "unit": "ns",
            "range": "± 111.35017337710396"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1388.1,
            "unit": "ns",
            "range": "± 85.71328174015196"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1570.3333333333333,
            "unit": "ns",
            "range": "± 69.51618516575834"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 482.44444444444446,
            "unit": "ns",
            "range": "± 88.4224393340162"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 414.55555555555554,
            "unit": "ns",
            "range": "± 51.415248494758615"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 462.625,
            "unit": "ns",
            "range": "± 29.451837197120902"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 228.375,
            "unit": "ns",
            "range": "± 13.86606855395058"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 111.88888888888889,
            "unit": "ns",
            "range": "± 11.016401913107162"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 369,
            "unit": "ns",
            "range": "± 35.358481616469646"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2828.4,
            "unit": "ns",
            "range": "± 102.79261754728411"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3053.875,
            "unit": "ns",
            "range": "± 171.09348948121067"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1611.4,
            "unit": "ns",
            "range": "± 144.08346346629943"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 731.7777777777778,
            "unit": "ns",
            "range": "± 100.74693268007937"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2887.5,
            "unit": "ns",
            "range": "± 134.44701558606647"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3437.722222222222,
            "unit": "ns",
            "range": "± 207.6931497292206"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 84563.5,
            "unit": "ns",
            "range": "± 5979.872030403326"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 114920.88888888889,
            "unit": "ns",
            "range": "± 5469.123180283208"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7befbd609ca33cb74dd3c22db1f9877c751ac0cd",
          "message": "Merge pull request #132 from thomhurst/feature/topic-producer\n\nfeat: Add topic-specific producer (ITopicProducer)",
          "timestamp": "2026-01-28T00:53:53Z",
          "tree_id": "bf2b9c06a31282bb77e2fd066f02212429ed9d6b",
          "url": "https://github.com/thomhurst/Dekaf/commit/7befbd609ca33cb74dd3c22db1f9877c751ac0cd"
        },
        "date": 1769562271465,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 130.64990139007568,
            "unit": "ns",
            "range": "± 0.854472576202588"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 124.77316226959229,
            "unit": "ns",
            "range": "± 0.9616425719027791"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 143984.28295898438,
            "unit": "ns",
            "range": "± 834.9746265480147"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 128.6535302400589,
            "unit": "ns",
            "range": "± 0.7643341681936985"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3018220006.5,
            "unit": "ns",
            "range": "± 854347.8506435967"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176139544.8,
            "unit": "ns",
            "range": "± 877355.0564068689"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015256272.4,
            "unit": "ns",
            "range": "± 961944.391813425"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180530731.4,
            "unit": "ns",
            "range": "± 861716.2317818436"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014235829.8,
            "unit": "ns",
            "range": "± 165519.80685313765"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172641087.1,
            "unit": "ns",
            "range": "± 1124238.0177372585"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013660915.4,
            "unit": "ns",
            "range": "± 782475.3013362787"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3178552972.7,
            "unit": "ns",
            "range": "± 582065.3423866603"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3014031818.25,
            "unit": "ns",
            "range": "± 514720.10926546284"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173947344.9,
            "unit": "ns",
            "range": "± 468298.3584562303"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012918097.6,
            "unit": "ns",
            "range": "± 578212.6772877779"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3178606172,
            "unit": "ns",
            "range": "± 610911.8295924544"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015085138.5,
            "unit": "ns",
            "range": "± 612723.2975679153"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173370127.6,
            "unit": "ns",
            "range": "± 551311.8087918125"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013673333.5,
            "unit": "ns",
            "range": "± 669954.8041644301"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178194337,
            "unit": "ns",
            "range": "± 707873.5441966312"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20494.444444444445,
            "unit": "ns",
            "range": "± 5382.673896659334"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 15411.777777777777,
            "unit": "ns",
            "range": "± 208.9370585713421"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15722.9,
            "unit": "ns",
            "range": "± 506.79021519976317"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 32981,
            "unit": "ns",
            "range": "± 6056.962888161973"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 16884.75,
            "unit": "ns",
            "range": "± 227.32277241214277"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 5192.6,
            "unit": "ns",
            "range": "± 115.9302472274696"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 48535.444444444445,
            "unit": "ns",
            "range": "± 12117.950642653146"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 51235.22222222222,
            "unit": "ns",
            "range": "± 8292.653643101492"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 56747,
            "unit": "ns",
            "range": "± 2787.7547417231667"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 440.1,
            "unit": "ns",
            "range": "± 23.71333427045261"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 303.7,
            "unit": "ns",
            "range": "± 25.40691069593293"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 572.3,
            "unit": "ns",
            "range": "± 162.46627000361917"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 328.1,
            "unit": "ns",
            "range": "± 26.784946020728384"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1482.1,
            "unit": "ns",
            "range": "± 120.22797788645813"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1130.5,
            "unit": "ns",
            "range": "± 57.02095131518667"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1371.611111111111,
            "unit": "ns",
            "range": "± 56.0076879643421"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 468.2,
            "unit": "ns",
            "range": "± 33.41589774676453"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 222.4,
            "unit": "ns",
            "range": "± 21.30831866770451"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 771.8,
            "unit": "ns",
            "range": "± 171.37599987551738"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 266.1,
            "unit": "ns",
            "range": "± 28.16794868877273"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 173.25,
            "unit": "ns",
            "range": "± 12.991755627539884"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 301,
            "unit": "ns",
            "range": "± 19.119507199599983"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 3009.2,
            "unit": "ns",
            "range": "± 181.34730338343726"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2644.3333333333335,
            "unit": "ns",
            "range": "± 137.32261285017844"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1407.5555555555557,
            "unit": "ns",
            "range": "± 43.90646624106497"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 687.2222222222222,
            "unit": "ns",
            "range": "± 89.35711748061507"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2814.4,
            "unit": "ns",
            "range": "± 54.314823022817635"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3190.3,
            "unit": "ns",
            "range": "± 94.85550414534028"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 83323.375,
            "unit": "ns",
            "range": "± 4619.455098895787"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 107361.875,
            "unit": "ns",
            "range": "± 4261.332033112249"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 422.44444444444446,
            "unit": "ns",
            "range": "± 28.478549432472466"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 359.7,
            "unit": "ns",
            "range": "± 21.998989875800106"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 269.2,
            "unit": "ns",
            "range": "± 25.13430590514354"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 334.6,
            "unit": "ns",
            "range": "± 26.818111624629932"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1549.3,
            "unit": "ns",
            "range": "± 162.87285429643168"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1245.5,
            "unit": "ns",
            "range": "± 27.345017827750635"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1335.5,
            "unit": "ns",
            "range": "± 114.11982007229653"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 427.44444444444446,
            "unit": "ns",
            "range": "± 17.118054146946076"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 498.4,
            "unit": "ns",
            "range": "± 22.993718949119803"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 417.6,
            "unit": "ns",
            "range": "± 29.508755762767546"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 219.9,
            "unit": "ns",
            "range": "± 21.860415773213873"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 161.8,
            "unit": "ns",
            "range": "± 28.026971136944343"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 298.625,
            "unit": "ns",
            "range": "± 24.599288607600016"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2617.8888888888887,
            "unit": "ns",
            "range": "± 27.67871223722504"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3264,
            "unit": "ns",
            "range": "± 143.52796862555317"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1438.5,
            "unit": "ns",
            "range": "± 50.714889332423866"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 863.2,
            "unit": "ns",
            "range": "± 150.69380286601776"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2784.875,
            "unit": "ns",
            "range": "± 83.39310951323085"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3568.7,
            "unit": "ns",
            "range": "± 452.40463697397655"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 72277.33333333333,
            "unit": "ns",
            "range": "± 5133.754327974801"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 108557.625,
            "unit": "ns",
            "range": "± 5573.408086299492"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 433.6666666666667,
            "unit": "ns",
            "range": "± 17.2554339267374"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 358.6,
            "unit": "ns",
            "range": "± 31.242599123632463"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 527.2,
            "unit": "ns",
            "range": "± 214.8689781652479"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 387.4,
            "unit": "ns",
            "range": "± 31.721706553504756"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1472.9444444444443,
            "unit": "ns",
            "range": "± 42.57672812438478"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1273.888888888889,
            "unit": "ns",
            "range": "± 33.71737105871558"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1366.9444444444443,
            "unit": "ns",
            "range": "± 47.334213606838105"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 483.4,
            "unit": "ns",
            "range": "± 15.086417732516889"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 319.7,
            "unit": "ns",
            "range": "± 17.209816320280055"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 191.875,
            "unit": "ns",
            "range": "± 11.319231422671772"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 241.6,
            "unit": "ns",
            "range": "± 31.952916750883457"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 105.875,
            "unit": "ns",
            "range": "± 12.111830108263103"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 337.75,
            "unit": "ns",
            "range": "± 8.79529094783924"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2761.5555555555557,
            "unit": "ns",
            "range": "± 93.37841173300056"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3186.3333333333335,
            "unit": "ns",
            "range": "± 119.29061153334742"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1597.7777777777778,
            "unit": "ns",
            "range": "± 33.43193749163282"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 697.125,
            "unit": "ns",
            "range": "± 53.56021311170661"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2765.875,
            "unit": "ns",
            "range": "± 23.727244738004092"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3332.8,
            "unit": "ns",
            "range": "± 74.21111027931659"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 86839,
            "unit": "ns",
            "range": "± 9249.234995392862"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 133190.55555555556,
            "unit": "ns",
            "range": "± 15070.76066520127"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "4c3ee91819c0417210bf75f8540e48be1f404b79",
          "message": "docs: Remove claim about \"most\" Kafka clients wrapping librdkafka\n\nReword to simply note that unlike libraries wrapping librdkafka, Dekaf\nis a native .NET implementation.",
          "timestamp": "2026-01-28T01:07:38Z",
          "tree_id": "65f50bc69ecb22f03e49b1aafe189feab6add787",
          "url": "https://github.com/thomhurst/Dekaf/commit/4c3ee91819c0417210bf75f8540e48be1f404b79"
        },
        "date": 1769563094682,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 129.69654165373908,
            "unit": "ns",
            "range": "± 0.6887530688169429"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 125.1128857533137,
            "unit": "ns",
            "range": "± 0.5476355271163057"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 142958.0192626953,
            "unit": "ns",
            "range": "± 592.9047966829323"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 122.92226088047028,
            "unit": "ns",
            "range": "± 0.43121345871425615"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3017098988,
            "unit": "ns",
            "range": "± 1170448.4849490386"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3174867666.6,
            "unit": "ns",
            "range": "± 159539.8848072168"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3014366674.5,
            "unit": "ns",
            "range": "± 755032.7374070169"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3180500786,
            "unit": "ns",
            "range": "± 653472.0829457828"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014446866,
            "unit": "ns",
            "range": "± 341006.3718436944"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3172808327.6,
            "unit": "ns",
            "range": "± 904052.0945884701"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3013462213.4,
            "unit": "ns",
            "range": "± 356721.61910907505"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3177742428.6,
            "unit": "ns",
            "range": "± 890545.3112586692"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013784898.8,
            "unit": "ns",
            "range": "± 503635.4640334614"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173571812.2,
            "unit": "ns",
            "range": "± 594016.4529313982"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3012821970.2,
            "unit": "ns",
            "range": "± 223495.6457242512"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3178863089.8,
            "unit": "ns",
            "range": "± 386497.8438700532"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013956151,
            "unit": "ns",
            "range": "± 286374.7823476548"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173646071,
            "unit": "ns",
            "range": "± 390720.8169543824"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013668194.7,
            "unit": "ns",
            "range": "± 421337.43642975757"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177012045,
            "unit": "ns",
            "range": "± 1110589.848205298"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20447,
            "unit": "ns",
            "range": "± 5382.732530973464"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 14436.777777777777,
            "unit": "ns",
            "range": "± 1139.8189305518856"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 15460.4,
            "unit": "ns",
            "range": "± 956.9730287619278"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 40316.625,
            "unit": "ns",
            "range": "± 4769.199182177833"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 21671.444444444445,
            "unit": "ns",
            "range": "± 688.1818638832164"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 4695.2,
            "unit": "ns",
            "range": "± 454.0055799950775"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 36396.77777777778,
            "unit": "ns",
            "range": "± 7803.202512074414"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 54966,
            "unit": "ns",
            "range": "± 9691.118214633438"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 66066.66666666667,
            "unit": "ns",
            "range": "± 1372.0922891700834"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 412.44444444444446,
            "unit": "ns",
            "range": "± 14.000992028344912"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 349.875,
            "unit": "ns",
            "range": "± 13.579790656917885"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 428.6666666666667,
            "unit": "ns",
            "range": "± 14.9248115565993"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 356.3,
            "unit": "ns",
            "range": "± 20.72599012512229"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1790.5,
            "unit": "ns",
            "range": "± 189.17775297910222"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1098.6,
            "unit": "ns",
            "range": "± 111.23968116938606"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1420.3,
            "unit": "ns",
            "range": "± 59.136471168156646"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 441.2,
            "unit": "ns",
            "range": "± 32.67278854201323"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 408.1111111111111,
            "unit": "ns",
            "range": "± 25.809128445399143"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 515.5,
            "unit": "ns",
            "range": "± 26.576036466819396"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 292.25,
            "unit": "ns",
            "range": "± 3.575711717366808"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 151.8,
            "unit": "ns",
            "range": "± 40.12425146411526"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 307.77777777777777,
            "unit": "ns",
            "range": "± 6.666666666666666"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2673.777777777778,
            "unit": "ns",
            "range": "± 63.509404377969446"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 2978.3333333333335,
            "unit": "ns",
            "range": "± 40.36087214122113"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1405.6,
            "unit": "ns",
            "range": "± 87.28300076316249"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 633.6,
            "unit": "ns",
            "range": "± 39.994999687460925"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2805.2,
            "unit": "ns",
            "range": "± 60.7381447051375"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 4087.5,
            "unit": "ns",
            "range": "± 115.59027256266468"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 75663.25,
            "unit": "ns",
            "range": "± 7948.833620637145"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 147773.66666666666,
            "unit": "ns",
            "range": "± 14041.247131220218"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 381.6,
            "unit": "ns",
            "range": "± 25.97199346304485"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 355.22222222222223,
            "unit": "ns",
            "range": "± 15.401118285515647"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 390.4,
            "unit": "ns",
            "range": "± 15.137884778117304"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 282.25,
            "unit": "ns",
            "range": "± 11.016221805008415"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1344.0555555555557,
            "unit": "ns",
            "range": "± 33.507876354340596"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1223.8,
            "unit": "ns",
            "range": "± 295.6483647097605"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1367.375,
            "unit": "ns",
            "range": "± 23.271303603979117"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 416,
            "unit": "ns",
            "range": "± 40.08671156935231"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 447.9,
            "unit": "ns",
            "range": "± 40.765453784083185"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 492.6,
            "unit": "ns",
            "range": "± 16.866798418457748"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 248.75,
            "unit": "ns",
            "range": "± 15.52647508520297"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 66.8,
            "unit": "ns",
            "range": "± 24.55289075535597"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 338.2,
            "unit": "ns",
            "range": "± 55.00060605726692"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2597.5,
            "unit": "ns",
            "range": "± 43.69401179414253"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 2998.2,
            "unit": "ns",
            "range": "± 67.74101006365674"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1556,
            "unit": "ns",
            "range": "± 19.817921182606415"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 764,
            "unit": "ns",
            "range": "± 148.45201244846766"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2966.2,
            "unit": "ns",
            "range": "± 78.61834674199429"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3958.9,
            "unit": "ns",
            "range": "± 222.74721596963275"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 71354,
            "unit": "ns",
            "range": "± 5887.103956955406"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 117301.22222222222,
            "unit": "ns",
            "range": "± 7620.8719609008285"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 463.8,
            "unit": "ns",
            "range": "± 11.898179132399491"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 353.3333333333333,
            "unit": "ns",
            "range": "± 13.095800853708793"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 433.5,
            "unit": "ns",
            "range": "± 7.0710678118654755"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 379.6,
            "unit": "ns",
            "range": "± 27.594081491025086"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1490.2,
            "unit": "ns",
            "range": "± 29.192084162967497"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1319.7777777777778,
            "unit": "ns",
            "range": "± 48.67693955503411"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 1434.2,
            "unit": "ns",
            "range": "± 86.70998917207996"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 489.1,
            "unit": "ns",
            "range": "± 31.013258813324054"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 501.6,
            "unit": "ns",
            "range": "± 23.195785057730735"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 483.2,
            "unit": "ns",
            "range": "± 31.867085073961675"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 240.1,
            "unit": "ns",
            "range": "± 16.775974884737202"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 131.9,
            "unit": "ns",
            "range": "± 11.985639555549616"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 293.375,
            "unit": "ns",
            "range": "± 19.76965568021572"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2717.777777777778,
            "unit": "ns",
            "range": "± 75.24589320650293"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3302,
            "unit": "ns",
            "range": "± 63.66654200935191"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1597.611111111111,
            "unit": "ns",
            "range": "± 61.67544982495962"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 766.875,
            "unit": "ns",
            "range": "± 14.095972069049676"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 2794.5,
            "unit": "ns",
            "range": "± 66.27001045506395"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3467.3,
            "unit": "ns",
            "range": "± 446.7748749525749"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 123909.44444444444,
            "unit": "ns",
            "range": "± 10532.849544533416"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 103225.875,
            "unit": "ns",
            "range": "± 5353.854791176167"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9ec0034a67927af8dc7156c3069b5665d4a85769",
          "message": "Merge pull request #134 from thomhurst/fix/complete-lock-release\n\nfix: Prevent batch pool race condition causing producer deadlock",
          "timestamp": "2026-01-28T10:03:06Z",
          "tree_id": "fff04d66b45c22ddd3a71b604875e332dd6d2a36",
          "url": "https://github.com/thomhurst/Dekaf/commit/9ec0034a67927af8dc7156c3069b5665d4a85769"
        },
        "date": 1769595223377,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateConsumeResult",
            "value": 137.97497794628143,
            "unit": "ns",
            "range": "± 3.027384025974134"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessKeyValue",
            "value": 125.42172603607177,
            "unit": "ns",
            "range": "± 1.1907068901689775"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.Create1000Results",
            "value": 144359.1987792969,
            "unit": "ns",
            "range": "± 1258.6027000156066"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumeResultBenchmarks.CreateAndAccessEagerBaseline",
            "value": 132.17356407642365,
            "unit": "ns",
            "range": "± 2.3623802239539984"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3018344645.5,
            "unit": "ns",
            "range": "± 919626.0136782053"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3176634675.6,
            "unit": "ns",
            "range": "± 1243246.0778077284"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 100)",
            "value": 3015167496.4,
            "unit": "ns",
            "range": "± 868789.062068751"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 100)",
            "value": 3181293500.25,
            "unit": "ns",
            "range": "± 433110.99992217164"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3014807752.5,
            "unit": "ns",
            "range": "± 920334.2027983639"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3173985078.6,
            "unit": "ns",
            "range": "± 1079253.5943531992"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 100, MessageSize: 1000)",
            "value": 3012976649,
            "unit": "ns",
            "range": "± 519893.931188212"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 100, MessageSize: 1000)",
            "value": 3178764295.6,
            "unit": "ns",
            "range": "± 300618.19084230415"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013718726.2,
            "unit": "ns",
            "range": "± 776542.3378742463"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3173440901.6,
            "unit": "ns",
            "range": "± 822573.6141199158"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 100)",
            "value": 3013018923.2,
            "unit": "ns",
            "range": "± 355652.7783072979"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 100)",
            "value": 3179259041.8,
            "unit": "ns",
            "range": "± 812004.0985455061"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014186158.8,
            "unit": "ns",
            "range": "± 927839.4638719567"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.ConsumeAll_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3173717946.25,
            "unit": "ns",
            "range": "± 141150.40189427967"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Dekaf(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013543511,
            "unit": "ns",
            "range": "± 183080.06932669287"
          },
          {
            "name": "Dekaf.Benchmarks.ConsumerBenchmarks.PollSingle_Confluent(MessageCount: 1000, MessageSize: 1000)",
            "value": 3177587724.75,
            "unit": "ns",
            "range": "± 1532449.4153201"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 1256783.904296875,
            "unit": "ns",
            "range": "± 85601.50697038697"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5430619.355902778,
            "unit": "ns",
            "range": "± 14746.335183202616"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 1218638.1243489583,
            "unit": "ns",
            "range": "± 42575.48445125267"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 5793495.222222222,
            "unit": "ns",
            "range": "± 10557.159908664551"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 100)",
            "value": 85395.34111328125,
            "unit": "ns",
            "range": "± 1275.4730215772151"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 100)",
            "value": 75342.47482910156,
            "unit": "ns",
            "range": "± 1255.3221454825127"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 100)",
            "value": 130873.45492553711,
            "unit": "ns",
            "range": "± 6007.924884772602"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 1099311.5934244792,
            "unit": "ns",
            "range": "± 876.2195659538281"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 5386565.36953125,
            "unit": "ns",
            "range": "± 4071.338337550199"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 4218301.536024306,
            "unit": "ns",
            "range": "± 58661.92972329487"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 7269066.26875,
            "unit": "ns",
            "range": "± 46759.9153492713"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 100, BatchSize: 1000)",
            "value": 913371.5546875,
            "unit": "ns",
            "range": "± 73978.35274090877"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 100, BatchSize: 1000)",
            "value": 772139.1287109375,
            "unit": "ns",
            "range": "± 28234.120133242803"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Confluent(MessageSize: 100, BatchSize: 1000)",
            "value": 1362733.185546875,
            "unit": "ns",
            "range": "± 14984.961862693606"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 1307627.0262586805,
            "unit": "ns",
            "range": "± 25881.618438113324"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 5395263.95,
            "unit": "ns",
            "range": "± 11186.408204242409"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 2752246.23359375,
            "unit": "ns",
            "range": "± 43200.02125230689"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 100)",
            "value": 6679944.4515625,
            "unit": "ns",
            "range": "± 19616.15240246652"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 100)",
            "value": 347010.770703125,
            "unit": "ns",
            "range": "± 9257.624427414521"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 100)",
            "value": 344375.82177734375,
            "unit": "ns",
            "range": "± 7460.2623128362775"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 1099626.828342014,
            "unit": "ns",
            "range": "± 407.4529298901991"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.SingleProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 5403738.4203125,
            "unit": "ns",
            "range": "± 7288.3311030572995"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 23753863.22265625,
            "unit": "ns",
            "range": "± 98649.9371795202"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.BatchProduce_Confluent(MessageSize: 1000, BatchSize: 1000)",
            "value": 11178510.53125,
            "unit": "ns",
            "range": "± 187279.1151981424"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf(MessageSize: 1000, BatchSize: 1000)",
            "value": 3472176.855902778,
            "unit": "ns",
            "range": "± 56346.10977730945"
          },
          {
            "name": "Dekaf.Benchmarks.ProducerBenchmarks.FireAndForget_Dekaf_Direct(MessageSize: 1000, BatchSize: 1000)",
            "value": 3347742.5921875,
            "unit": "ns",
            "range": "± 108733.39494648663"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandInt32s",
            "value": 20475.222222222223,
            "unit": "ns",
            "range": "± 5326.787370868528"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredStrings",
            "value": 12057.125,
            "unit": "ns",
            "range": "± 178.75477016612132"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteHundredCompactStrings",
            "value": 12785.875,
            "unit": "ns",
            "range": "± 148.76485520060558"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandInt32s",
            "value": 41432.666666666664,
            "unit": "ns",
            "range": "± 7021.278943326493"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteRecordBatch",
            "value": 16897.166666666668,
            "unit": "ns",
            "range": "± 319.71354366057125"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadRecordBatch",
            "value": 4360.722222222223,
            "unit": "ns",
            "range": "± 72.07249436813217"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.WriteThousandVarInts",
            "value": 36700.333333333336,
            "unit": "ns",
            "range": "± 7727.035476170663"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.ReadThousandVarInts",
            "value": 51933.22222222222,
            "unit": "ns",
            "range": "± 9179.055040931198"
          },
          {
            "name": "Dekaf.Benchmarks.MemoryBenchmarks.PartitionBatchAppendRecords",
            "value": 55122.75,
            "unit": "ns",
            "range": "± 587.8857396273045"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 10)",
            "value": 397.3,
            "unit": "ns",
            "range": "± 47.22299345775436"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 10)",
            "value": 615.4444444444445,
            "unit": "ns",
            "range": "± 165.00159931884835"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 10)",
            "value": 724.9,
            "unit": "ns",
            "range": "± 192.55905299125484"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 10)",
            "value": 366.77777777777777,
            "unit": "ns",
            "range": "± 36.37917597258691"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 10)",
            "value": 1437.5,
            "unit": "ns",
            "range": "± 275.50045372013454"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 10)",
            "value": 1253.8,
            "unit": "ns",
            "range": "± 87.67972019420074"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 10)",
            "value": 1490.5,
            "unit": "ns",
            "range": "± 104.68166028488467"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 10)",
            "value": 473.1666666666667,
            "unit": "ns",
            "range": "± 33.94849039353591"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 10)",
            "value": 729.9,
            "unit": "ns",
            "range": "± 202.48481424541447"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 10)",
            "value": 431.22222222222223,
            "unit": "ns",
            "range": "± 12.152960316089429"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 10)",
            "value": 230.1,
            "unit": "ns",
            "range": "± 47.82247263461906"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 10)",
            "value": 292.2,
            "unit": "ns",
            "range": "± 194.64885763286097"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 10)",
            "value": 612.1666666666666,
            "unit": "ns",
            "range": "± 165.77846663544693"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 10)",
            "value": 2672.375,
            "unit": "ns",
            "range": "± 61.19742641647604"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 10)",
            "value": 3045.722222222222,
            "unit": "ns",
            "range": "± 118.10035751192477"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 10)",
            "value": 1415.25,
            "unit": "ns",
            "range": "± 58.48259814435646"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 10)",
            "value": 789.7777777777778,
            "unit": "ns",
            "range": "± 97.21596805280728"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 10)",
            "value": 2921.6666666666665,
            "unit": "ns",
            "range": "± 62.72160712226688"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 10)",
            "value": 3473.9,
            "unit": "ns",
            "range": "± 283.07456811079146"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 10)",
            "value": 72194.11111111111,
            "unit": "ns",
            "range": "± 5966.729808790667"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 10)",
            "value": 100476.88888888889,
            "unit": "ns",
            "range": "± 5726.425050684861"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 100)",
            "value": 476.1,
            "unit": "ns",
            "range": "± 64.92080645764585"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 100)",
            "value": 476.9,
            "unit": "ns",
            "range": "± 150.78844635964506"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 100)",
            "value": 387.75,
            "unit": "ns",
            "range": "± 9.254342609978147"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 100)",
            "value": 355.6666666666667,
            "unit": "ns",
            "range": "± 46.18711941656461"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 100)",
            "value": 1347.375,
            "unit": "ns",
            "range": "± 41.43993070871206"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 100)",
            "value": 1551.5,
            "unit": "ns",
            "range": "± 365.4808856531047"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 100)",
            "value": 1371.1,
            "unit": "ns",
            "range": "± 79.14466360897478"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 100)",
            "value": 539.6,
            "unit": "ns",
            "range": "± 175.78534890282776"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 100)",
            "value": 487.5,
            "unit": "ns",
            "range": "± 18.648056198971517"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 100)",
            "value": 512.1,
            "unit": "ns",
            "range": "± 13.582668695395942"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 100)",
            "value": 280.1,
            "unit": "ns",
            "range": "± 54.79345054130304"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 100)",
            "value": 243,
            "unit": "ns",
            "range": "± 164.45330306469643"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 100)",
            "value": 286.8888888888889,
            "unit": "ns",
            "range": "± 35.73319340768624"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 100)",
            "value": 2951.1,
            "unit": "ns",
            "range": "± 342.7896828474665"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 100)",
            "value": 3116.1111111111113,
            "unit": "ns",
            "range": "± 247.13429772314305"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 100)",
            "value": 1540.375,
            "unit": "ns",
            "range": "± 77.17963739595716"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 100)",
            "value": 723.1666666666666,
            "unit": "ns",
            "range": "± 105.4407416514129"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 100)",
            "value": 2947.5,
            "unit": "ns",
            "range": "± 254.11808278829744"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 100)",
            "value": 3319.5,
            "unit": "ns",
            "range": "± 88.01298605481873"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 100)",
            "value": 96080.77777777778,
            "unit": "ns",
            "range": "± 16867.95906280438"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 100)",
            "value": 110605.66666666667,
            "unit": "ns",
            "range": "± 9505.441691473363"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Dekaf(StringLength: 1000)",
            "value": 483.72222222222223,
            "unit": "ns",
            "range": "± 51.715031126786"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt32_Baseline(StringLength: 1000)",
            "value": 394.8,
            "unit": "ns",
            "range": "± 102.18615909755641"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Dekaf(StringLength: 1000)",
            "value": 591.6,
            "unit": "ns",
            "range": "± 204.51579672756603"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteInt64_Baseline(StringLength: 1000)",
            "value": 377.1,
            "unit": "ns",
            "range": "± 103.85722036633862"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Dekaf(StringLength: 1000)",
            "value": 1403.8333333333333,
            "unit": "ns",
            "range": "± 79.7072769576279"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteString_Baseline(StringLength: 1000)",
            "value": 1774.7777777777778,
            "unit": "ns",
            "range": "± 153.32137634538913"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteCompactString_Dekaf(StringLength: 1000)",
            "value": 2062.625,
            "unit": "ns",
            "range": "± 77.53052394287776"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntSmall_Dekaf(StringLength: 1000)",
            "value": 503.55555555555554,
            "unit": "ns",
            "range": "± 31.080987400302742"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntLarge_Dekaf(StringLength: 1000)",
            "value": 457.875,
            "unit": "ns",
            "range": "± 60.90610454977868"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.WriteVarIntNegative_Dekaf(StringLength: 1000)",
            "value": 908.9,
            "unit": "ns",
            "range": "± 204.18357753093989"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Dekaf(StringLength: 1000)",
            "value": 399.9,
            "unit": "ns",
            "range": "± 302.2361843180117"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt32_Baseline(StringLength: 1000)",
            "value": 206,
            "unit": "ns",
            "range": "± 87.08233651742087"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadInt64_Dekaf(StringLength: 1000)",
            "value": 316.72222222222223,
            "unit": "ns",
            "range": "± 56.59897918199977"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadString_Dekaf(StringLength: 1000)",
            "value": 2810.5,
            "unit": "ns",
            "range": "± 80.3563492024299"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.ReadFullSequence_Dekaf(StringLength: 1000)",
            "value": 3263.5555555555557,
            "unit": "ns",
            "range": "± 218.83504695952558"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeString_Dekaf(StringLength: 1000)",
            "value": 1769.6666666666667,
            "unit": "ns",
            "range": "± 101.73494974687902"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializeInt32_Dekaf(StringLength: 1000)",
            "value": 932.6,
            "unit": "ns",
            "range": "± 220.69357841939026"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeString_Dekaf(StringLength: 1000)",
            "value": 3122.1111111111113,
            "unit": "ns",
            "range": "± 305.37126765809376"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.DeserializeInt32_Dekaf(StringLength: 1000)",
            "value": 3662.9,
            "unit": "ns",
            "range": "± 471.14687495278764"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_NewInstance(StringLength: 1000)",
            "value": 72643.55555555556,
            "unit": "ns",
            "range": "± 6766.345932464418"
          },
          {
            "name": "Dekaf.Benchmarks.SerializationBenchmarks.SerializationContext_ThreadStaticReuse(StringLength: 1000)",
            "value": 111241.88888888889,
            "unit": "ns",
            "range": "± 10810.841519563179"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "7bbd6786f06b8bda3f00bc0f00b0ad99706e903d",
          "message": "refactor: Redesign benchmark structure for better organization\n\n- Reorganize benchmarks into Client/ (require Kafka) and Unit/ (no Kafka)\n- Use consistent naming: Confluent_* and Dekaf_* prefix pattern\n- Mark Confluent benchmarks as Baseline for ratio comparison\n- Simplify workflow from 5 jobs to 2 (unit-benchmarks, client-benchmarks)\n- Remove internal micro-benchmarks (Accumulator, TopicPartitionCache, ConsumeResult)\n- Update summary output to show Dekaf vs Confluent comparison with ratios",
          "timestamp": "2026-01-28T11:21:14Z",
          "tree_id": "5a9f2ed0bd110a4604bb200b1187072b963f0c01",
          "url": "https://github.com/thomhurst/Dekaf/commit/7bbd6786f06b8bda3f00bc0f00b0ad99706e903d"
        },
        "date": 1769599374856,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteInt32_Thousand",
            "value": 20547.88888888889,
            "unit": "ns",
            "range": "± 5320.093125229211"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteString_Hundred",
            "value": 13356,
            "unit": "ns",
            "range": "± 352.51863622919126"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteCompactString_Hundred",
            "value": 10960.125,
            "unit": "ns",
            "range": "± 51.013828657391656"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteVarInt_Thousand",
            "value": 42739.333333333336,
            "unit": "ns",
            "range": "± 7147.076342813193"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadInt32_Thousand",
            "value": 14823.888888888889,
            "unit": "ns",
            "range": "± 5780.534651838972"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadVarInt_Thousand",
            "value": 24708.222222222223,
            "unit": "ns",
            "range": "± 5454.72450674133"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteRecordBatch",
            "value": 20975.7,
            "unit": "ns",
            "range": "± 1213.720821825733"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadRecordBatch",
            "value": 5374.5,
            "unit": "ns",
            "range": "± 346.1686727593934"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Small",
            "value": 1226.111111111111,
            "unit": "ns",
            "range": "± 29.400018896441395"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Medium",
            "value": 1573.5,
            "unit": "ns",
            "range": "± 81.43334015554618"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Large",
            "value": 1510,
            "unit": "ns",
            "range": "± 29.597297173897484"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.DeserializeString",
            "value": 3507.8333333333335,
            "unit": "ns",
            "range": "± 166.1873942271194"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeInt32",
            "value": 603.9,
            "unit": "ns",
            "range": "± 25.026430473046336"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeBatch",
            "value": 45993.666666666664,
            "unit": "ns",
            "range": "± 4768.907133715229"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_ArrayBufferWriter",
            "value": 3935.1,
            "unit": "ns",
            "range": "± 190.35606986207014"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_PooledBufferWriter",
            "value": 3244.7,
            "unit": "ns",
            "range": "± 139.63050446724662"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1KB",
            "value": 10255.111111111111,
            "unit": "ns",
            "range": "± 159.36941711354507"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1MB",
            "value": 823951.8,
            "unit": "ns",
            "range": "± 15065.852225774977"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1KB",
            "value": 7934.625,
            "unit": "ns",
            "range": "± 126.40178287621465"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1MB",
            "value": 1109058.6,
            "unit": "ns",
            "range": "± 21129.40350659347"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "f3c09a5c854a6be6607f2148e68ba67e31e23d92",
          "message": "fix: Use BenchmarkCategory to allow multiple baselines per class\n\nBenchmarkDotNet only allows one baseline per benchmark group. Use\n[BenchmarkCategory] and [GroupBenchmarksBy(ByCategory)] to separate\ndifferent operation types, enabling Dekaf vs Confluent ratio comparison\nfor each operation category independently.",
          "timestamp": "2026-01-28T11:27:26Z",
          "tree_id": "7cfbad664c42da6267c98d59dde3dd325e08e45c",
          "url": "https://github.com/thomhurst/Dekaf/commit/f3c09a5c854a6be6607f2148e68ba67e31e23d92"
        },
        "date": 1769600541946,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteInt32_Thousand",
            "value": 20668.222222222223,
            "unit": "ns",
            "range": "± 5456.441944568314"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteString_Hundred",
            "value": 10535.555555555555,
            "unit": "ns",
            "range": "± 247.2666127437705"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteCompactString_Hundred",
            "value": 11169.111111111111,
            "unit": "ns",
            "range": "± 212.5902422763357"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteVarInt_Thousand",
            "value": 35070.555555555555,
            "unit": "ns",
            "range": "± 8251.911477214098"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadInt32_Thousand",
            "value": 21954,
            "unit": "ns",
            "range": "± 6847.130968515207"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadVarInt_Thousand",
            "value": 32595.777777777777,
            "unit": "ns",
            "range": "± 7411.343582269306"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteRecordBatch",
            "value": 17878.222222222223,
            "unit": "ns",
            "range": "± 175.97711341093319"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadRecordBatch",
            "value": 5187.888888888889,
            "unit": "ns",
            "range": "± 278.0833707921261"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Small",
            "value": 1498.4,
            "unit": "ns",
            "range": "± 30.645644968829675"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Medium",
            "value": 1552.6,
            "unit": "ns",
            "range": "± 74.58358472955769"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Large",
            "value": 1608.5,
            "unit": "ns",
            "range": "± 39.33121463220332"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.DeserializeString",
            "value": 3490.6,
            "unit": "ns",
            "range": "± 136.65564996247562"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeInt32",
            "value": 750.875,
            "unit": "ns",
            "range": "± 20.635873756986538"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeBatch",
            "value": 33877.8,
            "unit": "ns",
            "range": "± 816.2623216698036"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_ArrayBufferWriter",
            "value": 3642.25,
            "unit": "ns",
            "range": "± 77.92074728301094"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_PooledBufferWriter",
            "value": 3391.3333333333335,
            "unit": "ns",
            "range": "± 21.93171219946131"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1KB",
            "value": 10222.25,
            "unit": "ns",
            "range": "± 152.54015864682978"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1MB",
            "value": 661203.6,
            "unit": "ns",
            "range": "± 172368.71810385628"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1KB",
            "value": 7395.6,
            "unit": "ns",
            "range": "± 761.9925925565885"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1MB",
            "value": 1126378.3,
            "unit": "ns",
            "range": "± 22860.953703494815"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 5957676.74921875,
            "unit": "ns",
            "range": "± 43874.47443927097"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 1299820.8383246528,
            "unit": "ns",
            "range": "± 38401.22217618928"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 7263539.0390625,
            "unit": "ns",
            "range": "± 80241.10502450712"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 4148902.55546875,
            "unit": "ns",
            "range": "± 52525.50131009685"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 6673017.228732639,
            "unit": "ns",
            "range": "± 20815.903611675727"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 2729311.891796875,
            "unit": "ns",
            "range": "± 18569.783771811322"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 11914895.158203125,
            "unit": "ns",
            "range": "± 459718.361239411"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 23379729.95,
            "unit": "ns",
            "range": "± 167365.59523197386"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_FireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 129380.14066569011,
            "unit": "ns",
            "range": "± 8937.253165387177"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 76865.16661241319,
            "unit": "ns",
            "range": "± 2247.1928364873743"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1330189.655078125,
            "unit": "ns",
            "range": "± 11437.691498632608"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 794887.653515625,
            "unit": "ns",
            "range": "± 83866.01142862922"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 331722.2714301215,
            "unit": "ns",
            "range": "± 9843.426486564204"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 3251882.53515625,
            "unit": "ns",
            "range": "± 101446.77353854895"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 5380589.314453125,
            "unit": "ns",
            "range": "± 1006.3394602721718"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 1304676.127734375,
            "unit": "ns",
            "range": "± 15922.700524562852"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 5378433.423828125,
            "unit": "ns",
            "range": "± 1110.9306791717065"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 1185603.3470703126,
            "unit": "ns",
            "range": "± 21081.590190429506"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 5380497.70390625,
            "unit": "ns",
            "range": "± 3636.6445484120695"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 1097761.9904296875,
            "unit": "ns",
            "range": "± 618.0810823876251"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 5380415.5390625,
            "unit": "ns",
            "range": "± 2541.3500493057923"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 1097568.064236111,
            "unit": "ns",
            "range": "± 668.0719646108141"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3176698351.2,
            "unit": "ns",
            "range": "± 1056299.146567013"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3014576369.6,
            "unit": "ns",
            "range": "± 796191.8554728503"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3174097671,
            "unit": "ns",
            "range": "± 696336.02390728"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3015102989.2,
            "unit": "ns",
            "range": "± 1053577.442219223"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3176048067,
            "unit": "ns",
            "range": "± 521110.3643360972"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3014630460.2,
            "unit": "ns",
            "range": "± 526300.8634010588"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3176099339,
            "unit": "ns",
            "range": "± 393616.3819043867"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015455338.6,
            "unit": "ns",
            "range": "± 527209.7109337043"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3179604796.8,
            "unit": "ns",
            "range": "± 663559.8206919252"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3013751048.8,
            "unit": "ns",
            "range": "± 471605.70429141336"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3178586825.6,
            "unit": "ns",
            "range": "± 276983.64075843897"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3013100302.8,
            "unit": "ns",
            "range": "± 648036.4476209652"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3178512926.4,
            "unit": "ns",
            "range": "± 903960.7393760527"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3013091964,
            "unit": "ns",
            "range": "± 883195.6480721019"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178269977,
            "unit": "ns",
            "range": "± 528361.2423687982"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013522686.4,
            "unit": "ns",
            "range": "± 403284.62763264356"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "c913c7715a820b4a76b0b88245aec37c17984db8",
          "message": "improve: Use BenchmarkDotNet markdown reports in workflow summary\n\nInstead of re-processing JSON data, use the pre-formatted markdown\ntables generated by BenchmarkDotNet. These have proper Ratio columns,\nerror bars, and memory allocation ratios already calculated.",
          "timestamp": "2026-01-28T11:47:11Z",
          "tree_id": "44b1ae1298b8ecc3d33bdddf4091e1553893839d",
          "url": "https://github.com/thomhurst/Dekaf/commit/c913c7715a820b4a76b0b88245aec37c17984db8"
        },
        "date": 1769601734617,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteInt32_Thousand",
            "value": 24080.222222222223,
            "unit": "ns",
            "range": "± 5130.706427427362"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteString_Hundred",
            "value": 14625.777777777777,
            "unit": "ns",
            "range": "± 1444.8644207829484"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteCompactString_Hundred",
            "value": 11398.5,
            "unit": "ns",
            "range": "± 387.9316950185947"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteVarInt_Thousand",
            "value": 34772.333333333336,
            "unit": "ns",
            "range": "± 7809.6962328889595"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadInt32_Thousand",
            "value": 18180.222222222223,
            "unit": "ns",
            "range": "± 5210.271460724905"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadVarInt_Thousand",
            "value": 35946.77777777778,
            "unit": "ns",
            "range": "± 6812.730725960365"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteRecordBatch",
            "value": 17412.222222222223,
            "unit": "ns",
            "range": "± 552.2417445688477"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadRecordBatch",
            "value": 5648.833333333333,
            "unit": "ns",
            "range": "± 214.17341104815043"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Small",
            "value": 1402.2,
            "unit": "ns",
            "range": "± 49.66062603086854"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Medium",
            "value": 2017.8,
            "unit": "ns",
            "range": "± 115.64317724986824"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Large",
            "value": 1777.1666666666667,
            "unit": "ns",
            "range": "± 27.85228895441091"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.DeserializeString",
            "value": 2895,
            "unit": "ns",
            "range": "± 39.68626966596886"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeInt32",
            "value": 633.3,
            "unit": "ns",
            "range": "± 55.35702304134499"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeBatch",
            "value": 36858.875,
            "unit": "ns",
            "range": "± 1747.7170363893904"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_ArrayBufferWriter",
            "value": 4701.2,
            "unit": "ns",
            "range": "± 220.63030919013218"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_PooledBufferWriter",
            "value": 3356.4,
            "unit": "ns",
            "range": "± 82.26035902344863"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1KB",
            "value": 12617.444444444445,
            "unit": "ns",
            "range": "± 1795.384715813794"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1MB",
            "value": 504222.3,
            "unit": "ns",
            "range": "± 17688.364418139085"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1KB",
            "value": 6736.555555555556,
            "unit": "ns",
            "range": "± 390.3284229693987"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1MB",
            "value": 1165336.5555555555,
            "unit": "ns",
            "range": "± 37772.61106314704"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 5973747.6984375,
            "unit": "ns",
            "range": "± 41834.9244990643"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 1187686.4791666667,
            "unit": "ns",
            "range": "± 55126.9605627475"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 7270120.321180556,
            "unit": "ns",
            "range": "± 52026.03804212818"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 4232403.154513889,
            "unit": "ns",
            "range": "± 35847.6212532915"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 6695841.0107421875,
            "unit": "ns",
            "range": "± 11367.580006219667"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 2772328.4625,
            "unit": "ns",
            "range": "± 16647.63858046541"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 11687183.5328125,
            "unit": "ns",
            "range": "± 126191.77780209541"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 23742267.854166668,
            "unit": "ns",
            "range": "± 61062.307731465466"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 74611.30285644531,
            "unit": "ns",
            "range": "± 1816.3683527626129"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1331800.2864583333,
            "unit": "ns",
            "range": "± 7300.672471602126"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 759574.9271484375,
            "unit": "ns",
            "range": "± 17118.789127806158"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 331922.9832763672,
            "unit": "ns",
            "range": "± 9227.116320676922"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 3322157.54609375,
            "unit": "ns",
            "range": "± 90816.03822918792"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 5385320.72421875,
            "unit": "ns",
            "range": "± 750.1609150455082"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 1098647.8426649305,
            "unit": "ns",
            "range": "± 242.31159101824133"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 5382175.40078125,
            "unit": "ns",
            "range": "± 2548.9898305080906"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 1098913.166015625,
            "unit": "ns",
            "range": "± 422.8599029986685"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 5385836.49296875,
            "unit": "ns",
            "range": "± 2358.1880916345362"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 1102199.5866699219,
            "unit": "ns",
            "range": "± 3826.2370981108575"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 5389268.96640625,
            "unit": "ns",
            "range": "± 3246.0720311293953"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 1277179.067578125,
            "unit": "ns",
            "range": "± 13647.996221562627"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3177883965.2,
            "unit": "ns",
            "range": "± 836903.9072600867"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3015002689,
            "unit": "ns",
            "range": "± 971058.1971625079"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3174755572.4,
            "unit": "ns",
            "range": "± 1230584.5792328946"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3014518642.25,
            "unit": "ns",
            "range": "± 234344.07689318"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3174881024,
            "unit": "ns",
            "range": "± 431264.3321861895"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3014199852.4,
            "unit": "ns",
            "range": "± 577670.2447078264"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3175360233.8,
            "unit": "ns",
            "range": "± 482690.1371653455"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015445620.4,
            "unit": "ns",
            "range": "± 438772.16524273733"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3178638865,
            "unit": "ns",
            "range": "± 327491.3494603076"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3013108136.75,
            "unit": "ns",
            "range": "± 210142.84440569626"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3178228826.6,
            "unit": "ns",
            "range": "± 573559.6853473927"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3013083151,
            "unit": "ns",
            "range": "± 768240.8674592234"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3178540573.4,
            "unit": "ns",
            "range": "± 261009.5244149148"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3012928032.5,
            "unit": "ns",
            "range": "± 468649.1473455026"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178570828.4,
            "unit": "ns",
            "range": "± 968054.4735028086"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013582407.25,
            "unit": "ns",
            "range": "± 237668.01328937107"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "2cbfdc9600de73c107e4215be7e8936b74144f76",
          "message": "feat: Add stress test project for sustained throughput comparison\n\nAdd Dekaf.StressTests console app that runs 15-minute throughput tests\ncomparing Dekaf vs Confluent.Kafka for both producing and consuming.\nIncludes metrics collection (throughput, latency percentiles, GC stats),\nmarkdown reporting, and GitHub Actions workflow for weekly scheduled runs.",
          "timestamp": "2026-01-28T14:02:43Z",
          "tree_id": "f6aedc12d7c4b67d61bcef2aca862463fc6aee5a",
          "url": "https://github.com/thomhurst/Dekaf/commit/2cbfdc9600de73c107e4215be7e8936b74144f76"
        },
        "date": 1769609880754,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteInt32_Thousand",
            "value": 21450.555555555555,
            "unit": "ns",
            "range": "± 6976.674478415757"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteString_Hundred",
            "value": 11690.9,
            "unit": "ns",
            "range": "± 1352.3344589593542"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteCompactString_Hundred",
            "value": 14064.625,
            "unit": "ns",
            "range": "± 938.189584480968"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteVarInt_Thousand",
            "value": 35500,
            "unit": "ns",
            "range": "± 6930.419197278041"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadInt32_Thousand",
            "value": 22796.444444444445,
            "unit": "ns",
            "range": "± 6855.787666474055"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadVarInt_Thousand",
            "value": 24309.75,
            "unit": "ns",
            "range": "± 5506.6488058917075"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteRecordBatch",
            "value": 21247.7,
            "unit": "ns",
            "range": "± 1151.5994481107089"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadRecordBatch",
            "value": 4371.5,
            "unit": "ns",
            "range": "± 98.51522724939531"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Small",
            "value": 1223.75,
            "unit": "ns",
            "range": "± 91.09453488390116"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Medium",
            "value": 1698,
            "unit": "ns",
            "range": "± 64.34283176858165"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Large",
            "value": 1782.9,
            "unit": "ns",
            "range": "± 218.90254046543686"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.DeserializeString",
            "value": 3306.8888888888887,
            "unit": "ns",
            "range": "± 221.25632445449128"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeInt32",
            "value": 746.6666666666666,
            "unit": "ns",
            "range": "± 105.04284840006959"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeBatch",
            "value": 42762.375,
            "unit": "ns",
            "range": "± 3712.439222686731"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_ArrayBufferWriter",
            "value": 4412.3,
            "unit": "ns",
            "range": "± 178.57696753314335"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_PooledBufferWriter",
            "value": 3751.9,
            "unit": "ns",
            "range": "± 130.00893131713"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1KB",
            "value": 10522.125,
            "unit": "ns",
            "range": "± 355.9205117920091"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1MB",
            "value": 454446.55555555556,
            "unit": "ns",
            "range": "± 13318.28464096551"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1KB",
            "value": 7163.777777777777,
            "unit": "ns",
            "range": "± 601.6981755369086"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1MB",
            "value": 1771721.7,
            "unit": "ns",
            "range": "± 33374.02625395983"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 6010229.345920139,
            "unit": "ns",
            "range": "± 48570.09818533492"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 1200358.05078125,
            "unit": "ns",
            "range": "± 58771.93010668917"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 7287347.6265625,
            "unit": "ns",
            "range": "± 53159.15227333082"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 4313091.184375,
            "unit": "ns",
            "range": "± 46599.01011234133"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 6645678.90703125,
            "unit": "ns",
            "range": "± 44696.50513115013"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 2779419.961328125,
            "unit": "ns",
            "range": "± 14678.951683118408"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 11787007.540625,
            "unit": "ns",
            "range": "± 118821.9463190101"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 24483716.78125,
            "unit": "ns",
            "range": "± 163495.2343322416"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 76655.63862304688,
            "unit": "ns",
            "range": "± 2704.8857214481495"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1336007.1944444445,
            "unit": "ns",
            "range": "± 14845.569052456269"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 824368.396875,
            "unit": "ns",
            "range": "± 45960.922241225366"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 341851.1511501736,
            "unit": "ns",
            "range": "± 9617.871807750367"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 3319484.9765625,
            "unit": "ns",
            "range": "± 54435.11836440343"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 5413138.80859375,
            "unit": "ns",
            "range": "± 16434.087870823827"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 1291597.807421875,
            "unit": "ns",
            "range": "± 28679.37477658604"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 5395666.347222222,
            "unit": "ns",
            "range": "± 10736.97626865796"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 1311321.3267144097,
            "unit": "ns",
            "range": "± 29608.697332298863"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 5395014.661458333,
            "unit": "ns",
            "range": "± 3496.9475113112358"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 1335708.1342230903,
            "unit": "ns",
            "range": "± 18051.871095584636"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 5396919.725,
            "unit": "ns",
            "range": "± 5182.782211660265"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 1346626.948828125,
            "unit": "ns",
            "range": "± 11987.849143650421"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3178018990.6,
            "unit": "ns",
            "range": "± 1723924.6825491826"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3015791580.4,
            "unit": "ns",
            "range": "± 563783.0082840383"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3175194691.2,
            "unit": "ns",
            "range": "± 817401.8342640295"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3015359324.9,
            "unit": "ns",
            "range": "± 877935.6819214036"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3175306732.8,
            "unit": "ns",
            "range": "± 586792.7496643938"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3014187299.8,
            "unit": "ns",
            "range": "± 786700.380389955"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3175850174.4,
            "unit": "ns",
            "range": "± 1211789.3054812788"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015046375,
            "unit": "ns",
            "range": "± 471881.6635107436"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3179211005.75,
            "unit": "ns",
            "range": "± 175264.49457391535"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3013645325.8,
            "unit": "ns",
            "range": "± 711819.2726828208"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3179555723.2,
            "unit": "ns",
            "range": "± 771484.6727114544"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3013649493.75,
            "unit": "ns",
            "range": "± 349025.0168630467"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3180271628.6,
            "unit": "ns",
            "range": "± 940105.5933302386"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3013626490.1,
            "unit": "ns",
            "range": "± 691782.1710649241"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3179850301.25,
            "unit": "ns",
            "range": "± 1388789.0610608403"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3014361161.7,
            "unit": "ns",
            "range": "± 296581.7051668899"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "committer": {
            "email": "30480171+thomhurst@users.noreply.github.com",
            "name": "Tom Longhurst",
            "username": "thomhurst"
          },
          "distinct": true,
          "id": "0b8caf9ee0ed174e92b625cec159618bf0fd7f13",
          "message": "fix: Correct Kafka health check path and reduce consumer seeding\n\n- Use /opt/kafka/bin/ path for apache/kafka image scripts\n- Reduce consumer seeding from 45M to 500K messages to avoid timeout",
          "timestamp": "2026-01-28T14:34:22Z",
          "tree_id": "4b125e6a84bf34627b2bd7c1c02a6e79c4ed753c",
          "url": "https://github.com/thomhurst/Dekaf/commit/0b8caf9ee0ed174e92b625cec159618bf0fd7f13"
        },
        "date": 1769611890324,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteInt32_Thousand",
            "value": 21946.166666666668,
            "unit": "ns",
            "range": "± 7603.278289659007"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteString_Hundred",
            "value": 12961.944444444445,
            "unit": "ns",
            "range": "± 231.75046446075956"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteCompactString_Hundred",
            "value": 11016.055555555555,
            "unit": "ns",
            "range": "± 72.99162813486063"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteVarInt_Thousand",
            "value": 39432.875,
            "unit": "ns",
            "range": "± 7518.93044136883"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadInt32_Thousand",
            "value": 14824.777777777777,
            "unit": "ns",
            "range": "± 5620.473596098859"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadVarInt_Thousand",
            "value": 26493.333333333332,
            "unit": "ns",
            "range": "± 4571.556737042645"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.WriteRecordBatch",
            "value": 20530.11111111111,
            "unit": "ns",
            "range": "± 1027.2565945814665"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.ProtocolBenchmarks.ReadRecordBatch",
            "value": 4528.9,
            "unit": "ns",
            "range": "± 368.1903040548461"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Small",
            "value": 1636.5,
            "unit": "ns",
            "range": "± 228.59048779665156"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Medium",
            "value": 1578,
            "unit": "ns",
            "range": "± 111.65795985956397"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeString_Large",
            "value": 1495.125,
            "unit": "ns",
            "range": "± 16.58689241539837"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.DeserializeString",
            "value": 3460.5,
            "unit": "ns",
            "range": "± 100.03856399266378"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeInt32",
            "value": 638.9,
            "unit": "ns",
            "range": "± 82.25765752938885"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.SerializeBatch",
            "value": 34095.5,
            "unit": "ns",
            "range": "± 1685.6266321036644"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_ArrayBufferWriter",
            "value": 4577,
            "unit": "ns",
            "range": "± 273.26055941780794"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.SerializerBenchmarks.Serialize_PooledBufferWriter",
            "value": 3437.3,
            "unit": "ns",
            "range": "± 148.92414176351664"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1KB",
            "value": 10561.9,
            "unit": "ns",
            "range": "± 230.55221148846573"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Compress_1MB",
            "value": 461897.375,
            "unit": "ns",
            "range": "± 14509.456797329507"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1KB",
            "value": 6802,
            "unit": "ns",
            "range": "± 276.95125924970984"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Unit.CompressionBenchmarks.Snappy_Decompress_1MB",
            "value": 1108196.2,
            "unit": "ns",
            "range": "± 22610.008074351106"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 5970804.82265625,
            "unit": "ns",
            "range": "± 75292.1119634382"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 100)",
            "value": 1146211.8559027778,
            "unit": "ns",
            "range": "± 6274.8901251374855"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 7220753.978125,
            "unit": "ns",
            "range": "± 76738.94998905201"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 100, BatchSize: 1000)",
            "value": 3519449.203125,
            "unit": "ns",
            "range": "± 66863.09959135363"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 6556855.52890625,
            "unit": "ns",
            "range": "± 33852.0266088688"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 100)",
            "value": 2167473.523828125,
            "unit": "ns",
            "range": "± 19811.682323051988"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 8535366.168402778,
            "unit": "ns",
            "range": "± 114808.12275115846"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceBatch(MessageSize: 1000, BatchSize: 1000)",
            "value": 18687158.211805556,
            "unit": "ns",
            "range": "± 69761.44154883924"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_FireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 126450.06703016494,
            "unit": "ns",
            "range": "± 3862.27650897084"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 100)",
            "value": 66774.28944227431,
            "unit": "ns",
            "range": "± 2604.714523161454"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 1251923.9880859375,
            "unit": "ns",
            "range": "± 20853.561468909964"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 100, BatchSize: 1000)",
            "value": 680830.3736328125,
            "unit": "ns",
            "range": "± 15711.488066655871"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 100)",
            "value": 257714.2904785156,
            "unit": "ns",
            "range": "± 7277.076607238192"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_FireAndForget(MessageSize: 1000, BatchSize: 1000)",
            "value": 2540486.228515625,
            "unit": "ns",
            "range": "± 37629.082677983286"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 5445260.31171875,
            "unit": "ns",
            "range": "± 13517.975194415443"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 100)",
            "value": 1099287.4697265625,
            "unit": "ns",
            "range": "± 1048.7872036841504"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 5424686.303385417,
            "unit": "ns",
            "range": "± 25356.228687499286"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 100, BatchSize: 1000)",
            "value": 1110541.7529296875,
            "unit": "ns",
            "range": "± 9771.487655755485"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 5425037.8408203125,
            "unit": "ns",
            "range": "± 13205.361304745757"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 100)",
            "value": 1179716.751736111,
            "unit": "ns",
            "range": "± 24262.857650753158"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Confluent_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 5441986.39453125,
            "unit": "ns",
            "range": "± 12363.451086255713"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks.Dekaf_ProduceSingle(MessageSize: 1000, BatchSize: 1000)",
            "value": 1247259.4931640625,
            "unit": "ns",
            "range": "± 16422.51146182341"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3177885322.5,
            "unit": "ns",
            "range": "± 377725.2741843821"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 100)",
            "value": 3015829125.7,
            "unit": "ns",
            "range": "± 419958.70012085716"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3174951338.2,
            "unit": "ns",
            "range": "± 936304.9090220022"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 100, MessageSize: 1000)",
            "value": 3014912528.3,
            "unit": "ns",
            "range": "± 671518.8811256166"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3175346464.3,
            "unit": "ns",
            "range": "± 308088.56400506006"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 100)",
            "value": 3015089536.1,
            "unit": "ns",
            "range": "± 363234.8496018795"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3174880867.8,
            "unit": "ns",
            "range": "± 1343622.3965641165"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_ConsumeAll(MessageCount: 1000, MessageSize: 1000)",
            "value": 3015645939.9,
            "unit": "ns",
            "range": "± 375574.926981022"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3178352104.5,
            "unit": "ns",
            "range": "± 515502.2363475578"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 100)",
            "value": 3013023938.9,
            "unit": "ns",
            "range": "± 356534.695625545"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3178394412.2,
            "unit": "ns",
            "range": "± 483430.67942074174"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 100, MessageSize: 1000)",
            "value": 3013385847.1,
            "unit": "ns",
            "range": "± 483667.3759365004"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3179342880.75,
            "unit": "ns",
            "range": "± 290640.7607894151"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 100)",
            "value": 3013285603.9,
            "unit": "ns",
            "range": "± 567425.8770472141"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Confluent_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3178320088.2,
            "unit": "ns",
            "range": "± 508804.99756753567"
          },
          {
            "name": "Dekaf.Benchmarks.Benchmarks.Client.ConsumerBenchmarks.Dekaf_PollSingle(MessageCount: 1000, MessageSize: 1000)",
            "value": 3013864068.4,
            "unit": "ns",
            "range": "± 859328.4766780395"
          }
        ]
      }
    ]
  }
}