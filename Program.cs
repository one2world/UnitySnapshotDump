// See https://aka.ms/new-console-template for more information
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Database;
using Unity.MemoryProfiler.Editor.Database.Operation;
using Unity.MemoryProfiler.Editor.Database.Operation.Filter;
using Unity.MemoryProfiler.Editor.UI;
using UnityEditor;
using UnityEngine;
using static Unity.MemoryProfiler.Editor.Database.Operation.Filter.Sort;

namespace UnityMemorySnapParserRunner
{
    unsafe class Program
    {
        static void Main(string[] args)
        {
            //using (SnapshotFileData snapshotFileData = new SnapshotFileData(@"E:/Backup/TempProjs/OffGcMemory/MemoryCaptures/Snapshot-638355122205091799.snap"))
            //{
            //    Console.WriteLine($"FieldDescriptions_Name, 0, 10 {snapshotFileData.GetSizeForEntryRange(UnityMemorySnap.Format.EntryType.FieldDescriptions_Name, 0, 10)}");
            //    Console.ReadLine();
            //}
            try
            {
                var formattingOptions = new FormattingOptions();
                formattingOptions.ObjectDataFormatter = new ObjectDataFormatter();
                var sizeDataFormatter = new Unity.MemoryProfiler.Editor.Database.SizeDataFormatter();
                formattingOptions.AddFormatter("size", sizeDataFormatter);
                using (SnapshotFileData snapshot = new SnapshotFileData(new FileInfo(@"E:/Backup/TempProjs/OffGcMemory/MemoryCaptures/Snapshot-638355122205091799.snap")))
                {
                    var reader = snapshot.LoadSnapshot();
                    var firstMode = new SnapshotMode(formattingOptions.ObjectDataFormatter, reader);
                    {
                        //0.Summary
                        {
                            var snapshotTargetMemoryStats = firstMode.snapshot.MetaData.TargetMemoryStats.Value;
                            var snapshotTargetInfo = firstMode.snapshot.MetaData.TargetInfo.Value;
                            var systemUsedMemory = snapshotTargetMemoryStats.TotalVirtualMemory;


                            var reservedMemoryAttributedToSpecificCategories =
                                snapshotTargetMemoryStats.GcHeapReservedMemory
                                + snapshotTargetMemoryStats.GraphicsUsedMemory
                                + snapshotTargetMemoryStats.ProfilerReservedMemory
                                + snapshotTargetMemoryStats.AudioUsedMemory;
                            var usedMemoryAttributedToSpecificCategories =
                                snapshotTargetMemoryStats.GcHeapUsedMemory
                                + snapshotTargetMemoryStats.GraphicsUsedMemory
                                + snapshotTargetMemoryStats.ProfilerUsedMemory
                                + snapshotTargetMemoryStats.AudioUsedMemory;

                            var unityMemoryReserved = snapshotTargetMemoryStats.TotalReservedMemory - reservedMemoryAttributedToSpecificCategories;
                            var unityMemoryUsed = snapshotTargetMemoryStats.TotalUsedMemory - usedMemoryAttributedToSpecificCategories;

                            if (reservedMemoryAttributedToSpecificCategories > snapshotTargetMemoryStats.TotalReservedMemory)
                            {
                                unityMemoryReserved = 0;
                            }
                            if (usedMemoryAttributedToSpecificCategories > snapshotTargetMemoryStats.TotalUsedMemory)
                            {
                                unityMemoryUsed = 0;
                            }

                            ulong totalCommitedUsed = 0ul;
                            ulong totalCommitedReserved = 0ul;
                            ulong[] used = new ulong[Enum.GetValues(typeof(BreakdownOrder)).Length];
                            ulong[] reserved = new ulong[Enum.GetValues(typeof(BreakdownOrder)).Length];
                            ulong[] managedUsed = new ulong[Enum.GetValues(typeof(ManagedBreakdownOrder)).Length];
                            ulong[] managedReserved = new ulong[Enum.GetValues(typeof(ManagedBreakdownOrder)).Length];

                            reserved[(int)BreakdownOrder.ManagedHeap] = snapshotTargetMemoryStats.GcHeapReservedMemory;
                            used[(int)BreakdownOrder.ManagedHeap] = snapshotTargetMemoryStats.GcHeapUsedMemory;

                            reserved[(int)BreakdownOrder.ManagedDomain] = firstMode.snapshot.ManagedHeapSections.VirtualMachineMemoryReserved;
                            used[(int)BreakdownOrder.ManagedDomain] = firstMode.snapshot.ManagedHeapSections.VirtualMachineMemoryReserved;
                            reserved[(int)BreakdownOrder.Graphics] = used[(int)BreakdownOrder.Graphics] = snapshotTargetMemoryStats.GraphicsUsedMemory;
                            reserved[(int)BreakdownOrder.Audio] = used[(int)BreakdownOrder.Audio] = snapshotTargetMemoryStats.AudioUsedMemory;

                            reserved[(int)BreakdownOrder.Other] = unityMemoryReserved;
                            used[(int)BreakdownOrder.Other] = unityMemoryUsed;

                            reserved[(int)BreakdownOrder.Profiler] = snapshotTargetMemoryStats.ProfilerReservedMemory;
                            used[(int)BreakdownOrder.Profiler] = snapshotTargetMemoryStats.ProfilerUsedMemory;
                            used[(int)BreakdownOrder.ExecutableAndDlls] = reserved[(int)BreakdownOrder.ExecutableAndDlls] = firstMode.snapshot.NativeRootReferences.ExecutableAndDllsReportedValue;
                            totalCommitedReserved = snapshotTargetMemoryStats.TotalReservedMemory + reserved[(int)BreakdownOrder.ManagedDomain] + reserved[(int)BreakdownOrder.ExecutableAndDlls];
                            totalCommitedUsed = snapshotTargetMemoryStats.TotalUsedMemory + used[(int)BreakdownOrder.ManagedDomain] + used[(int)BreakdownOrder.ExecutableAndDlls];
                            long totalTraceMemory = reserved.Sum(n => (long)n);

                            var totalManagedMemory = 0ul;
                            if (firstMode.snapshot.HasMemoryLabelSizesAndGCHeapTypes && firstMode.snapshot.CaptureFlags.HasFlag(Unity.MemoryProfiler.Editor.Format.CaptureFlags.ManagedObjects))
                            {
                                managedReserved[(int)ManagedBreakdownOrder.ManagedDomain] = managedUsed[(int)ManagedBreakdownOrder.ManagedDomain] = firstMode.snapshot.ManagedHeapSections.VirtualMachineMemoryReserved;
                                managedReserved[(int)ManagedBreakdownOrder.ManagedObjects] = managedUsed[(int)ManagedBreakdownOrder.ManagedObjects] = firstMode.snapshot.CrawledData.ManagedObjectMemoryUsage;
                                managedReserved[(int)ManagedBreakdownOrder.EmptyActiveHeapSpace] = managedUsed[(int)ManagedBreakdownOrder.EmptyActiveHeapSpace] = firstMode.snapshot.CrawledData.ActiveHeapMemoryEmptySpace;
                                managedReserved[(int)ManagedBreakdownOrder.EmptyFragmentedHeapSpace] = managedUsed[(int)ManagedBreakdownOrder.EmptyFragmentedHeapSpace] = firstMode.snapshot.ManagedHeapSections.ManagedHeapMemoryReserved - managedReserved[(int)ManagedBreakdownOrder.ManagedObjects] - managedReserved[(int)ManagedBreakdownOrder.EmptyActiveHeapSpace];

                                totalManagedMemory = firstMode.snapshot.ManagedHeapSections.ManagedHeapMemoryReserved + firstMode.snapshot.ManagedHeapSections.VirtualMachineMemoryReserved + firstMode.snapshot.ManagedStacks.StackMemoryReserved;

                            }

                            var totalReservedMemory = 0ul;
                            if (firstMode.snapshot.MetaData.TargetMemoryStats.HasValue)
                                totalReservedMemory = firstMode.snapshot.MetaData.TargetMemoryStats.Value.TotalVirtualMemory;
                            else
                            {
                                for (long i = 0; i < firstMode.snapshot.NativeMemoryRegions.Count; i++)
                                {
                                    if (!firstMode.snapshot.NativeMemoryRegions.MemoryRegionName[i].Contains("Virtual Memory"))
                                        totalReservedMemory += firstMode.snapshot.NativeMemoryRegions.AddressSize[i];
                                }
                                totalReservedMemory += firstMode.snapshot.ManagedHeapSections.ManagedHeapMemoryReserved + firstMode.snapshot.ManagedHeapSections.VirtualMachineMemoryReserved + firstMode.snapshot.ManagedStacks.StackMemoryReserved;
                            }
                            Console.WriteLine($"total {EditorUtility.FormatBytes((long)totalReservedMemory)} (tracked {EditorUtility.FormatBytes((long)totalTraceMemory)} + untracked {EditorUtility.FormatBytes((long)totalReservedMemory - totalTraceMemory)}), Use {EditorUtility.FormatBytes((long)totalCommitedUsed)}");
                            Console.WriteLine("Memory Usage");
                            for (int i = 0; i < Enum.GetValues(typeof(BreakdownOrder)).Length; i++)
                            {
                                Console.WriteLine($"\t{((BreakdownOrder)i).ToString()} {EditorUtility.FormatBytes((long)used[i])} ({EditorUtility.FormatBytes((long)reserved[i])})");
                            }
                            Console.WriteLine("Managed Memory");
                            for (int i = 0; i < Enum.GetValues(typeof(ManagedBreakdownOrder)).Length; i++)
                            {
                                Console.WriteLine($"\t{((ManagedBreakdownOrder)i).ToString()} {EditorUtility.FormatBytes((long)managedUsed[i])} ({EditorUtility.FormatBytes((long)managedReserved[i])})");
                            }
                            Console.WriteLine();
                            Console.WriteLine();
                        }

                        Table tableSource = null;

                        Sort m_AllLevelSortFilter = new Sort();
                        var m_Filters = new Multi();
                        m_Filters.filters.Add(m_AllLevelSortFilter);

                        //Table CreateFilter1(Table tableIn, ArrayRange range)
                        //{
                        //    if (m_AllLevelSortFilter.SortLevel.Count == 0)
                        //    {
                        //        return new IndexedTable(tableIn, range);
                        //    }

                        //    int[] columnIndex = new int[m_AllLevelSortFilter.SortLevel.Count];
                        //    SortOrder[] order = new SortOrder[m_AllLevelSortFilter.SortLevel.Count];
                        //    for (int i = 0; i != m_AllLevelSortFilter.SortLevel.Count; ++i)
                        //    {
                        //        columnIndex[i] = m_AllLevelSortFilter.SortLevel[i].GetColumnIndex(tableIn);
                        //        order[i] = m_AllLevelSortFilter.SortLevel[i].Order;
                        //    }
                        //    return new SortedTable(tableIn, columnIndex, order, 0, range);
                        //}
                        //Table CreateFilter2(Table tableIn)
                        //{
                        //    if (m_AllLevelSortFilter.SortLevel.Count == 0)
                        //    {
                        //        return tableIn;
                        //    }

                        //    // make sure we can get an accurate row count
                        //    tableIn.ComputeRowCount();

                        //    // This is a temporary fix to avoid sorting sub tables entries with top level entries.
                        //    // the real fix involve sorting sub tables entries as part of the group head
                        //    if (tableIn is ExpandTable)
                        //    {
                        //        var et = (ExpandTable)tableIn;
                        //        et.ResetAllGroup();
                        //    }

                        //    return CreateFilter1(tableIn, new ArrayRange(0, tableIn.GetRowCount()));
                        //}
                        void RemoveDefaultSortFilter()
                        {
                            m_AllLevelSortFilter.SortLevel.Clear();
                        }
                        bool SetDefaultSortFilter(int colIndex, SortOrder ss)
                        {
                            var changed = false;
                            if (m_AllLevelSortFilter.SortLevel.Count != 1 || m_AllLevelSortFilter.SortLevel[0].Order != ss || m_AllLevelSortFilter.SortLevel[0].GetColumnIndex(tableSource) != colIndex)
                            {
                                m_AllLevelSortFilter.SortLevel.Clear();

                                if (ss != SortOrder.None)
                                {
                                    Sort.Level sl = new Sort.LevelByIndex(colIndex, ss);
                                    m_AllLevelSortFilter.SortLevel.Add(sl);
                                }
                                changed = true;
                            }
                            return changed;
                        }

                        bool AddSubGroupFilter(int colIndex, bool update = true)
                        {
                            var newFilter = new GroupByColumnIndex(colIndex, SortOrder.Ascending);


                            var ds = new DefaultSort(m_AllLevelSortFilter, null);

                            var gfp = GetDeepestGroupFilter(m_Filters);
                            if (gfp.child != null)
                            {
                                //add the new group with the default sort filter
                                var newMulti = new Multi();
                                newMulti.filters.Add(newFilter);
                                newMulti.filters.Add(ds);
                                var subf = gfp.child.SubGroupFilter;
                                gfp.child.SubGroupFilter = newMulti;
                                newFilter.SubGroupFilter = subf;
                            }
                            else
                            {
                                //add it to top, already has te default sort filter there
                                newFilter.SubGroupFilter = ds;
                                m_Filters.filters.Insert(0, newFilter);
                            }
                            return true;
                        }

                        // return if something change
                        bool RemoveSubGroupFilter(long colIndex, bool update = true)
                        {
                            FilterParenthood<Filter, Group> fpToRemove = new FilterParenthood<Filter, Group>();

                            foreach (var fp in VisitAllSubGroupFilters(m_Filters))
                            {
                                if (fp.child.GetColumnIndex(tableSource) == colIndex)
                                {
                                    fpToRemove = fp;
                                    break;
                                }
                            }

                            if (fpToRemove.child != null)
                            {
                                if (Filter.RemoveFilter(fpToRemove.parent, fpToRemove.child))
                                {
                                    bool dirty = false;
                                    m_Filters.Simplify(ref dirty);
                                    return true;
                                }
                            }

                            return false;
                        }

                        IEnumerable<FilterParenthood<Filter, Group>> VisitAllSubGroupFilters(Filter filter)
                        {
                            foreach (var f in filter.SubFilters())
                            {
                                if (f is Group)
                                {
                                    Group gf = (Group)f;
                                    yield return new FilterParenthood<Filter, Group>(filter, gf);
                                }
                                foreach (var f2 in VisitAllSubGroupFilters(f))
                                {
                                    yield return f2;
                                }
                            }
                        }

                        FilterParenthood<Filter, Group> GetFirstSubGroupFilter(Filter filter)
                        {
                            var e = VisitAllSubGroupFilters(filter).GetEnumerator();
                            if (e.MoveNext()) return e.Current;
                            return new FilterParenthood<Filter, Group>();
                        }

                        FilterParenthood<Filter, Group> GetDeepestGroupFilter(Filter filter)
                        {
                            foreach (var f in filter.SubFilters())
                            {
                                var sgf = GetDeepestGroupFilter(f);
                                if (sgf.child != null) return sgf;

                                if (f is Group)
                                {
                                    Group gf = (Group)f;
                                    return new FilterParenthood<Filter, Group>(filter, gf);
                                }
                            }

                            return new FilterParenthood<Filter, Group>();
                        }

                        //1. All Managed Objects
                        {
                            tableSource = firstMode.GetSchema().GetTableByName(ObjectAllManagedTable.TableName);
                            SetDefaultSortFilter(4, SortOrder.Descending);
                            AddSubGroupFilter(2);
                            tableSource = m_Filters.CreateFilter(tableSource);
                            var meta = tableSource.GetMetaData();
                            int colCount = meta.GetColumnCount();

                            bool breakAfter = false;
                            for (int row = 0; !breakAfter && row < tableSource.GetRowCount(); row++)
                            {
                                if (row == 0)
                                {
                                    for (int col = 0; col < colCount; col++)
                                    {
                                        var metaColumn = meta.GetColumnByIndex(col);
                                        Console.Write($"{metaColumn.Name}\t");
                                    }
                                }
                                Console.WriteLine();
                                for (int col = 0; col < colCount; col++)
                                {
                                    var metaColumn = meta.GetColumnByIndex(col);
                                    var column = tableSource.GetColumnByIndex(col);
                                    Console.Write($"{column.GetRowValueString(row, formattingOptions.GetFormatter(metaColumn.FormatName))}\t");
                                    if (col == 4)
                                    {
                                        var size = (column as ColumnTyped<long>)?.GetRowValue(row);
                                        if (size.HasValue && size.Value < 512)
                                        {
                                            breakAfter = true; // break
                                        }
                                    }
                                }
                                Console.WriteLine();
                            }
                            Console.WriteLine();
                        }

                        //2. All Native Objects
                        {
                            tableSource = firstMode.GetSchema().GetTableByName(ObjectAllNativeTable.TableName);
                            SetDefaultSortFilter(4, SortOrder.Descending);
                            //AddSubGroupFilter(2);
                            tableSource = m_Filters.CreateFilter(tableSource);
                            var meta = tableSource.GetMetaData();
                            int colCount = meta.GetColumnCount();

                            bool breakAfter = false;
                            for (int row = 0; !breakAfter && row < tableSource.GetRowCount(); row++)
                            {
                                if (row == 0)
                                {
                                    for (int col = 0; col < colCount; col++)
                                    {
                                        var metaColumn = meta.GetColumnByIndex(col);
                                        Console.Write($"{metaColumn.Name}\t");
                                    }
                                }
                                Console.WriteLine();
                                for (int col = 0; col < colCount; col++)
                                {
                                    var metaColumn = meta.GetColumnByIndex(col);
                                    var column = tableSource.GetColumnByIndex(col);
                                    Console.Write($"{column.GetRowValueString(row, formattingOptions.GetFormatter(metaColumn.FormatName))}\t");
                                    if (col == 4)
                                    {
                                        var size = (column as ColumnTyped<long>)?.GetRowValue(row);
                                        if(size.HasValue && size.Value < 512)
                                        {
                                            breakAfter = true; // break
                                        }
                                    }
                                }
                                Console.WriteLine();
                            }
                            Console.WriteLine();
                        }
                    }
                    Console.WriteLine($"Version {snapshot.GuiData.UnityVersion}");
                    Console.ReadLine();
                }
            }catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }
    }
}



//namespace UnityMemory
//{
//    using Format;
//    using System.Diagnostics;
//    using System.Runtime.Serialization;
//    using System.IO.MemoryMappedFiles;
//    using System.Runtime.InteropServices;
//    using System.Reflection.PortableExecutable;
//    using System.Threading;
//    using System.ComponentModel;
//    using System.Collections.Generic;
//    using System.Linq;
//    using UnityMemorySnap.Containers;
//    using System.Collections;
//    //using static UnityMemory.SnapshotFileData.LowLevelFileReader;

////    internal unsafe class SnapshotFileData : IDisposable
////    {
////        private LowLevelFileReader m_LowLevelFileReader;
////        public Block[] Blocks { get; private set; }
////        public Entry[] Entries { get; private set; }
////        public FormatVersion Version { get; private set; }
////        internal ProfileTargetInfo? TargetInfo = null;
////        internal ProfileTargetMemoryStats? TargetMemoryStats = null;

////        const string k_UnknownPlatform = "Unknown Platform";
////        const string k_UnknownProductName = "Unknown Project";
////        const string k_UnknownUnityVersion = "Unknown";
////        public const uint InvalidSessionGUID = 0;
////        public string Content;
////        public string Platform;
////        public string PlatformExtra;
////        public string ProductName;
////        public uint SessionGUID;
////        public string UnityVersion;

////        public string FilePath => m_LowLevelFileReader.FilePath;
////        public string ScreenshotPath { get; private set; }

////        public DateTime TimeStamp { get; private set; }
////        public VirtualMachineInformation VirtualMachineInformation { get; private set; }
////        public NativeAllocationSiteEntriesCache NativeAllocationSites;
////        public TypeDescriptionEntriesCache TypeDescriptions;
////        public NativeTypeEntriesCache NativeTypes;
////        public NativeRootReferenceEntriesCache NativeRootReferences;
////        public NativeObjectEntriesCache NativeObjects;
////        public NativeMemoryRegionEntriesCache NativeMemoryRegions;
////        public NativeMemoryLabelEntriesCache NativeMemoryLabels;
////        public NativeCallstackSymbolEntriesCache NativeCallstackSymbols;
////        public NativeAllocationEntriesCache NativeAllocations;
////        public ManagedMemorySectionEntriesCache ManagedStacks;
////        public ManagedMemorySectionEntriesCache ManagedHeapSections;
////        public GCHandleEntriesCache GcHandles;
////        public FieldDescriptionEntriesCache FieldDescriptions;
////        public ConnectionEntriesCache Connections;
////        public CaptureFlags CaptureFlags = 0;

////        public SortedNativeMemoryRegionEntriesCache SortedNativeRegionsEntries;
////        public SortedManagedMemorySectionEntriesCache SortedManagedStacksEntries;
////        public SortedManagedMemorySectionEntriesCache SortedManagedHeapEntries;
////        public SortedManagedObjectsCache SortedManagedObjects;
////        public SortedNativeAllocationsCache SortedNativeAllocations;
////        public SortedNativeObjectsCache SortedNativeObjects;

////        public SceneRootEntriesCache SceneRoots;
////        public NativeAllocatorEntriesCache NativeAllocators;
////        public NativeGfxResourcReferenceEntriesCache NativeGfxResourceReferences;

////        public bool HasConnectionOverhaul
////        {
////            get { return Version >= FormatVersion.NativeConnectionsAsInstanceIdsVersion; }
////        }

////        public bool HasTargetAndMemoryInfo
////        {
////            get { return Version >= FormatVersion.ProfileTargetInfoAndMemStatsVersion; }
////        }

////        public bool HasMemoryLabelSizesAndGCHeapTypes
////        {
////            get { return Version >= FormatVersion.MemLabelSizeAndHeapIdVersion; }
////        }

////        public bool HasSceneRootsAndAssetbundles
////        {
////            get { return Version >= FormatVersion.SceneRootsAndAssetBundlesVersion; }
////        }

////        public bool HasGfxResourceReferencesAndAllocators
////        {
////            get { return Version >= FormatVersion.GfxResourceReferencesAndAllocatorsVersion; }
////        }
////        public SnapshotFileData(string filePath)
////        {
////            m_LowLevelFileReader = new LowLevelFileReader(filePath);
////            ScreenshotPath = Path.ChangeExtension(FilePath, ".png");
////            Blob16Byte fileOffsets = m_LowLevelFileReader.BlockEntriesOffsets;
////            long* fileOffsetsPtr = (long*)(&fileOffsets);
////            int* counts = stackalloc int[2];
////            *(counts + 0) = m_LowLevelFileReader.As<int>((int)*(fileOffsetsPtr + 0)..);
////            *(counts + 1) = m_LowLevelFileReader.As<int>((int)*(fileOffsetsPtr + 1)..);
////            if (*(counts + 1) < 1)
////                throw new($"Invalid block count {*(counts + 1)}");
////            if (*counts > (int)EntryType.Count)
////                *counts = (int)EntryType.Count;

////            var 
////            begin = *(fileOffsetsPtr + 1)+ UnsafeUtility.IntSize;
////            var dataBlockOffsets = m_LowLevelFileReader.AsSpan<long>(begin, counts[1] * UnsafeUtility.LongSize);
////            {
////                Blocks = new Block[dataBlockOffsets.Length];
////                for (int i = 0; i < dataBlockOffsets.Length; i++)
////                {
////                    var blockOffset = dataBlockOffsets[i];

////                    var header = m_LowLevelFileReader.As<BlockHeader>((int)blockOffset..);
////                    var block = new Block(header);
////                    Blocks[i] = block;

////                    var src = m_LowLevelFileReader.AsSpan<long>(blockOffset + sizeof(BlockHeader), UnsafeUtility.LongSize * block.OffsetCount);
////                    src.CopyTo(new Span<long>(block.Offsets));
////                }
////            }

////            begin = *fileOffsetsPtr + UnsafeUtility.IntSize;
////            var entryTypeToChapterOffset = m_LowLevelFileReader.AsSpan<long>(begin, counts[0] * UnsafeUtility.LongSize);
////            {
////                Entries = new Entry[entryTypeToChapterOffset.Length];
////                for (int i = 0; i < entryTypeToChapterOffset.Length; i++)
////                {
////                    var offset = entryTypeToChapterOffset[i];
////                    EntryHeader header = default(EntryHeader);
////                    if (offset != 0)
////                    {
////                        header = m_LowLevelFileReader.As<EntryHeader>((int)offset..);
////                    }

////                    var entry = new Entry(header);
////                    Entries[i] = entry;

////                    if(header.Format == EntryFormat.DynamicSizeElementArray)
////                    {
////                        var src = m_LowLevelFileReader.AsSpan<long>(offset + sizeof(EntryHeader), UnsafeUtility.LongSize * entry.Count);
////                        src.CopyTo(new Span<long>(entry.AdditionalEntryStorage));
////                    }
////                }

////                for (int i = 0; i < Entries.Length; i++)
////                {
////                    fixed (Entry* entriesBegin = &Entries[i])
////                    {
////                        if (entriesBegin->Header.Format == EntryFormat.DynamicSizeElementArray)
////                        {
////                            //swap back the first entry we read during the header read with the total size at the end of the entries array
////                            //also memmove the array by one to the right to make space for the first entry
////                            //This is required as we should not have to take cache hits when computing size by having to always jump to the end of the array and back
////                            fixed (long* storagePtr = entriesBegin->AdditionalEntryStorage)
////                            {
////                                //long* storagePtr = entriesBegin->GetAdditionalStoragePtr();
////                                long* headerMetaPtr = (long*)((byte*)entriesBegin + sizeof(EntryFormat) + sizeof(uint) * 2);
////                                long totalSize = storagePtr[entriesBegin->Count - 1];
////                                UnsafeUtility.MemMove(storagePtr + 1, storagePtr, (int)(sizeof(long) * (entriesBegin->Count - 1)));
////                                *storagePtr = *headerMetaPtr;
////                                *headerMetaPtr = totalSize;
////                            }
////                        }
////                    }
////                }
////            }

////            uint vBuffer = 0;
////            byte* versionPtr = (byte*)&vBuffer;
////            ReadUnsafe(EntryType.Metadata_Version, versionPtr, UnsafeUtility.UIntSize, 0, 1);
////            Version = (FormatVersion)vBuffer;

////            Checks.CheckEquals(true, m_LowLevelFileReader.IsCreated);

////            //Metadata_UserMetadata
////            {
////                var legacyBuffer = Read(EntryType.Metadata_UserMetadata, 0, 1);
////                if (Version >= FormatVersion.ProfileTargetInfoAndMemStatsVersion)
////                {
////                    ProfileTargetInfo info;
////                    ReadUnsafe(EntryType.ProfileTarget_Info, &info, UnsafeUtility.SizeOf<ProfileTargetInfo>(), 0, 1);
////                    TargetInfo = info;

////                    ProfileTargetMemoryStats memStats;
////                    ReadUnsafe(EntryType.ProfileTarget_MemoryStats, &memStats, UnsafeUtility.SizeOf<ProfileTargetMemoryStats>(), 0, 1);
////                    TargetMemoryStats = memStats;
////                    Console.WriteLine($"TotalVirtualMemory {memStats.TotalVirtualMemory}");
////                    Console.WriteLine($"UnityVersion {info.UnityVersion}");
////                    Console.WriteLine($"ProductName {info.ProductName}");
////                    DeserializeMetaData(legacyBuffer, info);
////                }
////                else
////                { 
////                    DeserializeLegacyMetadata(legacyBuffer);
////                }
////            }

////            VirtualMachineInformation vmInfo;
////            ReadUnsafe(EntryType.Metadata_VirtualMachineInformation, &vmInfo, UnsafeUtility.SizeOf<VirtualMachineInformation>(), 0, 1);

////            if (!VMTools.ValidateVirtualMachineInfo(vmInfo))
////            {
////                throw new UnityException("Invalid VM info. Snapshot file is corrupted.");
////            }
////            long ticks;
////            ReadUnsafe(EntryType.Metadata_RecordDate, &ticks, UnsafeUtility.SizeOf<long>(), 0, 1);
////            TimeStamp = new DateTime(ticks);

////            NativeAllocationSites = new NativeAllocationSiteEntriesCache(this);
////            FieldDescriptions = new FieldDescriptionEntriesCache(this);
////            TypeDescriptions = new TypeDescriptionEntriesCache(this, FieldDescriptions);
////            NativeTypes = new NativeTypeEntriesCache(this);
////            NativeRootReferences = new NativeRootReferenceEntriesCache(this);
////            NativeObjects = new NativeObjectEntriesCache(this);
////            NativeMemoryRegions = new NativeMemoryRegionEntriesCache(this);
////            NativeMemoryLabels = new NativeMemoryLabelEntriesCache(this, HasMemoryLabelSizesAndGCHeapTypes);
////            NativeCallstackSymbols = new NativeCallstackSymbolEntriesCache(this);
////            NativeAllocations = new NativeAllocationEntriesCache(this, NativeAllocationSites.Count != 0);
////            ManagedStacks = new ManagedMemorySectionEntriesCache(this, false, true);
////            ManagedHeapSections = new ManagedMemorySectionEntriesCache(this, HasMemoryLabelSizesAndGCHeapTypes, false);
////            GcHandles = new GCHandleEntriesCache(this);
////            Connections = new ConnectionEntriesCache(this, NativeObjects, GcHandles.Count, HasConnectionOverhaul);
////            SceneRoots = new SceneRootEntriesCache(this);
////            NativeGfxResourceReferences = new NativeGfxResourcReferenceEntriesCache(this);
////            NativeAllocators = new NativeAllocatorEntriesCache(this);

////            if (GcHandles.Count > 0)
////                CaptureFlags |= CaptureFlags.ManagedObjects;
////            if (NativeAllocations.Count > 0)
////                CaptureFlags |= CaptureFlags.NativeAllocations;
////            if (NativeAllocationSites.Count > 0)
////                CaptureFlags |= CaptureFlags.NativeAllocationSites;
////            if (NativeObjects.Count > 0)
////                CaptureFlags |= CaptureFlags.NativeObjects;
////            if (NativeCallstackSymbols.Count > 0)
////                CaptureFlags |= CaptureFlags.NativeStackTraces;

////            SortedManagedStacksEntries = new SortedManagedMemorySectionEntriesCache(ManagedStacks);
////            SortedManagedHeapEntries = new SortedManagedMemorySectionEntriesCache(ManagedHeapSections);
////            SortedManagedObjects = new SortedManagedObjectsCache(this);

////            SortedNativeRegionsEntries = new SortedNativeMemoryRegionEntriesCache(this);
////            SortedNativeAllocations = new SortedNativeAllocationsCache(this);
////            SortedNativeObjects = new SortedNativeObjectsCache(this);

////            CrawledData = new ManagedData(GcHandles.Count, Connections.Count);
////            SceneRoots.CreateTransformTrees(this);
////            SceneRoots.GenerateGameObjectData(this);
////        }

////        #region Read
////        #region Unity Read
////        public EntryFormat GetEntryFormat(EntryType type)
////        {
////            return Entries[(int)type].Header.Format;
////        }

////        public long GetSizeForEntryRange(EntryType entry, long offset, long count)
////        {
////            Checks.CheckEntryTypeValueIsValidAndThrow(entry);
////            var entryData = Entries[(int)entry];
////            Checks.CheckIndexOutOfBoundsAndThrow(offset, entryData.Count);

////            return entryData.ComputeByteSizeForEntryRange(offset, count, true);
////        }

////        public uint GetEntryCount(EntryType entry)
////        {
////            Checks.CheckEntryTypeValueIsValidAndThrow(entry);
////            var entryData = Entries[(int)entry];

////            return entryData.Count;
////        }

////        public GenericReadOperation Read(EntryType entry, DynamicArray<byte> buffer, long offset, long count)
////        {
////            unsafe
////            {
////                var op = AsyncRead(entry, buffer, offset, count);
////                //op.Complete();
////                return op;
////            }
////        }
////        public GenericReadOperation Read(EntryType entry, long offset, long count)
////        {
////            unsafe
////            {
////                var op = AsyncRead(entry, offset, count);
////                //op.Complete();
////                return op;
////            }
////        }
////        public unsafe ReadError ReadUnsafe(EntryType entry, void* buffer, long bufferLength, long offset, long count)
////        {
////            {
////                var res = InternalRead(entry, offset, count, buffer, bufferLength);

