using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RandomizerCommon
{
    // No business logic allowed
    public class Util
    {
        public static void Warn(string text)
        {
#if DEBUG
            throw new Exception(text);
#else
            Console.WriteLine($"WARNING: {text}");
#endif
        }

        public static void AddMulti<K, V, T>(IDictionary<K, T> dict, K key, V value)
            where T : ICollection<V>, new()
        {
            if (!dict.TryGetValue(key, out T col))
            {
                dict[key] = col = new T();
            }
            col.Add(value);
        }

        public static void AddMulti<K, V, T>(IDictionary<K, T> dict, K key, IEnumerable<V> values)
            where T : ICollection<V>, new()
        {
            if (!dict.TryGetValue(key, out T col))
            {
                dict[key] = col = new T();
            }
            if (col is ISet<V> set)
            {
                set.UnionWith(values);
            }
            else if (col is List<V> list)
            {
                list.AddRange(values);
            }
            else
            {
                foreach (V value in values)
                {
                    col.Add(value);
                }
            }
        }

        public static void AddMulti<K, V, V2, T>(IDictionary<K, T> dict, K key, V value, V2 value2)
            where T : IDictionary<V, V2>, new()
        {
            if (!dict.ContainsKey(key)) dict[key] = new T();
            dict[key][value] = value2;
        }

        public static void AddMultiNest<K, V, V2, T>(IDictionary<K, T> dict, K key, V value, V2 value2)
            where T : IDictionary<V, List<V2>>, new()
        {
            // List<V2> cannot be generic because C# can't this level of inference
            if (!dict.ContainsKey(key)) dict[key] = new T();
            AddMulti(dict[key], value, value2);
        }

        /// <summary>
        /// Returns the indefinite article appropriate for the given noun, followed by the noun
        /// itself.
        /// </summary>
        public static string IndefiniteArticle(string noun)
        {
            var lowerCase = noun.ToLowerInvariant();
            return noun.StartsWith("a") || noun.StartsWith("e") || noun.StartsWith("i") || noun.StartsWith("o") || noun.StartsWith("u")
                ? $"An {noun}"
                : $"A {noun}";
        }

        public class ReadIndexDictionary<K, V> : IReadOnlyDictionary<K, V>
        {
            public int Index { get; set; }
            public IDictionary<K, List<V>> Inner { get; set; }

            public V this[K key] => Inner[key][Index];

            public IEnumerable<K> Keys => Inner.Keys;

            public IEnumerable<V> Values => Inner.Values.Select(v => v[Index]);

            public int Count => Inner.Count;

            public bool ContainsKey(K key) => Inner.ContainsKey(key);

            public bool TryGetValue(K key, out V value)
            {
                value = default;
                if (!Inner.TryGetValue(key, out List<V> vs))
                {
                    value = vs[Index];
                    return true;
                }
                return false;
            }

            // Implement this if needed
            public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public static void Shuffle<T>(Random random, IList<T> list)
        {
            // Fisher Yates shuffle - O(n)
            for (var i = 0; i < list.Count - 1; i++)
            {
                int j = random.Next(i, list.Count);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public static T Choice<T>(Random random, IList<T> list)
        {
            return list[random.Next(list.Count)];
        }

        public static T WeightedChoice<T>(Random random, IList<T> list, Func<T, float> weightFunc)
        {
            // Weighted selection with accumulator - O(n)
            List<float> weights = list.Select(weightFunc).ToList();
            double sum = weights.Sum();
            double threshhold = random.NextDouble() * sum;
            double acc = 0;
            for (int i = 0; i < list.Count(); i++)
            {
                acc += weights[i];
                if (acc > threshhold)
                {
                    return list[i];
                }
            }
            return list[list.Count() - 1];
        }

        public static List<T> WeightedShuffle<T>(Random random, IList<T> list, Func<T, float> weightFunc)
        {
            // Like Fisher-Yates with weighted selection, but not in-place, because that is too much state to maintain.
            // It is O(n lg n). If some entry has a weight of x%, it will have an x% chance to be first, and so on.
            List<T> ret = new List<T>(list.Count);
            // Build initial heap. It is 1-indexed
            // Each node stores its own weight, and the weight of everything beneath it.
            HeapNode[] heap = new HeapNode[list.Count + 1];
            int size = list.Count;
            for (int i = 0; i < size; i++)
            {
                heap[i + 1] = new HeapNode
                {
                    OriginalIndex = i,
                    Weight = weightFunc(list[i])
                };
            }
            for (int i = size / 2; i >= 1; i--)
            {
                MaxHeapify(heap, size, i, false);
            }
            for (int i = size; i >= 1; i--)
            {
                int left = 2 * i;
                int right = 2 * i + 1;
                float weight = heap[i].Weight;
                if (left <= size) weight += heap[left].CumWeight;
                if (right <= size) weight += heap[right].CumWeight;
                heap[i].CumWeight = weight;
            }
            // Now, do repeated weighted random selection
            while (size > 0)
            {
                // Index into one of the tree elements by total weight
                float threshhold = (float) random.NextDouble() * heap[1].CumWeight;
                // Console.WriteLine($"[{seed}] Selecting from [{string.Join(", ", Enumerable.Range(1, size).Select(v => heap[v].Weight))}], {threshhold}/{heap[1].CumWeight}");
                int i = 1;
                while (true)
                {
                    // The ordering is: 1. Node itself 2. Left tree 3. Right tree
                    // Because of the heap property, this is likely to be near the top.
                    if (threshhold < heap[i].Weight)
                    {
                        // Console.WriteLine($"Selecting at {i} from {heap[i].Weight}, {(2*i<size ? heap[2*i].CumWeight : 0)}, {(2*i+1<size ? heap[2*i+1].CumWeight : 0)} with {ts} => start");
                        break;
                    }
                    threshhold -= heap[i].Weight;
                    int left = 2 * i;
                    int right = 2 * i + 1;
                    if (left > size) break;
                    if (threshhold < heap[left].CumWeight)
                    {
                        i = left;
                        continue;
                    }
                    if (right > size) break;
                    threshhold -= heap[left].CumWeight;
                    i = right;
                }
                ret.Add(list[heap[i].OriginalIndex]);
                HeapRemove(heap, size, i);
                size--;
            }
            return ret;
        }

        private static void MaxHeapify(HeapNode[] heap, int size, int i, bool updateWeights)
        {
            int left = 2 * i;
            int right = 2 * i + 1;
            int largest = i;
            if (left <= size && heap[left].Weight > heap[largest].Weight) largest = left;
            if (right <= size && heap[right].Weight > heap[largest].Weight) largest = right;
            if (largest != i)
            {
                SwapWithParent(heap, largest, updateWeights);
                MaxHeapify(heap, size, largest, updateWeights);
            }
        }

        private static void SwapWithParent(HeapNode[] heap, int i, bool updateWeights)
        {
            HeapNode child = heap[i];
            HeapNode parent = heap[i / 2];
            if (updateWeights)
            {
                // Overall subtree weight remains the same. But parent inherits child's subtree, with its weight rather than child's.
                float totalWeight = parent.CumWeight;
                parent.CumWeight = child.CumWeight + parent.Weight - child.Weight;
                child.CumWeight = totalWeight;
            }
            heap[i] = parent;
            heap[i / 2] = child;
        }

        private static void HeapRemove(HeapNode[] heap, int size, int rem)
        {
            if (rem < 1 || rem > size) throw new Exception($"Invalid arguments for remove: size {size} index {rem}");
            if (size == 1) return;
            // Cut off the last index
            // Console.WriteLine($"Removing {rem}");
            // Console.WriteLine($"Weights 1: [{string.Join(", ", Enumerable.Range(1, size).Select(v => $"{heap[v].Weight}/{heap[v].CumWeight}"))}]");
            HeapNode last = heap[size];
            HeapUpdateWeights(heap, size / 2, -last.Weight);
            // Replace middle node with last node if necessary
            if (rem != size)
            {
                float diff = last.Weight - heap[rem].Weight;
                heap[rem].OriginalIndex = last.OriginalIndex;
                heap[rem].Weight = last.Weight;
                HeapUpdateWeights(heap, rem, diff);
                MaxHeapify(heap, size - 1, rem, true);
            }
        }

        private static void HeapUpdateWeights(HeapNode[] heap, int start, float diff)
        {
            int i = start;
            while (i > 0)
            {
                heap[i].CumWeight += diff;
                i /= 2;
            }
        }

        public static void TestHeapWeights()
        {
            Random r = new Random();
            List<float> weights = new List<float> { 10, 20, 5, 5, 2, 3, 10, 10, 30, 5 };
            SortedDictionary<string, int> histogram = new SortedDictionary<string, int>();
            int count = 0;
            for (int i = 0; i < 100000; i++)
            {
                // public static IList<T> WeightedShuffle<T>(Random random, IList<T> list, Func<T, float> weightFunc)
                List<int> nums = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                nums = WeightedShuffle(r, nums, v => weights[v]);
                string perm = string.Join(",", nums);
                // if (!histogram.ContainsKey(perm)) histogram[perm] = 1; else histogram[perm]++;
                string f = nums[0].ToString();
                if (!histogram.ContainsKey(f)) histogram[f] = 0;
                histogram[f]++;
                count++;
            }
            foreach (KeyValuePair<string, int> entry in histogram)
            {
                Console.WriteLine($"{entry.Key}: {100.0 * entry.Value / count}%");
            }
        }

        private struct HeapNode
        {
            public float Weight { get; set; }
            public float CumWeight { get; set; }
            public int OriginalIndex { get; set; }
        }

        // https://stackoverflow.com/questions/283456/byte-array-pattern-search
        public static int SearchInt(byte[] array, uint num)
        {
            byte[] candidate = BitConverter.GetBytes(num);
            for (int i = 0; i < array.Length; i++)
            {
                if (IsMatch(array, i, candidate)) return i;
            }
            return -1;
        }

        private static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array[position + i] != candidate[i])
                    return false;

            return true;
        }

        public static HashSet<int> ManualBinarySearch(List<int> values, int start=0, int end=0)
        {
            // Used for things like finding which param row is responsible for certain behaviors
            int i, j;
            if (start == 0 && end == 0)
            {
                i = 0;
                j = values.Count - 1;
                start = values[i];
                end = values[j];
            }
            else
            {
                i = values.IndexOf(start);
                j = values.IndexOf(end);
            }
            Console.WriteLine($"Current range: {start},{end} ({j - i + 1} out of total {values.Count}, from {values[0]},{values[values.Count - 1]})");
            if (start == end) throw new Exception("fatcat");
            // if 3 to 4, make range 3 and 4. if 3 to 5, make range 34 and 5. if 3 to 6, make range 34 and 56
            int mid1 = (i + j) / 2;
            int mid2 = mid1 + 1;
            List<int> keepVal = values.GetRange(i, mid1 - i + 1);
            List<int> discardVal = values.GetRange(mid2, j - mid2 + 1);
            Console.WriteLine($"Keeping: {string.Join(",", keepVal)}");
            Console.WriteLine($"Discarding: {string.Join(",", discardVal)}");
            Console.WriteLine($"If behavior observed, use {start},{values[mid1]}");
            Console.WriteLine($"If not, use {values[mid2]},{end}");
            return new HashSet<int>(discardVal);
        }
    }
}
