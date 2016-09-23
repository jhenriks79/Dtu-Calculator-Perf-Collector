# Azure SQL Database DTU Calculator Perf Collector

This perf collector will capture resource utilization for a database server and create a CSV file to be uploaded to the [Azure SQL Database DTU Calculator](http://dtucalculator.azurewebsites.net/). To assist in capturing the correct performance metrics, download and run this project to capture your database utilization. The project is configured to capture the below performance counters for a one hour period.

* Processor - % Processor Time
* Logical Disk - Disk Reads/sec
* Logical Disk - Disk Writes/sec
* Database - Log Bytes Flushed/sec