////                if (res.error != ReadError.InvalidEntryFormat)
////                {
////                    //res.handle.JobHandle.Complete();
////                    res.error = ReadError.Success;
////                }
////                return res.error;
////            }
////        }

////        public GenericReadOperation AsyncRead(EntryType entry, long offset, long count)
////        {
////            var readSize = GetSizeForEntryRange(entry, offset, count);
////            return InternalAsyncRead(entry, new DynamicArray<byte>(readSize), offset, count, true);
////        }

////        public GenericReadOperation AsyncRead(EntryType entry, DynamicArray<byte> buffer, long offset, long count)
////        {
////            return InternalAsyncRead(entry, buffer, offset, count, false);
////        }

////        GenericReadOperation InternalAsyncRead(EntryType entry, DynamicArray<byte> buffer, long offset, long count, bool ownsBuffer)
////        {
////            {
////                unsafe
////                {
////                    var res = InternalRead(entry, offset, count, buffer.GetUnsafePtr(), buffer.Count);
////                    GenericReadOperation asyncOp = null;
////                    if (res.error != ReadError.InProgress)
////                    {
////                        asyncOp = new GenericReadOperation(default(DynamicArray<byte>));
////                        asyncOp.Error = res.error;
////                        return asyncOp;
////                    }
////                    return new GenericReadOperation(buffer);
////                }
////            }
////        }
////        #endregion
////        private ScheduleResult InternalRead(EntryType entry, long offset, long count, void* buffer, long bufferLength)
////        {
////            Checks.CheckEntryTypeValueIsValidAndThrow(entry);
////            Checks.CheckEquals(GetSizeForEntryRange(entry, 0, count), bufferLength);
////            var result = new ScheduleResult();

////            var entryData = Entries[(int)entry];
////            if (entryData.Count < 1)
////            {
////                result.error = ReadError.EmptyFormatEntry;
////                return result; //guard against reading empty format entries
////            }

////            Checks.CheckIndexOutOfBoundsAndThrow(offset, count);
////            Checks.CheckIndexInRangeAndThrow((long)entryData.ComputeByteSizeForEntryRange(offset, count, true), bufferLength);
////            long rangeByteSize = (long)entryData.ComputeByteSizeForEntryRange(offset, count, false);

////            var bufferPtr = (byte*)buffer;
////            ReadError error = ReadError.None;
////            switch (entryData.Header.Format)
////            {
////                case EntryFormat.SingleElement:
////                    ScheduleSingleElementEntryReads(entryData, rangeByteSize, bufferPtr);
////                    result.error = ReadError.InProgress;
////                    break;
////                case EntryFormat.ConstantSizeElementArray:
////                    ScheduleConstSizeElementArrayRead(entryData, offset, rangeByteSize, bufferPtr);
////                    result.error = ReadError.InProgress;
////                    break;
////                case EntryFormat.DynamicSizeElementArray:

////                    bool readHeaderMeta = count + offset == entryData.Count;
////                    long dynamicEntryLengthsArray = (count + 1) * sizeof(long);

////                    //dynamic entries require x bytes in front of the data to store lengths
////                    fixed(long* src = entryData.AdditionalEntryStorage)
////                    {
////                        NativeMemory.Copy(bufferPtr, src + offset, (uint)(dynamicEntryLengthsArray - (readHeaderMeta ? sizeof(long) : 0)));
////                    }

////                    if (readHeaderMeta)
////                    {
////                        var lastOffset = ((long*)bufferPtr) + count;
////                        *lastOffset = (long)entryData.Header.HeaderMeta;
////                    }

////                    //shift the offsets, so that we remove the lengths of the skipped elements
////                    if (offset > 0)
////                    {
////                        var offsetDiff = entryData.AdditionalEntryStorage[offset];
////                        long* offsetsPtr = (long*)bufferPtr;
////                        for (int i = 0; i < count + 1; ++i)
////                        {
////                            var offsetVal = *offsetsPtr;
////                            *offsetsPtr++ = offsetVal - offsetDiff;
////                        }
////                    }

////                    //offset to jump over where we copied the lengths
////                    bufferPtr = bufferPtr + dynamicEntryLengthsArray;
////                    ScheduleDynamicSizeElementArrayReads(entryData, offset, count, rangeByteSize, bufferPtr);
////                    result.error = ReadError.InProgress;
////                    break;
////                default:
////                    result.error = ReadError.InvalidEntryFormat;
////                    break;
////            }

////            return result;
////        }

////        unsafe void ScheduleSingleElementEntryReads(Entry entry, long readSize, void* dst)
////        {
////            var block = Blocks[(int)entry.Header.BlockIndex];
////            var blockOffset = entry.Header.HeaderMeta;
////            var chunkSize = block.Header.ChunkSize;
////            uint chunkIndex = (uint)(blockOffset / chunkSize);
////            var chunk = block.Offsets[chunkIndex];
////            var chunkWithLocalOffset = chunk + (uint)(blockOffset % chunkSize);

////            m_LowLevelFileReader.AsSpan<byte>(chunkWithLocalOffset, readSize).CopyTo(new Span<byte>(dst, (int)readSize));
////        }

////        unsafe void ScheduleConstSizeElementArrayRead(Entry entry, long firstElement, long readSize, void* dst)
////        {
////            var block = Blocks[(int)entry.Header.BlockIndex];
////            var blockOffset = entry.Header.EntriesMeta * (ulong)firstElement;
////            var chunkSize = block.Header.ChunkSize;
////            var chunkIndex = (uint)(blockOffset / chunkSize);
////            var chunk = block.Offsets[chunkIndex];
////            var chunkWithLocalOffset = chunk + (uint)(blockOffset % chunkSize);

////            byte* dstPtr = (byte*)dst;
////            {
////                m_LowLevelFileReader.AsSpan<byte>(chunkWithLocalOffset, readSize).CopyTo(new Span<byte>(dstPtr, (int)readSize));
////                dstPtr += (long)(chunkSize - (blockOffset % chunkSize));
////                readSize -= (long)(chunkSize - (blockOffset % chunkSize));

////                while (readSize > 0)
////                {
////                    ++chunkIndex;
////                    var chunkReadSize = Math.Min(readSize, (long)chunkSize);
////                    chunk = block.Offsets[chunkIndex];

////                    m_LowLevelFileReader.AsSpan<byte>(chunk, chunkReadSize).CopyTo(new Span<byte>(dstPtr, (int)chunkReadSize));
////                    dstPtr += chunkReadSize;
////                    readSize -= chunkReadSize;
////                }
////            }
////        }
////        unsafe void ScheduleDynamicSizeElementArrayReads(Entry entry, long elementOffset, long elementCount, long readSize, void* buffer)
////        {
////            var block = Blocks[(int)entry.Header.BlockIndex];
////            byte* dst = (byte*)buffer;

////            for (int i = 0; i < elementCount; ++i)
////            {
////                var e = new ElementRead();
////                e.start = entry.AdditionalEntryStorage[i + elementOffset];
////                e.end = i + elementOffset + 1 == entry.Count ? (long)entry.Header.HeaderMeta : entry.AdditionalEntryStorage[i + elementOffset + 1];
////                e.readDst = dst;
////                var readOffset = ProcessDynamicSizeElement(ref block, e);
////                dst += readOffset;
////                readSize -= readOffset;
////            }

////            Checks.CheckEquals(0, readSize);
////        }
////        unsafe long ProcessDynamicSizeElement(ref Block block, ElementRead elementRead)
////        {
////            long written = 0;
////            var chunkSize = (long)block.Header.ChunkSize;
////            var elementSize = elementRead.end - elementRead.start;
////            var chunkIndex = elementRead.start / chunkSize;
////            var chunkOffset = block.Offsets[chunkIndex];
////            var elementOffsetInChunk = elementRead.start % chunkSize;
////            var remainingChunksize = chunkSize - elementOffsetInChunk;

////            var rSize = Math.Min(chunkSize, elementSize);
////            if (remainingChunksize != chunkSize)
////            {
////                chunkOffset += elementOffsetInChunk; //align the read
////                if (rSize > remainingChunksize)
////                    rSize = remainingChunksize;
////            }

////            m_LowLevelFileReader.AsSpan<byte>(chunkOffset, rSize).CopyTo(new Span<byte>(elementRead.readDst, (int)rSize));
////            //chunkReads.Push(ReadCommandBufferUtils.GetCommand(elementRead.readDst, rSize, chunkOffset));
////            elementRead.readDst += rSize;
////            elementSize -= rSize;
////            written += rSize;

////            //if the element spans multiple chunks
////            while (elementSize > 0)
////            {
////                chunkIndex++;
////                chunkOffset = block.Offsets[chunkIndex];
////                rSize = Math.Min(chunkSize, elementSize);

////                m_LowLevelFileReader.AsSpan<byte>(chunkOffset, rSize).CopyTo(new Span<byte>(elementRead.readDst, (int)rSize));  
////                //chunkReads.Push(ReadCommandBufferUtils.GetCommand(elementRead.readDst, rSize, chunkOffset));

////                elementRead.readDst += rSize;
////                elementSize -= rSize;
////                written += rSize;
////            }

////            return written;
////        }

////        unsafe struct ElementRead
////        {
////            public long start;
////            public long end;
////            public byte* readDst;
////        }
////        #endregion

////        void DeserializeMetaData(ReadOnlySpan<byte> legacyDataBuffer, ProfileTargetInfo targetInfo)
////        {
////            int contentLength = legacyDataBuffer != null ? MemoryMarshal.Read<int>(legacyDataBuffer) : 0;

////            long offset = 0;
////            offset += sizeof(int);
////            if (contentLength == 0)
////                Content = "";
////            else
////            {
////                Content = new string('A', contentLength);
////                int copySize = sizeof(char) * contentLength;
////                fixed (char* cntPtr = Content)
////                {
////                    legacyDataBuffer.Slice((int)offset, copySize).CopyTo(MemoryMarshal.Cast<char, byte>(new Span<char>(cntPtr, contentLength)));
////                }

////                offset += copySize;
////                if (offset >= legacyDataBuffer.Length)
////                    return;
////            }

////            contentLength = legacyDataBuffer != null ? MemoryMarshal.Read<int>(legacyDataBuffer.Slice((int)offset)) : 0;
////            offset += sizeof(int);

////            if (contentLength != 0)
////                PlatformExtra = "";
////            else
////            {
////                PlatformExtra = new string('A', contentLength);
////                int copySize = sizeof(char) * contentLength;
////                fixed (char* cntPtr = Platform)
////                {
////                    legacyDataBuffer.Slice((int)offset, copySize).CopyTo(MemoryMarshal.Cast<char, byte>(new Span<char>(cntPtr, contentLength)));
////                }
////            }
////        }

////        void DeserializeLegacyMetadata(ReadOnlySpan<byte> legacyDataBuffer)
////        {
////            SessionGUID = InvalidSessionGUID;
////            ProductName = k_UnknownProductName;
////            UnityVersion = k_UnknownUnityVersion;
////            if (legacyDataBuffer.Length == 0)
////            {
////                Content = "";
////                Platform = k_UnknownPlatform;
////                return;
////            }

////            int contentLength = MemoryMarshal.Read<int>(legacyDataBuffer);
////            long offset = 0;
////            offset += sizeof(int);
////            if (contentLength == 0)
////                Content = "";
////            else
////            {
////                Content = new string('A', contentLength);
////                int copySize = sizeof(char) * contentLength;
////                fixed (char* cntPtr = Content)
////                {
////                    //UnsafeUtility.MemCpy(cntPtr, bufferPtr + offset, copySize);
////                    legacyDataBuffer.Slice((int)offset, copySize).CopyTo(MemoryMarshal.Cast<char, byte>(new Span<char>(cntPtr, contentLength)));
////                }

////                offset += copySize;
////                if (offset >= legacyDataBuffer.Length)
////                    return;
////            }

////            contentLength = MemoryMarshal.Read<int>(legacyDataBuffer.Slice((int)offset)); 
////            offset += sizeof(int);

////            if (contentLength == 0)
////                Platform = k_UnknownPlatform;
////            else
////            {
////                Platform = new string('A', contentLength);
////                int copySize = sizeof(char) * contentLength;
////                fixed (char* cntPtr = Platform)
////                {
////                    //UnsafeUtility.MemCpy(cntPtr, bufferPtr + offset, copySize);
////                    legacyDataBuffer.Slice((int)offset, copySize).CopyTo(MemoryMarshal.Cast<char, byte>(new Span<char>(cntPtr, contentLength)));
////                }

////                offset += copySize;
////                if (offset >= legacyDataBuffer.Length)
////                    return;
////            }

////            contentLength = MemoryMarshal.Read<int>(legacyDataBuffer.Slice((int)offset)); 
////            offset += sizeof(int);

////            if (contentLength == 0)
////            { 

////            }
////            else
////            {
////                byte[] pixels = new byte[contentLength]; //texturePixels
////                fixed (byte* pxPtr = pixels)
////                {
////                    //UnsafeUtility.MemCpy(pxPtr, bufferPtr + offset, contentLength);
////                    legacyDataBuffer.Slice((int)offset).CopyTo(MemoryMarshal.Cast<char, byte>(new Span<char>(pxPtr, contentLength)));
////                }
////                offset += contentLength;

////                int width = MemoryMarshal.Read<int>(legacyDataBuffer.Slice((int)offset)); 
////                offset += sizeof(int);
////                int height = MemoryMarshal.Read<int>(legacyDataBuffer.Slice((int)offset)); 
////                offset += sizeof(int);
////                int format = MemoryMarshal.Read<int>(legacyDataBuffer.Slice((int)offset)); 
////                offset += sizeof(int);

////            }
////        }

////        public void Dispose()
////        {
////            if (!m_LowLevelFileReader.IsCreated)
////                return;

////            m_LowLevelFileReader.Dispose();
////            GC.SuppressFinalize(this);
////        }

////        public ManagedData CrawledData { internal set; get; }

////        public class NativeAllocationSiteEntriesCache : IDisposable
////        {
////            public long Count;
////            public DynamicArray<long> id = default;
////            public DynamicArray<int> memoryLabelIndex = default;
////            public ulong[][] callstackSymbols;

////            unsafe public NativeAllocationSiteEntriesCache(SnapshotFileData reader)
////            {
////                Count = reader.GetEntryCount(EntryType.NativeAllocationSites_Id);
////                callstackSymbols = new ulong[Count][];

////                if (Count == 0)
////                    return;

////                id = reader.Read(EntryType.NativeAllocationSites_Id, 0, Count).Result.Reinterpret<long>();
////                memoryLabelIndex = reader.Read(EntryType.NativeAllocationSites_MemoryLabelIndex, 0, Count).Result.Reinterpret<int>();

////                using (var tmpBuffer = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = reader.GetSizeForEntryRange(EntryType.NativeAllocationSites_CallstackSymbols, 0, Count);
////                    tmpBuffer.Resize(tmpSize, false);
////                    reader.Read(EntryType.NativeAllocationSites_CallstackSymbols, tmpBuffer, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmpBuffer, ref callstackSymbols);
////                }
////            }

////            public string GetReadableCallstackForId(NativeCallstackSymbolEntriesCache symbols, long id)
////            {
////                long entryIdx = -1;
////                for (long i = 0; i < this.id.Count; ++i)
////                {
////                    if (this.id[i] == id)
////                    {
////                        entryIdx = i;
////                        break;
////                    }
////                }

////                return entryIdx < 0 ? string.Empty : GetReadableCallstack(symbols, entryIdx);
////            }

////            public string GetReadableCallstack(NativeCallstackSymbolEntriesCache symbols, long idx)
////            {
////                string readableStackTrace = "";

////                ulong[] callstackSymbols = this.callstackSymbols[idx];

////                for (int i = 0; i < callstackSymbols.Length; ++i)
////                {
////                    long symbolIdx = -1;
////                    ulong targetSymbol = callstackSymbols[i];
////                    for (int j = 0; j < symbols.Symbol.Count; ++i)
////                    {
////                        if (symbols.Symbol[j] == targetSymbol)
////                        {
////                            symbolIdx = i;
////                            break;
////                        }
////                    }

////                    if (symbolIdx < 0)
////                        readableStackTrace += "<unknown>\n";
////                    else
////                        readableStackTrace += symbols.ReadableStackTrace[symbolIdx];
////                }

////                return readableStackTrace;
////            }

////            public void Dispose()
////            {
////                id.Dispose();
////                memoryLabelIndex.Dispose();
////                callstackSymbols = null;
////                Count = 0;
////            }
////        }

////        public class NativeRootReferenceEntriesCache : IDisposable
////        {
////            public long Count;
////            public DynamicArray<long> Id = default;
////            public DynamicArray<ulong> AccumulatedSize = default;
////            public string[] AreaName;
////            public string[] ObjectName;
////            public Dictionary<long, long> IdToIndex;
////            public readonly ulong ExecutableAndDllsReportedValue;
////            public const string ExecutableAndDllsRootReferenceName = "ExecutableAndDlls";
////            readonly long k_ExecutableAndDllsRootReferenceIndex = -1;

////            public NativeRootReferenceEntriesCache(SnapshotFileData reader)
////            {
////                Count = reader.GetEntryCount(EntryType.NativeRootReferences_Id);

////                AreaName = new string[Count];
////                ObjectName = new string[Count];

////                IdToIndex = new Dictionary<long, long>((int)Count);

////                if (Count == 0)
////                    return;

////                Id = reader.Read(EntryType.NativeRootReferences_Id, 0, Count).Result.Reinterpret<long>();
////                AccumulatedSize = reader.Read(EntryType.NativeRootReferences_AccumulatedSize, 0, Count).Result.Reinterpret<ulong>();

////                using (var tmpBuffer = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = reader.GetSizeForEntryRange(EntryType.NativeRootReferences_AreaName, 0, Count);
////                    tmpBuffer.Resize(tmpSize, false);
////                    reader.Read(EntryType.NativeRootReferences_AreaName, tmpBuffer, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmpBuffer, ref AreaName);

////                    tmpSize = reader.GetSizeForEntryRange(EntryType.NativeRootReferences_ObjectName, 0, Count);
////                    tmpBuffer.Resize(tmpSize, false);
////                    reader.Read(EntryType.NativeRootReferences_ObjectName, tmpBuffer, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmpBuffer, ref ObjectName);
////                }
////                for (long i = 0; i < Count; i++)
////                {
////                    if (k_ExecutableAndDllsRootReferenceIndex == -1 && ObjectName[i] == ExecutableAndDllsRootReferenceName)
////                    {
////                        k_ExecutableAndDllsRootReferenceIndex = i;
////                        ExecutableAndDllsReportedValue = AccumulatedSize[i];
////                    }
////                    IdToIndex.Add(Id[i], i);
////                }
////            }

////            public void Dispose()
////            {
////                Id.Dispose();
////                AccumulatedSize.Dispose();
////                Count = 0;
////                AreaName = null;
////                ObjectName = null;
////            }
////        }

////        public class NativeMemoryRegionEntriesCache : IDisposable
////        {
////            public long Count;
////            public string[] MemoryRegionName;
////            public DynamicArray<int> ParentIndex = default;
////            public DynamicArray<ulong> AddressBase = default;
////            public DynamicArray<ulong> AddressSize = default;
////            public DynamicArray<int> FirstAllocationIndex = default;
////            public DynamicArray<int> NumAllocations = default;
////            public readonly bool UsesDynamicHeapAllocator = false;
////            public readonly bool UsesSystemAllocator;

////            const string k_DynamicHeapAllocatorName = "ALLOC_DEFAULT_MAIN";

////            public NativeMemoryRegionEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeMemoryRegions_AddressBase);
////                MemoryRegionName = new string[Count];

////                if (Count == 0)
////                    return;

////                ParentIndex = ssfd.Read(EntryType.NativeMemoryRegions_ParentIndex, 0, Count).Result.Reinterpret<int>();
////                AddressBase = ssfd.Read(EntryType.NativeMemoryRegions_AddressBase, 0, Count).Result.Reinterpret<ulong>();
////                AddressSize = ssfd.Read(EntryType.NativeMemoryRegions_AddressSize, 0, Count).Result.Reinterpret<ulong>();
////                FirstAllocationIndex = ssfd.Read(EntryType.NativeMemoryRegions_FirstAllocationIndex, 0, Count).Result.Reinterpret<int>();
////                NumAllocations = ssfd.Read(EntryType.NativeMemoryRegions_NumAllocations, 0, Count).Result.Reinterpret<int>();

////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.NativeMemoryRegions_Name, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.NativeMemoryRegions_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref MemoryRegionName);
////                }

////                for (int i = 0; i < Count; i++)
////                {
////                    if (MemoryRegionName[i].StartsWith(k_DynamicHeapAllocatorName) && AddressSize[i] > 0)
////                    {
////                        UsesDynamicHeapAllocator = true;
////                        break;
////                    }
////                }
////                if (Count > 0)
////                    UsesSystemAllocator = !UsesDynamicHeapAllocator;
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                MemoryRegionName = null;
////                ParentIndex.Dispose();
////                AddressBase.Dispose();
////                AddressSize.Dispose();
////                FirstAllocationIndex.Dispose();
////                NumAllocations.Dispose();
////            }
////        }

////        public class NativeMemoryLabelEntriesCache : IDisposable
////        {
////            public long Count;
////            public string[] MemoryLabelName;
////            public DynamicArray<ulong> MemoryLabelSizes = default;

////            public NativeMemoryLabelEntriesCache(SnapshotFileData ssfd, bool hasLabelSizes)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeMemoryLabels_Name);
////                MemoryLabelName = new string[Count];

////                if (Count == 0)
////                    return;

////                if (hasLabelSizes)
////                    MemoryLabelSizes = ssfd.Read(EntryType.NativeMemoryLabels_Size, 0, Count).Result.Reinterpret<ulong>();

////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.NativeMemoryLabels_Name, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.NativeMemoryLabels_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref MemoryLabelName);
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                MemoryLabelSizes.Dispose();
////                MemoryLabelName = null;
////            }
////        }

////        public class NativeCallstackSymbolEntriesCache : IDisposable
////        {
////            public long Count;
////            public DynamicArray<ulong> Symbol = default;
////            public string[] ReadableStackTrace;

////            public NativeCallstackSymbolEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeCallstackSymbol_Symbol);
////                ReadableStackTrace = new string[Count];

////                if (Count == 0)
////                    return;

////                Symbol = ssfd.Read(EntryType.NativeCallstackSymbol_Symbol, 0, Count).Result.Reinterpret<ulong>();
////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.NativeCallstackSymbol_ReadableStackTrace, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.NativeCallstackSymbol_ReadableStackTrace, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref ReadableStackTrace);
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                Symbol.Dispose();
////                ReadableStackTrace = null;
////            }
////        }

////        public class NativeAllocationEntriesCache : IDisposable
////        {
////            public long Count;
////            public DynamicArray<int> MemoryRegionIndex = default;
////            public DynamicArray<long> RootReferenceId = default;
////            public DynamicArray<ulong> Address = default;
////            public DynamicArray<ulong> Size = default;
////            public DynamicArray<int> OverheadSize = default;
////            public DynamicArray<int> PaddingSize = default;
////            public DynamicArray<long> AllocationSiteId = default;

////            public NativeAllocationEntriesCache(SnapshotFileData ssfd, bool allocationSites /*do not read allocation sites if they aren't present*/)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeAllocations_Address);

////                if (Count == 0)
////                    return;

////                MemoryRegionIndex = ssfd.Read(EntryType.NativeAllocations_MemoryRegionIndex, 0, Count).Result.Reinterpret<int>();
////                RootReferenceId = ssfd.Read(EntryType.NativeAllocations_RootReferenceId, 0, Count).Result.Reinterpret<long>();
////                Address = ssfd.Read(EntryType.NativeAllocations_Address, 0, Count).Result.Reinterpret<ulong>();
////                Size = ssfd.Read(EntryType.NativeAllocations_Size, 0, Count).Result.Reinterpret<ulong>();
////                OverheadSize = ssfd.Read(EntryType.NativeAllocations_OverheadSize, 0, Count).Result.Reinterpret<int>();
////                PaddingSize = ssfd.Read(EntryType.NativeAllocations_PaddingSize, 0, Count).Result.Reinterpret<int>();

////                if (allocationSites)
////                    AllocationSiteId = ssfd.Read(EntryType.NativeAllocations_AllocationSiteId, 0, Count).Result.Reinterpret<long>();
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                MemoryRegionIndex.Dispose();
////                RootReferenceId.Dispose();
////                Address.Dispose();
////                Size.Dispose();
////                OverheadSize.Dispose();
////                PaddingSize.Dispose();
////                AllocationSiteId.Dispose();
////            }
////        }

////        public unsafe class NativeTypeEntriesCache : IDisposable
////        {
////            public long Count;
////            public string[] TypeName;
////            public DynamicArray<int> NativeBaseTypeArrayIndex = default;
////            const string k_Transform = "Transform";
////            public int TransformIdx { get; private set; }

////            const string k_GameObject = "GameObject";
////            public int GameObjectIdx { get; private set; }

////            const string k_MonoBehaviour = "MonoBehaviour";
////            public int MonoBehaviourIdx { get; private set; }

////            const string k_Component = "Component";
////            public int ComponentIdx { get; private set; }

////            const string k_ScriptableObject = "ScriptableObject";
////            public int ScriptableObjectIdx { get; private set; }

////            public NativeTypeEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeTypes_Name);
////                TypeName = new string[Count];

////                if (Count == 0)
////                    return;

////                NativeBaseTypeArrayIndex = ssfd.Read(EntryType.NativeTypes_NativeBaseTypeArrayIndex, 0, Count).Result.Reinterpret<int>();
////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.NativeTypes_Name, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.NativeTypes_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref TypeName);
////                }

////                TransformIdx = Array.FindIndex(TypeName, x => x == k_Transform);
////                GameObjectIdx = Array.FindIndex(TypeName, x => x == k_GameObject);
////                MonoBehaviourIdx = Array.FindIndex(TypeName, x => x == k_MonoBehaviour);
////                ComponentIdx = Array.FindIndex(TypeName, x => x == k_Component);
////                ScriptableObjectIdx = Array.FindIndex(TypeName, x => x == k_ScriptableObject);
////            }

////            public bool DerivesFrom(int typeIndexToCheck, int baseTypeToCheckAgainst)
////            {
////                while (typeIndexToCheck != baseTypeToCheckAgainst && NativeBaseTypeArrayIndex[typeIndexToCheck] >= 0)
////                {
////                    typeIndexToCheck = NativeBaseTypeArrayIndex[typeIndexToCheck];
////                }
////                return typeIndexToCheck == baseTypeToCheckAgainst;
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                NativeBaseTypeArrayIndex.Dispose();
////                TypeName = null;
////            }
////        }

////        public unsafe class SceneRootEntriesCache : IDisposable
////        {
////            // the number of scenes
////            public long Count;
////            // the asset paths for the scenes
////            public string[] AssetPath;
////            // the scene names
////            public string[] Name;
////            //the paths to the scenes in the project
////            public string[] Path;
////            // the scene build index
////            public DynamicArray<int> BuildIndex = default;
////            //the number of roots in each of the scenes
////            public DynamicArray<int> RootCounts = default;
////            // each scenes offset into the main roots list
////            public DynamicArray<int> RootOffsets = default;
////            // first index is for the scene then the second is the array of ids for that scene
////            public int[][] SceneIndexedRootTransformInstanceIds;
////            public int[][] SceneIndexedRootGameObjectInstanceIds;
////            // all of the root transform instance ids
////            public DynamicArray<int> AllRootTransformInstanceIds = default;
////            // all of the root gameobject instance ids
////            public DynamicArray<int> AllRootGameObjectInstanceIds = default;
////            // hash set of the ids to avoid duplication ( not sure we really need this)
////            public HashSet<int> RootTransformInstanceIdHashSet = default;
////            public HashSet<int> RootGameObjectInstanceIdHashSet = default;
////            // tree structures for each scene of the transforms and gameobjects so that we can lookup the structure easily
////            public TransformTree[] SceneHierarchies;

////            public class TransformTree
////            {
////                public static int kInvalidInstanceID = 0;
////                public int InstanceID { get; private set; } = kInvalidInstanceID;
////                public int GameObjectID { get; set; } = kInvalidInstanceID;
////                public TransformTree Parent = null;
////                public List<TransformTree> Children = new List<TransformTree>();
////                public bool IsScene { get; private set; } = false;

////                public TransformTree(bool isScene)
////                {
////                    IsScene = isScene;
////                }

////                public TransformTree(int instanceId)
////                {
////                    InstanceID = instanceId;
////                }

////                public void AddChild(int instanceId)
////                {
////                    var child = new TransformTree(instanceId);
////                    child.Parent = this;
////                    Children.Add(child);
////                }

////                public void AddChildren(int[] instanceIds)
////                {
////                    foreach (var instanceId in instanceIds)
////                    {
////                        if (instanceId == Parent.InstanceID) continue;
////                        var child = new TransformTree(instanceId);
////                        child.Parent = this;
////                        Children.Add(child);
////                    }
////                }
////            }


////            public SceneRootEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.SceneObjects_Name);
////                AssetPath = new string[Count];
////                Name = new string[Count];
////                Path = new string[Count];


////                if (Count == 0)
////                    return;

////                using (var tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.SceneObjects_Name, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.SceneObjects_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref Name);
////                }

////                using (var tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.SceneObjects_Path, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.SceneObjects_Path, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref Path);
////                }

////                BuildIndex = ssfd.Read(EntryType.SceneObjects_BuildIndex, 0, Count).Result.Reinterpret<int>();

////                using (var tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.SceneObjects_AssetPath, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.SceneObjects_AssetPath, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref AssetPath);
////                }

////                SceneIndexedRootTransformInstanceIds = new int[Count][];
////                var rootCount = ssfd.GetEntryCount(EntryType.SceneObjects_RootIds);
////                RootCounts = ssfd.Read(EntryType.SceneObjects_RootIdCounts, 0, Count).Result.Reinterpret<int>();
////                RootOffsets = ssfd.Read(EntryType.SceneObjects_RootIdOffsets, 0, Count).Result.Reinterpret<int>();

////                AllRootTransformInstanceIds = ssfd.Read(EntryType.SceneObjects_RootIds, 0, rootCount).Result.Reinterpret<int>();
////                RootTransformInstanceIdHashSet = new HashSet<int>();
////                for (int i = 0; i < AllRootTransformInstanceIds.Count; i++)
////                {
////                    RootTransformInstanceIdHashSet.Add(AllRootTransformInstanceIds[i]);
////                }
////                for (int i = 0; i < Count; i++)
////                {
////                    SceneIndexedRootTransformInstanceIds[i] = new int[RootCounts[i]];
////                    for (int ii = 0; ii < RootCounts[i]; ii++)
////                    {
////                        SceneIndexedRootTransformInstanceIds[i][ii] = AllRootTransformInstanceIds[ii + RootOffsets[i]];
////                    }
////                }

////                SceneHierarchies = new TransformTree[Name.Length];
////                for (int i = 0; i < Name.Length; i++)
////                {
////                    SceneHierarchies[i] = new TransformTree(TransformTree.kInvalidInstanceID);
////                    foreach (var ii in SceneIndexedRootTransformInstanceIds[i])
////                    {
////                        SceneHierarchies[i].AddChild(ii);
////                    }
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                AssetPath = null;
////                Name = null;
////                BuildIndex.Dispose();
////                RootCounts.Dispose();
////                RootOffsets.Dispose();
////                if (SceneIndexedRootTransformInstanceIds != null)
////                {
////                    for (int i = 0; i < SceneIndexedRootTransformInstanceIds.Length; i++)
////                        SceneIndexedRootTransformInstanceIds[i] = null;
////                }

////                SceneIndexedRootTransformInstanceIds = null;
////                AllRootTransformInstanceIds.Dispose();
////                RootTransformInstanceIdHashSet = null;
////                SceneHierarchies = null;
////                AllRootGameObjectInstanceIds.Dispose();
////                RootGameObjectInstanceIdHashSet = null;
////                SceneIndexedRootGameObjectInstanceIds = null;
////            }

////            public void GenerateGameObjectData(SnapshotFileData snapshot)
////            {
////                AllRootGameObjectInstanceIds = new DynamicArray<int>(AllRootTransformInstanceIds.Count);
////                for (int i = 0; i < AllRootTransformInstanceIds.Count; i++)
////                {
////                    AllRootGameObjectInstanceIds[i] = ObjectConnection.GetGameObjectInstanceIdFromTransformInstanceId(snapshot, AllRootTransformInstanceIds[i]);
////                }

