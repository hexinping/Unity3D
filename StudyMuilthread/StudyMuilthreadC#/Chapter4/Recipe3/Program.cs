﻿using System;
using System.Threading.Tasks;
using static System.Console;
using static System.Threading.Thread;

namespace Recipe2
{
    class Program
    {
        static void Main(string[] args)
        {
            WriteLine($"主线程 线程 Id {CurrentThread.ManagedThreadId}");

            // 创建两个任务
            var firstTask = new Task<int>(() => TaskMethod("Frist Task",3));
            var secondTask = new Task<int>(()=> TaskMethod("Second Task",2));

            // 在默认的情况下 ContiueWith会在前面任务运行后再运行
            firstTask.ContinueWith(t => WriteLine($"第一次运行答案是 {t.Result}. 线程Id {CurrentThread.ManagedThreadId}. 是否为线程池线程: {CurrentThread.IsThreadPoolThread}"));

            // 启动任务
            firstTask.Start();
            secondTask.Start();

            Sleep(TimeSpan.FromSeconds(4));

            // 这里会紧接着 Second Task运行后运行， 但是由于添加了 OnlyOnRanToCompletion 和 ExecuteSynchronously 所以会由运行SecondTask的线程来 运行这个任务
            Task continuation = secondTask.ContinueWith(t => WriteLine($"第二次运行的答案是 {t.Result}. 线程Id {CurrentThread.ManagedThreadId}. 是否为线程池线程：{CurrentThread.IsThreadPoolThread}"),TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);

            // OnCompleted 是一个事件  当contiuation运行完成后 执行OnCompleted Action事件
            continuation.GetAwaiter().OnCompleted(() => WriteLine($"后继任务完成. 线程Id {CurrentThread.ManagedThreadId}. 是否为线程池线程 {CurrentThread.IsThreadPoolThread}"));

            Sleep(TimeSpan.FromSeconds(2));
            WriteLine();

            firstTask = new Task<int>(() => 
            {
                // 使用了TaskCreationOptions.AttachedToParent 将这个Task和父Task关联， 当这个Task没有结束时  父Task 状态为 WaitingForChildrenToComplete
                var innerTask = Task.Factory.StartNew(() => TaskMethod("Second Task",5), TaskCreationOptions.AttachedToParent);

                innerTask.ContinueWith(t => TaskMethod("Thrid Task", 2), TaskContinuationOptions.AttachedToParent);

                return TaskMethod("First Task",2);
            });

            firstTask.Start();

            // 检查firstTask线程状态  根据上面的分析 首先是  Running -> WatingForChildrenToComplete -> RanToCompletion
            while (! firstTask.IsCompleted)
            {
                WriteLine(firstTask.Status);

                Sleep(TimeSpan.FromSeconds(0.5));
            }

            WriteLine(firstTask.Status);

            Console.ReadLine();
        }

        static int TaskMethod(string name, int seconds)
        {
            WriteLine($"任务 {name} 正在运行,线程池线程 Id {CurrentThread.ManagedThreadId},是否为线程池线程: {CurrentThread.IsThreadPoolThread}");

            Sleep(TimeSpan.FromSeconds(seconds));

            return 42 * seconds;
        }
    }
}