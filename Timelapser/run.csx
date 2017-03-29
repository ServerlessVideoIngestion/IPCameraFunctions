#r "Microsoft.WindowsAzure.Storage"

using Microsoft.WindowsAzure.Storage.Blob;

using System.Diagnostics;
using System.IO;
using System.Text;

public static async Task Run(Stream myBlob, string name, Binder binder, TraceWriter log)
{
    log.Info($"Jordan C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");  

    byte[] bytes = null;

    using(var ms = new MemoryStream()){
        myBlob.CopyTo(ms);
        bytes = ms.ToArray();
    }    

    List<string> _directories = new List<string>();

    List<string> _files = new List<string>();

    var temp = Path.GetTempFileName() + ".mp4";   
    _files.Add(temp);


    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempPath);  

    _directories.Add(tempPath);

    var tempOut = Path.GetTempFileName() + ".mp4";
    _files.Add(tempOut);   

    File.WriteAllBytes(temp, bytes); 

    var splitArguments = $"-i \"{temp}\" -y -r 1/15 \"{tempPath}\\IMG_$filename%03d.jpg\"";  

    var result = _runFf(splitArguments, log);

    if(result != 0){
        _clean(_files, _directories);
        log.Info("Error: FF exit 1 not 0");
        return;
    }

    var fCount = Directory.GetFiles(tempPath);

    if(fCount.Length == 0){
        _clean(_files, _directories);
        log.Info("Error: File Count 0");
        return;
    }   

    //var timeLapseArguments = $"-r 15 -start_number 01 -i IMG_$filename%03d.jpg -f lavfi -i anullsrc -vcodec libx264  -c:a aac \"{tempOut}\"";
    var timeLapseArguments = $"-r 15 -start_number 01 -i IMG_$filename%03d.jpg -vcodec libx264  -c:a aac \"{tempOut}\"";

    result = _runFf(timeLapseArguments, log, tempPath);

    if(result != 0){
        _clean(_files, _directories);
        log.Info("Error: FF exit 2 not 0");
        return;
    }

    var outputReadBack = File.ReadAllBytes(tempOut);   

    var nameParts = name.Split('/');    

    var todayFile = $"timelapse/{nameParts[0]}/{nameParts[1]}/today.mp4";   

     var attributesReader = new Attribute[]
    {
        new BlobAttribute(todayFile),
        new StorageAccountAttribute("jortana_STORAGE")
    };

    var readerWriter = await binder.BindAsync<CloudBlockBlob>(attributesReader);

    if(await readerWriter.ExistsAsync()){
        //grab the file and append to it
       
        //read it for the join
         var tempJoinSource = Path.GetTempFileName();   
        _files.Add(tempJoinSource); 

       using(var ms = new MemoryStream()){
            await readerWriter.DownloadToStreamAsync(ms);
            File.WriteAllBytes(tempJoinSource, ms.ToArray());
       }       

        if(!File.Exists(tempJoinSource)){
            _clean(_files, _directories);
            log.Info("Error: temp file for download existing timelapse not found");
            return;
        }

        var concatFile = new StringBuilder();
        
        concatFile.AppendLine($"file '{tempJoinSource}'");
        concatFile.AppendLine($"file '{tempOut}'");

        var joinText = Path.Combine(tempPath, "join.txt");

        File.WriteAllText(joinText, concatFile.ToString());
        
        var fileTimeLapse = Path.Combine(tempPath, "timelapse.mp4");        

        var resultJoin = _runFf($"-f concat -safe 0 -i \"{joinText}\" -c copy \"{fileTimeLapse}\"", log, tempPath);        

        if(resultJoin != 0 || !File.Exists(fileTimeLapse)){
            _clean(_files, _directories);
            log.Info("Error: Could not create timelapse.");
            return;
        }

        var timeLapseBytes = File.ReadAllBytes(fileTimeLapse);

        await readerWriter.UploadFromByteArrayAsync(timeLapseBytes, 0, timeLapseBytes.Length);
       
        
        log.Info($"Uploaded daily timelapse to: {todayFile}");

    }else{
       
        var starterFile = File.ReadAllBytes(tempOut);
        //send up the current output as the starter file
        await readerWriter.UploadFromByteArrayAsync(starterFile, 0, starterFile.Length);
    }

     _clean(_files, _directories);
   
}

static void _clean(List<string> files, List<string> directories){
    foreach(var f in files){
        if(File.Exists(f)){
            File.Delete(f);
        }
    }

    foreach(var d in directories){
        if(Directory.Exists(d)){
            Directory.Delete(d, true);
        }
    }
}

 static int _runFf(string arugments, TraceWriter log, string workingDirectory = null)
        {
    var f = @"D:\home\site\wwwroot\Timelapser\ffmpeg.exe";

    var psi = new ProcessStartInfo();


    psi.FileName = f;
    psi.Arguments = arugments;
    psi.RedirectStandardOutput = true;
    psi.RedirectStandardError = true;
    psi.UseShellExecute = false;
    
    if (workingDirectory != null)
    {
        psi.WorkingDirectory = workingDirectory;
    }
    
    log.Info($"Args: {psi.Arguments}");

    var process = Process.Start(psi);

    while (!process.HasExited)
    {
        var line = process.StandardError.ReadLine();
        log.Info(line);
    }

     while (!process.HasExited)
    {
        var line = process.StandardError.ReadLine();
        log.Info(line);
    }

    process.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds);
   
    return process.ExitCode;
}