////                RootGameObjectInstanceIdHashSet = new HashSet<int>();
////                for (int i = 0; i < AllRootGameObjectInstanceIds.Count; i++)
////                {
////                    RootGameObjectInstanceIdHashSet.Add(AllRootGameObjectInstanceIds[i]);
////                }

////                SceneIndexedRootGameObjectInstanceIds = new int[Count][];
////                for (int i = 0; i < Count; i++)
////                {
////                    SceneIndexedRootGameObjectInstanceIds[i] = new int[RootCounts[i]];
////                    for (int ii = 0; ii < RootCounts[i]; ii++)
////                    {
////                        SceneIndexedRootGameObjectInstanceIds[i][ii] = AllRootGameObjectInstanceIds[ii + RootOffsets[i]];
////                    }
////                }
////            }

////            public void CreateTransformTrees(SnapshotFileData snapshot)
////            {
////                if (!snapshot.HasSceneRootsAndAssetbundles) return;
////                foreach (var hierarchy in SceneHierarchies)
////                {
////                    foreach (var child in hierarchy.Children)
////                    {
////                        AddTransforms(child, snapshot);
////                    }
////                }
////            }

////            void AddTransforms(TransformTree id, SnapshotFileData snapshot)
////            {
////                id.GameObjectID = ObjectConnection.GetGameObjectInstanceIdFromTransformInstanceId(snapshot, id.InstanceID);
////                id.AddChildren(ObjectConnection.GetConnectedTransformInstanceIdsFromTransformInstanceId(snapshot, id.InstanceID));
////                foreach (var child in id.Children)
////                {
////                    AddTransforms(child, snapshot);
////                }
////            }
////        }

////        /// <summary>
////        /// A list of gfx resources and their connections to native root id.
////        /// </summary>
////        public class NativeGfxResourcReferenceEntriesCache : IDisposable
////        {
////            /// <summary>
////            /// Count of active gfx resources.
////            /// </summary>
////            public long Count;
////            /// <summary>
////            /// Gfx resource identifiers.
////            /// </summary>
////            public DynamicArray<ulong> GfxResourceId = default;
////            /// <summary>
////            /// Size of the gfx resource in bytes.
////            /// </summary>
////            public DynamicArray<ulong> GfxSize = default;
////            /// <summary>
////            /// Related native rootId.
////            /// Native roots information is present in NativeRootReferenceEntriesCache table.
////            /// NativeRootReferenceEntriesCache.idToIndex allows to map RootId to the index in the NativeRootReferenceEntriesCache table and retrive
////            /// all available information about the root such as name, ram usage, etc.
////            /// The relation is Many-to-one - Multiple entires in NativeGfxResourcReferenceEntriesCache can point to the same native root.
////            /// </summary>
////            public DynamicArray<long> RootId = default;

////            /// <summary>
////            /// Use to retrieve related gfx allocations size for the specific RootId.
////            /// This is a derived acceleration structure built on top of the table data above.
////            /// </summary>
////            public Dictionary<long, ulong> RootIdToGfxSize;

////            public NativeGfxResourcReferenceEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeGfxResourceReferences_Id);
////                RootIdToGfxSize = new Dictionary<long, ulong>((int)Count);
////                if (Count == 0)
////                    return;

////                GfxResourceId = ssfd.Read(EntryType.NativeGfxResourceReferences_Id, 0, Count).Result.Reinterpret<ulong>();
////                GfxSize = ssfd.Read(EntryType.NativeGfxResourceReferences_Size, 0, Count).Result.Reinterpret<ulong>();
////                RootId = ssfd.Read(EntryType.NativeGfxResourceReferences_RootId, 0, Count).Result.Reinterpret<long>();

////                for (int i = 0; i < Count; ++i)
////                {
////                    var id = RootId[i];
////                    var gfxSize = GfxSize[i];

////                    if (RootIdToGfxSize.TryGetValue(id, out var size))
////                        RootIdToGfxSize[id] = size + gfxSize;
////                    else
////                        RootIdToGfxSize.Add(id, gfxSize);
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                GfxResourceId.Dispose();
////                GfxSize.Dispose();
////                RootId.Dispose();
////                RootIdToGfxSize = null;
////            }
////        }

////        /// <summary>
////        /// A table of all allocators which Unity uses to manage memory allocations in native code.
////        /// All size values are in bytes.
////        /// </summary>
////        public class NativeAllocatorEntriesCache : IDisposable
////        {
////            /// <summary>
////            /// Count of allocators.
////            /// </summary>
////            public long Count;
////            /// <summary>
////            /// Name of allocator.
////            /// </summary>
////            public string[] AllocatorName;
////            /// <summary>
////            /// Memory which was requested by Unity native systems from the allocator and is being used to store data.
////            /// </summary>
////            public DynamicArray<ulong> UsedSize = default;
////            /// <summary>
////            /// Total memory that was requested by allocator from System.
////            /// May be larger than UsedSize to utilize pooling approach.
////            /// </summary>
////            public DynamicArray<ulong> ReservedSize = default;
////            /// <summary>
////            /// Total size of memory dedicated to allocations tracking.
////            /// </summary>
////            public DynamicArray<ulong> OverheadSize = default;
////            /// <summary>
////            /// Maximum amount of memory allocated with this allocator since app start.
////            /// </summary>
////            public DynamicArray<ulong> PeakUsedSize = default;
////            /// <summary>
////            /// Allocations count made via the specific allocator.
////            /// </summary>
////            public DynamicArray<ulong> AllocationCount = default;

////            public NativeAllocatorEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeAllocatorInfo_AllocatorName);
////                AllocatorName = new string[Count];

////                if (Count == 0)
////                    return;

////                UsedSize = ssfd.Read(EntryType.NativeAllocatorInfo_UsedSize, 0, Count).Result.Reinterpret<ulong>();
////                ReservedSize = ssfd.Read(EntryType.NativeAllocatorInfo_ReservedSize, 0, Count).Result.Reinterpret<ulong>();
////                OverheadSize = ssfd.Read(EntryType.NativeAllocatorInfo_OverheadSize, 0, Count).Result.Reinterpret<ulong>();
////                PeakUsedSize = ssfd.Read(EntryType.NativeAllocatorInfo_PeakUsedSize, 0, Count).Result.Reinterpret<ulong>();
////                AllocationCount = ssfd.Read(EntryType.NativeAllocatorInfo_AllocationCount, 0, Count).Result.Reinterpret<ulong>();

////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.NativeAllocatorInfo_AllocatorName, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.NativeAllocatorInfo_AllocatorName, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref AllocatorName);
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                AllocatorName = null;
////                UsedSize.Dispose();
////                ReservedSize.Dispose();
////                OverheadSize.Dispose();
////                PeakUsedSize.Dispose();
////                AllocationCount.Dispose();
////            }
////        }

////        public class NativeObjectEntriesCache : IDisposable
////        {
////            public const int InstanceIDNone = 0;

////            public long Count;
////            public string[] ObjectName;
////            public DynamicArray<int> InstanceId = default;
////            public DynamicArray<ulong> Size = default;
////            public DynamicArray<int> NativeTypeArrayIndex = default;
////            public DynamicArray<HideFlags> HideFlags = default;
////            public DynamicArray<ObjectFlags> Flags = default;
////            public DynamicArray<ulong> NativeObjectAddress = default;
////            public DynamicArray<long> RootReferenceId = default;
////            public DynamicArray<int> ManagedObjectIndex = default;

////            //scondary data
////            public DynamicArray<int> refcount = default;
////            public Dictionary<ulong, int> nativeObjectAddressToInstanceId { private set; get; }
////            public Dictionary<long, int> rootReferenceIdToIndex { private set; get; }
////            public SortedDictionary<int, int> instanceId2Index;

////            public readonly ulong TotalSizes = 0ul;

////            unsafe public NativeObjectEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.NativeObjects_InstanceId);
////                nativeObjectAddressToInstanceId = new Dictionary<ulong, int>((int)Count);
////                rootReferenceIdToIndex = new Dictionary<long, int>((int)Count);
////                instanceId2Index = new SortedDictionary<int, int>();
////                ObjectName = new string[Count];

////                if (Count == 0)
////                    return;

////                InstanceId = ssfd.Read(EntryType.NativeObjects_InstanceId, 0, Count).Result.Reinterpret<int>();
////                Size = ssfd.Read(EntryType.NativeObjects_Size, 0, Count).Result.Reinterpret<ulong>();
////                NativeTypeArrayIndex = ssfd.Read(EntryType.NativeObjects_NativeTypeArrayIndex, 0, Count).Result.Reinterpret<int>();
////                HideFlags = ssfd.Read(EntryType.NativeObjects_HideFlags, 0, Count).Result.Reinterpret<HideFlags>();
////                Flags = ssfd.Read(EntryType.NativeObjects_Flags, 0, Count).Result.Reinterpret<ObjectFlags>();
////                NativeObjectAddress = ssfd.Read(EntryType.NativeObjects_NativeObjectAddress, 0, Count).Result.Reinterpret<ulong>();
////                RootReferenceId = ssfd.Read(EntryType.NativeObjects_RootReferenceId, 0, Count).Result.Reinterpret<long>();
////                ManagedObjectIndex = ssfd.Read(EntryType.NativeObjects_GCHandleIndex, 0, Count).Result.Reinterpret<int>();
////                refcount = new DynamicArray<int>(Count, true);

////                using (var tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.NativeObjects_Name, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.NativeObjects_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref ObjectName);
////                }

////                for (long i = 0; i < NativeObjectAddress.Count; ++i)
////                {
////                    var id = InstanceId[i];
////                    nativeObjectAddressToInstanceId.Add(NativeObjectAddress[i], id);
////                    rootReferenceIdToIndex.Add(RootReferenceId[i], (int)i);
////                    instanceId2Index[id] = (int)i;
////                    TotalSizes += Size[i];
////                }

////                //fallback for the legacy snapshot formats
////                //create the managedObjectIndex array and make it -1 on each entry so they can be overridden during crawling
////                //TODO: remove this when the new crawler lands :-/
////                if (ssfd.FormatVersion < FormatVersion.NativeConnectionsAsInstanceIdsVersion)
////                {
////                    ManagedObjectIndex.Dispose();
////                    ManagedObjectIndex = new DynamicArray<int>(Count);
////                    for (int i = 0; i < Count; ++i)
////                        ManagedObjectIndex[i] = -1;
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                InstanceId.Dispose();
////                Size.Dispose();
////                NativeTypeArrayIndex.Dispose();
////                HideFlags.Dispose();
////                Flags.Dispose();
////                NativeObjectAddress.Dispose();
////                RootReferenceId.Dispose();
////                ManagedObjectIndex.Dispose();
////                refcount.Dispose();
////                ObjectName = null;
////                nativeObjectAddressToInstanceId = null;
////                instanceId2Index = null;
////            }
////        }

////        public enum MemorySectionType : byte
////        {
////            GarbageCollector,
////            VirtualMachine
////        }

////        //TODO: Add on demand load of sections, and unused chunks unload
////        public class ManagedMemorySectionEntriesCache : IDisposable
////        {
////            ProfilerMarker CacheFind = new ProfilerMarker("ManagedMemorySectionEntriesCache.Find");
////            public long Count;
////            public DynamicArray<ulong> StartAddress = default;
////            public DynamicArray<ulong> SectionSize = default;
////            public DynamicArray<MemorySectionType> SectionType = default;
////            public string[] SectionName = default;
////            public byte[][] Bytes;
////            ulong m_MinAddress;
////            ulong m_MaxAddress;
////            const ulong k_ReferenceBit = 1UL << 63;

////            static readonly string k_VMSection = UnityEditor.L10n.Tr("Virtual Machine Memory Section");
////            static readonly string k_GCSection = UnityEditor.L10n.Tr("Managed Heap Section");
////            static readonly string k_ActiveGCSection = UnityEditor.L10n.Tr("Active Managed Heap Section");
////            static readonly string k_StackSection = UnityEditor.L10n.Tr("Managed Stack Section");
////            static readonly string k_ManagedMemorySection = UnityEditor.L10n.Tr("Managed Memory Section (unclear if Heap or Virtual Machine memory, please update Unity)");

////            public readonly ulong VirtualMachineMemoryReserved = 0;
////            // if the snapshot format is missing the VM section bit, this number will include VM memory
////            public readonly ulong ManagedHeapMemoryReserved = 0;
////            public readonly ulong TotalActiveManagedHeapSectionReserved = 0;
////            public readonly ulong StackMemoryReserved = 0;

////            public readonly long FirstAssumedActiveHeapSectionIndex = 0;
////            public readonly long LastAssumedActiveHeapSectionIndex = 0;

////            public ManagedMemorySectionEntriesCache(SnapshotFileData ssfd, bool HasGCHeapTypes, bool readStackMemory)
////            {
////                Count = ssfd.GetEntryCount(readStackMemory ? EntryType.ManagedStacks_StartAddress : EntryType.ManagedHeapSections_StartAddress);
////                Bytes = new byte[Count][];
////                m_MinAddress = m_MaxAddress = 0;

////                if (Count == 0)
////                    return;

////                SectionType = new DynamicArray<MemorySectionType>(Count, true);
////                SectionName = new string[Count];
////                StartAddress = ssfd.Read(readStackMemory ? EntryType.ManagedStacks_StartAddress : EntryType.ManagedHeapSections_StartAddress, 0, Count).Result.Reinterpret<ulong>();

////                //long heapSectionIndex = 0;
////                //long vmSectionIndex = 0;
////                if (HasGCHeapTypes)
////                {
////                    for (long i = 0; i < StartAddress.Count; ++i)
////                    {
////                        var encoded = StartAddress[i];
////                        StartAddress[i] = encoded & ~k_ReferenceBit; //unmask addr
////                        var isVMSection = (encoded & k_ReferenceBit) == k_ReferenceBit;
////                        SectionType[i] = isVMSection ? MemorySectionType.VirtualMachine : MemorySectionType.GarbageCollector; //get heaptype
////                        // numbering the sections could be confusing as people might expect the numbers to stay comparable over time,
////                        // but if one section is unloaded or merged/split in a following snapshot, people might confuse them as the same one
////                        // also, grouping the columns by name doesn't work nicely then either so, only number them for debugging purposes
////                        // bonus: waaaay less string memory usage and no GC.Allocs for these!
////                        if (isVMSection)
////                            SectionName[i] = k_VMSection;//"Managed Virtual Machine Memory Section " + vmSectionIndex++;
////                        else
////                            SectionName[i] = k_GCSection;//"Managed Heap Section " + heapSectionIndex++;
////                    }
////                }
////                else
////                {
////                    for (long i = 0; i < StartAddress.Count; ++i)
////                    {
////                        SectionName[i] = k_ManagedMemorySection;
////                    }
////                }
////                if (readStackMemory)
////                {
////                    for (long i = 0; i < Count; ++i)
////                    {
////                        SectionName[i] = k_StackSection;//"Managed Stack Section " + i;
////                    }
////                }

////                //use Persistent instead of TempJob so we don't bust the allocator to bits
////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.ManagedHeapSections_Bytes, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(readStackMemory ? EntryType.ManagedStacks_Bytes : EntryType.ManagedHeapSections_Bytes, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref Bytes);
////                }
////                SectionSize = new DynamicArray<ulong>(Count);
////                SortSectionEntries(ref StartAddress, ref SectionSize, ref SectionType, ref SectionName, ref Bytes, readStackMemory);
////                m_MinAddress = StartAddress[0];
////                m_MaxAddress = StartAddress[Count - 1] + (ulong)Bytes[Count - 1].LongLength;

////                var foundLastAssumedActiveHeap = false;
////                var foundFirstAssumedActiveHeap = false;

////                for (long i = Count - 1; i >= 0; i--)
////                {
////                    if (readStackMemory)
////                        StackMemoryReserved += SectionSize[i];
////                    else
////                    {
////                        if (SectionType[i] == MemorySectionType.GarbageCollector)
////                        {
////                            ManagedHeapMemoryReserved += SectionSize[i];
////                            if (!foundLastAssumedActiveHeap)
////                            {
////                                FirstAssumedActiveHeapSectionIndex = i;
////                                LastAssumedActiveHeapSectionIndex = i;
////                                foundLastAssumedActiveHeap = true;
////                            }
////                            else if (!foundFirstAssumedActiveHeap && StartAddress[i] + SectionSize[i] + VMTools.X64ArchPtrSize > StartAddress[FirstAssumedActiveHeapSectionIndex])
////                            {
////                                FirstAssumedActiveHeapSectionIndex = i;
////                            }
////                            else
////                                foundFirstAssumedActiveHeap = true;
////                        }
////                        else
////                            VirtualMachineMemoryReserved += SectionSize[i];
////                    }
////                }
////                if (foundFirstAssumedActiveHeap && foundLastAssumedActiveHeap)
////                {
////                    for (long i = FirstAssumedActiveHeapSectionIndex; i <= LastAssumedActiveHeapSectionIndex; i++)
////                    {
////                        SectionName[i] = k_ActiveGCSection;
////                    }
////                }
////                TotalActiveManagedHeapSectionReserved = StartAddress[LastAssumedActiveHeapSectionIndex] + SectionSize[LastAssumedActiveHeapSectionIndex] - StartAddress[FirstAssumedActiveHeapSectionIndex];
////            }

////            public BytesAndOffset Find(ulong address, VirtualMachineInformation virtualMachineInformation)
////            {
////                using (CacheFind.Auto())
////                {
////                    var bytesAndOffset = new BytesAndOffset();

////                    if (address != 0 && address >= m_MinAddress && address < m_MaxAddress)
////                    {
////                        long idx = DynamicArrayAlgorithms.BinarySearch(StartAddress, address);
////                        if (idx < 0)
////                        {
////                            // -1 means the address is smaller than the first StartAddress, early out with an invalid bytesAndOffset
////                            if (idx == -1)
////                                return bytesAndOffset;
////                            // otherwise, a negative Index just means there was no direct hit and ~idx - 1 will give us the index to the next smaller StartAddress
////                            idx = ~idx - 1;
////                        }

////                        if (address >= StartAddress[idx] && address < (StartAddress[idx] + (ulong)Bytes[idx].Length))
////                        {
////                            bytesAndOffset.bytes = Bytes[idx];
////                            bytesAndOffset.offset = (int)(address - StartAddress[idx]);
////                            bytesAndOffset.pointerSize = virtualMachineInformation.PointerSize;
////                        }
////                    }

////                    return bytesAndOffset;
////                }
////            }

////            static void SortSectionEntries(ref DynamicArray<ulong> startAddresses, ref DynamicArray<ulong> sizes, ref DynamicArray<MemorySectionType> associatedSectionType, ref string[] associatedSectionNames,
////                ref byte[][] associatedByteArrays, bool isStackMemory)
////            {
////                var sortMapping = new int[startAddresses.Count];

////                for (int i = 0; i < sortMapping.Length; ++i)
////                    sortMapping[i] = i;

////                var startAddr = startAddresses;
////                Array.Sort(sortMapping, (x, y) => startAddr[x].CompareTo(startAddr[y]));

////                var newSortedAddresses = new ulong[startAddresses.Count];
////                var newSortedByteArrays = new byte[startAddresses.Count][];
////                var newSortedSectionTypes = isStackMemory ? null : new MemorySectionType[startAddresses.Count];
////                var newSortedSectionNames = new string[startAddresses.Count];

////                for (long i = 0; i < startAddresses.Count; ++i)
////                {
////                    long idx = sortMapping[i];
////                    newSortedAddresses[i] = startAddresses[idx];
////                    newSortedByteArrays[i] = associatedByteArrays[idx];
////                    newSortedSectionNames[i] = associatedSectionNames[idx];

////                    if (!isStackMemory)
////                        newSortedSectionTypes[i] = associatedSectionType[idx];
////                }

////                for (long i = 0; i < startAddresses.Count; ++i)
////                {
////                    startAddresses[i] = newSortedAddresses[i];
////                    sizes[i] = (ulong)newSortedByteArrays[i].LongLength;
////                    if (!isStackMemory)
////                        associatedSectionType[i] = newSortedSectionTypes[i];
////                }
////                associatedByteArrays = newSortedByteArrays;
////                associatedSectionNames = newSortedSectionNames;
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                m_MinAddress = m_MaxAddress = 0;
////                StartAddress.Dispose();
////                SectionType.Dispose();
////                SectionSize.Dispose();
////                Bytes = null;
////            }
////        }

////        //leave this as second to last thing to convert, also a major pain in the ass
////        public class TypeDescriptionEntriesCache : IDisposable
////        {
////            public const int ITypeInvalid = -1;
////            const int k_DefaultFieldProcessingBufferSize = 64;
////            public const string UnityObjectTypeName = "UnityEngine.Object";
////            public const string UnityNativeObjectPointerFieldName = "m_CachedPtr";
////            public int IFieldUnityObjectMCachedPtr { get; private set; }
////            public int IFieldUnityObjectMCachedPtrOffset { get; private set; } = -1;

////            const string k_UnityMonoBehaviourTypeName = "UnityEngine.MonoBehaviour";
////            const string k_UnityScriptableObjectTypeName = "UnityEngine.ScriptableObject";
////            const string k_UnityComponentObjectTypeName = "UnityEngine.Component";

////            const string k_SystemObjectTypeName = "System.Object";
////            const string k_SystemValueTypeName = "System.ValueType";
////            const string k_SystemEnumTypeName = "System.Enum";

////            const string k_SystemInt16Name = "System.Int16";
////            const string k_SystemInt32Name = "System.Int32";
////            const string k_SystemInt64Name = "System.Int64";

////            const string k_SystemUInt16Name = "System.UInt16";
////            const string k_SystemUInt32Name = "System.UInt32";

////            const string k_SystemUInt64Name = "System.UInt64";
////            const string k_SystemBoolName = "System.Boolean";
////            const string k_SystemCharTypeName = "System.Char";
////            const string k_SystemDoubleName = "System.Double";
////            const string k_SystemSingleName = "System.Single";
////            const string k_SystemStringName = "System.String";
////            const string k_SystemIntPtrName = "System.IntPtr";
////            const string k_SystemByteName = "System.Byte";

////            public long Count;
////            public DynamicArray<TypeFlags> Flags = default;
////            public DynamicArray<int> BaseOrElementTypeIndex = default;
////            public DynamicArray<int> Size = default;
////            public DynamicArray<ulong> TypeInfoAddress = default;
////            public DynamicArray<int> TypeIndex = default;

////            public string[] TypeDescriptionName;
////            public string[] Assembly;
////#if !UNITY_2021_2_OR_NEWER // TODO: || QUICK_SEARCH_AVAILABLE
////            public string[] UniqueCurrentlyAvailableUnityAssemblyNames;
////#endif
////            public int[][] FieldIndices;
////            public byte[][] StaticFieldBytes;

////            //secondary data, handled inside InitSecondaryItems
////            public int[][] FieldIndicesInstance;//includes all bases' instance fields
////            public int[][] fieldIndicesStatic;  //includes all bases' static fields
////            public int[][] fieldIndicesOwnedStatic;  //includes only type's static fields
////            public bool[] HasStaticFields;

////            public int ITypeValueType { get; private set; }
////            public int ITypeUnityObject { get; private set; }
////            public int ITypeObject { get; private set; }
////            public int ITypeEnum { get; private set; }
////            public int ITypeInt16 { get; private set; }
////            public int ITypeInt32 { get; private set; }
////            public int ITypeInt64 { get; private set; }
////            public int ITypeUInt16 { get; private set; }
////            public int ITypeUInt32 { get; private set; }
////            public int ITypeUInt64 { get; private set; }
////            public int ITypeBool { get; private set; }
////            public int ITypeChar { get; private set; }
////            public int ITypeDouble { get; private set; }
////            public int ITypeSingle { get; private set; }
////            public int ITypeString { get; private set; }
////            public int ITypeIntPtr { get; private set; }
////            public int ITypeByte { get; private set; }

////            public int ITypeUnityMonoBehaviour { get; private set; }
////            public int ITypeUnityScriptableObject { get; private set; }
////            public int ITypeUnityComponent { get; private set; }
////            public Dictionary<ulong, int> TypeInfoToArrayIndex { get; private set; }
////            public Dictionary<int, int> TypeIndexToArrayIndex { get; private set; }
////            // only fully initialized after the Managed Crawler is done stitching up Objects. Might be better to be moved over to ManagedData
////            public Dictionary<int, int> UnityObjectTypeIndexToNativeTypeIndex { get; private set; }
////            public HashSet<int> PureCSharpTypeIndices { get; private set; }

////            public TypeDescriptionEntriesCache(SnapshotFileData ssfd, FieldDescriptionEntriesCache fieldDescriptions)
////            {
////                Count = ssfd.GetEntryCount(EntryType.TypeDescriptions_TypeIndex);
////                TypeDescriptionName = new string[Count];
////                Assembly = new string[Count];
////                FieldIndices = new int[Count][];
////                StaticFieldBytes = new byte[Count][];

////                if (Count == 0)
////                    return;

////                Flags = ssfd.Read(EntryType.TypeDescriptions_Flags, 0, Count).Result.Reinterpret<TypeFlags>();
////                BaseOrElementTypeIndex = ssfd.Read(EntryType.TypeDescriptions_BaseOrElementTypeIndex, 0, Count).Result.Reinterpret<int>();
////                Size = ssfd.Read(EntryType.TypeDescriptions_Size, 0, Count).Result.Reinterpret<int>();
////                TypeInfoAddress = ssfd.Read(EntryType.TypeDescriptions_TypeInfoAddress, 0, Count).Result.Reinterpret<ulong>();
////                TypeIndex = ssfd.Read(EntryType.TypeDescriptions_TypeIndex, 0, Count).Result.Reinterpret<int>();

////                using (DynamicArray<byte> tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpSize = ssfd.GetSizeForEntryRange(EntryType.TypeDescriptions_Name, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.TypeDescriptions_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref TypeDescriptionName);

////                    tmpSize = ssfd.GetSizeForEntryRange(EntryType.TypeDescriptions_Assembly, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.TypeDescriptions_Assembly, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref Assembly);

////                    tmpSize = ssfd.GetSizeForEntryRange(EntryType.TypeDescriptions_FieldIndices, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.TypeDescriptions_FieldIndices, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref FieldIndices);

////                    tmpSize = ssfd.GetSizeForEntryRange(EntryType.TypeDescriptions_StaticFieldBytes, 0, Count);
////                    tmp.Resize(tmpSize, false);
////                    ssfd.Read(EntryType.TypeDescriptions_StaticFieldBytes, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref StaticFieldBytes);
////                }

////                //change to consume field descriptions instead
////                InitSecondaryItems(this, fieldDescriptions);
////            }

////            // Check all bases' fields
////            public bool HasAnyField(int iType)
////            {
////                return FieldIndicesInstance[iType].Length > 0 || fieldIndicesStatic[iType].Length > 0;
////            }

////            // Check all bases' fields
////            public bool HasAnyStaticField(int iType)
////            {
////                return fieldIndicesStatic[iType].Length > 0;
////            }

////            // Check only the type's fields
////            public bool HasStaticField(int iType)
////            {
////                return HasStaticFields[iType];
////            }

////            public bool HasFlag(int arrayIndex, TypeFlags flag)
////            {
////                return (Flags[arrayIndex] & flag) == flag;
////            }

////            public int GetRank(int arrayIndex)
////            {
////                int r = (int)(Flags[arrayIndex] & TypeFlags.kArrayRankMask) >> 16;
////                return r;
////            }

////            public int TypeIndex2ArrayIndex(int typeIndex)
////            {
////                int i;
////                if (!TypeIndexToArrayIndex.TryGetValue(typeIndex, out i))
////                {
////                    throw new Exception("typeIndex not found");
////                }
////                return i;
////            }

////            public int TypeInfo2ArrayIndex(UInt64 aTypeInfoAddress)
////            {
////                int i;

////                if (!TypeInfoToArrayIndex.TryGetValue(aTypeInfoAddress, out i))
////                {
////                    return -1;
////                }
////                return i;
////            }

////            static ProfilerMarker typeFieldArraysBuild = new ProfilerMarker("MemoryProfiler.TypeFields.TypeFieldArrayBuilding");
////            void InitSecondaryItems(TypeDescriptionEntriesCache typeDescriptionEntries, FieldDescriptionEntriesCache fieldDescriptions)
////            {
////                TypeInfoToArrayIndex = Enumerable.Range(0, (int)TypeInfoAddress.Count).ToDictionary(x => TypeInfoAddress[x], x => x);
////                TypeIndexToArrayIndex = Enumerable.Range(0, (int)TypeIndex.Count).ToDictionary(x => TypeIndex[x], x => x);
////                UnityObjectTypeIndexToNativeTypeIndex = new Dictionary<int, int>();
////                PureCSharpTypeIndices = new HashSet<int>();


////                ITypeUnityObject = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == UnityObjectTypeName)];
////#if DEBUG_VALIDATION //This shouldn't really happen
////                if (ITypeUnityObject < 0)
////                {
////                    throw new Exception("Unable to find UnityEngine.Object");
////                }
////#endif

////                using (typeFieldArraysBuild.Auto())
////                {
////                    HasStaticFields = new bool[Count];
////                    FieldIndicesInstance = new int[Count][];
////                    fieldIndicesStatic = new int[Count][];
////                    fieldIndicesOwnedStatic = new int[Count][];
////                    List<int> fieldProcessingBuffer = new List<int>(k_DefaultFieldProcessingBufferSize);

////                    for (int i = 0; i < Count; ++i)
////                    {
////                        HasStaticFields[i] = false;
////                        foreach (var iField in FieldIndices[i])
////                        {
////                            if (fieldDescriptions.IsStatic[iField] == 1)
////                            {
////                                HasStaticFields[i] = true;
////                                break;
////                            }
////                        }

////                        TypeTools.AllFieldArrayIndexOf(ref fieldProcessingBuffer, i, typeDescriptionEntries, fieldDescriptions, TypeTools.FieldFindOptions.OnlyInstance, true);
////                        FieldIndicesInstance[i] = fieldProcessingBuffer.ToArray();

////                        TypeTools.AllFieldArrayIndexOf(ref fieldProcessingBuffer, i, typeDescriptionEntries, fieldDescriptions, TypeTools.FieldFindOptions.OnlyStatic, true);
////                        fieldIndicesStatic[i] = fieldProcessingBuffer.ToArray();

////                        TypeTools.AllFieldArrayIndexOf(ref fieldProcessingBuffer, i, typeDescriptionEntries, fieldDescriptions, TypeTools.FieldFindOptions.OnlyStatic, false);
////                        fieldIndicesOwnedStatic[i] = fieldProcessingBuffer.ToArray();

////                        var typeIndex = typeDescriptionEntries.TypeIndex[i];
////                        if (DerivesFromUnityObject(typeIndex))
////                            UnityObjectTypeIndexToNativeTypeIndex.Add(typeIndex, -1);
////                        else
////                            PureCSharpTypeIndices.Add(typeIndex);
////                    }
////                }

////                var fieldIndicesIndex = Array.FindIndex(
////                    typeDescriptionEntries.FieldIndices[TypeIndexToArrayIndex[ITypeUnityObject]]
////                    , iField => fieldDescriptions.FieldDescriptionName[iField] == UnityNativeObjectPointerFieldName);

////                IFieldUnityObjectMCachedPtr = fieldIndicesIndex >= 0 ? typeDescriptionEntries.FieldIndices[ITypeUnityObject][fieldIndicesIndex] : -1;

////                IFieldUnityObjectMCachedPtrOffset = -1;

////                if (IFieldUnityObjectMCachedPtr >= 0)
////                {
////                    IFieldUnityObjectMCachedPtrOffset = fieldDescriptions.Offset[IFieldUnityObjectMCachedPtr];
////                }

////#if DEBUG_VALIDATION
////                if (IFieldUnityObjectMCachedPtrOffset < 0)
////                {
////                    Debug.LogWarning("Could not find unity object instance id field or m_CachedPtr");
////                    return;
////                }
////#endif
////                ITypeValueType = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemValueTypeName)];
////                ITypeObject = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemObjectTypeName)];
////                ITypeEnum = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemEnumTypeName)];
////                ITypeChar = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemCharTypeName)];
////                ITypeInt16 = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemInt16Name)];
////                ITypeInt32 = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemInt32Name)];
////                ITypeInt64 = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemInt64Name)];
////                ITypeIntPtr = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemIntPtrName)];
////                ITypeString = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemStringName)];
////                ITypeBool = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemBoolName)];
////                ITypeSingle = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemSingleName)];
////                ITypeByte = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemByteName)];
////                ITypeDouble = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemDoubleName)];
////                ITypeUInt16 = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemUInt16Name)];
////                ITypeUInt32 = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemUInt32Name)];
////                ITypeUInt64 = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_SystemUInt64Name)];

