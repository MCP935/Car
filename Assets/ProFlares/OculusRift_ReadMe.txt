
This version of ProFlares was tested with - OVR V0.2.5


To add Oculus Rift Support to ProFlares. 

1 - Import the latest Oculus Rift SDK to your project.

2 - Then Unzip the OculusRift.zip, This will add an extra option to your ProFlares Menu.

3 - To use, select your OVRCameraController then use option. : 'Window->ProFlares->Create VR Setup On Selected OVR Controller'

    This will add a ProFlares VR setup, this includes Two ProFlareBatch components.

4 - Add the same ProFlareAtlas to both.
    
    This is the basic setup complete, you can now add flares two your scene.


VR Flare Depth - 
This is a new setting on a ProFlareBatch that is setup in VR mode. It used to control the depth of that is added to a flare. Zero is completely flat, 0.2 is Default. Any higher than 0.5 may cause to much depth and cause eye strain.

NOTE - If modifying this value make sure that both the left and right ProFlareBatch have the same Value!

