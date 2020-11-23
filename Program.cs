using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace _2lab {
    public class Serializer {

        public static string Serialize<T> (T ObjectToSerialize) {
            XmlSerializer xmlSerializer = new XmlSerializer (ObjectToSerialize.GetType ());

            using (StringWriter textWriter = new StringWriter ()) {
                xmlSerializer.Serialize (textWriter, ObjectToSerialize);
                return textWriter.ToString ();
            }
        }
    }

    [Serializable, XmlRoot ("config")]
    public class XMLConfig {
        [System.Xml.Serialization.XmlElementAttribute ("Source")]
        public string Source { get; set; }

        [System.Xml.Serialization.XmlElementAttribute ("Target")]
        public string Target { get; set; }

        [System.Xml.Serialization.XmlElementAttribute ("NeedToCompress")]
        public bool NeedToCompress { get; set; }

        [System.Xml.Serialization.XmlElementAttribute ("EncryptingKey")]
        public string EncryptingKey { get; set; }

        public XMLConfig () { }

    }

    public class JsonConfig {
        public string Source { get; set; }
        public string Target { get; set; }
        public bool NeedToCompress { get; set; }
        public string EncryptingKey { get; set; }
    }
    class Program {
        static string CurrentDirectory = Directory.GetCurrentDirectory ();
        static string ConfigDirectory = Path.Combine (CurrentDirectory, "Config");
        static string jsonFilePath = @"appsettings.json";
        static string xmlFilePath = @"config.xml";

        static dynamic config;

        public static XMLConfig Deserialize () {
            XMLConfig conf = null;
            XmlSerializer serializer = new XmlSerializer (typeof (XMLConfig));
            StreamReader reader = new StreamReader (Path.Combine (ConfigDirectory, xmlFilePath));
            conf = (XMLConfig) serializer.Deserialize (reader);
            reader.Close ();

            return conf;

        }
        public static string Encrypt (string clearText) {
            string EncryptionKey = config.EncryptingKey;
            byte[] clearBytes = Encoding.Unicode.GetBytes (clearText);
            using (Aes encryptor = Aes.Create ()) {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes (EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes (32);
                encryptor.IV = pdb.GetBytes (16);
                using (MemoryStream ms = new MemoryStream ()) {
                    using (CryptoStream cs = new CryptoStream (ms, encryptor.CreateEncryptor (), CryptoStreamMode.Write)) {
                        cs.Write (clearBytes, 0, clearBytes.Length);
                        cs.Close ();
                    }
                    clearText = Convert.ToBase64String (ms.ToArray ());
                }
            }
            return clearText;
        }
        public static string Decrypt (string cipherText) {
            string EncryptionKey = config.EncryptingKey;
            cipherText = cipherText.Replace (" ", "+");
            byte[] cipherBytes = Convert.FromBase64String (cipherText);
            using (Aes encryptor = Aes.Create ()) {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes (EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes (32);
                encryptor.IV = pdb.GetBytes (16);
                using (MemoryStream ms = new MemoryStream ()) {
                    using (CryptoStream cs = new CryptoStream (ms, encryptor.CreateDecryptor (), CryptoStreamMode.Write)) {
                        cs.Write (cipherBytes, 0, cipherBytes.Length);
                        cs.Close ();
                    }
                    cipherText = Encoding.Unicode.GetString (ms.ToArray ());
                }
            }
            return cipherText;
        }

        static void ProcessFile (string inputPath, bool encryptMode) {
            string directory = Path.Combine (CurrentDirectory, encryptMode ? config.Source : config.Target);
            string path = Path.Combine (directory, inputPath);
            if (encryptMode) {
                string dataToEncrypt = File.ReadAllText (path);
                File.WriteAllText (path, Encrypt (dataToEncrypt));
            } else {
                string dataToDecrypt = File.ReadAllText (path);
                File.WriteAllText (path, Decrypt (dataToDecrypt));
            }
        }
        private static void Run () {
            DetectConfig ();
            bool SourceExists = Directory.Exists (Path.Combine (CurrentDirectory, config.Source));
            bool TargetExists = Directory.Exists (Path.Combine (CurrentDirectory, config.Target));
            if (SourceExists && TargetExists) {
                string[] args = Environment.GetCommandLineArgs ();
                using (FileSystemWatcher watcher = new FileSystemWatcher ()) {
                    watcher.Path = Path.Combine (CurrentDirectory, config.Source);
                    watcher.Filter = "*.txt";
                    watcher.NotifyFilter = NotifyFilters.LastAccess |
                        NotifyFilters.LastWrite |
                        NotifyFilters.FileName |
                        NotifyFilters.DirectoryName;

                    watcher.Created += OnChanged;
                    watcher.EnableRaisingEvents = true;

                    Console.WriteLine ("Press 'q' to quit the program.");
                    while (Console.Read () != 'q');
                }
            } else
                throw new Exception ("Source or Target Directory Doesnt exist");
        }
        public static void Compress (string sourceFile) {
            string path = Path.Combine (CurrentDirectory, config.Source, sourceFile);
            string dest = Path.Combine (CurrentDirectory, config.Target, sourceFile.Split ('.') [0] + ".gz");
            if (File.Exists (path)) {
                using (FileStream sourceStream = new FileStream (path, FileMode.OpenOrCreate)) {
                    using (FileStream targetStream = File.Create (dest)) {
                        using (GZipStream compressionStream = new GZipStream (targetStream, CompressionMode.Compress)) {
                            sourceStream.CopyTo (compressionStream);
                        }
                    }
                }
            } else throw new Exception ("File doesnt exitst");
        }
        public static void Decompress (string compressedFile) {
            string path = Path.Combine (CurrentDirectory, config.Target, compressedFile);
            string dest = Path.Combine (CurrentDirectory, config.Target, compressedFile.Split ('.') [0] + ".txt");
            if (File.Exists (path)) {
                using (FileStream sourceStream = new FileStream (path, FileMode.OpenOrCreate)) {
                    // поток для записи восстановленного файла
                    using (FileStream targetStream = File.Create (dest)) {
                        // поток разархивации
                        using (GZipStream decompressionStream = new GZipStream (sourceStream, CompressionMode.Decompress)) {
                            decompressionStream.CopyTo (targetStream);
                        }
                    }
                }
            } else throw new Exception ("File doesnt exist");
        }

        private static void OnChanged (object source, FileSystemEventArgs e) {
            SuccessMessage ($"Encrypt {e.Name} data");
            ProcessFile (e.Name, true);
            if (config.NeedToCompress) {

                SuccessMessage ($"Compress {e.Name}");
                Compress (e.Name);
                SuccessMessage ($"Decompress {e.Name.Split('.')[0]}.gz");
                Decompress (e.Name.Split ('.') [0] + ".gz");
            } else {
                SuccessMessage ($"Moving {e.Name} from {config.Source} to {config.Target}");
                File.Move (Path.Combine (CurrentDirectory, config.Source, e.Name), Path.Combine (CurrentDirectory, config.Target, e.Name));
            }
            SuccessMessage ($"Decrypt {e.Name} data...");
            ProcessFile (e.Name, false);
        }

        public static void ChangeJsonConfig (string key, object value) {
            if (key == "Source") {
                config.Source = value.ToString ();
            } else if (key == "Target") {
                config.Target = value.ToString ();
            } else if (key == "EncKey") {
                config.EncryptingKey = value.ToString ();
            } else if (key == "NeedCompression") {
                config.NeedToCompress = (Boolean) value;
            }
            var options = new JsonSerializerOptions {
                WriteIndented = true,
            };
            string output = JsonSerializer.Serialize (config, options);
            File.WriteAllText (jsonFilePath, output);
        }

        public static void ChangeXmlConfig (string key, object value) {
            if (key == "Source") {
                config.Source = value.ToString ();
            } else if (key == "Target") {
                config.Target = value.ToString ();
            } else if (key == "EncKey") {
                config.EncryptingKey = value.ToString ();
            } else if (key == "NeedCompression") {
                config.NeedToCompress = (Boolean) value;
            }
            XmlSerializer formatter = new XmlSerializer (typeof (XMLConfig));

            string output = Serializer.Serialize<XMLConfig> (config);

            File.WriteAllText (xmlFilePath, output);
        }

        public static void DetectConfig () {
            bool JSON = Convert.ToBoolean (Directory.GetFiles (ConfigDirectory, "*.json").Length);
            bool XML = Convert.ToBoolean (Directory.GetFiles (ConfigDirectory, "*.xml").Length);

            if (JSON) {
                config = JsonSerializer.Deserialize<JsonConfig> (File.ReadAllText (Path.Combine (ConfigDirectory, jsonFilePath)));
            } else if (XML) {
                config = Deserialize ();
            }

        }

        static void SuccessMessage (string text) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine (text);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static void ErrorMessage (string text) {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine (text);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static void Main (string[] args) {
            // ChangeXmlConfig ("Source", "Testing");
            // ChangeJsonConfig ("NeedCompression", false);
            try {
                Run ();
            } catch (Exception e) {
                ErrorMessage (e.Message);
            }
        }
    }
}