////                ITypeUnityMonoBehaviour = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_UnityMonoBehaviourTypeName)];
////                ITypeUnityScriptableObject = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_UnityScriptableObjectTypeName)];
////                ITypeUnityComponent = TypeIndex[Array.FindIndex(TypeDescriptionName, x => x == k_UnityComponentObjectTypeName)];

////#if !UNITY_2021_2_OR_NEWER // TODO: || QUICK_SEARCH_AVAILABLE
////                var uniqueCurrentlyAvailableUnityAssemblyNames = new List<string>();
////                var assemblyHashSet = new HashSet<string>();
////                foreach (var assembly in Assembly)
////                {
////                    if (assemblyHashSet.Contains(assembly))
////                        continue;
////                    assemblyHashSet.Add(assembly);
////                    if (assembly.StartsWith("Unity"))
////                    {
////                        try
////                        {
////                            System.Reflection.Assembly.Load(assembly);
////                        }
////                        catch (Exception)
////                        {
////                            // only add assemblies currently available
////                            continue;
////                        }
////                        uniqueCurrentlyAvailableUnityAssemblyNames.Add(assembly);
////                    }
////                }
////                UniqueCurrentlyAvailableUnityAssemblyNames = uniqueCurrentlyAvailableUnityAssemblyNames.ToArray();
////#endif
////            }

////            public bool DerivesFromUnityObject(int iTypeDescription)
////            {
////                while (iTypeDescription != ITypeUnityObject && iTypeDescription >= 0)
////                {
////                    if (HasFlag(iTypeDescription, TypeFlags.kArray))
////                        return false;
////                    iTypeDescription = BaseOrElementTypeIndex[iTypeDescription];
////                }
////                return iTypeDescription == ITypeUnityObject;
////            }

////            public bool DerivesFrom(int iTypeDescription, int potentialBase, bool excludeArrayElementBaseTypes)
////            {
////                while (iTypeDescription != potentialBase && iTypeDescription >= 0)
////                {
////                    if (excludeArrayElementBaseTypes && HasFlag(iTypeDescription, TypeFlags.kArray))
////                        return false;
////                    iTypeDescription = BaseOrElementTypeIndex[iTypeDescription];
////                }

////                return iTypeDescription == potentialBase;
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                Flags.Dispose();
////                BaseOrElementTypeIndex.Dispose();
////                Size.Dispose();
////                TypeInfoAddress.Dispose();
////                TypeIndex.Dispose();
////                TypeDescriptionName = null;
////                Assembly = null;
////                FieldIndices = null;
////                StaticFieldBytes = null;

////                FieldIndicesInstance = null;
////                fieldIndicesStatic = null;
////                fieldIndicesOwnedStatic = null;
////                HasStaticFields = null;
////                ITypeValueType = ITypeInvalid;
////                ITypeObject = ITypeInvalid;
////                ITypeEnum = ITypeInvalid;
////                TypeInfoToArrayIndex = null;
////                TypeIndexToArrayIndex = null;
////                UnityObjectTypeIndexToNativeTypeIndex = null;
////                PureCSharpTypeIndices = null;
////            }
////        }

////        public class FieldDescriptionEntriesCache : IDisposable
////        {
////            public long Count;
////            public string[] FieldDescriptionName;
////            public DynamicArray<int> Offset = default;
////            public DynamicArray<int> TypeIndex = default;
////            public DynamicArray<byte> IsStatic = default;

////            unsafe public FieldDescriptionEntriesCache(SnapshotFileData ssfd)
////            {
////                Count = ssfd.GetEntryCount(EntryType.FieldDescriptions_Name);
////                FieldDescriptionName = new string[Count];

////                if (Count == 0)
////                    return;

////                Offset = new DynamicArray<int>(Count);
////                TypeIndex = new DynamicArray<int>(Count);
////                IsStatic = new DynamicArray<byte>(Count);

////                Offset = ssfd.Read(EntryType.FieldDescriptions_Offset, 0, Count).Result.Reinterpret<int>();
////                TypeIndex = ssfd.Read(EntryType.FieldDescriptions_TypeIndex, 0, Count).Result.Reinterpret<int>();
////                IsStatic = ssfd.Read(EntryType.FieldDescriptions_IsStatic, 0, Count).Result.Reinterpret<byte>();

////                using (var tmp = new DynamicArray<byte>(0))
////                {
////                    var tmpBufferSize = ssfd.GetSizeForEntryRange(EntryType.FieldDescriptions_Name, 0, Count);
////                    tmp.Resize(tmpBufferSize, false);
////                    ssfd.Read(EntryType.FieldDescriptions_Name, tmp, 0, Count);
////                    ConvertDynamicArrayByteBufferToManagedArray(tmp, ref FieldDescriptionName);
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                Offset.Dispose();
////                TypeIndex.Dispose();
////                IsStatic.Dispose();
////                FieldDescriptionName = null;
////            }
////        }

////        public class GCHandleEntriesCache : IDisposable
////        {
////            public DynamicArray<ulong> Target = default;
////            public long Count;

////            public GCHandleEntriesCache(SnapshotFileData ssfd)
////            {
////                unsafe
////                {
////                    Count = ssfd.GetEntryCount(EntryType.GCHandles_Target);
////                    if (Count == 0)
////                        return;

////                    Target = ssfd.Read(EntryType.GCHandles_Target, 0, Count).Result.Reinterpret<ulong>();
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                Target.Dispose();
////            }
////        }

////        public class ConnectionEntriesCache : IDisposable
////        {
////            public long Count;
////            public DynamicArray<int> From { private set; get; }
////            public DynamicArray<int> To { private set; get; }
////            // ToFromMappedConnection and FromToMappedConnections are derived data used to accelarate searches in the details panel
////            public Dictionary<int, List<int>> ToFromMappedConnection { get; private set; } = new Dictionary<int, List<int>>();
////            public Dictionary<int, List<int>> FromToMappedConnection { get; private set; } = new Dictionary<int, List<int>>();
////#if DEBUG_VALIDATION // could be always present but currently only used for validation in the crawler
////            public long IndexOfFirstNativeToGCHandleConnection = -1;
////#endif

////            unsafe public ConnectionEntriesCache(SnapshotFileData ssfd, NativeObjectEntriesCache nativeObjects, long gcHandlesCount, bool connectionsNeedRemaping)
////            {
////                Count = ssfd.GetEntryCount(EntryType.Connections_From);
////                From = new DynamicArray<int>(Count);
////                To = new DynamicArray<int>(Count);

////                if (Count == 0)
////                    return;

////                From = ssfd.Read(EntryType.Connections_From, 0, Count).Result.Reinterpret<int>();
////                To = ssfd.Read(EntryType.Connections_To, 0, Count).Result.Reinterpret<int>();

////                if (connectionsNeedRemaping)
////                {
////                    var instanceIds = nativeObjects.InstanceId;
////                    var gchandlesIndices = nativeObjects.ManagedObjectIndex;

////                    Dictionary<int, int> instanceIDToIndex = new Dictionary<int, int>();
////                    Dictionary<int, int> instanceIDToGcHandleIndex = new Dictionary<int, int>();

////                    for (int i = 0; i < instanceIds.Count; ++i)
////                    {
////                        if (gchandlesIndices[i] != -1)
////                        {
////                            instanceIDToGcHandleIndex.Add(instanceIds[i], gchandlesIndices[i]);
////                        }
////                        instanceIDToIndex.Add(instanceIds[i], i);
////                    }
////#if DEBUG_VALIDATION
////                    if (instanceIDToGcHandleIndex.Count > 0)
////                        IndexOfFirstNativeToGCHandleConnection = Count;
////#endif

////                    DynamicArray<int> fromRemap = new DynamicArray<int>(Count + instanceIDToGcHandleIndex.Count);
////                    DynamicArray<int> toRemap = new DynamicArray<int>(fromRemap.Count);

////                    // add all Native to Native connections.
////                    // The indexes they link to are all bigger than gcHandlesCount.
////                    // Such indices in the From/To arrays indicate a link from a native object to a native object
////                    // and subtracting gcHandlesCount from them gives the Native Object index
////                    for (long i = 0; i < Count; ++i)
////                    {
////                        fromRemap[i] = (int)(gcHandlesCount + instanceIDToIndex[From[i]]);
////                        toRemap[i] = (int)(gcHandlesCount + instanceIDToIndex[To[i]]);
////                    }

////                    //dispose of original data
////                    To.Dispose();
////                    From.Dispose();

////                    var enumerator = instanceIDToGcHandleIndex.GetEnumerator();
////                    for (long i = Count; i < fromRemap.Count; ++i)
////                    {
////                        enumerator.MoveNext();
////                        fromRemap[i] = (int)(gcHandlesCount + instanceIDToIndex[enumerator.Current.Key]);
////                        // elements in To that are `To[i] < gcHandlesCount` are indexes into the GCHandles list
////                        toRemap[i] = enumerator.Current.Value;
////                    }

////                    From = fromRemap;
////                    To = toRemap;
////                    Count = From.Count;

////                    for (int i = 0; i < Count; i++)
////                    {
////                        if (ToFromMappedConnection.TryGetValue(To[i], out var fromList))
////                            fromList.Add(From[i]);
////                        else
////                            ToFromMappedConnection[To[i]] = new List<int> { From[i] };


////                        if (FromToMappedConnection.TryGetValue(From[i], out var toList))
////                            toList.Add(To[i]);
////                        else
////                            FromToMappedConnection[From[i]] = new List<int> { To[i] };
////                    }
////                }
////            }

////            public void Dispose()
////            {
////                Count = 0;
////                From.Dispose();
////                To.Dispose();
////            }
////        }
////        internal struct ManagedConnection
////        {
////            public enum ConnectionType
////            {
////                ManagedObject_To_ManagedObject,
////                ManagedType_To_ManagedObject,
////                UnityEngineObject,
////            }
////            public ManagedConnection(ConnectionType t, int from, int to, int fieldFrom, int arrayIndexFrom)
////            {
////                connectionType = t;
////                index0 = from;
////                index1 = to;
////                this.fieldFrom = fieldFrom;
////                this.arrayIndexFrom = arrayIndexFrom;
////            }

////            private int index0;
////            private int index1;

////            public int fieldFrom;
////            public int arrayIndexFrom;

////            public ConnectionType connectionType;
////            public long GetUnifiedIndexFrom(SnapshotFileData snapshot)
////            {
////                switch (connectionType)
////                {
////                    case ConnectionType.ManagedObject_To_ManagedObject:
////                        return snapshot.ManagedObjectIndexToUnifiedObjectIndex(index0);
////                    case ConnectionType.ManagedType_To_ManagedObject:
////                        return index0;
////                    case ConnectionType.UnityEngineObject:
////                        return snapshot.NativeObjectIndexToUnifiedObjectIndex(index0);
////                    default:
////                        return -1;
////                }
////            }

////            public long GetUnifiedIndexTo(SnapshotFileData snapshot)
////            {
////                switch (connectionType)
////                {
////                    case ConnectionType.ManagedObject_To_ManagedObject:
////                    case ConnectionType.ManagedType_To_ManagedObject:
////                    case ConnectionType.UnityEngineObject:
////                        return snapshot.ManagedObjectIndexToUnifiedObjectIndex(index1);
////                    default:
////                        return -1;
////                }
////            }

////            public int fromManagedObjectIndex
////            {
////                get
////                {
////                    switch (connectionType)
////                    {
////                        case ConnectionType.ManagedObject_To_ManagedObject:
////                        case ConnectionType.ManagedType_To_ManagedObject:
////                            return index0;
////                    }
////                    return -1;
////                }
////            }
////            public int toManagedObjectIndex
////            {
////                get
////                {
////                    switch (connectionType)
////                    {
////                        case ConnectionType.ManagedObject_To_ManagedObject:
////                        case ConnectionType.ManagedType_To_ManagedObject:
////                            return index1;
////                    }
////                    return -1;
////                }
////            }

////            public int fromManagedType
////            {
////                get
////                {
////                    if (connectionType == ConnectionType.ManagedType_To_ManagedObject)
////                    {
////                        return index0;
////                    }
////                    return -1;
////                }
////            }
////            public int UnityEngineNativeObjectIndex
////            {
////                get
////                {
////                    if (connectionType == ConnectionType.UnityEngineObject)
////                    {
////                        return index0;
////                    }
////                    return -1;
////                }
////            }
////            public int UnityEngineManagedObjectIndex
////            {
////                get
////                {
////                    if (connectionType == ConnectionType.UnityEngineObject)
////                    {
////                        return index1;
////                    }
////                    return -1;
////                }
////            }
////            public static ManagedConnection MakeUnityEngineObjectConnection(int NativeIndex, int ManagedIndex)
////            {
////                return new ManagedConnection(ConnectionType.UnityEngineObject, NativeIndex, ManagedIndex, 0, 0);
////            }

////            public static ManagedConnection MakeConnection(SnapshotFileData snapshot, int fromIndex, ulong fromPtr, int toIndex, ulong toPtr, int fromTypeIndex, int fromField, int fieldArrayIndexFrom)
////            {
////                if (fromIndex >= 0)
////                {
////                    //from an object
////#if DEBUG_VALIDATION
////                if (fromField >= 0)
////                {
////                    if (snapshot.FieldDescriptions.IsStatic[fromField] == 1)
////                    {
////                        Debug.LogError("Cannot make a connection from an object using a static field.");
////                    }
////                }
////#endif
////                    return new ManagedConnection(ConnectionType.ManagedObject_To_ManagedObject, fromIndex, toIndex, fromField, fieldArrayIndexFrom);
////                }
////                else if (fromTypeIndex >= 0)
////                {
////                    //from a type static data
////#if DEBUG_VALIDATION
////                if (fromField >= 0)
////                {
////                    if (snapshot.FieldDescriptions.IsStatic[fromField] == 0)
////                    {
////                        Debug.LogError("Cannot make a connection from a type using a non-static field.");
////                    }
////                }
////#endif
////                    return new ManagedConnection(ConnectionType.ManagedType_To_ManagedObject, fromTypeIndex, toIndex, fromField, fieldArrayIndexFrom);
////                }
////                else
////                {
////                    throw new InvalidOperationException("Tried to add a Managed Connection without a valid source.");
////                }
////            }
////        }

////#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
////#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
////        internal struct ManagedObjectInfo
////#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
////#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
////        {
////            public ulong PtrObject;
////            public ulong PtrTypeInfo;
////            public int NativeObjectIndex;
////            public int ManagedObjectIndex;
////            public int ITypeDescription;
////            public int Size;
////            public int RefCount;

////            public bool IsKnownType()
////            {
////                return ITypeDescription >= 0;
////            }

////            public BytesAndOffset data;

////            public bool IsValid()
////            {
////                return PtrObject != 0 && PtrTypeInfo != 0 && data.bytes != null;
////            }

////            public static bool operator ==(ManagedObjectInfo lhs, ManagedObjectInfo rhs)
////            {
////                return lhs.PtrObject == rhs.PtrObject
////                    && lhs.PtrTypeInfo == rhs.PtrTypeInfo
////                    && lhs.NativeObjectIndex == rhs.NativeObjectIndex
////                    && lhs.ManagedObjectIndex == rhs.ManagedObjectIndex
////                    && lhs.ITypeDescription == rhs.ITypeDescription
////                    && lhs.Size == rhs.Size
////                    && lhs.RefCount == rhs.RefCount;
////            }

////            public static bool operator !=(ManagedObjectInfo lhs, ManagedObjectInfo rhs)
////            {
////                return !(lhs == rhs);
////            }
////        }

////        internal class ManagedData
////        {
////            public bool Crawled { private set; get; }
////            const int k_ManagedObjectBlockSize = 32768;
////            const int k_ManagedConnectionsBlockSize = 65536;
////            public BlockList<ManagedObjectInfo> ManagedObjects { private set; get; }
////            public Dictionary<ulong, int> MangedObjectIndexByAddress { private set; get; }
////            public BlockList<ManagedConnection> Connections { private set; get; }
////            public Dictionary<int, int> NativeUnityObjectTypeIndexToManagedBaseTypeIndex { get; private set; }
////            public ulong ManagedObjectMemoryUsage { private set; get; }
////            public ulong AbandonedManagedObjectMemoryUsage { private set; get; }
////            public ulong ActiveHeapMemoryUsage { private set; get; }
////            public ulong ActiveHeapMemoryEmptySpace { private set; get; }
////            public ulong AbandonedManagedObjectActiveHeapMemoryUsage { private set; get; }
////            // ConnectionsMappedToUnifiedIndex and ConnectionsMappedToNativeIndex are derived structure used in accelerating searches in the details view
////            public Dictionary<long, List<int>> ConnectionsToMappedToUnifiedIndex { private set; get; } = new Dictionary<long, List<int>>();
////            public Dictionary<long, List<int>> ConnectionsFromMappedToUnifiedIndex { private set; get; } = new Dictionary<long, List<int>>();
////            public Dictionary<long, List<int>> ConnectionsMappedToNativeIndex { private set; get; } = new Dictionary<long, List<int>>();


////            public ManagedData(long rawGcHandleCount, long rawConnectionsCount)
////            {
////                //compute initial block counts for larger snapshots
////                ManagedObjects = new BlockList<ManagedObjectInfo>(k_ManagedObjectBlockSize, rawGcHandleCount);
////                Connections = new BlockList<ManagedConnection>(k_ManagedConnectionsBlockSize, rawConnectionsCount);

////                MangedObjectIndexByAddress = new Dictionary<ulong, int>();
////                NativeUnityObjectTypeIndexToManagedBaseTypeIndex = new Dictionary<int, int>();
////            }

////            internal void AddUpTotalMemoryUsage(CachedSnapshot.ManagedMemorySectionEntriesCache managedMemorySections)
////            {
////                var totalManagedObjectsCount = ManagedObjects.Count;
////                ManagedObjectMemoryUsage = 0;
////                if (managedMemorySections.Count <= 0)
////                {
////                    ActiveHeapMemoryUsage = AbandonedManagedObjectMemoryUsage = 0;

////                    return;
////                }

////                var activeHeapSectionStartAddress = managedMemorySections.StartAddress[managedMemorySections.FirstAssumedActiveHeapSectionIndex];
////                var activeHeapSectionEndAddress = managedMemorySections.StartAddress[managedMemorySections.LastAssumedActiveHeapSectionIndex] + managedMemorySections.SectionSize[managedMemorySections.LastAssumedActiveHeapSectionIndex];
////                for (int i = 0; i < totalManagedObjectsCount; i++)
////                {
////                    var size = (ulong)ManagedObjects[i].Size;
////                    ManagedObjectMemoryUsage += size;
////                    if (ManagedObjects[i].RefCount == 0)
////                        AbandonedManagedObjectMemoryUsage += size;

////                    if (ManagedObjects[i].PtrObject > activeHeapSectionStartAddress && ManagedObjects[i].PtrObject < activeHeapSectionEndAddress)
////                    {
////                        ActiveHeapMemoryUsage += size;
////                        if (ManagedObjects[i].RefCount == 0)
////                            AbandonedManagedObjectActiveHeapMemoryUsage += size;
////                    }
////                }
////                ActiveHeapMemoryEmptySpace = managedMemorySections.StartAddress[managedMemorySections.LastAssumedActiveHeapSectionIndex]
////                    + managedMemorySections.SectionSize[managedMemorySections.LastAssumedActiveHeapSectionIndex]
////                    - managedMemorySections.StartAddress[managedMemorySections.FirstAssumedActiveHeapSectionIndex]
////                    - ActiveHeapMemoryUsage;
////            }

////            internal void FinishedCrawling()
////            {
////                Crawled = true;
////            }

////            public void CreateConnectionMaps(CachedSnapshot cs)
////            {
////                for (var i = 0; i < Connections.Count; i++)
////                {
////                    var key = Connections[i].GetUnifiedIndexTo(cs);
////                    if (ConnectionsToMappedToUnifiedIndex.TryGetValue(key, out var unifiedIndexList))
////                        unifiedIndexList.Add(i);
////                    else
////                        ConnectionsToMappedToUnifiedIndex[key] = new List<int> { i };
////                }

////                for (var i = 0; i < Connections.Count; i++)
////                {
////                    var key = Connections[i].GetUnifiedIndexFrom(cs);
////                    if (ConnectionsFromMappedToUnifiedIndex.TryGetValue(key, out var unifiedIndexList))
////                        unifiedIndexList.Add(i);
////                    else
////                        ConnectionsFromMappedToUnifiedIndex[key] = new List<int> { i };
////                }

////                for (var i = 0; i < Connections.Count; i++)
////                {
////                    var key = Connections[i].UnityEngineNativeObjectIndex;
////                    if (ConnectionsMappedToNativeIndex.TryGetValue(key, out var nativeObjectList))
////                        nativeObjectList.Add(i);
////                    else
////                        ConnectionsMappedToNativeIndex[key] = new List<int> { i };
////                }
////            }
////        }

////        internal struct BytesAndOffset
////        {
////            public byte[] bytes;
////            public int offset;
////            public int pointerSize;
////            public bool IsValid { get { return bytes != null; } }
////            public BytesAndOffset(byte[] bytes, int pointerSize)
////            {
////                this.bytes = bytes;
////                this.pointerSize = pointerSize;
////                offset = 0;
////            }

////            public enum PtrReadError
////            {
////                Success,
////                OutOfBounds,
////                InvalidPtrSize
////            }

////            public PtrReadError TryReadPointer(out ulong ptr)
////            {
////                ptr = unchecked(0xffffffffffffffff);

////                if (offset + pointerSize > bytes.Length)
////                    return PtrReadError.OutOfBounds;

////                switch (pointerSize)
////                {
////                    case VMTools.X64ArchPtrSize:
////                        ptr = BitConverter.ToUInt64(bytes, offset);
////                        return PtrReadError.Success;
////                    case VMTools.X86ArchPtrSize:
////                        ptr = BitConverter.ToUInt32(bytes, offset);
////                        return PtrReadError.Success;
////                    default: //should never happen
////                        return PtrReadError.InvalidPtrSize;
////                }
////            }

////            public byte ReadByte()
////            {
////                return bytes[offset];
////            }

////            public short ReadInt16()
////            {
////                return BitConverter.ToInt16(bytes, offset);
////            }

////            public Int32 ReadInt32()
////            {
////                return BitConverter.ToInt32(bytes, offset);
////            }

////            public Int32 ReadInt32(int additionalOffset)
////            {
////                return BitConverter.ToInt32(bytes, offset + additionalOffset);
////            }

////            public Int64 ReadInt64()
////            {
////                return BitConverter.ToInt64(bytes, offset);
////            }

////            public ushort ReadUInt16()
////            {
////                return BitConverter.ToUInt16(bytes, offset);
////            }

////            public uint ReadUInt32()
////            {
////                return BitConverter.ToUInt32(bytes, offset);
////            }

////            public ulong ReadUInt64()
////            {
////                return BitConverter.ToUInt64(bytes, offset);
////            }

////            public bool ReadBoolean()
////            {
////                return BitConverter.ToBoolean(bytes, offset);
////            }

////            public char ReadChar()
////            {
////                return BitConverter.ToChar(bytes, offset);
////            }

////            public double ReadDouble()
////            {
////                return BitConverter.ToDouble(bytes, offset);
////            }

////            public float ReadSingle()
////            {
////                return BitConverter.ToSingle(bytes, offset);
////            }

////            public string ReadString(out int fullLength)
////            {
////                var readLength = fullLength = ReadInt32();
////                var additionalOffsetForObjectHeader = 0;
////                if (fullLength < 0 || (long)offset + (long)sizeof(int) + ((long)fullLength * (long)2) > bytes.Length)
////                {
////                    // Why is the header not included for object data in the tables?
////                    // this workaround here is flakey!
////                    additionalOffsetForObjectHeader = 16;
////                    readLength = fullLength = ReadInt32(additionalOffsetForObjectHeader);

////                    if (fullLength < 0 || (long)offset + (long)sizeof(int) + ((long)fullLength * (long)2) > bytes.Length)
////                    {
////#if DEBUG_VALIDATION
////                    Debug.LogError("Attempted to read outside of binary buffer.");
////#endif
////                        return "Invalid String object, " + TextContent.InvalidObjectPleaseReportABugMessage;
////                    }
////                    // find out what causes this and fix it, then remove the additionalOffsetForObjectHeader workaround
////#if DEBUG_VALIDATION
////                Debug.LogError("String reading is broken.");
////#endif
////                }
////                if (fullLength > StringTools.MaxStringLengthToRead)
////                {
////                    readLength = StringTools.MaxStringLengthToRead;
////                    readLength += StringTools.Elipsis.Length;
////                }
////                unsafe
////                {
////                    fixed (byte* ptr = bytes)
////                    {
////                        string str = null;
////                        char* begin = (char*)(ptr + (offset + additionalOffsetForObjectHeader + sizeof(int)));
////                        str = new string(begin, 0, readLength);
////                        if (fullLength != readLength)
////                        {
////                            fixed (char* s = str, e = StringTools.Elipsis)
////                            {
////                                var c = s;
////                                c += readLength - StringTools.Elipsis.Length;
////                                UnsafeUtility.MemCpy(c, e, StringTools.Elipsis.Length);
////                            }
////                        }
////                        return str;
////                    }
////                }
////            }

////            public BytesAndOffset Add(int add)
////            {
////                return new BytesAndOffset() { bytes = bytes, offset = offset + add, pointerSize = pointerSize };
////            }

////            public void WritePointer(UInt64 value)
////            {
////                for (int i = 0; i < pointerSize; i++)
////                {
////                    bytes[i + offset] = (byte)value;
////                    value >>= 8;
////                }
////            }

////            public BytesAndOffset NextPointer()
////            {
////                return Add(pointerSize);
////            }
////        }

////        internal static class Crawler
////        {
////            internal struct StackCrawlData
////            {
////                public ulong ptr;
////                public ulong ptrFrom;
////                public int typeFrom;
////                public int indexOfFrom;
////                public int fieldFrom;
////                public int fromArrayIndex;
////            }

////            class IntermediateCrawlData
////            {
////                public List<int> TypesWithStaticFields { private set; get; }
////                public Stack<StackCrawlData> CrawlDataStack { private set; get; }
////                public BlockList<ManagedObjectInfo> ManagedObjectInfos { get { return CachedMemorySnapshot.CrawledData.ManagedObjects; } }
////                public BlockList<ManagedConnection> ManagedConnections { get { return CachedMemorySnapshot.CrawledData.Connections; } }
////                public CachedSnapshot CachedMemorySnapshot { private set; get; }
////                public Stack<int> DuplicatedGCHandleTargetsStack { private set; get; }
////                public ulong TotalManagedObjectMemoryUsage { set; get; }
////                const int kInitialStackSize = 256;
////                public IntermediateCrawlData(SnapshotFileData snapshot)
////                {
////                    DuplicatedGCHandleTargetsStack = new Stack<int>(kInitialStackSize);
////                    CachedMemorySnapshot = snapshot;
////                    CrawlDataStack = new Stack<StackCrawlData>();

////                    TypesWithStaticFields = new List<int>();
////                    for (long i = 0; i != snapshot.TypeDescriptions.Count; ++i)
////                    {
////                        if (snapshot.TypeDescriptions.StaticFieldBytes[i] != null
////                            && snapshot.TypeDescriptions.StaticFieldBytes[i].Length > 0)
////                        {
////                            TypesWithStaticFields.Add(snapshot.TypeDescriptions.TypeIndex[i]);
////                        }
////                    }
////                }
////            }

////            static void GatherIntermediateCrawlData(SnapshotFileData snapshot, IntermediateCrawlData crawlData)
////            {
////                unsafe
////                {
////                    var uniqueHandlesPtr = (ulong*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<ulong>() * snapshot.GcHandles.Count, UnsafeUtility.AlignOf<ulong>(), Collections.Allocator.Temp);

////                    ulong* uniqueHandlesBegin = uniqueHandlesPtr;
////                    int writtenRange = 0;

////                    // Parse all handles
////                    for (int i = 0; i != snapshot.GcHandles.Count; i++)
////                    {
////                        var moi = new ManagedObjectInfo();
////                        var target = snapshot.GcHandles.Target[i];

////                        moi.ManagedObjectIndex = i;
////                        moi.ITypeDescription = -1;

////                        //this can only happen pre 19.3 scripting snapshot implementations where we dumped all handle targets but not the handles.
////                        //Eg: multiple handles can have the same target. Future facing we need to start adding that as we move forward
////                        if (snapshot.CrawledData.MangedObjectIndexByAddress.ContainsKey(target))
////                        {
////                            moi.PtrObject = target;
////                            crawlData.DuplicatedGCHandleTargetsStack.Push(i);
////                        }
////                        else
////                        {
////                            snapshot.CrawledData.MangedObjectIndexByAddress.Add(target, moi.ManagedObjectIndex);
////                            *(uniqueHandlesBegin++) = target;
////                            ++writtenRange;
////                        }

////                        crawlData.ManagedObjectInfos.Add(moi);
////                    }
////                    uniqueHandlesBegin = uniqueHandlesPtr; //reset iterator
////                    ulong* uniqueHandlesEnd = uniqueHandlesPtr + writtenRange;
////                    //add handles for processing
////                    while (uniqueHandlesBegin != uniqueHandlesEnd)
////                    {
////                        crawlData.CrawlDataStack.Push(new StackCrawlData { ptr = UnsafeUtility.ReadArrayElement<ulong>(uniqueHandlesBegin++, 0), ptrFrom = 0, typeFrom = -1, indexOfFrom = -1, fieldFrom = -1, fromArrayIndex = -1 });
////                    }
////                    UnsafeUtility.Free(uniqueHandlesPtr, Collections.Allocator.Temp);
////                }
////            }

////            public static IEnumerator Crawl(SnapshotFileData snapshot)
////            {
////                const int stepCount = 5;
////                var status = new EnumerationUtilities.EnumerationStatus(stepCount);

////                IntermediateCrawlData crawlData = new IntermediateCrawlData(snapshot);

////                //Gather handles and duplicates
////                status.StepStatus = "Gathering snapshot managed data.";
////                yield return status;
////                GatherIntermediateCrawlData(snapshot, crawlData);

////                //crawl handle data
////                status.IncrementStep();
////                status.StepStatus = "Crawling GC handles.";
////                yield return status;
////                while (crawlData.CrawlDataStack.Count > 0)
////                {
////                    CrawlPointer(crawlData);
////                }

////                //crawl data pertaining to types with static fields and enqueue any heap objects
////                status.IncrementStep();
////                status.StepStatus = "Crawling data types with static fields";
////                yield return status;
////                for (int i = 0; i < crawlData.TypesWithStaticFields.Count; i++)
////                {
////                    var iTypeDescription = crawlData.TypesWithStaticFields[i];
////                    var bytesOffset = new BytesAndOffset { bytes = snapshot.TypeDescriptions.StaticFieldBytes[iTypeDescription], offset = 0, pointerSize = snapshot.VirtualMachineInformation.PointerSize };
////                    CrawlRawObjectData(crawlData, bytesOffset, iTypeDescription, true, 0, -1);
////                }

////                //crawl handles belonging to static instances
////                status.IncrementStep();
////                status.StepStatus = "Crawling static instances heap data.";
////                yield return status;
////                while (crawlData.CrawlDataStack.Count > 0)
////                {
////                    CrawlPointer(crawlData);
////                }

////                //copy crawled object source data for duplicate objects
////                foreach (var i in crawlData.DuplicatedGCHandleTargetsStack)
////                {
////                    var ptr = snapshot.CrawledData.ManagedObjects[i].PtrObject;
////                    snapshot.CrawledData.ManagedObjects[i] = snapshot.CrawledData.ManagedObjects[snapshot.CrawledData.MangedObjectIndexByAddress[ptr]];
////                }

////                //crawl connection data
////                status.IncrementStep();
////                status.StepStatus = "Crawling connection data";
////                yield return status;

