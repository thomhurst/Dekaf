import unittest

import mutation_shard


def source(*members):
    body = "\n".join(f"    void {name}()\n    {{\n        Work();\n    }}\n" for name in members)
    return f"class C\n{{\n{body}}}\n"


class MutationShardTests(unittest.TestCase):
    def test_shards_cover_the_file_contiguously(self):
        text = source(*(f"M{i}" for i in range(9)))
        spans = [mutation_shard.shard_span(text, shard, 3) for shard in (1, 2, 3)]

        self.assertEqual(0, spans[0][0])
        self.assertEqual(len(text), spans[-1][1])
        for (_, end), (start, _) in zip(spans, spans[1:]):
            self.assertEqual(end, start)
            self.assertLess(start, len(text))

    def test_cuts_fall_between_members(self):
        text = source(*(f"M{i}" for i in range(9)))
        _, end = mutation_shard.shard_span(text, 1, 3)

        self.assertEqual("    }\n", text[:end][-6:])
        self.assertEqual("\n", text[end])

    def test_crlf_offsets_count_both_characters(self):
        text = source("A", "B", "C", "D").replace("\n", "\r\n")
        _, end = mutation_shard.shard_span(text, 1, 2)

        self.assertEqual("    }\r\n", text[:end][-7:])

    def test_single_shard_is_the_whole_file(self):
        text = source("A")
        self.assertEqual((0, len(text)), mutation_shard.shard_span(text, 1, 1))

    def test_too_few_boundaries_fail(self):
        with self.assertRaises(ValueError):
            mutation_shard.shard_span(source("A", "B"), 1, 3)


if __name__ == "__main__":
    unittest.main()
