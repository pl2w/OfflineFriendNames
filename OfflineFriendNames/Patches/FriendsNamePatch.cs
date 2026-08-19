using System.Collections.Generic;
using HarmonyLib;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

[HarmonyPatch(typeof(FriendBackendController))]
[HarmonyPatch("GetFriendsComplete")]
public static class FriendsNamePatch
{
    private static readonly Dictionary<string, string> NameCache = new();

    [HarmonyPrefix]
    private static bool Prefix(FriendBackendController __instance, FriendBackendController.GetFriendsResponse response)
    {
        if (response?.Result?.Friends == null)
            return true;

        var toResolve = new List<FriendBackendController.Friend>();
        foreach (var friend in response.Result.Friends)
        {
            if (friend?.Presence == null || !string.IsNullOrEmpty(friend.Presence.UserName))
                continue;

            var id = friend.Presence.FriendLinkId;
            if (string.IsNullOrEmpty(id))
                continue;

            if (NameCache.TryGetValue(id, out var cached))
            {
                friend.Presence.UserName = cached;
            }
            else
            {
                toResolve.Add(friend);
            }
        }

        if (toResolve.Count == 0)
            return true;
        
        var pending = toResolve.Count;

        foreach (var friend in toResolve)
        {
            var id = friend.Presence.FriendLinkId;
            PlayFabClientAPI.GetPlayerProfile(new GetPlayerProfileRequest
            {
                PlayFabId = id,
                ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
            }, result =>
            {
                string name = result?.PlayerProfile?.DisplayName;
                if (!string.IsNullOrEmpty(name))
                {
                    NameCache[id] = name;
                    friend.Presence.UserName = name;
                }
                pending--;
                if (pending == 0)
                    __instance.GetFriendsComplete(response);
            }, error =>
            {
                Debug.LogError($"Could not get player profile for {id}: {error.GenerateErrorReport()}");
                pending--;
                if (pending == 0)
                    __instance.GetFriendsComplete(response);
            });
        }

        return false;
    }
}