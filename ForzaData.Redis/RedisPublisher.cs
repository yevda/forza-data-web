using ForzaData.Core;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ForzaData.Redis;

public class RedisPublisher(
	ILogger<RedisPublisher> logger,
	IDatabase db) : ForzaDataObserver
{
	public override void OnCompleted()
	{
		logger.LogInformation("Data transmission completed");
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
		var record = new
		{
			engineRpm = (int)value.Sled.CurrentEngineRpm,
			truckModel = value.Sled.CarOrdinal,
			speed = (int)value.Sled.VelocityX,
			brakeTemperature = (int)GetWheelTemperature(value),
			userThrottle = (int)value.Sled.AccelerationX,
			userBrake = value.CarDash?.Brake ?? 0,
			userSteer = value.CarDash?.Steer ?? 0,
			trailerMass = 0,
			truckOdometer = (int)(value.CarDash?.DistanceTraveled ?? 0)
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
		
		db.StringSet("engineRpm", ((int)value.Sled.CurrentEngineRpm).ToString());
		db.StringSet("truckModel", value.Sled.CarOrdinal.ToString());
		db.StringSet("speed", ((int)value.Sled.VelocityX).ToString());
		db.StringSet("brakeTemperature", ((int)GetWheelTemperature(value)).ToString());
		db.StringSet("userThrottle", ((int)value.Sled.AccelerationX).ToString());
		db.StringSet("userBrake", (value.CarDash?.Brake ?? 0).ToString());
		db.StringSet("userSteer", (value.CarDash?.Steer ?? 0).ToString());
		db.StringSet("trailerMass", "0");
		db.StringSet("truckOdometer", ((int)(value.CarDash?.DistanceTraveled ?? 0)).ToString());
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