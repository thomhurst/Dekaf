window.BENCHMARK_DATA = {
  "lastUpdate": 1769360558123,
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
      }
    ]
  }
}