////                // these key Unity Types will never show up as objects of their managed base type as they are only ever used via derived types
////                if (snapshot.TypeDescriptions.ITypeUnityMonoBehaviour >= 0 && snapshot.NativeTypes.MonoBehaviourIdx >= 0)
////                {
////                    snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.Add(snapshot.NativeTypes.MonoBehaviourIdx, snapshot.TypeDescriptions.ITypeUnityMonoBehaviour);
////                }
////                if (snapshot.TypeDescriptions.ITypeUnityScriptableObject >= 0 && snapshot.NativeTypes.ScriptableObjectIdx >= 0)
////                {
////                    snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.Add(snapshot.NativeTypes.ScriptableObjectIdx, snapshot.TypeDescriptions.ITypeUnityScriptableObject);
////                }
////                if (snapshot.TypeDescriptions.ITypeUnityComponent >= 0 && snapshot.NativeTypes.ComponentIdx >= 0)
////                {
////                    snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.Add(snapshot.NativeTypes.ComponentIdx, snapshot.TypeDescriptions.ITypeUnityComponent);
////                }
////                ConnectNativeToManageObject(crawlData);
////                AddupRawRefCount(snapshot);

////                snapshot.CrawledData.AddUpTotalMemoryUsage(crawlData.CachedMemorySnapshot.ManagedHeapSections);
////                snapshot.CrawledData.CreateConnectionMaps(snapshot);
////                snapshot.CrawledData.FinishedCrawling();
////            }

////            static void AddupRawRefCount(SnapshotFileData snapshot)
////            {
////                for (long i = 0; i != snapshot.Connections.Count; ++i)
////                {
////                    int iManagedTo = snapshot.UnifiedObjectIndexToManagedObjectIndex(snapshot.Connections.To[i]);
////                    if (iManagedTo >= 0)
////                    {
////                        var obj = snapshot.CrawledData.ManagedObjects[iManagedTo];
////                        ++obj.RefCount;
////                        snapshot.CrawledData.ManagedObjects[iManagedTo] = obj;
////                        continue;
////                    }

////                    int iNativeTo = snapshot.UnifiedObjectIndexToNativeObjectIndex(snapshot.Connections.To[i]);
////                    if (iNativeTo >= 0)
////                    {
////                        var rc = ++snapshot.NativeObjects.refcount[iNativeTo];
////                        snapshot.NativeObjects.refcount[iNativeTo] = rc;
////                        continue;
////                    }
////                }
////            }

////            static void ConnectNativeToManageObject(IntermediateCrawlData crawlData)
////            {
////                var snapshot = crawlData.CachedMemorySnapshot;
////                var objectInfos = crawlData.ManagedObjectInfos;

////                if (snapshot.TypeDescriptions.Count == 0)
////                    return;

////                int cachedPtrOffset = snapshot.TypeDescriptions.IFieldUnityObjectMCachedPtrOffset;

////#if DEBUG_VALIDATION
////            // These are used to double-check that all Native -> Managed connections reported via GCHandles on Native Objects are correctly found via m_CachedPtr
////            long firstManagedToNativeConnection = snapshot.CrawledData.Connections.Count;
////            Dictionary<ulong, int> managedObjectAddressToNativeObjectIndex = new Dictionary<ulong, int>();
////#endif

////                for (int i = 0; i != objectInfos.Count; i++)
////                {
////                    //Must derive of unity Object
////                    var objectInfo = objectInfos[i];
////                    objectInfo.NativeObjectIndex = -1;
////                    int instanceID = CachedSnapshot.NativeObjectEntriesCache.InstanceIDNone;
////                    int nativeTypeIndex;
////                    if (snapshot.TypeDescriptions.UnityObjectTypeIndexToNativeTypeIndex.TryGetValue(objectInfo.ITypeDescription, out nativeTypeIndex))
////                    {
////                        // TODO: Add index to a list of Managed Unity Objects here
////                        var heapSection = snapshot.ManagedHeapSections.Find(objectInfo.PtrObject + (ulong)cachedPtrOffset, snapshot.VirtualMachineInformation);
////                        if (!heapSection.IsValid)
////                        {
////                            Debug.LogWarning("Managed object (addr:" + objectInfo.PtrObject + ", index:" + objectInfo.ManagedObjectIndex + ") does not have data at cachedPtr offset(" + cachedPtrOffset + ")");
////                        }
////                        else
////                        {
////                            ulong cachedPtr;
////                            heapSection.TryReadPointer(out cachedPtr);

////                            if (!snapshot.NativeObjects.nativeObjectAddressToInstanceId.TryGetValue(cachedPtr, out instanceID))
////                                instanceID = CachedSnapshot.NativeObjectEntriesCache.InstanceIDNone;
////                            // cachedPtr == 0UL or instanceID == CachedSnapshot.NativeObjectEntriesCache.InstanceIDNone -> Leaked Shell
////                            // TODO: Add index to a list of leaked shells here.
////                        }

////                        if (instanceID != CachedSnapshot.NativeObjectEntriesCache.InstanceIDNone)
////                        {
////                            if (snapshot.NativeObjects.instanceId2Index.TryGetValue(instanceID, out objectInfo.NativeObjectIndex))
////                                snapshot.NativeObjects.ManagedObjectIndex[objectInfo.NativeObjectIndex] = i;

////                            if (nativeTypeIndex == -1)
////                            {
////                                nativeTypeIndex = snapshot.NativeObjects.NativeTypeArrayIndex[objectInfo.NativeObjectIndex];
////                                snapshot.TypeDescriptions.UnityObjectTypeIndexToNativeTypeIndex[objectInfo.ITypeDescription] = nativeTypeIndex;

////                                if (!snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.ContainsKey(nativeTypeIndex))
////                                {
////                                    // Check if this managed object's managed type cou
////                                    var typeName = snapshot.TypeDescriptions.TypeDescriptionName[objectInfo.ITypeDescription];
////                                    if (typeName.StartsWith("Unity"))
////                                    {
////                                        var startOfNamespaceStrippedManagedTypeName = typeName.LastIndexOf('.') + 1;
////                                        var managedTypeNameLength = typeName.Length - startOfNamespaceStrippedManagedTypeName;
////                                        var nativeTypeNameLength = snapshot.NativeTypes.TypeName[nativeTypeIndex].Length;
////                                        if (managedTypeNameLength == nativeTypeNameLength)
////                                        {
////                                            unsafe
////                                            {
////                                                fixed (char* nativeName = snapshot.NativeTypes.TypeName[nativeTypeIndex], managedName = typeName)
////                                                {
////                                                    // no need to create a bunch of managed substrings in a hot loop
////                                                    char* managedSubstring = managedName + startOfNamespaceStrippedManagedTypeName;
////                                                    if (UnsafeUtility.MemCmp(managedSubstring, nativeName, managedTypeNameLength) == 0)
////                                                        snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.Add(nativeTypeIndex, objectInfo.ITypeDescription);
////                                                }
////                                            }
////                                        }
////                                    }
////                                }
////                            }
////                            if (snapshot.HasConnectionOverhaul)
////                            {
////                                snapshot.CrawledData.Connections.Add(ManagedConnection.MakeUnityEngineObjectConnection(objectInfo.NativeObjectIndex, objectInfo.ManagedObjectIndex));
////                                var rc = ++snapshot.NativeObjects.refcount[objectInfo.NativeObjectIndex];
////                                snapshot.NativeObjects.refcount[objectInfo.NativeObjectIndex] = rc;
////#if DEBUG_VALIDATION
////                            managedObjectAddressToNativeObjectIndex.Add(objectInfo.PtrObject, objectInfo.NativeObjectIndex);
////#endif
////                            }
////                        }
////                    }
////                    //else
////                    //{
////                    // TODO: Add index to a list of Pure C# Objects here
////                    //}

////                    objectInfos[i] = objectInfo;
////                }

////#if DEBUG_VALIDATION
////            // Double-check that all Native -> Managed connections reported via GCHandles on Native Objects have been correctly found via m_CachedPtr
////            if (snapshot.Connections.IndexOfFirstNativeToGCHandleConnection >= 0)
////            {
////                var gcHandlesCount = snapshot.GcHandles.Count;
////                for (long nativeConnectionIndex = snapshot.Connections.IndexOfFirstNativeToGCHandleConnection; nativeConnectionIndex < snapshot.Connections.Count; nativeConnectionIndex++)
////                {
////                    var nativeObjectIndex = snapshot.Connections.From[nativeConnectionIndex] - gcHandlesCount;
////                    var managedShellAddress = snapshot.GcHandles.Target[snapshot.Connections.To[nativeConnectionIndex]];
////                    var managedObjectIndex = snapshot.CrawledData.MangedObjectIndexByAddress[managedShellAddress];
////                    var managedObject = snapshot.CrawledData.ManagedObjects[managedObjectIndex];
////                    if (managedObject.NativeObjectIndex != nativeObjectIndex)
////                        Debug.LogError("Native Object is not correctly linked with its Managed Object");
////                    bool foundConnection = managedObjectAddressToNativeObjectIndex.ContainsKey(managedShellAddress);
////                    if (!foundConnection)
////                        Debug.LogError("Native Object is not correctly linked with its Managed Object");
////                }
////            }
////#endif
////            }

////            static void CrawlRawObjectData(IntermediateCrawlData crawlData, BytesAndOffset bytesAndOffset, int iTypeDescription, bool useStaticFields, ulong ptrFrom, int indexOfFrom)
////            {
////                var snapshot = crawlData.CachedMemorySnapshot;

////                var fields = useStaticFields ? snapshot.TypeDescriptions.fieldIndicesOwnedStatic[iTypeDescription] : snapshot.TypeDescriptions.FieldIndicesInstance[iTypeDescription];
////                foreach (var iField in fields)
////                {
////                    int iField_TypeDescription_TypeIndex = snapshot.FieldDescriptions.TypeIndex[iField];
////                    int iField_TypeDescription_ArrayIndex = snapshot.TypeDescriptions.TypeIndex2ArrayIndex(iField_TypeDescription_TypeIndex);

////                    var fieldLocation = bytesAndOffset.Add(snapshot.FieldDescriptions.Offset[iField] - (useStaticFields ? 0 : snapshot.VirtualMachineInformation.ObjectHeaderSize));

////                    if (snapshot.TypeDescriptions.HasFlag(iField_TypeDescription_ArrayIndex, TypeFlags.kValueType))
////                    {
////                        CrawlRawObjectData(crawlData, fieldLocation, iField_TypeDescription_ArrayIndex, useStaticFields, ptrFrom, indexOfFrom);
////                        continue;
////                    }


////                    ulong fieldAddr;
////                    if (fieldLocation.TryReadPointer(out fieldAddr) == BytesAndOffset.PtrReadError.Success
////                        // don't process null pointers
////                        && fieldAddr != 0)
////                    {
////                        crawlData.CrawlDataStack.Push(new StackCrawlData() { ptr = fieldAddr, ptrFrom = ptrFrom, typeFrom = iTypeDescription, indexOfFrom = indexOfFrom, fieldFrom = iField, fromArrayIndex = -1 });
////                    }
////                }
////            }

////            static bool CrawlPointer(IntermediateCrawlData dataStack)
////            {
////                UnityEngine.Debug.Assert(dataStack.CrawlDataStack.Count > 0);

////                var snapshot = dataStack.CachedMemorySnapshot;
////                var typeDescriptions = snapshot.TypeDescriptions;
////                var data = dataStack.CrawlDataStack.Pop();
////                var virtualMachineInformation = snapshot.VirtualMachineInformation;
////                var managedHeapSections = snapshot.ManagedHeapSections;
////                var byteOffset = managedHeapSections.Find(data.ptr, virtualMachineInformation);

////                if (!byteOffset.IsValid)
////                {
////                    return false;
////                }

////                ManagedObjectInfo obj;
////                bool wasAlreadyCrawled;

////                obj = ParseObjectHeader(snapshot, data, out wasAlreadyCrawled, false, byteOffset);
////                bool addConnection = (data.typeFrom >= 0 || data.fieldFrom >= 0);
////                if (addConnection)
////                    ++obj.RefCount;

////                if (!obj.IsValid())
////                    return false;

////                snapshot.CrawledData.ManagedObjects[obj.ManagedObjectIndex] = obj;
////                snapshot.CrawledData.MangedObjectIndexByAddress[obj.PtrObject] = obj.ManagedObjectIndex;

////                if (addConnection)
////                    dataStack.ManagedConnections.Add(ManagedConnection.MakeConnection(snapshot, data.indexOfFrom, data.ptrFrom, obj.ManagedObjectIndex, data.ptr, data.typeFrom, data.fieldFrom, data.fromArrayIndex));

////                if (wasAlreadyCrawled)
////                    return true;

////                if (!typeDescriptions.HasFlag(obj.ITypeDescription, TypeFlags.kArray))
////                {
////                    CrawlRawObjectData(dataStack, byteOffset.Add(snapshot.VirtualMachineInformation.ObjectHeaderSize), obj.ITypeDescription, false, data.ptr, obj.ManagedObjectIndex);
////                    return true;
////                }

////                var arrayLength = ArrayTools.ReadArrayLength(snapshot, data.ptr, obj.ITypeDescription);
////                int iElementTypeDescription = typeDescriptions.BaseOrElementTypeIndex[obj.ITypeDescription];
////                if (iElementTypeDescription == -1)
////                {
////                    return false; //do not crawl uninitialized object types, as we currently don't have proper handling for these
////                }
////                var arrayData = byteOffset.Add(virtualMachineInformation.ArrayHeaderSize);
////                for (int i = 0; i != arrayLength; i++)
////                {
////                    if (typeDescriptions.HasFlag(iElementTypeDescription, TypeFlags.kValueType))
////                    {
////                        CrawlRawObjectData(dataStack, arrayData, iElementTypeDescription, false, data.ptr, obj.ManagedObjectIndex);
////                        arrayData = arrayData.Add(typeDescriptions.Size[iElementTypeDescription]);
////                    }
////                    else
////                    {
////                        ulong arrayDataPtr;
////                        if (arrayData.TryReadPointer(out arrayDataPtr) != BytesAndOffset.PtrReadError.Success)
////                            return false;

////                        // don't process null pointers
////                        if (arrayDataPtr != 0)
////                            dataStack.CrawlDataStack.Push(new StackCrawlData() { ptr = arrayDataPtr, ptrFrom = data.ptr, typeFrom = obj.ITypeDescription, indexOfFrom = obj.ManagedObjectIndex, fieldFrom = -1, fromArrayIndex = i });
////                        arrayData = arrayData.NextPointer();
////                    }
////                }
////                return true;
////            }

////            static int SizeOfObjectInBytes(SnapshotFileData snapshot, int iTypeDescription, BytesAndOffset bo, ulong address)
////            {
////                if (iTypeDescription < 0) return 0;

////                if (snapshot.TypeDescriptions.HasFlag(iTypeDescription, TypeFlags.kArray))
////                    return ArrayTools.ReadArrayObjectSizeInBytes(snapshot, address, iTypeDescription);

////                if (snapshot.TypeDescriptions.ITypeString == iTypeDescription)
////                    return StringTools.ReadStringObjectSizeInBytes(bo, snapshot.VirtualMachineInformation);

////                //array and string are the only types that are special, all other types just have one size, which is stored in the type description
////                return snapshot.TypeDescriptions.Size[iTypeDescription];
////            }

////            static int SizeOfObjectInBytes(SnapshotFileData snapshot, int iTypeDescription, BytesAndOffset byteOffset, CachedSnapshot.ManagedMemorySectionEntriesCache heap)
////            {
////                if (iTypeDescription < 0) return 0;

////                if (snapshot.TypeDescriptions.HasFlag(iTypeDescription, TypeFlags.kArray))
////                    return ArrayTools.ReadArrayObjectSizeInBytes(snapshot, byteOffset, iTypeDescription);

////                if (snapshot.TypeDescriptions.ITypeString == iTypeDescription)
////                    return StringTools.ReadStringObjectSizeInBytes(byteOffset, snapshot.VirtualMachineInformation);

////                // array and string are the only types that are special, all other types just have one size, which is stored in the type description
////                return snapshot.TypeDescriptions.Size[iTypeDescription];
////            }

////            internal static ManagedObjectInfo ParseObjectHeader(SnapshotFileData snapshot, StackCrawlData crawlData, out bool wasAlreadyCrawled, bool ignoreBadHeaderError, BytesAndOffset byteOffset)
////            {
////                var objectList = snapshot.CrawledData.ManagedObjects;
////                var objectsByAddress = snapshot.CrawledData.MangedObjectIndexByAddress;

////                ManagedObjectInfo objectInfo = default(ManagedObjectInfo);

////                int idx = 0;
////                if (!snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(crawlData.ptr, out idx))
////                {
////                    if (TryParseObjectHeader(snapshot, crawlData, out objectInfo, byteOffset))
////                    {
////                        objectInfo.ManagedObjectIndex = (int)objectList.Count;
////                        objectList.Add(objectInfo);
////                        objectsByAddress.Add(crawlData.ptr, objectInfo.ManagedObjectIndex);
////                    }
////                    wasAlreadyCrawled = false;
////                    return objectInfo;
////                }

////                objectInfo = snapshot.CrawledData.ManagedObjects[idx];
////                // this happens on objects from gcHandles, they are added before any other crawled object but have their ptr set to 0.
////                if (objectInfo.PtrObject == 0)
////                {
////                    idx = objectInfo.ManagedObjectIndex;
////                    if (TryParseObjectHeader(snapshot, crawlData, out objectInfo, byteOffset))
////                    {
////                        objectInfo.ManagedObjectIndex = idx;
////                        objectList[idx] = objectInfo;
////                        objectsByAddress[crawlData.ptr] = idx;
////                    }

////                    wasAlreadyCrawled = false;
////                    return objectInfo;
////                }

////                wasAlreadyCrawled = true;
////                return objectInfo;
////            }

////            public static bool TryParseObjectHeader(SnapshotFileData snapshot, StackCrawlData data, out ManagedObjectInfo info, BytesAndOffset boHeader)
////            {
////                bool resolveFailed = false;
////                var heap = snapshot.ManagedHeapSections;
////                info = new ManagedObjectInfo();
////                info.ManagedObjectIndex = -1;

////                ulong ptrIdentity = 0;
////                if (!boHeader.IsValid) boHeader = heap.Find(data.ptr, snapshot.VirtualMachineInformation);
////                if (!boHeader.IsValid)
////                    resolveFailed = true;
////                else
////                {
////                    boHeader.TryReadPointer(out ptrIdentity);

////                    info.PtrTypeInfo = ptrIdentity;
////                    info.ITypeDescription = snapshot.TypeDescriptions.TypeInfo2ArrayIndex(info.PtrTypeInfo);

////                    if (info.ITypeDescription < 0)
////                    {
////                        var boIdentity = heap.Find(ptrIdentity, snapshot.VirtualMachineInformation);
////                        if (boIdentity.IsValid)
////                        {
////                            ulong ptrTypeInfo;
////                            boIdentity.TryReadPointer(out ptrTypeInfo);
////                            info.PtrTypeInfo = ptrTypeInfo;
////                            info.ITypeDescription = snapshot.TypeDescriptions.TypeInfo2ArrayIndex(info.PtrTypeInfo);
////                            resolveFailed = info.ITypeDescription < 0;
////                        }
////                        else
////                        {
////                            resolveFailed = true;
////                        }
////                    }
////                }

////                if (resolveFailed)
////                {
////                    //enable this define in order to track objects that are missing type data, this can happen if for whatever reason mono got changed and there are types / heap chunks that we do not report
////                    //addresses here can be used to identify the objects within the Unity process by using a debug version of the mono libs in order to add to the capture where this data resides.
////#if DEBUG_VALIDATION
////                Debug.LogError($"Bad object detected:\nheader at address: { DefaultDataFormatter.Instance.FormatPointer(data.ptr)} \nvtable at address {DefaultDataFormatter.Instance.FormatPointer(ptrIdentity)}" +
////                    $"\nDetails:\n From object: {DefaultDataFormatter.Instance.FormatPointer(data.ptrFrom)}\n " +
////                    $"From type: {(data.typeFrom != -1 ? snapshot.TypeDescriptions.TypeDescriptionName[data.typeFrom] : data.typeFrom.ToString())}\n" +
////                    $"From field: {(data.fieldFrom != -1 ? snapshot.FieldDescriptions.FieldDescriptionName[data.fieldFrom] : data.fieldFrom.ToString())}\n" +
////                    $"From array data: arrayIndex - {(data.fromArrayIndex)}, indexOf - {(data.indexOfFrom)}");
////                //can add from array index too above if needed
////#endif
////                    info.PtrTypeInfo = 0;
////                    info.ITypeDescription = -1;
////                    info.Size = 0;
////                    info.PtrObject = 0;
////                    info.data = default(BytesAndOffset);

////                    return false;
////                }


////                info.Size = SizeOfObjectInBytes(snapshot, info.ITypeDescription, boHeader, heap);
////                info.data = boHeader;
////                info.PtrObject = data.ptr;
////                return true;
////            }
////        }

////        internal static class StringTools
////        {
////            const int k_StringBuilderMaxCap = 8000; // After 8000 chars, StringBuilder will ring buffer the strings and our UI breaks. Also see https://referencesource.microsoft.com/#mscorlib/system/text/stringbuilder.cs,76
////            public const int MaxStringLengthToRead = k_StringBuilderMaxCap - 10 /*Buffer for ellipsis, quotes and spaces*/;
////            public const string Elipsis = " [...]";

////            public static string ReadString(this BytesAndOffset bo, out int fullLength, VirtualMachineInformation virtualMachineInformation)
////            {
////                var lengthPointer = bo.Add(virtualMachineInformation.ObjectHeaderSize);
////                fullLength = lengthPointer.ReadInt32();
////                var firstChar = lengthPointer.Add(sizeof(int));

////                if (fullLength < 0 || (long)fullLength * 2 > bo.bytes.Length - bo.offset - sizeof(int))
////                {
////#if DEBUG_VALIDATION
////                Debug.LogError("Found a String Object of impossible length.");
////#endif
////                    fullLength = 0;
////                }

////                if (fullLength > MaxStringLengthToRead)
////                {
////                    var cappedLength = MaxStringLengthToRead;
////                    return $"{System.Text.Encoding.Unicode.GetString(firstChar.bytes, firstChar.offset, cappedLength * 2)}{Elipsis}";
////                }
////                else
////                    return System.Text.Encoding.Unicode.GetString(firstChar.bytes, firstChar.offset, fullLength * 2);
////            }

////            public static string ReadFirstStringLine(this BytesAndOffset bo, VirtualMachineInformation virtualMachineInformation, bool addQuotes)
////            {
////                var str = ReadString(bo, out _, virtualMachineInformation);
////                var firstLineBreak = str.IndexOf('\n');
////                const int maxCharsInLine = 30;
////                if (firstLineBreak >= 0)
////                {
////                    if (firstLineBreak < maxCharsInLine && str.Length > maxCharsInLine)
////                    {
////                        // reduce our working set
////                        str = str.Substring(0, Math.Min(str.Length, maxCharsInLine));
////                    }
////                    str = str.Replace("\n", "\\n");
////                    str += " [...]";
////                }
////                if (addQuotes)
////                {
////                    if (firstLineBreak >= 0)
////                        return $"\"{str}"; // open ended quote
////                    return $"\"{str}\"";
////                }
////                else
////                {
////                    return str;
////                }
////            }

////            public static int ReadStringObjectSizeInBytes(this BytesAndOffset bo, VirtualMachineInformation virtualMachineInformation)
////            {
////                var lengthPointer = bo.Add(virtualMachineInformation.ObjectHeaderSize);
////                var length = lengthPointer.ReadInt32();
////                if (length < 0 || (long)length * 2 > bo.bytes.Length - bo.offset - sizeof(int))
////                {
////#if DEBUG_VALIDATION
////                Debug.LogError("Found a String Object of impossible length.");
////#endif
////                    length = 0;
////                }

////                return virtualMachineInformation.ObjectHeaderSize + /*lengthfield*/ 1 + (length * /*utf16=2bytes per char*/ 2) + /*2 zero terminators*/ 2;
////            }
////        }
////        internal class ArrayInfo
////        {
////            public ulong baseAddress;
////            public int[] rank;
////            public int length;
////            public int elementSize;
////            public int arrayTypeDescription;
////            public int elementTypeDescription;
////            public BytesAndOffset header;
////            public BytesAndOffset data;
////            public BytesAndOffset GetArrayElement(int index)
////            {
////                return data.Add(elementSize * index);
////            }

////            public ulong GetArrayElementAddress(int index)
////            {
////                return baseAddress + (ulong)(elementSize * index);
////            }

////            public string IndexToRankedString(int index)
////            {
////                return ArrayTools.ArrayRankIndexToString(rank, index);
////            }

////            public string ArrayRankToString()
////            {
////                return ArrayTools.ArrayRankToString(rank);
////            }
////        }
////        internal static class ArrayTools
////        {
////            public static ArrayInfo GetArrayInfo(CachedSnapshot data, BytesAndOffset arrayData, int iTypeDescriptionArrayType)
////            {
////                var virtualMachineInformation = data.VirtualMachineInformation;
////                var arrayInfo = new ArrayInfo();
////                arrayInfo.baseAddress = 0;
////                arrayInfo.arrayTypeDescription = iTypeDescriptionArrayType;


////                arrayInfo.header = arrayData;
////                arrayInfo.data = arrayInfo.header.Add(virtualMachineInformation.ArrayHeaderSize);
////                ulong bounds;
////                arrayInfo.header.Add(virtualMachineInformation.ArrayBoundsOffsetInHeader).TryReadPointer(out bounds);

////                if (bounds == 0)
////                {
////                    arrayInfo.length = arrayInfo.header.Add(virtualMachineInformation.ArraySizeOffsetInHeader).ReadInt32();
////                    arrayInfo.rank = new int[1] { arrayInfo.length };
////                }
////                else
////                {
////                    int rank = data.TypeDescriptions.GetRank(iTypeDescriptionArrayType);
////                    arrayInfo.rank = new int[rank];

////                    var cursor = data.ManagedHeapSections.Find(bounds, virtualMachineInformation);
////                    if (cursor.IsValid)
////                    {
////                        arrayInfo.length = 1;
////                        for (int i = 0; i != rank; i++)
////                        {
////                            var l = cursor.ReadInt32();
////                            arrayInfo.length *= l;
////                            arrayInfo.rank[i] = l;
////                            cursor = cursor.Add(8);
////                        }
////                    }
////                    else
////                    {
////                        //object has corrupted data
////                        arrayInfo.length = 0;
////                        for (int i = 0; i != rank; i++)
////                        {
////                            arrayInfo.rank[i] = -1;
////                        }
////                    }
////                }

////                arrayInfo.elementTypeDescription = data.TypeDescriptions.BaseOrElementTypeIndex[iTypeDescriptionArrayType];
////                if (arrayInfo.elementTypeDescription == -1) //We currently do not handle uninitialized types as such override the type, making it return pointer size
////                {
////                    arrayInfo.elementTypeDescription = iTypeDescriptionArrayType;
////                }
////                if (data.TypeDescriptions.HasFlag(arrayInfo.elementTypeDescription, TypeFlags.kValueType))
////                {
////                    arrayInfo.elementSize = data.TypeDescriptions.Size[arrayInfo.elementTypeDescription];
////                }
////                else
////                {
////                    arrayInfo.elementSize = virtualMachineInformation.PointerSize;
////                }
////                return arrayInfo;
////            }

////            public static int GetArrayElementSize(CachedSnapshot data, int iTypeDescriptionArrayType)
////            {
////                int iElementTypeDescription = data.TypeDescriptions.BaseOrElementTypeIndex[iTypeDescriptionArrayType];
////                if (data.TypeDescriptions.HasFlag(iElementTypeDescription, TypeFlags.kValueType))
////                {
////                    return data.TypeDescriptions.Size[iElementTypeDescription];
////                }
////                return data.VirtualMachineInformation.PointerSize;
////            }

////            public static string ArrayRankToString(int[] rankLength)
////            {
////                string o = "";
////                for (int i = 0; i < rankLength.Length; ++i)
////                {
////                    if (o.Length > 0)
////                    {
////                        o += ", ";
////                    }
////                    o += rankLength[i].ToString();
////                }
////                return o;
////            }

////            public static string ArrayRankIndexToString(int[] rankLength, int index)
////            {
////                string o = "";
////                int remainder = index;
////                for (int i = 1; i < rankLength.Length; ++i)
////                {
////                    if (o.Length > 0)
////                    {
////                        o += ", ";
////                    }
////                    var l = rankLength[i];
////                    int rankIndex = remainder / l;
////                    o += rankIndex.ToString();
////                    remainder = remainder - rankIndex * l;
////                }
////                if (o.Length > 0)
////                {
////                    o += ", ";
////                }
////                o += remainder;
////                return o;
////            }

////            public static int[] ReadArrayRankLength(CachedSnapshot data, CachedSnapshot.ManagedMemorySectionEntriesCache heap, UInt64 address, int iTypeDescriptionArrayType, VirtualMachineInformation virtualMachineInformation)
////            {
////                if (iTypeDescriptionArrayType < 0) return null;

////                var bo = heap.Find(address, virtualMachineInformation);
////                ulong bounds;
////                bo.Add(virtualMachineInformation.ArrayBoundsOffsetInHeader).TryReadPointer(out bounds);

////                if (bounds == 0)
////                {
////                    return new int[1] { bo.Add(virtualMachineInformation.ArraySizeOffsetInHeader).ReadInt32() };
////                }

////                var cursor = heap.Find(bounds, virtualMachineInformation);
////                int rank = data.TypeDescriptions.GetRank(iTypeDescriptionArrayType);
////                int[] l = new int[rank];
////                for (int i = 0; i != rank; i++)
////                {
////                    l[i] = cursor.ReadInt32();
////                    cursor = cursor.Add(8);
////                }
////                return l;
////            }

////            public static int ReadArrayLength(CachedSnapshot data, UInt64 address, int iTypeDescriptionArrayType)
////            {
////                if (iTypeDescriptionArrayType < 0)
////                {
////                    return 0;
////                }

////                var heap = data.ManagedHeapSections;
////                var bo = heap.Find(address, data.VirtualMachineInformation);
////                return ReadArrayLength(data, bo, iTypeDescriptionArrayType);
////            }

////            public static int ReadArrayLength(CachedSnapshot data, BytesAndOffset arrayData, int iTypeDescriptionArrayType)
////            {
////                if (iTypeDescriptionArrayType < 0) return 0;

////                var virtualMachineInformation = data.VirtualMachineInformation;
////                var heap = data.ManagedHeapSections;
////                var bo = arrayData;

////                ulong bounds;
////                bo.Add(virtualMachineInformation.ArrayBoundsOffsetInHeader).TryReadPointer(out bounds);

////                if (bounds == 0)
////                    return bo.Add(virtualMachineInformation.ArraySizeOffsetInHeader).ReadInt32();

////                var cursor = heap.Find(bounds, virtualMachineInformation);
////                int length = 0;

////                if (cursor.IsValid)
////                {
////                    length = 1;
////                    int rank = data.TypeDescriptions.GetRank(iTypeDescriptionArrayType);
////                    for (int i = 0; i != rank; i++)
////                    {
////                        length *= cursor.ReadInt32();
////                        cursor = cursor.Add(8);
////                    }
////                }

////                return length;
////            }

////            public static int ReadArrayObjectSizeInBytes(CachedSnapshot data, UInt64 address, int iTypeDescriptionArrayType)
////            {
////                var arrayLength = ReadArrayLength(data, address, iTypeDescriptionArrayType);

////                var virtualMachineInformation = data.VirtualMachineInformation;
////                var ti = data.TypeDescriptions.BaseOrElementTypeIndex[iTypeDescriptionArrayType];
////                var ai = data.TypeDescriptions.TypeIndex2ArrayIndex(ti);
////                var isValueType = data.TypeDescriptions.HasFlag(ai, TypeFlags.kValueType);

////                var elementSize = isValueType ? data.TypeDescriptions.Size[ai] : virtualMachineInformation.PointerSize;
////                return virtualMachineInformation.ArrayHeaderSize + elementSize * arrayLength;
////            }

////            public static int ReadArrayObjectSizeInBytes(CachedSnapshot data, BytesAndOffset arrayData, int iTypeDescriptionArrayType)
////            {
////                var arrayLength = ReadArrayLength(data, arrayData, iTypeDescriptionArrayType);
////                var virtualMachineInformation = data.VirtualMachineInformation;

