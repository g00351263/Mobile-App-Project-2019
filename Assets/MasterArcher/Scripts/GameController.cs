using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour {

	/// <summary>
	/// Main game controller class.
	/// Game controller is responsible for assigning turns to player and opponent (thus making this game a turn-based one!), 
	/// setting ground types (to curved or flat types), managing UI elements including health bars and info panels, managing player inputs (on UI), 
	/// checking for gameover events and post gameover settings.
	/// </summary>

	//[Header("Background Type")]
	public enum groundTypes { flat, curved }
	public groundTypes groundType = groundTypes.flat;	//we have two options here. default is flat ground.
	public GameObject flatBg;							//reference to flag ground object.
	public GameObject curvedBg;							//reference to curved ground holder object.


	// Static variables //
	public static int gameMode;						//current game mode
	public static bool isArrowInScene;				//have any body shot an arrow? (is there an arrow inside the game?)
	public static bool gameIsStarted;				//global flag for game start state
	public static bool gameIsFinished;				//global flag for game finish state
	public static bool noMoreShooting;				//We use this to stop the shoots when someone has been killed but the game is yet to finish
	public static int round;						//internal counter to assign turn to player and AI
	public static bool playersTurn;					//flag to check if this is player's turn
	public static bool enemysTurn;					//flag to check if this is opponent's turn
	public static string whosTurn;					//current turn holder in string. useful if you want to extend the game.
	public static int playerCoins;					//available player coins
	public static int playerArrowShot;				//how many arrows player shot in this game
	// Static variables //

	// Private vars //
	private bool canTap;
	private GameObject AdManagerObject;

	[Header("AudioClips")]
	public AudioClip tapSfx;
	public AudioClip endSfx;


	[Header("Public GameObjects")]	
	//Reference to scene game objects		
	public GameObject uiPlayerHealthBar;			//Names are self-explanatory
	public GameObject uiEnemyHealthBar;
	public GameObject uiEnemyInfoPanel;
	public GameObject uiEnemyDistance;
	private GameObject player;
	private GameObject enemy;
	private GameObject cam;
	private GameObject uiCam;
	public GameObject gameoverManager;
	public GameObject uiGameStateLabel;
	public GameObject uiYouWon;

	[Header("BirdHunt Game Mode settings")]	
	public GameObject uiBirdhuntStatPanel;
	public GameObject uiTimeText;
	public GameObject uiBirdsHitText;
	public GameObject uiStatBirdHits;
	public GameObject uiStatBestScore;
	public static int birdsHit;
	///Game timer vars
	public int availableTime = 60;					//total gameplay time
	public static int bonusTime = 3;						//additional time when we hit a bird
	public static float gameTimer; 				
	private string remainingTime;
	private int seconds;
	private int minutes;

	private Vector3 playerHBSC;						//player health bar starting scale
	private Vector3 enemyHBSC;						//enemy health bar starting scale
	private float playerHealthScale;				//player health bar real-time scale
	private float enemyHealthScale;					//enemy health bar real-time scale



	/// <summary>
	/// INIT
	/// </summary>
	void Awake () {

		//get game mode
		gameMode = PlayerPrefs.GetInt ("GAMEMODE");

		// JUST TO PREVENT BUGS WHEN LOADING GAME MODES DIRECTLY FROM EDITOR
		// -- REMEMBER: You need to always start the game from the menu or init scenes --
		if (SceneManager.GetActiveScene ().name == "Game" && gameMode == 2) {
			//This is bad
			print("You need to run this game from menu scene.");
			SceneManager.LoadScene ("Menu");
		}

		if (SceneManager.GetActiveScene ().name == "BirdHunt" && gameMode == 1) {
			//This is bad
			print("You need to run this game from menu scene.");
			SceneManager.LoadScene ("Menu");

		}

		//set ground type with high priority
		switch (groundType) {
		case groundTypes.flat:
			flatBg.SetActive (true);
			curvedBg.SetActive (false);
			break;
		case groundTypes.curved:
			flatBg.SetActive (false);
			curvedBg.SetActive (true);
			break;
		}

		//cache main objects
		player = GameObject.FindGameObjectWithTag ("Player");
		enemy = GameObject.FindGameObjectWithTag ("enemy");
		cam = GameObject.FindGameObjectWithTag ("MainCamera");
		uiCam = GameObject.FindGameObjectWithTag ("uiCamera");

		gameoverManager.SetActive (false);

		if(uiBirdhuntStatPanel)
			uiBirdhuntStatPanel.SetActive (false);

		isArrowInScene = false;
		playerHBSC = uiPlayerHealthBar.transform.localScale;
		enemyHBSC = uiEnemyHealthBar.transform.localScale;

		canTap = false;
		gameIsStarted = false;
		gameIsFinished = false;
		noMoreShooting = false;
		round = 0;
		playerArrowShot = 0;
		playerCoins = 0;

		gameTimer = availableTime;
		seconds = 0;
		minutes = 0;
		birdsHit = 0;

		AdManagerObject = GameObject.FindGameObjectWithTag("AdManager");
	}



	void Start () {

		StartCoroutine(activateTap ());
		StartCoroutine(roundTurnManager ());

	}


	/// <summary>
	/// FSM
	/// </summary>
	void Update () {

		//receive inputs at all times
		StartCoroutine(inputManager ());

		//manage health bar status
		updateUiHealthBars ();

		//manage enemy distance info on the UI
		updateUiEnemyInfoPanel ();

		//Manage game timer only in birdhunt mode
		if(gameMode == 2)
			manageGameTimer();

		//we no longer need to loop into gameController if the game is already finished.
		if (gameIsFinished)
			return;
		
		//fast game finish checking...
		if (EnemyController.isEnemyDead) {
			//player is winner
			StartCoroutine(finishTheGame (1));
		} else if (PlayerController.isPlayerDead) {
			//we have lost
			StartCoroutine(finishTheGame (0));
		}


		// DEBUG COMMANDS //
		//Force restart
		if (Input.GetKey (KeyCode.R)) {
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}
		//Fake damage to player
		if (Input.GetKeyUp (KeyCode.D)) {
			player.GetComponent<PlayerController> ().playerCurrentHealth -= 10;
		}
		//Fake damage to enemy
		if (Input.GetKeyUp (KeyCode.E)) {
			enemy.GetComponent<EnemyController> ().enemyCurrentHealth -= 10;
		}
	}
		

	//*****************************************************************************
	// This function monitors player touches on menu buttons.
	// detects both touch and clicks and can be used with editor, handheld device and 
	// every other platforms at once.
	//*****************************************************************************
	private RaycastHit hitInfo;
	private Ray ray;
	IEnumerator inputManager (){

		//Prevent double click
		if (!canTap)
			yield break;

		//Mouse of touch?
		if(	Input.touches.Length > 0 && Input.touches[0].phase == TouchPhase.Ended)  
			ray = Camera.main.ScreenPointToRay(Input.touches[0].position);
		else if(Input.GetMouseButtonUp(0))
			ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		else
			yield break;

		if (Physics.Raycast(ray, out hitInfo)) {
			GameObject objectHit = hitInfo.transform.gameObject;
			//print ("objectHit: " + objectHit.name);
			switch(objectHit.name) {

			case "Button-Play":
				playSfx(tapSfx);							//play touch sound
				canTap = false;								//prevent double touch
				StartCoroutine(animateButton(objectHit));	//touch animation effect
				yield return new WaitForSeconds(0.25f);		//Wait for the animation to end
				SceneManager.LoadScene ("Menu");
				StartCoroutine(activateTap());
				break;
			case "Button-Menu":
				playSfx(tapSfx);							//play touch sound
				canTap = false;								//prevent double touch
				StartCoroutine(animateButton(objectHit));	//touch animation effect
				yield return new WaitForSeconds(0.25f);		//Wait for the animation to end
				SceneManager.LoadScene ("Menu");
				StartCoroutine(activateTap());
				break;
			case "Button-Retry":
				playSfx(tapSfx);							//play touch sound
				canTap = false;								//prevent double touch
				StartCoroutine(animateButton(objectHit));	//touch animation effect
				yield return new WaitForSeconds(0.25f);		//Wait for the animation to end
				SceneManager.LoadScene (SceneManager.GetActiveScene().name);
				StartCoroutine(activateTap());
				break;
			}	
		}
	}
		

	/// <summary>
	/// shows the distance of player and enemy on the UI
	/// </summary>
	void updateUiEnemyInfoPanel() {

		//only update UI if this game mode requires an enemy
		if (!GameModeController.isEnemyRequired ()) {
			return;
		}

		if (playersTurn) {
			
			uiEnemyInfoPanel.SetActive (true);
			float enemyDistance = Vector3.Distance (player.transform.position, enemy.transform.position);
			uiEnemyDistance.GetComponent<TextMesh> ().text = ((int)enemyDistance).ToString() + "m";

			uiEnemyInfoPanel.transform.position = new Vector3 (uiEnemyInfoPanel.transform.position.x, 
																enemy.transform.position.y,
																uiEnemyInfoPanel.transform.position.z);

		} else {

			uiEnemyInfoPanel.SetActive (false);

		}

	}


	/// <summary>
	/// Updates the user interface health bars based on available health for each side.
	/// </summary>
	void updateUiHealthBars() {

		//only update UI if this game mode requires an enemy
		if (!GameModeController.isEnemyRequired ()) {
			return;
		}

		playerHealthScale = (player.GetComponent<PlayerController> ().playerCurrentHealth * playerHBSC.x) / player.GetComponent<PlayerController> ().playerHealth;
		enemyHealthScale = (enemy.GetComponent<EnemyController> ().enemyCurrentHealth * enemyHBSC.x) / enemy.GetComponent<EnemyController> ().enemyHealth;

		uiPlayerHealthBar.transform.localScale = new Vector3(playerHealthScale, playerHBSC.y, playerHBSC.z);
		uiEnemyHealthBar.transform.localScale = new Vector3(enemyHealthScale, enemyHBSC.y, enemyHBSC.z);

	}


	/// <summary>
	/// Assign turns to player and AI.
	/// </summary>
	public IEnumerator roundTurnManager() {

		//1. first check if the game is already finished
		if (gameIsFinished) {
			yield break;
		}

		//2. then check if the situation meets a game over
		//check for game finish state
		if (EnemyController.isEnemyDead) {

			//player is winner
			StartCoroutine(finishTheGame (1));
			yield break;

		} else if (PlayerController.isPlayerDead) {

			//we have lost
			StartCoroutine(finishTheGame (0));
			yield break;

		}

		//3. if none of the above is true, continue with the turn-change...

		round++; 	//game starts with round 1
		print("Round: " + round);

		//if round carry is odd, its players turn, otherwise its opponent's turn
		int carry;
		carry = round % 2;

		if(carry == 1) {
			
			playersTurn = true;
			enemysTurn = false;
			whosTurn = "Player";

			yield return new WaitForSeconds(0.9f);

			//just incase we need to show the camera's starting animation, we do not need to switch to player, so we just leave the function
			if (!cam.GetComponent<CameraController> ().startMoveIsDoneFlag)
				yield break;

			//reset camera's old target
			cam.GetComponent<CameraController>().targetToFollow = null;
			//tell the camera to go to player position
			StartCoroutine(cam.GetComponent<CameraController>().goToPosition(cam.GetComponent<CameraController>().cameraCurrentPos, player.transform.position, 1));

		} else {
			
			playersTurn = false;
			enemysTurn = true;
			whosTurn = "Enemy";

			yield return new WaitForSeconds(0.9f);

			//reset camera's old target
			cam.GetComponent<CameraController>().targetToFollow = null;
			//tell the camera to go to enemy position
			StartCoroutine(cam.GetComponent<CameraController>().goToPosition(cam.GetComponent<CameraController>().cameraCurrentPos, enemy.transform.position, 1));

		}

		print ("whosTurn: " + whosTurn);
	}


	/// <summary>
	/// Gameover sequence.
	/// </summary>
	IEnumerator finishTheGame(int res) {

		//finish the game
		gameIsFinished = true;
		print ("Game Is Finished");

		//play sfx
		playSfx(endSfx);

		//wait a little
		yield return new WaitForSeconds(1.0f);

		//disable ui camera
		uiCam.SetActive(false);

		//activate game finish plane
		gameoverManager.SetActive (true);

		//set the label
		if (res == 0) {
			uiGameStateLabel.GetComponent<TextMesh> ().text = "You have Lost :(";
		} else if (res == 1) {
			uiGameStateLabel.GetComponent<TextMesh> ().text = "You have Won !";
		} else if (res == 2) {
			uiGameStateLabel.GetComponent<TextMesh> ().text = "Did you have a good hunt?";
			//set stat info
			uiBirdhuntStatPanel.SetActive(true);
			uiStatBirdHits.GetComponent<TextMesh> ().text = birdsHit.ToString();
			int BirdHuntBestScore = PlayerPrefs.GetInt ("BirdHuntBestScore");
			uiStatBestScore.GetComponent<TextMesh> ().text = BirdHuntBestScore.ToString();
			//save new best score
			if (birdsHit > BirdHuntBestScore) {
				PlayerPrefs.SetInt ("BirdHuntBestScore", birdsHit);
				uiStatBestScore.GetComponent<TextMesh> ().text = birdsHit.ToString();
			}

		}

		//calculate score and grants player some coins
		int shotBonus = 0;
		int timeBonus = 0;

		if (playerArrowShot <= 3)
			shotBonus = 150;
		else if (playerArrowShot > 3 && playerArrowShot <= 6)
			shotBonus = 75;
		else if (playerArrowShot > 6 && playerArrowShot <= 10)
			shotBonus = 25;

		if (Time.timeSinceLevelLoad <= 30)
			timeBonus = 250;
		else if (Time.timeSinceLevelLoad > 30 && Time.timeSinceLevelLoad < 60)
			timeBonus = 100;
		else if (Time.timeSinceLevelLoad > 60 && Time.timeSinceLevelLoad < 90)
			timeBonus = 50;

		//Final coin formula (normal game)
		playerCoins = shotBonus + timeBonus + (res * 200);

		//Override - Final coin formula (Birdhunt game mode)
		if(res == 2)
			playerCoins = birdsHit * 100;

		//set the score/coins on UI
		uiYouWon.GetComponent<TextMesh> ().text = playerCoins.ToString ();

		//Save new coin amount
		int savedCoins = PlayerPrefs.GetInt ("PlayerCoins");
		PlayerPrefs.SetInt ("PlayerCoins", playerCoins + savedCoins);

		//bring the panel inside game view
		float t = 0;
		while (t < 1) {
			t += Time.deltaTime;
			gameoverManager.transform.position = new Vector3(	cam.transform.position.x,
																Mathf.SmoothStep(-15, 0, t),
																gameoverManager.transform.position.z);
			yield return 0;
		}

		//show an Interstitial Ad when the game is finished
		if(AdManagerObject)
			AdManagerObject.GetComponent<AdManager>().showInterstitial();
	}


	//*****************************************************************************
	// This function animates a button by modifying it's scales on x-y plane.
	// can be used on any element to simulate the tap effect.
	//*****************************************************************************
	IEnumerator animateButton ( GameObject _btn  ){

		float buttonAnimationSpeed = 9.0f;
		canTap = false;
		Vector3 startingScale = _btn.transform.localScale;	//initial scale	
		Vector3 destinationScale = startingScale * 1.1f;	//target scale

		//Scale up
		float t = 0.0f; 
		while (t <= 1.0f) {
			t += Time.deltaTime * buttonAnimationSpeed;
			_btn.transform.localScale = new Vector3( Mathf.SmoothStep(startingScale.x, destinationScale.x, t),
				Mathf.SmoothStep(startingScale.y, destinationScale.y, t),
				_btn.transform.localScale.z);
			yield return 0;
		}

		//Scale down
		float r = 0.0f; 
		if(_btn.transform.localScale.x >= destinationScale.x) {
			while (r <= 1.0f) {
				r += Time.deltaTime * buttonAnimationSpeed;
				_btn.transform.localScale = new Vector3( Mathf.SmoothStep(destinationScale.x, startingScale.x, r),
					Mathf.SmoothStep(destinationScale.y, startingScale.y, r),
					_btn.transform.localScale.z);
				yield return 0;
			}
		}

		if(r >= 1)
			canTap = true;
	}


	/// <summary>
	/// enable touch commands again
	/// </summary>
	IEnumerator activateTap() {
		yield return new WaitForSeconds (1.0f);
		canTap = true;
	}


	/// <summary>
	/// Plays the sfx.
	/// </summary>
	void playSfx ( AudioClip _clip  ){
		GetComponent<AudioSource>().clip = _clip;
		if(!GetComponent<AudioSource>().isPlaying) {
			GetComponent<AudioSource>().Play();
		}
	}


	/// <summary>
	/// Game timer manager
	/// </summary>
	void manageGameTimer (){

		if (gameIsFinished)
			return;

		seconds = Mathf.CeilToInt(gameTimer - Time.timeSinceLevelLoad) % 60;
		minutes = Mathf.CeilToInt(gameTimer - Time.timeSinceLevelLoad) / 60; 

		if(seconds == 0 && minutes == 0) {
			StartCoroutine(finishTheGame (2));
		}

		remainingTime = string.Format("{0:00} : {1:00}", minutes, seconds); 
		uiTimeText.GetComponent<TextMesh>().text = remainingTime.ToString();

		//Also show hitted birds counter on UI
		uiBirdsHitText.GetComponent<TextMesh>().text = birdsHit.ToString();
	}


	/// <summary>
	/// Adds the bonus time.
	/// </summary>
	public static void addBonusTime() {
		gameTimer += bonusTime;
	}

}
