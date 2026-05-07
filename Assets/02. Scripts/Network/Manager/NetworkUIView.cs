using UnityEngine;
using UnityEngine.UI;
using R3;

namespace TankAttack.Network.Manager
{
    public class NetworkUIView : MonoBehaviour
    {
        [Header("Network Settings")]
        public string serverIP = "127.0.0.1";
        public int serverPort = 7777;
        public int heartbeatInterval = 5;

        [Header("UI Settings")]
        [SerializeField] private Button connectButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button exitButton;

        [Header("Player Settings")]
        public GameObject playerPrefab;
        
        [Header("Item Settings")]
        public GameObject speedItemPrefab;
        public GameObject healItemPrefab;
        public GameObject powerItemPrefab;

        [Header("UI Settings")] 
        public RectTransform globalCanvasRect;
        public GameObject hpBarPrefab;
        public GameObject damageTextPrefab;
        
        public Observable<Unit> OnConnectClicked => connectButton.OnClickAsObservable();
        public Observable<Unit> OnJoinClicked => joinButton.OnClickAsObservable();
        public Observable<Unit> OnExitClicked => exitButton.OnClickAsObservable();
        
        public void SetButtonStates(bool canConnect, bool canJoin, bool canExit)
        {
            connectButton.interactable = canConnect;
            joinButton.interactable = canJoin;
            exitButton.interactable = canExit;
        }
        
    }
}