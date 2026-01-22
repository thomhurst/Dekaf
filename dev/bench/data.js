window.BENCHMARK_DATA = {
  "lastUpdate": 1769090876807,
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
      }
    ]
  }
}