////                var ti = data.TypeDescriptions.BaseOrElementTypeIndex[iTypeDescriptionArrayType];
////                if (ti == -1) // check added as element type index can be -1 if we are dealing with a class member (eg: Dictionary.Entry) whose type is uninitialized due to their generic data not getting inflated a.k.a unused types
////                {
////                    ti = iTypeDescriptionArrayType;
////                }

////                var ai = data.TypeDescriptions.TypeIndex2ArrayIndex(ti);
////                var isValueType = data.TypeDescriptions.HasFlag(ai, TypeFlags.kValueType);
////                var elementSize = isValueType ? data.TypeDescriptions.Size[ai] : virtualMachineInformation.PointerSize;

////                return virtualMachineInformation.ArrayHeaderSize + elementSize * arrayLength;
////            }
////        }
////        public interface ISortedEntriesCache
////        {
////            void Preload();
////            int Count { get; }
////            ulong Address(int index);
////            ulong Size(int index);
////        }

////        public class SortedNativeMemoryRegionEntriesCache : ISortedEntriesCache
////        {
////            SnapshotFileData m_Snapshot;
////            int[] m_Sorting;

////            public SortedNativeMemoryRegionEntriesCache(SnapshotFileData snapshot)
////            {
////                m_Snapshot = snapshot;
////            }

////            public void Preload()
////            {
////                if (m_Sorting == null)
////                {
////                    m_Sorting = new int[m_Snapshot.NativeMemoryRegions.Count];

////                    for (int i = 0; i < m_Sorting.Length; ++i)
////                        m_Sorting[i] = i;

////                    Array.Sort(m_Sorting, (x, y) => m_Snapshot.NativeMemoryRegions.AddressBase[x].CompareTo(m_Snapshot.NativeMemoryRegions.AddressBase[y]));
////                }
////            }

////            int this[int index]
////            {
////                get
////                {
////                    Preload();
////                    return m_Sorting[index];
////                }
////            }

////            public int Count { get { return (int)m_Snapshot.NativeMemoryRegions.Count; } }
////            public ulong Address(int index) { return m_Snapshot.NativeMemoryRegions.AddressBase[this[index]]; }
////            public ulong Size(int index) { return m_Snapshot.NativeMemoryRegions.AddressSize[this[index]]; }

////            public string Name(int index) { return m_Snapshot.NativeMemoryRegions.MemoryRegionName[this[index]]; }
////            public int UnsortedParentRegionIndex(int index) { return m_Snapshot.NativeMemoryRegions.ParentIndex[this[index]]; }
////            public int UnsortedFirstAllocationIndex(int index) { return m_Snapshot.NativeMemoryRegions.FirstAllocationIndex[this[index]]; }
////            public int UnsortedNumAllocations(int index) { return m_Snapshot.NativeMemoryRegions.NumAllocations[this[index]]; }
////        }

////        //TODO: unify with the other old section entries as those are sorted by default now
////        public class SortedManagedMemorySectionEntriesCache : ISortedEntriesCache
////        {
////            ManagedMemorySectionEntriesCache m_Entries;

////            public SortedManagedMemorySectionEntriesCache(ManagedMemorySectionEntriesCache entries)
////            {
////                m_Entries = entries;
////            }

////            public void Preload()
////            {
////                //Dummy for the interface
////            }

////            public int Count { get { return (int)m_Entries.Count; } }
////            public ulong Address(int index) { return m_Entries.StartAddress[index]; }
////            public ulong Size(int index) { return (ulong)m_Entries.Bytes[index].Length; }
////            public byte[] Bytes(int index) { return m_Entries.Bytes[index]; }
////            public MemorySectionType SectionType(int index) { return m_Entries.SectionType[index]; }
////        }

////        public class SortedManagedObjectsCache : ISortedEntriesCache
////        {
////            SnapshotFileData m_Snapshot;
////            int[] m_Sorting;

////            public SortedManagedObjectsCache(SnapshotFileData snapshot)
////            {
////                m_Snapshot = snapshot;
////            }

////            public void Preload()
////            {
////                if (m_Sorting == null)
////                {
////                    m_Sorting = new int[m_Snapshot.CrawledData.ManagedObjects.Count];

////                    for (int i = 0; i < m_Sorting.Length; ++i)
////                        m_Sorting[i] = i;

////                    Array.Sort(m_Sorting, (x, y) => m_Snapshot.CrawledData.ManagedObjects[x].PtrObject.CompareTo(m_Snapshot.CrawledData.ManagedObjects[y].PtrObject));
////                }
////            }

////            ManagedObjectInfo this[int index]
////            {
////                get
////                {
////                    Preload();
////                    return m_Snapshot.CrawledData.ManagedObjects[m_Sorting[index]];
////                }
////            }

////            public int Count { get { return (int)m_Snapshot.CrawledData.ManagedObjects.Count; } }

////            public ulong Address(int index) { return this[index].PtrObject; }
////            public ulong Size(int index) { return (ulong)this[index].Size; }
////        }

////        public class SortedNativeAllocationsCache : ISortedEntriesCache
////        {
////            SnapshotFileData m_Snapshot;
////            int[] m_Sorting;

////            public SortedNativeAllocationsCache(SnapshotFileData snapshot)
////            {
////                m_Snapshot = snapshot;
////            }

////            public void Preload()
////            {
////                if (m_Sorting == null)
////                {
////                    m_Sorting = new int[m_Snapshot.NativeAllocations.Address.Count];

////                    for (int i = 0; i < m_Sorting.Length; ++i)
////                        m_Sorting[i] = i;

////                    Array.Sort(m_Sorting, (x, y) => m_Snapshot.NativeAllocations.Address[x].CompareTo(m_Snapshot.NativeAllocations.Address[y]));
////                }
////            }

////            int this[int index]
////            {
////                get
////                {
////                    Preload();
////                    return m_Sorting[index];
////                }
////            }

////            public int Count { get { return (int)m_Snapshot.NativeAllocations.Count; } }
////            public ulong Address(int index) { return m_Snapshot.NativeAllocations.Address[this[index]]; }
////            public ulong Size(int index) { return m_Snapshot.NativeAllocations.Size[this[index]]; }
////            public int MemoryRegionIndex(int index) { return m_Snapshot.NativeAllocations.MemoryRegionIndex[this[index]]; }
////            public long RootReferenceId(int index) { return m_Snapshot.NativeAllocations.RootReferenceId[this[index]]; }
////            public long AllocationSiteId(int index) { return m_Snapshot.NativeAllocations.AllocationSiteId[this[index]]; }
////            public int OverheadSize(int index) { return m_Snapshot.NativeAllocations.OverheadSize[this[index]]; }
////            public int PaddingSize(int index) { return m_Snapshot.NativeAllocations.PaddingSize[this[index]]; }
////        }

////        public class SortedNativeObjectsCache : ISortedEntriesCache
////        {
////            SnapshotFileData m_Snapshot;
////            int[] m_Sorting;

////            public SortedNativeObjectsCache(SnapshotFileData snapshot)
////            {
////                m_Snapshot = snapshot;
////            }

////            public void Preload()
////            {
////                if (m_Sorting == null)
////                {
////                    m_Sorting = new int[m_Snapshot.NativeObjects.NativeObjectAddress.Count];

////                    for (int i = 0; i < m_Sorting.Length; ++i)
////                        m_Sorting[i] = i;

////                    Array.Sort(m_Sorting, (x, y) => m_Snapshot.NativeObjects.NativeObjectAddress[x].CompareTo(m_Snapshot.NativeObjects.NativeObjectAddress[y]));
////                }
////            }

////            int this[int index]
////            {
////                get
////                {
////                    Preload();
////                    return m_Sorting[index];
////                }
////            }

////            public int Count { get { return (int)m_Snapshot.NativeObjects.Count; } }
////            public ulong Address(int index) { return m_Snapshot.NativeObjects.NativeObjectAddress[this[index]]; }
////            public ulong Size(int index) { return m_Snapshot.NativeObjects.Size[this[index]]; }
////            public string Name(int index) { return m_Snapshot.NativeObjects.ObjectName[this[index]]; }
////            public int InstanceId(int index) { return m_Snapshot.NativeObjects.InstanceId[this[index]]; }
////            public int NativeTypeArrayIndex(int index) { return m_Snapshot.NativeObjects.NativeTypeArrayIndex[this[index]]; }
////            public HideFlags HideFlags(int index) { return m_Snapshot.NativeObjects.HideFlags[this[index]]; }
////            public ObjectFlags Flags(int index) { return m_Snapshot.NativeObjects.Flags[this[index]]; }
////            public long RootReferenceId(int index) { return m_Snapshot.NativeObjects.RootReferenceId[this[index]]; }
////            public int Refcount(int index) { return m_Snapshot.NativeObjects.refcount[this[index]]; }
////            public int ManagedObjectIndex(int index) { return m_Snapshot.NativeObjects.ManagedObjectIndex[this[index]]; }
////        }
////        internal enum ObjectDataType
////        {
////            Unknown,
////            Value,
////            Object,
////            Array,
////            BoxedValue,
////            ReferenceObject,
////            ReferenceArray,
////            Type,
////            NativeObject,
////        }

////        internal enum CodeType
////        {
////            Native,
////            Managed,
////            Unknown,
////            Count,
////        }

////        internal class ObjectDataParent
////        {
////            public ObjectData obj;
////            public int iField;
////            public int arrayIndex;
////            public bool expandToTarget;//true means it should display the value/target of the field. False means it should display the owning object
////            public ObjectDataParent(ObjectData obj, int iField, int arrayIndex, bool expandToTarget)
////            {
////                this.obj = obj;
////                this.iField = iField;
////                this.arrayIndex = arrayIndex;
////                this.expandToTarget = expandToTarget;
////            }
////        }
////        internal struct ObjectData
////        {
////            private void SetManagedType(SnapshotFileData snapshot, int iType)
////            {
////                m_data.managed.iType = iType;
////            }

////            public static int InvalidInstanceID
////            {
////                get
////                {
////                    return CachedSnapshot.NativeObjectEntriesCache.InstanceIDNone;
////                }
////            }
////            private ObjectDataType m_dataType;
////            public ObjectDataParent m_Parent;//used for reference object/array and value to hold the owning object.
////            public ObjectData displayObject
////            {
////                get
////                {
////                    if (m_Parent != null && !m_Parent.expandToTarget)
////                    {
////                        return m_Parent.obj;
////                    }
////                    return this;
////                }
////            }
////            [StructLayout(LayoutKind.Explicit)]
////            public struct Data
////            {
////                [StructLayout(LayoutKind.Sequential)]
////                public struct Managed
////                {
////                    public ulong objectPtr;
////                    public int iType;
////                }
////                [StructLayout(LayoutKind.Sequential)]
////                public struct Native
////                {
////                    public int index;
////                }
////                [FieldOffset(0)] public Managed managed;
////                [FieldOffset(0)] public Native native;
////            }
////            private Data m_data;
////            public int managedTypeIndex
////            {
////                get
////                {
////                    switch (m_dataType)
////                    {
////                        case ObjectDataType.Array:
////                        case ObjectDataType.BoxedValue:
////                        case ObjectDataType.Object:
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.Value:
////                        case ObjectDataType.Type:
////                            return m_data.managed.iType;
////                    }

////                    return -1;
////                }
////            }
////            public BytesAndOffset managedObjectData;

////            public ObjectDataType dataType
////            {
////                get
////                {
////                    return m_dataType;
////                }
////            }
////            public int nativeObjectIndex
////            {
////                get
////                {
////                    if (m_dataType == ObjectDataType.NativeObject)
////                    {
////                        return m_data.native.index;
////                    }
////                    return -1;
////                }
////            }
////            public ulong hostManagedObjectPtr
////            {
////                get
////                {
////                    switch (m_dataType)
////                    {
////                        case ObjectDataType.Array:
////                        case ObjectDataType.BoxedValue:
////                        case ObjectDataType.Object:
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.Value:
////                            return m_data.managed.objectPtr;
////                    }
////                    return 0;
////                }
////            }

////            public int fieldIndex
////            {
////                get
////                {
////                    switch (m_dataType)
////                    {
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.Value:
////                            if (m_Parent != null)
////                            {
////                                return m_Parent.iField;
////                            }
////                            break;
////                    }
////                    return -1;
////                }
////            }
////            public int arrayIndex
////            {
////                get
////                {
////                    switch (m_dataType)
////                    {
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.Value:
////                            if (m_Parent != null)
////                            {
////                                return m_Parent.arrayIndex;
////                            }
////                            break;
////                    }
////                    return 0;
////                }
////            }
////            public bool dataIncludeObjectHeader
////            {
////                get
////                {
////                    switch (m_dataType)
////                    {
////                        case ObjectDataType.Unknown:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.Value:
////                        case ObjectDataType.Type:
////                            return false;
////                        case ObjectDataType.Array:
////                        case ObjectDataType.Object:
////                        case ObjectDataType.BoxedValue:
////                            return true;
////                    }
////                    throw new Exception("Bad datatype");
////                }
////            }
////            public bool IsValid
////            {
////                get
////                {
////                    return m_dataType != ObjectDataType.Unknown;//return data.IsValid;
////                }
////            }

////            public ObjectFlags GetFlags(CachedSnapshot cs)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.NativeObject:
////                        return cs.NativeObjects.Flags[nativeObjectIndex];
////                    case ObjectDataType.Unknown:
////                    case ObjectDataType.Value:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.ReferenceArray:
////                    case ObjectDataType.Type:
////                    default:
////                        return 0;
////                }
////            }

////            public bool HasFields(CachedSnapshot cachedSnapshot)
////            {
////                return GetInstanceFieldCount(cachedSnapshot) > 0;
////            }

////            public bool TryGetObjectPointer(out ulong ptr)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.ReferenceArray:
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.Value:
////                        ptr = hostManagedObjectPtr;
////                        return true;
////                    default:
////                        ptr = 0;
////                        return false;
////                }
////            }

////            public ulong GetObjectPointer(SnapshotFileData snapshot, bool logError = true)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.ReferenceArray:
////                    case ObjectDataType.ReferenceObject:
////                        return GetReferencePointer();
////                    case ObjectDataType.Object:
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                        return hostManagedObjectPtr;
////                    case ObjectDataType.Value:
////                        ulong offset = 0;
////                        bool isStatic = false;
////                        if (IsField())
////                        {
////                            int fieldIdx = fieldIndex;
////                            offset = (ulong)snapshot.FieldDescriptions.Offset[fieldIdx];
////                            isStatic = snapshot.FieldDescriptions.IsStatic[fieldIdx] == 1;
////                            if (isStatic)
////                            {
////                                offset = snapshot.TypeDescriptions.TypeInfoAddress[m_Parent.obj.managedTypeIndex];
////                                offset += (ulong)snapshot.TypeDescriptions.Size[m_Parent.obj.managedTypeIndex];

////                                var staticFieldIndices = snapshot.TypeDescriptions.fieldIndicesStatic[m_Parent.obj.managedTypeIndex];

////                                for (int i = 0; i < staticFieldIndices.Length; ++i)
////                                {
////                                    var cFieldIdx = staticFieldIndices[i];
////                                    if (cFieldIdx == fieldIdx)
////                                        break;
////                                    offset += (ulong)snapshot.FieldDescriptions.Offset[cFieldIdx];
////                                }
////                            }
////                        }
////                        else if (arrayIndex >= 0) //compute our offset within the array
////                        {
////                            offset += (ulong)(snapshot.VirtualMachineInformation.ArrayHeaderSize + arrayIndex * snapshot.TypeDescriptions.Size[managedTypeIndex]);
////                        }

////                        return isStatic ? offset : hostManagedObjectPtr + offset;
////                    case ObjectDataType.NativeObject:
////                        return snapshot.NativeObjects.NativeObjectAddress[nativeObjectIndex];
////                    case ObjectDataType.Type:
////                        if (m_data.managed.iType >= 0)
////                            return snapshot.TypeDescriptions.TypeInfoAddress[m_data.managed.iType];
////                        if (logError)
////                            UnityEngine.Debug.LogError("Requesting an object pointer on an invalid data type");
////                        return 0;
////                    default:
////                        if (logError)
////                            UnityEngine.Debug.LogError("Requesting an object pointer on an invalid data type");
////                        return 0;
////                }
////            }

////            public ulong GetReferencePointer()
////            {
////                switch (m_dataType)
////                {
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.ReferenceArray:
////                        ulong ptr;
////                        managedObjectData.TryReadPointer(out ptr);
////                        return ptr;
////                    default:
////                        UnityEngine.Debug.LogError("Requesting a reference pointer on an invalid data type");
////                        return 0;
////                }
////            }

////            public ObjectData GetBoxedValue(SnapshotFileData snapshot, bool expandToTarget)
////            {
////                switch (m_dataType)
////                {
////                    case ObjectDataType.Object:
////                    case ObjectDataType.BoxedValue:
////                        break;
////                    default:
////                        UnityEngine.Debug.LogError("Requesting a boxed value on an invalid data type");
////                        return Invalid;
////                }
////                ObjectData od = this;
////                od.m_Parent = new ObjectDataParent(this, -1, -1, expandToTarget);
////                od.m_dataType = ObjectDataType.Value;
////                od.managedObjectData = od.managedObjectData.Add(snapshot.VirtualMachineInformation.ObjectHeaderSize);
////                return od;
////            }

////            public ArrayInfo GetArrayInfo(SnapshotFileData snapshot)
////            {
////                if (m_dataType != ObjectDataType.Array)
////                {
////                    UnityEngine.Debug.LogError("Requesting an ArrayInfo on an invalid data type");
////                    return null;
////                }
////                return ArrayTools.GetArrayInfo(snapshot, managedObjectData, m_data.managed.iType);
////            }

////            public ObjectData GetArrayElement(SnapshotFileData snapshot, int index, bool expandToTarget)
////            {
////                return GetArrayElement(snapshot, GetArrayInfo(snapshot), index, expandToTarget);
////            }

////            public ObjectData GetArrayElement(SnapshotFileData snapshot, ArrayInfo ai, int index, bool expandToTarget)
////            {
////                switch (m_dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.ReferenceArray:
////                        break;
////                    default:
////                        Debug.Log("Requesting an array element on an invalid data type");
////                        return Invalid;
////                }
////                ObjectData o = new ObjectData();
////                o.m_Parent = new ObjectDataParent(this, -1, index, expandToTarget);
////                o.SetManagedType(snapshot, ai.elementTypeDescription);
////                o.m_data.managed.objectPtr = m_data.managed.objectPtr;
////                o.m_dataType = TypeToSubDataType(snapshot, ai.elementTypeDescription);
////                o.managedObjectData = ai.GetArrayElement(index);
////                return o;
////            }

////            public static ObjectDataType TypeToSubDataType(SnapshotFileData snapshot, int iType)
////            {
////                if (iType < 0)
////                    return ObjectDataType.Unknown;
////                if (snapshot.TypeDescriptions.HasFlag(iType, TypeFlags.kArray))
////                    return ObjectDataType.ReferenceArray;
////                else if (snapshot.TypeDescriptions.HasFlag(iType, TypeFlags.kValueType))
////                    return ObjectDataType.Value;
////                else
////                    return ObjectDataType.ReferenceObject;
////            }

////            public static ObjectDataType TypeToDataType(SnapshotFileData snapshot, int iType)
////            {
////                if (iType < 0)
////                    return ObjectDataType.Unknown;
////                if (snapshot.TypeDescriptions.HasFlag(iType, TypeFlags.kArray))
////                    return ObjectDataType.Array;
////                else if (snapshot.TypeDescriptions.HasFlag(iType, TypeFlags.kValueType))
////                    return ObjectDataType.BoxedValue;
////                else
////                    return ObjectDataType.Object;
////            }

////            // ObjectData is pointing to an object's field
////            public bool IsField()
////            {
////                return m_Parent != null && m_Parent.iField >= 0;
////            }

////            // ObjectData is pointing to an item in an array
////            public bool IsArrayItem()
////            {
////                return m_Parent != null && m_Parent.obj.dataType == ObjectDataType.Array;
////            }

////            // Returns the name of the field this ObjectData is pointing at.
////            // should be called only when IsField() return true
////            public string GetFieldName(SnapshotFileData snapshot)
////            {
////                return snapshot.FieldDescriptions.FieldDescriptionName[m_Parent.iField];
////            }

////            // Returns the number of fields the object (that this ObjectData is currently pointing at) has
////            public int GetInstanceFieldCount(SnapshotFileData snapshot)
////            {
////                switch (m_dataType)
////                {
////                    case ObjectDataType.Object:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.Value:
////                        if (managedTypeIndex < 0 || managedTypeIndex >= snapshot.TypeDescriptions.FieldIndicesInstance.Length)
////                            return 0;
////                        return snapshot.TypeDescriptions.FieldIndicesInstance[managedTypeIndex].Length;
////                    default:
////                        return 0;
////                }
////            }

////            // Returns a new ObjectData pointing to the object's (that this ObjectData is currently pointing at) field
////            // using the field index from [0, GetInstanceFieldCount()[
////            public ObjectData GetInstanceFieldByIndex(SnapshotFileData snapshot, int i)
////            {
////                int iField = snapshot.TypeDescriptions.FieldIndicesInstance[managedTypeIndex][i];
////                return GetInstanceFieldBySnapshotFieldIndex(snapshot, iField, true);
////            }

////            // Returns a new ObjectData pointing to the object's (that this ObjectData is currently pointing at) field
////            // using a field index from snapshot.fieldDescriptions
////            public ObjectData GetInstanceFieldBySnapshotFieldIndex(SnapshotFileData snapshot, int iField, bool expandToTarget)
////            {
////                ObjectData obj;
////                ulong objectPtr;

////                switch (m_dataType)
////                {
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.ReferenceArray:
////                        objectPtr = GetReferencePointer();
////                        obj = FromManagedPointer(snapshot, objectPtr);
////                        break;
////                    case ObjectDataType.Unknown: //skip unknown/uninitialized types as some snapshots will have them
////                        return new ObjectData();
////                    default:
////                        obj = this;
////                        objectPtr = m_data.managed.objectPtr;
////                        break;
////                }

////                var fieldOffset = snapshot.FieldDescriptions.Offset[iField];
////                var fieldType = snapshot.FieldDescriptions.TypeIndex[iField];
////                bool isStatic = snapshot.FieldDescriptions.IsStatic[iField] == 1;
////                switch (m_dataType)
////                {
////                    case ObjectDataType.Value:
////                        if (!isStatic)
////                            fieldOffset -= snapshot.VirtualMachineInformation.ObjectHeaderSize;
////                        break;
////                    case ObjectDataType.Object:
////                    case ObjectDataType.BoxedValue:
////                        break;
////                    case ObjectDataType.Type:
////                        if (!isStatic)
////                        {
////                            Debug.LogError("Requesting a non-static field on a type");
////                            return Invalid;
////                        }
////                        break;
////                    default:
////                        break;
////                }

////                ObjectData o = new ObjectData();
////                o.m_Parent = new ObjectDataParent(obj, iField, -1, expandToTarget);
////                o.SetManagedType(snapshot, fieldType);
////                o.m_dataType = TypeToSubDataType(snapshot, fieldType);

////                if (isStatic)
////                {
////                    //the field requested might come from a base class. make sure we are using the right staticFieldBytes.
////                    var iOwningType = obj.m_data.managed.iType;
////                    while (iOwningType >= 0)
////                    {
////                        var fieldIndex = Array.FindIndex(snapshot.TypeDescriptions.fieldIndicesOwnedStatic[iOwningType], x => x == iField);
////                        if (fieldIndex >= 0)
////                        {
////                            //field iField is owned by type iCurrentBase
////                            break;
////                        }
////                        iOwningType = snapshot.TypeDescriptions.BaseOrElementTypeIndex[iOwningType];
////                    }
////                    if (iOwningType < 0)
////                    {
////                        Debug.LogError("Field requested is not owned by the type not any of its bases");
////                        return Invalid;
////                    }

////                    o.m_data.managed.objectPtr = 0;
////                    var typeStaticData = new BytesAndOffset(snapshot.TypeDescriptions.StaticFieldBytes[iOwningType], snapshot.VirtualMachineInformation.PointerSize);
////                    o.managedObjectData = typeStaticData.Add(fieldOffset);
////                }
////                else
////                {
////                    o.m_data.managed.objectPtr = objectPtr;// m_data.managed.objectPtr;
////                    o.managedObjectData = obj.managedObjectData.Add(fieldOffset);
////                }
////                return o;
////            }

////            public int GetInstanceID(SnapshotFileData snapshot)
////            {
////                int nativeIndex = nativeObjectIndex;
////                if (nativeIndex < 0)
////                {
////                    int managedIndex = GetManagedObjectIndex(snapshot);
////                    if (managedIndex >= 0)
////                    {
////                        nativeIndex = snapshot.CrawledData.ManagedObjects[managedIndex].NativeObjectIndex;
////                    }
////                }

////                if (nativeIndex >= 0)
////                {
////                    return snapshot.NativeObjects.InstanceId[nativeIndex];
////                }
////                return CachedSnapshot.NativeObjectEntriesCache.InstanceIDNone;
////            }

////            public ObjectData GetBase(SnapshotFileData snapshot)
////            {
////                switch (m_dataType)
////                {
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.Type:
////                    case ObjectDataType.Value:
////                    case ObjectDataType.BoxedValue:
////                        break;
////                    default:
////                        UnityEngine.Debug.LogError("Requesting a base on an invalid data type");
////                        return Invalid;
////                }

////                var b = snapshot.TypeDescriptions.BaseOrElementTypeIndex[m_data.managed.iType];
////                if (b == snapshot.TypeDescriptions.ITypeValueType
////                    || b == snapshot.TypeDescriptions.ITypeObject
////                    || b == snapshot.TypeDescriptions.ITypeEnum
////                    || b == TypeDescriptionEntriesCache.ITypeInvalid)
////                    return Invalid;

////                ObjectData o = this;
////                o.SetManagedType(snapshot, b);
////                return o;
////            }

////            public long GetUnifiedObjectIndex(SnapshotFileData snapshot)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.BoxedValue:
////                        {
////                            int idx;
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(m_data.managed.objectPtr, out idx))
////                            {
////                                return snapshot.ManagedObjectIndexToUnifiedObjectIndex(idx);
////                            }
////                            break;
////                        }
////                    case ObjectDataType.NativeObject:
////                        return snapshot.NativeObjectIndexToUnifiedObjectIndex(m_data.native.index);
////                }

////                return -1;
////            }

////            public ManagedObjectInfo GetManagedObject(SnapshotFileData snapshot)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.BoxedValue:
////                        {
////                            int idx;
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(m_data.managed.objectPtr, out idx))
////                            {
////                                return snapshot.CrawledData.ManagedObjects[idx];
////                            }
////                            throw new Exception("Invalid object pointer used to query object list.");
////                        }
////                    case ObjectDataType.ReferenceObject:
////                    case ObjectDataType.ReferenceArray:
////                        {
////                            int idx;
////                            ulong refPtr = GetReferencePointer();
////                            if (refPtr == 0)
////                                return default(ManagedObjectInfo);
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(GetReferencePointer(), out idx))
////                            {
////                                return snapshot.CrawledData.ManagedObjects[idx];
////                            }
////                            //do not throw, if the ref pointer is not valid the object might have been null-ed
////                            return default(ManagedObjectInfo);
////                        }
////                    default:
////                        throw new Exception("GetManagedObjectSize was called on a instance of ObjectData which does not contain an managed object.");
////                }
////            }

////            public int GetManagedObjectIndex(SnapshotFileData snapshot)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.BoxedValue:
////                        {
////                            int idx;
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(m_data.managed.objectPtr, out idx))
////                            {
////                                return idx;
////                            }


////                            break;
////                        }
////                }

////                return -1;
////            }

////            public int GetNativeObjectIndex(SnapshotFileData snapshot)
////            {
////                switch (dataType)
////                {
////                    case ObjectDataType.NativeObject:
////                        return m_data.native.index;
////                }

////                return -1;
////            }

////            private ObjectData(ObjectDataType t)
////            {
////                m_dataType = t;
////                m_data = new Data();
////                m_data.managed.objectPtr = 0;
////                managedObjectData = new BytesAndOffset();
////                m_data.managed.iType = -1;
////                m_Parent = null;
////            }

////            public static ObjectData Invalid
////            {
////                get
////                {
////                    return new ObjectData();
////                }
////            }

////            public string GetValueAsString(CachedSnapshot cachedSnapshot)
////            {
////                if (isManaged)
////                {
////                    if (managedObjectData.bytes == null)
////                        return "null";

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeChar)
////                        return managedObjectData.ReadChar().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeInt16)
////                        return managedObjectData.ReadInt16().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeInt32)
////                        return managedObjectData.ReadInt32().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeInt64)
////                        return managedObjectData.ReadInt64().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeIntPtr)
////                        return managedObjectData.ReadUInt64().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeBool)
////                        return managedObjectData.ReadBoolean().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeSingle)
////                        return managedObjectData.ReadSingle().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeByte)
////                        return managedObjectData.ReadByte().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeDouble)
////                        return managedObjectData.ReadDouble().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeUInt16)
////                        return managedObjectData.ReadUInt16().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeUInt32)
////                        return managedObjectData.ReadUInt32().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeUInt64)
////                        return managedObjectData.ReadUInt64().ToString();

////                    if (managedTypeIndex == cachedSnapshot.TypeDescriptions.ITypeString)
////                        return managedObjectData.ReadString(out _);
////                }
////                return "";
////            }

////            internal string GetFieldDescription(CachedSnapshot cachedSnapshot)
////            {
////                string ret = "";
////                ret += "Field: " + GetFieldName(cachedSnapshot);
////                ret += " of type " + cachedSnapshot.TypeDescriptions.TypeDescriptionName[managedTypeIndex];

////                if (nativeObjectIndex != -1)
////                    return ret + " on object " + cachedSnapshot.NativeObjects.ObjectName[nativeObjectIndex];

////                if (dataType == ObjectDataType.ReferenceArray)
////                {
////                    return ret + $" on managed object [0x{hostManagedObjectPtr:x8}]";
////                }
////                if (GetManagedObject(cachedSnapshot).NativeObjectIndex != -1)
////                {
////                    return ret + " on object " + cachedSnapshot.NativeObjects.ObjectName[GetManagedObject(cachedSnapshot).NativeObjectIndex];
////                }

////                return ret + $" on managed object [0x{hostManagedObjectPtr:x8}]";
////            }

////            internal string GenerateArrayDescription(CachedSnapshot cachedSnapshot)
////            {
////                return $"{cachedSnapshot.TypeDescriptions.TypeDescriptionName[displayObject.managedTypeIndex]}[{arrayIndex}]";
////            }

////            public string GenerateTypeName(CachedSnapshot cachedSnapshot)
////            {
////                switch (displayObject.dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.Object:
////                    case ObjectDataType.ReferenceArray:
////                    case ObjectDataType.Value:
////                        return displayObject.managedTypeIndex < 0 ? "<unknown type>" : cachedSnapshot.TypeDescriptions.TypeDescriptionName[displayObject.managedTypeIndex];

////                    case ObjectDataType.ReferenceObject:
////                        {
////                            var ptr = displayObject.GetReferencePointer();
////                            if (ptr != 0)
////                            {
////                                var obj = ObjectData.FromManagedPointer(cachedSnapshot, ptr);
////                                if (obj.IsValid && obj.managedTypeIndex != displayObject.managedTypeIndex)
////                                {
////                                    return $"({cachedSnapshot.TypeDescriptions.TypeDescriptionName[obj.managedTypeIndex]}) {cachedSnapshot.TypeDescriptions.TypeDescriptionName[displayObject.managedTypeIndex]}";
////                                }
////                            }

////                            return cachedSnapshot.TypeDescriptions.TypeDescriptionName[displayObject.managedTypeIndex];
////                        }

////                    case ObjectDataType.Type:
////                        return "Type";
////                    case ObjectDataType.NativeObject:
////                        {
////                            int iType = cachedSnapshot.NativeObjects.NativeTypeArrayIndex[displayObject.nativeObjectIndex];
////                            return cachedSnapshot.NativeTypes.TypeName[iType];
////                        }
////                    case ObjectDataType.Unknown:
////                    default:
////                        return "<unintialized type>";
////                }
////            }

////            public static ObjectData FromManagedType(SnapshotFileData snapshot, int iType)
////            {
////                ObjectData o = new ObjectData();
////                o.SetManagedType(snapshot, iType);
////                o.m_dataType = ObjectDataType.Type;
////                o.managedObjectData = new BytesAndOffset { bytes = snapshot.TypeDescriptions.StaticFieldBytes[iType], offset = 0, pointerSize = snapshot.VirtualMachineInformation.PointerSize };
////                return o;
////            }

