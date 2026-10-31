using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    public sealed class AgentMemoryVector {
        public string revision; public List<double> values = new List<double>();
    }
    public sealed class AgentMemoryVectorData {
        public string key = ""; public List<AgentMemoryVector> items = new List<AgentMemoryVector>();
    }
    // Semantic recall for user-approved memories. Vectors are derived from memory text and stored in a
    // separate DPAPI-encrypted cache; failure of the local model always falls back to the lexical search.
    public sealed partial class AgentMemoryStore {
        readonly object vectorGate = new object();
        string VectorFile { get { return Path.Combine(library.DataDir, "agent-memory-vectors.sealed"); } }

        AgentMemoryVectorData ReadVectors(string key) {
            if (!File.Exists(VectorFile)) return new AgentMemoryVectorData { key = key };
            try {
                if (new FileInfo(VectorFile).Length > 2 * 1024 * 1024) throw new InvalidDataException();
                var data = Library.Json.Deserialize<AgentMemoryVectorData>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(VectorFile), null, DataProtectionScope.CurrentUser)));
                if (data == null || data.items == null || data.items.Count > 128 || data.items.Any(v => v == null || !AgentJournal.ValidId(v.revision) || v.values == null || v.values.Count < 2 || v.values.Count > 16384 || v.values.Any(x => Double.IsNaN(x) || Double.IsInfinity(x)))) throw new InvalidDataException();
                if (data.key != key) return new AgentMemoryVectorData { key = key };
                return data;
            } catch { return new AgentMemoryVectorData { key = key }; }
        }
        void WriteVectors(AgentMemoryVectorData data) {
            string json = Library.Json.Serialize(data);
            if (json.Length > 2000000) throw new InvalidDataException("记忆向量缓存过大。");
            Directory.CreateDirectory(library.DataDir);
            string temp = VectorFile + ".tmp";
            File.WriteAllBytes(temp, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));
            if (File.Exists(VectorFile)) File.Replace(temp, VectorFile, null); else File.Move(temp, VectorFile);
        }

        public async Task<List<AgentMemoryRecord>> SearchHybrid(string query, IEnumerable<string> allowed, int limit = 3, LocalEmbedder embedder = null, CancellationToken token = default(CancellationToken)) {
            var lexical = Search(query, allowed, limit);
            EmbeddingConfig config;
            try { config = library.ReadEmbeddingConfig(); } catch { return lexical; }
            if (config == null || !config.enabled || String.IsNullOrWhiteSpace(config.model)) return lexical;
            var scope = new HashSet<string>(allowed ?? Enumerable.Empty<string>());
            List<AgentMemoryRecord> fresh;
            lock (gate) fresh = Read().items.Where(r => r.ids.All(scope.Contains) && Fresh(r)).Select(Clone).ToList();
            if (fresh.Count == 0) return lexical;
            try {
                embedder = embedder ?? new LocalEmbedder();
                string key = Library.EmbeddingKey(config);
                AgentMemoryVectorData data;
                lock (vectorGate) data = ReadVectors(key);
                var missing = fresh.Where(r => !data.items.Any(v => v.revision == r.revision)).ToList();
                for (int offset = 0; offset < missing.Count; offset += 8) {
                    var batch = missing.Skip(offset).Take(8).ToList();
                    var vectors = await embedder.Embed(config, batch.Select(r => r.title + "\n" + r.text).ToList(), token);
                    for (int n = 0; n < batch.Count; n++) {
                        data.items.RemoveAll(v => v.revision == batch[n].revision);
                        data.items.Add(new AgentMemoryVector { revision = batch[n].revision, values = vectors[n].ToList() });
                    }
                }
                data.items = data.items.Where(v => fresh.Any(r => r.revision == v.revision)).Take(128).ToList();
                lock (vectorGate) WriteVectors(data);
                var queryVector = (await embedder.Embed(config, new[] { query }, token))[0];
                var semantic = fresh.Select(r => { var v = data.items.FirstOrDefault(x => x.revision == r.revision); return new { record = r, score = v == null ? -1d : v.values.Zip(queryVector, (a, b) => a * b).Sum() }; })
                    .Where(x => x.score >= config.minimumSimilarity).OrderByDescending(x => x.score).Take(Math.Max(limit, 8)).ToList();
                if (semantic.Count == 0) return lexical;
                var scores = new Dictionary<string, double>();
                int position = 0;
                foreach (var row in lexical) scores[row.id] = 1d / (60 + ++position);
                position = 0;
                foreach (var row in semantic) { double before; scores[row.record.id] = (scores.TryGetValue(row.record.id, out before) ? before : 0) + 1d / (60 + ++position); }
                var byId = fresh.ToDictionary(r => r.id);
                return scores.OrderByDescending(pair => pair.Value).Where(pair => byId.ContainsKey(pair.Key)).Take(Math.Max(1, Math.Min(5, limit))).Select(pair => byId[pair.Key]).ToList();
            } catch (OperationCanceledException) { throw; } catch (Exception) { return lexical; }
        }

        internal static bool SameSourceSet(List<string> left, List<string> right) {
            if (left == null || right == null) return false;
            return new HashSet<string>(left.Where(id => id != null)).SetEquals(new HashSet<string>(right.Where(id => id != null)));
        }
        static string Normalize(string text) { return new string((text ?? "").ToLowerInvariant().Where(c => Char.IsLetterOrDigit(c) || c >= 0x4e00 && c <= 0x9fff).ToArray()); }
        internal static bool NearDuplicate(string left, string right) {
            string a = Normalize(left), b = Normalize(right);
            return a.Length >= 8 && a == b;
        }
    }
}
