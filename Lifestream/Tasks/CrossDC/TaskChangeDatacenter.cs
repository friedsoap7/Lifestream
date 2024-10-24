﻿using ECommons.GameHelpers;
using Lifestream.Schedulers;

namespace Lifestream.Tasks.CrossDC;

internal static class TaskChangeDatacenter
{
    internal static int NumRetries = 300;
    internal static long RetryAt = 1;

    internal static void Enqueue(string destination, string charaName, uint charaWorld)
    {
        NumRetries = 0;
        EnqueueVisitTasks(destination, charaName, charaWorld);
        P.TaskManager.Enqueue(DCChange.ConfirmDcVisit, TaskSettings.Timeout2M);
        P.TaskManager.Enqueue(DCChange.ConfirmDcVisit2, TaskSettings.Timeout2M);
        P.TaskManager.Enqueue(DCChange.SelectOk, TaskSettings.TimeoutInfinite);
        P.TaskManager.Enqueue(() => DCChange.SelectServiceAccount(Utils.GetServiceAccount(charaName, charaWorld)), $"SelectServiceAccount_{charaName}@{charaWorld}", TaskSettings.Timeout1M);
    }

    private static void EnqueueVisitTasks(string destination, string charaName, uint charaWorld)
    {
        var dc = Utils.GetDataCenterName(destination);
        PluginLog.Debug($"Beginning data center changing process. Destination: {dc}, {destination}");
        P.TaskManager.Enqueue(() => DCChange.OpenContextMenuForChara(charaName, charaWorld), nameof(DCChange.OpenContextMenuForChara), TaskSettings.Timeout5M);
        P.TaskManager.Enqueue(DCChange.SelectVisitAnotherDC);
        P.TaskManager.Enqueue(DCChange.ConfirmDcVisitIntention);
        P.TaskManager.Enqueue(() => DCChange.SelectTargetDataCenter(dc), nameof(DCChange.SelectTargetDataCenter), TaskSettings.Timeout2M);
        P.TaskManager.Enqueue(() => DCChange.SelectTargetWorld(destination, () => RetryVisit(destination, charaName, charaWorld)), nameof(DCChange.SelectTargetWorld), TaskSettings.Timeout60M);
    }

    private static bool RetryVisit(string destination, string charaName, uint charaWorld)
    {
        if(!P.Config.EnableDvcRetry) return false;
        if(RetryAt == 0)
        {
            RetryAt = Environment.TickCount64 + P.Config.DcvRetryInterval * 1000;
        }
        else if(Environment.TickCount64 > RetryAt)
        {
            RetryAt = 0;
            NumRetries++;
            if(NumRetries > P.Config.MaxDcvRetries)
            {
                PluginLog.Warning($"DC visit retry limit exceeded");
                P.TaskManager.Abort();
                return true;
            }
            P.TaskManager.BeginStack();
            P.TaskManager.Enqueue(DCChange.CancelDcVisit);
            EnqueueVisitTasks(destination, charaName, charaWorld);
            P.TaskManager.InsertStack();
            return true;
        }
        return false;
    }
}
