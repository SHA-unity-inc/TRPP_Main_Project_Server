

// Это пример кода  обращения к серверу














using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using WebSocketSharp;

public static class CornSocketSharp
{
    private static List<int> packetInvite = new List<int> { 0 }, packetNotSend = new List<int> { 0 }, 
        packetLost = new List<int> { 0 }, packetSend = new List<int> { 0 };
    private static bool isPacketChekerOn = false;

    private static List<WebSocket> ws = new List<WebSocket>();
    private static string serverReturnID;
    private static List<Action<string>> callbackID = new List<Action<string>>();
    private static TaskCompletionSource<int> soldierIdTask = new TaskCompletionSource<int>();
    private static TaskCompletionSource<List<Solder>> soldiersListTask = new TaskCompletionSource<List<Solder>>();

    private static Dictionary<int, Vector3> playerPoses = new Dictionary<int, Vector3>();
    private static Dictionary<int, int> gunSet = new Dictionary<int, int>();
    private static Dictionary<int, (Vector3, Vector3)> playerRotations = new Dictionary<int, (Vector3, Vector3)>();
    
    private static List<(BulletType, Vector3, Vector3, bool)> bulletsCreator = new List<(BulletType, Vector3, Vector3, bool)>();
    private static List<(BulletType type, int playerID)> hits = new List<(BulletType type, int playerID)>();
    private static List<(float hp, int playerID)> playerHp = new List<(float hp, int playerID)>();
    private static List<(int playerId, string name, object parm)> animationParms = new List<(int senderId, string name, object parm)>();
    private static List<int> playerRemove = new List<int>(), playerNew = new List<int>();

    public static int setWs = 0;
    public static List<WebSocket> Ws { get => ws; }
    public static Dictionary<int, Vector3> PlayerPoses { get => playerPoses; }
    public static Dictionary<int, (Vector3, Vector3)> PlayerRotations { get => playerRotations; }
    public static List<(BulletType, Vector3, Vector3, bool)> BulletsCreator { get => bulletsCreator; }
    public static List<(BulletType type, int playerID)> Hits { get => hits; }
    public static List<(float hp, int playerID)> PlayerHp { get => playerHp; }
    public static List<(int playerId, string name, object parm)> AnimationParms { get => animationParms; }
    public static Dictionary<int, int> GunSet { get => gunSet; }
    public static List<int> PlayerRemove { get => playerRemove; }
    public static List<int> PlayerNew { get => playerNew; }

    private static List<string> serverIps = new List<string>
    {
        "95.165.27.159", 
    };

    public static async Task<bool> ConnectSerwer()
    {
        if (ws.Count > 0)
        {
            if (ws[setWs].IsAlive)
            {
                Debug.LogError("��������� �����������");
                return false;
            }
            else
            {
                ws[setWs]?.Close();
                ws.RemoveAt(setWs);
            }
        }

        Debug.Log(DateTime.Now + "                                         START ");

        // ������� ������ CancellationTokenSource ��� ������ ������
        var ctsList = new List<CancellationTokenSource>();
        var connectionTasks = serverIps.Select(ip =>
        {
            var cts = new CancellationTokenSource();
            ctsList.Add(cts);
            return ConnectToServer(ip, cts.Token);
        }).ToArray();

        while (connectionTasks.Length > 0)
        {
            try
            {
                // ������� ���������� ����� ������
                var completedTask = await Task.WhenAny(connectionTasks);

                // ������� ����������� ������ �� �������
                var remainingTasks = connectionTasks.Except(new[] { completedTask });
                connectionTasks = remainingTasks.ToArray();

                // ���� ������ ����������� �������, ���������� true
                if (completedTask.Result)
                {
                    packetSend[packetSend.Count - 1]++;
                    TrySend("I'm alive :D!");
                    PacketCheker();

                    // �������� ���������� ������
                    foreach (var cts in ctsList)
                    {
                        cts.Cancel();
                    }

                    return true;
                }
                else
                {
                    Debug.Log($"NotConnected");
                }
            }
            catch (OperationCanceledException)
            {
                // ���������� ���������� ������
            }
        }

        Debug.Log($"All Non Connection");
        ws[setWs]?.Close();
        ws.RemoveAt(setWs);

        return false;
    }

