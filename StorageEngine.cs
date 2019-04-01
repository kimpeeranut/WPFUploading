using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;
using System.Diagnostics;


using MediaInfoDotNet;

using NReco.VideoConverter;

using Npgsql;
using NpgsqlTypes;

using WEMS.Extractor.ViewModels;
using WEMS.Extractor.Engines.Streams;
using WEMS.Extractor.Engines.Security;
using WEMS.Extractor.Engines.GPS;
using System.Windows;
using System.Windows.Threading;
using WEMS.Extractor.Engines.FileSystem;
using ImageMagick;
using System.Drawing;
using WEMS.Extractor.Engines.MetaData;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GlobalConfigurationManager = WEMS.Middleware.GlobalConfigurationManager.Configuration;
using WEMS.Middleware.GlobalConfigurationManager;
using WEMS.Extractor.Models;
using WEMS.Extractor.Engines.HideVolume;
using System.Threading;

namespace WEMS.Extractor.Extensions.Storage
{
    namespace ActivityTypes
    {
        enum Action
        {
            upload_audio = 1,
            upload_video = 2,
            upload_picture = 3
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class StorageEngine
    {

        //private static int OID_BUFFER_SIZE = 1024 * 1024;
        //private static int OID_BUFFER_SIZE = 1024 * 2048;
        //private static int OID_BUFFER_SIZE = 1024 * 3072;
        //private static int OID_BUFFER_SIZE = 1024 * 4096;
        //private static int OID_BUFFER_SIZE = 1024 * 5120;
        //private static int OID_BUFFER_SIZE = 1024 * 6144;
        //private static int OID_BUFFER_SIZE = 1024 * 7168;
        //private static int OID_BUFFER_SIZE = 1024 * 8192;
        //private static int OID_BUFFER_SIZE = 1024 * 9216;
        //private static int OID_BUFFER_SIZE = 1024 * 10240;
        //private static int OID_BUFFER_SIZE = 1024 * 15360;
        //private static int OID_BUFFER_SIZE = 1024 * 20480;
        private static int OID_BUFFER_SIZE = 1024 * 1024;
        private static int BUFFER_SIZE = FileSystemEngine.GetBufferSize();
        private const int IMAGE_WIDTH = 240;
        private const int IMAGE_QUALITY = 75;

        public static bool disconnected = false;

        private static IConfigurationManager configurationManager = new GlobalConfigurationManager.ConfigurationManager();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static async Task<IDeviceFileViewModel> WriteDeviceFilesAsync(IDeviceFileViewModel device)
        {            

            IDeviceFileViewModel deviceResult = device;

            //check file storage path
            var filestoragepath = FileSystemEngine.CheckFileSystemStoragePath(device);

            //delete log file
            //Trace.WriteLine(device.DeviceId);
            DeleteLog("Logs", device);

            foreach (var deviceFile in device.DeviceFileStatisticsModel.DeviceFiles)
            {
                                
                //Start modified by Prin Sooksong 08/13/2017
                //check corrupted file
                MediaFile fileStats = new MediaFile(deviceFile);

                var deviceFileExt = Path.GetExtension(deviceFile);
                if (deviceFileExt.Equals(".WAV") || deviceFileExt.Equals(".wav"))
                {
                    var audioStat = fileStats.Audio.FirstOrDefault();

                    if (audioStat != null)
                    {
                        device.DeviceFileStatisticsModel.DeviceFile = new FileInfo(deviceFile);
                        var tuple = await Task.Run(() => WriteDeviceAudioFileAsync(device, filestoragepath));

                        deviceResult.DeviceFileStatisticsModel.DeviceFileToCheckSums.Add(tuple.Item1, tuple.Item2);
                    }
                    else
                    {
                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! Corrupt File"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! Corrupt File");
                    }
                }
                if (deviceFileExt.Equals(".MOV") || deviceFileExt.Equals(".mov") || deviceFileExt.Equals(".MP4") || deviceFileExt.Equals(".mp4"))
                {
                    var videoStat = fileStats.Video.FirstOrDefault();

                    if (videoStat != null)
                    {

                        device.DeviceFileStatisticsModel.DeviceFile = new FileInfo(deviceFile);
                        var tuple = await Task.Run(() => WriteDeviceVideoFileAsync(device, filestoragepath));

                        deviceResult.DeviceFileStatisticsModel.DeviceFileToCheckSums.Add(tuple.Item1, tuple.Item2);
                    }
                    else
                    {
                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! Corrupt File"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! Corrupt File");
                    }
                }
                if (deviceFileExt.Equals(".JPG") || deviceFileExt.Equals(".jpg"))
                {
                    var pictureStat = fileStats.Image.FirstOrDefault();

                    if (pictureStat != null)
                    {
                        device.DeviceFileStatisticsModel.DeviceFile = new FileInfo(deviceFile);
                        var tuple = await Task.Run(() => WriteDevicePictureFileAsync(device, filestoragepath));

                        deviceResult.DeviceFileStatisticsModel.DeviceFileToCheckSums.Add(tuple.Item1, tuple.Item2);
                    }
                    else
                    {

                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! Corrupt File"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! Corrupt File");
                    }
                }
                //End modified by Prin Sooksong 08/13/2017


                
            }            

            deviceResult.DeviceActiveStatus = false;
           

