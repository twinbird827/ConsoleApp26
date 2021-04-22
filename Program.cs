using GhostscriptSharp;
using GhostscriptSharp.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApp26
{
    class Program
    {
        static void Main(string[] args)
        {
            Execute(args).Wait();
        }

        private static async Task Execute(string[] args)
        {
            // ｵﾌﾟｼｮﾝを選択
            Console.WriteLine("起動ｵﾌﾟｼｮﾝを選択してください。");
            Console.WriteLine($"0: 元となるPDFﾌｧｲﾙを残す。");
            Console.WriteLine($"1: 処理が完了したらPDFﾌｧｲﾙを削除する。");
            Console.WriteLine($"ﾃﾞﾌｫﾙﾄ: {AppSettings.CopyPdf}");

            var option = GetOption(Console.ReadLine());

            var executes = args.Select(async arg =>
            {
                // 処理をﾃﾞﾘｹﾞｰﾄ化
                Func<bool> action = () => Execute(option, arg);

                // 非同期実行
                var iar = action.BeginInvoke(_ => { }, null);

                // 処理が終わるまで待機する
                while (!iar.IsCompleted)
                {
                    await Task.Delay(16);
                }

                // 非同期処理の結果を返却
                return action.EndInvoke(iar);
            });

            // 実行ﾊﾟﾗﾒｰﾀに対して処理実行
            var results = await Task.WhenAll(executes);

            if (results.Contains(false))
            {
                // ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
                Console.ReadLine();
            }
        }

        private static bool Execute(int option, string arg)
        {
            try
            {
                var settings = new GhostscriptSettings();

                settings.Device = GhostscriptDevices.jpeg;
                settings.Page.AllPages = true;
                settings.Resolution = new Size(AppSettings.Dpi, AppSettings.Dpi);
                settings.Size = new GhostscriptPageSize()
                {
                    Native = GhostscriptPageSizes.a1
                };

                // 元PDFのﾊﾟｽを絶対ﾊﾟｽに変換
                var pdf = RelativePathToAbsolutePath(arg);
                // GhostscriptWrapperが2ﾊﾞｲﾄ文字を許可していないので作業用ﾌｧｲﾙﾊﾟｽを作成
                var src = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString()}.pdf");
                var dsd = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                var dsf = Path.Combine(dsd, "out_%d.jpg");
                // 最終的に配置するﾃﾞｨﾚｸﾄﾘﾊﾟｽ
                var jpg = RelativePathToAbsolutePath(Path.Combine(
                    Path.GetDirectoryName(arg),
                    Path.GetFileNameWithoutExtension(arg)
                ));

                WriteConsole($"処理開始: {arg}");

                // 作業用にｺﾋﾟｰ
                File.Copy(pdf, src);
                // 作業用ﾃﾞｨﾚｸﾄﾘ作成
                Directory.CreateDirectory(dsd);
                // 作業用ﾃﾞｨﾚｸﾄﾘで画像変換
                GhostscriptWrapper.GenerateOutput(src, dsf, settings);
                // 連番のｾﾞﾛ埋め
                RenameInFiles(dsd);
                // 最終的な配置場所に移動
                DirectoryMove(dsd, jpg);

                if (option != 0)
                {
                    // ｵﾌﾟｼｮﾝによって元ﾌｧｲﾙを削除
                    File.Delete(pdf);
                }

                WriteConsole($"処理完了: {arg}");
                return true;
            }
            catch (Exception ex)
            {
                WriteConsole($"処理異常: {arg}");
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private static void DirectoryMove(string src, string dst)
        {
            var sinfo = new DirectoryInfo(src);
            var dinfo = new DirectoryInfo(dst);

            DirectoryDelete(dinfo);

            if (dinfo.Root.FullName == sinfo.Root.FullName)
            {
                // ﾄﾞﾗｲﾌﾞが同じならﾃﾞｨﾚｸﾄﾘの移動
                Directory.Move(src, dst);
            }
            else
            {
                // ﾄﾞﾗｲﾌﾞが異なるならﾃﾞｨﾚｸﾄﾘをｺﾋﾟｰして削除
                DirectoryCopy(src, dst);
                sinfo.Delete(true);
            }
        }

        private static void DirectoryDelete(DirectoryInfo info)
        {
            if (info.Exists)
            {
                // ﾃﾞｨﾚｸﾄﾘ内のﾌｧｲﾙ、またはﾃﾞｨﾚｸﾄﾘを削除可能な属性にする。
                foreach (var file in info.GetFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    if (file.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        file.Attributes = FileAttributes.Directory;
                    }
                    else
                    {
                        file.Attributes = FileAttributes.Normal;
                    }
                }

                // ﾃﾞｨﾚｸﾄﾘの削除
                info.Delete(true);
            }
        }

        private static void DirectoryCopy(string src, string dst)
        {
            DirectoryInfo srcdi = new DirectoryInfo(src);
            DirectoryInfo dstdi = new DirectoryInfo(dst);

            //ｺﾋﾟｰ先のﾃﾞｨﾚｸﾄﾘがなければ作成する
            if (dstdi.Exists == false)
            {
                dstdi.Create();
                dstdi.Attributes = srcdi.Attributes;
            }

            //ﾌｧｲﾙのｺﾋﾟｰ
            foreach (FileInfo fileInfo in srcdi.GetFiles())
            {
                //同じﾌｧｲﾙが存在していたら、常に上書きする
                fileInfo.CopyTo(Path.Combine(dstdi.FullName, fileInfo.Name), true);
            }

            // ﾃﾞｨﾚｸﾄﾘのｺﾋﾟｰ（再帰を使用）
            foreach (var directoryInfo in srcdi.GetDirectories())
            {
                DirectoryCopy(directoryInfo.FullName, Path.Combine(dstdi.FullName, directoryInfo.Name));
            }
        }

        private static void RenameInFiles(string path)
        {
            DirectoryInfo info = new DirectoryInfo(path);

            var files = info.GetFiles().ToArray();
            var c = $"{files.Count()}".Length;

            foreach (var file in files)
            {
                // ﾌｧｲﾙ数に応じてｾﾞﾛ埋めの数を変える。
                var name = Regex.Replace(file.Name, "[0-9]+", m => string.Format($"{{0,0:D{c}}}", int.Parse(m.Value)));
                var src = file.FullName;
                var dst = Path.Combine(Path.GetDirectoryName(src), name);

                if (src != dst)
                {
                    File.Move(src, dst);
                }
            }
        }

        private static string RelativePathToAbsolutePath(string relative)
        {
            var work = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            return Path.Combine(work, relative);
        }

        private static int GetOption(string src)
        {
            switch (src)
            {
                case "0":
                case "1":
                    return int.Parse(src);
                default:
                    return AppSettings.CopyPdf;
            }
        }

        private static void WriteConsole(string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("MM.dd HH:mm:ss.fff")}: {message}");
        }
    }
}
