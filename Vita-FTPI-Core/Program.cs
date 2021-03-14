﻿using System;
using System.Net.Sockets;
using System.IO;
using WinSCP;
using System.IO.Compression;
using System.Threading;

enum StorageType
{
    Unconfigured,
    sd2vita,
    OFFICIAL
}

namespace Vita_FTPI_Core
{
    class Program
    {
        static StorageType storageType;
        static string driveLetter = "";
        static bool useUSB = false;
        static string VitaIP = "";
        static string VPKPath = "";
        static int port = 1337;
        static string UploadFolder = "";
        static SessionOptions sessionOptions;
        static string ExtractPath = "Extracted/";
        static private string pkgTempFolder = "/temp/pkg";
        static string SendPath = "ux0:/data/sent.vpk";
        static string configDir = "ux0:/data/UnityLoader";
        static string TempFileName = "tempFile";
        static bool Extracted = false;
        static string[] configFiles = { "/EXTRACTED", "/CONFIG_READY", "/USB", "/sd2vita", "/OFFICIAL", "/RUNCOMPLETE", "/COPYING", "/INSTALL" };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No input specified Aboring!");
                return;
            }

            //Setting all the arguments
            for (int x = 0; x < args.Length; x += 2)
            {
                if (args[x] == "--vpk")
                {
                    VPKPath = args[x + 1];
                }
                if (args[x] == "--ip")
                {
                    VitaIP = args[x + 1];
                }
                if (args[x] == "port")
                {
                    port = int.Parse(args[x + 1]);
                }
                if (args[x] == "--usb")
                {
                    useUSB = (args[x + 1] == "true");
                }
                if (args[x] == "--drive-letter")
                {
                    driveLetter = args[x + 1];
                }
                if(args[x] == "--extracted")
                {
                    Extracted = (args[x + 1] == "true");
                }
                if (args[x] == "--storage-type")
                {
                    storageType = parseStorageType(args[x + 1]);
                    if (storageType == StorageType.Unconfigured)
                    {
                        Console.WriteLine("Incorrect storage type given it should either be sd2vita or OFFICIAL remember it is case sensitive");
                        return;
                    }
                }
                if(args[x] == "--upload-dir")
                {
                    if(Directory.Exists(args[x + 1]))
                    {
                        UploadFolder = args[x + 1];
                    }
                    else
                    {
                        Console.WriteLine("Error uploader folder not found exiting....");
                        Console.WriteLine(UploadFolder);
                        Thread.Sleep(5000);
                        return;
                    }
                }
            }

            if (Directory.Exists("Uploader") && UploadFolder == "")
                Directory.SetCurrentDirectory("Uploader");
            else Directory.SetCurrentDirectory(UploadFolder);

            if (VitaIP == "" || VPKPath == "")
            {
                Console.WriteLine("Invalid Arguments Aborting!");
                Thread.Sleep(5000);
                return;
            }

            if (!File.Exists(VPKPath))
            {
                //Checking if the input file specified exists
                Console.WriteLine("No file found. Check your input path and make sure to include the file extension.");
                Console.WriteLine(VPKPath);
                Thread.Sleep(5000);
                return;
            }


