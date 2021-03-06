﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIBehaviour : MonoBehaviour
{

    public class scoutedObject
    {
        public scoutedObject(GameObject _obj)
        {
            if (_obj != null)
            {
                obj = _obj;
                positionSpotted = obj.transform.position;
                objID = obj.GetComponent<ObjectID>();
                ownerID = objID.ownerPlayerID;
                objType = objID.objID;

                if (obj.GetComponent<TCController>())
                {
                    isTC = true;
                }
            }
        }

        public GameObject obj;
        public ObjectID objID;
        public bool isTC = false;
        public float distanceFromUs;
        public Vector3 positionSpotted;
        public ObjectID.OBJECTID objType;
        public ObjectID.PlayerID ownerID;
    };

    public class aiObject
    {
        public aiObject(GameObject _obj)
        {
            obj = _obj;
            objID = obj.GetComponent<ObjectID>();
            ownerID = objID.ownerPlayerID;
            aiCtrl = obj.GetComponent<AIDroneController>();
        }

        public GameObject obj;
        public ObjectID objID;
        public bool isTC = false;
        public float distanceFromUs;
        public ObjectID.PlayerID ownerID;
        public AIDroneController aiCtrl;
    };

    public class outpostBuilding{
        public outpostBuilding(GameObject _obj)
        {
            obj = _obj;
            lastSeenPosition = obj.transform.position;

            if (obj.tag == "CarWashParent")
            {
                carWashType = obj.GetComponentInChildren<CarWash>().carWashType;
                isCarWash = true;
                objID = obj.GetComponentInChildren<ObjectID>().objID;
            }
            else if (obj.tag == "Turret")
            {
                isTurret = true;
            }
            else if (obj.tag == "House")
            {
                isHouse = true;
            }

            if (objID == ObjectID.OBJECTID.UNASSIGNED)
            {
                objID = obj.GetComponent<ObjectID>().objID;
            }

        }

        public GameObject obj;
        public ObjectID.OBJECTID objID = ObjectID.OBJECTID.UNASSIGNED;
        public AIDroneController.DroneMode carWashType = AIDroneController.DroneMode.BOMBER;
        public Vector3 lastSeenPosition;
        public bool isCarWash = false;
        public bool isTurret = false;
        public bool isHouse = false;

    };

    public int playerID = -1;
    public float seekResourceRange = 50.0f;
    public Vector2 profitCheckRandomRange = new Vector2(5.0f, 20.0f);

    //Privates
    public List<float> balanceHistory = new List<float>() { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
    private List<aiObject> units = new List<aiObject>();
    private List<aiObject> idleUnits = new List<aiObject>();
    private List<aiObject> enemyUnits = new List<aiObject>();
    private List<scoutedObject> enemyBuilds = new List<scoutedObject>();
    private List<scoutedObject> resources = new List<scoutedObject>();
    private List<outpostBuilding> outpostBuildings = new List<outpostBuilding>();
    private List<outpostBuilding> destroyedBuildings = new List<outpostBuilding>();
    private GameManager GM;
    private ObjectID objID;
    private TCController TC;
    private GameObject scoutUnit;
    private GameObject blackHole;
    private GameObject ground;
    public GameObject closestKnownResource = null;
    public GameObject closestKnownEnemyBuilding = null;
    public GameObject closestKnownEnemyTC = null;
    public aiObject closestKnownEnemyUnit = null;
    public float profitCheckTimer = 0.0f;
    public float profitCheckThreshold = 7.0f;
    public float lastBalanceAvg = Mathf.Infinity;
    private float knowledgeTimer = 0.0f;
    private float knowledgeThreshold = 5.0f;
    private float acceptableAsteriodDistance = 100.0f;
    private bool AIStepLock = false;
    private float baseCost = 100;
    private float mineCost = 5000;
    private float attackCost = 100000;
    private float boostCost = 150000;
    private float escapeCost = 999999;
    public bool hasMinerCW = false;
    public bool hasFighterCW = false;
    public bool hasBoosterCW = false;
    public float timeOutThread = 0.0f;
    private float timeOutThreadThreshold = 6.0f;
    private LayerMask unitLayer;


    public void assignCity(List<GameObject> _outpostBuildings)
    {
        for (int i = 0; i < _outpostBuildings.Count; i++)
        {
            outpostBuildings.Add(new outpostBuilding(_outpostBuildings[i]));
        }
    }

    public void assignCity(GameObject _outpostBuilding)
    {
        outpostBuildings.Add(new outpostBuilding(_outpostBuilding));
    }

    void TickTickTickTickTickTickTickTickTickTickTickTickTickTick()
    {
        profitCheckTimer += Time.unscaledDeltaTime;
        timeOutThread += Time.unscaledDeltaTime;
    }

    void Escape()
    {
        //Win Condition
        TC.SpawnUnit(TCController.STORE.ESCAPE, true);
    }

    // Start is called before the first frame update
    void Start()
    {
        objID = GetComponent<ObjectID>();
        if (objID.ownerPlayerID == ObjectID.PlayerID.UNASSIGNED)
        { 
            playerID = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>().RequestID((int)ObjectID.PlayerID.AI_1);
            objID.ownerPlayerID = (ObjectID.PlayerID)playerID;
        }
        else
        {
            playerID = (int)objID.ownerPlayerID;
        }

        //Set up timers
        profitCheckThreshold = Random.Range(profitCheckRandomRange.x, profitCheckRandomRange.y);

        //Find References
        GM = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        TC = GetComponent<TCController>();
        blackHole = GameObject.FindGameObjectWithTag("Blackhole");
        ground = GameObject.FindGameObjectWithTag("Ground");
        acceptableAsteriodDistance = Vector3.Distance(ground.transform.position, GameObject.FindGameObjectWithTag("Henry'sStupidCube").transform.position);
        unitLayer = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<PlayerController>().unitInteractLayers;

        baseCost = TC.baseCost;
        mineCost = TC.mineCost;
        attackCost = TC.attackCost;
        boostCost = TC.boostCost;
        escapeCost = TC.escapeCost;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //TICK TICK TICK TICK TICK TICK TICK TICK TICK TICK TICK 
        TickTickTickTickTickTickTickTickTickTickTickTickTickTick();
        //IF ESCAPE IS THING THEN DO THE ESCAPE THING
        if (escapeCost <= GM.GetResouceCount(playerID))
        {
            Escape();
        }

        //OTHERWISE DO THE OTHER THINGS
        else if (!AIStepLock)
        {
            timeOutThread = 0.0f;
            StartCoroutine(AIStep());
        }

        else if (timeOutThreadThreshold < timeOutThread)
        {
            Debug.LogWarning("AI Step Reached Timeout, Skipping Step...");
            StopCoroutine(AIStep());
            timeOutThread = 0.0f;
            AIStepLock = false;
        }
    }
    
    IEnumerator AIStep()
    {
        //Lock
        AIStepLock = true;


        /*
         * 
         * CULL NULLS
         * 
         */

        for (int i = 0; i < units.Count; i++)
        {
            //Is the obj null?
            if (units[i] == null)
            {
                //Remove obj if null
                units.RemoveAt(i);
            }
            yield return null;
        }

        /*
         * 
         * FIND OBJECTS
         * 
         */

        Collider[] foundResources = Physics.OverlapSphere(transform.position, seekResourceRange, unitLayer);
        GameObject[] foundWorldUnits = GameObject.FindGameObjectsWithTag("Unit");
        GameObject[] foundBuildingsTCs = GameObject.FindGameObjectsWithTag("TC");
        GameObject[] foundBuildingsCWs = GameObject.FindGameObjectsWithTag("CarWash");

        /*
         * 
         * POPULATE LISTS
         * 
         */

        //Clear lists
        idleUnits.Clear();
        resources.Clear();
        enemyUnits.Clear();
        enemyBuilds.Clear();
        units.Clear();

        //Attacking unit counters
        int friendlyAttackUnits = 0;
        int enemyAttackUnits = 0;

        float tmpDistance = Mathf.Infinity;

        tmpDistance = Mathf.Infinity;

        for (int i = 0; i < foundBuildingsTCs.Length; i++)
        {
            if (foundBuildingsTCs[i] != null)
            {
                //Filter out friendlies
                if (foundBuildingsTCs[i].GetComponent<ObjectID>().ownerPlayerID != (ObjectID.PlayerID)playerID)
                {
                    enemyBuilds.Add(new scoutedObject(foundBuildingsTCs[i]));
                    if (Vector3.Distance(transform.position, enemyBuilds[enemyBuilds.Count - 1].obj.transform.position) < tmpDistance)
                    {
                        closestKnownEnemyTC = enemyBuilds[enemyBuilds.Count - 1].obj;
                        tmpDistance = Vector3.Distance(transform.position, enemyBuilds[enemyBuilds.Count - 1].obj.transform.position);
                    }
                }
            }
            yield return null;
        }

        tmpDistance = Mathf.Infinity;

        for (int i = 0; i < foundBuildingsCWs.Length; i++)
        {
            //Filter out fakes
            if (foundBuildingsCWs[i] != null)
            {
                if (foundBuildingsCWs[i].GetComponent<ObjectID>() != null)
                {
                    //Filter out friendlies
                    if (foundBuildingsCWs[i].GetComponent<ObjectID>().ownerPlayerID != (ObjectID.PlayerID)playerID)
                    {
                        enemyBuilds.Add(new scoutedObject(foundBuildingsCWs[i]));
                        if (Vector3.Distance(transform.position, enemyBuilds[enemyBuilds.Count - 1].obj.transform.position) < tmpDistance)
                        {
                            closestKnownEnemyBuilding = enemyBuilds[enemyBuilds.Count - 1].obj;
                            tmpDistance = Vector3.Distance(transform.position, enemyBuilds[enemyBuilds.Count - 1].obj.transform.position);
                        }
                    }
                    //If it ours check if we own it as it may of been repaired
                    else
                    {
                        bool knowOfCW = false;
                        for (int j = 0; j < outpostBuildings.Count; j++)
                        {
                            if (outpostBuildings[j].obj == foundBuildingsCWs[i])
                            {
                                knowOfCW = true;
                            }
                        }

                        if (!knowOfCW)
                        {
                            outpostBuildings.Add(new outpostBuilding(foundBuildingsCWs[i]));
                        }

                    }
                }
            }
            yield return null;
        }

        tmpDistance = Mathf.Infinity;

        for (int i = 0; i < foundWorldUnits.Length; i++)
        {
            if (foundWorldUnits[i] != null)
            {
                //Filter out friendlies
                if (foundWorldUnits[i].GetComponent<ObjectID>().ownerPlayerID != (ObjectID.PlayerID)playerID)
                {
                    //Counter
                    if (foundWorldUnits[i].GetComponent<AIDroneController>().droneMode == AIDroneController.DroneMode.FIGHTER || foundWorldUnits[i].GetComponent<AIDroneController>().droneMode == AIDroneController.DroneMode.BOOSTER)
                    {
                        enemyAttackUnits++;
                    }
                    enemyUnits.Add(new aiObject(foundWorldUnits[i]));
                    if (Vector3.Distance(transform.position, enemyUnits[enemyUnits.Count - 1].obj.transform.position) < tmpDistance)
                    {
                        closestKnownEnemyUnit = enemyUnits[enemyUnits.Count - 1];
                        tmpDistance = Vector3.Distance(transform.position, enemyUnits[enemyUnits.Count - 1].obj.transform.position);
                    }
                }
                else
                {
                    if (foundWorldUnits[i] != null)
                    {
                        units.Add(new aiObject(foundWorldUnits[i]));
                        //Check for idling
                        if (units[units.Count - 1].aiCtrl.isIdle())
                        {
                            idleUnits.Add(units[units.Count - 1]);
                        }

                        friendlyAttackUnits++;
                    }
                }
            }
            yield return null;
        }

        tmpDistance = Mathf.Infinity;

        for (int i = 0; i < foundResources.Length; i++)
        {
            //If not inside the game world enough
            if (foundResources[i] != null) {
                if (Vector3.Distance(foundResources[i].transform.position, ground.transform.position) < acceptableAsteriodDistance)
                {
                    if (foundResources[i].GetComponent<ObjectID>().objID == ObjectID.OBJECTID.RESOURCE)
                    {
                        resources.Add(new scoutedObject(foundResources[i].gameObject));

                        if (resources[resources.Count - 1].obj != null)
                        {
                            if (Vector3.Distance(transform.position, resources[resources.Count - 1].obj.transform.position) < tmpDistance)
                            {
                                closestKnownResource = resources[resources.Count - 1].obj;
                                tmpDistance = Vector3.Distance(transform.position, resources[resources.Count - 1].obj.transform.position);
                            }
                        }
                    }
                }
            }
            yield return null;
        }

        /*
         * 
         * Is base missing Things
         * 
         */

        for (int i = 0; i < outpostBuildings.Count; i++)
        {
            //Building was destoryed
            if (outpostBuildings[i].obj == null)
            {
                destroyedBuildings.Add(outpostBuildings[i]);
                outpostBuildings.RemoveAt(i);
            }
            else
            {
                if (outpostBuildings[i].obj.tag == "CarWashParent")
                {
                    switch (outpostBuildings[i].carWashType)
                    {
                        case AIDroneController.DroneMode.BOOSTER:
                            {
                                hasBoosterCW = true;
                                break;
                            }
                        case AIDroneController.DroneMode.FIGHTER:
                            {
                                hasFighterCW = true;
                                break;
                            }
                        case AIDroneController.DroneMode.MINER:
                            {
                                hasMinerCW = true;
                                break;
                            }
                        default:
                            {
                                Debug.LogWarning($"Found Unknown Type: {outpostBuildings[i].carWashType}");
                                break;
                            };
                    }

                }
            }
        }

        /*
         * 
         * SAFETY IS NUMBER SIX PRIORITY
         * 
         */

        //Check if too close to blackhole if so try and come back towards TC out of blackhole range
        for (int i = 0; i < units.Count; i++)
        {
            //If we are too close to blackhole
            if (Vector3.Distance(transform.position, blackHole.transform.position) < blackHole.transform.localScale.x + 2.0f)
            {
                //Move away
                Vector3 dir = (transform.position - blackHole.transform.position).normalized;
                Vector3 escapePos = transform.position + (dir * 10.0f);
                units[i].aiCtrl.UpdateTargetPos(escapePos, null);
            }

            yield return null;
        }

        /*
         * 
         * PROFIT CHECK
         * 
         */

        if ((profitCheckTimer > profitCheckThreshold))
        {
            profitCheckTimer = 0.0f;
            bool madeProfit = ((escapeCost / (blackHole.GetComponent<Blackhole>().twomintimer / blackHole.GetComponent<Blackhole>().timer)) < GM.GetResouceCount(playerID));

            /*
             * 
             * MINERS
             * 
             */


            //If we didn't make a profit
            if (!madeProfit)
            {

                Debug.Log($"Idle Unit Count: {idleUnits.Count} and total units is {units.Count}");

                int amountofUnitsAffected = 0;

                //Do we have idle miners
                for (int i = 0; i < idleUnits.Count; i++)
                {

                    if ((idleUnits[i].aiCtrl.droneMode == AIDroneController.DroneMode.MINER) || (idleUnits[i].aiCtrl.droneMode == AIDroneController.DroneMode.WORKER))
                    {
                        //Target resource until we have at least five
                        idleUnits[i].aiCtrl.UpdateTargetPos(Vector3.zero, closestKnownResource);

                        if (hasMinerCW)
                        {
                            idleUnits[i].aiCtrl.droneMode = AIDroneController.DroneMode.MINER;
                        }

                        amountofUnitsAffected++;
                    }
                    if (amountofUnitsAffected >= 5)
                    {
                        break;
                    }

                    yield return null;
                }

                //Make more miners if we don't have idle miners
                if (amountofUnitsAffected < 5)
                {
                    //build more units
                    for (int i = amountofUnitsAffected; i < 5; i++)
                    {
                        if (hasMinerCW)
                        {
                            TC.SpawnUnit(TCController.STORE.BASE, true, AIDroneController.DroneMode.MINER);
                        }
                        else
                        {
                            TC.SpawnUnit(TCController.STORE.BASE, true);
                        }
                    }
                }

            }
        }
        /*
         * 
         * Does anyone have bad rep (units and buildings will need a function for this)
         * 
         */


        /*
         * 
         * Is Attack Unit Count OK
         * 
         */

        //If we don't have a bigger army than slightly more than 1/4 of all enemy units on the map
        float sliceOfEnemyUnitCount = ((enemyAttackUnits * 0.25f) + (enemyAttackUnits * 0.2f));
        if (sliceOfEnemyUnitCount > friendlyAttackUnits)
        {
            //Convert base units to fighter if we have a CW
            if (hasFighterCW)
            {
                //Use idle units until we have used enough
                if (idleUnits.Count > 0)
                {
                    for (int i = 0; i < idleUnits.Count; i++)
                    {
                        idleUnits[i].aiCtrl.droneMode = AIDroneController.DroneMode.FIGHTER;
                        friendlyAttackUnits++;
                        if (friendlyAttackUnits >= sliceOfEnemyUnitCount)
                        {
                            break;
                        }
                    }
                }
                //Otherwise make new units equal to how many we need
                else
                {
                    for (int i = 0; i < (sliceOfEnemyUnitCount - friendlyAttackUnits); i++)
                    {
                        TC.SpawnUnit(TCController.STORE.BASE, true, AIDroneController.DroneMode.FIGHTER);
                    }
                }
            }
        }


        /*
         * 
         * FIX DESTORYED BUILDINGS
         * 
         */

        for (int i = 0; i < destroyedBuildings.Count; i++)
        {
            //Pick a random building
            int elementToFix = Random.Range(0, destroyedBuildings.Count);
            //Carwash
            if (destroyedBuildings[elementToFix].isCarWash)
            {
                switch (destroyedBuildings[elementToFix].carWashType)
                {
                    case AIDroneController.DroneMode.FIGHTER:
                        {
                            if (TC.SpawnUnit(TCController.STORE.ATTACKCW, true, destroyedBuildings[elementToFix]))
                            {
                                destroyedBuildings.RemoveAt(i);
                            }
                            break;
                        }
                    case AIDroneController.DroneMode.MINER:
                        {
                            if (TC.SpawnUnit(TCController.STORE.MINECW, true, destroyedBuildings[elementToFix]))
                            {
                                destroyedBuildings.RemoveAt(i);
                            }
                            break;
                        }
                    case AIDroneController.DroneMode.BOOSTER:
                        {
                            if (TC.SpawnUnit(TCController.STORE.BOOSTCW, true, destroyedBuildings[elementToFix]))
                            {
                                destroyedBuildings.RemoveAt(i);
                            }
                            break;
                        }
                    default:
                        {
                            Debug.LogWarning($"Unknown Type of CarWash Cannot Rebuilt {destroyedBuildings[elementToFix].carWashType}");
                            break;
                        }
                }
            }
            else if (destroyedBuildings[elementToFix].isTurret)
            {
                if (TC.SpawnUnit(TCController.STORE.TURRET, true, destroyedBuildings[i]))
                {
                    destroyedBuildings.RemoveAt(i);
                }
            }
            else if (destroyedBuildings[elementToFix].isHouse)
            {
                if (TC.SpawnUnit(TCController.STORE.HOUSE, true, destroyedBuildings[i]))
                {
                    destroyedBuildings.RemoveAt(i);
                }
            }
            else
            {
                Debug.LogWarning($"Cannot fix building {destroyedBuildings[elementToFix]} as there is no logic for it");
            }
        }

        //Unlock
        AIStepLock = false;
        yield return null;
    }



}