////            //index from an imaginary array composed of native objects followed by managed objects.
////            public static ObjectData FromUnifiedObjectIndex(SnapshotFileData snapshot, long index)
////            {
////                int iNative = snapshot.UnifiedObjectIndexToNativeObjectIndex(index);
////                if (iNative >= 0)
////                {
////                    return FromNativeObjectIndex(snapshot, iNative);
////                }

////                int iManaged = snapshot.UnifiedObjectIndexToManagedObjectIndex(index);
////                if (iManaged >= 0)
////                {
////                    return FromManagedObjectIndex(snapshot, iManaged);
////                }

////                return ObjectData.Invalid;
////            }

////            public static ObjectData FromNativeObjectIndex(SnapshotFileData snapshot, int index)
////            {
////                if (index < 0 || index >= snapshot.NativeObjects.Count)
////                    return ObjectData.Invalid;
////                ObjectData o = new ObjectData();
////                o.m_dataType = ObjectDataType.NativeObject;
////                o.m_data.native.index = index;
////                return o;
////            }

////            public static ObjectData FromManagedObjectInfo(SnapshotFileData snapshot, ManagedObjectInfo moi)
////            {
////                if (moi.ITypeDescription < 0)
////                    return ObjectData.Invalid;
////                ObjectData o = new ObjectData();
////                o.m_dataType = TypeToDataType(snapshot, moi.ITypeDescription);// ObjectDataType.Object;
////                o.m_data.managed.objectPtr = moi.PtrObject;
////                o.SetManagedType(snapshot, moi.ITypeDescription);
////                o.managedObjectData = moi.data;
////                return o;
////            }

////            public static ObjectData FromManagedObjectIndex(SnapshotFileData snapshot, int index)
////            {
////                if (index < 0 || index >= snapshot.CrawledData.ManagedObjects.Count)
////                    return ObjectData.Invalid;
////                var moi = snapshot.CrawledData.ManagedObjects[index];

////                if (index < snapshot.GcHandles.Count)
////                {
////                    //When snapshotting we might end up getting some handle targets as they are about to be collected
////                    //we do restart the world temporarily this can cause us to end up with targets that are not present in the dumped heaps
////                    if (moi.PtrObject == 0)
////                        return ObjectData.Invalid;

////                    if (moi.PtrObject != snapshot.GcHandles.Target[index])
////                    {
////                        throw new Exception("bad object");
////                    }
////                }

////                return FromManagedObjectInfo(snapshot, moi);
////            }

////            public static ObjectData FromManagedPointer(SnapshotFileData snapshot, ulong ptr, int asTypeIndex = -1)
////            {
////                if (ptr == 0)
////                    return Invalid;
////                int idx;
////                if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(ptr, out idx))
////                {
////                    return FromManagedObjectInfo(snapshot, snapshot.CrawledData.ManagedObjects[idx]);
////                }
////                else
////                {
////                    ObjectData o = new ObjectData();
////                    o.m_data.managed.objectPtr = ptr;
////                    o.managedObjectData = snapshot.ManagedHeapSections.Find(ptr, snapshot.VirtualMachineInformation);
////                    ManagedObjectInfo info = default(ManagedObjectInfo);
////                    if (Crawler.TryParseObjectHeader(snapshot, new Crawler.StackCrawlData() { ptr = ptr }, out info, o.managedObjectData))
////                    {
////                        if (asTypeIndex >= 0)
////                        {
////                            o.SetManagedType(snapshot, asTypeIndex);
////                        }
////                        else
////                        {
////                            o.SetManagedType(snapshot, info.ITypeDescription);
////                        }

////                        o.m_dataType = TypeToDataType(snapshot, info.ITypeDescription);
////                        return o;
////                    }
////                }
////                return Invalid;
////            }

////            public bool isNative
////            {
////                get
////                {
////                    return dataType == ObjectDataType.NativeObject;
////                }
////            }
////            public bool isManaged
////            {
////                get
////                {
////                    switch (dataType)
////                    {
////                        case ObjectDataType.Value:
////                        case ObjectDataType.Object:
////                        case ObjectDataType.Array:
////                        case ObjectDataType.BoxedValue:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.Type:
////                            return true;
////                    }
////                    return false;
////                }
////            }
////            public CodeType codeType
////            {
////                get
////                {
////                    switch (dataType)
////                    {
////                        case ObjectDataType.Value:
////                        case ObjectDataType.Object:
////                        case ObjectDataType.Array:
////                        case ObjectDataType.BoxedValue:
////                        case ObjectDataType.ReferenceObject:
////                        case ObjectDataType.ReferenceArray:
////                        case ObjectDataType.Type:
////                            return CodeType.Managed;
////                        case ObjectDataType.NativeObject:
////                            return CodeType.Native;
////                        default:
////                            return CodeType.Unknown;
////                    }
////                }
////            }

////            public bool IsGameObject(CachedSnapshot cs)
////            {
////                return cs.NativeObjects.NativeTypeArrayIndex[nativeObjectIndex] == cs.NativeTypes.GameObjectIdx;
////            }

////            public bool IsTransform(CachedSnapshot cs)
////            {
////                var id = cs.NativeObjects.NativeTypeArrayIndex[nativeObjectIndex];
////                if (id == cs.NativeTypes.TransformIdx || id == cs.NativeTypes.GameObjectIdx)
////                    return cs.NativeObjects.NativeTypeArrayIndex[nativeObjectIndex] == cs.NativeTypes.TransformIdx;
////                return false;
////            }

////            public bool IsRootTransform(CachedSnapshot cs)
////            {
////                return cs != null && cs.HasSceneRootsAndAssetbundles && cs.SceneRoots.RootTransformInstanceIdHashSet.Contains(GetInstanceID(cs));
////            }

////            public bool IsRootGameObject(CachedSnapshot cs)
////            {
////                return cs != null && cs.HasSceneRootsAndAssetbundles && cs.SceneRoots.RootGameObjectInstanceIdHashSet.Contains(GetInstanceID(cs));
////            }

////            public string GetAssetPath(CachedSnapshot cs)
////            {
////                for (int i = 0; i < cs.SceneRoots.SceneIndexedRootTransformInstanceIds.Length; i++)
////                {
////                    for (int ii = 0; ii < cs.SceneRoots.SceneIndexedRootTransformInstanceIds[i].Length; ii++)
////                    {
////                        if (cs.SceneRoots.SceneIndexedRootTransformInstanceIds[i][ii].Equals(GetInstanceID(cs)))
////                            return cs.SceneRoots.Path[i];
////                    }
////                }
////                return String.Empty;
////            }

////            public ObjectData[] GetAllReferencingObjects(CachedSnapshot cs)
////            {
////                return ObjectConnection.GetAllReferencingObjects(cs, displayObject);
////            }

////            public ObjectData[] GetAllReferencedObjects(CachedSnapshot cs)
////            {
////                return ObjectConnection.GetAllReferencedObjects(cs, displayObject);
////            }

////            public bool InvalidType()
////            {
////                return displayObject.dataType == ObjectDataType.Unknown;
////            }

////            public bool IsUnknownDataType()
////            {
////                return displayObject.dataType == ObjectDataType.Unknown;
////            }
////        }

////        internal struct ObjectConnection
////        {
////            public static ObjectData[] GetAllReferencingObjects(SnapshotFileData snapshot, ObjectData obj)
////            {
////                var referencingObjects = new List<ObjectData>();
////                long objIndex = -1;
////                switch (obj.dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.Object:
////                        {
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(obj.hostManagedObjectPtr, out var idx))
////                            {
////                                objIndex = snapshot.ManagedObjectIndexToUnifiedObjectIndex(idx);
////                                if (!snapshot.CrawledData.ConnectionsToMappedToUnifiedIndex.TryGetValue(objIndex, out var connectionIndicies))
////                                    break;

////                                //add crawled connections
////                                foreach (var i in connectionIndicies)
////                                {
////                                    var c = snapshot.CrawledData.Connections[i];
////                                    switch (c.connectionType)
////                                    {
////                                        case ManagedConnection.ConnectionType.ManagedObject_To_ManagedObject:

////                                            var objParent = ObjectData.FromManagedObjectIndex(snapshot, c.fromManagedObjectIndex);
////                                            if (c.fieldFrom >= 0)
////                                            {
////                                                referencingObjects.Add(objParent.GetInstanceFieldBySnapshotFieldIndex(snapshot, c.fieldFrom, false));
////                                            }
////                                            else if (c.arrayIndexFrom >= 0)
////                                            {
////                                                referencingObjects.Add(objParent.GetArrayElement(snapshot, c.arrayIndexFrom, false));
////                                            }
////                                            else
////                                            {
////                                                referencingObjects.Add(objParent);
////                                            }

////                                            break;
////                                        case ManagedConnection.ConnectionType.ManagedType_To_ManagedObject:

////                                            var objType = ObjectData.FromManagedType(snapshot, c.fromManagedType);
////                                            if (c.fieldFrom >= 0)
////                                            {
////                                                referencingObjects.Add(objType.GetInstanceFieldBySnapshotFieldIndex(snapshot, c.fieldFrom, false));
////                                            }
////                                            else if (c.arrayIndexFrom >= 0)
////                                            {
////                                                referencingObjects.Add(objType.GetArrayElement(snapshot, c.arrayIndexFrom, false));
////                                            }
////                                            else
////                                            {
////                                                referencingObjects.Add(objType);
////                                            }

////                                            break;
////                                        case ManagedConnection.ConnectionType.UnityEngineObject:
////                                            // these get at added in the loop at the end of the function
////                                            // tried using a hash set to prevent duplicates but the lookup during add locks up the window
////                                            // if there are more than about 50k references
////                                            //referencingObjects.Add(ObjectData.FromNativeObjectIndex(snapshot, c.UnityEngineNativeObjectIndex));
////                                            break;
////                                    }
////                                }
////                            }
////                            break;
////                        }
////                    case ObjectDataType.NativeObject:
////                        objIndex = snapshot.NativeObjectIndexToUnifiedObjectIndex(obj.nativeObjectIndex);
////                        if (!snapshot.CrawledData.ConnectionsMappedToNativeIndex.TryGetValue(obj.nativeObjectIndex, out var connectionIndices))
////                            break;

////                        //add crawled connection
////                        foreach (var i in connectionIndices)
////                        {
////                            switch (snapshot.CrawledData.Connections[i].connectionType)
////                            {
////                                case ManagedConnection.ConnectionType.ManagedObject_To_ManagedObject:
////                                case ManagedConnection.ConnectionType.ManagedType_To_ManagedObject:
////                                    break;
////                                case ManagedConnection.ConnectionType.UnityEngineObject:
////                                    referencingObjects.Add(ObjectData.FromManagedObjectIndex(snapshot, snapshot.CrawledData.Connections[i].UnityEngineManagedObjectIndex));
////                                    break;
////                            }
////                        }
////                        break;
////                }
////                //add connections from the raw snapshot
////                if (objIndex >= 0 && snapshot.Connections.ToFromMappedConnection.ContainsKey((int)objIndex))
////                {
////                    foreach (var i in snapshot.Connections.ToFromMappedConnection[(int)objIndex])
////                    {
////                        referencingObjects.Add(ObjectData.FromUnifiedObjectIndex(snapshot, i));
////                    }
////                }

////                return referencingObjects.ToArray();
////            }

////            public static int[] GetConnectedTransformInstanceIdsFromTransformInstanceId(SnapshotFileData snapshot, int instanceID)
////            {
////                HashSet<int> found = new HashSet<int>();
////                var objectData = ObjectData.FromNativeObjectIndex(snapshot, snapshot.NativeObjects.instanceId2Index[instanceID]);
////                if (snapshot.Connections.FromToMappedConnection.ContainsKey((int)objectData.GetUnifiedObjectIndex(snapshot)))
////                {
////                    var list = snapshot.Connections.FromToMappedConnection[(int)objectData.GetUnifiedObjectIndex(snapshot)];
////                    foreach (var connection in list)
////                    {
////                        objectData = ObjectData.FromUnifiedObjectIndex(snapshot, connection);
////                        if (objectData.isNative && snapshot.NativeTypes.TransformIdx == snapshot.NativeObjects.NativeTypeArrayIndex[objectData.nativeObjectIndex])
////                            found.Add(snapshot.NativeObjects.InstanceId[objectData.nativeObjectIndex]);
////                    }
////                }

////                int[] returnedObjectData = new int[found.Count];
////                found.CopyTo(returnedObjectData);
////                return returnedObjectData;
////            }

////            public static int GetGameObjectInstanceIdFromTransformInstanceId(SnapshotFileData snapshot, int instanceID)
////            {
////                var objectData = ObjectData.FromNativeObjectIndex(snapshot, snapshot.NativeObjects.instanceId2Index[instanceID]);
////                if (snapshot.Connections.FromToMappedConnection.ContainsKey((int)objectData.GetUnifiedObjectIndex(snapshot)))
////                {
////                    var list = snapshot.Connections.FromToMappedConnection[(int)objectData.GetUnifiedObjectIndex(snapshot)];
////                    foreach (var connection in list)
////                    {
////                        objectData = ObjectData.FromUnifiedObjectIndex(snapshot, connection);
////                        if (objectData.isNative && objectData.IsGameObject(snapshot) && snapshot.NativeObjects.ObjectName[objectData.nativeObjectIndex] == snapshot.NativeObjects.ObjectName[ObjectData.FromUnifiedObjectIndex(snapshot, connection).nativeObjectIndex])
////                            return snapshot.NativeObjects.InstanceId[objectData.nativeObjectIndex];
////                    }
////                }
////                return -1;
////            }

////            public static ObjectData[] GenerateReferencesTo(SnapshotFileData snapshot, ObjectData obj)
////            {
////                var referencedObjects = new List<ObjectData>();
////                long objIndex = -1;
////                HashSet<long> foundUnifiedIndices = new HashSet<long>();
////                switch (obj.dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.Object:
////                        {
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(obj.hostManagedObjectPtr, out var idx))
////                            {
////                                objIndex = snapshot.ManagedObjectIndexToUnifiedObjectIndex(idx);

////                                if (!snapshot.CrawledData.ConnectionsFromMappedToUnifiedIndex.TryGetValue(objIndex, out var connectionIdxs))
////                                    break;

////                                //add crawled connections
////                                foreach (var i in connectionIdxs)
////                                {
////                                    var c = snapshot.CrawledData.Connections[i];
////                                    switch (c.connectionType)
////                                    {
////                                        case ManagedConnection.ConnectionType.ManagedObject_To_ManagedObject:
////                                            referencedObjects.Add(ObjectData.FromUnifiedObjectIndex(snapshot, c.GetUnifiedIndexTo(snapshot)));
////                                            break;
////                                        case ManagedConnection.ConnectionType.UnityEngineObject:
////                                            // these get at added in the loop at the end of the function
////                                            // tried using a hash set to prevent duplicates but the lookup during add locks up the window
////                                            // if there are more than about 50k references
////                                            referencedObjects.Add(ObjectData.FromNativeObjectIndex(snapshot, c.UnityEngineNativeObjectIndex));
////                                            break;
////                                    }
////                                }
////                            }
////                            break;
////                        }
////                    case ObjectDataType.NativeObject:
////                        {
////                            objIndex = snapshot.NativeObjectIndexToUnifiedObjectIndex(obj.nativeObjectIndex);
////                            if (!snapshot.CrawledData.ConnectionsMappedToNativeIndex.TryGetValue(obj.nativeObjectIndex, out var connectionIdxs))
////                                break;

////                            //add crawled connection
////                            foreach (var i in connectionIdxs)
////                            {
////                                switch (snapshot.CrawledData.Connections[i].connectionType)
////                                {
////                                    case ManagedConnection.ConnectionType.ManagedObject_To_ManagedObject:
////                                    case ManagedConnection.ConnectionType.ManagedType_To_ManagedObject:
////                                        break;
////                                    case ManagedConnection.ConnectionType.UnityEngineObject:
////                                        var managedIndex = snapshot.CrawledData.Connections[i].UnityEngineManagedObjectIndex;
////                                        foundUnifiedIndices.Add(snapshot.ManagedObjectIndexToUnifiedObjectIndex(managedIndex));
////                                        referencedObjects.Add(ObjectData.FromManagedObjectIndex(snapshot, managedIndex));
////                                        break;
////                                }
////                            }
////                            break;
////                        }
////                    case ObjectDataType.Type:
////                        {     //TODO this will need to be changed at some point to use the mapped searches
////                            if (snapshot.TypeDescriptions.TypeIndexToArrayIndex.TryGetValue(obj.managedTypeIndex, out var idx))
////                            {
////                                //add crawled connections
////                                for (int i = 0; i != snapshot.CrawledData.Connections.Count; ++i)
////                                {
////                                    var c = snapshot.CrawledData.Connections[i];
////                                    if (c.connectionType == ManagedConnection.ConnectionType.ManagedType_To_ManagedObject && c.fromManagedType == idx)
////                                    {
////                                        if (c.fieldFrom >= 0)
////                                        {
////                                            referencedObjects.Add(obj.GetInstanceFieldBySnapshotFieldIndex(snapshot, c.fieldFrom, false));
////                                        }
////                                        else if (c.arrayIndexFrom >= 0)
////                                        {
////                                            referencedObjects.Add(obj.GetArrayElement(snapshot, c.arrayIndexFrom, false));
////                                        }
////                                        else
////                                        {
////                                            var referencedObject = ObjectData.FromManagedObjectIndex(snapshot, c.toManagedObjectIndex);
////                                            referencedObjects.Add(referencedObject);
////                                        }
////                                    }
////                                }
////                            }
////                            break;
////                        }
////                }

////                //add connections from the raw snapshot
////                if (objIndex >= 0 && snapshot.Connections.FromToMappedConnection.ContainsKey((int)objIndex))
////                {
////                    var cns = snapshot.Connections.FromToMappedConnection[(int)objIndex];
////                    foreach (var i in cns)
////                    {
////                        // Don't count Native -> Managed Connections again if they have been added based on m_CachedPtr entries
////                        if (!foundUnifiedIndices.Contains(i))
////                        {
////                            foundUnifiedIndices.Add(i);
////                            referencedObjects.Add(ObjectData.FromUnifiedObjectIndex(snapshot, i));
////                        }
////                    }
////                }
////                return referencedObjects.ToArray();
////            }

////            public static ObjectData[] GetAllReferencedObjects(SnapshotFileData snapshot, ObjectData obj)
////            {
////                var referencedObjects = new List<ObjectData>();
////                long objIndex = -1;
////                HashSet<long> foundUnifiedIndices = new HashSet<long>();
////                switch (obj.dataType)
////                {
////                    case ObjectDataType.Array:
////                    case ObjectDataType.BoxedValue:
////                    case ObjectDataType.Object:
////                        {
////                            if (snapshot.CrawledData.MangedObjectIndexByAddress.TryGetValue(obj.hostManagedObjectPtr, out var idx))
////                            {
////                                objIndex = snapshot.ManagedObjectIndexToUnifiedObjectIndex(idx);

////                                if (!snapshot.CrawledData.ConnectionsFromMappedToUnifiedIndex.TryGetValue(objIndex, out var connectionIdxs))
////                                    break;

////                                //add crawled connections
////                                foreach (var i in connectionIdxs)
////                                {
////                                    var c = snapshot.CrawledData.Connections[i];
////                                    switch (c.connectionType)
////                                    {
////                                        case ManagedConnection.ConnectionType.ManagedObject_To_ManagedObject:
////                                            if (c.fieldFrom >= 0)
////                                            {
////                                                referencedObjects.Add(obj.GetInstanceFieldBySnapshotFieldIndex(snapshot, c.fieldFrom, false));
////                                            }
////                                            else if (c.arrayIndexFrom >= 0)
////                                            {
////                                                referencedObjects.Add(obj.GetArrayElement(snapshot, c.arrayIndexFrom, false));
////                                            }
////                                            else
////                                            {
////                                                var referencedObject = ObjectData.FromManagedObjectIndex(snapshot, c.toManagedObjectIndex);
////                                                referencedObjects.Add(referencedObject);
////                                            }
////                                            break;
////                                        case ManagedConnection.ConnectionType.UnityEngineObject:
////                                            // these get at added in the loop at the end of the function
////                                            // tried using a hash set to prevent duplicates but the lookup during add locks up the window
////                                            // if there are more than about 50k references
////                                            referencedObjects.Add(ObjectData.FromNativeObjectIndex(snapshot, c.UnityEngineNativeObjectIndex));
////                                            break;
////                                    }
////                                }
////                            }
////                            break;
////                        }
////                    case ObjectDataType.NativeObject:
////                        {
////                            objIndex = snapshot.NativeObjectIndexToUnifiedObjectIndex(obj.nativeObjectIndex);
////                            if (!snapshot.CrawledData.ConnectionsMappedToNativeIndex.TryGetValue(obj.nativeObjectIndex, out var connectionIdxs))
////                                break;

////                            //add crawled connection
////                            foreach (var i in connectionIdxs)
////                            {
////                                switch (snapshot.CrawledData.Connections[i].connectionType)
////                                {
////                                    case ManagedConnection.ConnectionType.ManagedObject_To_ManagedObject:
////                                    case ManagedConnection.ConnectionType.ManagedType_To_ManagedObject:
////                                        break;
////                                    case ManagedConnection.ConnectionType.UnityEngineObject:
////                                        // A ManagedConnection.ConnectionType.UnityEngineObject comes about because a Managed field's m_CachedPtr points at a Native Object
////                                        // while that connection is technically correct and correctly bidirectional, the Native -> Managed side of the connection
////                                        // should already be tracked via snapshot.Connections, aka GCHandles reported in the snapshot.
////                                        // To avoid double reporting this connection, track these in the hashmap to de-dup the list when adding connections based on GCHandle reporting
////                                        var managedIndex = snapshot.CrawledData.Connections[i].UnityEngineManagedObjectIndex;
////                                        foundUnifiedIndices.Add(snapshot.ManagedObjectIndexToUnifiedObjectIndex(managedIndex));
////                                        referencedObjects.Add(ObjectData.FromManagedObjectIndex(snapshot, managedIndex));
////                                        break;
////                                }
////                            }
////                            break;
////                        }
////                    case ObjectDataType.Type:
////                        {     //TODO this will need to be changed at some point to use the mapped searches
////                            if (snapshot.TypeDescriptions.TypeIndexToArrayIndex.TryGetValue(obj.managedTypeIndex, out var idx))
////                            {
////                                //add crawled connections
////                                for (int i = 0; i != snapshot.CrawledData.Connections.Count; ++i)
////                                {
////                                    var c = snapshot.CrawledData.Connections[i];
////                                    if (c.connectionType == ManagedConnection.ConnectionType.ManagedType_To_ManagedObject && c.fromManagedType == idx)
////                                    {
////                                        if (c.fieldFrom >= 0)
////                                        {
////                                            referencedObjects.Add(obj.GetInstanceFieldBySnapshotFieldIndex(snapshot, c.fieldFrom, false));
////                                        }
////                                        else if (c.arrayIndexFrom >= 0)
////                                        {
////                                            referencedObjects.Add(obj.GetArrayElement(snapshot, c.arrayIndexFrom, false));
////                                        }
////                                        else
////                                        {
////                                            var referencedObject = ObjectData.FromManagedObjectIndex(snapshot, c.toManagedObjectIndex);
////                                            referencedObjects.Add(referencedObject);
////                                        }
////                                    }
////                                }
////                            }
////                            break;
////                        }
////                }

////                //add connections from the raw snapshot
////                if (objIndex >= 0 && snapshot.Connections.FromToMappedConnection.ContainsKey((int)objIndex))
////                {
////                    var cns = snapshot.Connections.FromToMappedConnection[(int)objIndex];
////                    foreach (var i in cns)
////                    {
////                        // Don't count Native -> Managed Connections again if they have been added based on m_CachedPtr entries
////                        if (!foundUnifiedIndices.Contains(i))
////                        {
////                            foundUnifiedIndices.Add(i);
////                            referencedObjects.Add(ObjectData.FromUnifiedObjectIndex(snapshot, i));
////                        }
////                    }
////                }
////                return referencedObjects.ToArray();
////            }
////        }
////        public unsafe class LowLevelFileReader : IDisposable
////        {
////            public struct ScheduleResult
////            {
////                public ReadError error;
////            }
////            public unsafe class GenericReadOperation
////            {
////                ReadError m_Err;
////                DynamicArray<byte> m_Buffer;

////                internal GenericReadOperation(DynamicArray<byte> buffer)
////                {
////                    m_Err = ReadError.InProgress;
////                    m_Buffer = buffer;
////                }

////                public ReadError Error
////                {
////                    get
////                    {
////                        return m_Err;
////                    }
////                    internal set { m_Err = value; }
////                }

////                public DynamicArray<byte> Result
////                {
////                    get
////                    {
////                        Checks.CheckEquals(true, Error == ReadError.Success);
////                        return m_Buffer;
////                    }
////                }
////            }

////            enum FormatSignature : uint
////            {
////                HeaderSignature = 0xAEABCDCD,
////                DirectorySignature = 0xCDCDAEAB,
////                FooterSignature = 0xABCDCDAE,
////                ChapterSectionVersion = 0x20170724,
////                BlockSectionVersion = 0x20170724
////            }

////            private GCHandle m_FilePath;
////            private readonly MemoryMappedFile m_MemoryMappedFile;
////            private readonly MemoryMappedViewAccessor m_Accessor;
////            private byte* m_Ptr;

////            public int FileLength { get; private set; }

////            public bool IsCreated { get { return m_FilePath.IsAllocated; } }
////            public string FilePath { get { return m_FilePath.Target as string; } }

////            public Blob16Byte BlockEntriesOffsets { get; private set; }

////            internal LowLevelFileReader(string filePath)
////            {
////                Checks.CheckFileExistsAndThrow(filePath);
////                var fileInfo = new FileInfo(filePath);
////                FileLength = (int)fileInfo.Length;
////                m_FilePath = GCHandle.Alloc(filePath, GCHandleType.Normal); //readonly no need to pin
////                m_MemoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
////                m_Accessor = m_MemoryMappedFile.CreateViewAccessor(0, FileLength, MemoryMappedFileAccess.Read);
////                m_Accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref m_Ptr);
////                InternalOpenCheck();
////            }

////            private void InternalOpenCheck()
////            { 
////                FormatSignature* sig = stackalloc FormatSignature[2];
////                *(sig + 0) = (FormatSignature)As<UInt32>(0);
////                *(sig + 1) = (FormatSignature)As<UInt32>(^UnsafeUtility.UIntSize..);

////                if (*sig != FormatSignature.HeaderSignature)
////                    throw new($"Header signature mismatch. Expected {FormatSignature.HeaderSignature} but got {*sig}");
////                if (*(sig + 1) != FormatSignature.FooterSignature)
////                    throw new($"Footer signature mismatch. Expected {FormatSignature.FooterSignature} but got {*(sig + 1)}");

////                long _8ByteBuffer = As<long>(^(UnsafeUtility.UIntSize + UnsafeUtility.LongSize)..);
////                if (!(_8ByteBuffer < FileLength && _8ByteBuffer > 0))
////                    throw new($"Invalid chapter location, {_8ByteBuffer}");

////                *(sig + 0) = (FormatSignature)As<UInt32>((int)_8ByteBuffer..);
////                *(sig + 1) = (FormatSignature)As<UInt32>((int)(_8ByteBuffer + UnsafeUtility.UIntSize)..);

////                if (*sig != FormatSignature.DirectorySignature)
////                    throw new($"Chapter signature mismatch. Expected {FormatSignature.DirectorySignature} but got {*sig}");
////                if (*(sig + 1) != FormatSignature.ChapterSectionVersion)
////                    throw new($"Chapter signature mismatch. Expected {FormatSignature.ChapterSectionVersion} but got {*(sig + 1)}");

////                var _offset = _8ByteBuffer + UnsafeUtility.UIntSize + UnsafeUtility.UIntSize;
////                _8ByteBuffer = As<long>(_offset);
////                _offset += UnsafeUtility.LongSize;

////                *(sig + 0) = (FormatSignature)As<UInt32>(_8ByteBuffer);
////                if (*sig != FormatSignature.BlockSectionVersion)
////                    throw new($"Block signature mismatch. Expected {FormatSignature.BlockSectionVersion} but got {*sig}");

////                Blob16Byte offsets;
////                long* dataPtr = (long*)(&offsets);
////                *dataPtr++ = _offset;
////                *dataPtr = _8ByteBuffer + UnsafeUtility.UIntSize;
////                BlockEntriesOffsets = offsets;
////            }

////            public ReadOnlySpan<byte> Span => new ReadOnlySpan<byte>(m_Ptr, FileLength);
////            public ReadOnlySpan<byte> this[Range range] => Span[range];
////            public T As<T>(Range range) where T : struct => MemoryMarshal.Read<T>(Span[range]);
////            public T As<T>(ulong start) where T : struct => MemoryMarshal.Read<T>(Span[(int)start..]);
////            public T As<T>(long start) where T : struct => MemoryMarshal.Read<T>(Span[(int)start..]);
////            public T As<T>(int start) where T : struct => MemoryMarshal.Read<T>(Span[start..]);

////            public T As<T>(ulong start, ulong count) where T : struct => MemoryMarshal.Read<T>(Span[(int)start..(int)(start + count)]);
////            public T As<T>(long start, long count) where T : struct => MemoryMarshal.Read<T>(Span[(int)start..(int)(start + count)]);
////            public T As<T>(int start, int count) where T : struct => MemoryMarshal.Read<T>(Span[start..(start + count)]);



////            public ReadOnlySpan<T> AsSpan<T>(Range range) where T : struct => MemoryMarshal.Cast<byte, T>(Span[range]);
////            public ReadOnlySpan<T> AsSpan<T>(long start, long count) where T : struct => AsSpan<T>((int)start..(int)(start + count));

////            //public long AbsPosition(long position) => position >= 0? position : FileLength + position;

////            public void Dispose()
////            {
////                if (!IsCreated)
////                    return;

////                FileLength = 0;
////                m_FilePath.Free();
////                m_Accessor.Dispose();
////                m_MemoryMappedFile.Dispose();
////                m_Ptr = null;
////                GC.SuppressFinalize(this);
////            }
////        }

////        internal static class VMTools
////        {
////            //supported archs
////            public const int X64ArchPtrSize = 8;
////            public const int X86ArchPtrSize = 4;

////            public static bool ValidateVirtualMachineInfo(VirtualMachineInformation vmInfo)
////            {
////                if (!(vmInfo.PointerSize == X64ArchPtrSize || vmInfo.PointerSize == X86ArchPtrSize))
////                    return false;

////                //partial checks to validate computations based on pointer size
////                int expectedObjHeaderSize = 2 * vmInfo.PointerSize;

////                if (expectedObjHeaderSize != vmInfo.ObjectHeaderSize)
////                    return false;

////                if (expectedObjHeaderSize != vmInfo.AllocationGranularity)
////                    return false;

////                return true;
////            }
////        }

////        unsafe static void ConvertDynamicArrayByteBufferToManagedArray<T>(DynamicArray<byte> nativeEntryBuffer, ref T[] elements) where T : class
////        {
////            byte* binaryDataStream = (byte*)nativeEntryBuffer.GetUnsafePtr();
////            //jump over the offsets array
////            long* binaryEntriesLength = (long*)binaryDataStream;
////            binaryDataStream = binaryDataStream + sizeof(long) * (elements.Length + 1); //+1 due to the final element offset being at the end

////            for (int i = 0; i < elements.Length; ++i)
////            {
////                byte* srcPtr = binaryDataStream + binaryEntriesLength[i];
////                int actualLength = (int)(binaryEntriesLength[i + 1] - binaryEntriesLength[i]);

