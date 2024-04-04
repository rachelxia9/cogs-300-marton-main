
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.iOS;
using UnityEngine.AI;
using System.Runtime.CompilerServices;

// Marton with raycasting

/*
// TODO (27/3/24):
1. Fix laser shooting. Only shoot laser when enemy approaches or is in range
2. Raycasting features: detect target, enemy, and drive behaviour based on observations
3. Agent behaviour to implement:
    - !!! list out here
4. Tweak imitation learning to aid training - see config
*/

public class Marton : CogsAgent
{
    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    
    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
    }

    // Prints out ray cast output
    // Note: this works sometimes? Can give NRE error and sometimes not?
    public string[] RayCastHelper(){
        // Get component of raycast
        RayPerceptionSensorComponent3D raycast = GetComponent<RayPerceptionSensorComponent3D>();
        // Get outputs of the raycasts
        var raycast_output = RayPerceptionSensor.Perceive(raycast.GetRayPerceptionInput()).RayOutputs;
        string[] ret = new string[2];
        ret[0] = "holder";

        for (int i = 0; i < raycast_output.Length; i++){
            GameObject hit = raycast_output[i].HitGameObject;
            if (hit != null) {
                ret[0] = hit.name;
                Debug.Log("Rays " + hit.name);
            }
        }
        
        return ret;
    }
    
    

    
    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate() {
        base.FixedUpdate();
        
        LaserControl();
        // Movement based on DirToGo and RotateDir
        moveAgent(dirToGo, rotateDir);
        // raycast here
        RayCastHelper();
    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        // Time remaning
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);

        // for each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
        
        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());
    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; //Simulated NN output 0
        discreteActionsOut[1] = 0; //....................1
        discreteActionsOut[2] = 0; //....................2
        discreteActionsOut[3] = 0; //....................3
        discreteActionsOut[4] = 0; //TODO-2: Uncomment this next line when implementing GoBackToBase();

       
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;
        }       
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            //TODO-1: Using the above as examples, set the action out for the left arrow press
            discreteActionsOut[1] = 2; 
        }
        

        //Shoot
        if (Input.GetKey(KeyCode.Space)){
            discreteActionsOut[2] = 1;
        }

        //GoToNearestTarget
        if (Input.GetKey(KeyCode.A)){
            discreteActionsOut[3] = 1;
        }


        //TODO-2: implement a keypress (your choice of key) for the output for GoBackToBase();
        if(Input.GetKey(KeyCode.B)){
            discreteActionsOut[4] = 1;
        }

    }

        // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
        public override void OnActionReceived(ActionBuffers actions){

        int forwardAxis = (int)actions.DiscreteActions[0]; //NN output 0

        //TODO-1: Set these variables to their appopriate item from the act list
        int rotateAxis = (int)actions.DiscreteActions[1]; 
        int shootAxis = (int)actions.DiscreteActions[2]; 
        int goToTargetAxis = (int)actions.DiscreteActions[3];
        
        //TODO-2: Uncomment this next line and set it to the appropriate item from the act list
         int goToBaseAxis = (int)actions.DiscreteActions[4];

        //TODO-2: Make sure to remember to add goToBaseAxis when working on that part!
        
        MovePlayer(forwardAxis, rotateAxis, shootAxis, goToTargetAxis, goToBaseAxis);

        //AddReward(rewardDict["collectnow"]); // based on arena example
    }


// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision)
    {
        
        if (collision.gameObject.CompareTag("HomeBase") && 
            collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            int ballsInBase = collision.gameObject.GetComponent<HomeBase>().GetCaptured();
            if (ballsInBase == 1) { // items at home base == 1
                // Debug.Log("Collected 1 target");
                AddReward(rewardDict["reward"]);
            }
            if (ballsInBase > 1) { // more than one ball
                // Debug.Log("Collected many targets");
                AddReward(rewardDict["reward"] * ballsInBase);
            }
        }
        base.OnTriggerEnter(collision);
  
    }

    protected override void OnCollisionEnter(Collision collision) 
    {
        //target is not in my base and is not being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && 
            collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && 
            collision.gameObject.GetComponent<Target>().GetCarried() == 0 && 
            !IsFrozen())
        {
            AddReward(rewardDict["reward"]);
            // Debug.Log("Agent has " + carriedTargets.Count + " balls");
            // Go to base when carrying more than 2 balls
            if (GetCarrying() > 2) {
                Debug.Log("Agent: I'm heading back!");
                GoToBase();
            }

            // add simple if collect more than x balls, give negative reward
        }

        if (collision.gameObject.CompareTag("Wall"))
        {
            // Debug.Log("Agent : I hate walls");
            AddReward(rewardDict["avoid"]);
        }
        base.OnCollisionEnter(collision);
    }



    //  --------------------------HELPERS---------------------------- 
     private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", -3f);
        rewardDict.Add("shooting-laser", 0f); // Note: maybe give negative reward for just shooting laser? Must hit target to get net positive reward
        rewardDict.Add("hit-enemy", 0.3f);
        rewardDict.Add("dropped-one-target", -0.4f); // drops object upon getting shot
        rewardDict.Add("dropped-targets", -3f); // drops multiple objects upon getting shot
        rewardDict.Add("reward", 1.5f); // general reward factor
        rewardDict.Add("avoid", -1f); // general punishment factor
        rewardDict.Add("collectnow", -0.01f);
    }
    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int goToBase)
    //TODO-2: Add goToBase as an argument to this function ^
    {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

        //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing. This case is not necessary to include, it's only here to explicitly show what happens in case 0
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            //TODO-1: Tell your agent to go backward!
            dirToGo = backward;
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            //do nothing
        }
        
        //TODO-1 : Implement the other cases for rotateDir
        else if (rotateAxis == 1) {
            rotateDir = right;
        }

        else if (rotateAxis == 2) {
            rotateDir = left;
        }

        //shoot
        if (shootAxis == 1){
            SetLaser(true);
        }
        else {
            SetLaser(false);
        }


        //go to the nearest target
        if (goToTargetAxis == 1){
            GoToNearestTarget();
        }

        //TODO-2: Implement the case for goToBaseAxis
        
        if (goToBase == 1) {
            GoToBase(); 
        }

    //     string[] raycastResults = RaycastHelper();
    //     string detectedObjectName = raycastResults[0];
    //     if (detectedObjectName != "ball"){  
    //     GameObject detectedObject = GameObject.Find(detectedObjectName);
    //     if(detectedObject != null)
    //     {
    //         float angleToObject = GetYAngle(detectedObject);
    //         TurnAndGo(angleToObject);
    //     }
    // }


//NEW

    // Use raycast to go to nearest target
    GameObject obj = GetNearestTarget();
    /*
    if (obj.name == "target") {
        TurnAndGo(GetYAngle(obj));
    }
    AddReward(-0.1f); // based on arena example
        
    // !!! Have Marton be able to approach group of targets in the most optimal path possible
    
    // TODO
    // check getCarrying() == 0 , 
    
    - give reward, 
    - check base number of balls for advantage, 
    - check if holding too many balls
    */
    }

    // Go to home base
    private void GoToBase(){
        TurnAndGo(GetYAngle(myBase));
    }

    // Go to the nearest target
    private void GoToNearestTarget(){
        GameObject target = GetNearestTarget();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }        
    }

    // Rotate and go in specified direction
    private void TurnAndGo(float rotation){

        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            dirToGo = transform.forward;
        }
    }

    // return reference to nearest target
    protected GameObject GetNearestTarget(){
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }

    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle; 
        
    }
}