    private static async Task<bool> ConnectToServer(string ip, CancellationToken cancellationToken)
    {
        WebSocket ws = new WebSocket($"ws://{ip}:8888/");
        {
            ws.EmitOnPing = true;

            ws.OnOpen += (sender, e) => {
                Debug.Log("Connection: OK!");
            };

            ws.OnMessage += (sender, e) =>
            {
                OnMessage(e);
            };

            ws.OnError += (sender, e) => {
                Debug.LogError("OnErrorMessage says: " + e.Message);
                Debug.LogError("OnErrorException says: " + e.Exception);
            };

            ws.OnClose += (sender, e) => {
                Debug.Log("OnCloseCode Says: " + e.Code);
                Debug.Log("OnCloseReason says: " + e.Reason);
                Debug.Log("Was it closed cleanly?: " + e.WasClean);
            };

            if (ws != null)
            {
                await Task.Run(() => ws.Connect(), cancellationToken);
            }

            bool x = await TryConnect(ws);

            if (x)
            {
                CornSocketSharp.ws.Add(ws);
                Debug.Log(DateTime.Now + " CONNECT " + ip);
            }
            else
            {
                Debug.Log(DateTime.Now + " ERROR " + ip);
            }
            return x;
        }
    }

    private static async void OnMessage(MessageEventArgs e)
    {
        if (e.Data.StartsWith("/cmd"))
        {
            Debug.Log(e.Data);
            switch (e.Data)
            {
                case string data when data.StartsWith("/cmdGetNewPlayer"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetNewPlayer(data.Substring("/cmdGetNewPlayer".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdRemovePlayer"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetRemovePlayer(data.Substring("/cmdRemovePlayer".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetGun"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetGun(data.Substring("/cmdGetGun".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetAnimatorBool"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetAnimatorBoolAsync(data.Substring("/cmdGetAnimatorBool".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetHp"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetHpAsync(data.Substring("/cmdGetHp".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdHitBullet"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetHitBulletAsync(data.Substring("/cmdHitBullet".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdShootBullet"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetShootBulletAsync(data.Substring("/cmdShootBullet".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetPosition"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetPositionAsync(data.Substring("/cmdGetPosition".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetRotation"):
                    packetInvite[packetInvite.Count - 1]++;
                    GetRotationAsync(data.Substring("/cmdGetRotation".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetID"):
                    packetInvite[packetInvite.Count - 1]++;
                    serverReturnID = data.Substring("/cmdGetID".Length).Trim();
                    callbackID[0](serverReturnID);
                    Debug.Log(serverReturnID);
                    break;
                case string data when data.StartsWith("/cmdCpuLoad"):
                    packetInvite[packetInvite.Count - 1]++;
                    Debug.Log(data.Substring("/cmdCpuLoad".Length).Trim());
                    break;
                case string data when data.StartsWith("/cmdGetSoldiers"):
                    packetInvite[packetInvite.Count - 1]++;
                    Debug.Log(data["/cmdGetSoldiers".Length..].Trim());
                    List<string> invStr = (data.Substring("/cmdGetSoldiers".Length).Trim()).Split("   ").ToList();
                    if (invStr.Count == 0 || (invStr.Count == 1 && invStr[0] == ""))
                        break;
                    List<Solder> solders = new();
                    foreach (string str in invStr)
                    {
                        List<string> parms = str.Split("  ").ToList();
                        solders.Add(new Solder(PlayerDatabase.PlayerSqlId, int.Parse(parms[0]), parms[1], parms[2], int.Parse(parms[3]), int.Parse(parms[4]), int.Parse(parms[5]), parms[6]));
                    }
                    soldiersListTask.SetResult(solders);
                    break;
                case string data when data.StartsWith("/cmdSoldierId"):
                    packetInvite[packetInvite.Count - 1]++;
                    int id = int.Parse(data.Substring("/cmdSoldierId".Length).Trim());
                    soldierIdTask.SetResult(id);
                    break;
                default:
                    Debug.LogError("����������� ������� " + e.Data);
                    break;
            }
        }
        else
        {
            Debug.Log(e.Data);
            if (e.IsPing)
            {
                Debug.Log("Received ping!");
                return;
            }
        }
    }



    private static async void PacketCheker()
    {
        if (isPacketChekerOn)
            return;
        isPacketChekerOn = true;
        while (true)
        {
            packetInvite.Add(0);
            if(packetInvite.Count > 100)
                packetInvite.RemoveAt(0);

            packetSend.Add(0);
            if (packetSend.Count > 100)
                packetSend.RemoveAt(0);

            packetNotSend.Add(0);
            if (packetNotSend.Count > 100)
                packetNotSend.RemoveAt(0);

            packetLost.Add(0);
            if (packetLost.Count > 100)
                packetLost.RemoveAt(0);

            await Task.Delay(10);
        }
    }

    public static int GetSummPacketInvite()
    {
        return packetInvite.Sum();
    }
    public static int GetSummPacketSend()
    {
        return packetSend.Sum();
    }
    public static int GetSummPacketNotSend()
    {
        return packetNotSend.Sum();
    }
    public static int GetSummPacketLost() =>  packetLost.Sum();
    
    public static async Task<bool> TryConnect(WebSocket ws)
    {
        await Task.Yield();

        return (ws != null) && ws.IsAlive;
    }

    public static void ApplicationExit()
    {
        if(ws.Count > 0)
            ws[setWs]?.Close();
        Debug.Log("Closing websocket correctly by calling it before exiting application.");
    }



    public static void SetGun(int id)
    {
        string sendStr = "/cmdSetGun " + id.ToString();

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static void SetPosition(Vector3 pos)
    {
        string sendStr = "/cmdSetPosition " + 
            pos.x.ToString() + " " + pos.y.ToString() + " " + pos.z.ToString();
        sendStr = sendStr.Replace(",", ".");

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static void SetShootBullet(BulletType type, Vector3 spawnPoint, Vector3 toMovePoint)
    {
        string sendStr = "/cmdShootBullet " +
            type.ToString() + " " +
            spawnPoint.x + " " + spawnPoint.y + " " + spawnPoint.z + " " +
            toMovePoint.x.ToString() + " " + toMovePoint.y.ToString() + " " + toMovePoint.z.ToString();
        sendStr = sendStr.Replace(",", ".");

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static void SetAnimatorBool(string name, bool isBool)
    {
        string sendStr = "/cmdSetAnimatorBool " + name + " " + isBool.ToString();
        sendStr = sendStr.Replace(",", ".");

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static void SetHitBullet(BulletType type, int playerId)
    {
        string sendStr = "/cmdHitBullet " + type.ToString() + " " + playerId;
        sendStr = sendStr.Replace(",", ".");

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static void SetHp(float hp, int id)
    {
        string sendStr = "/cmdSetHp " + hp.ToString() + " " + id.ToString();
        sendStr = sendStr.Replace(",", ".");

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static void SetRotation(Vector3 localEulerAnglesHead, Vector3 localEulerAnglesBody)
    {
        string sendStr = "/cmdSetRotation " +
            localEulerAnglesHead.x.ToString() + " " + localEulerAnglesHead.y.ToString() + " " + localEulerAnglesHead.z.ToString() + " " +
            localEulerAnglesBody.x.ToString() + " " + localEulerAnglesBody.y.ToString() + " " + localEulerAnglesBody.z.ToString();
        sendStr = sendStr.Replace(",", ".");

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    public static async Task SetNewPlayer(Vector3 pos, Vector3 localEulerAnglesHead, Vector3 localEulerAnglesBody, int gunId)
    {
        SetPosition(pos);
        SetRotation(localEulerAnglesHead, localEulerAnglesBody);
        SetGun(gunId);

        await Task.Delay(500);

        string sendStr = "/cmdSetNewPlayer";

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }



    private static async void TrySend(string sendStr)
    {
        try
        {
            ws[setWs].Send(sendStr);
        }
        catch (Exception)
        {
            packetNotSend[packetNotSend.Count - 1]++;
            if (Ws.Count > 0)
                if (await TryConnect(ws[setWs]))
                    Debug.LogError("������");
                else
                    ws.RemoveAt(setWs);
        }
    }

    public async static Task<string> GetID()
    {
        var tcs = new TaskCompletionSource<string>();

        Action<string> ID;
        callbackID.Add(
        ID = response =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.SetResult(response);
                callbackID.RemoveAt(0);
            }
            else
            {
                tcs.SetResult(null);
            }
        }
        );

        packetSend[packetSend.Count - 1]++;
        TrySend("/sqlGetID");

        await tcs.Task;

        return tcs.Task.Result;
    }



    public static (BulletType, Vector3, Vector3, bool) GetAndDestroyBullet()
    {
        (BulletType, Vector3, Vector3, bool) bullet = bulletsCreator[0];
        bulletsCreator.Remove(bullet);
        return bullet;
    }

    public static (BulletType type, int playerID) GetAndDestroyHit()
    {
        (BulletType type, int playerID) hit = hits[0];
        hits.Remove(hit);
        return hit;
    }

    public static (float hp, int playerID) GetAndDestroyHp()
    {
        (float hp, int playerID) hp = playerHp[0];
        playerHp.Remove(hp);
        return hp;
    }

    public static int GetAndDestroyPlayer()
    {
        int player = PlayerRemove[0];
        PlayerRemove.Remove(player);
        return player;
    }

    public static int GetAndDestroyNewPlayer()
    {
        int player = PlayerNew[0];
        PlayerNew.Remove(player);
        return player;
    }

    public static (int playerId, string name, object parm) GetAndDestroyAnimatorParms()
    {
        (int playerId, string name, object parm) parm = animationParms[0];
        animationParms.Remove(parm);
        return parm;
    }



    private static void GetHpAsync(string strHp)
    {
        if (strHp != null)
        {
            List<string> newHp = CleanStr(strHp);
            float hp = float.Parse(newHp[0]);
            int id = int.Parse(newHp[1]);
            PlayerHp.Add((hp, id));
        }
    }

    private static void GetNewPlayer(string newPlayer)
    {
        if (newPlayer != null)
        {
            List<string> newNewPlayer = CleanStr(newPlayer);

            int id = int.Parse(newNewPlayer[0]);
            playerNew.Add(id);
        }
        else
        {
            Debug.LogError("����� �� �������");
        }
    }

    public static void GetAllPlayer()
    {
        string sendStr = "/cmdGetAllPlayer";

        packetSend[packetSend.Count - 1]++;
        TrySend(sendStr);
    }

    private static void GetRemovePlayer(string removePlayer)
    {
        if (removePlayer != null)
        {
            List<string> newRemovePlayer = CleanStr(removePlayer);

            int id = int.Parse(newRemovePlayer[0]);
            if (playerPoses.ContainsKey(id))
                playerPoses.Remove(id);
            if (gunSet.ContainsKey(id))
                gunSet.Remove(id);
            if (playerRotations.ContainsKey(id))
                playerRotations.Remove(id);
            PlayerRemove.Add(id);
        }
        else
        {
            Debug.LogError("����� �� �������");
        }
    }

    private static void GetGun(string gun)
    {
        if (gun != null)
        {
            List<string> newGuns = CleanStr(gun);

            if (!PlayerPoses.ContainsKey(int.Parse(newGuns[0])))
                gunSet.Add(int.Parse(newGuns[0]), int.Parse(newGuns[1]));
            else
                gunSet[int.Parse(newGuns[0])] = int.Parse(newGuns[1]);
        }
        else
        {
            Debug.LogError("������ �� �������");
        }
    }

    private static void GetAnimatorBoolAsync(string animatorBool)
    {
        if (animatorBool != null)
        {
            List<string> newAnimatorBool = CleanStr(animatorBool);
            int playerId = int.Parse(newAnimatorBool[0]);
            string name = newAnimatorBool[1];
            bool isBool = bool.Parse(newAnimatorBool[2].ToLower());
            animationParms.Add((playerId, name, isBool));
        }
    }

    private static void GetHitBulletAsync(string hit)
    {
        if (hit != null)
        {
            List<string> newHit = CleanStr(hit);
            BulletType type = StringToBulletType(newHit[0]);
            int id = int.Parse(newHit[1]);
            hits.Add((type, id));
        }
    }

    private static void GetShootBulletAsync(string shoot)
    {
        if (shoot != null)
        {
            List<string> newShoot = CleanStr(shoot);
            (BulletType, Vector3, Vector3, bool) bullet = 
                (StringToBulletType(newShoot[1]),
                new Vector3(float.Parse(newShoot[2]), float.Parse(newShoot[3]), float.Parse(newShoot[4])),
                new Vector3(float.Parse(newShoot[5]), float.Parse(newShoot[6]), float.Parse(newShoot[7])),
                false);
            bulletsCreator.Add(bullet);
        }
    }

    private static void GetPositionAsync(string positions)
    {
        if (positions != null)
        {
            List<string> newPositions = CleanStr(positions);

            if (!PlayerPoses.ContainsKey(int.Parse(newPositions[0])))
                PlayerPoses.Add(int.Parse(newPositions[0]), new Vector3(float.Parse(newPositions[1]),
                    float.Parse(newPositions[2]), float.Parse(newPositions[3])));
            else
                PlayerPoses[int.Parse(newPositions[0])] = new Vector3(float.Parse(newPositions[1]),
                        float.Parse(newPositions[2]), float.Parse(newPositions[3]));
        }
        else
        {
            Debug.LogError("������� �� �������");
        }
    }

    private static void GetRotationAsync(string localEulerAngles)
    {
        if (localEulerAngles != null)
        {
            List<string> newRotations = CleanStr(localEulerAngles);

            if (!PlayerRotations.ContainsKey(int.Parse(newRotations[0])))
                PlayerRotations.Add(int.Parse(newRotations[0]), 
                    (new Vector3(float.Parse(newRotations[1]), float.Parse(newRotations[2]), float.Parse(newRotations[3])),
                    new Vector3(float.Parse(newRotations[4]), float.Parse(newRotations[5]), float.Parse(newRotations[6]))));
            else
                PlayerRotations[int.Parse(newRotations[0])] = 
                    (new Vector3(float.Parse(newRotations[1]), float.Parse(newRotations[2]), float.Parse(newRotations[3])),
                    new Vector3(float.Parse(newRotations[4]), float.Parse(newRotations[5]), float.Parse(newRotations[6])));
        }
        else
        {
            Debug.LogError("������� �� �������");
        }
    }



    private static List<string> CleanStr(string str)
    {
        str = str.Replace("<", "");
        str = str.Replace(">", "");
        str = str.Replace("{", "");
        str = str.Replace("}", "");
        str = str.Replace("(", "");
        str = str.Replace(")", "");
        str = str.Replace(",", "");
        str = str.Replace(":", "");
        str = str.Replace(".", ",");
        return (str.Split(" ")).ToList();
    }

    private static BulletType StringToBulletType(string str)
    {
        if (Enum.TryParse(typeof(BulletType), str, true, out object result))
            return (BulletType)result;
        else
            return BulletType.pistol;
    }








    // ��� ����� ����� SQL

    private static async Task<string> TrySendAsyncLogin(string sendStr)
    {
        try
        {
            ws[setWs].Send(sendStr);
            string id = await GetID();
            return id;  // ����� �� ������ ������� ����������� ��������� ��������
        }
        catch (Exception)
        {
            packetNotSend[packetNotSend.Count - 1]++;
            if (Ws.Count > 0)
                if (await TryConnect(ws[setWs]))
                    return "error";
                else
                    ws.RemoveAt(setWs);
        }

        return "error";
    }

    private static async Task<string> TrySendAsyncRegister(string sendStr)
    {
        try
        {
            ws[setWs].Send(sendStr);
            return "success";  // ����� �� ������ ������� ����������� ��������� ��������
        }
        catch (Exception)
        {
            packetNotSend[packetNotSend.Count - 1]++;
            if (Ws.Count > 0)
                if (await TryConnect(ws[setWs]))
                    return "error";
                else
                    ws.RemoveAt(setWs);
        }

        return "error";
    }

    public static async void Login(string username, string password)
    {
        string command = "/sqlLogin " + username + " " + password;

        try
        {
            string response = await TrySendAsyncLogin(command);

            // �������� ������ �� �������
            if (response != "error")
            {
                PlayerDatabase.setId(int.Parse(response));
                PlayerDatabase.setUsername(username);
                Console.WriteLine("Login successful " + response);
                // ��������� �������������� �������� ��� �������� �����
            }
            else
            {
                Console.WriteLine("Login failed: " + response);
                // ��������� �������������� �������� ��� ��������� �����
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }

    public static async void Register(string username, string password)
    {
        string command = "/sqlRegistration " + username + " " + password;

        try
        {
            string response = await TrySendAsyncRegister(command);

            // �������� ������ �� �������
            if (response == "success")
            {
                Console.WriteLine("Registration successful");
                Login(username, password);
                // ��������� �������������� �������� ��� �������� �����������
            }
            else
            {
                Console.WriteLine("Registration failed: " + response);
                // ��������� �������������� �������� ��� ��������� �����������
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }

    public static async Task<int> SendUnitAsSoldierAsync(Solder unit)
    {
        try
        {
            string message = $"/sqlSaveSoldier {unit.playerId}  {unit.unitId}  {unit.name}  {unit.perks}  {unit.weight}  {unit.height}  {unit.level}  {unit.skills}";
            ws[setWs].Send(message);

            // ������� ���������� ������ �� �������
            soldierIdTask = new TaskCompletionSource<int>();
            int newSoldierId = await soldierIdTask.Task;

            return newSoldierId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending unit as soldier: {ex.Message}");
            return -1; // ��� ������ �������� �� ���������
        }
    }

    public static async Task<List<Solder>> GetPlayerSoldiersAsync()
    {
        try
        {
            string message = $"/sqlGetSoldiers {PlayerDatabase.PlayerSqlId}";
            ws[setWs].Send(message);

            // ������� ���������� ������ �� �������
            List<Solder> playerSoldiers = await soldiersListTask.Task;

            return playerSoldiers;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting player soldiers: {ex.Message}");
            return new List<Solder>(); // ��� ������ �������� �� ���������
        }
    }
}