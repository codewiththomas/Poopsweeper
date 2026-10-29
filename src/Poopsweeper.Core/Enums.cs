namespace Poopsweeper.Core;

public enum CellKind { Void, Obstacle, Meadow }

public enum ObstacleType { None, Tree, Bush, Rock, Bench }

public enum CellState { Hidden, Revealed }

public enum SpecialKind { None, Puddle, Sprinkler }

public enum GameState { Ready, Playing, Won, Lost }