////                if (typeof(T) == typeof(string))
////                {
////                    var nStr = new string('A', actualLength);
////                    elements[i] = nStr as T;
////                    fixed (char* dstPtr = nStr)
////                    {
////                        UnsafeUtility.MemCpyStride(dstPtr, UnsafeUtility.SizeOf<char>(),
////                            srcPtr, UnsafeUtility.SizeOf<byte>(), UnsafeUtility.SizeOf<byte>(), actualLength);
////                    }
////                }
////                else
////                {
////                    Span<byte> srcSpan = new Span<byte>(srcPtr, actualLength);
////                    object arr = null;
////                    if (typeof(T) == typeof(byte[]))
////                    {
////                        var bytes = new byte[actualLength];
////                        srcSpan.CopyTo(bytes);
////                        arr = bytes;
////                    }
////                    else if (typeof(T) == typeof(int[]))
////                    {
////                        var bytes = new int[actualLength / UnsafeUtility.SizeOf<int>()];
////                        srcSpan.CopyTo(MemoryMarshal.Cast<int, byte>(new Span<int>(bytes)));
////                        arr = bytes;
////                    }
////                    else if (typeof(T) == typeof(ulong[]))
////                    {
////                        var bytes = new ulong[actualLength / UnsafeUtility.SizeOf<ulong>()];
////                        srcSpan.CopyTo(MemoryMarshal.Cast<ulong, byte>(new Span<ulong>(bytes)));
////                        arr = bytes;
////                    }
////                    else if (typeof(T) == typeof(long[]))
////                    {
////                        var bytes = new long[actualLength / UnsafeUtility.SizeOf<long>()];
////                        srcSpan.CopyTo(MemoryMarshal.Cast<long, byte>(new Span<long>(bytes)));
////                        arr = bytes;
////                    }
////                    else
////                    {
////                        throw new Exception(string.Format("Unsuported type provided for conversion, type name: {0}", typeof(T).FullName));
////                        return;
////                    }

////                    //ulong handle = 0;
////                    //void* dstPtr = UnsafeUtility.PinGCArrayAndGetDataAddress(arr as Array, out handle);
////                    //UnsafeUtility.MemCpy(dstPtr, srcPtr, actualLength);
////                    //UnsafeUtility.ReleaseGCObject(handle);
////                    elements[i] = arr as T;
////                }
////            }
////        }

////    }

//    namespace Format
//    {
//        [StructLayout(LayoutKind.Explicit, Size = 16, Pack = 4)]
//        struct Blob16Byte { }
//        struct BlockHeader
//        {
//            public readonly ulong ChunkSize;
//            public readonly ulong TotalBytes;
//        }

//        unsafe struct Block 
//        {
//            public readonly BlockHeader Header;
//            public readonly uint OffsetCount;
//            public readonly long[] Offsets;
//            public Block(BlockHeader header)
//            {
//                Header = header;
//                OffsetCount = (uint)((Header.TotalBytes / Header.ChunkSize) + (Header.TotalBytes % Header.ChunkSize != 0UL ? 1UL : 0UL));
//                Offsets = new long[OffsetCount];
//            }
//        }

//        public enum EntryFormat : ushort
//        {
//            Undefined = 0,
//            SingleElement,
//            ConstantSizeElementArray,
//            DynamicSizeElementArray
//        }

//        [StructLayout(LayoutKind.Explicit, Size = 18, Pack = 2)]
//        struct EntryHeader
//        {
//            [FieldOffset(0)]
//            public readonly EntryFormat Format;
//            [FieldOffset(2)]
//            public readonly uint BlockIndex;
//            //the data in this meta value needs to be interpreted based on entry format, can be entry size or entries count
//            [FieldOffset(6)]
//            public readonly uint EntriesMeta;
//            //the Data in this meta value needs to be interpreted based on EntryFormat
//            [FieldOffset(10)]
//            public readonly ulong HeaderMeta;
//        }

//        unsafe struct Entry
//        {
//            public readonly EntryHeader Header;
//            //long* m_AdditionalEntryStorage;
//            public readonly long[] AdditionalEntryStorage;

//            public uint Count
//            {
//                get
//                {
//                    switch (Header.Format)
//                    {
//                        case EntryFormat.SingleElement:
//                            return 1;
//                        case EntryFormat.ConstantSizeElementArray:
//                            return (uint)Header.HeaderMeta;
//                        case EntryFormat.DynamicSizeElementArray:
//                            return Header.EntriesMeta;
//                        default:
//                            return 0;
//                    }
//                }
//            }

//            public long ComputeByteSizeForEntryRange(long offset, long count, bool includeOffsetsMemory)
//            {
//                switch (Header.Format)
//                {
//                    case EntryFormat.SingleElement:
//                        return Header.EntriesMeta;
//                    case EntryFormat.ConstantSizeElementArray:
//                        return Header.EntriesMeta * (count - offset);
//                    case EntryFormat.DynamicSizeElementArray:
//                        long size = 0;
//                        if (count + offset == Count)
//                        {
//                            var entryOffset = AdditionalEntryStorage[offset];
//                            size = (long)(Header.HeaderMeta - (ulong)entryOffset); //adding the size of the last element
//                        }
//                        else
//                            size = (AdditionalEntryStorage[offset + count] - AdditionalEntryStorage[offset]);

//                        return size + (includeOffsetsMemory ? (UnsafeUtility.LongSize * (count + 1)) : 0);
//                    default:
//                        return 0;
//                }
//            }

//            public Entry(EntryHeader header)
//            {
//                AdditionalEntryStorage = Array.Empty<long>();
//                Header = header;

//                switch (Header.Format)
//                {
//                    // we read uint64 and that's the offset in the block
//                    case EntryFormat.SingleElement:
//                    //we cast the uint64 value to uint32 in order to recover the array size
//                    case EntryFormat.ConstantSizeElementArray:
//                        break;
//                    case EntryFormat.DynamicSizeElementArray:
//                        //read from the second index and override the meta value stored in the header with total size
//                        //m_AdditionalEntryStorage = (long*)UnsafeUtility.Malloc(sizeof(long) * header.EntriesMeta, UnsafeUtility.AlignOf<long>());
//                        AdditionalEntryStorage = new long[header.EntriesMeta];
//                        break;
//                    case EntryFormat.Undefined:
//                        //Unwritten block, should be skipped
//                        break;
//                    default:
//                        Checks.ThrowExceptionGeneric<IOException>("Invalid chapter format");
//                        break;
//                }
//            }

//            //public unsafe long* GetAdditionalStoragePtr() { return m_AdditionalEntryStorage; }
//        }

//        public enum FormatVersion : uint
//        {
//            SnapshotMinSupportedFormatVersion = 8, //Added metadata to file, min supported version for capture
//            NativeConnectionsAsInstanceIdsVersion = 10, //native object collection reworked, added new gchandleIndex array to native objects for fast managed object access (2019.3 or newer?)
//            ProfileTargetInfoAndMemStatsVersion = 11, //added profile target info and memory summary struct (shortly before 2021.2.0a12 on 2021.2, backported together with v.12)
//            MemLabelSizeAndHeapIdVersion = 12, //added gc heap / vm heap identification encoded within each heap address and memory label size reporting (2021.2.0a12, 2021.1.9, 2020.3.12f1, 2019.4.29f1 or newer)
//            SceneRootsAndAssetBundlesVersion = 13, //added scene roots and asset bundle relations (not yet landed)
//            GfxResourceReferencesAndAllocatorsVersion = 14 //added gfx resource to root mapping and allocators information (including extra allocator information to memory labels)
//        };

//        public enum EntryType : ushort
//        {
//            Metadata_Version = 0,
//            Metadata_RecordDate,
//            Metadata_UserMetadata,
//            Metadata_CaptureFlags,
//            Metadata_VirtualMachineInformation,
//            NativeTypes_Name,
//            NativeTypes_NativeBaseTypeArrayIndex,
//            NativeObjects_NativeTypeArrayIndex,
//            NativeObjects_HideFlags,
//            NativeObjects_Flags,
//            NativeObjects_InstanceId,
//            NativeObjects_Name,
//            NativeObjects_NativeObjectAddress,
//            NativeObjects_Size,
//            NativeObjects_RootReferenceId,
//            GCHandles_Target,
//            Connections_From,
//            Connections_To,
//            ManagedHeapSections_StartAddress,
//            ManagedHeapSections_Bytes,
//            ManagedStacks_StartAddress,
//            ManagedStacks_Bytes,
//            TypeDescriptions_Flags,
//            TypeDescriptions_Name,
//            TypeDescriptions_Assembly,
//            TypeDescriptions_FieldIndices,
//            TypeDescriptions_StaticFieldBytes,
//            TypeDescriptions_BaseOrElementTypeIndex,
//            TypeDescriptions_Size,
//            TypeDescriptions_TypeInfoAddress,
//            TypeDescriptions_TypeIndex,
//            FieldDescriptions_Offset,
//            FieldDescriptions_TypeIndex,
//            FieldDescriptions_Name,
//            FieldDescriptions_IsStatic,
//            NativeRootReferences_Id,
//            NativeRootReferences_AreaName,
//            NativeRootReferences_ObjectName,
//            NativeRootReferences_AccumulatedSize,
//            NativeAllocations_MemoryRegionIndex,
//            NativeAllocations_RootReferenceId,
//            NativeAllocations_AllocationSiteId,
//            NativeAllocations_Address,
//            NativeAllocations_Size,
//            NativeAllocations_OverheadSize,
//            NativeAllocations_PaddingSize,
//            NativeMemoryRegions_Name,
//            NativeMemoryRegions_ParentIndex,
//            NativeMemoryRegions_AddressBase,
//            NativeMemoryRegions_AddressSize,
//            NativeMemoryRegions_FirstAllocationIndex,
//            NativeMemoryRegions_NumAllocations,
//            NativeMemoryLabels_Name,
//            NativeAllocationSites_Id,
//            NativeAllocationSites_MemoryLabelIndex,
//            NativeAllocationSites_CallstackSymbols,
//            NativeCallstackSymbol_Symbol,
//            NativeCallstackSymbol_ReadableStackTrace,
//            NativeObjects_GCHandleIndex,
//            ProfileTarget_Info,
//            ProfileTarget_MemoryStats,
//            NativeMemoryLabels_Size,
//            SceneObjects_Name,
//            SceneObjects_Path,
//            SceneObjects_AssetPath,
//            SceneObjects_BuildIndex,
//            SceneObjects_RootIdCounts,
//            SceneObjects_RootIdOffsets,
//            SceneObjects_RootIds,
//            // GfxResourceReferencesAndAllocatorsVersion = 14
//            // Added gfx resource to root mapping and allocators information (including extra allocator information to memory labels)
//            NativeMemoryLabels_AllocatorIdentifier,
//            NativeGfxResourceReferences_Id,
//            NativeGfxResourceReferences_Size,
//            NativeGfxResourceReferences_RootId,
//            NativeAllocatorInfo_AllocatorName,
//            NativeAllocatorInfo_Identifier,
//            NativeAllocatorInfo_UsedSize,
//            NativeAllocatorInfo_ReservedSize,
//            NativeAllocatorInfo_OverheadSize,
//            NativeAllocatorInfo_PeakUsedSize,
//            NativeAllocatorInfo_AllocationCount,
//            NativeAllocatorInfo_Flags,
//            Count, //used to keep track of entry count, only add c++ matching entries above this one
//        }

//        public enum ReadError : ushort
//        {
//            None = 0,
//            Success,
//            InProgress,
//            FileReadFailed,
//            FileNotFound,
//            InvalidHeaderSignature,
//            InvalidDirectorySignature,
//            InvalidFooterSignature,
//            InvalidChapterLocation,
//            InvalidChapterSectionVersion,
//            InvalidBlockSectionVersion,
//            InvalidBlockSectionCount,
//            InvalidEntryFormat,
//            EmptyFormatEntry
//        }

//        [Flags]
//        internal enum ObjectFlags
//        {
//            IsDontDestroyOnLoad = 0x1,
//            IsPersistent = 0x2,
//            IsManager = 0x4,
//        }

//        [Flags]
//        internal enum TypeFlags
//        {
//            kNone = 0,
//            kValueType = 1 << 0,
//            kArray = 1 << 1,
//            kArrayRankMask = unchecked((int)0xFFFF0000)
//        }


//        internal struct VirtualMachineInformation
//        {
//            public int PointerSize { get; internal set; }
//            public int ObjectHeaderSize { get; internal set; }
//            public int ArrayHeaderSize { get; internal set; }
//            public int ArrayBoundsOffsetInHeader { get; internal set; }
//            public int ArraySizeOffsetInHeader { get; internal set; }
//            public int AllocationGranularity { get; internal set; }
//        }

//        [StructLayout(LayoutKind.Sequential, Size = 260)]
//        internal unsafe struct ProfileTargetMemoryStats : IEquatable<ProfileTargetMemoryStats>
//        {
//            const int k_FreeBlockPowOf2BucketCount = 32;
//            const int k_PaddingSize = 32;

//            public readonly ulong TotalVirtualMemory;
//            public readonly ulong TotalUsedMemory;
//            public readonly ulong TotalReservedMemory;
//            public readonly ulong TempAllocatorUsedMemory;
//            public readonly ulong GraphicsUsedMemory;
//            public readonly ulong AudioUsedMemory;
//            public readonly ulong GcHeapUsedMemory;
//            public readonly ulong GcHeapReservedMemory;
//            public readonly ulong ProfilerUsedMemory;
//            public readonly ulong ProfilerReservedMemory;
//            public readonly ulong MemoryProfilerUsedMemory;
//            public readonly ulong MemoryProfilerReservedMemory;
//            public readonly uint FreeBlockBucketCount;
//            fixed uint m_FreeBlockBuckets[k_FreeBlockPowOf2BucketCount];
//            fixed byte m_Padding[k_PaddingSize];

//            public bool Equals(ProfileTargetMemoryStats other)
//            {
//                unsafe
//                {
//                    fixed (void* freeBlocks = m_FreeBlockBuckets)
//                        return TotalVirtualMemory == other.TotalVirtualMemory
//                            && TotalUsedMemory == other.TotalUsedMemory
//                            && TempAllocatorUsedMemory == other.TempAllocatorUsedMemory
//                            && TotalReservedMemory == other.TotalReservedMemory
//                            && GraphicsUsedMemory == other.GraphicsUsedMemory
//                            && AudioUsedMemory == other.AudioUsedMemory
//                            && GcHeapUsedMemory == other.GcHeapUsedMemory
//                            && GcHeapReservedMemory == other.GcHeapReservedMemory
//                            && ProfilerUsedMemory == other.ProfilerUsedMemory
//                            && ProfilerReservedMemory == other.ProfilerReservedMemory
//                            && MemoryProfilerUsedMemory == other.MemoryProfilerUsedMemory
//                            && MemoryProfilerReservedMemory == other.MemoryProfilerReservedMemory
//                            && FreeBlockBucketCount == other.FreeBlockBucketCount
//                            && UnsafeUtility.MemCmp((byte*)freeBlocks, (byte*)other.m_FreeBlockBuckets, (uint)(sizeof(uint) * k_FreeBlockPowOf2BucketCount));
//                }
//            }

//        };

//        [StructLayout(LayoutKind.Sequential, Size = 512)]
//        internal unsafe struct ProfileTargetInfo : IEquatable<ProfileTargetInfo>
//        {
//            const int k_UnityVersionBufferSize = 16;
//            const int k_ProductNameBufferSize = 256;
//            // decrease value when adding new members to target info
//            const int k_FormatPaddingSize = 192;

//            public readonly uint SessionGUID;
//            public readonly RuntimePlatform RuntimePlatform;
//            public readonly GraphicsDeviceType GraphicsDeviceType;
//            public readonly ulong TotalPhysicalMemory;
//            public readonly ulong TotalGraphicsMemory;
//            public readonly ScriptingImplementation ScriptingBackend;
//            public readonly double TimeSinceStartup;
//            readonly uint m_UnityVersionLength;
//            fixed byte m_UnityVersionBuffer[k_UnityVersionBufferSize];
//            readonly uint m_ProductNameLength;
//            fixed byte m_ProductNameBuffer[k_ProductNameBufferSize];
//            // space for later expansion of the format
//            fixed byte m_Padding[k_FormatPaddingSize];

//            public string UnityVersion
//            {
//                get
//                {
//                    fixed (byte* ptr = m_UnityVersionBuffer)
//                        return MakeStringFromBuffer(ptr, m_UnityVersionLength);
//                }
//            }
//            public string ProductName
//            {
//                get
//                {
//                    fixed (byte* ptr = m_ProductNameBuffer)
//                        return MakeStringFromBuffer(ptr, m_ProductNameLength);
//                }
//            }

//            unsafe string MakeStringFromBuffer(byte* srcPtr, uint length)
//            {
//                if (length == 0)
//                    return string.Empty;

//                //string str = new string('A', (int)length);
//                //fixed (char* dstPtr = str)
//                //{
//                //    UnsafeUtility.MemCpyStride(dstPtr, Marshal.SizeOf<char>(), srcPtr, Marshal.SizeOf<byte>(), Marshal.SizeOf<byte>(), (int)length);
//                //    //new Span<byte>(srcPtr, (int)length).CopyTo(MemoryMarshal.Cast<char, byte>(new Span<char>(dstPtr, (int)length)));
//                //}
//                string str = Marshal.PtrToStringAnsi((IntPtr)srcPtr, (int)length);
//                return str;
//            }

//            public bool Equals(ProfileTargetInfo other)
//            {
//                unsafe
//                {
//                    fixed (void* prod = m_ProductNameBuffer, version = m_UnityVersionBuffer)
//                        return SessionGUID == other.SessionGUID
//                            && RuntimePlatform == other.RuntimePlatform
//                            && GraphicsDeviceType == other.GraphicsDeviceType
//                            && TotalPhysicalMemory == other.TotalPhysicalMemory
//                            && TotalGraphicsMemory == other.TotalGraphicsMemory
//                            && ScriptingBackend == other.ScriptingBackend
//                            && TimeSinceStartup == other.TimeSinceStartup
//                            && m_UnityVersionLength == other.m_UnityVersionLength
//                            && m_ProductNameLength == other.m_ProductNameLength
//                            && UnsafeUtility.MemCmp(prod, other.m_ProductNameBuffer, m_ProductNameLength)
//                            && UnsafeUtility.MemCmp(version, other.m_UnityVersionBuffer, m_UnityVersionLength);
//                }
//            }
//        };

//        [Flags]
//        internal enum CaptureFlags : uint
//        {
//            ManagedObjects = 1 << 0,
//            NativeObjects = 1 << 1,
//            NativeAllocations = 1 << 2,
//            NativeAllocationSites = 1 << 3,
//            NativeStackTraces = 1 << 4,
//        }
//    }

//    internal unsafe struct UnsafeUtility
//    {
//        internal static int IntSize => Marshal.SizeOf<int>();
//        internal static int UIntSize => Marshal.SizeOf<uint>();
//        internal static int LongSize => Marshal.SizeOf<long>();
//        internal static int ULongSize => Marshal.SizeOf<ulong>();

//        internal static int SizeOf<T>() where T : struct => Marshal.SizeOf<T>();


//        public static bool MemCmp(byte* ptr, byte* other, uint length)
//        {
//            for (int i = 0; i < length; i++)
//            {
//                if (ptr[i] != other[i])
//                    return false;
//            }

//            return true;
//        }

//        public static bool MemCmp(void* ptr, void* other, uint length)
//        {
//           return MemCmp((byte*)ptr, (byte*)other, length);
//        }

//        public static void MemCpyStride(void* dst, int dstStride, void* src, int srcStride, int elementSize, int elementCount)
//        {
//                byte* dstPtr = (byte*)dst;
//                byte* srcPtr = (byte*)src;

//                for (int i = 0; i < elementCount; i++)
//                {
//                    Buffer.MemoryCopy(srcPtr, dstPtr, elementSize, elementSize);
//                    dstPtr += dstStride;
//                    srcPtr += srcStride;
//                }
//            }

//        public unsafe static void MemCpy(void* destination, void* source, long size)
//        {
//           NativeMemory.Copy(destination, source, (uint)size);
//        }

//        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
//        public static extern void MemMove(void* dest, void* src, [MarshalAs(UnmanagedType.U4)] int length);
//    }

//    internal struct Checks
//    {
//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckEntryTypeValueIsValidAndThrow(EntryType val)
//        {
//            if (val == EntryType.Count || (int)val < 0)
//                throw new UnityException("Invalid Entry type");
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckIndexOutOfBoundsAndThrow(long index, long count)
//        {
//            if (index >= count)
//                throw new ArgumentOutOfRangeException("Index out of bounds.");
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckIndexInRangeAndThrow(long index, long count)
//        {
//            if (index < 0 || index > count)
//                throw new ArgumentOutOfRangeException("Index out of bounds.");
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckEquals<T>(T rhs, T lhs) where T : IEquatable<T>
//        {
//            if (!rhs.Equals(lhs))
//                throw new Exception(string.Format("Expected: {0}, but actual value was: {1}.", rhs, lhs));
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckNotEquals<T>(T rhs, T lhs) where T : IEquatable<T>
//        {
//            if (rhs.Equals(lhs))
//                throw new Exception(string.Format("Expected comparands to be different, but they were the same. Value: {0}", rhs));
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckNotNull(object obj)
//        {
//            if (obj == null)
//                throw new Exception("Expected provided parameter to be non-null");
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void CheckFileExistsAndThrow(string path)
//        {
//            if (!File.Exists(path))
//                throw new FileNotFoundException(string.Format("File not found at provided path: {0}", path));
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void ThrowExceptionGeneric<T>(string message) where T : Exception, new()
//        {
//            var except = (T)Activator.CreateInstance(typeof(T), message);
//            throw except;
//        }

//        [Conditional("ENABLE_MEMORY_PROFILER_DEBUG")]
//        public static void IsTrue(bool condition)
//        {
//            if (!condition)
//                throw new Exception("Expected condition to be true, but was false.");
//        }
//    }

//    [Serializable]
//    public class UnityException : SystemException
//    {
//        private const int Result = -2147467261;

//        private string unityStackTrace;

//        public UnityException()
//            : base("A Unity Runtime error occurred!")
//        {
//            base.HResult = -2147467261;
//            unityStackTrace = Environment.StackTrace;
//        }

//        public UnityException(string message)
//            : base(message)
//        {
//            base.HResult = -2147467261;
//            unityStackTrace = Environment.StackTrace;
//        }

//        public UnityException(string message, Exception innerException)
//            : base(message, innerException)
//        {
//            base.HResult = -2147467261;
//            unityStackTrace = Environment.StackTrace;
//        }

//        protected UnityException(SerializationInfo info, StreamingContext context)
//            : base(info, context)
//        {
//            unityStackTrace = Environment.StackTrace;
//        }
//    }

//    public enum RuntimePlatform
//    {
//        //
//        // 摘要:
//        //     In the Unity editor on macOS.
//        OSXEditor = 0,
//        //
//        // 摘要:
//        //     In the player on macOS.
//        OSXPlayer = 1,
//        //
//        // 摘要:
//        //     In the player on Windows.
//        WindowsPlayer = 2,
//        //
//        // 摘要:
//        //     In the web player on macOS.
//        [Obsolete("WebPlayer export is no longer supported in Unity 5.4+.", true)]
//        OSXWebPlayer = 3,
//        //
//        // 摘要:
//        //     In the Dashboard widget on macOS.
//        [Obsolete("Dashboard widget on Mac OS X export is no longer supported in Unity 5.4+.", true)]
//        OSXDashboardPlayer = 4,
//        //
//        // 摘要:
//        //     In the web player on Windows.
//        [Obsolete("WebPlayer export is no longer supported in Unity 5.4+.", true)]
//        WindowsWebPlayer = 5,
//        //
//        // 摘要:
//        //     In the Unity editor on Windows.
//        WindowsEditor = 7,
//        //
//        // 摘要:
//        //     In the player on the iPhone.
//        IPhonePlayer = 8,
//        [Obsolete("Xbox360 export is no longer supported in Unity 5.5+.")]
//        XBOX360 = 10,
//        [Obsolete("PS3 export is no longer supported in Unity >=5.5.")]
//        PS3 = 9,
//        //
//        // 摘要:
//        //     In the player on Android devices.
//        Android = 11,
//        [Obsolete("NaCl export is no longer supported in Unity 5.0+.")]
//        NaCl = 12,
//        [Obsolete("FlashPlayer export is no longer supported in Unity 5.0+.")]
//        FlashPlayer = 0xF,
//        //
//        // 摘要:
//        //     In the player on Linux.
//        LinuxPlayer = 13,
//        //
//        // 摘要:
//        //     In the Unity editor on Linux.
//        LinuxEditor = 0x10,
//        //
//        // 摘要:
//        //     In the player on WebGL
//        WebGLPlayer = 17,
//        [Obsolete("Use WSAPlayerX86 instead")]
//        MetroPlayerX86 = 18,
//        //
//        // 摘要:
//        //     In the player on Windows Store Apps when CPU architecture is X86.
//        WSAPlayerX86 = 18,
//        [Obsolete("Use WSAPlayerX64 instead")]
//        MetroPlayerX64 = 19,
//        //
//        // 摘要:
//        //     In the player on Windows Store Apps when CPU architecture is X64.
//        WSAPlayerX64 = 19,
//        [Obsolete("Use WSAPlayerARM instead")]
//        MetroPlayerARM = 20,
//        //
//        // 摘要:
//        //     In the player on Windows Store Apps when CPU architecture is ARM.
//        WSAPlayerARM = 20,
//        [Obsolete("Windows Phone 8 was removed in 5.3")]
//        WP8Player = 21,
//        [Obsolete("BB10Player export is no longer supported in Unity 5.4+.")]
//        [EditorBrowsable(EditorBrowsableState.Never)]
//        BB10Player = 22,
//        [Obsolete("BlackBerryPlayer export is no longer supported in Unity 5.4+.")]
//        BlackBerryPlayer = 22,
//        [Obsolete("TizenPlayer export is no longer supported in Unity 2017.3+.")]
//        TizenPlayer = 23,
//        [Obsolete("PSP2 is no longer supported as of Unity 2018.3")]
//        PSP2 = 24,
//        //
//        // 摘要:
//        //     In the player on the Playstation 4.
//        PS4 = 25,
//        [Obsolete("PSM export is no longer supported in Unity >= 5.3")]
//        PSM = 26,
//        //
//        // 摘要:
//        //     In the player on Xbox One.
//        XboxOne = 27,
//        [Obsolete("SamsungTVPlayer export is no longer supported in Unity 2017.3+.")]
//        SamsungTVPlayer = 28,
//        [Obsolete("Wii U is no longer supported in Unity 2018.1+.")]
//        WiiU = 30,
//        //
//        // 摘要:
//        //     In the player on the Apple's tvOS.
//        tvOS = 0x1F,
//        //
//        // 摘要:
//        //     In the player on Nintendo Switch.
//        Switch = 0x20,
//        Lumin = 33,
//        //
//        // 摘要:
//        //     In the player on Stadia.
//        Stadia = 34,
//        //
//        // 摘要:
//        //     In the player on CloudRendering.
//        CloudRendering = 35,
//        [Obsolete("GameCoreScarlett is deprecated, please use GameCoreXboxSeries (UnityUpgradable) -> GameCoreXboxSeries", false)]
//        GameCoreScarlett = -1,
//        GameCoreXboxSeries = 36,
//        GameCoreXboxOne = 37,
//        //
//        // 摘要:
//        //     In the player on the Playstation 5.
//        PS5 = 38,
//        EmbeddedLinuxArm64 = 39,
//        EmbeddedLinuxArm32 = 40,
//        EmbeddedLinuxX64 = 41,
//        EmbeddedLinuxX86 = 42,
//        //
//        // 摘要:
//        //     In the server on Linux.
//        LinuxServer = 43,
//        //
//        // 摘要:
//        //     In the server on Windows.
//        WindowsServer = 44,
//        //
//        // 摘要:
//        //     In the server on macOS.
//        OSXServer = 45
//    }
//    public enum GraphicsDeviceType
//    {
//        //
//        // 摘要:
//        //     OpenGL 2.x graphics API. (deprecated, only available on Linux and MacOSX)
//        [Obsolete("OpenGL2 is no longer supported in Unity 5.5+")]
//        OpenGL2 = 0,
//        //
//        // 摘要:
//        //     Direct3D 9 graphics API.
//        [Obsolete("Direct3D 9 is no longer supported in Unity 2017.2+")]
//        Direct3D9 = 1,
//        //
//        // 摘要:
//        //     Direct3D 11 graphics API.
//        Direct3D11 = 2,
//        //
//        // 摘要:
//        //     PlayStation 3 graphics API.
//        [Obsolete("PS3 is no longer supported in Unity 5.5+")]
//        PlayStation3 = 3,
//        //
//        // 摘要:
//        //     No graphics API.
//        Null = 4,
//        [Obsolete("Xbox360 is no longer supported in Unity 5.5+")]
//        Xbox360 = 6,
//        //
//        // 摘要:
//        //     OpenGL ES 2.0 graphics API. (deprecated on iOS and tvOS)
//        OpenGLES2 = 8,
//        //
//        // 摘要:
//        //     OpenGL ES 3.0 graphics API. (deprecated on iOS and tvOS)
//        OpenGLES3 = 11,
//        [Obsolete("PVita is no longer supported as of Unity 2018")]
//        PlayStationVita = 12,
//        //
//        // 摘要:
//        //     PlayStation 4 graphics API.
//        PlayStation4 = 13,
//        //
//        // 摘要:
//        //     Xbox One graphics API using Direct3D 11.
//        XboxOne = 14,
//        //
//        // 摘要:
//        //     PlayStation Mobile (PSM) graphics API.
//        [Obsolete("PlayStationMobile is no longer supported in Unity 5.3+")]
//        PlayStationMobile = 0xF,
//        //
//        // 摘要:
//        //     iOS Metal graphics API.
//        Metal = 0x10,
//        //
//        // 摘要:
//        //     OpenGL (Core profile - GL3 or later) graphics API.
//        OpenGLCore = 17,
//        //
//        // 摘要:
//        //     Direct3D 12 graphics API.
//        Direct3D12 = 18,
//        //
//        // 摘要:
//        //     Nintendo 3DS graphics API.
//        [Obsolete("Nintendo 3DS support is unavailable since 2018.1")]
//        N3DS = 19,
//        //
//        // 摘要:
//        //     Vulkan (EXPERIMENTAL).
//        Vulkan = 21,
//        //
//        // 摘要:
//        //     Nintendo Switch graphics API.
//        Switch = 22,
//        //
//        // 摘要:
//        //     Xbox One graphics API using Direct3D 12.
//        XboxOneD3D12 = 23,
//        //
//        // 摘要:
//        //     Game Core Xbox One graphics API using Direct3D 12.
//        GameCoreXboxOne = 24,
//        [Obsolete("GameCoreScarlett is deprecated, please use GameCoreXboxSeries (UnityUpgradable) -> GameCoreXboxSeries", false)]
//        GameCoreScarlett = -1,
//        //
//        // 摘要:
//        //     Game Core XboxSeries graphics API using Direct3D 12.
//        GameCoreXboxSeries = 25,
//        PlayStation5 = 26,
//        PlayStation5NGGC = 27
//    }
//    public enum ScriptingImplementation
//    {
//        //
//        // 摘要:
//        //     The standard Mono 2.6 runtime.
//        Mono2x,
//        //
//        // 摘要:
//        //     Unity's .NET runtime.
//        IL2CPP,
//        //
//        // 摘要:
//        //     Microsoft's .NET runtime.
//        WinRTDotNET
//    }
//    [Flags]
//    public enum HideFlags
//    {
//        //
//        // 摘要:
//        //     A normal, visible object. This is the default.
//        None = 0x0,
//        //
//        // 摘要:
//        //     The object will not appear in the hierarchy.
//        HideInHierarchy = 0x1,
//        //
//        // 摘要:
//        //     It is not possible to view it in the inspector.
//        HideInInspector = 0x2,
//        //
//        // 摘要:
//        //     The object will not be saved to the Scene in the editor.
//        DontSaveInEditor = 0x4,
//        //
//        // 摘要:
//        //     The object is not editable in the Inspector.
//        NotEditable = 0x8,
//        //
//        // 摘要:
//        //     The object will not be saved when building a player.
//        DontSaveInBuild = 0x10,
//        //
//        // 摘要:
//        //     The object will not be unloaded by Resources.UnloadUnusedAssets.
//        DontUnloadUnusedAsset = 0x20,
//        //
//        // 摘要:
//        //     The object will not be saved to the Scene. It will not be destroyed when a new
//        //     Scene is loaded. It is a shortcut for HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
//        //     | HideFlags.DontUnloadUnusedAsset.
//        DontSave = 0x34,
//        //
//        // 摘要:
//        //     The GameObject is not shown in the Hierarchy, not saved to Scenes, and not unloaded
//        //     by Resources.UnloadUnusedAssets.
//        HideAndDontSave = 0x3D
//    }
//}

