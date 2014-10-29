/// ProFlares - v1.04 - Copyright 2013-2014 All rights reserved - ProFlares.com

/// <summary>
/// ProFlareBatch.cs
/// Processes all the ProFlares in a scene, converts them into geometry that can be rendered.
/// </summary>

using UnityEngine;
using System.Collections;
using System.Collections.Generic;



[System.Serializable]
public class FlareOcclusion {
	public bool occluded = false;
	public float occlusionScale = 1;
}

[ExecuteInEditMode]
[RequireComponent (typeof (MeshRenderer))]
[RequireComponent (typeof (MeshFilter))]
public class ProFlareBatch : MonoBehaviour {
	
	public enum Mode{
		Standard = 0,
		SingleCamera = 1,
		VR = 2
	}
	
	public Mode mode = Mode.Standard;
	
	public ProFlareAtlas _atlas;
	
	//List of flares
	public List<ProFlare> Flares = new List<ProFlare>();
	//List of FlareElements
	public List<ProFlareElement> FlareElements = new List<ProFlareElement>();
	
	public Camera GameCamera;
	public Transform GameCameraTrans;
	
	//Camera that the flare geometry will be rendered from.
	public Camera FlareCamera;
	public Transform FlareCameraTrans;
	//Cached Components
	public MeshFilter meshFilter;
	public Transform thisTransform;
	
	public MeshRenderer meshRender;
	
	public float zPos;
	
	//Multiple meshes used for double buffering technique
	public Mesh bufferMesh;
	public Mesh meshA;
	public Mesh meshB;
	
	//Ping pong value for double Buffering
	bool PingPong;
	
	//Material used for the Flares, this is automatically created.
	public Material mat;
	
	//Geometry Arrays
	Vector3[] vertices;
	Vector2[] uv;
	Color32[] colors;
	
	int[] triangles;
	
	public FlareOcclusion[] FlareOcclusionData;
	
	//Debug Propertys
	public bool useBrightnessThreshold = true;
	public int BrightnessThreshold = 1;
	public bool overdrawDebug;
	
	//When set to true the Flarebatches' geomerty will be rebuilt.
	public bool dirty = false;
	
	//HelperTransform used for FlarePositions calculations.
	public Transform helperTransform;
		
	public bool VR_Mode = false;
	public float VR_Depth = 0.2f;
	public bool SingleCamera_Mode = false;
	
