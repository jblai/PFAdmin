﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.AdminModels;
using PlayFab.MultiplayerModels;
using PlayFab.AuthenticationModels;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class PlayerController : MonoBehaviour {
    
    [SerializeField] private BuildBundleID buildID;
    [SerializeField] private GameObject loader;
    [SerializeField] private GameObject segmentButton;
    [SerializeField] private GameObject playerButton;
    [SerializeField] private GameObject containerButton;
    [SerializeField] private GameObject segmentWindow; 
    [SerializeField] private GameObject playerWindow; 
    [SerializeField] private GameObject containerWindow; 
    [SerializeField] private GameObject playerInfoWindow; 
    [SerializeField] private GameObject deleteSelectedButton;
    [SerializeField] private GameObject informModal;
    [SerializeField] private GameObject informTagsModal;
    [SerializeField] private Button informButton;
    [SerializeField] private GameObject informOkButton;
    [SerializeField] private GameObject informUrlButton;
    [SerializeField] private Text informText;
    [SerializeField] private Text informTagsText;
    [SerializeField] private InputField informField;
    [SerializeField] private GameObject informFieldModal;
    private PlayerID lastPlayerIdentifier;
    private bool selectAll = false;
    private int frames = 0;
    private string lastURL = "";
    
    [Header("Player InfoWindow")]
    [SerializeField] private Text displayName;
    [SerializeField] private Text lastLogin;
    [SerializeField] private Text creationDate;
    [SerializeField] private Text bannedUntil;
    [SerializeField] private Text playerID;
    [SerializeField] private Text location;
    [SerializeField] private Text originPlatform;
    [SerializeField] private Text currencies;
    [SerializeField] private Text deletionWarningText;
    [SerializeField] private GameObject deletionWarningModal;
    [SerializeField] private Image mapImage;
    //
    [Header("Ban Window")]
    [SerializeField] private GameObject banModal;    
    [SerializeField] private Text banText;
    [SerializeField] private InputField banReason;
    [SerializeField] private InputField banTimeInHours;
    [Header("Currency Window")]
    [SerializeField] private GameObject currencyModal;    
    [SerializeField] private Text currencyText;
    [SerializeField] private InputField currencyType;
    [SerializeField] private InputField currencyNewValue;
    [Header("Username Window")]
    [SerializeField] private GameObject usernameModal;    
    [SerializeField] private Text usernameText;
    [SerializeField] private InputField usernameNewValue;
    [Header("Login Window")]
    [SerializeField] private GameObject loginWindow;
    [SerializeField] private InputField titleIDField;
    [SerializeField] private InputField secretKeyField;

    void Start() {
        Application.targetFrameRate = 60;

        float height = Screen.currentResolution.height*0.75f;
        Screen.SetResolution((int)(height/1.4f), (int)height, false);

        string titleID = PlayerPrefs.GetString("titleID", null);
        string secretKey = PlayerPrefs.GetString("secretKey", null);

        if ((string.IsNullOrEmpty(titleID) || 
            string.IsNullOrEmpty(secretKey))) {
            loginWindow.SetActive(true);
        } else {
            PlayFabSettings.TitleId = titleID;
            PlayFabSettings.DeveloperSecretKey = secretKey;
            Authenticate();
        }

        if (!PowerShellExists()) {
            Inform("PowerShell not found!");
        }
    }

    void ShowLoader() {
        loader.SetActive(true);
    }

    void HideLoader() {
        loader.SetActive(false);
    }
    string PowerShellDirectory() {
        string psDir = "";
        #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        psDir = "/Applications/PowerShell.app/Contents/MacOS/";
        #elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        psDir = "/usr/bin/"
        #else
        psDir = "C:/WINDOWS/system32/WindowsPowerShell/v1.0/";
        #endif
        
        return psDir;
    }

    string PowerShellPath() {
        string psDir = "";
        #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        psDir = "PowerShell.sh";
        #elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        psDir = "PowerShell.sh"
        #else
        psDir = "powershell.exe";
        #endif
        return PowerShellDirectory() + psDir;
    }

    bool PowerShellExists() {
        return Directory.Exists(PowerShellDirectory());        
    }

    public void LoginWithTitleID() {
        bool setupOK = false;
        if (!string.IsNullOrEmpty(titleIDField.text)) {
            PlayFabSettings.TitleId = titleIDField.text;
            setupOK = true;            
        } else {
            Inform("Title ID cannot be empty!");
            setupOK = false;
        }

        if (!string.IsNullOrEmpty(secretKeyField.text)) {
            PlayFabSettings.DeveloperSecretKey = secretKeyField.text;
            setupOK = true;
        } else {
            Inform("Developer Key cannot be empty!");
            setupOK = false;
        }

        if (setupOK) {
            PlayerPrefs.SetString("titleID", PlayFabSettings.TitleId);
            PlayerPrefs.SetString("secretKey", PlayFabSettings.DeveloperSecretKey);
            Authenticate();
        }
    }

    public void Logout() {
        PlayFabSettings.TitleId = "";
        PlayFabSettings.DeveloperSecretKey = "";
        PlayerPrefs.DeleteAll();
        Application.Quit();
    }

    public void Authenticate() {
        ShowLoader();
        var request = new GetEntityTokenRequest();
        PlayFabAuthenticationAPI.GetEntityToken(request, 
        result => {
            Debug.Log("GOT TOKEN OK: " + result.ToJson().ToString());
            Inform ("Authentication Success!\n\nExpires " + result.TokenExpiration.Value.ToLocalTime().ToString());
            HideLoader();
            loginWindow.SetActive(false);
        }, error => {
            HideLoader();
            loginWindow.SetActive(true);
            Inform("GET TOKEN FAILED: " + error.ErrorMessage);
            Debug.LogError("GET TOKEN FAILED: " + error.ToString());
        });
    }

    public void ListContainerImages() {
        ShowLoader();
        PlayFabMultiplayerAPI.ListContainerImages(new ListContainerImagesRequest(),
        result => {
            HideLoader();
            Debug.Log ("GOT IMAGES OK: " + result.ToJson().ToString());
            foreach (var container in result.Images) {
                GameObject newContainerButton = Instantiate(containerButton, 
                                                Vector3.zero, Quaternion.identity, 
                                                containerButton.transform.parent) as GameObject;

                ContainerID identity = newContainerButton.GetComponent<ContainerID>();
                identity.containerName = container;
                identity.text.text = container;
                newContainerButton.SetActive(true);
            }
            containerWindow.SetActive(true);
        },
        error => {
            HideLoader();
            Inform("GET IMAGES FAILED: " + error.ErrorMessage);
            Debug.LogError ("GET IMAGES FAILED: " + error.ToString());
        });
    }

    public void GetContainerTags(ContainerID identity) {
        ShowLoader();
        PlayFabMultiplayerAPI.ListContainerImageTags(new ListContainerImageTagsRequest{
            ImageName = identity.containerName
        },
        result => {
            HideLoader();
            string tags = "";
            foreach (var tag in result.Tags) {
                tags += "\n" + tag;
                if (tag.Equals("latest")) {
                    identity.containerTag = tag;
                }
            }
            InformTags(string.Format("Tags for '<b>{0}</b>':\n{1}", identity.containerName, tags), identity);
            Debug.Log ("GOT TAGS OK: " + result.ToJson());
        },
        error => {
            HideLoader();
            Debug.LogError("GET TAGS FAILED: " + error.ToString());
            Inform("Failed to retrieve information for the container!\n\n" + error.ErrorMessage);
        });
    }

    private T GetEnumValue<T>(string str) where T : struct {   
        try  {   
            T res = (T)System.Enum.Parse(typeof(T), str);   
            if (!System.Enum.IsDefined(typeof(T), res)) return default(T);   
            return res;   
        } catch {   
            return default(T);   
        }   
    }   

    public void CreateBuildWithCustomContainer(BuildBundleID identity) {
        ShowLoader();
        try {
            PlayFabMultiplayerAPI.CreateBuildWithCustomContainer(new CreateBuildWithCustomContainerRequest{
                BuildName = identity.buildName.text,
                ContainerFlavor = GetEnumValue<ContainerFlavor>(identity.containerFlavor.options[identity.containerFlavor.value].text),
                ContainerImageReference = new ContainerImageReference{
                    ImageName = identity.containerName.text,
                    Tag = identity.containerTag.text
                },
                ContainerRunCommand = "echo \"Server is being allocated...\" >> /data/GameLogs/Server.log",
                MultiplayerServerCountPerVm = int.Parse(identity.serverCountPerVm.text),
                Ports = new List<Port> {
                    new Port {
                        Name = "game", 
                        Num = int.Parse(identity.portNumber.text), 
                        Protocol = GetEnumValue<ProtocolType>(
                            identity.portProtocol.options[identity.portProtocol.value].text
                        )
                    }
                },
                RegionConfigurations = new List<BuildRegionParams> {
                    new BuildRegionParams{
                        Region = GetEnumValue<AzureRegion>(identity.region.options[identity.region.value].text), 
                        MaxServers = int.Parse(identity.maxServers.text), 
                        StandbyServers = int.Parse(identity.standByServers.text)
                    }
                },
                VmSize = GetEnumValue<AzureVmSize>(identity.vmSize.options[identity.vmSize.value].text)
            }, 
            result => {
                Debug.Log ("CREATE BUILD OK: " + result.ToJson());
                buildID.gameObject.SetActive(false);
                InformURL("Build Created Successfully!\n\nBuild ID:\n" + result.BuildId, 
                string.Format("https://developer.playfab.com/en-US/{0}/multiplayer/server/builds", PlayFabSettings.TitleId));
            },
            error => {
                Debug.LogError ("CREATE BUILD FAILURE: " + error.ToString());
                Inform("Build Creation Failure!\n\n" + error.ErrorMessage);
            });
        } catch (System.Exception e) {
            Inform (e.Message);
        }
    }

    void RunPowershellSetup(string dockerPath) {
        if (!PowerShellExists()) {
            Inform ("Unable to launch: PowerShell not detected!");
            return;
        }

        var ps1File = dockerPath + "DockerCommands.ps1";
        if (Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.OSXPlayer) {
            var startInfo = new ProcessStartInfo() {
                FileName = PowerShellPath(),
                Arguments = $"-NoProfile -NoExit -ExecutionPolicy unrestricted \"{ps1File}\"",
                UseShellExecute = false
            };

            Process.Start(startInfo);
        } else {
            StartCoroutine(WaitForPSMac(ps1File));
        }
    }

    public void GetContainerCredentialsWithToken() {
        ShowLoader();
        PlayFabMultiplayerAPI.GetContainerRegistryCredentials(new GetContainerRegistryCredentialsRequest(),
        result => {
            Debug.Log ("GOT CREDS OK: " + result.ToJson().ToString());
            HideLoader();

            Inform ("Ready to build container!");
            string dockerPath = Application.dataPath + 
            (Application.platform == RuntimePlatform.OSXPlayer ? "/.." : "") + "/../Docker/"; 
            string path = dockerPath + "Credentials";
            string credentials = string.Format("Repo={0}\n" + 
                                               "User={1}\n" +
                                               "Pass={2}", 
                                               result.DnsName,
                                               result.Username,
                                               result.Password);
            try {
                File.WriteAllText(path, credentials);
                RunPowershellSetup(dockerPath);
            } catch (System.Exception e) {
                Inform (e.Message);
            }
        },
        error => {
            Debug.LogError ("GET CREDS FAILED: " + error.ToString());
            HideLoader();
            Inform("GET CREDENTIALS FAILED: " + error.ErrorMessage);
        });
    }


    public void GetSegments() {
        ShowLoader();
        PlayFabAdminAPI.GetAllSegments(new GetAllSegmentsRequest(), 
        result => {
            Debug.Log ("GOT ALL SEGMENTS OK: " + result.ToJson().ToString());
            foreach (var segment in result.Segments) {
                GameObject newSegmentButton = Instantiate(segmentButton, Vector3.zero, Quaternion.identity, segmentButton.transform.parent) as GameObject;
                SegmentID identity = newSegmentButton.GetComponent<SegmentID>();
                identity.segmentID = segment.Id;
                identity.nameText.text = segment.Name;
                newSegmentButton.SetActive(true);
            }
            segmentWindow.SetActive(true);
            HideLoader();
        }, 
        error => {
            HideLoader();
            Debug.LogError("ERROR GETTING SEGMENTS: " + error.GenerateErrorReport());
            Inform(error.ErrorMessage);
        });
    }

    public void GetServers() {
        ShowLoader();
        PlayFabAdminAPI.ListServerBuilds(new ListBuildsRequest(),
        result => {
            Debug.Log ("GOT BUILDS OK: " + result.ToJson().ToString());
            HideLoader();
            if (result.Builds.Count > 0) {
                //
            } else {
                Inform ("No servers were found.");
            }
        },
        error => {
            Debug.LogError ("ERROR GETTING BUILDS: " + error.GenerateErrorReport());
            HideLoader();
            Inform(error.ErrorMessage);
        });
    }

    public void SelectSegment(SegmentID id) {
        ShowLoader();
        PlayFabAdminAPI.GetPlayersInSegment(new GetPlayersInSegmentRequest{
            SegmentId = id.segmentID
        }, 
        result => {
            Debug.Log ("GOT SEGMENT PLAYERS OK: " + result.ToJson().ToString());
            foreach (var playerProfile in result.PlayerProfiles) {
                GameObject newPlayerButton = Instantiate(playerButton, 
                                             Vector3.zero, Quaternion.identity, 
                                             playerButton.transform.parent) as GameObject;

                PlayerID identity = newPlayerButton.GetComponent<PlayerID>();
                identity.avatarURL = playerProfile.AvatarUrl;
                if (playerProfile.BannedUntil.HasValue) {
                    identity.bannedUntil = playerProfile.BannedUntil.Value.ToLocalTime().ToString();
                } else {
                    identity.bannedUntil = "N/A";
                }
                identity.creationDate = playerProfile.Created.Value.ToLocalTime().ToString();
                identity.lastLogin = playerProfile.LastLogin.Value.ToLocalTime().ToString();
                identity.displayName = playerProfile.DisplayName;
                foreach (var location in playerProfile.Locations) {
                    identity.location = location.Value.City + ", " + 
                                        location.Value.CountryCode;
                    identity.latLong = location.Value.Latitude + "," +
                                        location.Value.Longitude;
                }
                string currencies = "";
                foreach (var currency in playerProfile.VirtualCurrencyBalances) {
                    currencies += "\n" + currency.Key + ": " + currency.Value;
                }
                identity.currencies = currencies;
                identity.originPlatform = playerProfile.Origination.Value.ToString();
                identity.playerID = playerProfile.PlayerId;
                identity.nameText.text = playerProfile.DisplayName;
                newPlayerButton.SetActive(true);
            }
            DestroySegmentWindow();
            playerWindow.SetActive(true);
            HideLoader();
        }, 
        error => {
            HideLoader();
            Debug.LogError("ERROR GETTING SEGMENT PLAYERS: " + error.GenerateErrorReport());
            Inform(error.ErrorMessage);
        });
    } 

    public void DestroySegmentWindow() {
        for (int i = 0; i < segmentButton.transform.parent.childCount; i++) {
            Transform child = segmentButton.transform.parent.GetChild(i);
            if (child.gameObject.activeSelf) {
                Destroy(child.gameObject);
            }
        }
        segmentWindow.SetActive(false);
    }

    public void DestroyPlayerWindow() {
        for (int i = 0; i < playerButton.transform.parent.childCount; i++) {
            Transform child = playerButton.transform.parent.GetChild(i);
            if (child.gameObject.activeSelf) {
                Destroy(child.gameObject);
            }
        }
        playerWindow.SetActive(false);
    }

    public void DestroyContainerWindow() {
        for (int i = 0; i < containerButton.transform.parent.childCount; i++) {
            Transform child = containerButton.transform.parent.GetChild(i);
            if (child.gameObject.activeSelf) {
                Destroy(child.gameObject);
            }
        }
        containerWindow.SetActive(false);
    }

    IEnumerator WaitForPSMac(string ps1Location) {
        ProcessStartInfo startInfo = new ProcessStartInfo("osascript", 
        "-e 'tell application \"Terminal\"' -e 'do script \"pwsh -f " + ps1Location + "\"' -e 'end tell'");

        startInfo.CreateNoWindow = true;
        startInfo.UseShellExecute = true;
 
        Process process = new Process();
        process.StartInfo = startInfo;
        process.Start();
       
        process.WaitForExit();
        yield return null;
    }

    void Update() {
        frames++;
        
        if (frames % 30 == 0) {
            Transform parent = playerButton.transform.parent;
            int numDisabled = 0;
            for (int i = 0; i < parent.childCount; i++) {
                if (parent.GetChild(i).GetComponentInChildren<Toggle>().isOn) {
                    if (!deleteSelectedButton.activeSelf) {
                        deleteSelectedButton.SetActive(true);
                    }
                } else {
                    numDisabled++;
                }
            }
            if (deleteSelectedButton.activeSelf && numDisabled == parent.childCount) {
                deleteSelectedButton.SetActive(false);
            }
            frames = 0;
        }
    }

    public void DeleteSelection() {
        Transform parent = playerButton.transform.parent;
        int numToggled = 0;
        for (int i = 0; i < parent.childCount; i++) {
            if (parent.GetChild(i).GetComponentInChildren<Toggle>().isOn) {
                numToggled++;
            }
        }
        deletionWarningText.text = "Are you sure you want to <i>PERMANENTLY</i> delete " + numToggled + " players?";
        deletionWarningModal.SetActive(true);
    }

    public void ConfirmDeleteSelected() {
        ShowLoader();
        Transform parent = playerButton.transform.parent;
        for (int i = 0; i < parent.childCount; i++) {
            if (parent.GetChild(i).GetComponentInChildren<Toggle>().isOn) {
                DeletePlayerImmediate(parent.GetChild(i).GetComponent<PlayerID>());
            }
        }
        playerInfoWindow.SetActive(false);
        deletionWarningModal.SetActive(false);
        HideLoader();
    }

    public void ToggleSelectAll() {
        selectAll = !selectAll;
        Transform parent = playerButton.transform.parent;
        for (int i = 0; i < parent.childCount; i++) {
            parent.GetChild(i).GetComponentInChildren<Toggle>().isOn = selectAll;
        }
    }

    public void DeletePlayerImmediate(PlayerID playerID) {
        Debug.LogFormat ("Call to delete player: {0} with ID {1}", playerID.nameText.text, playerID.playerID);
        PlayFabAdminAPI.DeleteMasterPlayerAccount(new DeleteMasterPlayerAccountRequest{
            PlayFabId = playerID.playerID
        },
        result => {
            Debug.Log ("DELETE PLAYER OK: " + result.ToJson().ToString());
            Destroy (playerID.gameObject);
        },
        error => {
            Debug.LogError ("DELETE PLAYER ERROR: " + error.GenerateErrorReport());
            Inform(string.Format("Unable to delete {0}! {1}", playerID.displayName, error.ErrorMessage));
        });
    }

    public void DeleteUser() {
        ShowLoader();
        Transform parent = playerButton.transform.parent;
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.GetComponent<PlayerID>().playerID.Equals(lastPlayerIdentifier.playerID)) {
                child.GetComponentInChildren<Toggle>().isOn = true;
            } else {
                child.GetComponentInChildren<Toggle>().isOn = false;
            }
        }
        HideLoader();
        DeleteSelection();
    }

    public void BanUser() {
        banText.text = "Add Ban for User: \"" + lastPlayerIdentifier.displayName + "\"";
        banModal.SetActive(true);
    }

    public void ConfirmBanUser() {
        var request = new BanRequest();
        try {
            if (!string.IsNullOrEmpty(banTimeInHours.text)) {
                request.DurationInHours = uint.Parse(banTimeInHours.text);
            }
        } catch (System.Exception e) {
            request.DurationInHours = 0;
        }
        request.Reason = banReason.text;
        request.PlayFabId = lastPlayerIdentifier.playerID;
        var banList = new List<BanRequest>();
        banList.Add(request);
        PlayFabAdminAPI.BanUsers(new BanUsersRequest{
            Bans = banList
        }, 
        result => {
            banModal.SetActive(false);
            Debug.Log ("BAN USER OK: " + result.ToJson().ToString());
            Inform(string.Format("{0} was successfully banned for {1} hours for \"{2}\"", 
                                lastPlayerIdentifier.displayName, banTimeInHours.text, banReason.text));
        },
        error => {
            Debug.LogError("BAN PLAYER FAILED: " + error.ToString());
            Inform("Unable to ban user! " + error.ErrorMessage);
            banModal.SetActive(false);
        });
    }

    public void ModifyUserTitleName() {
        usernameText.text = string.Format("Change Display Name for {0}", lastPlayerIdentifier.displayName);
        usernameModal.SetActive(true);
    }

    public void ConfirmModifyUserTitleName() {
        if (usernameNewValue.text.ToCharArray().Length < 4) {
            Inform ("Username too short!");
            return;
        } else {
            PlayFabAdminAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest{
                PlayFabId = lastPlayerIdentifier.playerID,
                DisplayName = usernameNewValue.text
            },
            result => {
                Debug.Log ("UPDATE DISPLAY NAME OK: " + result.ToJson().ToString());
                Inform (string.Format("Successfully renamed {0} to {1}", 
                                    lastPlayerIdentifier.displayName, 
                                    usernameNewValue.text));
            },
            error => {
                Debug.LogError("UPDATE NAME FAILURE: " + error.ToString());
                Inform("Unable to update name! " + error.ErrorMessage);
            });
        }
    }

    public void ModifyCurrency() {
        currencyText.text = string.Format("Modify Currency for {0}\n\nCurrent Values:{1}", 
                            lastPlayerIdentifier.displayName, 
                            lastPlayerIdentifier.currencies);

        currencyModal.SetActive(true);
    }

    public void ConfirmModifyCurrency(bool add) {
        if (!string.IsNullOrEmpty(currencyType.text) && !string.IsNullOrEmpty(currencyNewValue.text)) {
            int newCurrencyValue = int.Parse(currencyNewValue.text);

            if (add) {
                PlayFabAdminAPI.AddUserVirtualCurrency(new AddUserVirtualCurrencyRequest{
                    PlayFabId = lastPlayerIdentifier.playerID,
                    VirtualCurrency = currencyType.text,
                    Amount = newCurrencyValue
                },
                result => {
                    Debug.Log ("ADD CURRENCY OK: " + result.ToJson().ToString());
                    Inform (string.Format("Modified Currency for {0}\n\nLast Value: {1}\n\nNew Value: {2}",
                                            lastPlayerIdentifier.displayName, lastPlayerIdentifier.currencies, 
                                            result.Balance + " " + currencyType.text));
                },
                error => {
                    Debug.LogError("ADD CURRENCY FAILURE: " + error.ToString());
                    Inform ("Add Currency Failed! " + error.ErrorMessage);
                });
            } else {
                PlayFabAdminAPI.SubtractUserVirtualCurrency(new SubtractUserVirtualCurrencyRequest{
                    PlayFabId = lastPlayerIdentifier.playerID,
                    VirtualCurrency = currencyType.text,
                    Amount = newCurrencyValue
                },
                result => {
                    Debug.Log ("REMOVE CURRENCY OK: " + result.ToJson().ToString());
                    Inform (string.Format("Modified Currency for {0}\n\nLast Value: {1}\n\nNew Value: {2}",
                                            lastPlayerIdentifier.displayName, lastPlayerIdentifier.currencies, 
                                            result.Balance + " " + currencyType.text));
                },
                error => {
                    Debug.LogError("REMOVE CURRENCY FAILURE: " + error.ToString());
                    Inform ("REMOVE Currency Failed! " + error.ErrorMessage);
                });
            }
        } else {
            Inform ("Fields cannot be empty!");
        }
    }

    public void GetUserInventory() {
        ShowLoader();
        PlayerID playerIdentifier = lastPlayerIdentifier;
        PlayFabAdminAPI.GetUserInventory(new GetUserInventoryRequest{
            PlayFabId = playerIdentifier.playerID
        },
        result => {
            HideLoader();
            Debug.Log ("GOT INVENTORY OK: " + result.ToJson());
            string items = "";
            int itemCount = 0;
            foreach (var item in result.Inventory) {
                itemCount++;
                items += string.Format("\n{0}. {1}", itemCount, item.DisplayName);
            }
            Inform(string.Format("\"{0}'s\" inventory:\n{1}", lastPlayerIdentifier.displayName, items));
        },
        error => {
            HideLoader();
            Debug.LogError ("GET INVENTORY FAILED: " + error.ToString());
            Inform ("Unable to get inventory for \"" + playerIdentifier.displayName + "\"!\n\n" + error.ErrorMessage);
        });
    }

    public void Inventory() {
        
    }

    public void ViewPlayer(PlayerID playerIdentifier) {
        lastPlayerIdentifier = playerIdentifier;
        displayName.text = "<b>\"" + playerIdentifier.displayName + "\"</b>";
        creationDate.text = "<b>Creation Date</b>: " + playerIdentifier.creationDate;
        lastLogin.text = "<b>Last Login</b>: " + playerIdentifier.lastLogin;
        location.text = "<b>Location</b>: " + playerIdentifier.location;
        originPlatform.text = "<b>Origin Platform</b>: " + playerIdentifier.originPlatform;
        currencies.text = "<b>Currencies</b>: " + playerIdentifier.currencies;
        bannedUntil.text = "<b>Banned Until</b>: <color=red>" + playerIdentifier.bannedUntil + "</color>";
        playerID.text = "<b>PlayFab ID</b>: " + playerIdentifier.playerID;
        StartCoroutine(GetMapImage(playerIdentifier.latLong));
        playerInfoWindow.SetActive(true);
    }

    IEnumerator GetMapImage(string latLong) {
        string apiKey = "UPdUOwnBZdPmID280PvZiwyQWkALryyR";
        string defaultSize = "425,500";
        string url = string.Format("https://open.mapquestapi.com/staticmap/v4/getmap?key={0}&size={1}&zoom=10&center={2}&mcenter={2}", apiKey, defaultSize, latLong);
        WWW www = new WWW(url);
        yield return www;
        if (www.isDone) {
            mapImage.sprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height), new Vector2(0, 0));
            mapImage.preserveAspect = true;
        }
    }

    public void InformTags(string message, ContainerID identity) {
        HideLoader();
        informTagsText.text = message;
        informTagsModal.GetComponent<ContainerID>().containerName = identity.containerName;
        informTagsModal.GetComponent<ContainerID>().containerTag = identity.containerTag;
        informTagsModal.SetActive(true);
    }

    public void CreateNewBuild(ContainerID identity) {
        string containerName = identity.containerName;
        string containerTag = identity.containerTag;
        buildID.containerName.text = containerName;
        buildID.containerTag.text = containerTag;
        buildID.gameObject.SetActive(true);
    }

    public void GoUrl() {
        if (!string.IsNullOrEmpty(lastURL)) {
            Application.OpenURL(lastURL);
        }
    }

    public void InformURL(string message, string URL) {
        HideLoader();
        lastURL = URL;
        informText.text = message;
        informUrlButton.SetActive(true);
        informOkButton.SetActive(false);
        informModal.SetActive(true);
    }

    public void Inform(string message) {
        HideLoader();
        Debug.LogWarning(message);
        informText.text = message;
        informUrlButton.SetActive(false);
        informOkButton.SetActive(true);
        informModal.SetActive(true);
    }

    public void InformField(string message) {
        HideLoader();
        informField.text = message;
        informFieldModal.SetActive(true);
    }

}