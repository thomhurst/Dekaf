window.BENCHMARK_DATA = {
  "lastUpdate": 1769040857154,
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
      }
    ]
  }
}