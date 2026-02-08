# ![Logo](/resources/icon_readme.png) VRC Tracked Objects *(powered by OSC)*

A project to bring real world objects into the virtual world of VRChat. The objects need to be added to an avatar and are tracked with Vive trackers.

# Getting Started

Before getting started, make sure you have the ".NET Desktop Runtime 6.0" installed (latest version at time of writing is 6.0.29). You can download it [here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).
Make sure you specifically download and install the **Desktop Runtime**, since the app is not going to run otherwise.

Afterwards download the latest version of the software from the [Releases](https://github.com/jangxx/VRC-OSC-TrackedObjectApp/releases/latest) page. There are two downloads, the app itself and a Unity package containing the required avatar setup. To actually put it on the avatar, you can either use [VRCFury](https://vrcfury.com/) or a tool like [VRCAvatars3Tools](https://booth.pm/en/items/2207020), which you can download from booth for free in order to use the _AnimatorControllerCombiner_ and the _ExpressionParametersCombiner_.

## Setup (VRCFury)

This section explains the initial setup of the avatar when using VRCFury.
The later [Calibration](#calibration) section is exactly the same between using VRCfury or setting up the avatar manually.

1. Unpack the downloaded files to a location of your liking. The app does not need to be installed and can be run by just clicking on the executable, but you might want to copy it to `C:\\Program Files\VRC Tracked Objects` for example.

2. Before you can calibrate the avatar, you will need to add the required setup to the avatar. Drag and drop the Unity package you downloaded into Unity to open it. Afterwards move the `TrackedObject VRCFury` prefab into the root of your avatar.

3. Click on `TrackedObject VRCFury` in the hierarchy and in the "Armature Link" component, change the dropdown either to "Right Hand" (default) or "Left Hand". Where you put it will determine to which hand the object will be relatively placed so take into consideration with which hand you are going to hold the object more often. Due to the relative positioning, the tracking is by far the most stable when held in the hand to which the container is anchored, so if you're adding a bottle for example, anchoring it to your dominant hand is going to be the best option. If you enter playmode, the debug cube should now be moved into the wrist of your avatar.

4. Upload the avatar as a new version.

After these steps your avatar is fully set up for the next step, i.e. the [Calibration](#calibration).
In a later step you will remove the debug cube and replace it with the object you actually want to track and then upload it again.

## Setup (manually)

This section explains the initial setup of the avatar when merging controllers and parameters manually.
The later [Calibration](#calibration) section is exactly the same between setting up the avatar manually or using VRCFury.
This method assumes that you have already set up your avatar with a custom FX layer and custom parameters as well as an expressions menu.
If you don't know how to do these things you need to look for another tutorial on basic prop toggles for example, since this section assumes at least basic knowledge of Unity.

If you prefer to watch a video tutorial of this whole process, you can find it on [YouTube](https://youtu.be/y6I-t1YBorY).

1. Unpack the downloaded files to a location of your liking. The app does not need to be installed and can be run by just clicking on the executable, but you might want to copy it to `C:\\Program Files\VRC Tracked Objects` for example.

2. Before you can calibrate the avatar, you will need to add the required setup to the avatar. Drag and drop the Unity package you downloaded into Unity to open it. Afterwards move the `TrackedObject Package` into your scene and make sure it's located at `0,0,0`.

3. Unpack the `TrackedObject Package` prefab.

4. Move the `TrackedObject Container` into the root of your avatar and the `TrackedObject Anchor` either into the right hand or left hand bone transform. Where you put it will determine to which hand the object will be relatively placed so take into consideration with which hand you are going to hold the object more often. Due to the relative positioning, the tracking is by far the most stable when held in the hand to which the container is anchored, so if you're adding a bottle for example, anchoring it to your dominant hand is going to be the best option. Set the position and rotation of the Anchor object to be all 0 (that normally causes the calibration cube to sit within the wrist).

5. Find the VRCAvatars3Tools in the Unity menu and open the _AnimatorControllerCombiner_. Set the included _FX Layer Layers_ as the source controller and the FX layer on your avatar as the destination. Afterwards copy all layers and paramters by clicking on **Combine**.

6. Next you need to open the _ExpressionParametersCombiner_ from the same VRCAvatars3Tools. Set the included _Expression Parameters_ as the source and the VRCExpressionParameters object on your avatar as the destination. Then click **Combine**.

7. Finally add a Four Axis puppet to your Expression menu which has the three `OscTrackedPos` parameters on it,. Set the Parameter option to `OSCTrackingEnabled` so that the parameter gets set to `true` when the menu is open and `false` when it is closed. This will cause the object to only track and be visible when the menu is open and the parameters are IK synced.  
Example:  
![menu setup](/resources/screenshot_2.png)

8. Upload the avatar as a new version.

After these steps your avatar is fully set up for the next step, i.e. the [Calibration](#calibration).
In a later step you will remove the debug cube and replace it with the object you actually want to track and then upload it again.

## Calibration

1. Start SteamVR and connect at least the controller of the hand you chose as the anchor as well as the tracker you want to use for the object. Open the app you downloaded and unpacked in step 1. to be greeted with this window:  
![menu setup](/resources/screenshot_1.png).  
Enter a name for your first configuration (e.g. the name of your avatar) and click "Add configuration".
Open the "Avatars" tab and copy-paste the Avatar ID from Unity into the respective input (you can find the Avatar ID for example in the `Pipeline Manager` on your avatar root or in the Content Manager section in the VRChat SDK window). Enter a name for the avatar and then click **Add**.

2. Select your controller and tracker from the respective drop-down menus. Afterwards you have to start VRChat and switch into your freshly uploaded avatar. For the actual calibration I would recommend you to sit down on your desk with your VR headset so that you can still reach the keyboard while wearing it. Click on the **Start calibration** button to have three sides of a cube appear in the location of your controller. The task is now to align this cube with the one you have added to your avatar. To do this, use the arrow keys on your keyboard to switch through the seven different inputs. Up and down on the keyboard increments and decrements the value respectively, while left and right will switch to the next and previous input. Make sure that the arrows pointing from `X Neg` to `X Pos`, `Y Neg` to `Y Pos` and `Z Neg` to `Z Pos` point along the respective axes on the debug cube, while being perfectly in line with the sides of the cube. Also ensure that the scale matches. If you are satisfied with the result, click on **Stop calibration** to finish the calibration process.

3. You are now ready for the first test! Switch to the "Tracking" tab and click on **Start Tracking**. In VRChat go to the OSC section of the Action Menu in order to reset the OSC config (so that it includes the new parameters you added). This action should also reload your avatar so that the tracking app can pick up the avatar change. If the "Current status" still says "Inactive (unkown avatar)", switch to another avatar and back so that the app can get notified of the change. Open the Action Menu again, go to your expressions and open the Four Axis puppet labeled "Tracked Object". If you did everything correctly, the cube should now follow the tracker you chose! We are almost done at this point.

4. Open the "File" menu within the tracking app and click "Save config" to save the config to the default location.

5. Go back to Unity and replace the debug cube with an object of your liking. Alternatively you can also change the Tag on the debug cube to *EditorOnly* and disable the object. This will prevent it from being uploaded and taking up a material slot, but you can easily re-enable it if you need to get a sense for the orientation of a tracked object later.

6. Finally toggle the `Container` off so that your object is hidden by default. It will automatically get toggled on when the menu is open and the object is tracking, but it will be hidden otherwise.

# Normal usage

While the initial setup of the system is rather involved, actually using it is really easy. Simply launch the app and click on start tracking.
You can then jump into VRChat and see the tracked object by simply opening the Four Axis puppet menu.

# The user interface

This is an overview over the entire interface of the app as well as an explanation of what each part does.

![entire interface](/resources/explanation_screenshots.png)

1. If this is checked, tracking will begin immediately after the program is launched and a config has been saved to the default location or is supplied as the first launch parameter as a path to a config file.

2. Configure your OSC input and output addresses here. Both are needed, because we need bidirectional communication with VRChat. On the one end we need to listen for the enable parameter and on the other end we need to send in the tracking values.

3. Here you can select your controller and tracker that the tracking is realtive to. The refresh button queries SteeamVR for a list of controllers. If a name is followed by `(Not found)` it means that the serial number was specified in the config file, but the controller or tracker is not currently connected. After you have connected the tracker, hit `Refresh` to have the app see the device properly.

4. These are the parameters that the app publishes and listens to. The `Activate` parameter is optional. If the field is left blank, tracking data will be fed into the game as soon as a compatible avatar is changed into. 

5. This status field can show the statuses `active` when a compatible avatar is worn and tracking data is sent, `inactive (unknown avatar)` if the current avatar is not compatible, `inactive (disabled)` when the `Activate` parameter is set to false and `inactive` when tracking has not been started.

6. This button attempts to start tracking. It will show an error if the controller or tracker is not connected or if required fields are empty.

7. This is the global configuration selector. Here you can choose the configuration you want to calibrate, add avatars to or setup parameters for.

8. Here you can see and edit the calibration values. Do note that these values are only read at the very beginning of the tracking and calibration process. It is therefore not possible to live edit calibration values, not even while in the calibration procedure.

9. This buttons starts the calibration procedure. The currently active field will be highlighted in red. Pressing `Left Arrow` and `Right Arrow` on the keyboard switches between the different fields, while pressing `Up Arrow` and `Down Arrow` increments and decrements the values respectively.

10. This is the list of avatars currently existing in the active configuration. It is currently not possible to edit avatar names or IDs in the app directly, so if you want to rename an avatar, you need to do it in the config file directly.
If you right click an entry in the list, you can copy its ID or move the avatar to a different configuration.

11. Here you can add a new avatar to the active configuration. As mentioned before it's currently not possible to edit a configured avatar so make sure that the ID and name is correct.

# Working principle

Bringing externally tracked objects into VRChat comes with one main challenge: How do we synchronize the tracking universe of SteamVR with the transform of the avatar.
The solution I came up with is this: Find a point on the avatar that has a (mostly) fixed offset to a tracked device in SteamVR. Ideally we could use the hip for this but:

1. not everyone has FBT
2. the relative position of the hip tracker to the hip bone changes every time the avatar is recalibrated.

The next option would be the headset, but unfortunately the `Head` bone is not actually locked to the headset position with a static offset either. Instead it _mostly_ follows it, but especially when looking up and down it is very common for the HMD position and the `Head` bone to diverge quite a bit.

The final option are the controllers and this is what I went with. The offset between the position of the controller and the `(Right|Left)Hand` bones is not completely static either, but from all available options it is by far the best one. Using a controller also comes with the advantage that the offset between the tracker and the controller will be essentially static if the tracker is held in the same hand, so for objects that are supposed to be picked up, using the controller is also the option with the lowest jitter.

So then, with the controller's and the tracker's position in hand we can just start tracking, right? Unfortunately not yet.
Each controller is different, but on most of them, the point that is actually tracked is the very tip of the controller, which obviously does not line up with the root of the `Hand` bone.
In order to find this offset, a calibration step is needed.
The result of the calibration is a 4x4 transformation matrix including scale which lets us calculate the position of the root of the `Hand` pretty accurately.

The avatar is then set up in such a way that six nested GameObjects are used to translate and rotate a virtual object in all axes of position and rotation relative to the `Hand` bone.

Afterwards the final calculation we need to do is:

1. Calculate the inverse of the controller matrix and multiply it with the tracker matrix to get the transform from the controller to the tracker.
2. Calculate the inverse of the controller -> `Hand` bone matrix (i.e. the calibration matrix) in order to get the transform from the `Hand` bone to the controller (this is of course not done once per update but only one time when tracking is started).
3. Multiply the inverse of the calibration matrix with the controller -> tracker transform from step 1). This gives us a transform from the `Hand` bone -> controller -> tracker, i.e. a transform from the hand to the tracker.
4. Extract the position and rotation from the resulting matrix. The position is simply the last column and the rotation can be extracted from the upper left 3x3 submatrix. To extract the rotation we actually need to invert the rotation component again however, since we are not interested in the rotation required to rotate an object from the `Hand` bone to the tracker, but instead the inverse rotation to counteract the relative rotation between the controller and the tracker. The virtual object already rotates with the controller after all, since it is parented to it. This needs to be undone to get the virtual object to line up with the tracker.

# Troubleshooting

## The object moves in weird ways

Check that the calibration is correct. Make sure that the calibration cube has exactly the same size on your avatar as it has when you first put it into the scene (that means its scale should be 1 if your avatar is not scaled and `1/scale` if it is).

**If using the manual setup method:** Also make sure that the Anchor is actually at (0,0,0) and with (0,0,0) rotation relative to the hand bone. Technically this is not strictly neccessary, but for your own sanity it is way easier to do all the transforming and rotating in the tracking app than doing some of it in Unity and some of it in the tracking app. Normally the tracking cube should sit in your wrist and because you remove it after the calibration anyway, there is no need to move it around to make it look like you're holding it in your hand.

## The tracking suddenly got worse

The most common reason for this problem that I have found is changes in the scale between the real world and VRChat.
This can happen in a variety of ways:

- When changing the "real height" in the settings
- When changing size measurement between wingspan and height
- probably others too

If you changed _anything at all_ that might be related to your scale in VRChat, try to run the calibration again and see if the size of the cubes still line up.
More often than not they don't anymore and small tweaks to the scale are neccessary.
Luckily it's only the scale that gets messed up so this calibration step should not take more than a few seconds.
