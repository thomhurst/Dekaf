window.BENCHMARK_DATA = {
  "lastUpdate": 1769449377285,
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
      }
    ]
  }
}