import sys, requests, shutil
from PyQt5.QtWidgets import QApplication, QWidget


def download():
    test_url = 'https://storage.googleapis.com/ygoprodeck.com/pics/27551.jpg'

    r = requests.get(test_url, stream=True)
    if r.status_code == 200:
        with open("test.jpg", 'wb') as f:
            r.raw.decode_content = True
            shutil.copyfileobj(r.raw, f)      


def main():

    app = QApplication(sys.argv)

    w = QWidget()
    w.resize(250, 150)
    w.move(300, 300)
    w.setWindowTitle('Simple')
    w.show()

    sys.exit(app.exec_())


if __name__ == '__main__':
    download()
    #main()