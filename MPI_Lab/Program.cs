using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MPI;

static class ArrayMethods
{
    public static int[] MultiplyRowByMatrix(int[] row, int[,] matrixB)
    {
        int[] result = new int[4];

        // Умножаем строку на матрицу Б
        for (int i = 0; i < matrixB.GetLength(1); i++)
        {
            int sum = 0;
            for (int j = 0; j < matrixB.GetLength(0); j++)
            {
                sum += row[j] * matrixB[j, i];
            }
            result[i] = sum;
        }

        return result;
    }

    public static int[][] SynchronousMatrixSolve(int[,] matrixA, int[,] matrixB)
    {
        int[][] result = new int[3][];

        for (int i = 0; i < matrixA.GetLength(0); i++)
        {
            result[i] = MultiplyRowByMatrix(matrixA.GetRow(i), matrixB);
        }

        return result;
    }
}

static class ArrayExtensions
{
    public static int[] GetRow(this int[,] matrix, int rowNumber)
    {
        return Enumerable.Range(0, matrix.GetLength(1))
                .Select(x => matrix[rowNumber, x])
                .ToArray();
    }
}

[Serializable]
class Payload
{
    public int[] MatrixARow { get; set; }

    public int[,] MatrixB { get; set; }
}

class MPILab
{
    static void Main(string[] args)
    {
        MPI.Environment.Run(ref args, comm =>
        {
            // Если выделено меньше 4 потоков, то выполнится без MPI
            if (comm.Size < 4)
            {                
                Stopwatch sw = Stopwatch.StartNew();

                Random rng = new Random();

                for(int i = 0; i < 1_000_000; i++)
                {
                    int[,] matrixA =
                    {
                        { rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9) }
                    };

                    int[,] matrixB =
                    {
                        { rng.Next(9), rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9), rng.Next(9) }
                    };

                    var result = ArrayMethods.SynchronousMatrixSolve(matrixA, matrixB);

                    //for(int j = 0; j < 3; j++)
                    //{
                    //    Console.WriteLine($"{result[j][0]} {result[j][1]} {result[j][2]} {result[j][3]}");
                    //}

                }

                sw.Stop();

                Console.WriteLine($"Прошло времени: {sw.ElapsedMilliseconds/1000.00} c");
                Console.WriteLine($"Времени на операцию: {sw.ElapsedMilliseconds / 1_000_000.00} мс");
            }
            // Работа главного потока
            else if (comm.Rank == 0)
            {
                Random rng = new Random();

                Stopwatch sw = Stopwatch.StartNew();

                const double iterations = 1_000.00;

                int[][] resultMatrix = new int[3][];

                for (int currentIteration = 0; currentIteration < iterations; currentIteration++)
                {
                    int[,] matrixA =
                    {
                        { rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9) }
                    };

                    int[,] matrixB =
                    {
                        { rng.Next(9), rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9), rng.Next(9) },
                        { rng.Next(9), rng.Next(9), rng.Next(9), rng.Next(9) }
                    };


                    // Отсылаем строки потокам
                    for (int i = 0; i < matrixA.GetLength(0); i++)
                    {
                        comm.Send<Payload>(new Payload { MatrixARow = matrixA.GetRow(i), MatrixB = matrixB}, i + 1, i + 1);
                    }

                    // Принимаем результат умножения
                    for (int i = 0; i < matrixA.GetLength(0); i++)
                    {
                        comm.Receive(i + 1, i + 1, out resultMatrix[i]);
                    }

                }

                sw.Stop();

                Console.WriteLine($"Прошло времени: {sw.ElapsedMilliseconds/1000.00} с");
                Console.WriteLine($"Время на операцию: {sw.ElapsedMilliseconds/iterations} мс");
            }
            else
            {
                for (int currentIteration = 0; currentIteration < 1000; currentIteration++)
                {
                    // Получаем строку
                    comm.Receive<Payload>(0, comm.Rank, out Payload payload);

                    var result = ArrayMethods.MultiplyRowByMatrix(payload.MatrixARow, payload.MatrixB);

                    //Console.WriteLine($"Результат умножения {comm.Rank} строки матрицы А на матрицу Б на процессе #{comm.Rank}: {result[0]} {result[1]} {result[2]} {result[3]}");

                    // Отсылаем результат в главный процесс
                    comm.Send<int[]>(result, 0, comm.Rank);
                }
            }
        });
    }
}
