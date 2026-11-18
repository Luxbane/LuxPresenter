import uno
import os
import sys
import json
import time
import subprocess
import atexit
from com.sun.star.beans import PropertyValue

class ImpressController:
    def __init__(self, port=2002):
        self.port = port
        self.doc = None
        self.presentation = None
        self.controller = None
        self.desktop = None
        self.ctx = None
        self.soffice_proc = None
        self.is_running = False

    def ensure_libreoffice_running(self):
        # First test if already running on port
        if self.connect(timeout_sec=1.5):
            return True

        # Not running, auto-spawn LibreOffice
        program_dir = os.path.dirname(sys.executable) # LibreOffice/program/python.exe
        soffice_exe = os.path.join(program_dir, "soffice.exe")
        if not os.path.exists(soffice_exe):
            # Fallback relative to script
            soffice_exe = r"E:\LibreOffice\program\soffice.exe"

        user_dir = os.path.abspath(os.path.join(program_dir, "..", "user_profile")).replace("\\", "/")
        user_url = f"file:///{user_dir}"

        cmd = [
            soffice_exe,
            "--invisible",
            "--norestore",
            "--nologo",
            "--nodefault",
            f"-env:UserInstallation={user_url}",
            f"--accept=socket,host=127.0.0.1,port={self.port};urp;"
        ]

        try:
            self.soffice_proc = subprocess.Popen(
                cmd,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
            )
        except Exception as ex:
            sys.stderr.write(f"Failed to spawn LibreOffice: {ex}\n")
            return False

        # Wait for socket to become available
        return self.connect(timeout_sec=10)

    def connect(self, timeout_sec=10):
        start_time = time.time()
        while time.time() - start_time < timeout_sec:
            try:
                localContext = uno.getComponentContext()
                resolver = localContext.ServiceManager.createInstanceWithContext(
                    "com.sun.star.bridge.UnoUrlResolver", localContext
                )
                self.ctx = resolver.resolve(f"uno:socket,host=127.0.0.1,port={self.port};urp;StarOffice.ComponentContext")
                smgr = self.ctx.ServiceManager
                self.desktop = smgr.createInstanceWithContext("com.sun.star.frame.Desktop", self.ctx)
                return True
            except Exception:
                time.sleep(0.4)
        return False

    def load_presentation(self, file_path):
        if not self.desktop:
            raise RuntimeError("LibreOffice Desktop not connected")

        self.close_presentation()

        abs_path = os.path.abspath(file_path)
        file_url = uno.systemPathToFileUrl(abs_path)
        load_props = (
            PropertyValue("Hidden", 0, False, 0),
            PropertyValue("ReadOnly", 0, True, 0),
        )
        self.doc = self.desktop.loadComponentFromURL(file_url, "_blank", 0, load_props)
        if not self.doc:
            raise RuntimeError(f"Failed to load presentation: {file_path}")

        # Hide the Impress editor window so it never appears in front of FreeWorship
        try:
            frame = self.doc.getCurrentController().getFrame()
            container = frame.getContainerWindow()
            container.setVisible(False)
        except Exception:
            pass

        slide_count = self.doc.getDrawPages().getCount()
        self.presentation = self.doc.getPresentation()
        return slide_count

    def start_slideshow(self, display=1, fullscreen=False, initial_slide=0):
        if not self.presentation:
            raise RuntimeError("No presentation loaded")

        self.presentation.setPropertyValue("Display", int(display))
        self.presentation.setPropertyValue("IsFullScreen", bool(fullscreen))
        self.presentation.setPropertyValue("AllowAnimations", False)
        self.presentation.start()

        self.controller = None
        for _ in range(25):
            time.sleep(0.2)
            self.controller = self.presentation.getController()
            if self.controller:
                break

        if not self.controller:
            raise RuntimeError("Could not obtain SlideShowController")

        self.is_running = True
        if initial_slide > 0:
            self.controller.gotoSlideIndex(int(initial_slide))

        return {
            "slide_count": self.controller.getSlideCount(),
            "current_slide": self.controller.getCurrentSlideIndex()
        }

    def next_slide(self):
        if not self.controller:
            return None
        self.controller.gotoNextSlide()
        return {
            "current_slide": self.controller.getCurrentSlideIndex()
        }

    def prev_slide(self):
        if not self.controller:
            return None
        self.controller.gotoPreviousSlide()
        return {
            "current_slide": self.controller.getCurrentSlideIndex()
        }

    def goto_slide(self, index):
        if not self.controller:
            return None
        self.controller.gotoSlideIndex(int(index))
        return {
            "current_slide": self.controller.getCurrentSlideIndex()
        }

    def blank_screen(self, color=0):
        if self.controller:
            self.controller.blankScreen(int(color))

    def resume_screen(self):
        if self.controller:
            self.controller.resume()

    def stop_slideshow(self):
        if self.presentation:
            try:
                self.presentation.end()
            except Exception:
                pass
        self.controller = None
        self.is_running = False

    def close_presentation(self):
        self.stop_slideshow()
        if self.doc:
            try:
                self.doc.close(True)
            except Exception:
                pass
            self.doc = None
            self.presentation = None

    def terminate_all(self):
        self.close_presentation()
        if self.desktop:
            try:
                self.desktop.terminate()
            except Exception:
                pass
            self.desktop = None
        if self.soffice_proc:
            try:
                self.soffice_proc.terminate()
                self.soffice_proc.wait(timeout=2)
            except Exception:
                try:
                    self.soffice_proc.kill()
                except Exception:
                    pass
            self.soffice_proc = None


