using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace PersonalStickyNote
{
    public sealed class Note
    {
        public Guid Id = Guid.NewGuid();
        public string Rtf = "";
        public string PlainText = "";
        public string Color = "yellow";
        public bool Pinned;
        public bool Open;
        public Guid GroupId;
        public WindowPosition Position = new WindowPosition();
        public DateTime Updated = DateTime.UtcNow;
    }

    public sealed class NoteGroup
    {
        public Guid Id = Guid.NewGuid();
        public string Name = "";

        public override string ToString() { return Name; }
    }

    public sealed class WindowPosition
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    public sealed class Notebook
    {
        public string Theme = "system";
        public bool ConfirmDelete = true;
        public bool ListVisible = true;
        public WindowPosition ListPosition = new WindowPosition();
        public List<Note> Notes = new List<Note>();
        public List<NoteGroup> Groups = new List<NoteGroup>();
    }

    internal sealed class NoteStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Notebook));
        private readonly string file;
        private bool keepBackup;
        public bool Recovered { get; private set; }

        public NoteStore(string directory)
        {
            file = Path.Combine(directory, "notes.xml");
        }

        public Notebook Load()
        {
            try { return Read(file); }
            catch (FileNotFoundException)
            {
                if (!File.Exists(file + ".bak")) return new Notebook();
            }
            catch (DirectoryNotFoundException) { return new Notebook(); }
            catch (InvalidOperationException)
            {
                if (!File.Exists(file + ".bak")) throw;
            }

            var backup = Read(file + ".bak");
            Recovered = keepBackup = true;
            return backup;
        }

        public void Save(Notebook notebook)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                Serializer.Serialize(stream, notebook);
                stream.Flush(true);
            }

            if (File.Exists(file))
                File.Replace(temp, file, keepBackup ? null : file + ".bak", true);
            else
                File.Move(temp, file);
            keepBackup = false;
        }

        private static Notebook Read(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            }))
                return (Notebook)Serializer.Deserialize(reader);
        }
    }
}
