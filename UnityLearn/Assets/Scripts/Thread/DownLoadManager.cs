﻿
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DownLoadManager
{
    private static DownLoadManager _instance;

    const string lookUpFile = "lookUp.txt";
    private Dictionary<string, string> lookUpDic = new Dictionary<string, string>();
    private static System.Object fileLocker = new System.Object();
    private static System.Object processLocker = new System.Object();

    private string ConstSavePath = string.Empty; 
    private string lookUpfilePath = string.Empty;

    const int ReadWriteTimeOut = 2 * 1000;//超时等待时间
    const int TimeOutWait = 5 * 1000;//超时等待时间


    public static DownLoadManager GetInstance()
    {
        if (_instance == null)
        {
            _instance = new DownLoadManager();
            _instance.Init();
        }
        return _instance;

    }

    private void Init()
    {
        ConstSavePath = Path.Combine(Application.dataPath, "DownLoadTest"); //TODO 需要修改成Application.persistentDataPath
        ConstSavePath = ConstSavePath.Replace(@"\", @"/");

        if (!Directory.Exists(ConstSavePath))
        {
            Directory.CreateDirectory(ConstSavePath);
        }
        lookUpfilePath = ConstSavePath + "/" + lookUpFile;
        if (!File.Exists(lookUpfilePath))
        {
            File.Create(lookUpfilePath);
        }
        string[] RawString = System.IO.File.ReadAllLines(lookUpfilePath);  //路径

        for (int i = 0; i < RawString.Length; i++)     //
        {
            string[] ss = RawString[i].Split(';');     //截断字节
            lookUpDic.Add(ss[0], ss[1]); // key为url，value 为filePath

        }
        Loom.Initialize();
    }

    public static Texture2D BytesToTexture2D(byte[] bytes, int w = 100, int h = 100)
    {
        Texture2D texture2D = new Texture2D(w, h);
        texture2D.LoadImage(bytes);
        return texture2D;
    }

    private static string GetMD5(string str)
    {

        byte[] resultBytes = System.Text.Encoding.UTF8.GetBytes(str);
        //创建一个MD5的对象
        MD5 md5 = new MD5CryptoServiceProvider();
        //调用MD5的ComputeHash方法将字节数组加密
        byte[] outPut = md5.ComputeHash(resultBytes);
        System.Text.StringBuilder hashString = new System.Text.StringBuilder();
        //最后把加密后的字节数组转为字符串
        for (int i = 0; i < outPut.Length; i++)
        {
            hashString.Append(System.Convert.ToString(outPut[i], 16).PadLeft(2, '0'));
        }
        md5.Dispose();
        return hashString.ToString();
    }

    private static long GetLength(string url)
    {
        HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
        request.Method = "HEAD";
        request.ReadWriteTimeout = ReadWriteTimeOut;
        request.Timeout = TimeOutWait;
        HttpWebResponse response = null;
        try
        {
            response = request.GetResponse() as HttpWebResponse;
        }
        catch (Exception ex)
        {

            Debug.LogError($"url == {url} |||| {ex.ToString()}");
        }

        return response.ContentLength;
    }

    #region 下载接口

    public void DownLoadTextureWithTaskAD(DownLoadImageTask downLoadTask)
    {
        if (downLoadTask == null) return;
        string filePath = string.Empty;
        string url = downLoadTask.url;
        if (lookUpDic.Count > 0)
        {
            if (lookUpDic.ContainsKey(url))
            {
                filePath = lookUpDic[url];
            }
        }
        if (!string.IsNullOrEmpty(filePath))
        {
            //已经下载过了
            downLoadTask.isDone = true;
            downLoadTask.progress = 1;
            //子线程进行读取操作，读取完成回调到主线程
            Debug.Log($"文件:{filePath} 已下载完了 直接读取文件======");
            Task.Run(() =>
            {
                byte[] fbuffer = null;
                lock (fileLocker)
                {
                    FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    fbuffer = new byte[fs.Length];
                    fs.Position = 0;
                    fs.Read(fbuffer, 0, fbuffer.Length);
                    fs.Close();
                    fs.Dispose();
                }
               
                //使用loom传回给主线程，执行主线程回调函数
                Loom.QueueOnMainThread((System.Object t) =>
                {
                    Texture2D texture = BytesToTexture2D(fbuffer, downLoadTask.texW, downLoadTask.texH);
                    downLoadTask.ExcuteHandle(texture);
                }, null);

            });
        }
        else
        {
            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
            var cts = new CancellationTokenSource();
            Task task = new Task(() =>
            {
                stopWatch.Start();
                //************开启子线程下载
                //判断保存路径是否存在
                if (!Directory.Exists(ConstSavePath))
                {
                    Directory.CreateDirectory(ConstSavePath);
                }
                long totalLength = GetLength(url);

                if (string.IsNullOrEmpty(downLoadTask.saveFileName))
                {
                    string fileName = GetMD5(url);
                    filePath = $"{ConstSavePath}/{fileName}.png";
                }
                else
                {
                    filePath = $"{ConstSavePath}/{downLoadTask.saveFileName}.png";
                }
                //获取文件现在的长度
                FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                long fileLength = fs.Length;

                //如果没下载完
                byte[] fbuffer = null; //回调函数里需要的Byte数组
                if (fileLength < totalLength)
                {
                    downLoadTask.progress = fileLength/ totalLength;
                    //断点续传核心，设置本地文件流的起始位置
                    fs.Seek(fileLength, SeekOrigin.Begin);

                    HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                    request.ReadWriteTimeout = ReadWriteTimeOut;
                    request.Timeout = TimeOutWait;
                    Stream stream = null;
                    //断点续传核心，设置远程访问文件流的起始位置
                    request.AddRange((int)fileLength);

                    try
                    {
                        stream = request.GetResponse().GetResponseStream();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"url == {url} |||| {ex.ToString()}");
                        cts.Cancel();
                    }

                    byte[] buffer = new byte[1024];
                    //使用流读取内容到buffer中
                    //返回值代表读取的实际长度,并不是buffer有多大，stream就会读进去多少
                    int length = stream.Read(buffer, 0, buffer.Length);
                    
                    while (length > 0)
                    {

                        //将内容再写入本地文件中
                        fs.Write(buffer, 0, length);
                        //计算进度
                        fileLength += length;
                        downLoadTask.progress = (float)fileLength / (float)totalLength;
                        //类似尾递归
                        length = stream.Read(buffer, 0, buffer.Length);

                    }


                    fbuffer = new byte[fs.Length];
                    fs.Position = 0;
                    fs.Read(fbuffer, 0, fbuffer.Length);
                    stream.Close();
                    stream.Dispose();
                }
                else
                {
                    downLoadTask.progress = 1f;
                }

                stopWatch.Stop();
                Debug.Log($"耗时: {stopWatch.ElapsedMilliseconds}");
                fs.Close();
                fs.Dispose();
                //如果下载完毕，执行回调
                if (downLoadTask.progress == 1)
                {
                    downLoadTask.isDone = true;
                    //使用loom传回给主线程，执行主线程回调函数
                    Loom.QueueOnMainThread((System.Object t) =>
                    {
                        Texture2D texture = BytesToTexture2D(fbuffer, downLoadTask.texW, downLoadTask.texH);
                        downLoadTask.ExcuteHandle(texture);
                    }, null);


                    //子线程中写入lookup文件中，url和filePath 一一对应
                    lock (fileLocker)
                    {
                        FileStream fs1 = new FileStream(lookUpfilePath, FileMode.Append, FileAccess.Write);
                        string content = $"{url};{filePath} \n";

                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                        fs1.Write(bytes, 0, bytes.Length);
                        fs1.Flush();
                        fs1.Close();
                        fs1.Dispose();

                        lookUpDic.Add(url, filePath);
                    }

                    Debug.Log($"{url} download finished");
                    cts.Cancel();
                }

            }, cts.Token);
            task.Start();
        }

    }

    public void DownLoadTextureWithTaskADs(List<DownLoadImageTask> downLoadTaskList, Action AllDownLoadEndHandle)
    {
        if (downLoadTaskList == null)
            return;

        int totalTaskCount = downLoadTaskList.Count;
        if (totalTaskCount > 0)
        {
            bool isDownLoadAllEnd = false;
            int idx = 0;
            float totalProgress = 0;
            foreach (var item in downLoadTaskList)
            {
                DownLoadImageTask downLoadTask = item;
                if (downLoadTask == null) return;
                string filePath = string.Empty;
                string url = downLoadTask.url;
                if (lookUpDic.Count > 0)
                {
                    if (lookUpDic.ContainsKey(url))
                    {
                        filePath = lookUpDic[url];
                    }
                }
                if (!string.IsNullOrEmpty(filePath))
                {
                    //已经下载过了
                    //子线程进行读取操作，读取完成回调到主线程
                    Debug.Log($"文件:{filePath} 已下载完了 直接读取文件======");
                    
                    downLoadTask.isDone = true;
                    downLoadTask.progress = 1;

                    lock (processLocker)
                    {
                        idx++;
                        totalProgress = (float)idx / (float)totalTaskCount;
                        Debug.Log($"totalProgress {totalProgress}");
                    }
                    if (totalProgress >= 1 && !isDownLoadAllEnd)
                    {
                        isDownLoadAllEnd = true;
                        AllDownLoadEndHandle?.Invoke();
                    }
                    Task.Run(() =>
                    {
                        byte[] fbuffer = null;
                        lock (fileLocker)
                        {
                            FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            fbuffer = new byte[fs.Length];
                            fs.Position = 0;
                            fs.Read(fbuffer, 0, fbuffer.Length);
                            fs.Close();
                            fs.Dispose();
                        }
                        
                        //使用loom传回给主线程，执行主线程回调函数
                        Loom.QueueOnMainThread((System.Object t) =>
                        {
                            Texture2D texture = BytesToTexture2D(fbuffer, downLoadTask.texW, downLoadTask.texH);
                            downLoadTask.ExcuteHandle(texture);
                        }, null);

                        
                    });
                }
                else
                {
                    System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
                    var cts = new CancellationTokenSource();
                    Task task = new Task(() =>
                    {
                        stopWatch.Start();
                        //************开启子线程下载
                        //判断保存路径是否存在
                        if (!Directory.Exists(ConstSavePath))
                        {
                            Directory.CreateDirectory(ConstSavePath);
                        }
                        long totalLength = GetLength(url);

                        if (string.IsNullOrEmpty(downLoadTask.saveFileName))
                        {
                            string fileName = GetMD5(url);
                            filePath = $"{ConstSavePath}/{fileName}.png";
                        }
                        else
                        {
                            filePath = $"{ConstSavePath}/{downLoadTask.saveFileName}.png";
                        }
                        //获取文件现在的长度
                        FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        long fileLength = fs.Length;

                        //如果没下载完
                        byte[] fbuffer = null; //回调函数里需要的Byte数组
                        if (fileLength < totalLength)
                        {
                            downLoadTask.progress = fileLength / totalLength;
                            //断点续传核心，设置本地文件流的起始位置
                            fs.Seek(fileLength, SeekOrigin.Begin);

                            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                            request.ReadWriteTimeout = ReadWriteTimeOut;
                            request.Timeout = TimeOutWait;
                            Stream stream = null;
                            //断点续传核心，设置远程访问文件流的起始位置
                            request.AddRange((int)fileLength);

                            try
                            {
                                stream = request.GetResponse().GetResponseStream();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"url == {url} |||| {ex.ToString()}");
                                cts.Cancel();
                            }

                            byte[] buffer = new byte[1024];
                            //使用流读取内容到buffer中
                            //注意方法返回值代表读取的实际长度,并不是buffer有多大，stream就会读进去多少
                            int length = stream.Read(buffer, 0, buffer.Length);

                            while (length > 0)
                            {

                                //将内容再写入本地文件中
                                fs.Write(buffer, 0, length);
                                //计算进度
                                fileLength += length;
                                downLoadTask.progress = (float)fileLength / (float)totalLength;
                                //类似尾递归
                                length = stream.Read(buffer, 0, buffer.Length);

                            }


                            fbuffer = new byte[fs.Length];
                            fs.Position = 0;
                            fs.Read(fbuffer, 0, fbuffer.Length);
                            stream.Close();
                            stream.Dispose();

                            lock (processLocker)
                            {
                                idx++;
                                totalProgress = (float)idx / (float)totalTaskCount;
                                Debug.Log($"totalProgress {totalProgress}");
                            }
                            
                        }
                        else
                        {
                            downLoadTask.progress = 1f;
                            lock (processLocker)
                            {
                                idx++;
                                totalProgress = (float)idx / (float)totalTaskCount;
                                Debug.Log($"totalProgress {totalProgress}");
                            }
                        }

                        stopWatch.Stop();
                        Debug.Log($"耗时: {stopWatch.ElapsedMilliseconds}");
                        fs.Close();
                        fs.Dispose();
                        //如果下载完毕，执行回调
                        if (downLoadTask.progress == 1)
                        {
                            downLoadTask.isDone = true;
                            //使用loom传回给主线程，执行主线程回调函数
                            Loom.QueueOnMainThread((System.Object t) =>
                            {
                                Texture2D texture = BytesToTexture2D(fbuffer, downLoadTask.texW, downLoadTask.texH);
                                downLoadTask.ExcuteHandle(texture);
                            }, null);


                            //子线程中写入lookup文件中，url和filePath 一一对应
                            lock (fileLocker)
                            {
                                FileStream fs1 = new FileStream(lookUpfilePath, FileMode.Append, FileAccess.Write);
                                string content = $"{url};{filePath} \n";

                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                                fs1.Write(bytes, 0, bytes.Length);
                                fs1.Flush();
                                fs1.Close();
                                fs1.Dispose();

                                lookUpDic.Add(url, filePath);
                            }

                            Debug.Log($"{url} download finished");
                            cts.Cancel();
                        }

                        if (totalProgress >= 1 && !isDownLoadAllEnd)
                        {
                            isDownLoadAllEnd = true;
                            Loom.QueueOnMainThread((System.Object t) =>
                            {
                                AllDownLoadEndHandle?.Invoke();
                            }, null);
                        }

                    }, cts.Token);
                    task.Start();
                }
            }
        
        
        }
    }


    public void DownLoadFileWithTaskAD(DownLoadFileTask downLoadTask)
    {
        if (downLoadTask == null) return;
        string filePath = string.Empty;
        string url = downLoadTask.url;
        if (lookUpDic.Count > 0)
        {
            if (lookUpDic.ContainsKey(url))
            {
                filePath = lookUpDic[url];
            }
        }
        if (!string.IsNullOrEmpty(filePath))
        {
            //已经下载过了
            downLoadTask.isDone = true;
            downLoadTask.progress = 1;
            //子线程进行读取操作，读取完成回调到主线程
            Debug.Log($"文件:{filePath} 已下载完了 直接读取文件======");
            Task.Run(() =>
            {
                byte[] fbuffer = null;
                lock (fileLocker)
                {
                    FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    fbuffer = new byte[fs.Length];
                    fs.Position = 0;
                    fs.Read(fbuffer, 0, fbuffer.Length);
                    fs.Close();
                    fs.Dispose();
                }

                //使用loom传回给主线程，执行主线程回调函数
                Loom.QueueOnMainThread((System.Object t) =>
                {
                    downLoadTask.ExcuteHandle(fbuffer);
                }, null);

            });
        }
        else
        {
            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
            var cts = new CancellationTokenSource();
            Task task = new Task(() =>
            {
                stopWatch.Start();
                //************开启子线程下载
                //判断保存路径是否存在
                if (!Directory.Exists(ConstSavePath))
                {
                    Directory.CreateDirectory(ConstSavePath);
                }
                long totalLength = GetLength(url);

                if (string.IsNullOrEmpty(downLoadTask.saveFileName))
                {
                    string fileName = GetMD5(url);
                    filePath = $"{ConstSavePath}/{fileName}.txt";
                }
                else
                {
                    filePath = $"{ConstSavePath}/{downLoadTask.saveFileName}.txt";
                }
                //获取文件现在的长度
                FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                long fileLength = fs.Length;

                //如果没下载完
                byte[] fbuffer = null; //回调函数里需要的Byte数组
                if (fileLength < totalLength)
                {
                    downLoadTask.progress = fileLength / totalLength;
                    //断点续传核心，设置本地文件流的起始位置
                    fs.Seek(fileLength, SeekOrigin.Begin);

                    HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                    request.ReadWriteTimeout = ReadWriteTimeOut;
                    request.Timeout = TimeOutWait;
                    Stream stream = null;
                    //断点续传核心，设置远程访问文件流的起始位置
                    request.AddRange((int)fileLength);

                    try
                    {
                        stream = request.GetResponse().GetResponseStream();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"url == {url} |||| {ex.ToString()}");
                        cts.Cancel();
                    }

                    byte[] buffer = new byte[1024];
                    //使用流读取内容到buffer中
                    //返回值代表读取的实际长度,并不是buffer有多大，stream就会读进去多少
                    int length = stream.Read(buffer, 0, buffer.Length);

                    while (length > 0)
                    {

                        //将内容再写入本地文件中
                        fs.Write(buffer, 0, length);
                        //计算进度
                        fileLength += length;
                        downLoadTask.progress = (float)fileLength / (float)totalLength;
                        //类似尾递归
                        length = stream.Read(buffer, 0, buffer.Length);

                    }


                    fbuffer = new byte[fs.Length];
                    fs.Position = 0;
                    fs.Read(fbuffer, 0, fbuffer.Length);
                    stream.Close();
                    stream.Dispose();
                }
                else
                {
                    downLoadTask.progress = 1f;
                }

                stopWatch.Stop();
                Debug.Log($"耗时: {stopWatch.ElapsedMilliseconds}");
                fs.Close();
                fs.Dispose();
                //如果下载完毕，执行回调
                if (downLoadTask.progress == 1)
                {
                    downLoadTask.isDone = true;
                    //使用loom传回给主线程，执行主线程回调函数
                    Loom.QueueOnMainThread((System.Object t) =>
                    {
                        downLoadTask.ExcuteHandle(fbuffer);
                    }, null);


                    //子线程中写入lookup文件中，url和filePath 一一对应
                    lock (fileLocker)
                    {
                        FileStream fs1 = new FileStream(lookUpfilePath, FileMode.Append, FileAccess.Write);
                        string content = $"{url};{filePath} \n";

                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                        fs1.Write(bytes, 0, bytes.Length);
                        fs1.Flush();
                        fs1.Close();
                        fs1.Dispose();

                        lookUpDic.Add(url, filePath);
                    }

                    Debug.Log($"{url} download finished");
                    cts.Cancel();
                }

            }, cts.Token);
            task.Start();
        }

    }

    public void DownLoadFileWithTaskADs(List<DownLoadFileTask> downLoadTaskList, Action AllDownLoadEndHandle)
    {
        if (downLoadTaskList == null)
            return;

        int totalTaskCount = downLoadTaskList.Count;
        if (totalTaskCount > 0)
        {
            bool isDownLoadAllEnd = false;
            int idx = 0;
            float totalProgress = 0;
            foreach (var item in downLoadTaskList)
            {
                DownLoadFileTask downLoadTask = item;
                if (downLoadTask == null) return;
                string filePath = string.Empty;
                string url = downLoadTask.url;
                if (lookUpDic.Count > 0)
                {
                    if (lookUpDic.ContainsKey(url))
                    {
                        filePath = lookUpDic[url];
                    }
                }
                if (!string.IsNullOrEmpty(filePath))
                {
                    //已经下载过了
                    //子线程进行读取操作，读取完成回调到主线程
                    Debug.Log($"文件:{filePath} 已下载完了 直接读取文件======");

                    downLoadTask.isDone = true;
                    downLoadTask.progress = 1;

                    lock (processLocker)
                    {
                        idx++;
                        totalProgress = (float)idx / (float)totalTaskCount;
                        Debug.Log($"totalProgress {totalProgress}");
                    }
                    if (totalProgress >= 1 && !isDownLoadAllEnd)
                    {
                        isDownLoadAllEnd = true;
                        AllDownLoadEndHandle?.Invoke();
                    }
                    Task.Run(() =>
                    {
                        byte[] fbuffer = null;
                        lock (fileLocker)
                        {
                            FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            fbuffer = new byte[fs.Length];
                            fs.Position = 0;
                            fs.Read(fbuffer, 0, fbuffer.Length);
                            fs.Close();
                            fs.Dispose();
                        }

                        //使用loom传回给主线程，执行主线程回调函数
                        Loom.QueueOnMainThread((System.Object t) =>
                        {
                            downLoadTask.ExcuteHandle(fbuffer);
                        }, null);


                    });
                }
                else
                {
                    System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
                    var cts = new CancellationTokenSource();
                    Task task = new Task(() =>
                    {
                        stopWatch.Start();
                        //************开启子线程下载
                        //判断保存路径是否存在
                        if (!Directory.Exists(ConstSavePath))
                        {
                            Directory.CreateDirectory(ConstSavePath);
                        }
                        long totalLength = GetLength(url);

                        if (string.IsNullOrEmpty(downLoadTask.saveFileName))
                        {
                            string fileName = GetMD5(url);
                            filePath = $"{ConstSavePath}/{fileName}.txt";
                        }
                        else
                        {
                            filePath = $"{ConstSavePath}/{downLoadTask.saveFileName}.txt";
                        }
                        //获取文件现在的长度
                        FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        long fileLength = fs.Length;

                        //如果没下载完
                        byte[] fbuffer = null; //回调函数里需要的Byte数组
                        if (fileLength < totalLength)
                        {
                            downLoadTask.progress = fileLength / totalLength;
                            //断点续传核心，设置本地文件流的起始位置
                            fs.Seek(fileLength, SeekOrigin.Begin);

                            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                            request.ReadWriteTimeout = ReadWriteTimeOut;
                            request.Timeout = TimeOutWait;
                            Stream stream = null;
                            //断点续传核心，设置远程访问文件流的起始位置
                            request.AddRange((int)fileLength);

                            try
                            {
                                stream = request.GetResponse().GetResponseStream();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"url == {url} |||| {ex.ToString()}");
                                cts.Cancel();
                            }

                            byte[] buffer = new byte[1024];
                            //使用流读取内容到buffer中
                            //注意方法返回值代表读取的实际长度,并不是buffer有多大，stream就会读进去多少
                            int length = stream.Read(buffer, 0, buffer.Length);

                            while (length > 0)
                            {

                                //将内容再写入本地文件中
                                fs.Write(buffer, 0, length);
                                //计算进度
                                fileLength += length;
                                downLoadTask.progress = (float)fileLength / (float)totalLength;
                                //类似尾递归
                                length = stream.Read(buffer, 0, buffer.Length);

                            }


                            fbuffer = new byte[fs.Length];
                            fs.Position = 0;
                            fs.Read(fbuffer, 0, fbuffer.Length);
                            stream.Close();
                            stream.Dispose();

                            lock (processLocker)
                            {
                                idx++;
                                totalProgress = (float)idx / (float)totalTaskCount;
                                Debug.Log($"totalProgress {totalProgress}");
                            }

                        }
                        else
                        {
                            downLoadTask.progress = 1f;
                            lock (processLocker)
                            {
                                idx++;
                                totalProgress = (float)idx / (float)totalTaskCount;
                                Debug.Log($"totalProgress {totalProgress}");
                            }
                        }

                        stopWatch.Stop();
                        Debug.Log($"耗时: {stopWatch.ElapsedMilliseconds}");
                        fs.Close();
                        fs.Dispose();
                        //如果下载完毕，执行回调
                        if (downLoadTask.progress == 1)
                        {
                            downLoadTask.isDone = true;
                            //使用loom传回给主线程，执行主线程回调函数
                            Loom.QueueOnMainThread((System.Object t) =>
                            {
                                downLoadTask.ExcuteHandle(fbuffer);
                            }, null);


                            //子线程中写入lookup文件中，url和filePath 一一对应
                            lock (fileLocker)
                            {
                                FileStream fs1 = new FileStream(lookUpfilePath, FileMode.Append, FileAccess.Write);
                                string content = $"{url};{filePath} \n";

                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                                fs1.Write(bytes, 0, bytes.Length);
                                fs1.Flush();
                                fs1.Close();
                                fs1.Dispose();

                                lookUpDic.Add(url, filePath);
                            }

                            Debug.Log($"{url} download finished");
                            cts.Cancel();
                        }

                        if (totalProgress >= 1 && !isDownLoadAllEnd)
                        {
                            isDownLoadAllEnd = true;
                            Loom.QueueOnMainThread((System.Object t) =>
                            {
                                AllDownLoadEndHandle?.Invoke();
                            }, null);
                        }

                    }, cts.Token);
                    task.Start();
                }
            }


        }
    }
    #endregion
}