            ConfigureOptions();
            if (!useUSB) UploadInstall();
            else USBInstall();
        }

        static StorageType parseStorageType(string st)
        {
            if (st == "OFFICIAL")
                return StorageType.OFFICIAL;

            if (st == "sd2vita")
                return StorageType.sd2vita;

            return StorageType.Unconfigured;
        }

        static string StorageTypeToString(StorageType st)
        {
            if (st == StorageType.OFFICIAL)
                return "OFFICIAL";

            if (st == StorageType.sd2vita)
                return "sd2vita";

            return "unconfigured";
        }

        static void USBInstall()
        {
            using (Session session = new Session())
            {
                session.FileTransferProgress += new FileTransferProgressEventHandler(ProgressChanged);
                Console.WriteLine("Connecting to vita.");
                session.Open(sessionOptions);
                TransferOptions toptions = new TransferOptions();
                toptions.TransferMode = TransferMode.Binary;

                //This part tells the app which settings we need and if we are ready
                Console.WriteLine("Creating Config Directory");
                if (!session.FileExists(configDir))
                    session.CreateDirectory("ux0:/data/UnityLoader");

                Console.WriteLine("Deleting Old Config Files...");

                foreach (string file in configFiles)
                    if (session.FileExists(configDir + file)) session.RemoveFile(configDir + file);

                File.WriteAllText(TempFileName, "");

                Console.WriteLine("Creating Config");

                TransferOperationResult tresult = session.PutFiles(TempFileName, configDir + "/CONFIG_READY");
                tresult.Check();
                TransferOperationResult tresult2 = session.PutFiles(TempFileName, configDir + "/USB");
                tresult2.Check();
                TransferOperationResult tresult3 = session.PutFiles(TempFileName, configDir + "/" + StorageTypeToString(storageType));
                tresult3.Check();
                TransferOperationResult tresult4 = session.PutFiles(TempFileName, configDir + "/COPYING");
                tresult4.Check();
                if (Extracted)
                {
                    TransferOperationResult tresult5 = session.PutFiles(TempFileName, configDir + "/EXTRACTED");
                    tresult5.Check();
                }
                TransferOperationResult tresult6 = session.PutFiles(TempFileName, configDir + "/INSTALL");
                tresult6.Check();
                Console.WriteLine("Config Sucess");
                
                if (!Extracted)
                {
                    Console.WriteLine("Copying VPK");
                    LaunchUnityLoader();
                    while (!Directory.Exists(driveLetter + "/data"))
                    {
                        Thread.Sleep(100);
                    }
                    File.Copy(VPKPath, driveLetter + "/data/sent.vpk", true);
                    session.RemoveFile(configDir + "/COPYING");
                }
                else
                {
                    Console.WriteLine("Extracting...");
                    Extract(VPKPath, ExtractPath);
                    Console.WriteLine("Copying game files...");
                    LaunchUnityLoader();
                    while (!Directory.Exists(driveLetter + "/temp"))
                    {
                        Thread.Sleep(100);
                    }
                    if (Directory.Exists(driveLetter + pkgTempFolder))
                        Directory.Delete(driveLetter + pkgTempFolder);

                    Directory.CreateDirectory(driveLetter + pkgTempFolder);

                    CopyAll(new DirectoryInfo(ExtractPath), new DirectoryInfo(driveLetter + pkgTempFolder));
                    session.RemoveFile(configDir + "/COPYING");
                }
                File.Delete(TempFileName);

                session.Close();
            }
        }

        static void UploadInstall()
        {
            using (Session session = new Session())
            {
                session.FileTransferProgress += new FileTransferProgressEventHandler(ProgressChanged);
                Console.WriteLine("Connecting to vita.");
                session.Open(sessionOptions);
                TransferOptions toptions = new TransferOptions();
                toptions.TransferMode = TransferMode.Binary;

                //This part tells the app which settings we need and if we are ready
                Console.WriteLine("Creating Config Directory");
                if (!session.FileExists(configDir))
                    session.CreateDirectory("ux0:/data/UnityLoader");

                Console.WriteLine("Deleting Old Config Files...");

                foreach (string file in configFiles)
                    if (session.FileExists(configDir + file)) session.RemoveFile(configDir + file);

                File.WriteAllText(TempFileName, "");

                Console.WriteLine("Creating Config");
                TransferOperationResult iresult = session.PutFiles(TempFileName, configDir + "/INSTALL");
                TransferOperationResult tresult = session.PutFiles(TempFileName, configDir + "/CONFIG_READY");
                tresult.Check();
                foreach (FileOperationEventArgs result in tresult.Transfers)
                {
                    Console.WriteLine("Upload of {0} successful", (object)result.FileName);
                }
                if(Extracted)
                {
                    TransferOperationResult tresult2 = session.PutFiles(TempFileName, configDir + "/EXTRACTED");
                    tresult2.Check();
                }
                Console.WriteLine("Config Sucess");

                if (!Extracted)
                {
                    Console.WriteLine("Uploading VPK");
                    TransferOperationResult toresult = session.PutFiles(VPKPath, SendPath);
                    toresult.Check();
                    foreach (FileOperationEventArgs res in toresult.Transfers)
                    {
                        Console.WriteLine("Upload of {0} successful", (object)res.FileName);
                    }
                }
                if(Extracted)
                {
                    if (Directory.Exists(ExtractPath))
                        Directory.Delete(ExtractPath);
                    Directory.CreateDirectory(ExtractPath);
                    Console.WriteLine("Extracting VPK...");
                    ZipFile.ExtractToDirectory(VPKPath, "Extracted");
                    Console.WriteLine("Uploading VPK...");

                    TransferOperationResult result = session.PutFiles("Extracted", "ux0:" + pkgTempFolder, true);
                    result.Check();
                    foreach (FileOperationEventArgs res in result.Transfers)
                    {
                        Console.WriteLine("Upload of {0} successful", (object)res.FileName);
                    }
                }
                File.Delete(TempFileName);
                session.Close();
            }
            LaunchUnityLoader();
        }

        static void Extract(string sourceFile, string destDir)
        {
            if(Directory.Exists(sourceFile))
            {
                ZipFile.ExtractToDirectory(sourceFile, destDir);
            }    
        }
        static void LaunchUnityLoader()
        {
            Console.WriteLine("Launching Unity Loader on Vita");
            using (TcpClient client = new TcpClient(VitaIP, 1338))
            {
                using (NetworkStream ns = client.GetStream())
                {
                    using (StreamWriter sw = new StreamWriter(ns))
                    {
                        sw.Write("launch UNITYLOAD\n");
                        sw.Flush();
                        using (StreamReader sr = new StreamReader(ns))
                        {
                            Console.Write(sr.ReadToEnd());
                            sr.Close();
                        }
                        sw.Close();
                    }
                    ns.Close();
                }
                client.Close();
            }
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);
            foreach (FileInfo fi in source.GetFiles())
            {
                Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        static void ConfigureOptions()
        {
            Console.WriteLine("Configuring options.");
            //Configure the options for the FTP transfer
            sessionOptions = new SessionOptions
            {
                Protocol = Protocol.Ftp,
                HostName = VitaIP,
                PortNumber = port,
                UserName = "Anonymous",
                Password = ""
            };
        }

        static void ProgressChanged(object sender, FileTransferProgressEventArgs e)
        {
            Console.Clear();
            Console.WriteLine("Uploading: " + e.OverallProgress * 100 + "%");
        }
    }
}
