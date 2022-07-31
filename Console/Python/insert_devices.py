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
    devices = []

    if limit == 0 and page == 0:
        host = config['API_GET_DEVICES_URL'] + \
            '?limit=1&page=0&search={}'.format(searchQuery)

        response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)})

        totalResults = None

        if response.ok:
            totalResults = response.json()['metadata']['page']['totalResults']
            print("total: {}".format(totalResults))

            if totalResults < 500:
                host = config['API_GET_DEVICES_URL'] + \
                    '?limit={}&page=0&search={}'.format(totalResults, searchQuery)

                print('host: {}'.format(host))
                response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
                    accessToken)})

                for device_json in response.json()['data']:
                    device = device_json['deviceInfo']
                    devices.append(
                        {'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})
            else:
                index = 0
                while totalResults > 0:
                    host = config['API_GET_DEVICES_URL'] + \
                        '?limit={}&page={}&search={}'.format(500, index, searchQuery)

                    print('host: {}'.format(host))
                    response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
                        accessToken)})

                    for device_json in response.json()['data']:
                        device = device_json['deviceInfo']
                        devices.append(
                            {'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})

                    index = index + 1
                    totalResults = totalResults - 500
    else:
        host = config['API_GET_DEVICES_URL'] + \
            '?limit={0}&page={1}&search={2}'.format(limit, page, searchQuery)
        print('host: {}'.format(host))

        response = requests.get(url=host, headers={'Content-Type': 'application/json', 'Authorization': 'Bearer {}'.format(
            accessToken)})

        for device_json in response.json()['data']:
            device = device_json['deviceInfo']
            devices.append(
                {'deviceType': device['deviceType'], 'deviceSerialNumber': device['deviceSerialNumber']})

    if response and response.ok:
        print('Devices were retrieved successfully.')
    else:
        print('Error: cannot retrieve the devices. status code: {}. {}'.format(
            response.status_code, response.text))
    

    print('Number of devices collected: {}'.format(len(devices)))

    return devices, accessToken


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

    from joblib import Parallel, delayed

    #accessToken = SendRequestLogin(config)
    rdsSim = True if config['RDS_SIMUL'] == 'True' else False
    if rdsSim:
        print('Simulating RDS.')
        # return
    if not accessToken:
        print('Error: Login failed.')
        return
    print('Login success.')
    Parallel(n_jobs=10)(delayed(InsertDevice)(accessToken, deviceRecord, config) for deviceRecord in deviceRecords)
    
    #for deviceRecord in deviceRecords:
    #    print('Inserting device: {}'.format(deviceRecord))
    #    InsertDevice(accessToken, deviceRecord, config)


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

        strategy = sys.argv[2] if sys.argv[2] else 'union'
        print('Strategy: ' + strategy)

        config = LoadConfigText(configFile)

        devicesCsvFile = config['DEVICES_PATH']
        env = config['ENV']

        print('Devices csv file: {}'.format(devicesCsvFile))
        print('Env: {}'.format(env))

        #print('Devices: {}'.format(portalDeviceRecords))

        csvDeviceRecords = ReadRecordsFromCsvFile(devicesCsvFile)

        if strategy == 'intersect':
            # insert missing from csv
            # delete not needed devices on aws

            portalDeviceRecords, accessToken = CollectDevicesFromPortalByAPI(
            config)

            deltaDevicesCsv = GetDeltaDevices(
                csvDeviceRecords, portalDeviceRecords)
            print('Delta Devices CSV: {}'.format(deltaDevicesCsv))

            deltaDevicesAws = GetDeltaDevices(
                portalDeviceRecords, csvDeviceRecords)
            print('Delta Devices AWS: {}'.format(deltaDevicesAws))

            InsertDevices(accessToken, env, deltaDevicesCsv, config)
            DeleteDevices(accessToken, deltaDevicesAws, config)
            DeleteDevices(accessToken, csvDeviceRecords, config)

            pass
        elif strategy == 'all_new':
            # delete all from aws
            # insert all from csv

            portalDeviceRecords, accessToken = CollectDevicesFromPortalByAPI(
            config)

            DeleteDevices(accessToken, portalDeviceRecords, config)
            InsertDevices(accessToken, env, csvDeviceRecords, config)

            pass
        elif strategy == 'union':
            # insert missing from csv

            portalDeviceRecords = []
            accessToken = SendRequestLogin(config)

            deltaDevicesCsv = GetDeltaDevices(
                csvDeviceRecords, portalDeviceRecords)
            print('Delta Devices CSV: {}'.format(deltaDevicesCsv))

            InsertDevices(accessToken, env, deltaDevicesCsv, config)

        # Insert devices by Elad
        # portalDeviceRecords, accessToken = RetrieveDevicesFromPortal(
        #    config, 300)
        #print('Devices in portal: {}'.format(len(portalDeviceRecords)))
        #
        #csvDeviceRecords = ReadRecordsFromCsvFile(devicesCsvFile)
        #
        #newDevices = GetDeltaDevices(csvDeviceRecords, portalDeviceRecords)
        #
        #print('New devices: {}'.format(len(newDevices)))
        #
        #InsertDevices(accessToken, env, newDevices, config)

        print('-----success-----')
        exit(0)

    except Exception as ex:
        print('Error: {}'.format(ex))
        traceback.print_exc()
        print('-----fail-----')
        exit(1)
