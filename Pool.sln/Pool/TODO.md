# TODO: implement PoolAsync

# TODO: PoolStats
    // TODO: Add PoolStats history to allow transitioning from a larger stats period and mantain the history
    // TODO: Consider having history for the last minutes in seconds ganularity, last hour in minutes, 
    // last day in hours, month in days, years in months
    // total size for one year: 
    // (12 + 31 + 24 + 60 + 60) x (12 x ( 8 bytes (DateTime) + 4 bytes (int)) 
    //      = 187 x 12 x 12 = 2244 objects x 12 bytes = 26.928 bytes

- [x] object must have current determinant, minute, hour, day, month, year

allTimeStats = new Dictionary<int, PoolStats>(); // where key is the year
yearStats = new PoolStats();
monthStats = new PoolStats();
dayStats = new PoolStats();
hourStats = new PoolStats();
minuteStats = new PoolStats();

## Strategy:
7. If new minute, rotate minute stats
1. update minute stats 
7. If new hour, rotate minute stats
2. update hour stats
7. If new day, rotate minute stats
3. update day stats
7. If new month, rotate minute stats
4. update month stats
7. If new year, rotate minute stats
5. update year stats
6. update allTime stats

- [ ] Update RabbitMQ Clients

https://www.nuget.org/packages/rabbitmq.client/
6.8.1
7.1.2