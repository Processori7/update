using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.IO.Compression;
class Program
{
    static void Main()
    {

        // Проверка, установлен ли git
        if (IsGitInstalled())
        {
            Console.WriteLine("Git установлен, выполняю git pull...");
            try
            {
                ExecuteCommand("git pull");
                Console.WriteLine("Команда выполнена успешно, проект обновлен!");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Возникла ошибка {ex.ToString()}\nНачинаю скачивание архива...");
                DownloadArchive();
            }
        }
        else
        {
            DownloadArchive();
        }
        Console.WriteLine("Обновлние завершено");
        Console.ReadKey();
    }

    static void DownloadArchive() 
    {
        Console.WriteLine("Git не установлен. Загружается архив...");
        string url = "https://github.com/Processori7/FreeAiChromeSidebar/archive/refs/heads/master.zip";
        string zipFile = "freeaichromesidebar-master.zip";
        string extractDir = "freeAiChromeSidebar-master";

        // Загрузка архива
        using (WebClient client = new WebClient())
        {
            client.DownloadFile(url, zipFile);
        }

        // Извлечение архива с помощью встроенных средств .NET
        try
        {
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true); // true для удаления всех подкаталогов и файлов
            }

            Console.WriteLine("Извлечение архива...");
            using (ZipArchive archive = ZipFile.OpenRead(zipFile))
            {
                archive.ExtractToDirectory(extractDir);
            }
            Console.WriteLine("Архив успешно извлечен.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при извлечении архива: " + ex.Message);
            Console.ReadKey();
            return;
        }

        // Удаление загруженного архива
        File.Delete(zipFile);

        if (Directory.Exists(extractDir))
        {
            Console.WriteLine($"Копирование содержимого");

            // Показать файлы в распакованной папке
            Console.WriteLine($"Содержимое папки \"{extractDir}\":");
            foreach (var file in Directory.GetFiles($"{extractDir}\\{extractDir}"))
            {
                Console.WriteLine(Path.GetFileName(file));
            }
            string currentDirectory = Directory.GetCurrentDirectory();
            foreach (var file in Directory.GetFiles($"{extractDir}\\{extractDir}"))
            {
                string destFile = Path.Combine(currentDirectory, Path.GetFileName(file));
                try
                {
                    File.Copy(file, destFile, true); // true для замены существующих файлов
                    Console.WriteLine($"Файл \"{Path.GetFileName(file)}\" скопирован в {currentDirectory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при копировании файла \"{Path.GetFileName(file)}\": {ex.Message}");
                }
            }
            Console.WriteLine($"Копирование файлов успешно завершено");
            // Удаление распакованной папки
            Directory.Delete(extractDir, true);
        }
        else
        {
            Console.WriteLine("Ошибка: Распакованная папка не найдена.");
            Console.ReadKey();
        }
    }

    static bool IsGitInstalled()
    {
        try
        {
            var processInfo = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    static void ExecuteCommand(string command)
    {
        var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine(output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("Ошибка: " + error);
                Console.ReadKey();
            }
        }
    }
}
