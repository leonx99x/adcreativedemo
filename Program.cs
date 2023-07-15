// ---------------------------------------------------------------------
//  Author : Koray Cil
//  File : Program.cs
//  Creation Date : 14/07/2023
//  Purpose : Bie web sitesindeki resimleri paralel indirme
// ---------------------------------------------------------------------
//  "The art of coding is not just writing codes, 
//   it's creating experiences that resonate and matter."
// ---------------------------------------------------------------------
//
//  © 2023 Koray CIL. All Rights Reserved.
//

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Program;

class Program
{
    private static HttpClient client = new HttpClient();
    private static CancellationTokenSource cts = new CancellationTokenSource();

    public delegate void ProgressHandler(int current, int total);
    public static event ProgressHandler ProgressChanged;


    public class Input
    {
        public int Count { get; set; }
        public int Parallelism { get; set; }
        public string SavePath { get; set; }
    }

    static async Task Main()
    {

        Console.Write("Resimleri indirmek için URL girin: ");
        string imageUrl = Console.ReadLine();

        string pattern = @"^http(s)?://([\w-]+.)+[\w-]+(/[\w- ;,./?%&=]*)?$";
        Regex regex = new Regex(pattern);
        while (!regex.IsMatch(imageUrl))
        {
            Console.WriteLine("Geçersiz url tekrar deneyin.");
            Console.Write("Resimleri indirmek için URL girin: ");
            imageUrl = Console.ReadLine();
        }

        Console.CancelKeyPress += new ConsoleCancelEventHandler(CtrlCHandler);
        ProgressChanged += PrintProgress;

        string json = await File.ReadAllTextAsync("Input.json");
        Input input = JsonSerializer.Deserialize<Input>(json);

        // folder varmı kontrol eder yoksa oluşturur.
        if (!Directory.Exists(input.SavePath))
        {
            Directory.CreateDirectory(input.SavePath);
        }

        // imajlar iindirilmeye başlandı
        for (int i = 0; i < input.Count; i += input.Parallelism)
        {
            int tasksToRun = Math.Min(input.Parallelism, input.Count - i);
            Task[] tasks = new Task[tasksToRun];

            for (int j = 0; j < tasksToRun; j++)
            {
                int imageNumber = i + j;
                tasks[j] = DownloadImage(imageNumber, input.Count, input.SavePath, imageUrl, cts.Token);
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("İndirme işlemi iptal edildi.");
                return;
            }
        }
    }

    static void CtrlCHandler(object sender, ConsoleCancelEventArgs args)
    {

        Console.WriteLine("\nTemizleme işlemi başlatıldı...");

        // İndirme işlemlerini iptal eder.
        cts.Cancel();

        args.Cancel = true;
    }

    static async Task DownloadImage(int imageNumber, int totalImages, string savePath, string imageUrl, CancellationToken ct)
    {
        byte[] imageBytes = await client.GetByteArrayAsync(imageUrl, ct);

        string filename = $"{savePath}/{imageNumber}.png";
        await File.WriteAllBytesAsync(filename, imageBytes, ct);

        // İlerleme durumu.
        ProgressChanged?.Invoke(imageNumber + 1, totalImages);
    }
    /// <summary>
    /// Bu methodu eğer sayfadaki imageler alınacaksa diye yaptım aktif değil
    /// </summary>
    /// <param name="url"></param>
    /// <param name="savePath"></param>
    /// <returns></returns>
    public async Task DownloadAllImages(string url, string savePath)
    {
        var httpClient = new HttpClient();
        var html = await httpClient.GetStringAsync(url);

        var htmlDocument = new HtmlAgilityPack.HtmlDocument();
        htmlDocument.LoadHtml(html);

        var imgTags = htmlDocument.DocumentNode.Descendants("img");

        foreach (var imgTag in imgTags)
        {
            var imgSrc = imgTag.GetAttributeValue("src", null);
            if (!string.IsNullOrEmpty(imgSrc))
            {
                var imgUri = new Uri(new Uri(url), imgSrc);
                var imageData = await httpClient.GetByteArrayAsync(imgUri);
                var fileName = Path.Combine(savePath, Path.GetFileName(imgUri.LocalPath));

                await File.WriteAllBytesAsync(fileName, imageData);
            }
        }
    }


    static void PrintProgress(int current, int total)
    {
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write($"İlerleme Durumu: {current}/{total}");
    }
}
