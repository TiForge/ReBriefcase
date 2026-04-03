# ReBriefcase
Aims to recreate the briefcases found in Windows 7, with better conflict detections and a cleaner UI. Not compatable with native briefcases.

# How to use ReBriefcase
The way ReBriefcase works is it turns folders into briefcases. These "briefcases" however, are still folders, they just have metadata and an icon embedded. This lets you use ReBriefcase briefcases fairly similarly to Windows briefcases.
<img width="294" height="159" alt="Screenshot_01" src="https://github.com/user-attachments/assets/0759ab77-e9e6-43b7-a5e6-968fd0ebfb2c" />

# The sync engine
ReBriefcase has two sync engines. "Safe" and "Unsafe"

<img width="466" height="212" alt="Screenshot_02" src="https://github.com/user-attachments/assets/5018791a-aaa9-4b97-a755-2210cf2b968a" />
The "Unsafe" sync engine is what ReBriefcase uses when it does not have a point of reference (from a previous sync). It does not have conflict detection, and prioritizes the largest version of files to try and save the most data possible.

The "Safe" sync engine is what ReBriefcase uses when does have a point of reference (from a previous sync). It does have conflict detection, but unfortunately as of now there is no way to keep multiple versions of the same file.

# Conflict detection

The ReBriefcase conflict detection window differs greatly compared to the Windows briefcase conflict detection window. The goal was to make it easier to use quickly, and to make it more intuitive for causual users.
<img width="445" height="288" alt="Screenshot_03" src="https://github.com/user-attachments/assets/19625d57-5951-4b23-af1e-f0c466db07cb" />
