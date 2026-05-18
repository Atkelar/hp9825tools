using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace HP9825CPU
{
    /// <summary>
    /// Provides a means of inspecting and modifying a tape cartridge's content.
    /// </summary>
    /// <remarks>
    /// <para>Most of the functionallity is contained within the tape cartridge object (private sub-classes) but this class adds a layer of comfort and validation to ensure proper data management.</para>
    /// </remarks>
    public class TapeCartridgeManager
        : IDisposable
    {
        private TapeCartridge? _Cartridge;

        public TapeCartridgeManager(TapeCartridge cartridge)
        {
            cartridge.Lock();
            _Cartridge = cartridge; // remember *after* the lock call...
        }

        /// <summary>
        /// This command mimics the "ert" command.
        /// </summary>
        /// <param name="track">The track to use.</param>
        /// <param name="firstFileToRemove">The index of the file to become the new "null" file, i.e. the "end of tape". Everything from here on will be deleted.</param>
        public void Erase(TapeTrack track, int firstFileToRemove)
        {
            EnsureWriteCartridge();
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method emulates the "mrk" command; it starts laying down a new tape structure and returns the number of successfully created files.
        /// </summary>
        /// <param name="track">The track of the tape to write.</param>
        /// <param name="fileSize">The size of each of the new files.</param>
        /// <param name="numberOfFiles">The number of files to create.</param>
        /// <param name="firstFileToOverwrite">The first index of the file</param>
        /// <returns>As with the "mrk" command, the last usable file number is returned; i.e. "return + 1" is the file index of the null-file.</returns>
        public int Mark(TapeTrack track, int numberOfFiles, int fileSize, int firstFileToOverwrite = 0)
        {
            EnsureWriteCartridge();
            throw new NotImplementedException();
        }

        private void EnsureWriteCartridge()
        {
            if (_Cartridge== null || _Cartridge.Readonly)
                throw new InvalidOperationException("Cartridge is read only!");
        }

        public IEnumerable<FileEntry> ReadDirectory(TapeTrack track)
        {
            if (_Cartridge == null)
                return Array.Empty<FileEntry>();
            return _Cartridge.GetDirectory((int)track);
        }

        public FileContent? ReadFile(TapeTrack track, int fileIndex = 0)
        {
            if (_Cartridge == null)
                return null;
            return _Cartridge.ReadFile((int)track, fileIndex);
        }

        public RawFileContent ReadFileRaw(TapeTrack track, int fileIndex = 0)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Updated the content of an existing file.
        /// </summary>
        /// <param name="file">The file to update. Has to have been loaded via <see cref="ReadFileRaw(TapeTrack, int)"/> or any other of the Read??File methods before the update. Note that the type of the file may change, but not the size limit!</param>
        public void UpdateFile(FileContent file)
        {
            throw new NotImplementedException();
        }

        public NumericFileContent ReadNumericFile(TapeTrack track, int fileIndex = 0)
        {
            throw new NotImplementedException();
        }

        public MixedFileContent ReadMixedFile(TapeTrack track, int fileIndex = 0)
        {
            throw new NotImplementedException();
        }

        public NumericFileContent CreateNumericFile(TapeTrack track, int fileIndex = 0, bool overwriteExisting = false)
        {
            throw new NotImplementedException();
        }

        public MixedFileContent CreateMixedFile(TapeTrack track, int fileIndex = 0, bool overwriteExisting = false)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (_Cartridge != null)
            {
                _Cartridge.Unlock();
            }
            _Cartridge = null;
        }

        public bool IsEmpty(int trk)
        {
            return _Cartridge?.IsEmpty(trk) ?? true;
        }
    }
}