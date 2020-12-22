

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


def RetrieveDevicesFromPortal(config: object):
    """
    Retrieve device list from portal and return list of their records {GA, SN}
    """
    simFlag = True if config['RDS_SIM'] == 'True' else False
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
        ]

    accessToken = SendRequestLogin(config)
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success.')

    getDeviceHost = config['API_SEARCH_DEVICE']
    response = requests.get(url=getDeviceHost, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)})
    if response.ok:
        print('Devices list was retrieved successfully.')
        jsonObj = response.json()
        jsonData = jsonObj['data']
        deviceRecords = []
        for jsonDeviceObj in jsonData:
            deviceType = jsonDeviceObj['deviceInfo']['deviceType']
            serialNum = jsonDeviceObj['deviceInfo']['deviceSerialNumber']
            deviceRec = {'deviceType': deviceType,
                         'deviceSerialNumber': serialNum}
            deviceRecords.append(deviceRec)
        return deviceRecords, accessToken
    else:
        print('Error: cannot retreive devices from portal. status code: {}.'.format(
            response.status_code))
        raise Exception('Error: {}: {}'.format(
            response.status_code, response.text))


def CollectDevicesFromPortalOld(config: object):
    """
    Collect devices list from the portal and return list of their records {GA, SN}
    """
    host = config['RDS_HOST']
    db = config['RDS_DB']
    username = config['RDS_USER']
    password = config['RDS_PASS']

    # Connect to RDS
    simFlag = True if config['RDS_SIM'] == 'True' else False
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
        ]
    else:
        deviceRecords = []
        dbConn = ConnectRDS(host, db, username, password)
        if dbConn:
            print('Connected to RDS.')
            deviceEntries = GetDeviceEntries(dbConn)
            deviceRecords = [ConvertEntryToDeviceRecord(
                entry) for entry in deviceEntries]
        else:
            print('Error: Cannot connect to RDS.')

    return deviceRecords


def SendRequestLogin(config: object):
    """
    Sends to api host login request and return access token
    """
    loginHost = config['API_LOGIN_HOST']
    loginData = {
        "email": config['API_USER'],
        "password": config['API_PASS']
    }
    response = requests.post(
        url=loginHost, headers={'Content-Type': 'application/json'}, data=json.dumps(loginData))
    if not response.ok:
        print('Request failed. status code: {}'.format(response.status_code))
        return None
    jsonObj = response.json()
    return jsonObj['accessToken']


def InsertDevice(accessToken: str, deviceRecord: dict, config: object):
    """
    Insert new device to portal using access token
    """
    host = config['API_INSERT_DEVICE']
    response = requests.post(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)}, data=json.dumps({'createLumDeviceRequest': deviceRecord}))
    if response.ok:
        print('Device device {} was inserted successfully.'.format(deviceRecord))
    else:
        print('Error: cannot insert device {}. status code: {}. {}'.format(
            deviceRecord, response.status_code, response.text))


def GetDeltaDevices(devicesList1, devicesList2):
    """
    Return new list containing devices from devices list 1 that do not exist in devices list 2
    """
    deltaDevices = []
    for deviceRec in devicesList1:
        if deviceRec not in devicesList2:
            deltaDevices.append(deviceRec)
    return deltaDevices


def InsertDevices(accessToken: str, env: str, deviceRecords: dict, config: object):
    """
    Insert new devices from csv file to portal
    """
    #accessToken = SendRequestLogin(config)
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success.')
    for deviceRecord in deviceRecords:
        print('Inserting device: {}'.format(deviceRecord))
        InsertDevice(accessToken, deviceRecord, config)


if __name__ == "__main__":
    from activate_env import *
    ActivateEnv()

    import sys
    import requests
    import json
    import configparser
    import traceback
    from utils import *
    from sql import *
    from record import *

    print('-----insert_devices-----')
    print('Arguments: {}'.format(sys.argv))

    try:
        if len(sys.argv) != 2:
            print('Enter 1 arguments: config file')
            exit(1)

        configFile = sys.argv[1]
        print('arg 1: env: {}'.format(configFile))

        if not os.path.exists(configFile):
            print('Config file {} not exist'.format(configFile))
            exit(2)

        config = LoadConfigText(configFile)

        devicesCsvFile = config['DEVICES_PATH']
        env = config['ENV']

        print('Devices csv file: {}'.format(devicesCsvFile))
        print('Env: {}'.format(env))

        portalDeviceRecords, accessToken = RetrieveDevicesFromPortal(config)
        print('Devices in portal: {}'.format(len(portalDeviceRecords)))

        csvDeviceRecords = ReadRecordsFromCsvFile(devicesCsvFile)
        newDevices = GetDeltaDevices(csvDeviceRecords, portalDeviceRecords)

        print('New devices:')
        for deltaDevice in newDevices:
            print(deltaDevice)

        InsertDevices(accessToken, env, newDevices, config)

        print('-----success-----')

    except Exception as ex:
        print('Error: {}'.format(ex))
        traceback.print_exc()
        print('-----fail-----')
        exit(1)
