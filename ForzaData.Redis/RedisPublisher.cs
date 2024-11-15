using ForzaData.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ForzaData.Redis;

public class RedisPublisher(
	ILogger<RedisPublisher> logger,
	IDatabase db) : ForzaDataObserver
{
	// private const float StandardGravity = 9.80665f; // m/s²
	private float _distance;
	private uint _prevTime = 0;
	
	public override void OnCompleted()
	{
		logger.LogInformation("Data transmission completed");
		_distance = 0;
	}

	public override void OnError(Exception error)
	{
		logger.LogError(error, "An error occurred");
	}

	public override void OnError(ForzaDataException error)
	{
		logger.LogError(error, "A Forza Data error occurred");
	}

	public override void OnNext(ForzaDataStruct value)
	{
		var sd = value.Sled;
		ForzaCarDashDataStruct cdd = value.CarDash ?? default;
		if (sd.IsRaceOn == 0)
		{
			logger.LogInformation("Race is off...");
			return;
		}

		if (_prevTime == 0)
		{
			_prevTime = sd.TimestampMS;
		}
		
		var curTime = sd.TimestampMS;
		if (_prevTime != curTime)
		{
			_distance += (int)(cdd.Speed * (curTime - _prevTime) / 1000f) / 1000f;
			_prevTime = curTime;
		}
		
		var record = new
		{
			engineRpm = (int)sd.CurrentEngineRpm,
			truckModel = sd.CarOrdinal,
			speed = (int)(cdd.Speed * 3.6f),
			brakeTemperature = (int)GetWheelTemperature(value),
			userThrottle = (int)(cdd.Accel / 2.55f),
			userBrake = (int)(cdd.Brake / 2.55f),
			userSteer = (int)(cdd.Steer / 1.27f),
			trailerMass = 1750,
			truckOdometer = _distance.ToString("000000.00")
		};
		
		logger.LogInformation(
			"Publishing data to Redis:" +
			" RPM: {EngineRPM}" +
			" | Vehicle #: {TruckModel}" +
			" | Speed: {Speed}" +
			" | Brake Temperature: {BrakeTemperature}" +
			" | User Throttle: {UserThrottle}" +
			" | User Brake: {UserBrake}" +
			" | User Steer: {UserSteer}" +
			" | Trailer Mass: {TrailerMass}" +
			" | Truck Odometer: {TruckOdometer}",
			record.engineRpm,
			record.truckModel,
			record.speed,
			record.brakeTemperature,
			record.userThrottle,
			record.userBrake,
			record.userSteer,
			record.trailerMass,
			record.truckOdometer);
		
		db.StringSet("engineRpm", record.engineRpm.ToString());
		db.StringSet("truckModel", record.truckModel.ToString());
		db.StringSet("speed", record.speed.ToString());
		db.StringSet("brakeTemperature", record.brakeTemperature.ToString());
		db.StringSet("userThrottle", record.userThrottle.ToString());
		db.StringSet("userBrake", record.userBrake.ToString());
		db.StringSet("userSteer", record.userSteer.ToString());
		db.StringSet("trailerMass", record.trailerMass.ToString());
		db.StringSet("truckOdometer", record.truckOdometer.ToString());
	}

	private static float GetWheelTemperature(ForzaDataStruct value)
	{
		if (!value.CarDash.HasValue)
		{
			return 0;
		}
		var carDash = value.CarDash.Value;
		float[] wheelTemps = [carDash.TireTempFrontLeft, carDash.TireTempFrontRight, carDash.TireTempRearLeft, carDash.TireTempRearRight];
		var avg = wheelTemps.Average();
		return avg;
	}
}