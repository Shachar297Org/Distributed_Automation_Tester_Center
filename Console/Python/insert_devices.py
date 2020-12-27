def RetrieveDevicesFromPortal(config: object, limit: int):
    """
    Retrieve device list from portal and return list of their records {GA, SN}
    """
    rdsSim = True if config['RDS_SIMUL'] == 'True' else False
    if rdsSim:
        return [], None

    accessToken = SendRequestLogin(config)
    if not accessToken:
        print('Error: Login failed.')
        raise Exception('Cannot login portal')
    print('Login success.')

    getDeviceHost = '?limit='.join([config['API_SEARCH_DEVICE'], str(limit)])
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
        deviceName1 = '_'.join(
            [deviceRec['deviceSerialNumber'], deviceRec['deviceType']])
        deviceNames = [
            '_'.join([d2['deviceSerialNumber'], d2['deviceType']]) for d2 in devicesList2]
        if deviceName1 not in deviceNames:
            deltaDevices.append(deviceRec)
    return deltaDevices


def InsertDevices(accessToken: str, env: str, deviceRecords: dict, config: object):
    """
    Insert new devices from csv file to portal
    """
    #accessToken = SendRequestLogin(config)
    rdsSim = True if config['RDS_SIMUL'] == 'True' else False
    if rdsSim:
        print('Simulating RDS.')
        return
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

        portalDeviceRecords, accessToken = RetrieveDevicesFromPortal(
            config, 300)
        print('Devices in portal: {}'.format(len(portalDeviceRecords)))

        csvDeviceRecords = ReadRecordsFromCsvFile(devicesCsvFile)

        newDevices = GetDeltaDevices(csvDeviceRecords, portalDeviceRecords)

        print('New devices: {}'.format(len(newDevices)))

        InsertDevices(accessToken, env, newDevices, config)

        print('-----success-----')

    except Exception as ex:
        print('Error: {}'.format(ex))
        traceback.print_exc()
        print('-----fail-----')
        exit(1)
