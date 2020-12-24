

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


def CollectDevicesFromPortal(config: object):
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
            print('Connected to RDS.')
            deviceEntries = GetDeviceEntries(dbConn)
            deviceRecords = [ConvertEntryToDeviceRecord(
                entry) for entry in deviceEntries]
        else:
            print('Error: Cannot connect to RDS.')

    return deviceRecords



def CollectDevicesFromPortalByAPI(config: object, limit=0, page=0, searchQuery=''):
    """
    Collect devices list from the portal by API and return list of their records {GA, SN}
    """
    accessToken = SendRequestLogin(config)
    
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success. ')

    response = None

    if limit == 0 and page == 0:
        host = config['API_GET_DEVICES_URL'] + '?limit=1&page=0&search={}'.format(searchQuery)
        
        response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)})

        totalResults = None
        
        if response.ok:
            totalResults = response.json()['metadata']['page']['totalResults']
            print("total: {}".format(totalResults))
            host = config['API_GET_DEVICES_URL'] + '?limit={}&page=0&search={}'.format(totalResults, searchQuery)

            print('host: {}'.format(host))
            response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)})
    else:
        host = config['API_GET_DEVICES_URL'] + '?limit={0}&page={1}&search={2}'.format(limit, page, searchQuery)
        print('host: {}'.format(host))

        response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
        accessToken)})

    if response and response.ok:
        print('Devices were retrieved successfully.')
    else:
        print('Error: cannot retrieve the devices. status code: {}. {}'.format(response.status_code, response.text))

    devices = []
    for device_json in response.json()['data']:
        device = device_json['deviceInfo']
        devices.append({'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})

    return devices




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


def InsertDevices(env: str, deviceRecords: dict, config: object):
    """
    Insert new devices from csv file to portal
    """
    accessToken = SendRequestLogin(config)
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
    from utils import *
    from sql import *
    from record import *
    from delete_device import DeleteDevices

    print('-----insert_devices-----')
    print('Arguments: {}'.format(sys.argv))

    try:
        if len(sys.argv) != 3:
            print('Enter 2 arguments: config file and strategy')
            exit(1)

        configFile = sys.argv[1]
        print('arg 1: env: {}'.format(configFile))

        if not os.path.exists(configFile):
            print('Config file {} not exist'.format(configFile))
            exit(2)

        strategy = int(sys.argv[2]) if sys.argv[2] else 3

        config = LoadConfigText(configFile)

        devicesCsvFile = config['DEVICES_PATH']
        env = config['ENV']

        print('Devices csv file: {}'.format(devicesCsvFile))
        print('Env: {}'.format(env))

        portalDeviceRecords = CollectDevicesFromPortalByAPI(config)

        #print('Devices: {}'.format(portalDeviceRecords))

        csvDeviceRecords = ReadRecordsFromCsvFile(devicesCsvFile)      

        if strategy == 1:
            # insert missing from csv
            # delete not needed devices on aws

            deltaDevicesCsv = GetDeltaDevices(csvDeviceRecords, portalDeviceRecords)
            print('Delta Devices CSV: {}'.format(deltaDevicesCsv))
            
            deltaDevicesAws = GetDeltaDevices(portalDeviceRecords, csvDeviceRecords)
            print('Delta Devices AWS: {}'.format(deltaDevicesAws))

            InsertDevices(env, deltaDevicesCsv, config)
            DeleteDevices(deltaDevicesAws, config) 
            #DeleteDevices(csvDeviceRecords, config)

            pass
        elif strategy == 2:
            # delete all from aws
            # insert all from csv

            #DeleteDevices(portalDeviceRecords, config)
            #InsertDevices(env, csvDeviceRecords, config)

            pass
        elif strategy == 3:
            # insert missing from csv
 
            deltaDevicesCsv = GetDeltaDevices(csvDeviceRecords, portalDeviceRecords)
            print('Delta Devices CSV: {}'.format(deltaDevicesCsv))
 
            InsertDevices(env, deltaDevicesCsv, config)

        print('-----success-----')

    except Exception as ex:
        print('Error: {}'.format(ex))
        print('-----fail-----')
        exit(1)
