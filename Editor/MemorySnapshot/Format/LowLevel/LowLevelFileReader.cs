using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Unity.IO.LowLevel.Unsafe;
using Unity.MemoryProfiler.Editor.Diagnostics;

namespace Unity.MemoryProfiler.Editor.Format.LowLevel.IO
{
    public enum ReadMode
    {
        Async,
        Blocking
    }

    public unsafe struct LowLevelFileReader : IDisposable
    {
        GCHandle m_FilePath;
        public long FileLength { get; private set; }
        public bool IsCreated { get { return m_FilePath.IsAllocated; } }
        public string FilePath { get { return m_FilePath.Target as string; } }

        private readonly MemoryMappedFile m_MemoryMappedFile;
        private readonly MemoryMappedViewAccessor m_Accessor;
        private byte* m_Ptr;
        public LowLevelFileReader(string filePath)
        {
            Checks.CheckFileExistsAndThrow(filePath);

            var fileInfo = new FileInfo(filePath);
            FileLength = fileInfo.Length;
            m_FilePath = GCHandle.Alloc(filePath, GCHandleType.Normal); //readonly no need to pin

            m_MemoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            m_Accessor = m_MemoryMappedFile.CreateViewAccessor(0, FileLength, MemoryMappedFileAccess.Read);
            m_Accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref m_Ptr);
        }
        private ReadOnlySpan<byte> Span => new ReadOnlySpan<byte>(m_Ptr, (int)FileLength);
        private ReadOnlySpan<byte> this[System.Range range] => Span[range];
        private T As<T>(System.Range range) where T : struct => MemoryMarshal.Read<T>(Span[range]);
        private T As<T>(ulong start) where T : struct => MemoryMarshal.Read<T>(Span[(int)start..]);
        private T As<T>(long start) where T : struct => MemoryMarshal.Read<T>(Span[(int)start..]);
        private T As<T>(int start) where T : struct => MemoryMarshal.Read<T>(Span[start..]);
        private ReadOnlySpan<T> AsSpan<T>(System.Range range) where T : struct => MemoryMarshal.Cast<byte, T>(Span[range]);


        public ReadHandle Read(ReadCommand* readCmds, uint cmdCount, ReadMode mode = ReadMode.Async)
        {
            ReadHandle handle;
            for (int i = 0; i < cmdCount; i++)
            {
                AsSpan<byte>((int)(readCmds + i)->Offset..(int)((readCmds + i)->Offset + (readCmds + i)->Size))
                    .CopyTo(new Span<byte>((readCmds + i)->Buffer, (int)(readCmds + i)->Size));
            }

            return new ReadHandle { status = ReadStatus.Complete, readCount = (int)cmdCount};
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            FileLength = 0;
            m_FilePath.Free();
            m_Accessor.Dispose();
            m_MemoryMappedFile.Dispose();
            m_Ptr = null;
        }
    }
}
