# A racing game | My personal project
This is a work-in-progress game I am currently working on in my spare time. It's an arcade racing game with an emphasis on control and customization

## How It's Made:

**Tech used:** Unity and C#, Visual Studio

## Introduction
Hi. I am making this short presentation to showcase one of my many personal projects and go through my thought process while working on it, so by the end of this you'll have a better idea of who I am and what I can do regarding of algorythmic thinking and programming.

## The incentive
It all started with an idea: "Can I make a good racing game?"

https://github.com/user-attachments/assets/ff13c28f-f77b-4ff7-8c40-e7e99d8e9923

Which led to what you're seeing in the video capture above. It is far from a completely developed game, but it's getting there

## First steps
I kicked off by studying how a car works in real life and taking notes. I brainstormed ideas of how I was going ot make it happen in the digital world. My gamedev journal proved itself very useful in this stage. After I have all the information I need in front of me, I make schemes to tie it all together and have a concrede undertanding of how I am going to achieve something.

<img width="2188" height="1174" alt="image" src="https://github.com/user-attachments/assets/4d14622e-822c-4614-a0fc-fcdb5ae02700" />

I followed an online tutorial to get a very simple but working script which would allow my car to move using Unity's physics engine. The end script is way more complex and bears very little resemblance of the starting script, as the script I obtained from following the tutorial only acted as the base layer of what I was going to build.

## Start()

The Start() function, which is the first thing that runs once our game is loaded, initializes the preset for our car, which can come in all shapes and sizes.

<img width="436" height="225" alt="image" src="https://github.com/user-attachments/assets/4a1dfc7a-2e8e-418a-a22e-9866881e4f06" />

If we take a look at our preset script, we can define alot of properties. Like some cars have a higher rev capacity than others, or the car can be a manual or automatic.

<img width="893" height="822" alt="image" src="https://github.com/user-attachments/assets/06d23422-4ab0-40f4-bdb3-307662f7e9f5" />

I highlighted the torque curve, which is basically the personality of the car. previously to showcase this

<img width="2302" height="639" alt="image" src="https://github.com/user-attachments/assets/dc4bdaf9-94ed-4e66-ad12-649742739470" />

A torque curve inside the game editor, which is pretty close to a torque curve you'd get if you were to go to a special car service shop to test your sports car. And the game reads the data off this curve to manipulate the car's power

## Update()
Moving on to the functions Update() and FixedUpdate() which happen continuously.

<img width="658" height="592" alt="image" src="https://github.com/user-attachments/assets/c8d11d9e-f4f8-4103-875d-fce08a4f69c9" />

For the Move() script, I needed a good reference of acceleration and speed. Since I don't have access to a real life car, I have decided to reverse engineer how *EA Games* designed the acceleration for their racing game *Need for Speed: Most Wanted*. I have recorded myself playing the game, pushing the car to various speeds in various gears, and I mapped out the amount of seconds it took to go from one RPM range to another, from one gear to another and what speed I had at the end of it

<img width="769" height="385" alt="image" src="https://github.com/user-attachments/assets/2aadc0a5-392b-4c4d-9ad1-5b107b6e4441" />

With a little bit of math I was able to come up with a Look Up Table with a specific acceleration value for various RPM values

<img width="470" height="361" alt="image" src="https://github.com/user-attachments/assets/28fb73cd-d6c4-4ab7-aacf-e702099b4aa4" />

This is how our speed and acceleration are deduced

<img width="818" height="448" alt="image" src="https://github.com/user-attachments/assets/5dd06cfd-fb50-4b38-9d5d-bc40be841026" />

And here's the code

<img width="898" height="651" alt="image" src="https://github.com/user-attachments/assets/c173f388-074a-4779-9035-5261ceeef28f" />

The foreach here applies the power evenly to all wheels which push the car forward (since some cars are being moved only by the front or rear wheels, while others have an all-wheel drive)

## The steering

<img width="1211" height="353" alt="image" src="https://github.com/user-attachments/assets/8a5ce0bf-d222-45e0-baa7-60ea298eae92" />

We want to steer in dependence of our speed and we can very easily do that using a curve. This make it easier later on if we want to introduce a tuning feature for our car

<img width="1341" height="659" alt="image" src="https://github.com/user-attachments/assets/b78d97a8-db19-4f33-99f8-b1993ea6f772" />

## The braking
In our presets config we defined a brakeDistance variable, which is the distance it takes the car to come to a full stop from 100 km/h. With more maths, we convert this to a force which we apply to our car when stopping

<img width="750" height="455" alt="image" src="https://github.com/user-attachments/assets/cbca309e-b8b1-42c2-8810-debf1edbfd53" />

## The drifting
Here we make our game a little more fun by implementing drifting. We start off with calculating the drift angle which is the angle between where the car is facing and where it's moving. Then we find out if we are spinning out or performing a clutch kick (which is a method of initiating a drift in motorsport by quickly engaging and disengaging the cltuch while holding the throttle all the way down, which causes the rear wheels to spin faster and start to drift)

<img width="954" height="641" alt="image" src="https://github.com/user-attachments/assets/560f86a2-3ac3-4f53-ae58-2f8df4eb1803" />

After determining that we are eligible for a drift, we make the rear tires more slippery and let the game engine do the rest

<img width="986" height="529" alt="image" src="https://github.com/user-attachments/assets/0674bc0f-8924-464b-af80-a3ee91c5e20a" />

## Conclusion
There is alot of room for improvement and new features to think about, but I'd say the current scripts serve as a pretty solid ground and starting point to what could be a good and fun racing game, implementing the best of both worlds, arcade and simulation racing games.
