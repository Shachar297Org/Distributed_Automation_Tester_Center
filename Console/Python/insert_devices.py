import sys
import requests
import json
import configparser
from utils import *
from logger import *
from sql import *
from record import *


def ConnectRDS(host: str, db: str, username: str, password: str):
    """
    Connect to RDS and return connection object
    """
    dbConn = None
    try:
        dbConn = DbConnector(host, db, username, password)
        if dbConn:
            return dbConn
        else:
            return None
    except Exception as ex:
        print(ex)
        return None


def GetDeviceEntries(dbConn: object):
    """
    Return all device entries from database
    """
    tableName = '.'.join(['DeviceService', 'Devices'])
    query = "SELECT * FROM {} as devices left join DeviceService.BaseDevice as baseDevices on devices.manufacturerId=baseDevices.manufacturerId".format(
        tableName)
    resultsSet = dbConn.ExecuteQuery(tableName, query)
    return resultsSet


def CollectDevicesFromPortal(configParser, logger):
    """
    Collect devices list from the portal and return list of their records {GA, SN}
    """
    host = configParser.get('RDSConnection', 'host')
    db = configParser.get('RDSConnection', 'db')
    username = configParser.get('RDSConnection', 'username')
    password = configParser.get('RDSConnection', 'password')

    # Connect to RDS
    simFlag = configParser.get('Debug', 'simulate')
    if simFlag:
        return [
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-1'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-2'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-3'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-4'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-5'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-6'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-7'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-8'},
            {'deviceType': 'GA-0000180', 'deviceSerialNumber': 'ELAD-TEST-9'},
            {'deviceType': 'GA-0000180', 'deviceSerialNumber': 'ELAD-TEST-10'},
            {'deviceType': 'GA-0000180', 'deviceSerialNumber': 'ELAD-TEST-11'},
            {'deviceType': 'GA-0000080CN', 'deviceSerialNumber': 'ELAD-TEST-12'},
            {'deviceType': 'GA-0000180',
                'deviceSerialNumber': 'ELAD-TEST-13'},  # new
            {'deviceType': 'GA-0000180',
                'deviceSerialNumber': 'ELAD-TEST-14'},  # new
            {'deviceType': 'GA-0000180',
                'deviceSerialNumber': 'ELAD-TEST-15'},  # new
        ]
    else:
        deviceRecords = []
        dbConn = ConnectRDS(host, db, username, password)
        if dbConn:
            logger.WriteLog('Connected to RDS.', 'info')
            deviceEntries = GetDeviceEntries(dbConn)
            deviceRecords = [ConvertEntryToDeviceRecord(
                entry) for entry in deviceEntries]
        else:
            logger.WriteLog('Cannot connect to RDS.', 'error')

    return deviceRecords


def SendRequestLogin(configParser: object, logger: object):
    """
    Sends to api host login request and return access token
    """
    loginHost = configParser.get('api', 'login_host')
    loginData = {
        "email": configParser.get('api', 'username'),
        "password": configParser.get('api', 'password')
    }
    response = requests.post(
        url=loginHost, headers={'Content-Type': 'application/json'}, data=json.dumps(loginData))
    if not response.ok:
        logger.WriteLog('Request failed. status code: {}'.format(
            response.status_code), 'error')
        return None
    jsonObj = response.json()
    return jsonObj['accessToken']


def InsertDevice(accessToken: str, deviceRecord: dict, configParser: object, logger: object):
    """
    Insert new device to portal using access token
    """
    host = configParser.get('api', 'insert_device')
    response = requests.post(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)}, data=json.dumps({'createLumDeviceRequest': deviceRecord}))
    if response.ok:
        logger.WriteLog('Device device {} was inserted successfully.'.format(
            deviceRecord), 'info')
    else:
        logger.WriteLog('Error: cannot insert device {}. status code: {}. {}'.format(
            deviceRecord, response.status_code, response.text), 'error')


def GetDeltaDevices(devicesList1, devicesList2):
    """
    Return new list containing devices from devices list 1 that do not exist in devices list 2
    """
    deltaDevices = []
    for deviceRec in devicesList1:
        if deviceRec not in devicesList2:
            deltaDevices.append(deviceRec)
    return deltaDevices


def InsertDevices(env: str, deviceRecords: dict, configParser: object, logger: object):
    """
    Insert new devices from csv file to portal
    """
    accessToken = SendRequestLogin(configParser, logger)
    if not accessToken:
        logger.WriteLog('Login failed.', 'error')
        return
    logger.WriteLog('Login success.', 'info')
    for deviceRecord in deviceRecords:
        logger.WriteLog('Inserting device: {}'.format(deviceRecord), 'info')
        InsertDevice(accessToken, deviceRecord, configParser, logger)


if __name__ == "__main__":
    print('-----insert_devices-----')
    print('Arguments: {}'.format(sys.argv))

    try:
        if len(sys.argv) != 3:
            print('Enter 2 arguments: env and csv file')
            exit(1)

        env = sys.argv[1]
        csvFile = sys.argv[2]
        print('arg 1: env: {}'.format(env))
        print('arg 2: Csv file: {}'.format(csvFile))

        if not os.path.exists(csvFile):
            print('Csv file {} not exist'.format(csvFile))
            exit(2)

        configParser = LoadConfig()
        logger = InitLogger(configParser)

        portalDeviceRecords = CollectDevicesFromPortal(configParser, logger)
        csvDeviceRecords = ReadRecordsFromCsvFile(csvFile)
        deltaDevices = GetDeltaDevices(csvDeviceRecords, portalDeviceRecords)

        InsertDevices(env, deltaDevices, configParser, logger)

        print('-----success-----')

    except Exception as ex:
        print('Error: {}'.format(ex))
        print('-----fail-----')
        exit(1)