def main():
    port = 2002
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            pass

    controller = ImpressController(port=port)
    atexit.register(controller.terminate_all)

    if not controller.ensure_libreoffice_running():
        sys.stdout.write(json.dumps({"status": "error", "message": f"Could not connect to LibreOffice on port {port}"}) + "\n")
        sys.stdout.flush()
        return 1

    sys.stdout.write(json.dumps({"status": "ready", "port": port}) + "\n")
    sys.stdout.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            cmd = json.loads(line)
            action = cmd.get("action")
            req_id = cmd.get("id")

            result = {"status": "ok", "id": req_id}

            if action == "load":
                slide_count = controller.load_presentation(cmd["path"])
                result["slide_count"] = slide_count

            elif action == "start":
                display = cmd.get("display", 1)
                fullscreen = cmd.get("fullscreen", False)
                initial_slide = cmd.get("slide", 0)
                info = controller.start_slideshow(display=display, fullscreen=fullscreen, initial_slide=initial_slide)
                result.update(info)

            elif action == "next_slide":
                res = controller.next_slide()
                if res:
                    result.update(res)

            elif action == "prev_slide":
                res = controller.prev_slide()
                if res:
                    result.update(res)

            elif action == "goto_slide":
                res = controller.goto_slide(cmd["index"])
                if res:
                    result.update(res)

            elif action == "blank":
                controller.blank_screen(cmd.get("color", 0))

            elif action == "resume":
                controller.resume_screen()

            elif action == "stop":
                controller.stop_slideshow()

            elif action == "close":
                controller.close_presentation()

            elif action == "quit":
                controller.terminate_all()
                sys.stdout.write(json.dumps({"status": "bye", "id": req_id}) + "\n")
                sys.stdout.flush()
                break

            else:
                result = {"status": "error", "id": req_id, "message": f"Unknown action: {action}"}

            sys.stdout.write(json.dumps(result) + "\n")
            sys.stdout.flush()

        except Exception as e:
            sys.stdout.write(json.dumps({"status": "error", "message": str(e), "id": cmd.get("id") if 'cmd' in locals() else None}) + "\n")
            sys.stdout.flush()

    return 0

if __name__ == "__main__":
    sys.exit(main())
