import mysql.connector as mysql

"""
MySQL Database Connector
"""


class DbConnector:
    def __init__(self, host: str, db: str, user: str, password: str):
        """
        Creates new MySQL connection object\n
        Arguments: hostname, database name, username, password
        """
        self.host = host
        self.db = db
        self.user = user
        self.password = password

        try:
            self.conn = mysql.connect(
                host=host, user=user, password=password, database=db)
        except Exception as ex:
            print(ex)
            self.conn = None

    def Close(self):
        """
        Closes connection
        """
        if self.conn:
            self.conn.close()

    def __str__(self):
        return ','.join([self.host, self.user, self.db])

    def GetDatabses(self):
        """
        Returns databases list
        """
        if self.conn:
            cursor = self.conn.cursor()
            cursor.execute("SHOW DATABASES")
            databases = cursor.fetchall()
            return databases
        else:
            return None

    def GetColumns(self, table: str):
        """
        Returns column names of a given table
        """
        if self.conn:
            cursor = self.conn.cursor()
            cursor.execute("SHOW columns FROM {}".format(table))
            columnElements = cursor.fetchall()
            return [columnElement[0] for columnElement in columnElements]
        else:
            return None

    def ExecuteQuery(self, table: str, query: str):
        """
        Returns results set as list of record dictionaries from a given query
        """
        if self.conn:
            columns = self.GetColumns(table)
            cursor = self.conn.cursor()
            cursor.execute(query)
            records = cursor.fetchall()
            recordDictionaries = []
            for record in records:
                recordDict = {columns[i]: record[i]
                              for i in range(len(columns))}
                recordDictionaries.append(recordDict)
            return recordDictionaries
        else:
            return []