	void Reset(){
		if(helperTransform == null)
			CreateHelperTransform();
 
		
		mat = new Material(Shader.Find("ProFlares/Textured Flare Shader"));
		
		if(meshFilter == null)
			meshFilter = GetComponent<MeshFilter>();
        if(meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();
		
		meshRender = gameObject.GetComponent<MeshRenderer>();
		
		if(meshRender == null)
            meshRender = gameObject.AddComponent<MeshRenderer>();
		
		
		if(FlareCamera == null){
			FlareCamera = transform.root.GetComponentInChildren<Camera>();
		}
        
		meshRender.material = mat;
		
	 	
		meshA = SetupGeometry();
		meshB = SetupGeometry();
		
		meshA.MarkDynamic();
		meshB.MarkDynamic();
		
		dirty = true;
	}
	
	void Awake(){
		
	}
	
	void Start () {
		//Turn off overdraw debug mode.
		if(Application.isPlaying){
			overdrawDebug = false;
			dirty = true;
		}
		
		if(GameCamera == null){
			GameObject GameCameraGo = GameObject.FindWithTag("MainCamera");
			if(GameCameraGo)
				if(GameCameraGo.camera)
					GameCamera = GameCameraGo.camera;
		}
        
		if(GameCamera)
			GameCameraTrans = GameCamera.transform;
		//Make sure we have the transform cached
		thisTransform = transform;
		
	 	meshA = SetupGeometry();
		meshA.name = "MeshA";
		meshB = SetupGeometry();
		meshB.name = "MeshB";
		meshA.MarkDynamic();
		meshB.MarkDynamic();
		

	}
	
	void CreateMat(){
		Debug.LogError("CreateMat");
		mat = new Material(Shader.Find("ProFlares/Textured Flare Shader"));
		meshRender.material = mat;
		if(_atlas)
			if(_atlas.texture)
				mat.mainTexture = _atlas.texture;
	}
	
	
	
	//Call when you switch your main render.
	public void SwitchCamera(Camera newCamera){
		GameCamera = newCamera;
		GameCameraTrans = newCamera.transform; 
		
		
		//Update Occlusion data on changing camera new in v1.03
		FixedUpdate();

		for(int F = 0; F < Flares.Count; F++){
			if(FlareOcclusionData[F] != null){
			 if(FlareOcclusionData[F].occluded)
				FlareOcclusionData[F].occlusionScale = 0;
				
			}
		}
	}
	
	void OnDestroy(){
		//Remove the helper transform.
		if(Application.isPlaying){
			Destroy(helperTransform.gameObject);
			Destroy(mat);
		}
		else{
			DestroyImmediate(helperTransform.gameObject);
			DestroyImmediate(mat);
		}
	}
	
    
	
	//Called from ProFlare.cs
	//First checks if the flare is already in the list, if not adds it and rebuils the Flarebatch Geometry
	public void AddFlare(ProFlare _flare){
		bool found = false;
        
		for(int i = 0; i < Flares.Count; i++){
			if(_flare == Flares[i]){
				found = true;
				break;
			}
		}
		
		if(!found){
			Flares.Add(_flare);
			
			FlareOcclusionData = new FlareOcclusion[Flares.Count];
			
			for(int i = 0; i < FlareOcclusionData.Length; i++){
				FlareOcclusionData[i] = new FlareOcclusion();
			}
			dirty = true;
		}
	}
	
	void CreateHelperTransform(){
		
		GameObject HelpTransformGo =  new GameObject("_HelperTransform");
		
		helperTransform = HelpTransformGo.transform;
		helperTransform.parent =  transform;
		helperTransform.localScale = Vector3.one;
		helperTransform.localPosition = Vector3.zero;
	}
	
	void Update(){
		
		if(thisTransform)
				thisTransform.localPosition = Vector3.forward*zPos;
		
		
		
		//Checks if you have deleted the helper transform. If its missing recreate it.....
		if(helperTransform == null)
			CreateHelperTransform();
		
        if(meshRender){
			if(meshRender.sharedMaterial == null)
				CreateMat();
		}else
			meshRender = gameObject.GetComponent<MeshRenderer>();
		
		if(meshA == null){
			meshA = SetupGeometry();
			meshA.name = "MeshA";
			meshA.MarkDynamic();
		}
		
		if(meshB == null){
			meshB = SetupGeometry();
			meshB.name = "MeshB";
			meshB.MarkDynamic();
		}
		
		if(dirty)
			ReBuildGeometry();
	}
	
	//Late update
	void LateUpdate () {
 		
		if(_atlas == null)
			return;
		
		
		if(!VR_Mode)
 			UpdateFlares();
	
	}
	
	public void UpdateFlares(){
		
		bufferMesh = PingPong ? meshA : meshB;
		
		PingPong = !PingPong;
		
		UpdateGeometry();
		
		bufferMesh.vertices = vertices;
		
		bufferMesh.colors32 = colors;

	  	meshFilter.sharedMesh = bufferMesh;
	}
	
	public void ForceRefresh(){
		
		Flares.Clear();
		
		ProFlare[] flares = GameObject.FindObjectsOfType(typeof(ProFlare)) as ProFlare[];
		
		for(int i = 0; i < flares.Length; i++){
			if(flares[i]._Atlas == _atlas)
				Flares.Add(flares[i]);
		}
		
		dirty = true;
	}
	
	void ReBuildGeometry(){
#if UNITY_EDITOR
		//See when the geometry is built, try and avoid triggering this in the middle of your game.
 		Debug.Log("ProFlares - Rebuilding Geometry : "+ gameObject.name,gameObject); //Commented out if gets annoying
#endif
		
		
		FlareElements.Clear();
		
		for(int i = 0; i < Flares.Count; i++){
			for(int i2 = 0; i2 < Flares[i].Elements.Count; i2++){
				FlareElements.Add(Flares[i].Elements[i2]);
			}
		}
		
		FlareOcclusionData = new FlareOcclusion[Flares.Count];
		
		for(int i = 0; i < FlareOcclusionData.Length; i++){
			FlareOcclusionData[i] = new FlareOcclusion();
		}
		
		DestroyImmediate(meshA);
		DestroyImmediate(meshB);
		DestroyImmediate(bufferMesh);
		
		meshA = null;
		meshB = null;
		bufferMesh = null;
		
		meshA = SetupGeometry();
		meshB = SetupGeometry();
		
		dirty = false;
	}
	
	Mesh SetupGeometry()
    {
        Mesh mesh = new Mesh();
		
		if(_atlas == null)
			return mesh;
		
		int vertSize = 0;
		int uvSize = 0;
		int colSize = 0;
		int triCount = 0;
		
        
		//Calculate how big each array needs to be based on the Flares
	 	for(int i = 0; i < FlareElements.Count; i++){
			switch(FlareElements[i].type){
				case(ProFlareElement.Type.Single):
				{
					vertSize = vertSize+4;
					uvSize = uvSize+4;
					colSize = colSize+4;
					triCount = triCount+6;
				}
                    break;
				case(ProFlareElement.Type.Multi):
				{
					int subCount = FlareElements[i].subElements.Count;
					vertSize = vertSize+(4*subCount);
					uvSize = uvSize+(4*subCount);
					colSize = colSize+(4*subCount);
					triCount = triCount+(6*subCount);
				}
                    break;
			}
		}
		
		
		//Create Built in arrays
		vertices = new Vector3[vertSize];
    	uv = new Vector2[uvSize];
		colors = new Color32[colSize];
		triangles = new int[triCount];
        
		
		//Set Inital valuse for each vertex
		for(int i = 0; i < vertices.Length/4; i++){
			int extra = i * 4;
			vertices[0+extra] = ((Vector3.right)+(Vector3.up));
			vertices[1+extra] = ((Vector3.right)+(Vector3.down));
			vertices[2+extra] = ((Vector3.left)+(Vector3.up));
			vertices[3+extra] = ((Vector3.left)+(Vector3.down));
		}
		
		//Set UV coordinates for each vertex, this only needs to be done in the mesh rebuild.
		int count = 0;
		for(int i = 0; i < FlareElements.Count; i++){
			switch(FlareElements[i].type){
				case(ProFlareElement.Type.Single):
				{
					int extra = (count) * 4;
					Rect final = _atlas.elementsList[FlareElements[i].elementTextureID].UV;
					uv[0+extra] = new Vector2(final.xMax,final.yMax);
					uv[1+extra] = new Vector2(final.xMax,final.yMin);
					uv[2+extra] = new Vector2(final.xMin,final.yMax);
					uv[3+extra] = new Vector2(final.xMin,final.yMin);
					count++;
				}break;
				case(ProFlareElement.Type.Multi):
				{
					for(int i2 = 0; i2 < FlareElements[i].subElements.Count; i2++){
						
						int extra2 = (count+i2) * 4;
						
						Rect final = _atlas.elementsList[FlareElements[i].elementTextureID].UV;
						
						uv[0+extra2] = new Vector2(final.xMax,final.yMax);
						uv[1+extra2] = new Vector2(final.xMax,final.yMin);
						uv[2+extra2] = new Vector2(final.xMin,final.yMax);
						uv[3+extra2] = new Vector2(final.xMin,final.yMin);
                        
					}
					count = count+FlareElements[i].subElements.Count;
				}break;
			}
		}
		
		//Set inital vertex colors.
		for(int i = 0; i < colors.Length/4; i++){
			int extra = i * 4;
			colors[0+extra] = new Color32(0,255,0,255);
			colors[1+extra] = new Color32(255,0,0,255);
			colors[2+extra] = new Color32(0,255,255,255);
			colors[3+extra] = new Color32(255,0,255,255);
		}
		
        
		
		//Set triangle array, this is only done in the mesh rebuild.
		for(int i = 0; i < triangles.Length/6; i++){
			int extra = i * 4;
			int extra2 = i * 6;
			triangles[0+extra2] = 0+extra;
			triangles[1+extra2] = 1+extra;
			triangles[2+extra2] = 2+extra;
			triangles[3+extra2] = 2+extra;
			triangles[4+extra2] = 1+extra;
			triangles[5+extra2] = 3+extra;
		}
		
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
  		mesh.colors32 = colors;
		
		
		mesh.bounds = new Bounds(Vector3.zero,Vector3.one*1000);
	 	 
        return mesh;
    }
	
	Vector3[] verts;
	Vector2 _scale;
	Color32 _color;
    
	
	public Vector3 RotatePoint(Vector3 p, float d,float ct,float st) {
		//float r = d * (Mathf.PI / 180);
		//float ct = Mathf.Cos(r);
		//float st = Mathf.Sin(r);
		float x = (ct * p.x - st * p.y);
		float y = (st * p.x + ct * p.y);
		return new Vector3(x,y,0);
	}
	
	void FixedUpdate(){
		
	
		if(!dirty)
		for(int F = 0; F < Flares.Count; F++){
			
			 FlareOcclusionData[F].occluded = false;
			
			//Flares[F].Occluded = false;
			
			if(Flares[F].RaycastPhysics){
				RaycastHit hit;
				
				Vector3 direction = GameCameraTrans.position-Flares[F].thisTransform.position;
				
				float distanceRay = Vector3.Distance(GameCameraTrans.position,Flares[F].thisTransform.position);
		        
				//Flares[F].Occluded = false;
				
				Ray ray = new Ray(Flares[F].thisTransform.position,direction);
				
				if (Physics.Raycast(ray,  out hit,distanceRay,Flares[F].mask)){
					
					//Flares[F].Occluded = true;
					
					FlareOcclusionData[F].occluded = true;

		#if UNITY_EDITOR					
					Debug.DrawRay(Flares[F].thisTransform.position,direction,Color.red);
					
					Flares[F].OccludingObject = hit.collider.gameObject;
		#endif
				}else{
		#if UNITY_EDITOR
		            Flares[F].OccludingObject = null;
					
					Debug.DrawRay(Flares[F].thisTransform.position,direction);
		#endif
				}
			}
		}
	}
    
	
 	
	void UpdateGeometry(){
		
		if(GameCamera == null){
			meshRender.enabled = false;
			return;
		}
		
		meshRender.enabled = true;
		
		for(int F = 0; F < Flares.Count; F++){

			Flares[F].LensPosition = GameCamera.WorldToViewportPoint(Flares[F].thisTransform.position);

			Vector3 LensPosition = Flares[F].LensPosition;
			
			bool isVisible = (LensPosition.z > 0f && LensPosition.x+Flares[F].OffScreenFadeDist > 0f && LensPosition.x-Flares[F].OffScreenFadeDist < 1f && LensPosition.y+Flares[F].OffScreenFadeDist > 0f && LensPosition.y-Flares[F].OffScreenFadeDist < 1f);
			
			//Off Screen fading
			float offScreenFade = 1;
			if(!(LensPosition.x > 0f && LensPosition.x < 1f && LensPosition.y > 0f && LensPosition.y < 1f)){
				float offScreenNorm = 1f/Flares[F].OffScreenFadeDist;
				float xPos = 0;
				float yPos = 0;
				
				if(!(LensPosition.x > 0f && LensPosition.x < 1f))
					xPos = LensPosition.x > 0.5f ? LensPosition.x-1f : Mathf.Abs(LensPosition.x);
				
				if(!(LensPosition.y > 0f && LensPosition.y < 1f))
					yPos = LensPosition.y > 0.5f ? LensPosition.y-1f : Mathf.Abs(LensPosition.y);
				
				offScreenFade = Mathf.Clamp01( offScreenFade - (Mathf.Max(xPos,yPos))*offScreenNorm);
			}
	        
			//Dynamic Triggering Center
			float centerBoost = 0;
			if(LensPosition.x > 0.5f-Flares[F].DynamicCenterRange && LensPosition.x < 0.5f+Flares[F].DynamicCenterRange && LensPosition.y > 0.5f-Flares[F].DynamicCenterRange && LensPosition.y < 0.5f+Flares[F].DynamicCenterRange){
				if(Flares[F].DynamicCenterRange > 0){
					float centerBoostNorm = 1/(Flares[F].DynamicCenterRange);
					centerBoost = 1-Mathf.Max(Mathf.Abs(LensPosition.x-0.5f),Mathf.Abs(LensPosition.y-0.5f))*centerBoostNorm;
				}
			}
		
			//Dynamic Triggering Edge
			float DynamicEdgeAmount = 0;
			
			bool isInEdgeZone1 = (
	                              LensPosition.x > 0f+Flares[F].DynamicEdgeBias+(Flares[F].DynamicEdgeRange) &&
	                              LensPosition.x < 1f-Flares[F].DynamicEdgeBias-(Flares[F].DynamicEdgeRange) &&
	                              LensPosition.y > 0f+Flares[F].DynamicEdgeBias+(Flares[F].DynamicEdgeRange) &&
	                              LensPosition.y < 1f-Flares[F].DynamicEdgeBias-(Flares[F].DynamicEdgeRange)
	                              );
			
			bool isInEdgeZone2 = (
	                              LensPosition.x+(Flares[F].DynamicEdgeRange) > 0f+Flares[F].DynamicEdgeBias &&
	                              LensPosition.x-(Flares[F].DynamicEdgeRange) < 1f-Flares[F].DynamicEdgeBias &&
	                              LensPosition.y+(Flares[F].DynamicEdgeRange) > 0f+Flares[F].DynamicEdgeBias &&
	                              LensPosition.y-(Flares[F].DynamicEdgeRange) < 1f-Flares[F].DynamicEdgeBias
	                              );
			
			if(!isInEdgeZone1&&isInEdgeZone2){
				
				float DynamicEdgeNormalizeValue = 1/(Flares[F].DynamicEdgeRange);
				float DynamicEdgeX = 0;
				float DynamicEdgeY = 0;
				
				bool isInEdgeZoneX1 = (LensPosition.x > 0f+Flares[F].DynamicEdgeBias+(Flares[F].DynamicEdgeRange) && LensPosition.x < 1f-Flares[F].DynamicEdgeBias-(Flares[F].DynamicEdgeRange));
				bool isInEdgeZoneX2 = (LensPosition.x+(Flares[F].DynamicEdgeRange) > 0f+Flares[F].DynamicEdgeBias && LensPosition.x-(Flares[F].DynamicEdgeRange) < 1f-Flares[F].DynamicEdgeBias);
				bool isInEdgeZoneY1 = (LensPosition.y > 0f+Flares[F].DynamicEdgeBias+(Flares[F].DynamicEdgeRange) && LensPosition.y < 1f-Flares[F].DynamicEdgeBias-(Flares[F].DynamicEdgeRange));
				bool isInEdgeZoneY2 = (LensPosition.y+(Flares[F].DynamicEdgeRange) > 0f+Flares[F].DynamicEdgeBias && LensPosition.y-(Flares[F].DynamicEdgeRange) < 1f-Flares[F].DynamicEdgeBias);
				
				if(!isInEdgeZoneX1&&isInEdgeZoneX2){
					DynamicEdgeX = LensPosition.x > 0.5f ? (LensPosition.x - 1 +Flares[F].DynamicEdgeBias) + (Flares[F].DynamicEdgeRange) : Mathf.Abs(LensPosition.x -Flares[F].DynamicEdgeBias - (Flares[F].DynamicEdgeRange));
	                
					DynamicEdgeX = (DynamicEdgeX*DynamicEdgeNormalizeValue)*0.5f;
				}
				
				if(!isInEdgeZoneY1&&isInEdgeZoneY2){
					DynamicEdgeY = LensPosition.y > 0.5f ? (LensPosition.y - 1 + Flares[F].DynamicEdgeBias) + (Flares[F].DynamicEdgeRange) : Mathf.Abs(LensPosition.y-Flares[F].DynamicEdgeBias - (Flares[F].DynamicEdgeRange));
					
					DynamicEdgeY = (DynamicEdgeY*DynamicEdgeNormalizeValue)*0.5f;
				}
				
				DynamicEdgeAmount = Mathf.Max(DynamicEdgeX,DynamicEdgeY);
			}
			
						
			DynamicEdgeAmount = Flares[F].DynamicEdgeCurve.Evaluate(DynamicEdgeAmount);
			
			float AngleFallOff = 1;
			
			if(Flares[F].UseAngleLimit){
				Vector3 playerAngle = Flares[F].thisTransform.forward;
	            
				Vector3 cameraAngle = GameCameraTrans.forward;
				
				float horizDiffAngle = Vector3.Angle(cameraAngle, playerAngle);
				
				horizDiffAngle = Mathf.Abs( Mathf.Clamp(180-horizDiffAngle,-Flares[F].maxAngle,Flares[F].maxAngle));
				
				AngleFallOff = 1f-(horizDiffAngle*(1f/(Flares[F].maxAngle*0.5f)));
				
				if(Flares[F].UseAngleCurve)
					AngleFallOff = Flares[F].AngleCurve.Evaluate(AngleFallOff);
			}
			
			float distanceFalloff = 1f;
			
			if(Flares[F].useMaxDistance){
				
				Vector3 heading  = Flares[F].thisTransform.position - GameCameraTrans.position;
				
				float distance = Vector3.Dot(heading, GameCameraTrans.forward);
				
				float distanceNormalised = 1f-(distance/Flares[F].GlobalMaxDistance);
				
				distanceFalloff =  1f*distanceNormalised;
				
			}
			
			if(!dirty)
			if(FlareOcclusionData[F] != null)
				if(FlareOcclusionData[F].occluded){
		           	FlareOcclusionData[F].occlusionScale = Mathf.Lerp(FlareOcclusionData[F].occlusionScale,0,Time.deltaTime*16);
				}else{
					FlareOcclusionData[F].occlusionScale = Mathf.Lerp(FlareOcclusionData[F].occlusionScale,1,Time.deltaTime*16);
				}
			
			float tempScale = 1;
			
		 	if(FlareCamera)
		 		helperTransform.position = FlareCamera.ViewportToWorldPoint(LensPosition);
			
		  	LensPosition = helperTransform.localPosition;
			
			if((!VR_Mode) && (!SingleCamera_Mode))
			  	LensPosition.z = 0f;
			 
				 
	        
			float finalAlpha;
			
			for(int i = 0; i < Flares[F].Elements.Count; i++){
				
				if(isVisible)
					switch(Flares[F].Elements[i].type){
						case(ProFlareElement.Type.Single):
							//Do the color stuff.
					
							Flares[F].Elements[i].ElementFinalColor.r = (Flares[F].Elements[i].ElementTint.r * Flares[F].GlobalTintColor.r);
						 	Flares[F].Elements[i].ElementFinalColor.g = (Flares[F].Elements[i].ElementTint.g * Flares[F].GlobalTintColor.g);
						 	Flares[F].Elements[i].ElementFinalColor.b = (Flares[F].Elements[i].ElementTint.b * Flares[F].GlobalTintColor.b);
	                        
							finalAlpha = Flares[F].Elements[i].ElementTint.a * Flares[F].GlobalTintColor.a;
							
							if(Flares[F].useDynamicEdgeBoost){
								if(Flares[F].Elements[i].OverrideDynamicEdgeBrightness)
									finalAlpha = finalAlpha + (Flares[F].Elements[i].DynamicEdgeBrightnessOverride*DynamicEdgeAmount);
								else
									finalAlpha = finalAlpha + (Flares[F].DynamicEdgeBrightness*DynamicEdgeAmount);
							}
							
							if(Flares[F].useDynamicCenterBoost){
								if(Flares[F].Elements[i].OverrideDynamicCenterBrightness)
	                                finalAlpha += (Flares[F].Elements[i].DynamicCenterBrightnessOverride*centerBoost);
								else
	                                finalAlpha += (Flares[F].DynamicCenterBrightness*centerBoost);
	                        }
							
							if(Flares[F].UseAngleBrightness)
								finalAlpha *= AngleFallOff;
	                        
							if(Flares[F].useDistanceFade)
								finalAlpha *= distanceFalloff;
	                        
 							finalAlpha *= FlareOcclusionData[F].occlusionScale;
							
							finalAlpha *= offScreenFade;
												
							Flares[F].Elements[i].ElementFinalColor.a = finalAlpha;
	                        
	                        break;
	                        
						case(ProFlareElement.Type.Multi):
							for(int i2 = 0; i2 < Flares[F].Elements[i].subElements.Count; i2++){
	                            //Do the color stuff.
						
	                            Flares[F].Elements[i].subElements[i2].colorFinal.r = (Flares[F].Elements[i].subElements[i2].color.r * Flares[F].GlobalTintColor.r);
	                           	Flares[F].Elements[i].subElements[i2].colorFinal.g = (Flares[F].Elements[i].subElements[i2].color.g * Flares[F].GlobalTintColor.g);
	                            Flares[F].Elements[i].subElements[i2].colorFinal.b = (Flares[F].Elements[i].subElements[i2].color.b * Flares[F].GlobalTintColor.b);
	                            
	                            finalAlpha = Flares[F].Elements[i].subElements[i2].color.a * Flares[F].GlobalTintColor.a;
	                            
	                            if(Flares[F].useDynamicEdgeBoost){
	                                if(Flares[F].Elements[i].OverrideDynamicEdgeBrightness)
	                                    finalAlpha = finalAlpha + (Flares[F].Elements[i].DynamicEdgeBrightnessOverride*DynamicEdgeAmount);
	                                else
	                                    finalAlpha = finalAlpha + (Flares[F].DynamicEdgeBrightness*DynamicEdgeAmount);
	                            }
	                            
	                            if(Flares[F].useDynamicCenterBoost){
	                                if(Flares[F].Elements[i].OverrideDynamicCenterBrightness)
	                                    finalAlpha += (Flares[F].Elements[i].DynamicCenterBrightnessOverride*centerBoost);
	                                else
	                                    finalAlpha += (Flares[F].DynamicCenterBrightness*centerBoost);
	                            } 
	                            
	                            if(Flares[F].UseAngleBrightness)
	                                finalAlpha *= AngleFallOff;
	                            
	                            if(Flares[F].useDistanceFade)
	                                finalAlpha *= distanceFalloff;
	                            
								finalAlpha *= FlareOcclusionData[F].occlusionScale;
						
	                            finalAlpha *= offScreenFade;
	                            
 	                            Flares[F].Elements[i].subElements[i2].colorFinal.a = finalAlpha;
	                            
							}
	                        break;
					}
				else{
					switch(Flares[F].Elements[i].type){
						case(ProFlareElement.Type.Single):
							Flares[F].Elements[i].ElementFinalColor = Color.black;
							tempScale = 0;
						break;
						case(ProFlareElement.Type.Multi):
							for(int i2 = 0; i2 < Flares[F].Elements[i].subElements.Count; i2++){
								Flares[F].Elements[i].subElements[i2].colorFinal = Color.black;
							}
							tempScale = 0;
						break;
					}
				}
	            
				//Element Scale
				float finalScale = tempScale;
				
				if(Flares[F].useDynamicEdgeBoost){
					if(Flares[F].Elements[i].OverrideDynamicEdgeBoost)
						finalScale = finalScale+((DynamicEdgeAmount)*Flares[F].Elements[i].DynamicEdgeBoostOverride);
					else
						finalScale = finalScale+((DynamicEdgeAmount)*Flares[F].DynamicEdgeBoost);
				}
				
				if(Flares[F].useDynamicCenterBoost){
					if(Flares[F].Elements[i].OverrideDynamicCenterBoost)
						finalScale = finalScale+(Flares[F].Elements[i].DynamicCenterBoostOverride*centerBoost);
					else
						finalScale = finalScale+(Flares[F].DynamicCenterBoost*centerBoost);
				}
				
				if(finalScale < 0) finalScale = 0;
				
				if(Flares[F].UseAngleScale)
					finalScale *= AngleFallOff;
				
				if(Flares[F].useDistanceScale)
					finalScale *= distanceFalloff;
				
				finalScale *= FlareOcclusionData[F].occlusionScale;
				
				if(!Flares[F].Elements[i].Visible)
					finalScale = 0;
				
				Flares[F].Elements[i].ScaleFinal = finalScale;
	            
				//Apply final screen position.
				switch(Flares[F].Elements[i].type){
					case(ProFlareElement.Type.Single):{
						Vector3 pos = LensPosition*-Flares[F].Elements[i].position;
						
						
						float zpos = LensPosition.z;
						
						if(VR_Mode){
							float flarePos = (Flares[F].Elements[i].position*-1)-1; 
							zpos = LensPosition.z *((flarePos*VR_Depth)+1);
						} 
					
						Vector3 newVect = new Vector3(
	                                                  Mathf.Lerp(pos.x,LensPosition.x,Flares[F].Elements[i].Anamorphic.x),
	                                                  Mathf.Lerp(pos.y,LensPosition.y,Flares[F].Elements[i].Anamorphic.y),
													  zpos
	                                                  );
						
						newVect = newVect + Flares[F].Elements[i].OffsetPostion;
						Flares[F].Elements[i].OffsetPosition = newVect;
	                    
						}break;
					case(ProFlareElement.Type.Multi):{
						for(int i2 = 0; i2 < Flares[F].Elements[i].subElements.Count; i2++){
	                        if(Flares[F].Elements[i].useRangeOffset){
								
	                            Vector3 posM = LensPosition*-Flares[F].Elements[i].subElements[i2].position;
 							
								
								float zpos = LensPosition.z;
						
								if(VR_Mode){
									float  flarePos = (Flares[F].Elements[i].subElements[i2].position*-1)-1;
 									 
									zpos = LensPosition.z *((flarePos*VR_Depth)+1);
								} 
							
	                            Vector3 newVectM = new Vector3(
	                                                           Mathf.Lerp(posM.x,LensPosition.x,Flares[F].Elements[i].Anamorphic.x),
	                                                           Mathf.Lerp(posM.y,LensPosition.y,Flares[F].Elements[i].Anamorphic.y),
															   zpos
	                                                           );
	                            
	                            newVectM = newVectM + Flares[F].Elements[i].OffsetPostion;
	                            
	                            Flares[F].Elements[i].subElements[i2].offset = newVectM;
								
	                        }
	                        else
	                            Flares[F].Elements[i].subElements[i2].offset = LensPosition*-Flares[F].Elements[i].position;
						}
						}break;
				}
				
				//Apply final element angle.
				float angles = 0;
				
				if(Flares[F].Elements[i].rotateToFlare){
					angles =  (180 / Mathf.PI)*(Mathf.Atan2(LensPosition.y - 0,LensPosition.x - 0));
				}
				angles = angles + (LensPosition.x*Flares[F].Elements[i].rotationSpeed);
				
				angles = angles + (LensPosition.y*Flares[F].Elements[i].rotationSpeed);
				
				angles = angles + (Time.time*Flares[F].Elements[i].rotationOverTime);
				
				Flares[F].Elements[i].FinalAngle = (Flares[F].Elements[i].angle)+angles;
			}
			
		}
		//Rendering Bit
		int count = 0;
		for(int i = 0; i < FlareElements.Count; i++){
			switch(FlareElements[i].type){
				case(ProFlareElement.Type.Single):
				{
					int extra = (count) * 4;
					
					//Check for DisabledPlayMode, then scale to zero. then skip over the rest of the calculations
					if(FlareElements[i].flare.DisabledPlayMode){

					
						vertices[0+extra] = Vector3.zero;
						vertices[1+extra] = Vector3.zero;
						vertices[2+extra] = Vector3.zero;
						vertices[3+extra] = Vector3.zero;
						
					}
					
					_scale = ((FlareElements[i].size*FlareElements[i].Scale*0.01f)*FlareElements[i].flare.GlobalScale)*FlareElements[i].ScaleFinal;
					
					//Avoid Negative scaling
					if((_scale.x < 0)||(_scale.y < 0))
                        _scale = Vector3.zero;
					
					Vector3 offset = FlareElements[i].OffsetPosition;
					
					float angle = FlareElements[i].FinalAngle;
					
					_color = FlareElements[i].ElementFinalColor;
					
					if(useBrightnessThreshold){
                        
						if(_color.a < BrightnessThreshold){
							_scale = Vector2.zero;
						}else if(_color.r+_color.g+_color.b < BrightnessThreshold){
							_scale = Vector2.zero;
						}
					}
					
					if(overdrawDebug)
						_color = new Color32(20,20,20,100);
					
					if(!FlareElements[i].flare.DisabledPlayMode){
						//Precalculate some of the RotatePoint function to avoid repeat math.
						float r = angle * (Mathf.PI / 180);
						float ct = Mathf.Cos(r);
						float st = Mathf.Sin(r);
                        
						vertices[0+extra] = RotatePoint((Vector3.right*_scale.x)+(Vector3.up*_scale.y),angle,ct,st)+offset;
						vertices[1+extra] = RotatePoint((Vector3.right*_scale.x)+(Vector3.down*_scale.y),angle,ct,st)+offset;
						vertices[2+extra] = RotatePoint((Vector3.left*_scale.x)+(Vector3.up*_scale.y),angle,ct,st)+offset;
						vertices[3+extra] = RotatePoint((Vector3.left*_scale.x)+(Vector3.down*_scale.y),angle,ct,st)+offset;
					}
				

						Color32 _color32 = _color;
						colors[0+extra] = _color32;
						colors[1+extra] = _color32; 
						colors[2+extra] = _color32; 
						colors[3+extra] = _color32;
					
                    count++;
				}
                    break;
                    
				case(ProFlareElement.Type.Multi):
				{
					for(int i2 = 0; i2 < FlareElements[i].subElements.Count; i2++){
						int extra2 = (count+i2) * 4;
                        
						//Check for DisabledPlayMode, then scale to zero. then skip over the rest of the calculations
                        if(FlareElements[i].flare.DisabledPlayMode){
							vertices[0+extra2] = Vector3.zero;
							vertices[1+extra2] = Vector3.zero;
							vertices[2+extra2] = Vector3.zero;
							vertices[3+extra2] = Vector3.zero;
							continue;
						}
                        
                        
						_scale = (((FlareElements[i].size*FlareElements[i].Scale*0.01f)*FlareElements[i].flare.GlobalScale)*FlareElements[i].subElements[i2].scale)*FlareElements[i].ScaleFinal;
						
						//Avoid Negative scaling
						if((_scale.x < 0)||(_scale.y < 0))
							_scale = Vector3.zero;
						
						Vector3 offset = FlareElements[i].subElements[i2].offset;
                        
						float angle = FlareElements[i].FinalAngle;
						
						angle += FlareElements[i].subElements[i2].angle;
						
						_color = FlareElements[i].subElements[i2].colorFinal;
                        
						if(useBrightnessThreshold){
							if(_color.a < BrightnessThreshold){
								_scale = Vector2.zero;
							}else if(_color.r+_color.g+_color.b < BrightnessThreshold){
								_scale = Vector2.zero;
							}
						}
                        
						if(overdrawDebug)
							_color = new Color32(20,20,20,100);
                        
						if(!FlareElements[i].flare.DisabledPlayMode){
                            
							//Precalculate some of the RotatePoint function to avoid repeat math.
							float r = angle * (Mathf.PI / 180);
							float ct = Mathf.Cos(r);
							float st = Mathf.Sin(r);
                            
							vertices[0+extra2] = RotatePoint((Vector3.right*_scale.x)+(Vector3.up*_scale.y),angle,ct,st)+offset;
							vertices[1+extra2] = RotatePoint((Vector3.right*_scale.x)+(Vector3.down*_scale.y),angle,ct,st)+offset;
							vertices[2+extra2] = RotatePoint((Vector3.left*_scale.x)+(Vector3.up*_scale.y),angle,ct,st)+offset;
							vertices[3+extra2] = RotatePoint((Vector3.left*_scale.x)+(Vector3.down*_scale.y),angle,ct,st)+offset;
						}
						
						 
							Color32 _color32 = _color;
							colors[0+extra2] = _color32;
							colors[1+extra2] = _color32;
							colors[2+extra2] = _color32;
							colors[3+extra2] = _color32;

 
					}
					count = count+FlareElements[i].subElements.Count;
				}
                break;
			}
		}
    }
}