            return deviceResult;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        internal static async Task<Tuple<string, byte[]>> WriteDeviceAudioFileAsync(IDeviceFileViewModel device, string FileStoragePath)
        {
            var config = configurationManager.GetConfiguration<ExtractorConfiguration>();


            NpgsqlConnection.MapEnumGlobally<ActivityTypes.Action>();
            using (NpgsqlConnection connection = new NpgsqlConnection("Server=" + config.DatabaseSetting.Host + ";" +
                                                                      "Port=" + config.DatabaseSetting.Port + ";" +
                                                                      "Database=" + config.DatabaseSetting.Database + ";" +
                                                                      "User Id=" + config.DatabaseSetting.UserName + ";" +
                                                                      "Password=" + config.DatabaseSetting.Password + ";")
            )
            {

                try
                {
                    await connection.OpenAsync();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                    throw e;
                }

                try
                {

                        //var fileOid = await WriteFileOid(connection, device, device.DeviceFileStatisticsModel.DeviceFile);
                        //convert .wav to .mp3 file
                        Tuple<FileInfo, DirectoryInfo> transcoded = StreamEngine.TranscodeDeviceAudioFile(device.DeviceFileStatisticsModel.DeviceFile);
                        device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded = transcoded.Item1;
                        device.DeviceFileStatisticsModel.DeviceAudioTranscodedDirectory = transcoded.Item2;
                                           

                        //write raw file to ubuntu file system
                        var filestoreRaw = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceFile.Name;
                        await WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath);                       

                        //var transcodedAudioFileOid = await WriteFileOid(connection, device, device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded);

                        //write mp3 file to ubuntu file system
                        var filestoreMp3 = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded.Name;
                        await WriteFileMP3(device, device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded, FileStoragePath);

                        var fileChecksum = SecurityEngine.WriteFileCheckSum(device,device.DeviceFileStatisticsModel.DeviceFile);

                        MediaFile fileStats = new MediaFile(device.DeviceFileStatisticsModel.DeviceFile.FullName);
                        //MediaFile fileStats = new MediaFile(device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded.FullName);
                        var audioStat = fileStats.Audio.FirstOrDefault();

                        //var audioSize = audioStat.Size;
                        var audioSize = fileStats.General.Size;

                    var durationTime = audioStat.Duration;
                        TimeSpan t = TimeSpan.FromMilliseconds(durationTime);
                        TimeSpan fileDuration = new TimeSpan(t.Hours, t.Minutes, t.Seconds);

                        var fileCreated = device.DeviceFileStatisticsModel.DeviceFile.CreationTime;
                        var fileChanged = device.DeviceFileStatisticsModel.DeviceFile.LastWriteTime;

                        if (fileCreated.Kind == DateTimeKind.Local)
                        {
                            fileCreated = fileCreated.ToUniversalTime();
                        }
                        else if (fileCreated.Kind == DateTimeKind.Unspecified)
                        {
                            fileCreated = fileCreated.ToLocalTime().ToUniversalTime();
                        }

                        if (fileChanged.Kind == DateTimeKind.Local)
                        {
                            fileChanged = fileChanged.ToUniversalTime();
                        }
                        else if (fileChanged.Kind == DateTimeKind.Unspecified)
                        {
                            fileChanged = fileChanged.ToLocalTime().ToUniversalTime();
                        }

                        //Trace.WriteLine(fileCreated.Kind);
                        //Trace.WriteLine(fileChanged.Kind);

                        //var fileMimeType = System.Web.MimeMapping.GetMimeMapping(device.DeviceFileStatisticsModel.DeviceFile.FullName);
                        var fileMimeType = getFileMimeType(device.DeviceFileStatisticsModel.DeviceFile.FullName.ToUpper());

                        Guid deviceUserUuid = Guid.Parse(device.DeviceUserUuId.ToString());
                        Guid deviceIDUuid = Guid.Parse(device.DeviceIdUuid.ToString());
                        Guid id = Guid.Parse(Guid.NewGuid().ToString());

                        byte[] bytes = Encoding.Default.GetBytes(device.DeviceId);
                        var deviceId = Encoding.ASCII.GetString(bytes);

                        Guid activityId = Guid.Parse(Guid.NewGuid().ToString());

                        var fileTitle = getMediaTitle(device);

                        var metadatainfo = MetaDataEngine.getMetaDataInfo(device);


                    //DoEvents();

                    if (metadatainfo != null)
                    {

                        var sqlInsertGrp = "INSERT INTO " +
                                                    "audios (id, created, duration, device_id, user_id, modified, checksum, file_path, file_extension, file_size,prev_file_path,mime_type,title, classification, description) " +
                                                        " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12::mime,@p13,@p14::classification,@p15);" +
                                           "INSERT INTO " +
                                                    "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                                                        " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";

                        using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                        {
                            //Audio part
                            sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                            sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, fileCreated);
                            sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Interval, fileDuration);
                            sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Uuid, deviceIDUuid);
                            sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Timestamp, fileChanged);
                            sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Bytea, fileChecksum);
                            sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Text, filestoreRaw);
                            sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Text, "WAV");
                            sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Text, audioSize);
                            sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Text, filestoreMp3);
                            sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, fileMimeType);

                            //audio file meta data                            
                            sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Text, metadatainfo.Title);
                            sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, metadatainfo.ClassificationData);
                            sqlGrp.Parameters.AddWithValue("@p15", NpgsqlDbType.Text, metadatainfo.Note);

                            //Activity part
                            sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                            sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                            //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                            //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                            IPAddress ip = GetIP();

                            var ipStr = Utf16ToUtf8(ip.ToString());
                            sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                            sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_audio);
                            //Start modified by Prin Sooksong 08/07/2017                    
                            sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "audios");
                            //End modified by Prin Sooksong 08/07/2017

                            sqlGrp.Prepare();
                            await sqlGrp.ExecuteNonQueryAsync();

                        }
                    }
                    else
                    {

                        var sqlInsertGrp = "INSERT INTO " +
                                                    "audios (id, created, duration, device_id, user_id, modified, checksum, file_path, file_extension, file_size,prev_file_path,mime_type,title) " +
                                                        " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12::mime,@p13);" +
                                           "INSERT INTO " +
                                                    "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                                                        " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";

                        using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                        {
                            //Audio part
                            sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                            sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, fileCreated);
                            sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Interval, fileDuration);
                            sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Uuid, deviceIDUuid);
                            sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Timestamp, fileChanged);
                            sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Bytea, fileChecksum);
                            sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Text, filestoreRaw);
                            sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Text, "WAV");
                            sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Text, audioSize);
                            sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Text, filestoreMp3);
                            sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, fileMimeType);
                            sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Text, fileTitle);


                            //Activity part
                            sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                            sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                            //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                            //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                            IPAddress ip = GetIP();

                            var ipStr = Utf16ToUtf8(ip.ToString());
                            sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                            sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_audio);
                            //Start modified by Prin Sooksong 08/07/2017                    
                            sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "audios");
                            //End modified by Prin Sooksong 08/07/2017

                            sqlGrp.Prepare();
                            await sqlGrp.ExecuteNonQueryAsync();

                        }
                    }

                    //write file to ubuntu file system
                    //var filestoreRaw = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceFile.Name;
                    //await WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath);
                    //write file to ubuntu file system
                    //var filestoreMp3 = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded.Name;
                    //await WriteFile(device, device.DeviceFileStatisticsModel.DeviceAudioFileTranscoded, FileStoragePath);

                    //delete temp mp3 file
                    await StreamEngine.DeleteMP3File(transcoded.Item1);

                        disconnected = false;
                    //write log file
                    //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tUploaded successfully"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tUploaded successfully");

                        var tupleChecksum = new Tuple<string, byte[]>(device.DeviceFileStatisticsModel.DeviceFile.FullName, fileChecksum);
                        return tupleChecksum;
                    
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("duplicate key value"))
                    {
                        device.DuplicateFileErrorCount++;

                        disconnected = false;
                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! File already exists"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! File already exists");

                        //delete duplicate file on samba server
                        FileSystemEngine.DeleteFile(device, FileStoragePath);

                        //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        //{
                        //    MessageBox.Show(Application.Current.MainWindow, "Duplicate file '" + device.DeviceFileStatisticsModel.DeviceFile.Name + "' found in camera '" + device.DeviceId + "'.\r\nClick OK to continue.", "", MessageBoxButton.OK, MessageBoxImage.Warning);
                        //}));
                    }
                    else
                    {
                        device.UnknownErrorCount++;

                        disconnected = true;

                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\t" + e.Message));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\t" + e.Message);

                        //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        //{
                        //    MessageBox.Show(Application.Current.MainWindow, "Error with camera '" + device.DeviceId + "'.\r\nDescription : " + e.Message + ".\r\nClick OK to continue.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                        //}));

                    }

                    var tupleChecksum = new Tuple<string, byte[]>(device.DeviceFileStatisticsModel.DeviceFile.FullName, new byte[] { });
                    return tupleChecksum;
                }

            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns></returns>
        private static string getFileMimeType(string FileName)
        {
            string[] tempString = FileName.Split('.');
            if (tempString[tempString.Length - 1] == "MOV")
            {
                return "video/quicktime";
            }
            else if (tempString[tempString.Length - 1] == "MP4")
            {
                return "video/mp4";
            }
            else if (tempString[tempString.Length - 1] == "JPG")
            {
                return "image/jpeg";
            }
            else if (tempString[tempString.Length - 1] == "WAV")
            {
                return "audio/wav";
            }
            else if (tempString[tempString.Length - 1] == "MP3")
            {
                return "audio/mpeg";
            }
            else if (tempString[tempString.Length - 1] == "MPG")
            {
                return "video/mpeg";
            }
            else if (tempString[tempString.Length - 1] == "MPEG")
            {
                return "video/mpeg";
            }
            else if (tempString[tempString.Length - 1] == "AVI")
            {
                return "video/x-msvideo";
            }
            else if (tempString[tempString.Length - 1] == "WMV")
            {
                return "video/x-ms-wmv";
            }
            else
            {
                return "";
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        internal static async Task<Tuple<string, byte[]>> WriteDeviceVideoFileAsync(IDeviceFileViewModel device, string FileStoragePath)
        {           

            var config = configurationManager.GetConfiguration<ExtractorConfiguration>();


            NpgsqlConnection.MapEnumGlobally<ActivityTypes.Action>();
            using (NpgsqlConnection connection = new NpgsqlConnection("Server=" + config.DatabaseSetting.Host + ";" +
                                                                      "Port=" + config.DatabaseSetting.Port + ";" +
                                                                      "Database=" + config.DatabaseSetting.Database + ";" +
                                                                      "User Id=" + config.DatabaseSetting.UserName + ";" +
                                                                      "Password=" + config.DatabaseSetting.Password + ";")
            )
            {

                //DateTime st = DateTime.Now;

                try
                {
                    await connection.OpenAsync();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                    throw e;
                }

                try
                {

                        //var fileOid = await WriteFileOid(connection, device, device.DeviceFileStatisticsModel.DeviceFile);
                    
                        //write file to ubuntu file system
                        var filestore = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceFile.Name;
                        await WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath);

                        //Dispatcher.CurrentDispatcher.Invoke( new Action(()=>{ WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath); }));

                        //create checksum
                        var fileChecksum = SecurityEngine.WriteFileCheckSum(device, device.DeviceFileStatisticsModel.DeviceFile);

                        //DateTime en = DateTime.Now;

                        //DateTime st0 = DateTime.Now;

                        MediaFile fileStats = new MediaFile(device.DeviceFileStatisticsModel.DeviceFile.FullName);
                        var videoFile = fileStats.Video.FirstOrDefault();                        

                        var videoSize = fileStats.General.Size;

                        var fileHeight = videoFile.Height;
                        var fileWidth = videoFile.Width;

                        var fileCreated = device.DeviceFileStatisticsModel.DeviceFile.CreationTime;
                        var fileChanged = device.DeviceFileStatisticsModel.DeviceFile.LastWriteTime;
                        var fileChangedOriginal = device.DeviceFileStatisticsModel.DeviceFile.LastWriteTime;

                        if (fileCreated.Kind == DateTimeKind.Local)
                        {
                            fileCreated = fileCreated.ToUniversalTime();
                        }
                        else if (fileCreated.Kind == DateTimeKind.Unspecified)
                        {
                            fileCreated = fileCreated.ToLocalTime().ToUniversalTime();
                        }

                        if (fileChanged.Kind == DateTimeKind.Local)
                        {
                            fileChanged = fileChanged.ToUniversalTime();
                        }
                        else if (fileChanged.Kind == DateTimeKind.Unspecified)
                        {
                            fileChanged = fileChanged.ToLocalTime().ToUniversalTime();
                        }

                        //Trace.WriteLine(fileCreated.Kind);
                        //Trace.WriteLine(fileChanged.Kind);

                        var durationTime = videoFile.Duration;

                        TimeSpan t = TimeSpan.FromMilliseconds(durationTime);
                        TimeSpan fileDuration = new TimeSpan(t.Hours, t.Minutes, t.Seconds);

                        var thumbnailStream = new MemoryStream();
                        new FFMpegConverter().GetVideoThumbnail(device.DeviceFileStatisticsModel.DeviceFile.FullName, thumbnailStream, 0);
                        var fileThumbnail = thumbnailStream.ToArray();
                        thumbnailStream.Dispose();

                        //resize thumbnail video image fow width=240
                        string tmpImagePath = Path.GetDirectoryName(device.DeviceFileStatisticsModel.DeviceFile.FullName) + @"\" + Path.GetFileNameWithoutExtension(device.DeviceFileStatisticsModel.DeviceFile.FullName) + @".JPG";
                        //Trace.WriteLine(tmpImagePath);

                        HideVolumeEngine.WriteMediaFile(device.DeviceDrive, device.DeviceFileStatisticsModel.DeviceFile.Name, fileThumbnail);

                        //using (FileStream fs = new FileStream(tmpImagePath, FileMode.Create, FileAccess.Write))
                        //{
                        //    fs.Flush();
                        //    fs.Write(fileThumbnail, 0, fileThumbnail.Length);
                        //    fs.Close();
                        //    fileThumbnail = null;
                        //}

                        fileThumbnail = Resize(tmpImagePath);  

                        //delete temporary thumbnail file
                        File.Delete(tmpImagePath);

                        //var fileMimeType = System.Web.MimeMapping.GetMimeMapping(device.DeviceFileStatisticsModel.DeviceFile.FullName);
                        var fileMimeType = getFileMimeType(device.DeviceFileStatisticsModel.DeviceFile.FullName.ToUpper());

                        Guid deviceUserUuid = Guid.Parse(device.DeviceUserUuId.ToString());
                        Guid deviceIDUuid = Guid.Parse(device.DeviceIdUuid.ToString());
                        Guid id = Guid.Parse(Guid.NewGuid().ToString());

                        byte[] bytes = Encoding.Default.GetBytes(device.DeviceId);
                        var deviceId = Encoding.ASCII.GetString(bytes);

                        Guid activityId = Guid.Parse(Guid.NewGuid().ToString());

                        var fileTitle = getMediaTitle(device);

                        //get file meta data
                        var metadatainfo = MetaDataEngine.getMetaDataInfo(device);

                    //DateTime en0 = DateTime.Now;

                    //DateTime st1 = DateTime.Now;

                    //Start modified by Prin Sooksong 08/16/2017
                    //GPS log data for Third Eye Camera

                    //DoEvents();


                    if (device.DeviceFileStatisticsModel.GPSRawData != null)
                    {
                        DateTime originalFileCreated = fileChangedOriginal.AddMinutes(-fileDuration.Minutes).AddSeconds(-fileDuration.Seconds);

                        string[] path = GPSEngine.ParseGPSData(device.DeviceFileStatisticsModel.GPSRawData, originalFileCreated, fileChangedOriginal);

                        if (metadatainfo != null)
                        {

                            var sqlInsertGrp = "INSERT INTO " +
                                                    "videos (id, created, duration, width, height, device_id, user_id, thumbnail, modified, checksum, path, file_path, file_extension, file_size, mime_type, title, classification, description)" +
                                                        " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15::mime, @p16, @p17::classification, @p18); " +

                                           "INSERT INTO " +
                                                    "activity (id, user_id, entity_id, user_ip, action,  entity_type) " +
                                                        " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";

                            //                   "INSERT INTO " +
                            //                         "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                            //                             " VALUES (@a1, @a2, @a3, @a4, @a5, @a6)";



                            using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                            {
                                //Video part0+ 
                                sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                                sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, fileCreated);
                                sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Interval, fileDuration);
                                sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Integer, fileWidth);
                                sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Integer, fileHeight);
                                sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Uuid, deviceIDUuid);
                                sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Bytea, fileThumbnail);
                                sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Timestamp, fileChanged);
                                sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Bytea, fileChecksum);
                                sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Unknown, "{" + String.Join(",", path) + "}");
                                sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, filestore);
                                sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Text, device.DeviceFileStatisticsModel.DeviceFile.Extension.Substring(1, 3));
                                sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, videoSize);
                                sqlGrp.Parameters.AddWithValue("@p15", NpgsqlDbType.Text, fileMimeType);

                                //video file meta data                                
                                sqlGrp.Parameters.AddWithValue("@p16", NpgsqlDbType.Text, metadatainfo.Title);
                                sqlGrp.Parameters.AddWithValue("@p17", NpgsqlDbType.Text, metadatainfo.ClassificationData);
                                sqlGrp.Parameters.AddWithValue("@p18", NpgsqlDbType.Text, metadatainfo.Note);

                                //Activity part
                                sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                                sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                                //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                                //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                                IPAddress ip = GetIP();

                                var ipStr = Utf16ToUtf8(ip.ToString());
                                sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                                sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_video);
                                //Start modified by Prin Sooksong 08/07/2017  
                                sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "videos");
                                //End modified by Prin Sooksong 08/07/2017  

                                sqlGrp.Prepare();
                                await sqlGrp.ExecuteNonQueryAsync();
                                //Dispatcher.CurrentDispatcher.Invoke(new Action(() => { sqlGrp.ExecuteNonQueryAsync(); }));

                            }
                        }
                        else
                        {
                            var sqlInsertGrp = "INSERT INTO " +
                                                        "videos (id, created, duration, width, height, device_id, user_id, thumbnail, modified, checksum, path, file_path, file_extension, file_size, mime_type, title)" +
                                                            " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15::mime, @p16); " +

                                               "INSERT INTO " +
                                                        "activity (id, user_id, entity_id, user_ip, action,  entity_type) " +
                                                            " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";

                            //                   "INSERT INTO " +
                            //                         "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                            //                             " VALUES (@a1, @a2, @a3, @a4, @a5, @a6)";



                            using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                            {
                                //Video part0+ 
                                sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                                sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, fileCreated);
                                sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Interval, fileDuration);
                                sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Integer, fileWidth);
                                sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Integer, fileHeight);
                                sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Uuid, deviceIDUuid);
                                sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Bytea, fileThumbnail);
                                sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Timestamp, fileChanged);
                                sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Bytea, fileChecksum);
                                sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Unknown, "{" + String.Join(",", path) + "}");
                                sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, filestore);
                                sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Text, device.DeviceFileStatisticsModel.DeviceFile.Extension.Substring(1, 3));
                                sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, videoSize);
                                sqlGrp.Parameters.AddWithValue("@p15", NpgsqlDbType.Text, fileMimeType);
                                sqlGrp.Parameters.AddWithValue("@p16", NpgsqlDbType.Text, fileTitle);

                                //Activity part
                                sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                                sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                                //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                                //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                                IPAddress ip = GetIP();

                                var ipStr = Utf16ToUtf8(ip.ToString());
                                sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                                sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_video);
                                //Start modified by Prin Sooksong 08/07/2017  
                                sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "videos");
                                //End modified by Prin Sooksong 08/07/2017  

                                sqlGrp.Prepare();
                                await sqlGrp.ExecuteNonQueryAsync();
                                //Dispatcher.CurrentDispatcher.Invoke(new Action(() => { sqlGrp.ExecuteNonQueryAsync(); }));

                            }
                        }
                    }
                    else
                    {
                        if (metadatainfo != null)
                        {

                            var sqlInsertGrp = "INSERT INTO " +
                                                        "videos (id, created, duration, width, height, device_id, user_id, thumbnail, modified, checksum, file_path, file_extension, file_size, mime_type, title, classification, description)" +
                                                            " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14::mime, @p15, @p16::classification, @p17); " +

                                               "INSERT INTO " +
                                                        "activity (id, user_id, entity_id, user_ip, action,  entity_type) " +
                                                            " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";

                            //                   "INSERT INTO " +
                            //                         "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                            //                             " VALUES (@a1, @a2, @a3, @a4, @a5, @a6)";

                            using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                            {
                                //Video part
                                sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                                sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, fileCreated);
                                sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Interval, fileDuration);
                                sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Integer, fileWidth);
                                sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Integer, fileHeight);
                                sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Uuid, deviceIDUuid);
                                sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Bytea, fileThumbnail);
                                sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Timestamp, fileChanged);
                                sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Bytea, fileChecksum);
                                sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Text, filestore);
                                sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, device.DeviceFileStatisticsModel.DeviceFile.Extension.Substring(1, 3));
                                sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Text, videoSize);
                                sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, fileMimeType);

                                //video file meta data                                
                                sqlGrp.Parameters.AddWithValue("@p15", NpgsqlDbType.Text, metadatainfo.Title);
                                sqlGrp.Parameters.AddWithValue("@p16", NpgsqlDbType.Text, metadatainfo.ClassificationData);
                                sqlGrp.Parameters.AddWithValue("@p17", NpgsqlDbType.Text, metadatainfo.Note);

                                //Activity part
                                sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                                sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                                //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                                //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                                IPAddress ip = GetIP();

                                var ipStr = Utf16ToUtf8(ip.ToString());
                                sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                                sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_video);
                                //Start modified by Prin Sooksong 08/07/2017  
                                sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "videos");
                                //End modified by Prin Sooksong 08/07/2017  

                                sqlGrp.Prepare();
                                await sqlGrp.ExecuteNonQueryAsync();
                                //Dispatcher.CurrentDispatcher.Invoke(new Action(() => { sqlGrp.ExecuteNonQueryAsync(); }));

                            }
                        }
                        else
                        {
                            var sqlInsertGrp = "INSERT INTO " +
                                                        "videos (id, created, duration, width, height, device_id, user_id, thumbnail, modified, checksum, file_path, file_extension, file_size, mime_type, title)" +
                                                            " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14::mime, @p15); " +

                                               "INSERT INTO " +
                                                        "activity (id, user_id, entity_id, user_ip, action,  entity_type) " +
                                                            " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";

                            //                   "INSERT INTO " +
                            //                         "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                            //                             " VALUES (@a1, @a2, @a3, @a4, @a5, @a6)";

                            using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                            {
                                //Video part
                                sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                                sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, fileCreated);
                                sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Interval, fileDuration);
                                sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Integer, fileWidth);
                                sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Integer, fileHeight);
                                sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Uuid, deviceIDUuid);
                                sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Bytea, fileThumbnail);
                                sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Timestamp, fileChanged);
                                sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Bytea, fileChecksum);
                                sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Text, filestore);
                                sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, device.DeviceFileStatisticsModel.DeviceFile.Extension.Substring(1, 3));
                                sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Text, videoSize);
                                sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, fileMimeType);
                                sqlGrp.Parameters.AddWithValue("@p15", NpgsqlDbType.Text, fileTitle);

                                //Activity part
                                sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                                sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                                sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                                //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                                //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                                IPAddress ip = GetIP();

                                var ipStr = Utf16ToUtf8(ip.ToString());
                                sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                                sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_video);
                                //Start modified by Prin Sooksong 08/07/2017  
                                sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "videos");
                                //End modified by Prin Sooksong 08/07/2017  

                                sqlGrp.Prepare();
                                await sqlGrp.ExecuteNonQueryAsync();
                                //Dispatcher.CurrentDispatcher.Invoke(new Action(() => { sqlGrp.ExecuteNonQueryAsync(); }));

                            }
                        }
                    }

                    //End modified by Prin Sooksong 08/16/2017    

                    //DateTime en1 = DateTime.Now;
                    //write file to ubuntu file system
                    //var filestore = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceFile.Name;

                    //DateTime st2 = DateTime.Now;
                    //await WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath);
                    //DateTime en2 = DateTime.Now;

                    disconnected = false;

                    //write log file
                    //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tUploaded successfully"));
                    WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tUploaded successfully");

                        var tupleChecksum = new Tuple<string, byte[]>(device.DeviceFileStatisticsModel.DeviceFile.FullName, fileChecksum);

                        return tupleChecksum;
                    
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("duplicate key value"))
                    {
                        device.DuplicateFileErrorCount++;

                        disconnected = false;

                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! File already exists"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! File already exists");

                        //delete duplicate file on samba server
                        FileSystemEngine.DeleteFile(device, FileStoragePath);

                        //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        //{
                        //    MessageBox.Show(Application.Current.MainWindow, "Duplicate file '" + device.DeviceFileStatisticsModel.DeviceFile.Name + "' found in camera '" + device.DeviceId + "'.\r\nClick OK to continue.", "", MessageBoxButton.OK, MessageBoxImage.Warning);
                        //}));
                    }
                    else
                    {
                        device.UnknownErrorCount++;

                        disconnected = true;

                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\t" + e.Message));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\t" + e.Message);

                       

                        //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        //{
                        //    MessageBox.Show(Application.Current.MainWindow, "Error with camera '" + device.DeviceId + "'.\r\nDescription : " + e.Message + ".\r\nClick OK to continue.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                        //}));

                    }

                    var tupleChecksum = new Tuple<string, byte[]>(device.DeviceFileStatisticsModel.DeviceFile.FullName, new byte[] { });
                    return tupleChecksum;
                }

                //End modified by Prin Sooksong 08/16/2017    

            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        //internal static IPAddress GetIP()
        //{
        //    string Str = "";
        //    Str = System.Net.Dns.GetHostName();
        //    IPHostEntry ipEntry = System.Net.Dns.GetHostEntry(Str);
        //    IPAddress[] addr = ipEntry.AddressList;
        //    return addr[addr.Length - 1];

        //}
        internal static IPAddress GetIP()
        {
            try
            {
                IPAddress returnIP = null;

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up
                        && (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                    {
                        UnicastIPAddressInformation ipInfo = nic.GetIPProperties().UnicastAddresses?.FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

                        var gatewayAddress = nic.GetIPProperties().GatewayAddresses.FirstOrDefault();

                        if (gatewayAddress != null)
                        {
                            returnIP = ipInfo.Address;
                        }
                    }
                }

                return returnIP;

            }
            catch (NetworkInformationException networkInformationException)
            {
                throw networkInformationException;
            }
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        internal static async Task<Tuple<string, byte[]>> WriteDevicePictureFileAsync(IDeviceFileViewModel device, string FileStoragePath)
        {
            var config = configurationManager.GetConfiguration<ExtractorConfiguration>();


            NpgsqlConnection.MapEnumGlobally<ActivityTypes.Action>();
            using (NpgsqlConnection connection = new NpgsqlConnection("Server=" + config.DatabaseSetting.Host + ";" +
                                                                      "Port=" + config.DatabaseSetting.Port + ";" +
                                                                      "Database=" + config.DatabaseSetting.Database + ";" +
                                                                      "User Id=" + config.DatabaseSetting.UserName + ";" +
                                                                      "Password=" + config.DatabaseSetting.Password + ";")
            )
            {
                //DateTime st = DateTime.Now;

                try
                {
                    await connection.OpenAsync();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                    throw e;
                }

                try
                {
                    
                        //var fileOid = File.ReadAllBytes(device.DeviceFileStatisticsModel.DeviceFile.FullName);

                        //write file to ubuntu file system
                        var filestore = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceFile.Name;
                        await WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath);

                        var fileChecksum = SecurityEngine.WriteFileCheckSum(device, device.DeviceFileStatisticsModel.DeviceFile);

                        var fileThumbnail = Resize(device);

                        MediaFile fileStats = new MediaFile(device.DeviceFileStatisticsModel.DeviceFile.FullName);
                        var picture = fileStats.Image.FirstOrDefault();

                        //var pictureSize = picture.Size;
                        var pictureSize = fileStats.General.Size;

                    var pictureHeight = picture.Height;
                        var pictureWidth = picture.Width;

                        var pictureFileCreated = device.DeviceFileStatisticsModel.DeviceFile.CreationTime;
                        var pictureFileChanged = device.DeviceFileStatisticsModel.DeviceFile.LastWriteTime;

                        if (pictureFileCreated.Kind == DateTimeKind.Local)
                        {
                            pictureFileCreated = pictureFileCreated.ToUniversalTime();
                        }
                        else if (pictureFileCreated.Kind == DateTimeKind.Unspecified)
                        {
                            pictureFileCreated = pictureFileCreated.ToLocalTime().ToUniversalTime();
                        }

                        if (pictureFileChanged.Kind == DateTimeKind.Local)
                        {
                            pictureFileChanged = pictureFileChanged.ToUniversalTime();
                        }
                        else if (pictureFileChanged.Kind == DateTimeKind.Unspecified)
                        {
                            pictureFileChanged = pictureFileChanged.ToLocalTime().ToUniversalTime();
                        }

                        //Trace.WriteLine(pictureFileCreated.Kind);
                        //Trace.WriteLine(pictureFileChanged.Kind);

                        //var fileMimeType = System.Web.MimeMapping.GetMimeMapping(device.DeviceFileStatisticsModel.DeviceFile.FullName);
                        var fileMimeType = getFileMimeType(device.DeviceFileStatisticsModel.DeviceFile.FullName.ToUpper());

                        Guid deviceUserUuid = Guid.Parse(device.DeviceUserUuId.ToString());
                        Guid deviceIDUuid = Guid.Parse(device.DeviceIdUuid.ToString());
                        Guid id = Guid.Parse(Guid.NewGuid().ToString());

                        byte[] bytes = Encoding.Default.GetBytes(device.DeviceId);
                        var deviceId = Encoding.ASCII.GetString(bytes);



                        Guid activityId = Guid.Parse(Guid.NewGuid().ToString());

                        var fileTitle = getMediaTitle(device);

                        //get file meta data
                        var metadatainfo = MetaDataEngine.getMetaDataInfo(device);


                    //DoEvents();

                    if (metadatainfo != null)
                    {

                        var sqlInsertGrp = "INSERT INTO " +
                                                    "pictures (id, created, width, height, device_id, user_id, modified, checksum, file_path, file_extension, file_size, mime_type, thumbnail, title, classification, description)" +
                                                        " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12::mime, @p13, @p14, @p15::classification, @p16); " +
                                           "INSERT INTO " +
                                                    "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                                                        " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";


                        using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                        {
                            //Pictures part
                            sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                            sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, pictureFileCreated);
                            sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Integer, pictureWidth);
                            sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Integer, pictureHeight);
                            sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Uuid, deviceIDUuid);
                            sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Timestamp, pictureFileChanged);
                            sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Bytea, fileChecksum);
                            sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Text, filestore);
                            sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Text, "JPG");
                            sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Text, pictureSize);
                            sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, fileMimeType);
                            sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Bytea, fileThumbnail);

                            //picture file meta data                            
                            sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, metadatainfo.Title);
                            sqlGrp.Parameters.AddWithValue("@p15", NpgsqlDbType.Text, metadatainfo.ClassificationData);
                            sqlGrp.Parameters.AddWithValue("@p16", NpgsqlDbType.Text, metadatainfo.Note);


                            //Activity part
                            sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                            sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                            //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                            //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                            IPAddress ip = GetIP();

                            var ipStr = Utf16ToUtf8(ip.ToString());
                            sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                            sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_picture);
                            //Start modified by Prin Sooksong 08/07/2017  
                            sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "pictures");
                            //End modified by Prin Sooksong 08/07/2017  


                            sqlGrp.Prepare();
                            await sqlGrp.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        var sqlInsertGrp = "INSERT INTO " +
                                                    "pictures (id, created, width, height, device_id, user_id, modified, checksum, file_path, file_extension, file_size, mime_type, thumbnail, title)" +
                                                        " VALUES (@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12::mime, @p13, @p14); " +
                                           "INSERT INTO " +
                                                    "activity (id, user_id, entity_id, user_ip, action, entity_type) " +
                                                        " VALUES (@a1, @a2, @a3, @a4, @a5, @a6);";


                        using (var sqlGrp = new NpgsqlCommand(sqlInsertGrp, connection))
                        {
                            //Pictures part
                            sqlGrp.Parameters.AddWithValue("@p1", NpgsqlDbType.Uuid, id);
                            sqlGrp.Parameters.AddWithValue("@p2", NpgsqlDbType.Timestamp, pictureFileCreated);
                            sqlGrp.Parameters.AddWithValue("@p3", NpgsqlDbType.Integer, pictureWidth);
                            sqlGrp.Parameters.AddWithValue("@p4", NpgsqlDbType.Integer, pictureHeight);
                            sqlGrp.Parameters.AddWithValue("@p5", NpgsqlDbType.Uuid, deviceIDUuid);
                            sqlGrp.Parameters.AddWithValue("@p6", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@p7", NpgsqlDbType.Timestamp, pictureFileChanged);
                            sqlGrp.Parameters.AddWithValue("@p8", NpgsqlDbType.Bytea, fileChecksum);
                            sqlGrp.Parameters.AddWithValue("@p9", NpgsqlDbType.Text, filestore);
                            sqlGrp.Parameters.AddWithValue("@p10", NpgsqlDbType.Text, "JPG");
                            sqlGrp.Parameters.AddWithValue("@p11", NpgsqlDbType.Text, pictureSize);
                            sqlGrp.Parameters.AddWithValue("@p12", NpgsqlDbType.Text, fileMimeType);
                            sqlGrp.Parameters.AddWithValue("@p13", NpgsqlDbType.Bytea, fileThumbnail);
                            sqlGrp.Parameters.AddWithValue("@p14", NpgsqlDbType.Text, fileTitle);



                            //Activity part
                            sqlGrp.Parameters.AddWithValue("@a1", NpgsqlDbType.Uuid, activityId);
                            sqlGrp.Parameters.AddWithValue("@a2", NpgsqlDbType.Uuid, deviceUserUuid);
                            sqlGrp.Parameters.AddWithValue("@a3", NpgsqlDbType.Uuid, id);

                            //IPAddress ip = Dns.GetHostEntry(Dns.GetHostName())
                            //                                  .AddressList.(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                            IPAddress ip = GetIP();

                            var ipStr = Utf16ToUtf8(ip.ToString());
                            sqlGrp.Parameters.AddWithValue("@a4", NpgsqlDbType.Text, ipStr);
                            sqlGrp.Parameters.AddWithValue("@a5", NpgsqlDbType.Enum, ActivityTypes.Action.upload_picture);
                            //Start modified by Prin Sooksong 08/07/2017  
                            sqlGrp.Parameters.AddWithValue("@a6", NpgsqlDbType.Text, "pictures");
                            //End modified by Prin Sooksong 08/07/2017  


                            sqlGrp.Prepare();
                            await sqlGrp.ExecuteNonQueryAsync();
                        }
                    }

                    //write file to ubuntu file system
                    //var filestore = FileStoragePath + @"\" + device.DeviceFileStatisticsModel.DeviceFile.Name;
                    //await WriteFile(device, device.DeviceFileStatisticsModel.DeviceFile, FileStoragePath);

                    disconnected = false;
                    //DateTime en = DateTime.Now;

                    //write log file
                    //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tUploaded successfully"));
                    WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tUploaded successfully");

                        var tupleChecksum = new Tuple<string, byte[]>(device.DeviceFileStatisticsModel.DeviceFile.FullName, fileChecksum);
                        return tupleChecksum;
                    
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("duplicate key value"))
                    {
                        device.DuplicateFileErrorCount++;

                        disconnected = false;
                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! File already exists"));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\tSkipped! File already exists");

                        //delete duplicate file on samba server
                        FileSystemEngine.DeleteFile(device, FileStoragePath);

                        //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        //{
                        //    MessageBox.Show(Application.Current.MainWindow, "Duplicate file '" + device.DeviceFileStatisticsModel.DeviceFile.Name + "' found in camera '" + device.DeviceId + "'.\r\nClick OK to continue.", "", MessageBoxButton.OK, MessageBoxImage.Warning);
                        //}));
                    }
                    else
                    {
                        device.UnknownErrorCount++;

                        disconnected = true;

                        //write log file
                        //var taskWriteLog = Task.Run(() => WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\t" + e.Message));
                        WriteLog("Logs", device, device.DeviceFileStatisticsModel.DeviceFile.Name + "\t" + e.Message);

                        //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
                        //{
                        //    MessageBox.Show(Application.Current.MainWindow, "Error with camera '" + device.DeviceId + "'.\r\nDescription : " + e.Message + ".\r\nClick OK to continue.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                        //}));

                    }

                    var tupleChecksum = new Tuple<string, byte[]>(device.DeviceFileStatisticsModel.DeviceFile.FullName, new byte[] { });
                    return tupleChecksum;
                }

            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        internal static byte[] Resize(IDeviceFileViewModel device)
        {
            using (var image = new MagickImage(device.DeviceFileStatisticsModel.DeviceFile) )
            {

                image.Resize(IMAGE_WIDTH, 0);

                image.Strip();

                image.Quality = IMAGE_QUALITY;

                return image.ToByteArray();
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns></returns>
        internal static byte[] Resize(string FileName)
        {
            using (var image = new MagickImage(new FileInfo(FileName)))
            {

                image.Resize(IMAGE_WIDTH, 0);

                image.Strip();

                image.Quality = IMAGE_QUALITY;

                return image.ToByteArray();
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="device"></param>
        /// <param name="deviceFile"></param>
        /// <returns></returns>
        private static async Task<uint> WriteFileOid(NpgsqlConnection connection, IDeviceFileViewModel device, FileInfo deviceFile)
        {
            uint oid = 0;



            using (var fileStream = new FileStream(deviceFile.FullName, FileMode.Open))
            {

                using (var fileTransaction = connection.BeginTransaction())
                {

                    var manager = new NpgsqlLargeObjectManager(connection);
                    oid = manager.Create();



                    using (var storeStream = manager.OpenReadWrite(oid))
                    {
                        //try
                        //{

                        //Start inserted by Prin Sooksong 08/02/2017
                        long oldStreamPosition = 0;
                        double progressBarVal = 0;
                        //End inserted by Prin Sooksong 08/02/2017

                        long fileLength = deviceFile.Length;
                        var streamReader = new StreamReader(fileStream);

                        while (streamReader.BaseStream.Position < fileLength)
                        {
                            if ((fileLength - streamReader.BaseStream.Position) <= OID_BUFFER_SIZE)
                            {
                                byte[] buffer = new byte[(fileLength - streamReader.BaseStream.Position)];

                                fileStream.Read(buffer, 0, buffer.Length);

                                storeStream.Write(buffer, 0, buffer.Length);
                            }
                            else
                            {
                                byte[] buffer = new byte[OID_BUFFER_SIZE];

                                fileStream.Read(buffer, 0, buffer.Length);
                                storeStream.Write(buffer, 0, buffer.Length);
                            }

                            //Start modified by Prin Sooksong 08/02/2017
                            //var pos = streamReader.BaseStream.Position;
                            //device.DeviceFilesTransitStatus += device.DeviceFileStatisticsModel.DeviceTotalFileSize / pos;
                            device.DeviceFileStatisticsModel.DeviceTotalTransferFileSize += streamReader.BaseStream.Position - oldStreamPosition;
                            progressBarVal = (double)((device.DeviceFileStatisticsModel.DeviceTotalTransferFileSize / device.DeviceFileStatisticsModel.DeviceTotalFileSize) * 99);
                            device.DeviceFilesTransitStatus = progressBarVal--;
                            oldStreamPosition = streamReader.BaseStream.Position;
                            //End modified by Prin Sooksong 08/02/2017
                        }

                        //}
                        //catch (Exception e)
                        //{
                        //    Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => {
                        //        MessageBox.Show(Application.Current.MainWindow, "Error with Camera Volume Name '" + device.DeviceId + "'\r\nDescription : " + e.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
                        //    }));
                        //    //MessageBox.Show(device.DeviceId + ":" + e.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
                        //}
                    }


                    fileTransaction.Commit();

                }

            }

            return oid;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <param name="deviceFile"></param>
        /// <param name="FileStoragePath"></param>
        /// <returns></returns>
        private static async Task<string> WriteFile(IDeviceFileViewModel device, FileInfo deviceFile, string FileStoragePath)
        {            

            try { 

            var config = configurationManager.GetConfiguration<ExtractorConfiguration>();
            var filestorageserverpath =
                Path.Combine(@"\\" + config.FileSystemSetting.IpAddress + config.FileSystemSetting.Path);

            string target_folder = "";
            if (filestorageserverpath.Substring(filestorageserverpath.Length - 1, 1) == @"\" || filestorageserverpath.Substring(filestorageserverpath.Length - 1, 1) == @"/")
            {
                target_folder = filestorageserverpath.Substring(0, filestorageserverpath.Length - 1) + FileStoragePath + @"\" + deviceFile.Name;
            }
            else
            {
                target_folder = filestorageserverpath + FileStoragePath + @"\" + deviceFile.Name;
            }

            using (var fileStream = HideVolumeEngine.ReadMediaFile(device.DeviceDrive, deviceFile.Name))
            {
                using (var outputStream = File.OpenWrite(target_folder))
                {
                    //try
                    //{
                    long oldStreamPosition = 0;
                    double progressBarVal = 0;

                    long fileLength = deviceFile.Length;

                    var streamReader = new StreamReader(fileStream);
                    streamReader.BaseStream.Position = 0;

                    while (streamReader.BaseStream.Position < fileLength)
                    {
                            //DoEvents();

                        if ((fileLength - streamReader.BaseStream.Position) <= BUFFER_SIZE)
                        {
                            byte[] data = new byte[fileLength - streamReader.BaseStream.Position];
                            streamReader.BaseStream.Read(data, 0, data.Length);
                            outputStream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            byte[] data = new byte[BUFFER_SIZE];
                            streamReader.BaseStream.Read(data, 0, BUFFER_SIZE);
                            outputStream.Write(data, 0, data.Length);
                        }

                            //DoEvents();

                        device.DeviceFileStatisticsModel.DeviceTotalTransferFileSize += streamReader.BaseStream.Position - oldStreamPosition;
                        progressBarVal = (double)((device.DeviceFileStatisticsModel.DeviceTotalTransferFileSize / device.DeviceFileStatisticsModel.DeviceTotalFileSize) * 99);
                        device.DeviceFilesTransitStatus = progressBarVal--;
                        oldStreamPosition = streamReader.BaseStream.Position;

                            //DoEvents();

                    }

                    outputStream.Flush();
                    //}
                    //catch (Exception ex)
                    //{
                    //    MessageBox.Show(ex.Message);
                    //}
                }

            }

            return FileStoragePath + @"\" + deviceFile.Name;

            }
            catch (AggregateException ex)
            {
                
                foreach (var exception in ex.InnerExceptions)
                    Trace.WriteLine(ex.InnerException.Message);
                return "";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <param name="deviceFile"></param>
        /// <param name="FileStoragePath"></param>
        /// <returns></returns>
        private static async Task<string> WriteFileMP3(IDeviceFileViewModel device, FileInfo deviceFile, string FileStoragePath)
        {
            
            try { 

            var config = configurationManager.GetConfiguration<ExtractorConfiguration>();
            var filestorageserverpath =
                Path.Combine(@"\\" + config.FileSystemSetting.IpAddress + config.FileSystemSetting.Path);

            string target_folder = "";
            if (filestorageserverpath.Substring(filestorageserverpath.Length - 1, 1) == @"\" || filestorageserverpath.Substring(filestorageserverpath.Length - 1, 1) == @"/")
            {
                target_folder = filestorageserverpath.Substring(0, filestorageserverpath.Length - 1) + FileStoragePath + @"\" + deviceFile.Name;
            }
            else
            {
                target_folder = filestorageserverpath + FileStoragePath + @"\" + deviceFile.Name;
            }

            using (var fileStream = new FileStream(deviceFile.FullName, FileMode.Open))
            {
                using (var outputStream = File.OpenWrite(target_folder))
                {
                    //try
                    //{
                    long oldStreamPosition = 0;
                    double progressBarVal = 0;

                    long fileLength = deviceFile.Length;

                    fileStream.Position = 0;

                    while (fileStream.Position < fileLength)
                    {
                            //DoEvents();

                        if ((fileLength - fileStream.Position) <= BUFFER_SIZE)
                        {
                            byte[] data = new byte[fileLength - fileStream.Position];
                            fileStream.Read(data, 0, data.Length);
                            outputStream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            byte[] data = new byte[BUFFER_SIZE];
                            fileStream.Read(data, 0, BUFFER_SIZE);
                            outputStream.Write(data, 0, data.Length);
                        }

                            //DoEvents();

                        device.DeviceFileStatisticsModel.DeviceTotalTransferFileSize += fileStream.Position - oldStreamPosition;
                        progressBarVal = (double)((device.DeviceFileStatisticsModel.DeviceTotalTransferFileSize / device.DeviceFileStatisticsModel.DeviceTotalFileSize) * 99);
                        device.DeviceFilesTransitStatus = progressBarVal--;
                        oldStreamPosition = fileStream.Position;

                            //DoEvents();
                            

                    }
                    outputStream.Flush();
                    //}
                    //catch (Exception ex)
                    //{
                    //    MessageBox.Show(ex.Message);
                    //}
                }

            }

            return FileStoragePath + @"\" + deviceFile.Name;
            }
            catch (AggregateException ex)
            {
                
                foreach (var exception in ex.InnerExceptions)
                    Trace.WriteLine(ex.InnerException.Message);
                return "";
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="utf16String"></param>
        /// <returns></returns>
        public static string Utf16ToUtf8(string utf16String)
        {
            // Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(utf16String);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

            // Return UTF8 bytes as ANSI string
            return Encoding.Default.GetString(utf8Bytes);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="LogFolderName"></param>
        /// <param name="device"></param>
        /// <param name="LogMessage"></param>
        /// <returns></returns>
        public static bool WriteLog(string LogFolderName, IDeviceFileViewModel device, string LogMessage)
        {
            //if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\" + LogFolderName) == false)
            //    Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\" + LogFolderName);

            try
            {

                //if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\" + LogFolderName) == false)
                //    Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\" + LogFolderName);

                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName) == false)
                    Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName);

                if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log") == false)
                {
                    StreamWriter sr = File.CreateText(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log");
                    sr.Close();
                }

                using (StreamWriter writer = File.AppendText(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log"))
                {
                    writer.WriteLine(DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt") + "\t" + LogMessage);
                    writer.Close();
                    writer.Dispose();
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="LogFolderName"></param>
        /// <param name="device"></param>
        /// <param name="LogFileName"></param>
        /// <returns></returns>
        public static List<string[]> ReadLog(string LogFolderName, IDeviceFileViewModel device, string LogFileName)
        {
            List<string[]> deviceLog = new List<string[]>();

            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log"))
            {
                using (StreamReader reader = new StreamReader(Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData) + @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log"))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] _templine = line.Split(new char[] { '\t' });
                        deviceLog.Add(_templine);
                    }
                }
            }

            return deviceLog;

            //return File.ReadAllLines(System.AppDomain.CurrentDomain.BaseDirectory + @"\" + LogFolderName + @"\" + LogFileName)
            //            .SelectMany(l => l.Split(new char[] { '\t' })).ToArray();

            //using (StreamReader reader = new StreamReader(System.AppDomain.CurrentDomain.BaseDirectory + @"\" + LogFolderName + @"\" + LogFileName))
            //{

            //    return reader.ReadToEnd();
            //}
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static string getMediaTitle(IDeviceFileViewModel device)
        {
            return "BodyCam-" + device.DeviceId + "-" + device.DeviceFileStatisticsModel.DeviceFile.LastWriteTime.ToString("yyyyMMdd-HHmmss");
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="LogFolderName"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static bool DeleteLog(string LogFolderName, IDeviceFileViewModel device)
        {
            try
            {
                //Trace.WriteLine(DateTime.Now + "," + LogFolderName + "," + LogFileName);

                if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) +
                                @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log"))
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) +
                                @"\Wolfcom\Wems Extractor 2.1\" + LogFolderName + @"\" + device.DeviceId + ".log");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                HideVolumeEngine.WriteLogs("logs", ex.Message + ex.StackTrace);
                return false;
            }
        }

    }

}
