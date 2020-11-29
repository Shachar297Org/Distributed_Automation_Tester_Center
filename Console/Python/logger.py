import logging


class Logger:
    def __init__(self, name, logFilePath):
        formatString = '%(asctime)s: [%(levelname)s]:[{}]: %(message)s'.format(
            name)
        logging.basicConfig(filename=logFilePath,
                            filemode='w', level=logging.INFO, format=formatString)

    def WriteDebug(self, msg: str):
        logging.debug(msg)
        print(msg)

    def WriteInfo(self, msg: str):
        logging.info(msg)
        print(msg)

    def WriteWarning(self, msg: str):
        logging.warning(msg)
        print(msg)

    def WriteError(self, msg):
        logging.warning(msg)
        print(msg)

    def WriteLog(self, msg: str, level: str):
        if level == 'debug':
            self.WriteDebug(msg)
        elif level == 'info':
            self.WriteInfo(msg)
        elif level == 'warning':
            self.WriteWarning(msg)
        elif level == 'error':
            self.WriteError(msg)
