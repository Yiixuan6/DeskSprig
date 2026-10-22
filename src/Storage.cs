using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace DesktopWidgets
{
    public sealed class Storage
    {
        public readonly string DirectoryPath;
        public string RecoveryNotice = "";
        public Storage(string path)
        {
            DirectoryPath = path;
            Directory.CreateDirectory(path);
            string probe = Path.Combine(path, ".write-probe-" + Guid.NewGuid().ToString("N"));
            using (var f = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.WriteByte(0); f.Flush(true); }
            File.Delete(probe);
        }
        public T Load<T>(string name, Func<T, bool> valid, Func<T> defaults)
        {
            string path = Path.Combine(DirectoryPath, name);
            string backup = path + ".bak";
            if (!File.Exists(path) && !File.Exists(backup)) return defaults();
            Exception original = null;
            if (File.Exists(path))
            {
                try { return Read<T>(path, valid); }
                catch (Exception ex) { original = ex; }
            }
            T recovered;
            try { recovered = Read<T>(backup, valid); }
            catch (Exception ex) { throw new InvalidDataException(name + " 无法读取，且没有有效备份。原文件已保留，请先备份 data 文件夹再处理。", original ?? ex); }
            if (File.Exists(path)) File.Move(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            // Backup was validated; copy without replacing it with a corrupt primary.
            File.Copy(backup, path, false);
            RecoveryNotice += name + " 已从备份恢复；损坏文件已保留。\n";
            return recovered;
        }
        public static T Read<T>(string path, Func<T, bool> valid)
        {
            using (var stream = File.OpenRead(path))
            {
                T value = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                if (!valid(value)) throw new InvalidDataException("数据版本或内容无效：" + path);
                return value;
            }
        }
        public void Save<T>(string name, T value)
        {
            string path = Path.Combine(DirectoryPath, name), tmp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", true);
                else { File.Move(tmp, path); File.Copy(path, path + ".bak", true); }
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }
}
