using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SoulsFormats;

namespace RandomizerCommon
{
    public class EnemyPlacement : SoulsFile<EnemyPlacement>, IReadOnlyList<EnemyPlacement.Entry>
    {
        private BinaryReaderEx LazyReader;
        private int Length;
        public List<Entry> Entries { get; set; }

        // Note: Does *not* support compression.
        // Getting the bytes in memory seems like acceptable time/space tradeoff.
        public static EnemyPlacement ReadLazy(string path)
        {
            EnemyPlacement ret = new EnemyPlacement();
            BinaryReaderEx br = new BinaryReaderEx(false, File.ReadAllBytes(path));
            br.AssertASCII("ENPL");
            long size = br.Length - 4;
            ret.Length = (int)(size / (7 * 4));
            ret.LazyReader = br;
            return ret;
        }

        protected override void Read(BinaryReaderEx br)
        {
            br.BigEndian = false;
            br.AssertASCII("ENPL");
            long size = br.Length - 4;
            Length = (int)(size / (7 * 4));
            Entries = new(Length);
            for (int i = 0; i < Length; i++)
            {
                Entries.Add(new Entry(br));
            }
        }

        protected override void Write(BinaryWriterEx bw)
        {
            bw.BigEndian = false;
            bw.WriteASCII("ENPL");
            Entries.Sort();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].Write(bw);
            }
        }

        public Entry GetEntry(uint dest, uint id, int index = 0)
        {
            Entry fakeEntry = new Entry() { Dest = dest, ID = id, Index = index };
            if (Entries != null)
            {
                int loc = Entries.BinarySearch(fakeEntry);
                return loc >= 0 ? Entries[loc] : null;
            }
            else
            {
                // Of >60k entries (~2^16), usually <400 will be used for normal randomizer
                // Some of these will be adjacent for many-enemy bosses, but just focus on each search being independently as fast as possible
                // Also pre-filter entities eligible for this placement
                int loc = BinarySearchIndex(Length, index =>
                {
                    LazyReader.Position = 4 + index * (7 * 4);
                    // Need to return fakeEntry.CompareTo(Entries[index])
                    return -Entry.CompareEntry(fakeEntry, LazyReader);
                });
                return loc >= 0 ? this[loc] : null;
            }
        }

        public int Count
        {
            get => Length;
        }

        public Entry this[int index]
        {
            get
            {
                if (Entries != null)
                {
                    return Entries[index];
                }
                else
                {
                    LazyReader.Position = 4 + index * (7 * 4);
                    return new Entry(LazyReader);
                }
            }
        }

        // https://stackoverflow.com/questions/967047/how-to-perform-a-binary-search-on-ilistt
        private static int BinarySearchIndex(int count, Func<int, int> compareIndex)
        {
            int lower = 0;
            int upper = count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                // Previously: comparer.Compare(value, list[middle]);
                int comparisonResult = compareIndex(middle);
                if (comparisonResult == 0)
                    return middle;
                else if (comparisonResult < 0)
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            return ~lower;
        }

        private IEnumerable<Entry> GetEnumerable()
        {
            return Entries ?? GetLazyEnumerable();
        }

        private IEnumerable<Entry> GetLazyEnumerable()
        {
            LazyReader.Position = 4;
            for (int i = 0; i < Length; i++)
            {
                yield return new Entry(LazyReader);
            }
        }

        // Awful GetEnumerator boilerplate
        public IEnumerator<Entry> GetEnumerator()
        {
            return GetEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerable().GetEnumerator();
        }

        public class Entry : IComparable<Entry>
        {
            public uint Dest { get; set; }
            public uint ID { get; set; }
            public int Index { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Rot { get; set; }

            public Vector3 Position => new Vector3(X, Y, Z);
            public Vector3 Rotation => new Vector3(0, Rot, 0);

            public Entry() { }

            public int CompareTo(Entry o)
            {
                return (Dest, ID, Index).CompareTo((o.Dest, o.ID, o.Index));
            }

            internal static int CompareEntry(Entry o, BinaryReaderEx br)
            {
                uint dest = br.ReadUInt32();
                int cmp = dest.CompareTo(o.Dest);
                if (cmp != 0) return cmp;
                uint id = br.ReadUInt32();
                cmp = id.CompareTo(o.ID);
                if (cmp != 0) return cmp;
                int index = br.ReadInt32();
                return index.CompareTo(o.Index);
            }

            internal Entry(BinaryReaderEx br)
            {
                Dest = br.ReadUInt32();
                ID = br.ReadUInt32();
                Index = br.ReadInt32();
                X = br.ReadSingle();
                Y = br.ReadSingle();
                Z = br.ReadSingle();
                Rot = br.ReadSingle();
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteUInt32(Dest);
                bw.WriteUInt32(ID);
                bw.WriteInt32(Index);
                bw.WriteSingle(X);
                bw.WriteSingle(Y);
                bw.WriteSingle(Z);
                bw.WriteSingle(Rot);
            }
        }
    }
}
