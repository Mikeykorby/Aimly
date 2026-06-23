> [!NOTE]
> If you enjoy Aimly, please consider giving us a star ⭐! We appreciate it! :)

<div align="center">
  <a href="https://aimmy.dev/" target="_blank">
    <img width="100%" src="readme_assets/AimmyV2Banner.png" alt="Aimly Banner">
  </a>
</div>

**Aimly** is a universal AI-Based Aim Alignment Mechanism forked from Aimmy to provide a way better experience that actually listens to the community!

Unlike most AI-Based Aim Alignment Mechanisms, Aimly utilizes DirectML, ONNX, and YOLOV8 to detect players, offering both higher accuracy and faster performance compared to other Aim Aligners, especially on AMD GPUs, which would not perform well on Aim Alignment Mechanisms that utilize TensorRT.

Aimly also provides an easy to use user-interface, a wide set of features and customizability options tailored explicitly to community requests which makes Aimly a great option for anyone who wants to use and tailor an Aim Alignment Mechanism for a specific game without having to code.

Aimly is 100% free to use. This means no ads, no key system, and no paywalled features. Aimly is not, and will never be for sale for the end user, and is considered a source-available product, **not open source** as we actively discourage other developers from making commercial forks of Aimly.

Please do not confuse Aimly as an open-source project, we are not, and we have never been one.

* **Want to connect with us?** Join our Discord Server: https://discord.gg/aimmy
* **If you want to share Aimly with your friends, our website is:** https://aimmy.dev/

---

## Table of Contents
- [What is the purpose of Aimly?](#what-is-the-purpose-of-aimly)
- [How does Aimly Work?](#how-does-aimly-work)
- [Features](#features)
- [Setup](#setup)
- [How is Aimly better than similar AI-Based tools?](#how-is-aimly-better-than-similar-ai-based-tools)
- [How the hell is Aimly free?](#how-the-hell-is-aimly-free)
- [How do I train my own model?](#how-do-i-train-my-own-model)
- [How do I upload my model to the "Downloadable Models" menu](ModelUpload.md)

---

## What is the purpose of Aimly?
### Aimly was designed for Gamers who are at a severe disadvantage over normal gamers.
### This includes but is not limited to:
- Gamers who are physically challenged
- Gamers who are mentally challenged
- Gamers who suffer from untreated/untreatable visual impairments
- Gamers who do not have access to a seperate Human-Interface Device (HID) for controlling the pointer
- Gamers trying to improve their reaction time
- Gamers with poor Hand/Eye coordination
- Gamers who perform poorly in FPS games
- Gamers who play for long periods in hot environments, causing greasy hands that make aiming difficult 

---

## How does Aimly Work?
```mermaid
flowchart LR
    A["Playing Game System"]
    C["Screen Grabbing Functionality"]
    B["YOLOv8 (DirectML + ONNX) Recognition"]
    D{"Making Decision"}
    DA["X+Y Adjustment"]
    DB["FOV"]
    E["Triggering Functionality"]
    F["Mouse Cursor"]

    A --> E --> C --> B --> D --> F 
    DA --> D
    DB --> D
