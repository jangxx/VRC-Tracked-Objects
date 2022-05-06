# ![Logo](/resources/icon_readme.png) VRC Tracked Objects *(powered by OSC)*

A project to bring real world objects into the virtual world of VRChat. The objects need to be added to an avatar and are tracked with Vive trackers.

## Getting Started

Download the latest version of the software from the [Releases](https://github.com/jangxx/VRC-OSC-TrackedObjectApp/releases/latest) page. There are two downloads, the app itself and a Unity package containing the required avatar setup. You will also need the [VRCAvatars3Tools](https://booth.pm/en/items/2207020), which you can download from booth for free in order to use the _AnimatorControllerCombiner_ and the _ExpressionParametersCombiner_.

## Setup

This section explains the initial setup, both of the software and also the avatar. If you prefer to watch a video tutorial of this whole process, you can find it on [YouTube](https://youtu.be/y6I-t1YBorY).
This section assumes that you have already set up your avatar with a custom FX layer and custom parameters as well as an expressions menu.
If you don't know how to do these things you need to look for another tutorial on basic prop toggles for example, since this section assumes at least basic knowledge of Unity.

1. Unpack the downloaded files to a location of your liking. The app does not need to be installed and can be run by just clicking on the executable, but you might want to copy it to `C:\\Program Files\VRC Tracked Objects` for example.

2. Before you can calibrate the avatar, you will need to add the required setup to the avatar. Drag and drop the Unity package you downloaded into Unity to open it. Afterwards move the `TrackedObject Package` into your scene and make sure it's located at `0,0,0`.

3. Unpack the `TrackedObject Package` prefab.

4. Move the `TrackedObject Container` into the root of your avatar and the `TrackedObject Anchor` either into the right hand or left hand bone transform. Where you put it will determine to which hand the object will be relatively placed so take into consideration with which hand you are going to hold the object more often. Due to the relative positioning, the tracking is by far the most stable when held in the hand to which the container is anchored, so if you're adding a bottle for example, anchoring it to your dominant hand is going to be the best option.

5. Find the VRCAvatars3Tools in the Unity menu and open the _AnimatorControllerCombiner_. Set the included _FX Layer Layers_ as the source controller and the FX layer on your avatar as the destination. Afterwards copy all layers and paramters by clicking on **Combine**.

6. Next you need to open the _ExpressionParametersCombiner_ from the same VRCAvatars3Tools. Set the included _Expression Parameters_ as the source and the VRCExpressionParameters object on your avatar as the destination. Then click **Combine**.

7. Finally add a Four Axis puppet to your Expression menu which has the three `OscTrackedPos` parameters on it, as well as one of the `OscTrackedRot` ones (personally I chose `RotX` but it doesn't really matter). Set the Parameter option to `OSCTrackingEnabled` so that the parameter gets set to `true` when the menu is open and `false` when it is closed. This will cause the object to only track and be visible when the menu is open and the parameters are IK synced.  
Example:  
![menu setup](/resources/screenshot_2.png)

8. Upload the avatar as a new version.

After these steps your avatar is fully set up for the next step, i.e. the calibration.
In a later step you will remove the debug cube and replace it with the object you actually want to track and the upload it again.

9. Start SteamVR and connect at least the controller of the hand you chose as the anchor in step 4 as well as the tracker you want to use for the object. Open the app you downloaded and unpacked in step 1. to be greeted with this window:  
![menu setup](/resources/screenshot_1.png).  
Open the "Avatars" tab and copy-paste the Avatar ID from Unity into the respective input (you can find the Avatar ID for example in the `Pipeline Manager` on your avatar root or in the Content Manager section in the VRChat SDK window). Enter a name for the avatar and then click **Add**.

10. Select your controller and tracker from the respective drop-down menus. Afterwards you have to start VRChat and switch into your freshly uploaded avatar. For the actual calibration I would recommend you to sit down on your desk with your VR headset so that you can still reach the keyboard while wearing it. Click on the **Start calibration** button to have three sides of a cube appear in the location of your controller. The task is now to align this cube with the one you have added to your avatar. To do this, use the arrow keys on your keyboard to switch through the seven different inputs. Up and down on the keyboard increments and decrements the value respectively, while left and right will switch to the next and previous input. Make sure that the arrows pointing from `X Neg` to `X Pos`, `Y Neg` to `Y Pos` and `Z Neg` to `Z Pos` point along the respective axes on the debug cube, while being perfectly in line with the sides of the cube. Also ensure that the scale matches. If you are satisfied with the result, click on **Stop calibration** to finish the calibration process.

11. You are now ready for the first test! Switch to the "Tracking" tab and click on **Start Tracking**. In VRChat go to the OSC section of the Action Menu in order to reset the OSC config (so that it includes the new parameters you added). This action should also reload your avatar so that the tracking app can pick up the avatar change. If the "Current status" still says "Inactive (unkown avatar)", switch to another avatar and back so that the app can get notified of the change. Open the Action Menu again, go to your expressions and open the Four Axis puppet you added in step 7. If you did everything correctly, the cube should now follow the tracker you chose! We are almost done at this point.

12. Open the "File" menu within the tracking app and save the config to a file.

13. Go back to Unity and replace the debug cube with an object of your liking (or place it next to it within the `Container` object). It can be a good idea to do this _after_ you have already put the tracker on the object you want to track with the debug cube still visiable so that you can get a sense for the orientation.

14. Finally toggle the `Container` off so that your object is hidden by default. It will automatically get toggled on when the menu is open and the object is tracking, but it will be hidden otherwise.

# Normal usage

While the initial setup of the system is rather involved, actually using it is really easy. Simply launch the app, load the config under "File > Open config file" and click on start tracking.
You can then jump into VRChat and see the tracked object by simply opening the Four Axis puppet menu.

If you want to streamline the process even more, you can check "Start tracking when launched from config file", save the config again and then set up a shortcut that has the path of the config file as its first parameter.
This way you can click on a single shortcut which will start the app and start tracking immediately (as long as the controller and tracker are connected).

# Troubleshooting

**coming soon!**